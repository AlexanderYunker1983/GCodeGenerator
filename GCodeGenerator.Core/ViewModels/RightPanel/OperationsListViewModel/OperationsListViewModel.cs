using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.Models;

namespace GCodeGenerator.Core.ViewModels.RightPanel.OperationsListViewModel;

public partial class OperationsListViewModel : ViewModelBase, IHasDisplayName
{
    public ObservableCollection<OperationItem> Operations { get; } = new();

    [ObservableProperty]
    private OperationItem? _selectedOperation;

    /// <summary>
    /// Можно ли удалить текущую операцию.
    /// </summary>
    public bool CanDelete => SelectedOperation is not null;

    /// <summary>
    /// Можно ли сдвинуть текущую операцию вверх.
    /// </summary>
    public bool CanMoveUp =>
        SelectedOperation is not null &&
        Operations.IndexOf(SelectedOperation) > 0;

    /// <summary>
    /// Можно ли сдвинуть текущую операцию вниз.
    /// </summary>
    public bool CanMoveDown =>
        SelectedOperation is not null &&
        Operations.IndexOf(SelectedOperation) >= 0 &&
        Operations.IndexOf(SelectedOperation) < Operations.Count - 1;

    public OperationsListViewModel()
    {
        InitializeResources();

        // Временные данные-заглушки, чтобы было что увидеть в UI.
        Operations.Add(new OperationItem("Операция 1"));
        Operations.Add(new OperationItem("Операция 2"));
        Operations.Add(new OperationItem("Операция 3"));
    }

    partial void OnSelectedOperationChanged(OperationItem? value)
    {
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (!CanMoveUp)
            return;

        var operation = SelectedOperation!;
        var index = Operations.IndexOf(operation);
        Operations.Move(index, index - 1);

        // Явно переустанавливаем выделение, чтобы UI не терял выбранный элемент.
        SelectedOperation = operation;
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (!CanMoveDown)
            return;

        var operation = SelectedOperation!;
        var index = Operations.IndexOf(operation);
        Operations.Move(index, index + 1);

        SelectedOperation = operation;
    }

    [RelayCommand]
    private void Delete()
    {
        if (!CanDelete)
            return;

        var index = Operations.IndexOf(SelectedOperation!);
        if (index >= 0)
        {
            Operations.RemoveAt(index);
            SelectedOperation = index < Operations.Count ? Operations[index] : null;

            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(CanMoveUp));
            OnPropertyChanged(nameof(CanMoveDown));
        }
    }
}



