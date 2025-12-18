using System;
using System.Globalization;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Helpers
{
    /// <summary>
    /// Класс-помощник для генерации G-кода карманов.
    /// Содержит общую логику обработки черновой и чистовой обработки.
    /// </summary>
    public class PocketGenerationHelper
    {
        /// <summary>
        /// Обрабатывает логику черновой и чистовой обработки кармана.
        /// </summary>
        /// <typeparam name="T">Тип операции кармана</typeparam>
        /// <param name="operation">Операция кармана</param>
        /// <param name="generateInternal">Делегат для генерации внутренней обработки</param>
        /// <param name="generateWallsFinishing">Делегат для генерации чистовой обработки стенок</param>
        /// <param name="cloneOperation">Делегат для клонирования операции</param>
        /// <param name="applyRoughingAllowance">Делегат для применения припуска при черновой обработке</param>
        /// <param name="isOperationTooSmall">Делегат для проверки, не стал ли карман слишком маленьким</param>
        /// <param name="applyBottomFinishingAllowance">Делегат для применения припуска при чистовой обработке дна</param>
        /// <param name="addLine">Делегат для добавления строки G-кода</param>
        /// <param name="g0">Команда быстрого перемещения (обычно G0)</param>
        /// <param name="g1">Команда рабочей подачи (обычно G1)</param>
        /// <param name="settings">Настройки генерации G-кода</param>
        public void ProcessRoughingFinishing<T>(
            T operation,
            Action<T> generateInternal,
            Action<T, double> generateWallsFinishing,
            Func<T, T> cloneOperation,
            Action<T, double> applyRoughingAllowance,
            Func<T, bool> isOperationTooSmall,
            Action<T, double> applyBottomFinishingAllowance,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
            where T : IPocketOperation
        {
            bool roughing = operation.IsRoughingEnabled;
            bool finishing = operation.IsFinishingEnabled;
            double allowance = Math.Max(0.0, operation.FinishAllowance);

            // Если оба выключены – обрабатываем как раньше, без припуска
            if (!roughing && !finishing)
            {
                roughing = true;
                allowance = 0.0;
            }

            // Черновая обработка
            if (roughing)
            {
                var roughOp = cloneOperation(operation);
                double depthAllowance = Math.Min(allowance, Math.Max(0.0, roughOp.TotalDepth - 1e-6));

                if (depthAllowance > 0)
                {
                    applyRoughingAllowance(roughOp, depthAllowance);

                    if (isOperationTooSmall(roughOp))
                    {
                        if (settings.UseComments)
                            addLine("(Pocket too small after roughing allowance, skipping)");
                        return;
                    }
                }

                generateInternal(roughOp);
            }

            // Чистовая обработка
            if (finishing && allowance > 0)
            {
                double depthAllowance = Math.Min(allowance, Math.Max(0.0, operation.TotalDepth));
                if (depthAllowance < 1e-6)
                    return;

                // Базовая чистовая операция по глубине: работаем только в слое припуска
                var baseFinishOp = cloneOperation(operation);
                baseFinishOp.ContourHeight = operation.ContourHeight - (operation.TotalDepth - depthAllowance);
                baseFinishOp.TotalDepth = depthAllowance;
                baseFinishOp.IsRoughingEnabled = false;
                baseFinishOp.IsFinishingEnabled = false;
                baseFinishOp.FinishAllowance = allowance;

                switch (operation.FinishingMode)
                {
                    case PocketFinishingMode.Walls:
                        generateWallsFinishing(baseFinishOp, allowance);
                        break;

                    case PocketFinishingMode.Bottom:
                        {
                            var bottomOp = cloneOperation(baseFinishOp);
                            applyBottomFinishingAllowance(bottomOp, allowance);
                            if (!isOperationTooSmall(bottomOp))
                                generateInternal(bottomOp);
                        }
                        break;

                    case PocketFinishingMode.All:
                    default:
                        {
                            var bottomOp = cloneOperation(baseFinishOp);
                            applyBottomFinishingAllowance(bottomOp, allowance);
                            if (!isOperationTooSmall(bottomOp))
                                generateInternal(bottomOp);

                            generateWallsFinishing(baseFinishOp, allowance);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Генерирует цикл обработки по слоям.
        /// </summary>
        /// <param name="op">Операция кармана</param>
        /// <param name="generateLayer">Делегат для генерации одного слоя (currentZ, nextZ, passNumber)</param>
        /// <param name="addLine">Делегат для добавления строки G-кода</param>
        /// <param name="g0">Команда быстрого перемещения</param>
        /// <param name="g1">Команда рабочей подачи</param>
        /// <param name="settings">Настройки генерации G-кода</param>
        public void GenerateLayerLoop(
            IPocketOperation op,
            Action<double, double, int> generateLayer,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            double currentZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;
            int pass = 0;

            while (currentZ > finalZ)
            {
                double nextZ = currentZ - op.StepDepth;
                if (nextZ < finalZ) nextZ = finalZ;
                pass++;

                if (settings.UseComments)
                    addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                generateLayer(currentZ, nextZ, pass);

                currentZ = nextZ;
            }
        }

        /// <summary>
        /// Вычисляет эффективный радиус инструмента с учетом уклона стенок.
        /// </summary>
        /// <param name="op">Операция кармана</param>
        /// <param name="depthFromTop">Глубина от верха (расстояние от начальной высоты до текущей глубины)</param>
        /// <param name="baseToolRadius">Базовый радиус инструмента</param>
        /// <returns>Эффективный радиус инструмента с учетом уклона</returns>
        public double CalculateEffectiveToolRadius(
            IPocketOperation op,
            double depthFromTop,
            double baseToolRadius)
        {
            double offset = GCodeGenerationHelper.CalculateTaperOffset(depthFromTop, op.WallTaperAngleDeg);
            return baseToolRadius + offset;
        }
    }
}

