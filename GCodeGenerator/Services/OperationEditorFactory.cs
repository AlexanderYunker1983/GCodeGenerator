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
        /// Показывает диалог редактора операции (модально): создаёт диалоговую
        /// VM, задаёт единую коллекцию операций и операцию, показывает окно.
        /// </summary>
        void ShowEditor(OperationBase operation, ObservableCollection<OperationBase> allOperations);
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
            var vmType = GetViewModelType(operation);
            if (vmType == null) return;
            var workingCopy = OperationEditTransaction.CreateWorkingCopy(operation);
            var vm = _dialogService.CreateViewModel(vmType);
            switch (vm)
            {
                case IDrillDialogViewModel drillVm:
                    drillVm.Operations = allOperations;
                    drillVm.Operation = (DrillPointsOperation)workingCopy;
                    break;
                case PocketCircleOperationViewModel pocketCircleVm:
                    pocketCircleVm.Operations = allOperations;
                    pocketCircleVm.Operation = (PocketCircleOperation)workingCopy;
                    break;
                case PocketRectangleOperationViewModel pocketRectangleVm:
                    pocketRectangleVm.Operations = allOperations;
                    pocketRectangleVm.Operation = (PocketRectangleOperation)workingCopy;
                    break;
                case PocketEllipseOperationViewModel pocketEllipseVm:
                    pocketEllipseVm.Operations = allOperations;
                    pocketEllipseVm.Operation = (PocketEllipseOperation)workingCopy;
                    break;
                case PocketDxfOperationViewModel pocketDxfVm:
                    pocketDxfVm.Operations = allOperations;
                    pocketDxfVm.Operation = (PocketDxfOperation)workingCopy;
                    break;
                case ProfileCircleOperationViewModel profileCircleVm:
                    profileCircleVm.Operations = allOperations;
                    profileCircleVm.Operation = (ProfileCircleOperation)workingCopy;
                    break;
                case ProfileRectangleOperationViewModel profileRectangleVm:
                    profileRectangleVm.Operations = allOperations;
                    profileRectangleVm.Operation = (ProfileRectangleOperation)workingCopy;
                    break;
                case ProfileRoundedRectangleOperationViewModel profileRoundedRectangleVm:
                    profileRoundedRectangleVm.Operations = allOperations;
                    profileRoundedRectangleVm.Operation = (ProfileRoundedRectangleOperation)workingCopy;
                    break;
                case ProfileEllipseOperationViewModel profileEllipseVm:
                    profileEllipseVm.Operations = allOperations;
                    profileEllipseVm.Operation = (ProfileEllipseOperation)workingCopy;
                    break;
                case ProfilePolygonOperationViewModel profilePolygonVm:
                    profilePolygonVm.Operations = allOperations;
                    profilePolygonVm.Operation = (ProfilePolygonOperation)workingCopy;
                    break;
                case ProfileDxfOperationViewModel profileDxfVm:
                    profileDxfVm.Operations = allOperations;
                    profileDxfVm.Operation = (ProfileDxfOperation)workingCopy;
                    break;
                default:
                    throw new InvalidOperationException($"Неизвестный тип диалоговой VM: {vmType}");
            }
            _dialogService.ShowDialog(vmType, vm);

            if (vm is IOperationEditorSession session)
            {
                if (session.IsAccepted)
                    OperationEditTransaction.Commit(workingCopy, operation);
                else if (session.IsRemovalRequested)
                    allOperations?.Remove(operation);
            }
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
