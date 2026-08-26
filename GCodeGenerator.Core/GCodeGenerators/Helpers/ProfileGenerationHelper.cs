#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators.Helpers
{
    /// <summary>
    /// Класс-помощник для генерации G-кода профилей.
    /// Содержит общую логику обработки по слоям и входа в материал.
    /// Пункт 4.4 плана: пишет структурированные блоки через ToolPathBuilder.
    /// </summary>
    public class ProfileGenerationHelper
    {
        /// <summary>
        /// Генерирует цикл обработки по слоям для профилей.
        /// </summary>
        /// <param name="op">Операция профиля</param>
        /// <param name="generateLayer">Делегат для генерации одного слоя (currentZ, nextZ, passNumber)</param>
        /// <param name="builder">Построитель траектории</param>
        /// <param name="settings">Настройки генерации G-кода</param>
        /// <param name="cancellation">Отмена: проверяется перед каждым слоем.</param>
        public void GenerateLayerLoop(
            ProfileOperationBase op,
            Action<double, double, int> generateLayer,
            ToolPathBuilder builder,
            GCodeSettings settings,
            CancellationToken cancellation = default)
        {
            // Пункт 3.8 плана: StepDepth <= 0 не двигает Z вниз — цикл по слоям
            // превращается в бесконечный. Бросаем исключение вместо зависания.
            if (op.StepDepth <= 0)
                throw new ArgumentOutOfRangeException(nameof(op),
                    $"StepDepth must be greater than zero (got {op.StepDepth.ToString(CultureInfo.InvariantCulture)}); otherwise the layer loop would run forever.");

            int decimals = op.Decimals;
            double currentZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;
            int passNumber = 0;

            while (currentZ > finalZ)
            {
                // Слой — единица работы: глубокая операция строится из сотен
                // слоёв, и отмена не должна ждать конца операции.
                cancellation.ThrowIfCancellationRequested();

                double nextZ = currentZ - op.StepDepth;
                if (nextZ < finalZ) nextZ = finalZ;
                passNumber++;

                builder.Comment(ProgramComments.Pass(passNumber, GCodeGenerationHelper.FormatNumber(nextZ, GCodeGenerationHelper.DecimalFormat(decimals))));

                generateLayer(currentZ, nextZ, passNumber);

                if (nextZ > finalZ)
                {
                    var retractZAfterPass = nextZ + op.RetractHeight;
                    builder.RapidTo(z: retractZAfterPass, feed: op.FeedZRapid);
                }

                currentZ = nextZ;
            }

            builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid);
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
        /// <param name="getCornerDistances">Делегат для получения расстояний изломов контура (для рампы)</param>
        /// <param name="builder">Построитель траектории</param>
        /// <param name="settings">Настройки генерации G-кода</param>
        public void GenerateEntry(
            ProfileOperationBase op,
            (double x, double y) startPoint,
            double currentZ,
            double nextZ,
            Func<double, (double x, double y)> getPointOnContour,
            Func<double> getPerimeter,
            Func<IReadOnlyList<double>> getCornerDistances,
            ToolPathBuilder builder,
            GCodeSettings settings)
        {
            int decimals = op.Decimals;

            builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid);
            builder.RapidTo(x: startPoint.x, y: startPoint.y, feed: op.FeedXYRapid);

            if (op.EntryMode == EntryMode.Vertical)
            {
                builder.RapidTo(z: currentZ, feed: op.FeedZRapid);
                builder.LinearTo(z: nextZ, feed: op.FeedZWork);
            }
            else
            {
                GenerateRampEntry(op, startPoint, currentZ, nextZ, getPointOnContour, getPerimeter, getCornerDistances, builder, decimals);
            }
        }

        /// <summary>
        /// Наибольшее число витков рампы. Малый угол врезания на длинной
        /// глубине даёт сотни витков; выше этого предела остаток глубины
        /// проходится одним витком, круче заданного угла, — так же, как
        /// поступала прежняя реализация с любой рампой длиннее периметра.
        /// </summary>
        private const int MaxRampLaps = 20;

        /// <summary>
        /// Генерирует вход по рампе: спуск вдоль контура под заданным углом.
        ///
        /// За один оборот по контуру рампа опускается на «периметр × тангенс
        /// угла». Если слой глубже, спуск идёт несколькими витками, между
        /// которыми инструмент отводится над материалом на безопасное
        /// расстояние между проходами и возвращается к началу контура.
        /// Прежде рампа всегда укладывалась в один оборот: заданный угол
        /// молча становился круче ровно настолько, насколько не хватало
        /// длины контура.
        /// </summary>
        private void GenerateRampEntry(
            ProfileOperationBase op,
            (double x, double y) startPoint,
            double currentZ,
            double nextZ,
            Func<double, (double x, double y)> getPointOnContour,
            Func<double> getPerimeter,
            Func<IReadOnlyList<double>> getCornerDistances,
            ToolPathBuilder builder,
            int decimals)
        {
            var entryAngleRad = op.EntryAngle * Math.PI / 180.0;
            var retractZ = currentZ + op.RetractHeight;

            builder.RapidTo(z: retractZ, feed: op.FeedZRapid);

            var totalDepth = retractZ - nextZ;
            var tangent = Math.Tan(entryAngleRad);
            var totalDistance = tangent > 0 ? totalDepth / tangent : 0.0;

            // Периметр задаёт длину одного витка; без него остаётся прежняя
            // оценка «рампа укладывается в половину оборота».
            var perimeter = getPerimeter();
            if (perimeter <= 0) perimeter = totalDistance * 2;

            var depthPerLap = perimeter * tangent;
            var laps = depthPerLap > 0 ? (int)Math.Ceiling(totalDepth / depthPerLap) : 1;
            laps = Math.Max(1, Math.Min(laps, MaxRampLaps));

            var lapDepth = totalDepth / laps;
            var lapDistance = totalDistance / laps;
            var corners = getCornerDistances();
            var zFrom = retractZ;

            for (int lap = 1; lap <= laps; lap++)
            {
                var zTo = lap == laps ? nextZ : zFrom - lapDepth;
                EmitRampLap(op, zFrom, zTo, lapDistance, perimeter, getPointOnContour, corners, builder, decimals);
                zFrom = zTo;

                // Между витками инструмент уходит от материала и возвращается
                // к началу контура, чтобы следующий виток начался оттуда же.
                if (lap < laps)
                    ReturnToStart(op, startPoint, zTo, builder, decimals);
            }

            ReturnToStart(op, startPoint, nextZ, builder, decimals);
        }

        /// <summary>Один виток рампы: спуск от <paramref name="zFrom"/> к <paramref name="zTo"/> вдоль контура.</summary>
        private static void EmitRampLap(
            ProfileOperationBase op,
            double zFrom,
            double zTo,
            double distance,
            double perimeter,
            Func<double, (double x, double y)> getPointOnContour,
            IReadOnlyList<double> cornerDistances,
            ToolPathBuilder builder,
            int decimals)
        {
            var depth = zFrom - zTo;

            // Число отрезков — по доле контура, которую проходит виток
            // (прежняя формула: шаг около 11 градусов оборота).
            var lapFraction = distance / Math.Max(1e-6, perimeter);
            var lapAngle = Math.Min(Math.Abs(lapFraction), 1.0) * 2 * Math.PI;
            var segments = Math.Max(4, (int)(lapAngle / (Math.PI / 16)));

            foreach (var (s, t) in RampStops(distance, segments, cornerDistances))
            {
                var point = getPointOnContour(s);
                var z = zFrom - t * depth;
                builder.LinearTo(x: point.x, y: point.y, z: z, feed: op.FeedXYWork);
            }
        }

        /// <summary>
        /// Остановки витка рампы: равномерные сэмплы, объединённые с изломами
        /// контура. Сэмплы задают крутизну по гладким участкам, а изломы
        /// рампа обязана пройти точно: сэмпл почти никогда не попадает
        /// в вершину, и хорда между соседними сэмплами срезала бы угол —
        /// зарез детали, который не исправить следующим проходом.
        /// Каждая остановка — пара «расстояние вдоль контура, доля глубины»:
        /// глубина распределяется по пройденному пути, поэтому угол врезания
        /// остаётся заданным и между изломами.
        /// </summary>
        /// <param name="distance">Длина витка вдоль контура.</param>
        /// <param name="segments">Число равномерных сэмплов витка.</param>
        /// <param name="cornerDistances">Расстояния изломов контура от его начала.</param>
        private static IEnumerable<(double Distance, double DepthFraction)> RampStops(
            double distance,
            int segments,
            IReadOnlyList<double> cornerDistances)
        {
            var stops = new List<(double Distance, double DepthFraction)>(segments + cornerDistances.Count);
            for (int i = 1; i <= segments; i++)
                stops.Add((distance * i / segments, (double)i / segments));

            // Вырожденный виток (угол врезания 90°) не движется по контуру:
            // изломов на нулевом пути нет, остаются прежние ступени по глубине.
            if (distance > 0)
            {
                foreach (var corner in cornerDistances)
                {
                    if (corner > 0 && corner < distance)
                        stops.Add((corner, corner / distance));
                }
            }

            stops.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            // Совпавшие остановки (сэмпл попал в вершину) не повторяются:
            // повторная точка дала бы кадр нулевой длины.
            var previous = double.NegativeInfinity;
            foreach (var stop in stops)
            {
                if (stop.Distance - previous < GeometryTolerances.Degenerate && distance > 0)
                    continue;

                previous = stop.Distance;
                yield return stop;
            }
        }

        /// <summary>
        /// Отводит инструмент от материала и возвращает его к началу контура
        /// на глубину <paramref name="z"/>.
        ///
        /// Высота отвода — безопасное расстояние между проходами над текущей
        /// глубиной. Пока параметр не задан (ноль в старых проектах),
        /// используется безопасная высота, как было до его появления.
        /// </summary>
        private static void ReturnToStart(
            ProfileOperationBase op,
            (double x, double y) startPoint,
            double z,
            ToolPathBuilder builder,
            int decimals)
        {
            var retractZ = op.SafeDistanceBetweenPasses > 0
                ? z + op.SafeDistanceBetweenPasses
                : op.SafeZHeight;

            builder.RapidTo(z: retractZ, feed: op.FeedZRapid);
            builder.RapidTo(x: startPoint.x, y: startPoint.y, feed: op.FeedXYRapid);
            builder.RapidTo(z: z, feed: op.FeedZRapid);
        }
    }
}
