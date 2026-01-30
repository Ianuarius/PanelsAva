using Avalonia.Controls;
using Avalonia.Input;
using PanelsAva.ViewModels;

namespace PanelsAva.Views;

public partial class PanelContentView : UserControl
{
    public PanelContentView()
    {
        InitializeComponent();
    }

    async void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (DataContext is not PanelViewModel panel)
            return;

        var data = new DataObject();
        data.Set("panelId", panel.Id);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        e.Handled = true;
    }
}