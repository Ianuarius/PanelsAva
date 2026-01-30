using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using PanelsAva.Views;
using PanelsAva;

namespace PanelsAva.ViewModels;

public class MainViewModel : ViewModelBase
{
    readonly ObservableCollection<PanelViewModel> leftPanels = new();
    readonly ObservableCollection<PanelViewModel> rightPanels = new();
    readonly ObservableCollection<PanelViewModel> bottomPanels = new();

    public MainViewModel()
    {
        PanelService = new PanelService(new PanelFilePersistence("PanelsAva"));

        PanelService.Register(new PanelViewModel("tools", "Tools", new ToolsPanelView(), DockZone.Left));
        PanelService.Register(new PanelViewModel("layers", "Layers", new LayersPanelView(), DockZone.Right));
        PanelService.Register(new PanelViewModel("history", "History", new HistoryPanelView(), DockZone.Bottom));

        PanelMenuItems = new ObservableCollection<PanelMenuItemViewModel>(
        [
            new PanelMenuItemViewModel(PanelService.Get("tools")!),
            new PanelMenuItemViewModel(PanelService.Get("layers")!),
            new PanelMenuItemViewModel(PanelService.Get("history")!)
        ]);

        LeftPanels = new ReadOnlyObservableCollection<PanelViewModel>(leftPanels);
        RightPanels = new ReadOnlyObservableCollection<PanelViewModel>(rightPanels);
        BottomPanels = new ReadOnlyObservableCollection<PanelViewModel>(bottomPanels);

        PanelService.Panels.CollectionChanged += PanelsCollectionChanged;
        foreach (var panel in PanelService.Panels)
        {
            AttachPanel(panel);
        }

        RefreshZones();
    }

    public PanelService PanelService { get; }
    public ObservableCollection<PanelMenuItemViewModel> PanelMenuItems { get; }
    public ReadOnlyObservableCollection<PanelViewModel> LeftPanels { get; }
    public ReadOnlyObservableCollection<PanelViewModel> RightPanels { get; }
    public ReadOnlyObservableCollection<PanelViewModel> BottomPanels { get; }

    void PanelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (PanelViewModel panel in e.NewItems)
                AttachPanel(panel);
        }

        if (e.OldItems != null)
        {
            foreach (PanelViewModel panel in e.OldItems)
                DetachPanel(panel);
        }

        RefreshZones();
    }

    void AttachPanel(PanelViewModel panel)
    {
        panel.PropertyChanged += PanelPropertyChanged;
    }

    void DetachPanel(PanelViewModel panel)
    {
        panel.PropertyChanged -= PanelPropertyChanged;
    }

    void PanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PanelViewModel.IsVisible) or nameof(PanelViewModel.IsFloating) or nameof(PanelViewModel.DockZone))
            RefreshZones();
    }

    void RefreshZones()
    {
        UpdateCollection(leftPanels, DockZone.Left);
        UpdateCollection(rightPanels, DockZone.Right);
        UpdateCollection(bottomPanels, DockZone.Bottom);
    }

    void UpdateCollection(ObservableCollection<PanelViewModel> target, DockZone zone)
    {
        var panels = PanelService.Panels.Where(p => p.IsVisible && !p.IsFloating && p.DockZone == zone).ToList();
        target.Clear();
        foreach (var panel in panels)
            target.Add(panel);
    }
}
