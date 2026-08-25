using System;
using System.Collections.Generic;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.Pocket;
using GCodeGenerator.ViewModels.PocketMill;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Соответствие «операция → диалог её параметров».
    ///
    /// Это единственная грань операции, которую ядро описать не может: оно
    /// не знает ни об окнах, ни о view-моделях. Поэтому каталог операций
    /// дополняется здесь, в приложении, а полнота таблицы относительно
    /// каталога проверяется тестом: тип без диалога открывался бы молча
    /// ничем — пользователь нажимал бы «изменить» без всякого результата.
    ///
    /// Сверление — один тип операции с девятью режимами расстановки
    /// отверстий, у каждого свой диалог, поэтому оно разбирается по
    /// <see cref="DrillMode"/>, а не по типу.
    /// </summary>
    public static class OperationEditorRegistry
    {
        private static readonly Dictionary<Type, Type> ByOperationType = new Dictionary<Type, Type>
        {
            [typeof(PocketRectangleOperation)] = typeof(PocketRectangleOperationViewModel),
            [typeof(PocketCircleOperation)] = typeof(PocketCircleOperationViewModel),
            [typeof(PocketEllipseOperation)] = typeof(PocketEllipseOperationViewModel),
            [typeof(PocketDxfOperation)] = typeof(PocketDxfOperationViewModel),
            [typeof(ProfileRectangleOperation)] = typeof(ProfileRectangleOperationViewModel),
            [typeof(ProfileRoundedRectangleOperation)] = typeof(ProfileRoundedRectangleOperationViewModel),
            [typeof(ProfileCircleOperation)] = typeof(ProfileCircleOperationViewModel),
            [typeof(ProfileEllipseOperation)] = typeof(ProfileEllipseOperationViewModel),
            [typeof(ProfilePolygonOperation)] = typeof(ProfilePolygonOperationViewModel),
            [typeof(ProfileDxfOperation)] = typeof(ProfileDxfOperationViewModel),
        };

        private static readonly Dictionary<DrillMode, Type> ByDrillMode = new Dictionary<DrillMode, Type>
        {
            [DrillMode.Points] = typeof(DrillPointsOperationViewModel),
            [DrillMode.Line] = typeof(DrillLineOperationViewModel),
            [DrillMode.Array] = typeof(DrillArrayOperationViewModel),
            [DrillMode.Rect] = typeof(DrillRectOperationViewModel),
            [DrillMode.Circle] = typeof(DrillCircleOperationViewModel),
            [DrillMode.Arc] = typeof(DrillArcOperationViewModel),
            [DrillMode.Polygon] = typeof(DrillPolygonOperationViewModel),
            [DrillMode.Ellipse] = typeof(DrillEllipseOperationViewModel),
            [DrillMode.Package] = typeof(DrillPackageOperationViewModel),
        };

        /// <summary>
        /// Тип view-модели диалога для операции; <c>null</c>, если диалога
        /// для неё не зарегистрировано.
        /// </summary>
        public static Type ViewModelTypeFor(OperationBase operation)
        {
            if (operation == null)
                return null;

            if (operation is DrillPointsOperation drill)
                return ByDrillMode.TryGetValue(drill.DrillMode, out var drillViewModel) ? drillViewModel : null;

            return ByOperationType.TryGetValue(operation.GetType(), out var viewModel) ? viewModel : null;
        }

        /// <summary>Все зарегистрированные диалоги — для проверки полноты.</summary>
        public static IReadOnlyDictionary<Type, Type> Registrations => ByOperationType;

        /// <summary>Диалоги режимов сверления — для проверки полноты.</summary>
        public static IReadOnlyDictionary<DrillMode, Type> DrillRegistrations => ByDrillMode;
    }
}
