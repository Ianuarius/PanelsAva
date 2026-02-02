using Avalonia.Controls;
using Avalonia.VisualTree;
using System;
using PanelsAva.ViewModels;
using Avalonia.Media;
using Avalonia.Layout;
using System.Collections.Specialized;
using System.Collections.Generic;
using PanelsAva;
using Avalonia;
using PanelsAva.Models;
using Avalonia.Threading;

namespace PanelsAva.Views;

public partial class MainView : UserControl
{
	DockHost? leftDockHost;
	DockHost? rightDockHost;
	DockHost? bottomDockHost;
	Toolbar? toolbar;
	RowDefinition? topToolbarRow;
	RowDefinition? bottomToolbarRow;
	ColumnDefinition? leftToolbarColumn;
	ColumnDefinition? rightToolbarColumn;
	Grid? mainGrid;
	GridSplitter? leftDockSplitter;
	GridSplitter? rightDockSplitter;
	GridSplitter? bottomDockSplitter;
	GridLength leftDockWidth;
	GridLength rightDockWidth;
	GridLength bottomDockHeight;
	GridLength leftSplitterWidth;
	GridLength rightSplitterWidth;
	GridLength bottomSplitterHeight;
	double leftDockMinWidth = 100;
	double leftDockMaxWidth = double.MaxValue;
	double rightDockMinWidth = 100;
	double rightDockMaxWidth = double.MaxValue;
	double bottomDockMinHeight = 100;
	double bottomDockMaxHeight = double.MaxValue;
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
	readonly Dictionary<Document, FileTabItem> fileTabs = new();
	readonly HashSet<Document> floatingDocuments = new();
	readonly Dictionary<Document, FileTabFloatingPanel> floatingPanels = new();
	Border? fileTabPreview;
	LayoutConfig? layoutConfig;
	LayoutConfig? defaultLayoutConfig;
	WorkspaceProfiles? workspaceProfiles;
	string activeProfileName = string.Empty;
	const string defaultProfileName = "Default";
	const string legacyProfileName = "Last Workspace";
	DispatcherTimer? layoutSaveTimer;
	bool isApplyingLayout;
	bool isWorkspaceLocked;

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
		if (e.PropertyName == nameof(MainViewModel.CurrentDocumentIndex) || e.PropertyName == nameof(MainViewModel.CurrentDocument) || e.PropertyName == nameof(MainViewModel.SelectedDocument))
		{
			RefreshFileTabStrip();
			UpdateCanvas();
			UpdatePanelFileNames();
		}
	}

	void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RefreshFileTabStrip();
		UpdateCanvas();
		UpdatePanelFileNames();
	}

	void UpdateCanvas()
	{
		if (canvasImage == null) return;
		if (currentViewModel == null) return;
		var currentDoc = currentViewModel.CurrentDocument;
		canvasImage.Source = (currentDoc != null && !floatingDocuments.Contains(currentDoc)) ? currentDoc.Bitmap : null;
	}

	void OnToolbarPositionChanged(object? sender, ToolbarPosition position)
	{
		UpdateToolbarPosition(position);
		ScheduleLayoutSave();
	}

	void UpdateToolbarPosition(ToolbarPosition position)
	{
		if (toolbar == null || mainGrid == null) return;
		if (leftDockHost == null || rightDockHost == null || bottomDockHost == null) return;

		const double toolbarSize = 40;

		if (topToolbarRow != null) topToolbarRow.Height = new GridLength(0);
		if (bottomToolbarRow != null) bottomToolbarRow.Height = new GridLength(0);
		if (leftToolbarColumn != null) leftToolbarColumn.Width = new GridLength(0);
		if (rightToolbarColumn != null) rightToolbarColumn.Width = new GridLength(0);

		switch (position)
		{
			case ToolbarPosition.Top:
				if (topToolbarRow != null) topToolbarRow.Height = new GridLength(toolbarSize);
				Grid.SetRow(toolbar!, 0);
				Grid.SetColumn(toolbar!, 1);
				Grid.SetColumnSpan(toolbar!, 5);
				Grid.SetRowSpan(toolbar!, 1);
				Grid.SetRow(leftDockHost!, 1);
				Grid.SetRowSpan(leftDockHost!, 3);
				Grid.SetRow(leftDockSplitter!, 1);
				Grid.SetRowSpan(leftDockSplitter!, 3);
				Grid.SetRow(rightDockSplitter!, 1);
				Grid.SetRowSpan(rightDockSplitter!, 3);
				Grid.SetRow(rightDockHost!, 1);
				Grid.SetRowSpan(rightDockHost!, 3);
				Grid.SetRow(bottomDockSplitter!, 2);
				Grid.SetRow(bottomDockHost!, 3);
				break;
			case ToolbarPosition.Bottom:
				if (bottomToolbarRow != null) bottomToolbarRow.Height = new GridLength(toolbarSize);
				Grid.SetRow(toolbar!, 4);
				Grid.SetColumn(toolbar!, 1);
				Grid.SetColumnSpan(toolbar!, 5);
				Grid.SetRowSpan(toolbar!, 1);
				Grid.SetRow(leftDockHost!, 1);
				Grid.SetRowSpan(leftDockHost!, 3);
				Grid.SetRow(leftDockSplitter!, 1);
				Grid.SetRowSpan(leftDockSplitter!, 3);
				Grid.SetRow(rightDockSplitter!, 1);
				Grid.SetRowSpan(rightDockSplitter!, 3);
				Grid.SetRow(rightDockHost!, 1);
				Grid.SetRowSpan(rightDockHost!, 3);
				Grid.SetRow(bottomDockSplitter!, 2);
				Grid.SetRow(bottomDockHost!, 3);
				break;
			case ToolbarPosition.Left:
				if (leftToolbarColumn != null) leftToolbarColumn.Width = new GridLength(toolbarSize);
				Grid.SetRow(toolbar!, 1);
				Grid.SetColumn(toolbar!, 0);
				Grid.SetRowSpan(toolbar!, 3);
				Grid.SetColumnSpan(toolbar!, 1);
				Grid.SetRow(leftDockHost!, 1);
				Grid.SetColumn(leftDockHost!, 1);
				Grid.SetRowSpan(leftDockHost!, 3);
				Grid.SetRow(leftDockSplitter!, 1);
				Grid.SetColumn(leftDockSplitter!, 2);
				Grid.SetRowSpan(leftDockSplitter!, 3);
				Grid.SetRow(rightDockSplitter!, 1);
				Grid.SetColumn(rightDockSplitter!, 4);
				Grid.SetRowSpan(rightDockSplitter!, 3);
				Grid.SetRow(rightDockHost!, 1);
				Grid.SetColumn(rightDockHost!, 5);
				Grid.SetRowSpan(rightDockHost!, 3);
				Grid.SetRow(bottomDockSplitter!, 2);
				Grid.SetColumn(bottomDockSplitter!, 3);
				Grid.SetRow(bottomDockHost!, 3);
				Grid.SetColumn(bottomDockHost!, 3);
				break;
			case ToolbarPosition.Right:
				if (rightToolbarColumn != null) rightToolbarColumn.Width = new GridLength(toolbarSize);
				Grid.SetRow(toolbar!, 1);
				Grid.SetColumn(toolbar!, 6);
				Grid.SetRowSpan(toolbar!, 3);
				Grid.SetColumnSpan(toolbar!, 1);
				Grid.SetRow(leftDockHost!, 1);
				Grid.SetColumn(leftDockHost!, 1);
				Grid.SetRowSpan(leftDockHost!, 3);
				Grid.SetRow(leftDockSplitter!, 1);
				Grid.SetColumn(leftDockSplitter!, 2);
				Grid.SetRowSpan(leftDockSplitter!, 3);
				Grid.SetRow(rightDockSplitter!, 1);
				Grid.SetColumn(rightDockSplitter!, 4);
				Grid.SetRowSpan(rightDockSplitter!, 3);
				Grid.SetRow(rightDockHost!, 1);
				Grid.SetColumn(rightDockHost!, 5);
				Grid.SetRowSpan(rightDockHost!, 3);
				Grid.SetRow(bottomDockSplitter!, 2);
				Grid.SetColumn(bottomDockSplitter!, 3);
				Grid.SetRow(bottomDockHost!, 3);
				Grid.SetColumn(bottomDockHost!, 3);
				break;
		}
	}

	void OnLoaded(object? sender, EventArgs e)
	{
		leftDockHost = this.FindControl<DockHost>("LeftDockHost");
		rightDockHost = this.FindControl<DockHost>("RightDockHost");
		bottomDockHost = this.FindControl<DockHost>("BottomDockHost");
		toolbar = this.FindControl<Toolbar>("ToolbarControl");
		mainGrid = this.FindControl<Grid>("MainGrid");
		if (mainGrid != null)
		{
			topToolbarRow = mainGrid.RowDefinitions.Count > 0 ? mainGrid.RowDefinitions[0] : null;
			bottomToolbarRow = mainGrid.RowDefinitions.Count > 4 ? mainGrid.RowDefinitions[4] : null;
			leftToolbarColumn = mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions[0] : null;
			rightToolbarColumn = mainGrid.ColumnDefinitions.Count > 6 ? mainGrid.ColumnDefinitions[6] : null;
		}
		leftDockSplitter = this.FindControl<GridSplitter>("LeftDockSplitter");
		rightDockSplitter = this.FindControl<GridSplitter>("RightDockSplitter");
		bottomDockSplitter = this.FindControl<GridSplitter>("BottomDockSplitter");
		fileTabStrip = this.FindControl<StackPanel>("FileTabStrip");
		canvasImage = this.FindControl<Image>("CanvasImage");
		floatingLayer = FindFloatingLayer();
		if (toolbar != null)
		{
			toolbar.FloatingLayer = floatingLayer;
			toolbar.PositionChanged += OnToolbarPositionChanged;
			UpdateToolbarPosition(toolbar.Position);
		}
		InitDockSizes();
		HookDockHostEvents();
		if (leftDockHost != null) leftDockHost.DockEdge = DockEdge.Left;
		if (rightDockHost != null) rightDockHost.DockEdge = DockEdge.Right;
		if (bottomDockHost != null) bottomDockHost.DockEdge = DockEdge.Bottom;
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
		UpdateDockHostSizes();
		HookLayoutEvents();
		LoadAndApplyLayout();
		UpdatePanelFloatability();
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
