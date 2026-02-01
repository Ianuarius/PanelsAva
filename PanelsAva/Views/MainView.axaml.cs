using Avalonia.Controls;
using Avalonia.VisualTree;
using System;
using PanelsAva.ViewModels;
using Avalonia.Media;
using Avalonia.Layout;
using System.Collections.Specialized;
using Avalonia.Input;

namespace PanelsAva.Views;

public partial class MainView : UserControl
{
	DockHost? leftDockHost;
	DockHost? rightDockHost;
	DockHost? bottomDockHost;
	Canvas? floatingLayer;
	DockablePanel? layersPanel;
	DockablePanel? propertiesPanel;
	DockablePanel? colorPanel;
	DockablePanel? brushesPanel;
	DockablePanel? historyPanel;
	DockablePanel? timelinePanel;
	StackPanel? fileTabStrip;
	Image? canvasImage;
	MainViewModel? currentViewModel;

	public MainView()
	{
		InitializeComponent();
		Loaded += OnLoaded;
		DataContextChanged += OnDataContextChanged;
	}

	void OnDataContextChanged(object? sender, EventArgs e)
	{
		SetViewModel(DataContext as MainViewModel);
	}

	void SetViewModel(MainViewModel? vm)
	{
		if (currentViewModel != null)
		{
			currentViewModel.OpenDocuments.CollectionChanged -= OnDocumentsChanged;
			currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
		}

		currentViewModel = vm;

		if (currentViewModel != null)
		{
			currentViewModel.OpenDocuments.CollectionChanged += OnDocumentsChanged;
			currentViewModel.PropertyChanged += OnViewModelPropertyChanged;
		}

		RefreshFileTabStrip();
		UpdateCanvas();
		UpdatePanelFileNames();
	}

	void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(MainViewModel.CurrentDocumentIndex) || e.PropertyName == nameof(MainViewModel.CurrentDocument))
		{
			RefreshFileTabStrip();
			UpdateCanvas();
			UpdatePanelFileNames();
		}
	}

	void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RefreshFileTabStrip();
	}

	void UpdateCanvas()
	{
		if (canvasImage == null) return;
		if (currentViewModel == null) return;
		canvasImage.Source = currentViewModel.CurrentDocument?.Bitmap;
	}

	void RefreshFileTabStrip()
	{
		if (fileTabStrip == null) return;
		if (currentViewModel == null) return;
		var vm = currentViewModel;

		fileTabStrip.Children.Clear();

		for (int i = 0; i < vm.OpenDocuments.Count; i++)
		{
			var doc = vm.OpenDocuments[i];
			var isActive = i == vm.CurrentDocumentIndex;

			var tabBorder = new Border
			{
				Background = new SolidColorBrush(isActive ? Color.FromRgb(70, 70, 100) : Color.FromRgb(50, 50, 70)),
				Padding = new Avalonia.Thickness(8, 2, 8, 2),
				BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 50)),
				BorderThickness = new Avalonia.Thickness(1, 0, 1, 0),
				Tag = i
			};

			var tabText = new TextBlock
			{
				Text = doc.Name,
				FontSize = 12,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Left,
				Foreground = new SolidColorBrush(Colors.White)
			};
			tabBorder.Child = tabText;

			tabBorder.PointerPressed += FileTabOnPointerPressed;

			fileTabStrip.Children.Add(tabBorder);
		}
	}

	void FileTabOnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is Border border && border.Tag is int index && currentViewModel != null)
		{
			currentViewModel.CurrentDocumentIndex = index;
		}
	}

	void UpdatePanelFileNames()
	{
		var name = currentViewModel?.CurrentDocument?.Name ?? string.Empty;
		if (layersPanel?.Content is LayersPanel layersPanelView && layersPanelView.DataContext is ViewModels.LayersViewModel layersVm)
			layersVm.CurrentFileName = name;
		if (propertiesPanel?.Content is PropertiesPanel propertiesPanelView && propertiesPanelView.DataContext is ViewModels.PropertiesViewModel propertiesVm)
			propertiesVm.CurrentFileName = name;
		if (colorPanel?.Content is ColorPanel colorPanelView && colorPanelView.DataContext is ViewModels.ColorViewModel colorVm)
			colorVm.CurrentFileName = name;
		if (brushesPanel?.Content is BrushesPanel brushesPanelView && brushesPanelView.DataContext is ViewModels.BrushesViewModel brushesVm)
			brushesVm.CurrentFileName = name;
		if (historyPanel?.Content is HistoryPanel historyPanelView && historyPanelView.DataContext is ViewModels.HistoryViewModel historyVm)
			historyVm.CurrentFileName = name;
		if (timelinePanel?.Content is TimelinePanel timelinePanelView && timelinePanelView.DataContext is ViewModels.TimelineViewModel timelineVm)
			timelineVm.CurrentFileName = name;
	}

	void OnLoaded(object? sender, EventArgs e)
	{
		leftDockHost = this.FindControl<DockHost>("LeftDockHost");
		rightDockHost = this.FindControl<DockHost>("RightDockHost");
		bottomDockHost = this.FindControl<DockHost>("BottomDockHost");
		fileTabStrip = this.FindControl<StackPanel>("FileTabStrip");
		canvasImage = this.FindControl<Image>("CanvasImage");
		floatingLayer = FindFloatingLayer();
		if (DataContext == null && this.Parent is Control parentControl && parentControl.DataContext != null)
			DataContext = parentControl.DataContext;
		SetViewModel(DataContext as MainViewModel);

		if (layersPanel != null) return;
		
		if (leftDockHost != null && floatingLayer != null)
		{
			layersPanel = new DockablePanel
			{
				Title = "Layers",
				Content = new LayersPanel(),
				DockHost = leftDockHost,
				FloatingLayer = floatingLayer
			};
			layersPanel.CloseRequested += OnPanelCloseRequested;
			leftDockHost.AddPanel(layersPanel);

			propertiesPanel = new DockablePanel
			{
				Title = "Properties",
				Content = new PropertiesPanel(),
				DockHost = leftDockHost,
				FloatingLayer = floatingLayer
			};
			propertiesPanel.CloseRequested += OnPanelCloseRequested;
			leftDockHost.AddPanel(propertiesPanel);

			colorPanel = new DockablePanel
			{
				Title = "Color",
				Content = new ColorPanel(),
				DockHost = leftDockHost,
				FloatingLayer = floatingLayer
			};
			colorPanel.CloseRequested += OnPanelCloseRequested;
			leftDockHost.AddPanel(colorPanel);

			brushesPanel = new DockablePanel
			{
				Title = "Brushes",
				Content = new BrushesPanel(),
				DockHost = leftDockHost,
				FloatingLayer = floatingLayer
			};
			brushesPanel.CloseRequested += OnPanelCloseRequested;
			leftDockHost.AddPanel(brushesPanel);
		}

		if (rightDockHost != null && floatingLayer != null)
		{
			historyPanel = new DockablePanel
			{
				Title = "History",
				Content = new HistoryPanel(),
				DockHost = rightDockHost,
				FloatingLayer = floatingLayer
			};
			historyPanel.CloseRequested += OnPanelCloseRequested;
			rightDockHost.AddPanel(historyPanel);
		}

		if (bottomDockHost != null && floatingLayer != null)
		{
			timelinePanel = new DockablePanel
			{
				Title = "Timeline",
				Content = new TimelinePanel(),
				DockHost = bottomDockHost,
				FloatingLayer = floatingLayer
			};
			timelinePanel.CloseRequested += OnPanelCloseRequested;
			bottomDockHost.AddPanel(timelinePanel);
		}
	}

	public bool ToggleLayersPanel()
	{
		return TogglePanel(layersPanel);
	}

	public bool TogglePropertiesPanel()
	{
		return TogglePanel(propertiesPanel);
	}

	public bool ToggleColorPanel()
	{
		return TogglePanel(colorPanel);
	}

	public bool ToggleBrushesPanel()
	{
		return TogglePanel(brushesPanel);
	}

	public bool ToggleHistoryPanel()
	{
		return TogglePanel(historyPanel);
	}

	public bool ToggleTimelinePanel()
	{
		return TogglePanel(timelinePanel);
	}

	public bool IsLayersPanelVisible => IsPanelVisible(layersPanel);
	public bool IsPropertiesPanelVisible => IsPanelVisible(propertiesPanel);
	public bool IsColorPanelVisible => IsPanelVisible(colorPanel);
	public bool IsBrushesPanelVisible => IsPanelVisible(brushesPanel);
	public bool IsHistoryPanelVisible => IsPanelVisible(historyPanel);
	public bool IsTimelinePanelVisible => IsPanelVisible(timelinePanel);

	bool TogglePanel(DockablePanel? panel)
	{
		if (panel == null) return false;
		if (IsPanelVisible(panel))
		{
			HidePanel(panel);
			return false;
		}
		ShowPanel(panel);
		return true;
	}

	bool IsPanelVisible(DockablePanel? panel)
	{
		return panel != null && panel.Parent != null;
	}

	void HidePanel(DockablePanel? panel)
	{
		if (panel == null) return;
		if (panel.DockHost != null && panel.Parent is Grid)
		{
			panel.DockHost.RemovePanel(panel);
			return;
		}
		if (panel.Parent is DockHost host)
		{
			host.RemovePanel(panel);
			return;
		}
		if (panel.Parent is Canvas canvas)
		{
			canvas.Children.Remove(panel);
			return;
		}
		if (panel.Parent is Panel parentPanel)
		{
			parentPanel.Children.Remove(panel);
			return;
		}
		if (panel.Parent is ContentControl contentControl)
		{
			contentControl.Content = null;
		}
	}

	void OnPanelCloseRequested(object? sender, EventArgs e)
	{
		if (sender is DockablePanel panel)
		{
			HidePanel(panel);
		}
	}

	void ShowPanel(DockablePanel? panel)
	{
		if (panel == null) return;
		if (panel.Parent != null) return;
		var host = panel.DockHost;
		if (host != null)
		{
			host.AddPanel(panel);
			return;
		}
		if (floatingLayer != null)
		{
			floatingLayer.Children.Add(panel);
		}
	}

	Canvas? FindFloatingLayer()
	{
		var grid = this.Parent as Grid;
		if (grid != null)
		{
			foreach (var child in grid.Children)
			{
				if (child is Canvas canvas && canvas.Name == "FloatingLayer")
					return canvas;
			}
		}
		return null;
	}
}
