using System;
using System.Collections.Generic;
using System.Globalization;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Односторонняя миграция легаси-словаря <c>Metadata</c> в типизированные
    /// свойства операции. Вызывается <c>ProjectFileService</c> только при чтении
    /// форматов v1-v3; текущие доменные модели словаря Metadata больше не имеют.
    ///
    /// Старые .ygc (v1 и v2 до пункта 3.1) хранят параметры паттерна сверления
    /// только в Metadata. После миграции значения находятся в типизированных
    /// свойствах, а распознанные ключи удаляются из переданного словаря.
    /// Оставшиеся ключи проверяются файловым адаптером: они не должны быть
    /// незаметно потеряны при сохранении в новом формате.
    ///
    /// Профили миграции не требуют: легаси-файлы профилей содержат типизированные
    /// свойства, а старый словарь был лишь их дубликатом.
    ///
    /// Карманы (пункт 7.2c): старые диалоги хранили параметры карманов в
    /// <c>Metadata</c> (ключ-триггер геометрии Radius/Width/RadiusX) — значения
    /// копируются в типизированные свойства, мигрированные ключи удаляются.
    /// DXF-карманы не мигрируются: их диалог никогда не читал Metadata.
    ///
    /// Метод идемпотентен: операция с пустым словарём не изменяется.
    /// </summary>
    public static class LegacyMetadataMigrator
    {
        /// <summary>
        /// Мигрирует легаси-Metadata операции в типизированные свойства.
        /// </summary>
        public static void Migrate(OperationBase operation, IDictionary<string, object> metadata)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (metadata == null || metadata.Count == 0)
                return;

            switch (operation)
            {
                case DrillPointsOperation drill:
                    MigrateDrill(drill, metadata);
                    break;
                case PocketCircleOperation pocketCircle:
                    MigratePocketCircle(pocketCircle, metadata);
                    break;
                case PocketRectangleOperation pocketRectangle:
                    MigratePocketRectangle(pocketRectangle, metadata);
                    break;
                case PocketEllipseOperation pocketEllipse:
                    MigratePocketEllipse(pocketEllipse, metadata);
                    break;
                default:
                    // Профили: Metadata не десериализуется (пункт 3.6), миграция не нужна.
                    // PocketDxfOperation: диалог никогда не читал Metadata (только
                    // типизированные свойства) — миграция значений не требуется.
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Сверление
        // ------------------------------------------------------------------

        private static void MigrateDrill(DrillPointsOperation op, IDictionary<string, object> meta)
        {
            var mode = DetectDrillMode(op, meta);
            if (mode == DrillMode.Points)
                return; // распознанных ключей паттерна нет — Metadata оставляем как есть

            op.DrillMode = mode;

            switch (mode)
            {
                case DrillMode.Line:
                case DrillMode.Array:
                case DrillMode.Rect:
                    ReadDouble(meta, "StartX", v => op.StartX = v);
                    ReadDouble(meta, "StartY", v => op.StartY = v);
                    ReadDouble(meta, "StartZ", v => op.StartZ = v);
                    ReadDouble(meta, "Distance", v => op.Distance = v);
                    ReadInt(meta, "HoleCount", v => op.HoleCount = v);
                    ReadDouble(meta, "AngleDeg", v => op.AngleDeg = v);
                    if (mode != DrillMode.Line)
                    {
                        ReadDouble(meta, "RowPitch", v => op.RowPitch = v);
                        ReadInt(meta, "RowCount", v => op.RowCount = v);
                    }
                    break;

                case DrillMode.Circle:
                case DrillMode.Arc:
                    ReadDouble(meta, "CenterX", v => op.CenterX = v);
                    ReadDouble(meta, "CenterY", v => op.CenterY = v);
                    ReadDouble(meta, "Z", v => op.Z = v);
                    ReadDouble(meta, "Radius", v => op.Radius = v);
                    ReadInt(meta, "HoleCount", v => op.HoleCount = v);
                    ReadDouble(meta, "StartAngleDeg", v => op.StartAngleDeg = v);
                    if (mode == DrillMode.Arc)
                        ReadDouble(meta, "EndAngleDeg", v => op.EndAngleDeg = v);
                    break;

                case DrillMode.Polygon:
                    ReadDouble(meta, "CenterX", v => op.CenterX = v);
                    ReadDouble(meta, "CenterY", v => op.CenterY = v);
                    ReadDouble(meta, "Z", v => op.Z = v);
                    ReadDouble(meta, "Radius", v => op.Radius = v);
                    ReadInt(meta, "NumberOfSides", v => op.NumberOfSides = v);
                    ReadInt(meta, "HolesPerSide", v => op.HolesPerSide = v);
                    ReadDouble(meta, "RotationAngle", v => op.RotationAngle = v);
                    break;

                case DrillMode.Ellipse:
                    ReadDouble(meta, "CenterX", v => op.CenterX = v);
                    ReadDouble(meta, "CenterY", v => op.CenterY = v);
                    ReadDouble(meta, "Z", v => op.Z = v);
                    ReadDouble(meta, "RadiusX", v => op.RadiusX = v);
                    ReadDouble(meta, "RadiusY", v => op.RadiusY = v);
                    ReadDouble(meta, "RotationAngle", v => op.RotationAngle = v);
                    ReadInt(meta, "HoleCount", v => op.HoleCount = v);
                    ReadDouble(meta, "StartAngleDeg", v => op.StartAngleDeg = v);
                    break;

                case DrillMode.Package:
                    ReadDouble(meta, "CenterX", v => op.CenterX = v);
                    ReadDouble(meta, "CenterY", v => op.CenterY = v);
                    ReadDouble(meta, "Z", v => op.Z = v);
                    ReadDouble(meta, "RotationAngle", v => op.RotationAngle = v);
                    if (meta.TryGetValue("PackageName", out var packageName) && packageName is string name)
                    {
                        op.PackageName = name;
                        meta.Remove("PackageName");
                    }
                    break;
            }

            // Общие Z-параметры (записывались всеми режимами, кроме Points).
            ReadDouble(meta, "TotalDepth", v => op.TotalDepth = v);
            ReadDouble(meta, "StepDepth", v => op.StepDepth = v);
            ReadDouble(meta, "FeedZRapid", v => op.FeedZRapid = v);
            ReadDouble(meta, "FeedZWork", v => op.FeedZWork = v);
            ReadDouble(meta, "RetractHeight", v => op.RetractHeight = v);
        }

        /// <summary>
        /// Определяет режим сверления по ключам Metadata (порядок проверок
        /// однозначен: у каждого режима есть ключ, которого нет у других).
        /// Array и Rect имеют одинаковый набор ключей — различаются по числу
        /// отверстий (см. ниже).
        /// </summary>
        private static DrillMode DetectDrillMode(DrillPointsOperation op, IDictionary<string, object> meta)
        {
            if (meta.ContainsKey("PackageName"))
                return DrillMode.Package;
            if (meta.ContainsKey("NumberOfSides"))
                return DrillMode.Polygon;
            if (meta.ContainsKey("RadiusX"))
                return DrillMode.Ellipse;
            if (meta.ContainsKey("EndAngleDeg"))
                return DrillMode.Arc;
            if (meta.ContainsKey("CenterX"))
                return DrillMode.Circle;
            if (meta.ContainsKey("StartX") && meta.ContainsKey("RowCount"))
                return DetectArrayOrRect(op, meta);
            if (meta.ContainsKey("StartX"))
                return DrillMode.Line;
            return DrillMode.Points;
        }

        /// <summary>
        /// Array и Rect пишут одинаковые ключи Metadata, различие — в формуле
        /// построения отверстий (все точки сетки vs только контур). По числу
        /// отверстий: Array = RowCount*HoleCount, Rect = 2*RowCount+2*HoleCount-4.
        /// При RowCount==2 или HoleCount==2 обе формулы совпадают — G-код
        /// идентичен, режим выбираем Array (отличается только диалог редактирования).
        /// </summary>
        private static DrillMode DetectArrayOrRect(DrillPointsOperation op, IDictionary<string, object> meta)
        {
            var rowCount = ReadIntValue(meta, "RowCount");
            var holeCount = ReadIntValue(meta, "HoleCount");
            var holes = op.Holes?.Count ?? 0;

            var fitsArray = rowCount > 0 && holeCount > 0 && holes == rowCount * holeCount;
            var fitsRect = rowCount > 1 && holeCount > 1 && holes == 2 * rowCount + 2 * holeCount - 4;

            return fitsRect && !fitsArray ? DrillMode.Rect : DrillMode.Array;
        }

        // ------------------------------------------------------------------
        // Чтение значений Metadata
        //
        // Значения после legacy JSON-адаптера: Int32/Int64/Decimal/
        // string/bool/null (enum — Int32). Читаем через Convert, как это делали
        // диалоги. Неконвертируемый ключ остаётся в словаре, поэтому файловый
        // адаптер отклонит проект вместо тихой подстановки значения по умолчанию.
        // ------------------------------------------------------------------

        private static void ReadDouble(IDictionary<string, object> meta, string key, Action<double> apply)
        {
            if (meta.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    apply(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    meta.Remove(key);
                }
                catch (Exception)
                {
                    // Файловый адаптер увидит оставшийся некорректный ключ.
                }
            }
        }

        private static void ReadInt(IDictionary<string, object> meta, string key, Action<int> apply)
        {
            if (meta.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    apply(Convert.ToInt32(value, CultureInfo.InvariantCulture));
                    meta.Remove(key);
                }
                catch (Exception)
                {
                    // Файловый адаптер увидит оставшийся некорректный ключ.
                }
            }
        }

        private static void ReadEnum<TEnum>(IDictionary<string, object> meta, string key, Action<TEnum> apply)
            where TEnum : struct
        {
            if (meta.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    // enum в Metadata хранится как Int32.
                    apply((TEnum)Enum.ToObject(typeof(TEnum), Convert.ToInt32(value, CultureInfo.InvariantCulture)));
                    meta.Remove(key);
                }
                catch (Exception)
                {
                    // Файловый адаптер увидит оставшийся некорректный ключ.
                }
            }
        }

        private static int ReadIntValue(IDictionary<string, object> meta, string key)
        {
            if (meta.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
            return 0;
        }

        private static void ReadBool(IDictionary<string, object> meta, string key, Action<bool> apply)
        {
            if (meta.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    apply(Convert.ToBoolean(value, CultureInfo.InvariantCulture));
                    meta.Remove(key);
                }
                catch (Exception)
                {
                    // Файловый адаптер увидит оставшийся некорректный ключ.
                }
            }
        }

        // ------------------------------------------------------------------
        // Карманы (пункт 7.2c плана)
        //
        // Старые диалоги карманов хранили параметры в Metadata (типизированные
        // свойства при этом могли отсутствовать или быть устаревшими). Старое
        // поведение диалогов: если в Metadata есть ключ геометрии (Radius/Width/
        // RadiusX), значения берутся из Metadata (Metadata побеждает), иначе —
        // из типизированных свойств. Миграция повторяет это поведение в Core:
        // значения копируются в типизированные свойства, мигрированные ключи
        // удаляются, при следующем сохранении файл их уже не содержит.
        // Нераспознанные ключи остаются в переданном словаре: файловый адаптер
        // отклоняет такой проект, чтобы данные не потерялись при сохранении в v4.
        // ------------------------------------------------------------------

        private static void MigratePocketCircle(PocketCircleOperation op, IDictionary<string, object> meta)
        {
            if (meta.Count == 0 || !meta.ContainsKey("Radius"))
                return;

            ReadDouble(meta, "CenterX", v => op.CenterX = v);
            ReadDouble(meta, "CenterY", v => op.CenterY = v);
            ReadDouble(meta, "Radius", v => op.Radius = v);
            ReadCommonPocket(meta,
                v => op.Direction = v,
                v => op.PocketStrategy = v,
                v => op.TotalDepth = v,
                v => op.StepDepth = v,
                v => op.ToolDiameter = v,
                v => op.ContourHeight = v,
                v => op.FeedXYRapid = v,
                v => op.FeedXYWork = v,
                v => op.FeedZRapid = v,
                v => op.FeedZWork = v,
                v => op.SafeZHeight = v,
                v => op.RetractHeight = v,
                v => op.StepPercentOfTool = v,
                v => op.Decimals = v,
                v => op.LineAngleDeg = v,
                v => op.WallTaperAngleDeg = v,
                v => op.IsRoughingEnabled = v,
                v => op.IsFinishingEnabled = v,
                v => op.FinishAllowance = v,
                v => op.FinishingMode = v);
        }

        private static void MigratePocketRectangle(PocketRectangleOperation op, IDictionary<string, object> meta)
        {
            if (meta.Count == 0 || !meta.ContainsKey("Width"))
                return;

            ReadDouble(meta, "Width", v => op.Width = v);
            ReadDouble(meta, "Height", v => op.Height = v);
            ReadDouble(meta, "RotationAngle", v => op.RotationAngle = v);
            ReadDouble(meta, "ReferencePointX", v => op.ReferencePointX = v);
            ReadDouble(meta, "ReferencePointY", v => op.ReferencePointY = v);
            ReadEnum<ReferencePointType>(meta, "ReferencePointType", v => op.ReferencePointType = v);
            ReadCommonPocket(meta,
                v => op.Direction = v,
                v => op.PocketStrategy = v,
                v => op.TotalDepth = v,
                v => op.StepDepth = v,
                v => op.ToolDiameter = v,
                v => op.ContourHeight = v,
                v => op.FeedXYRapid = v,
                v => op.FeedXYWork = v,
                v => op.FeedZRapid = v,
                v => op.FeedZWork = v,
                v => op.SafeZHeight = v,
                v => op.RetractHeight = v,
                v => op.StepPercentOfTool = v,
                v => op.Decimals = v,
                v => op.LineAngleDeg = v,
                v => op.WallTaperAngleDeg = v,
                v => op.IsRoughingEnabled = v,
                v => op.IsFinishingEnabled = v,
                v => op.FinishAllowance = v,
                v => op.FinishingMode = v);
        }

        private static void MigratePocketEllipse(PocketEllipseOperation op, IDictionary<string, object> meta)
        {
            if (meta.Count == 0 || !meta.ContainsKey("RadiusX"))
                return;

            ReadDouble(meta, "CenterX", v => op.CenterX = v);
            ReadDouble(meta, "CenterY", v => op.CenterY = v);
            ReadDouble(meta, "RadiusX", v => op.RadiusX = v);
            ReadDouble(meta, "RadiusY", v => op.RadiusY = v);
            ReadDouble(meta, "RotationAngle", v => op.RotationAngle = v);
            ReadCommonPocket(meta,
                v => op.Direction = v,
                v => op.PocketStrategy = v,
                v => op.TotalDepth = v,
                v => op.StepDepth = v,
                v => op.ToolDiameter = v,
                v => op.ContourHeight = v,
                v => op.FeedXYRapid = v,
                v => op.FeedXYWork = v,
                v => op.FeedZRapid = v,
                v => op.FeedZWork = v,
                v => op.SafeZHeight = v,
                v => op.RetractHeight = v,
                v => op.StepPercentOfTool = v,
                v => op.Decimals = v,
                v => op.LineAngleDeg = v,
                v => op.WallTaperAngleDeg = v,
                v => op.IsRoughingEnabled = v,
                v => op.IsFinishingEnabled = v,
                v => op.FinishAllowance = v,
                v => op.FinishingMode = v);
        }

        /// <summary>Общие фрезерные параметры карманов (одинаковый набор у всех типов).</summary>
        private static void ReadCommonPocket(
            IDictionary<string, object> meta,
            Action<MillingDirection> direction,
            Action<PocketStrategy> pocketStrategy,
            Action<double> totalDepth,
            Action<double> stepDepth,
            Action<double> toolDiameter,
            Action<double> contourHeight,
            Action<double> feedXYRapid,
            Action<double> feedXYWork,
            Action<double> feedZRapid,
            Action<double> feedZWork,
            Action<double> safeZHeight,
            Action<double> retractHeight,
            Action<double> stepPercentOfTool,
            Action<int> decimals,
            Action<double> lineAngleDeg,
            Action<double> wallTaperAngleDeg,
            Action<bool> isRoughingEnabled,
            Action<bool> isFinishingEnabled,
            Action<double> finishAllowance,
            Action<PocketFinishingMode> finishingMode)
        {
            ReadEnum<MillingDirection>(meta, "Direction", direction);
            ReadEnum<PocketStrategy>(meta, "PocketStrategy", pocketStrategy);
            ReadDouble(meta, "TotalDepth", totalDepth);
            ReadDouble(meta, "StepDepth", stepDepth);
            ReadDouble(meta, "ToolDiameter", toolDiameter);
            ReadDouble(meta, "ContourHeight", contourHeight);
            ReadDouble(meta, "FeedXYRapid", feedXYRapid);
            ReadDouble(meta, "FeedXYWork", feedXYWork);
            ReadDouble(meta, "FeedZRapid", feedZRapid);
            ReadDouble(meta, "FeedZWork", feedZWork);
            ReadDouble(meta, "SafeZHeight", safeZHeight);
            ReadDouble(meta, "RetractHeight", retractHeight);
            ReadDouble(meta, "StepPercentOfTool", stepPercentOfTool);
            ReadInt(meta, "Decimals", decimals);
            ReadDouble(meta, "LineAngleDeg", lineAngleDeg);
            ReadDouble(meta, "WallTaperAngleDeg", wallTaperAngleDeg);
            ReadBool(meta, "IsRoughingEnabled", isRoughingEnabled);
            ReadBool(meta, "IsFinishingEnabled", isFinishingEnabled);
            ReadDouble(meta, "FinishAllowance", finishAllowance);
            ReadEnum<PocketFinishingMode>(meta, "FinishingMode", finishingMode);
        }
    }
}
