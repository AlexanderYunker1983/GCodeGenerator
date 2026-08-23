using System;
using System.Collections.Generic;
using System.Globalization;
using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Односторонняя миграция легаси-словарей <c>Metadata</c> в типизированные
    /// свойства операций (пункт 3.2 плана). Вызывается <c>ProjectFileService</c>
    /// сразу после десериализации каждой операции.
    ///
    /// Старые .ygc (v1 и v2 до пункта 3.1) хранят параметры паттерна сверления
    /// только в <see cref="DrillPointsOperation.Metadata"/>; старые диалоги
    /// профилей записывали значения и в типизированные свойства, и в
    /// <c>Metadata</c> (двойная запись). После миграции значения находятся в
    /// типизированных свойствах, а мигрированные ключи удаляются из
    /// <c>Metadata</c>, поэтому при следующем сохранении файл уже не содержит
    /// их. Нераспознанные ключи (вручную созданные файлы) сохраняются.
    ///
    /// Метод идемпотентен: операция с пустым <c>Metadata</c> не изменяется.
    /// </summary>
    public static class LegacyMetadataMigrator
    {
        /// <summary>
        /// Мигрирует легаси-Metadata операции в типизированные свойства.
        /// </summary>
        public static void Migrate(OperationBase operation)
        {
            switch (operation)
            {
                case DrillPointsOperation drill:
                    MigrateDrill(drill);
                    break;
                case ProfileRectangleOperation profileRectangle:
                    MigrateProfile(profileRectangle);
                    break;
                case ProfileRoundedRectangleOperation profileRoundedRectangle:
                    MigrateProfile(profileRoundedRectangle);
                    break;
                case ProfileCircleOperation profileCircle:
                    MigrateProfile(profileCircle);
                    break;
                case ProfileEllipseOperation profileEllipse:
                    MigrateProfile(profileEllipse);
                    break;
                case ProfilePolygonOperation profilePolygon:
                    MigrateProfile(profilePolygon);
                    break;
                default:
                    // Pocket*Operation и ProfileDxfOperation: Metadata не используется
                    // генераторами и диалогами (карманы — кандидаты на аналогичную
                    // миграцию в поздней фазе плана).
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Сверление
        // ------------------------------------------------------------------

        private static void MigrateDrill(DrillPointsOperation op)
        {
            var meta = op.Metadata;
            if (meta == null)
            {
                op.Metadata = new Dictionary<string, object>();
                return;
            }
            if (meta.Count == 0)
                return;

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
        // Профили
        // ------------------------------------------------------------------

        /// <summary>
        /// Общие для всех профилей ключи Metadata (двойная запись старых диалогов).
        /// </summary>
        private static void MigrateProfileCommon(IDictionary<string, object> meta, IProfileOperation op)
        {
            ReadEnum<ToolPathMode>(meta, "ToolPathMode", e => op.ToolPathMode = e);
            ReadEnum<MillingDirection>(meta, "Direction", e => op.Direction = e);
            ReadEnum<EntryMode>(meta, "EntryMode", e => op.EntryMode = e);
            ReadDouble(meta, "TotalDepth", v => op.TotalDepth = v);
            ReadDouble(meta, "StepDepth", v => op.StepDepth = v);
            ReadDouble(meta, "ToolDiameter", v => op.ToolDiameter = v);
            ReadDouble(meta, "ContourHeight", v => op.ContourHeight = v);
            ReadDouble(meta, "FeedXYRapid", v => op.FeedXYRapid = v);
            ReadDouble(meta, "FeedXYWork", v => op.FeedXYWork = v);
            ReadDouble(meta, "FeedZRapid", v => op.FeedZRapid = v);
            ReadDouble(meta, "FeedZWork", v => op.FeedZWork = v);
            ReadDouble(meta, "SafeZHeight", v => op.SafeZHeight = v);
            ReadDouble(meta, "RetractHeight", v => op.RetractHeight = v);
            ReadDouble(meta, "EntryAngle", v => op.EntryAngle = v);
            ReadDouble(meta, "MaxSegmentLength", v => op.MaxSegmentLength = v);
            ReadInt(meta, "Decimals", v => op.Decimals = v);
            // SafeDistanceBetweenPasses отсутствует в IProfileOperation —
            // читается в методах конкретных типов ниже.
        }

        private static void MigrateProfile(ProfileRectangleOperation op)
        {
            var meta = op.Metadata;
            if (meta == null || meta.Count == 0)
                return;

            MigrateProfileCommon(meta, op);
            ReadDouble(meta, "Width", v => op.Width = v);
            ReadDouble(meta, "Height", v => op.Height = v);
            ReadDouble(meta, "RotationAngle", v => op.RotationAngle = v);
            ReadDouble(meta, "ReferencePointX", v => op.ReferencePointX = v);
            ReadDouble(meta, "ReferencePointY", v => op.ReferencePointY = v);
            ReadEnum<ReferencePointType>(meta, "ReferencePointType", e => op.ReferencePointType = e);
            ReadDouble(meta, "SafeDistanceBetweenPasses", v => op.SafeDistanceBetweenPasses = v);
        }

        private static void MigrateProfile(ProfileRoundedRectangleOperation op)
        {
            var meta = op.Metadata;
            if (meta == null || meta.Count == 0)
                return;

            MigrateProfileCommon(meta, op);
            ReadDouble(meta, "Width", v => op.Width = v);
            ReadDouble(meta, "Height", v => op.Height = v);
            ReadDouble(meta, "RotationAngle", v => op.RotationAngle = v);
            ReadDouble(meta, "RadiusTopLeft", v => op.RadiusTopLeft = v);
            ReadDouble(meta, "RadiusTopRight", v => op.RadiusTopRight = v);
            ReadDouble(meta, "RadiusBottomLeft", v => op.RadiusBottomLeft = v);
            ReadDouble(meta, "RadiusBottomRight", v => op.RadiusBottomRight = v);
            ReadDouble(meta, "ReferencePointX", v => op.ReferencePointX = v);
            ReadDouble(meta, "ReferencePointY", v => op.ReferencePointY = v);
            ReadEnum<ReferencePointType>(meta, "ReferencePointType", e => op.ReferencePointType = e);
            ReadDouble(meta, "SafeDistanceBetweenPasses", v => op.SafeDistanceBetweenPasses = v);
        }

        private static void MigrateProfile(ProfileCircleOperation op)
        {
            var meta = op.Metadata;
            if (meta == null || meta.Count == 0)
                return;

            MigrateProfileCommon(meta, op);
            ReadDouble(meta, "CenterX", v => op.CenterX = v);
            ReadDouble(meta, "CenterY", v => op.CenterY = v);
            ReadDouble(meta, "Radius", v => op.Radius = v);
            ReadDouble(meta, "SafeDistanceBetweenPasses", v => op.SafeDistanceBetweenPasses = v);
        }

        private static void MigrateProfile(ProfileEllipseOperation op)
        {
            var meta = op.Metadata;
            if (meta == null || meta.Count == 0)
                return;

            MigrateProfileCommon(meta, op);
            ReadDouble(meta, "CenterX", v => op.CenterX = v);
            ReadDouble(meta, "CenterY", v => op.CenterY = v);
            ReadDouble(meta, "RadiusX", v => op.RadiusX = v);
            ReadDouble(meta, "RadiusY", v => op.RadiusY = v);
            ReadDouble(meta, "RotationAngle", v => op.RotationAngle = v);
            ReadDouble(meta, "SafeDistanceBetweenPasses", v => op.SafeDistanceBetweenPasses = v);
        }

        private static void MigrateProfile(ProfilePolygonOperation op)
        {
            var meta = op.Metadata;
            if (meta == null || meta.Count == 0)
                return;

            MigrateProfileCommon(meta, op);
            ReadDouble(meta, "CenterX", v => op.CenterX = v);
            ReadDouble(meta, "CenterY", v => op.CenterY = v);
            ReadInt(meta, "NumberOfSides", v => op.NumberOfSides = v);
            ReadDouble(meta, "Radius", v => op.Radius = v);
            ReadDouble(meta, "RotationAngle", v => op.RotationAngle = v);
            ReadDouble(meta, "SafeDistanceBetweenPasses", v => op.SafeDistanceBetweenPasses = v);
        }

        // ------------------------------------------------------------------
        // Чтение значений Metadata
        //
        // Значения после PrimitiveDictionaryConverter: Int32/Int64/Decimal/
        // string/bool/null (enum — Int32). Читаем через Convert, как это делали
        // диалоги; при неконвертируемом значении свойство не изменяется, а
        // ключ удаляется (данные в файле были некорректны).
        // ------------------------------------------------------------------

        private static void ReadDouble(IDictionary<string, object> meta, string key, Action<double> apply)
        {
            if (meta.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    apply(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                }
                catch (Exception)
                {
                    // Некорректное значение — оставляем текущее свойство.
                }
                meta.Remove(key);
            }
        }

        private static void ReadInt(IDictionary<string, object> meta, string key, Action<int> apply)
        {
            if (meta.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    apply(Convert.ToInt32(value, CultureInfo.InvariantCulture));
                }
                catch (Exception)
                {
                    // Некорректное значение — оставляем текущее свойство.
                }
                meta.Remove(key);
            }
        }

        private static void ReadEnum<TEnum>(IDictionary<string, object> meta, string key, Action<TEnum> apply)
            where TEnum : struct
        {
            if (meta.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    // enum в Metadata хранится как Int32 (PrimitiveDictionaryConverter).
                    apply((TEnum)Enum.ToObject(typeof(TEnum), Convert.ToInt32(value, CultureInfo.InvariantCulture)));
                }
                catch (Exception)
                {
                    // Некорректное значение — оставляем текущее свойство.
                }
                meta.Remove(key);
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
    }
}
