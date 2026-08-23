using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Helpers
{
    /// <summary>
    /// Класс-помощник для генерации G-кода профилей.
    /// Содержит общую логику обработки по слоям и входа в материал.
    /// </summary>
    public class ProfileGenerationHelper
    {
        /// <summary>
        /// Генерирует цикл обработки по слоям для профилей.
        /// </summary>
        /// <param name="op">Операция профиля</param>
        /// <param name="generateLayer">Делегат для генерации одного слоя (currentZ, nextZ, passNumber)</param>
        /// <param name="addLine">Делегат для добавления строки G-кода</param>
        /// <param name="g0">Команда быстрого перемещения</param>
        /// <param name="g1">Команда рабочей подачи</param>
        /// <param name="settings">Настройки генерации G-кода</param>
        public void GenerateLayerLoop(
            IProfileOperation op,
            Action<double, double, int> generateLayer,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            // Пункт 3.8 плана: StepDepth <= 0 не двигает Z вниз — цикл по слоям
            // превращается в бесконечный. Бросаем исключение вместо зависания.
            if (op.StepDepth <= 0)
                throw new ArgumentOutOfRangeException(nameof(op),
                    $"StepDepth must be greater than zero (got {op.StepDepth.ToString(CultureInfo.InvariantCulture)}); otherwise the layer loop would run forever.");

            var fmt = $"0.{new string('0', op.Decimals)}";

            double currentZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;
            int passNumber = 0;

            while (currentZ > finalZ)
            {
                double nextZ = currentZ - op.StepDepth;
                if (nextZ < finalZ) nextZ = finalZ;
                passNumber++;

                if (settings.UseComments)
                    addLine($"(Pass {passNumber}, depth {GCodeGenerationHelper.FormatNumber(nextZ, fmt)})");

                generateLayer(currentZ, nextZ, passNumber);

                if (nextZ > finalZ)
                {
                    var retractZAfterPass = nextZ + op.RetractHeight;
                    addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(retractZAfterPass, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedZRapid, fmt)}");
                }

                currentZ = nextZ;
            }

            addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(op.SafeZHeight, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedZRapid, fmt)}");
        }

        /// <summary>
        /// Генерирует вход в материал (вертикальный или по рампе).
        /// </summary>
        /// <param name="op">Операция профиля</param>
        /// <param name="startPoint">Начальная точка контура</param>
        /// <param name="currentZ">Текущая высота Z</param>
        /// <param name="nextZ">Следующая высота Z (целевая глубина)</param>
        /// <param name="getPointOnContour">Делегат для получения точки на контуре по расстоянию (для рампы)</param>
        /// <param name="getPerimeter">Делегат для получения периметра контура (для расчета рампы)</param>
        /// <param name="addLine">Делегат для добавления строки G-кода</param>
        /// <param name="g0">Команда быстрого перемещения</param>
        /// <param name="g1">Команда рабочей подачи</param>
        /// <param name="settings">Настройки генерации G-кода</param>
        public void GenerateEntry(
            IProfileOperation op,
            (double x, double y) startPoint,
            double currentZ,
            double nextZ,
            Func<double, (double x, double y)> getPointOnContour,
            Func<double> getPerimeter,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";

            addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(op.SafeZHeight, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedZRapid, fmt)}");
            addLine($"{g0} X{GCodeGenerationHelper.FormatNumber(startPoint.x, fmt)} Y{GCodeGenerationHelper.FormatNumber(startPoint.y, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedXYRapid, fmt)}");

            if (op.EntryMode == EntryMode.Vertical)
            {
                addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(currentZ, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedZRapid, fmt)}");
                addLine($"{g1} Z{GCodeGenerationHelper.FormatNumber(nextZ, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedZWork, fmt)}");
            }
            else
            {
                GenerateRampEntry(op, startPoint, currentZ, nextZ, getPointOnContour, getPerimeter, addLine, g0, g1, fmt);
            }
        }

        /// <summary>
        /// Генерирует вход по рампе.
        /// </summary>
        private void GenerateRampEntry(
            IProfileOperation op,
            (double x, double y) startPoint,
            double currentZ,
            double nextZ,
            Func<double, (double x, double y)> getPointOnContour,
            Func<double> getPerimeter,
            Action<string> addLine,
            string g0,
            string g1,
            string fmt)
        {
            var entryAngleRad = op.EntryAngle * Math.PI / 180.0;
            var retractZ = currentZ + op.RetractHeight;

            addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(retractZ, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedZRapid, fmt)}");

            var rampDepth = retractZ - nextZ;
            var rampDistance = rampDepth / Math.Tan(entryAngleRad);

            // Получаем периметр контура для определения угла рампы
            var perimeter = getPerimeter();
            if (perimeter <= 0) perimeter = rampDistance * 2; // Fallback оценка

            // Вычисляем угол рампы на основе расстояния и периметра
            var angleForRamp = (rampDistance / Math.Max(1e-6, perimeter)) * 2 * Math.PI;

            // Ограничиваем угол рампы, чтобы не превышать один оборот
            angleForRamp = Math.Min(Math.Abs(angleForRamp), 2 * Math.PI) * Math.Sign(angleForRamp);
            if (op.Direction == MillingDirection.Clockwise)
                angleForRamp = -Math.Abs(angleForRamp);
            else
                angleForRamp = Math.Abs(angleForRamp);

            var rampSegments = Math.Max(4, (int)(Math.Abs(angleForRamp) / (Math.PI / 16)));

            for (int i = 1; i <= rampSegments; i++)
            {
                var t = (double)i / rampSegments;
                var distance = rampDistance * t;
                var point = getPointOnContour(distance);
                var z = retractZ - t * rampDepth;

                addLine($"{g1} X{GCodeGenerationHelper.FormatNumber(point.x, fmt)} Y{GCodeGenerationHelper.FormatNumber(point.y, fmt)} Z{GCodeGenerationHelper.FormatNumber(z, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedXYWork, fmt)}");
            }

            addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(op.SafeZHeight, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedZRapid, fmt)}");
            addLine($"{g0} X{GCodeGenerationHelper.FormatNumber(startPoint.x, fmt)} Y{GCodeGenerationHelper.FormatNumber(startPoint.y, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedXYRapid, fmt)}");
            addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(nextZ, fmt)} F{GCodeGenerationHelper.FormatNumber(op.FeedZRapid, fmt)}");
        }

        /// <summary>
        /// Генерирует путь по контуру из списка точек.
        /// </summary>
        /// <param name="points">Точки контура</param>
        /// <param name="direction">Направление фрезерования</param>
        /// <param name="feedXYWork">Скорость рабочей подачи в плоскости XY</param>
        /// <param name="allowArcs">Разрешить использование дуг в G-коде</param>
        /// <param name="settings">Настройки генерации G-кода</param>
        /// <param name="addLine">Делегат для добавления строки G-кода</param>
        /// <param name="g1">Команда рабочей подачи</param>
        /// <param name="decimals">Количество знаков после запятой</param>
        public void GenerateContourPath(
            IEnumerable<(double x, double y)> points,
            MillingDirection direction,
            double feedXYWork,
            bool allowArcs,
            GCodeSettings settings,
            Action<string> addLine,
            string g1,
            int decimals)
        {
            var fmt = $"0.{new string('0', decimals)}";

            var pointsList = direction == MillingDirection.Clockwise
                ? points.Reverse().ToList()
                : points.ToList();

            foreach (var point in pointsList)
            {
                addLine($"{g1} X{GCodeGenerationHelper.FormatNumber(point.x, fmt)} Y{GCodeGenerationHelper.FormatNumber(point.y, fmt)} F{GCodeGenerationHelper.FormatNumber(feedXYWork, fmt)}");
            }
        }

        /// <summary>
        /// Генерирует путь по контуру с поддержкой дуг (если доступны).
        /// </summary>
        /// <param name="points">Точки контура (линейные сегменты)</param>
        /// <param name="arcSegments">Сегменты дуг (если есть)</param>
        /// <param name="direction">Направление фрезерования</param>
        /// <param name="feedXYWork">Скорость рабочей подачи в плоскости XY</param>
        /// <param name="allowArcs">Разрешить использование дуг в G-коде</param>
        /// <param name="settings">Настройки генерации G-кода</param>
        /// <param name="addLine">Делегат для добавления строки G-кода</param>
        /// <param name="g1">Команда рабочей подачи</param>
        /// <param name="decimals">Количество знаков после запятой</param>
        public void GenerateContourPathWithArcs(
            IEnumerable<(double x, double y)> points,
            IEnumerable<(double x, double y, double centerX, double centerY, double radius, bool isClockwise)> arcSegments,
            MillingDirection direction,
            double feedXYWork,
            bool allowArcs,
            GCodeSettings settings,
            Action<string> addLine,
            string g1,
            int decimals)
        {
            var fmt = $"0.{new string('0', decimals)}";

            // Эта реализация будет расширена позже для поддержки дуг
            // Пока используем простую генерацию точек
            GenerateContourPath(points, direction, feedXYWork, allowArcs, settings, addLine, g1, decimals);
        }
    }
}

