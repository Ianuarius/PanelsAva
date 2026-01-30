using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using PanelsAva;

namespace PanelsAva.ViewModels;

public class PanelViewModel : ViewModelBase
{
    PanelService? panelService;
    string title;
    bool isVisible = true;
    bool isCollapsed;
    bool isFloating;
    DockZone dockZone;
    Rect? floatingBounds;
    Control content;

    public PanelViewModel(string id, string title, Control content, DockZone dockZone)
    {
        Id = id;
        this.title = title;
        this.content = content;
        this.dockZone = dockZone;

        ToggleVisibilityCommand = ReactiveCommand.Create(ToggleVisibilityInternal);
        ToggleCollapseCommand = ReactiveCommand.Create(ToggleCollapseInternal);
        FloatCommand = ReactiveCommand.Create(FloatInternal);
        DockCommand = ReactiveCommand.Create<DockZone>(DockInternal);
    }

    public string Id { get; }

    public string Title
    {
        get => title;
        set => this.RaiseAndSetIfChanged(ref title, value);
    }

    public Control Content
    {
        get => content;
        set => this.RaiseAndSetIfChanged(ref content, value);
    }

    public bool IsVisible
    {
        get => isVisible;
        set => this.RaiseAndSetIfChanged(ref isVisible, value);
    }

    public bool IsCollapsed
    {
        get => isCollapsed;
        set => this.RaiseAndSetIfChanged(ref isCollapsed, value);
    }

    public bool IsFloating
    {
        get => isFloating;
        set => this.RaiseAndSetIfChanged(ref isFloating, value);
    }

    public DockZone DockZone
    {
        get => dockZone;
        set => this.RaiseAndSetIfChanged(ref dockZone, value);
    }

    public Rect? FloatingBounds
    {
        get => floatingBounds;
        set => this.RaiseAndSetIfChanged(ref floatingBounds, value);
    }

    public ReactiveCommand<Unit, Unit> ToggleVisibilityCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCollapseCommand { get; }
    public ReactiveCommand<Unit, Unit> FloatCommand { get; }
    public ReactiveCommand<DockZone, Unit> DockCommand { get; }

    internal void Connect(PanelService service)
    {
        panelService = service;
    }

    void ToggleVisibilityInternal()
    {
        panelService?.Toggle(Id);
    }

    void ToggleCollapseInternal()
    {
        IsCollapsed = !IsCollapsed;
    }

    void FloatInternal()
    {
        panelService?.Float(this);
    }

    void DockInternal(DockZone zone)
    {
        panelService?.Dock(this, zone);
    }
}

public class PanelMenuItemViewModel : ViewModelBase
{
    public PanelMenuItemViewModel(PanelViewModel panel)
    {
        Panel = panel;
    }

    public PanelViewModel Panel { get; }
}