using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PanelsAva.Views;

public partial class MainWindow : Window
{
	MainView? mainView;
	MenuItem? layersMenuItem;
	MenuItem? propertiesMenuItem;
	MenuItem? colorMenuItem;
	MenuItem? brushesMenuItem;
	MenuItem? historyMenuItem;
	MenuItem? timelineMenuItem;

	public MainWindow()
	{
		InitializeComponent();
		Loaded += OnLoaded;
	}

	void OnLoaded(object? sender, RoutedEventArgs e)
	{
		mainView = this.FindControl<MainView>("MainView");
		if (mainView != null) mainView.Loaded += OnMainViewLoaded;
		layersMenuItem = this.FindControl<MenuItem>("LayersMenuItem");
		propertiesMenuItem = this.FindControl<MenuItem>("PropertiesMenuItem");
		colorMenuItem = this.FindControl<MenuItem>("ColorMenuItem");
		brushesMenuItem = this.FindControl<MenuItem>("BrushesMenuItem");
		historyMenuItem = this.FindControl<MenuItem>("HistoryMenuItem");
		timelineMenuItem = this.FindControl<MenuItem>("TimelineMenuItem");
		UpdateMenuChecks();
	}

	void OnMainViewLoaded(object? sender, RoutedEventArgs e)
	{
		UpdateMenuChecks();
	}

	void UpdateMenuChecks()
	{
		if (mainView == null) return;
		if (layersMenuItem != null) layersMenuItem.IsChecked = mainView.IsLayersPanelVisible;
		if (propertiesMenuItem != null) propertiesMenuItem.IsChecked = mainView.IsPropertiesPanelVisible;
		if (colorMenuItem != null) colorMenuItem.IsChecked = mainView.IsColorPanelVisible;
		if (brushesMenuItem != null) brushesMenuItem.IsChecked = mainView.IsBrushesPanelVisible;
		if (historyMenuItem != null) historyMenuItem.IsChecked = mainView.IsHistoryPanelVisible;
		if (timelineMenuItem != null) timelineMenuItem.IsChecked = mainView.IsTimelinePanelVisible;
	}

	public void OnToggleLayers(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		var isVisible = mainView.ToggleLayersPanel();
		if (sender is MenuItem menuItem) menuItem.IsChecked = isVisible;
	}

	public void OnToggleProperties(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		var isVisible = mainView.TogglePropertiesPanel();
		if (sender is MenuItem menuItem) menuItem.IsChecked = isVisible;
	}

	public void OnToggleColor(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		var isVisible = mainView.ToggleColorPanel();
		if (sender is MenuItem menuItem) menuItem.IsChecked = isVisible;
	}

	public void OnToggleBrushes(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		var isVisible = mainView.ToggleBrushesPanel();
		if (sender is MenuItem menuItem) menuItem.IsChecked = isVisible;
	}

	public void OnToggleHistory(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		var isVisible = mainView.ToggleHistoryPanel();
		if (sender is MenuItem menuItem) menuItem.IsChecked = isVisible;
	}

	public void OnToggleTimeline(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		var isVisible = mainView.ToggleTimelinePanel();
		if (sender is MenuItem menuItem) menuItem.IsChecked = isVisible;
	}

}
