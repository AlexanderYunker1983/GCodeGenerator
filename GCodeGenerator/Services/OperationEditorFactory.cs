using System;
using System.Collections.ObjectModel;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.Pocket;
using GCodeGenerator.ViewModels.PocketMill;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Фабрика диалогов редактора операций (пункт 7.3 плана): реестр
    /// «тип операции → тип диалоговой VM». Сверление диспетчеризуется по
    /// <see cref="DrillMode"/> (пункт 3.4 плана), а не по имени операции.
    /// Единая точка создания/показа диалогов операций: добавление
    /// (категорийные VM) и редактирование (MainViewModel).
    /// </summary>
    public interface IOperationEditorFactory
    {
        /// <summary>
        /// Тип диалоговой VM для операции (сверление — по DrillMode).
        /// <c>null</c>, если для типа нет диалога.
        /// </summary>
        Type GetViewModelType(OperationBase operation);

        /// <summary>
        /// Показывает диалог редактора существующей операции (модально).
        /// Изменения применяются к операции только по OK.
        /// </summary>
        void ShowEditor(OperationBase operation, ObservableCollection<OperationBase> allOperations);

        /// <summary>
        /// Показывает диалог для новой операции и добавляет её в список
        /// только в случае подтверждения.
        /// </summary>
        /// <param name="operation">Новая операция со значениями по умолчанию.</param>
        /// <param name="allOperations">Единая коллекция операций документа.</param>
        /// <returns><c>true</c>, если операция подтверждена и добавлена.</returns>
        bool CreateOperation(OperationBase operation, ObservableCollection<OperationBase> allOperations);
    }

    /// <summary>
    /// Реестр-реализация <see cref="IOperationEditorFactory"/> (пункт 7.3 плана).
    /// </summary>
    public class OperationEditorFactory : IOperationEditorFactory
    {
        private readonly IDialogService _dialogService;

        public OperationEditorFactory(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public Type GetViewModelType(OperationBase operation)
        {
            switch (operation)
            {
                case DrillPointsOperation drill:
                    return GetDrillViewModelType(drill.DrillMode);
                case PocketCircleOperation: return typeof(PocketCircleOperationViewModel);
                case PocketRectangleOperation: return typeof(PocketRectangleOperationViewModel);
                case PocketEllipseOperation: return typeof(PocketEllipseOperationViewModel);
                case PocketDxfOperation: return typeof(PocketDxfOperationViewModel);
                case ProfileCircleOperation: return typeof(ProfileCircleOperationViewModel);
                case ProfileRectangleOperation: return typeof(ProfileRectangleOperationViewModel);
                case ProfileRoundedRectangleOperation: return typeof(ProfileRoundedRectangleOperationViewModel);
                case ProfileEllipseOperation: return typeof(ProfileEllipseOperationViewModel);
                case ProfilePolygonOperation: return typeof(ProfilePolygonOperationViewModel);
                case ProfileDxfOperation: return typeof(ProfileDxfOperationViewModel);
                default: return null;
            }
        }

        public void ShowEditor(OperationBase operation, ObservableCollection<OperationBase> allOperations)
        {
            if (operation == null) return;

            // Диалог правит копию: отмена и закрытие крестиком не меняют
            // операцию, а неверные параметры её не удаляют — окно не
            // закроется, пока их не исправят.
            var workingCopy = OperationEditTransaction.CreateWorkingCopy(operation);
            if (RunEditor(workingCopy))
                OperationEditTransaction.Commit(workingCopy, operation);
        }

        public bool CreateOperation(OperationBase operation, ObservableCollection<OperationBase> allOperations)
        {
            if (operation == null) return false;

            // Новая операция ещё никому не принадлежит, поэтому диалог правит
            // её саму: в документ она попадает только после подтверждения,
            // а отмена не оставляет после себя ничего.
            if (!RunEditor(operation))
                return false;

            allOperations?.Add(operation);
            return true;
        }

        /// <summary>
        /// Показывает диалог операции модально и сообщает, подтвердил ли
        /// пользователь параметры.
        /// </summary>
        private bool RunEditor(OperationBase operation)
        {
            var vmType = GetViewModelType(operation);
            if (vmType == null) return false;

            var vm = _dialogService.CreateViewModel(vmType);
            if (!(vm is IOperationEditorViewModel editor))
                throw new InvalidOperationException(
                    $"View-модель {vmType.Name} не реализует {nameof(IOperationEditorViewModel)}.");

            editor.SetOperation(operation);
            _dialogService.ShowDialog(vmType, vm);
            return editor.IsAccepted;
        }

        /// <summary>
        /// Тип диалоговой VM сверления (пункт 3.4 плана): диспетчеризация по
        /// <see cref="DrillMode"/>, а не по имени операции. Пункт 7.3: перенесён
        /// из MainViewModel.
        /// </summary>
        private static Type GetDrillViewModelType(DrillMode mode)
        {
            switch (mode)
            {
                case DrillMode.Line: return typeof(DrillLineOperationViewModel);
                case DrillMode.Array: return typeof(DrillArrayOperationViewModel);
                case DrillMode.Rect: return typeof(DrillRectOperationViewModel);
                case DrillMode.Circle: return typeof(DrillCircleOperationViewModel);
                case DrillMode.Arc: return typeof(DrillArcOperationViewModel);
                case DrillMode.Polygon: return typeof(DrillPolygonOperationViewModel);
                case DrillMode.Ellipse: return typeof(DrillEllipseOperationViewModel);
                case DrillMode.Package: return typeof(DrillPackageOperationViewModel);
                default: return typeof(DrillPointsOperationViewModel);
            }
        }
    }
}
