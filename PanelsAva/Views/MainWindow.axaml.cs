using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;

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
	MenuItem? saveWorkspaceMenuItem;
	MenuItem? loadWorkspaceMenuItem;
	MenuItem? defaultWorkspaceMenuItem;
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
		saveWorkspaceMenuItem = this.FindControl<MenuItem>("SaveWorkspaceMenuItem");
		loadWorkspaceMenuItem = this.FindControl<MenuItem>("LoadWorkspaceMenuItem");
		defaultWorkspaceMenuItem = this.FindControl<MenuItem>("DefaultWorkspaceMenuItem");
		lockWorkspaceMenuItem = this.FindControl<MenuItem>("LockWorkspaceMenuItem");
		UpdateMenuChecks();
		RefreshWorkspaceMenu();
	}

	void OnMainViewLoaded(object? sender, RoutedEventArgs e)
	{
		UpdateMenuChecks();
		RefreshWorkspaceMenu();
	}

	void RefreshWorkspaceMenu()
	{
		if (mainView == null || loadWorkspaceMenuItem == null) return;
		var profiles = mainView.GetWorkspaceProfileNames();
		loadWorkspaceMenuItem.Items.Clear();
		foreach (var name in profiles)
		{
			var item = new MenuItem { Header = name, Tag = name };
			item.Click += OnLoadWorkspaceProfile;
			loadWorkspaceMenuItem.Items.Add(item);
		}
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

	public async void OnSaveWorkspace(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		var name = await ShowWorkspaceNameDialog();
		if (string.IsNullOrWhiteSpace(name)) return;
		var saved = mainView.SaveWorkspaceProfile(name);
		if (saved)
			RefreshWorkspaceMenu();
		UpdateMenuChecks();
	}

	public void OnLoadDefaultWorkspace(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		mainView.LoadDefaultWorkspace();
		UpdateMenuChecks();
	}

	public void OnLoadWorkspaceProfile(object? sender, RoutedEventArgs e)
	{
		if (mainView == null) return;
		if (sender is MenuItem item && item.Tag is string name)
		{
			mainView.LoadWorkspaceProfile(name);
			UpdateMenuChecks();
		}
	}

	Task<string?> ShowWorkspaceNameDialog()
	{
		var dialog = new Window
		{
			Title = "Save Workspace",
			Width = 360,
			Height = 160,
			CanResize = false,
			WindowStartupLocation = WindowStartupLocation.CenterOwner
		};

		var textBox = new TextBox { Watermark = "Profile name" };
		var okButton = new Button { Content = "Save", IsEnabled = false, Width = 80 };
		var cancelButton = new Button { Content = "Cancel", Width = 80 };

		textBox.TextChanged += (_, __) =>
		{
			okButton.IsEnabled = !string.IsNullOrWhiteSpace(textBox.Text);
		};

		okButton.Click += (_, __) => dialog.Close(textBox.Text?.Trim());
		cancelButton.Click += (_, __) => dialog.Close(null);

		var buttonPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		buttonPanel.Children.Add(cancelButton);
		buttonPanel.Children.Add(okButton);

		var panel = new StackPanel
		{
			Margin = new Thickness(12),
			Spacing = 10
		};
		panel.Children.Add(new TextBlock { Text = "Profile name" });
		panel.Children.Add(textBox);
		panel.Children.Add(buttonPanel);

		dialog.Content = panel;
		dialog.SystemDecorations = SystemDecorations.BorderOnly;
		dialog.SizeToContent = SizeToContent.Height;
		dialog.Padding = new Thickness(0);

		return dialog.ShowDialog<string?>(this);
	}

}
