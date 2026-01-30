using System;
using Avalonia.Controls;
using Avalonia.Input;
using PanelsAva.ViewModels;

namespace PanelsAva.Views;

public partial class PanelHost : UserControl
{
    public PanelHost()
    {
        InitializeComponent();
        
        AttachHandlers(this.FindControl<Border>("LeftZone")!);
        AttachHandlers(this.FindControl<Border>("RightZone")!);
        AttachHandlers(this.FindControl<Border>("CenterZone")!);
        AttachHandlers(this.FindControl<Border>("BottomZone")!);
    }

    void AttachHandlers(Border border)
    {
        border.AddHandler(DragDrop.DragOverEvent, OnZoneDragOver);
        border.AddHandler(DragDrop.DropEvent, OnZoneDrop);
    }

    void OnZoneDragOver(object? sender, DragEventArgs e)
    {
        var panel = GetPanelFromDrag(e);
        e.DragEffects = panel == null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    void OnZoneDrop(object? sender, DragEventArgs e)
    {
        var panel = GetPanelFromDrag(e);
        if (panel == null)
            return;

        if (sender is not Control control)
            return;

        var zone = GetZone(control.Tag?.ToString());
        if (zone == null)
            return;

        if (DataContext is MainViewModel viewModel)
            viewModel.PanelService.Dock(panel, zone.Value);
    }

    PanelViewModel? GetPanelFromDrag(DragEventArgs e)
    {
        var id = e.Data.Get("panelId") as string;
        if (id == null)
            return null;

        if (DataContext is not MainViewModel viewModel)
            return null;

        return viewModel.PanelService.Get(id);
    }

    DockZone? GetZone(string? value)
    {
        if (value == null)
            return null;

        return Enum.TryParse<DockZone>(value, out var zone) ? zone : null;
    }
}