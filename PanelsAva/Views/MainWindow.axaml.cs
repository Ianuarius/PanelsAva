using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

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
	MenuItem? lockWorkspaceMenuItem;

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
		lockWorkspaceMenuItem = this.FindControl<MenuItem>("LockWorkspaceMenuItem");
		UpdateMenuChecks();
	}

	void OnMainViewLoaded(object? sender, RoutedEventArgs e)
	{
		UpdateMenuChecks();
	}

	void UpdateMenuChecks()
	{
		if (mainView == null) return;
		if (layersMenuItem != null)
		{
			var isChecked = mainView.IsLayersPanelVisible;
			var panel = new StackPanel { Orientation = Orientation.Horizontal };
			panel.Children.Add(new TextBlock { Text = isChecked ? "✓" : "", Width = 25, VerticalAlignment = VerticalAlignment.Center });
			panel.Children.Add(new TextBlock { Text = "Layers" });
			layersMenuItem.Header = panel;
		}
		if (propertiesMenuItem != null)
		{
			var isChecked = mainView.IsPropertiesPanelVisible;
			var panel = new StackPanel { Orientation = Orientation.Horizontal };
			panel.Children.Add(new TextBlock { Text = isChecked ? "✓" : "", Width = 25, VerticalAlignment = VerticalAlignment.Center });
			panel.Children.Add(new TextBlock { Text = "Properties" });
			propertiesMenuItem.Header = panel;
		}
		if (colorMenuItem != null)
		{
			var isChecked = mainView.IsColorPanelVisible;
			var panel = new StackPanel { Orientation = Orientation.Horizontal };
			panel.Children.Add(new TextBlock { Text = isChecked ? "✓" : "", Width = 25, VerticalAlignment = VerticalAlignment.Center });
			panel.Children.Add(new TextBlock { Text = "Color" });
			colorMenuItem.Header = panel;
		}
		if (brushesMenuItem != null)
		{
			var isChecked = mainView.IsBrushesPanelVisible;
			var panel = new StackPanel { Orientation = Orientation.Horizontal };
			panel.Children.Add(new TextBlock { Text = isChecked ? "✓" : "", Width = 25, VerticalAlignment = VerticalAlignment.Center });
			panel.Children.Add(new TextBlock { Text = "Brushes" });
			brushesMenuItem.Header = panel;
		}
		if (historyMenuItem != null)
		{
			var isChecked = mainView.IsHistoryPanelVisible;
			var panel = new StackPanel { Orientation = Orientation.Horizontal };
			panel.Children.Add(new TextBlock { Text = isChecked ? "✓" : "", Width = 25, VerticalAlignment = VerticalAlignment.Center });
			panel.Children.Add(new TextBlock { Text = "History" });
			historyMenuItem.Header = panel;
		}
		if (timelineMenuItem != null)
		{
			var isChecked = mainView.IsTimelinePanelVisible;
			var panel = new StackPanel { Orientation = Orientation.Horizontal };
			panel.Children.Add(new TextBlock { Text = isChecked ? "✓" : "", Width = 25, VerticalAlignment = VerticalAlignment.Center });
			panel.Children.Add(new TextBlock { Text = "Timeline" });
			timelineMenuItem.Header = panel;
		}
		if (lockWorkspaceMenuItem != null)
		{
			var isChecked = mainView.IsWorkspaceLocked;
			var panel = new StackPanel { Orientation = Orientation.Horizontal };
			panel.Children.Add(new TextBlock { Text = isChecked ? "✓" : "", Width = 25, VerticalAlignment = VerticalAlignment.Center });
			panel.Children.Add(new TextBlock { Text = isChecked ? "Unlock Workspace" : "Lock Workspace" });
			lockWorkspaceMenuItem.Header = panel;
		}
	}

	public void OnToggleLayers(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		mainView.ToggleLayersPanel();
		UpdateMenuChecks();
	}

	public void OnToggleProperties(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		mainView.TogglePropertiesPanel();
		UpdateMenuChecks();
	}

	public void OnToggleColor(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		mainView.ToggleColorPanel();
		UpdateMenuChecks();
	}

	public void OnToggleBrushes(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		mainView.ToggleBrushesPanel();
		UpdateMenuChecks();
	}

	public void OnToggleHistory(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		mainView.ToggleHistoryPanel();
		UpdateMenuChecks();
	}

	public void OnToggleTimeline(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		mainView.ToggleTimelinePanel();
		UpdateMenuChecks();
	}

	public void OnToggleLockWorkspace(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		mainView.ToggleWorkspaceLock();
		UpdateMenuChecks();
	}

}
