using Avalonia;
using Avalonia.Controls;
using PanelsAva.ViewModels;

namespace PanelsAva.Views;

public partial class PanelFloatingWindow : Window
{
    readonly PanelViewModel panel;
    readonly PanelService panelService;

    public PanelFloatingWindow(PanelViewModel panel, PanelService panelService)
    {
        InitializeComponent();
        this.panel = panel;
        this.panelService = panelService;
        DataContext = panel;

        PositionChanged += OnPositionChanged;
        SizeChanged += OnSizeChanged;
        Title = panel.Title;
    }

    void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        UpdateBounds();
    }

    void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateBounds();
    }

    void UpdateBounds()
    {
        var bounds = new Rect(Position.X, Position.Y, Width, Height);
        panelService.UpdateFloatingBounds(panel, bounds);
    }
}