using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PanelsAva.Models;
using PanelsAva.Services;
using PanelsAva.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace PanelsAva.Views;

public partial class MainView : UserControl
{
	DockGrid? leftDockGrid;
	DockGrid? rightDockGrid;
	DockGrid? bottomDockGrid;
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
	PanelTabGroup? layersPanel;
	PanelTabGroup? propertiesPanel;
	PanelTabGroup? colorPanel;
	PanelTabGroup? brushesPanel;
	PanelTabGroup? historyPanel;
	PanelTabGroup? timelinePanel;
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
	bool preserveLayoutOnSave;
	Dictionary<string, PanelState> panelStateCache = new();
	public DragManager? dragManager;

	public event EventHandler? PanelVisibilityChanged;

	public bool IsDragging => dragManager?.IsDragging ?? false;

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
		if (leftDockGrid == null || rightDockGrid == null || bottomDockGrid == null) return;

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
				Grid.SetRow(leftDockGrid!, 1);
				Grid.SetRowSpan(leftDockGrid!, 3);
				Grid.SetRow(leftDockSplitter!, 1);
				Grid.SetRowSpan(leftDockSplitter!, 3);
				Grid.SetRow(rightDockSplitter!, 1);
				Grid.SetRowSpan(rightDockSplitter!, 3);
				Grid.SetRow(rightDockGrid!, 1);
				Grid.SetRowSpan(rightDockGrid!, 3);
				Grid.SetRow(bottomDockSplitter!, 2);
				Grid.SetRow(bottomDockGrid!, 3);
				break;
			case ToolbarPosition.Bottom:
				if (bottomToolbarRow != null) bottomToolbarRow.Height = new GridLength(toolbarSize);
				Grid.SetRow(toolbar!, 4);
				Grid.SetColumn(toolbar!, 1);
				Grid.SetColumnSpan(toolbar!, 5);
				Grid.SetRowSpan(toolbar!, 1);
				Grid.SetRow(leftDockGrid!, 1);
				Grid.SetRowSpan(leftDockGrid!, 3);
				Grid.SetRow(leftDockSplitter!, 1);
				Grid.SetRowSpan(leftDockSplitter!, 3);
				Grid.SetRow(rightDockSplitter!, 1);
				Grid.SetRowSpan(rightDockSplitter!, 3);
				Grid.SetRow(rightDockGrid!, 1);
				Grid.SetRowSpan(rightDockGrid!, 3);
				Grid.SetRow(bottomDockSplitter!, 2);
				Grid.SetRow(bottomDockGrid!, 3);
				break;
			case ToolbarPosition.Left:
				if (leftToolbarColumn != null) leftToolbarColumn.Width = new GridLength(toolbarSize);
				Grid.SetRow(toolbar!, 1);
				Grid.SetColumn(toolbar!, 0);
				Grid.SetRowSpan(toolbar!, 3);
				Grid.SetColumnSpan(toolbar!, 1);
				Grid.SetRow(leftDockGrid!, 1);
				Grid.SetColumn(leftDockGrid!, 1);
				Grid.SetRowSpan(leftDockGrid!, 3);
				Grid.SetRow(leftDockSplitter!, 1);
				Grid.SetColumn(leftDockSplitter!, 2);
				Grid.SetRowSpan(leftDockSplitter!, 3);
				Grid.SetRow(rightDockSplitter!, 1);
				Grid.SetColumn(rightDockSplitter!, 4);
				Grid.SetRowSpan(rightDockSplitter!, 3);
				Grid.SetRow(rightDockGrid!, 1);
				Grid.SetColumn(rightDockGrid!, 5);
				Grid.SetRowSpan(rightDockGrid!, 3);
				Grid.SetRow(bottomDockSplitter!, 2);
				Grid.SetColumn(bottomDockSplitter!, 3);
				Grid.SetRow(bottomDockGrid!, 3);
				Grid.SetColumn(bottomDockGrid!, 3);
				break;
			case ToolbarPosition.Right:
				if (rightToolbarColumn != null) rightToolbarColumn.Width = new GridLength(toolbarSize);
				Grid.SetRow(toolbar!, 1);
				Grid.SetColumn(toolbar!, 6);
				Grid.SetRowSpan(toolbar!, 3);
				Grid.SetColumnSpan(toolbar!, 1);
				Grid.SetRow(leftDockGrid!, 1);
				Grid.SetColumn(leftDockGrid!, 1);
				Grid.SetRowSpan(leftDockGrid!, 3);
				Grid.SetRow(leftDockSplitter!, 1);
				Grid.SetColumn(leftDockSplitter!, 2);
				Grid.SetRowSpan(leftDockSplitter!, 3);
				Grid.SetRow(rightDockSplitter!, 1);
				Grid.SetColumn(rightDockSplitter!, 4);
				Grid.SetRowSpan(rightDockSplitter!, 3);
				Grid.SetRow(rightDockGrid!, 1);
				Grid.SetColumn(rightDockGrid!, 5);
				Grid.SetRowSpan(rightDockGrid!, 3);
				Grid.SetRow(bottomDockSplitter!, 2);
				Grid.SetColumn(bottomDockSplitter!, 3);
				Grid.SetRow(bottomDockGrid!, 3);
				Grid.SetColumn(bottomDockGrid!, 3);
				break;
		}
	}

	void OnLoaded(object? sender, EventArgs e)
	{
		leftDockGrid = this.FindControl<DockGrid>("LeftDockGrid");
		rightDockGrid = this.FindControl<DockGrid>("RightDockGrid");
		bottomDockGrid = this.FindControl<DockGrid>("BottomDockGrid");
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
		if (floatingLayer != null)
		{
			var visualRoot = this.GetVisualRoot() as Visual;
			if (visualRoot != null)
				dragManager = new DragManager(visualRoot, floatingLayer, this);
			floatingLayer.PointerMoved += (s, e) =>
			{
				if (dragManager == null || !dragManager.IsDragging) return;
				var root = this.GetVisualRoot() as Visual;
				if (root == null) return;
				dragManager.UpdateDrag(e.GetPosition(root));
			};
			floatingLayer.PointerReleased += (s, e) =>
			{
				if (dragManager == null || !dragManager.IsDragging) return;
				var root = this.GetVisualRoot() as Visual;
				if (root == null) return;
				dragManager.EndDrag(e.GetPosition(root));
			};
		}
		if (toolbar != null)
		{
			toolbar.FloatingLayer = floatingLayer;
			toolbar.SetMainView(this);
			toolbar.PositionChanged += OnToolbarPositionChanged;
			UpdateToolbarPosition(toolbar.Position);
		}
		InitDockSizes();
		HookDockGridEvents();
		if (leftDockGrid != null) leftDockGrid.DockEdge = DockEdge.Left;
		if (rightDockGrid != null) rightDockGrid.DockEdge = DockEdge.Right;
		if (bottomDockGrid != null) bottomDockGrid.DockEdge = DockEdge.Bottom;
		if (DataContext == null && this.Parent is Control parentControl && parentControl.DataContext != null)
			DataContext = parentControl.DataContext;
		SetViewModel(DataContext as MainViewModel);

		if (layersPanel != null) return;
		
		if (leftDockGrid != null && floatingLayer != null)
		{
			layersPanel = new PanelTabGroup
			{
				Title = "Layers",
				Content = new LayersPanel(),
				DockGrid = leftDockGrid,
				FloatingLayer = floatingLayer
			};
			layersPanel.SetMainView(this);
			layersPanel.CloseRequested += OnPanelCloseRequested;
			leftDockGrid.AddPanel(layersPanel);

			propertiesPanel = new PanelTabGroup
			{
				Title = "Properties",
				Content = new PropertiesPanel(),
				DockGrid = leftDockGrid,
				FloatingLayer = floatingLayer
			};
			propertiesPanel.SetMainView(this);
			propertiesPanel.CloseRequested += OnPanelCloseRequested;
			leftDockGrid.AddPanel(propertiesPanel);

			colorPanel = new PanelTabGroup
			{
				Title = "Color",
				Content = new ColorPanel(),
				DockGrid = leftDockGrid,
				FloatingLayer = floatingLayer
			};
			colorPanel.SetMainView(this);
			colorPanel.CloseRequested += OnPanelCloseRequested;
			leftDockGrid.AddPanel(colorPanel);

			brushesPanel = new PanelTabGroup
			{
				Title = "Brushes",
				Content = new BrushesPanel(),
				DockGrid = leftDockGrid,
				FloatingLayer = floatingLayer
			};
			brushesPanel.SetMainView(this);
			brushesPanel.CloseRequested += OnPanelCloseRequested;
			leftDockGrid.AddPanel(brushesPanel);
		}

		if (rightDockGrid != null && floatingLayer != null)
		{
			historyPanel = new PanelTabGroup
			{
				Title = "History",
				Content = new HistoryPanel(),
				DockGrid = rightDockGrid,
				FloatingLayer = floatingLayer
			};
			historyPanel.SetMainView(this);
			historyPanel.CloseRequested += OnPanelCloseRequested;
			rightDockGrid.AddPanel(historyPanel);
		}

		if (bottomDockGrid != null && floatingLayer != null)
		{
			timelinePanel = new PanelTabGroup
			{
				Title = "Timeline",
				Content = new TimelinePanel(),
				DockGrid = bottomDockGrid,
				FloatingLayer = floatingLayer
			};
			timelinePanel.SetMainView(this);
			timelinePanel.CloseRequested += OnPanelCloseRequested;
			bottomDockGrid.AddPanel(timelinePanel);
		}
		UpdateDockHostSizes();
		HookLayoutEvents();
		LoadAndApplyLayout();
		UpdatePanelFloatability();
	}

	PanelTabGroup CreatePanel(string title, Control content, DockGrid host, Canvas floatingLayer)
	{
		var panel = new PanelTabGroup
		{
			Title = title,
			Content = content,
			DockGrid = host,
			FloatingLayer = floatingLayer
		};
		panel.CloseRequested += OnPanelCloseRequested;
		host.AddPanel(panel);
		return panel;
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
