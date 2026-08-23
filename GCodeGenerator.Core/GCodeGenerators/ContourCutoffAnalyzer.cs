using System;
using System.Collections.Generic;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Эвристики отсечки слоёв DXF-кармана (пункт 4.6 плана).
    /// Чистый класс без зависимости от геометрии: принимает площади слоёв
    /// и результаты геометрических проверок, ведёт состояние на контур
    /// (площади предыдущих слоёв, данные подобия первых двух слоёв)
    /// и решает, нужно ли пропустить контур в текущем слое.
    ///
    /// Перенесён из UnifiedPocketGenerator.GenerateDxfLayerWithSpiral
    /// без изменения поведения:
    /// 1) изменение площади относительно предыдущего слоя
    ///    (уклон 0 — стоп при увеличении; уклон &gt; 0 — стоп при
    ///    увеличении или равенстве — инверсия/вырождение контура);
    /// 2) смена направления обхода контура;
    /// 3) смена направления хотя бы одного вектора от вершины до центра;
    /// 4) «песочные часы»: оценка номера слоя, где площадь упадёт до 1%
    ///    от исходной (An = A0 * ratio^(n-1), An &lt;= 0.01 * A0), по
    ///    площадям первых двух слоёв.
    /// </summary>
    public sealed class ContourCutoffAnalyzer
    {
        private const double Tolerance = 1e-6;

        private readonly Dictionary<int, double> _previousContourAreas = new Dictionary<int, double>();
        private readonly Dictionary<int, (double firstArea, double secondArea, double ratio, int hourglassLayer)>
            _contourSimilarityData = new Dictionary<int, (double firstArea, double secondArea, double ratio, int hourglassLayer)>();

        /// <summary>
        /// Вызывается, когда смещённый контур слоя получить не удалось
        /// (GetContour вернул null). Поведение legacy: на первых двух
        /// слоях инициализирует данные подобия нулями, если их ещё нет.
        /// </summary>
        public void RecordMissingContour(int contourIndex, int passNumber)
        {
            if (passNumber <= 2 && !_contourSimilarityData.ContainsKey(contourIndex))
            {
                _contourSimilarityData[contourIndex] = (0, 0, 0, 0);
            }
        }

        /// <summary>
        /// Вызывается с площадью текущего слоя контура. Обновляет данные
        /// подобия (первые два слоя — ДО проверки остальных критериев,
        /// чтобы данные сохранялись для всех контуров, даже если они
        /// будут пропущены по другим критериям) и оценивает все критерии
        /// отсечки.
        /// </summary>
        /// <param name="contourIndex">Индекс контура в операции.</param>
        /// <param name="currentArea">Площадь смещённого контура текущего слоя.</param>
        /// <param name="passNumber">Номер слоя (прохода), начиная с 1.</param>
        /// <param name="wallTaperAngleDeg">Уклон стенок операции.</param>
        /// <param name="windingDirectionChanged">Сменилось ли направление обхода контура (критерий 2).</param>
        /// <param name="vectorDirectionChanged">Сменилось ли направление хотя бы одного вектора от вершины до центра (критерий 3).</param>
        /// <param name="contourTooSmall">Не стал ли контур слишком маленьким для обработки.</param>
        /// <returns>true, если контур нужно пропустить в этом слое.</returns>
        public bool ShouldSkip(
            int contourIndex,
            double currentArea,
            int passNumber,
            double wallTaperAngleDeg,
            bool windingDirectionChanged,
            bool vectorDirectionChanged,
            bool contourTooSmall)
        {
            // Сохраняем площади первых двух слоев для вычисления подобия (ДО проверки других критериев отсечки)
            if (passNumber <= 2)
            {
                if (passNumber == 1)
                {
                    // Первый слой - сохраняем площадь
                    if (!_contourSimilarityData.ContainsKey(contourIndex))
                    {
                        _contourSimilarityData[contourIndex] = (currentArea, 0, 0, 0);
                    }
                    else
                    {
                        var existing = _contourSimilarityData[contourIndex];
                        _contourSimilarityData[contourIndex] = (currentArea, existing.secondArea, existing.ratio, existing.hourglassLayer);
                    }
                }
                else
                {
                    // Второй слой - сохраняем площадь и вычисляем соотношение
                    if (_contourSimilarityData.ContainsKey(contourIndex))
                    {
                        var existing = _contourSimilarityData[contourIndex];
                        double firstArea = existing.firstArea;

                        if (firstArea > Tolerance && currentArea > Tolerance)
                        {
                            double ratio = currentArea / firstArea;

                            // Вычисляем номер слоя, где будет точка "песочных часов"
                            // Точка "песочных часов" - это когда площадь становится меньше 1% от исходной
                            // Для слоя n: An = A0 * ratio^(n-1)
                            // Находим n, где An <= 0.01 * A0
                            // ratio^(n-1) <= 0.01
                            // (n-1) * log(ratio) <= log(0.01)
                            // n-1 >= log(0.01) / log(ratio)
                            // n >= log(0.01) / log(ratio) + 1
                            int hourglassLayer = 0;
                            if (ratio > 0 && ratio < 1)
                            {
                                double logRatio = Math.Log(ratio);
                                if (Math.Abs(logRatio) > Tolerance)
                                {
                                    double n = Math.Log(0.01) / logRatio + 1;
                                    hourglassLayer = (int)Math.Ceiling(n);
                                    // Убеждаемся, что hourglassLayer >= 2 (минимум после второго слоя)
                                    if (hourglassLayer < 2)
                                        hourglassLayer = 2;
                                }
                            }

                            _contourSimilarityData[contourIndex] = (firstArea, currentArea, ratio, hourglassLayer);

                            // Если текущий слой уже достиг точки "песочных часов", прекращаем обработку
                            if (hourglassLayer > 0 && passNumber >= hourglassLayer)
                            {
                                // Этот контур достиг точки "песочных часов" - пропускаем его, но продолжаем обрабатывать остальные контуры
                                return true;
                            }
                        }
                        else
                        {
                            // Если первая площадь не была сохранена, сохраняем текущую как первую
                            _contourSimilarityData[contourIndex] = (currentArea, 0, 0, 0);
                        }
                    }
                    else
                    {
                        // Если данных о первом слое нет, сохраняем текущую площадь как первую
                        _contourSimilarityData[contourIndex] = (currentArea, 0, 0, 0);
                    }
                }
            }

            // Критерий 4: Проверка подобия фигур (если есть хотя бы два первых слоя)
            // Проверяем ПОСЛЕ сохранения данных, чтобы использовать актуальные данные
            if (_contourSimilarityData.ContainsKey(contourIndex))
            {
                var similarityData = _contourSimilarityData[contourIndex];
                // Если точка "песочных часов" уже вычислена и текущий слой >= этой точки, прекращаем обработку
                if (similarityData.hourglassLayer > 0 && passNumber >= similarityData.hourglassLayer)
                {
                    // Этот контур достиг точки "песочных часов" - пропускаем его, но продолжаем обрабатывать остальные контуры
                    return true;
                }
            }

            // Критерий 1: Изменение площади относительно предыдущего слоя
            bool shouldStop = false;
            if (_previousContourAreas.ContainsKey(contourIndex))
            {
                double previousArea = _previousContourAreas[contourIndex];

                // Проверяем уклон: для нулевого уклона площадь остается постоянной, для положительного - уменьшается
                if (wallTaperAngleDeg == 0.0)
                {
                    // Для нулевого уклона: площадь должна оставаться примерно постоянной
                    // Останавливаемся только если площадь увеличилась (что было бы ошибкой)
                    if (currentArea > previousArea + Tolerance)
                    {
                        shouldStop = true;
                    }
                }
                else
                {
                    // Для положительного уклона (сужение внутрь): площадь должна уменьшаться
                    // Если площадь увеличилась или осталась равной - контур инвертировался или вырожден
                    if (currentArea >= previousArea - Tolerance)
                    {
                        shouldStop = true;
                    }
                }
            }

            // Критерий 2: Смена направления обхода контура
            if (!shouldStop && windingDirectionChanged)
            {
                shouldStop = true;
            }

            // Критерий 3: Смена направления хотя бы одного вектора от вершины до центра
            if (!shouldStop && vectorDirectionChanged)
            {
                shouldStop = true;
            }

            if (shouldStop)
            {
                // Этот контур достиг своего последнего слоя - пропускаем его, но продолжаем обрабатывать остальные контуры
                return true;
            }

            // Проверяем, не стал ли контур слишком маленьким для обработки (дополнительная проверка)
            return contourTooSmall;
        }

        /// <summary>
        /// Вызывается после успешной обработки контура в слое:
        /// сохраняет площадь текущего слоя для критерия 1 на следующем слое.
        /// </summary>
        public void RecordMilled(int contourIndex, double currentArea)
        {
            _previousContourAreas[contourIndex] = currentArea;
        }
    }
}
