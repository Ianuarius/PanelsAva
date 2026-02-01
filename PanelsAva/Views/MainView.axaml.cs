using Avalonia.Controls;
using Avalonia.VisualTree;
using System;
using PanelsAva.ViewModels;
using Avalonia.Media;
using Avalonia.Layout;
using System.Collections.Specialized;
using Avalonia.Input;
using System.Collections.Generic;
using PanelsAva;
using Avalonia;
using PanelsAva.Models;
using System.Text.Json;
using System.IO;
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

	void RefreshFileTabStrip()
	{
		if (fileTabStrip == null) return;
		if (currentViewModel == null) return;
		var vm = currentViewModel;

		var removeList = new List<Document>();
		foreach (var pair in fileTabs)
		{
			if (!vm.OpenDocuments.Contains(pair.Key))
				removeList.Add(pair.Key);
		}
		foreach (var doc in removeList)
			RemoveTabForDocument(doc);

		fileTabStrip.Children.Clear();

		for (int i = 0; i < vm.OpenDocuments.Count; i++)
		{
			var doc = vm.OpenDocuments[i];
			var isActive = doc == vm.SelectedDocument;

			if (!fileTabs.TryGetValue(doc, out var tab))
			{
				tab = new FileTabItem(this, doc);
				fileTabs[doc] = tab;
			}

			tab.SetActive(isActive);
			if (floatingDocuments.Contains(doc))
			{
				EnsureFloatingPanel(doc);
				continue;
			}
			tab.SetFloating(false);
			fileTabStrip.Children.Add(tab);
		}
		UpdateFloatingPanelActiveStates();
		ClearDockPreview();
	}

	void EnsureFloatingPanel(Document doc)
	{
		if (floatingLayer == null) return;
		var panel = GetOrCreateFloatingPanel(doc);
		if (!floatingLayer.Children.Contains(panel))
			floatingLayer.Children.Add(panel);
		panel.UpdateFromDocument();
		panel.SetActive(currentViewModel?.SelectedDocument == doc);
	}

	void UpdateFloatingPanelActiveStates()
	{
		if (currentViewModel == null) return;
		foreach (var pair in floatingPanels)
			pair.Value.SetActive(pair.Key == currentViewModel.SelectedDocument);
	}

	void RemoveTabForDocument(Document doc)
	{
		if (!fileTabs.TryGetValue(doc, out var tab)) return;
		if (fileTabStrip != null && fileTabStrip.Children.Contains(tab))
			fileTabStrip.Children.Remove(tab);
		fileTabs.Remove(doc);
		floatingDocuments.Remove(doc);
		if (floatingPanels.TryGetValue(doc, out var panel))
		{
			if (floatingLayer != null && floatingLayer.Children.Contains(panel))
				floatingLayer.Children.Remove(panel);
			floatingPanels.Remove(doc);
		}
	}

	public void SelectDocument(Document doc, bool updateCanvas)
	{
		if (currentViewModel == null) return;
		currentViewModel.SelectedDocument = doc;
		if (updateCanvas)
		{
			var index = currentViewModel.OpenDocuments.IndexOf(doc);
			if (index >= 0)
				currentViewModel.CurrentDocumentIndex = index;
		}
	}

	public void CloseDocument(Document doc)
	{
		if (currentViewModel == null) return;
		var index = currentViewModel.OpenDocuments.IndexOf(doc);
		if (index < 0) return;
		RemoveTabForDocument(doc);
		currentViewModel.OpenDocuments.RemoveAt(index);
		if (currentViewModel.OpenDocuments.Count == 0)
		{
			currentViewModel.CurrentDocumentIndex = -1;
			return;
		}
		if (currentViewModel.CurrentDocumentIndex > index)
			currentViewModel.CurrentDocumentIndex -= 1;
		else if (currentViewModel.CurrentDocumentIndex == index)
			currentViewModel.CurrentDocumentIndex = Math.Min(index, currentViewModel.OpenDocuments.Count - 1);
	}

	public void BeginFloatingTab(FileTabItem tab, Point posRoot, IPointer? pointer, double dragOffsetX, double dragOffsetY)
	{
		if (floatingLayer == null || fileTabStrip == null) return;
		if (!floatingDocuments.Contains(tab.Document))
			floatingDocuments.Add(tab.Document);
		if (fileTabStrip.Children.Contains(tab))
			fileTabStrip.Children.Remove(tab);

		var panel = GetOrCreateFloatingPanel(tab.Document);
		if (!floatingLayer.Children.Contains(panel))
			floatingLayer.Children.Add(panel);
		panel.UpdateFromDocument();

		if (pointer != null)
			panel.BeginExternalDrag(pointer, posRoot, dragOffsetX, dragOffsetY);

		if (currentViewModel != null && currentViewModel.CurrentDocument == tab.Document)
		{
			currentViewModel.CurrentDocumentIndex = GetFirstDockedDocumentIndex();
		}
	}

	public void MoveFloatingTab(FileTabItem tab, Point posRoot, double dragOffsetX, double dragOffsetY)
	{
		if (!floatingPanels.TryGetValue(tab.Document, out var panel)) return;
		MoveFloatingPanel(panel, posRoot, dragOffsetX, dragOffsetY);
	}

	public void TryDockFloatingTab(FileTabItem tab, Point posRoot)
	{
		if (!floatingPanels.TryGetValue(tab.Document, out var panel)) return;
		TryDockFloatingPanel(panel, posRoot);
	}

	public void MoveFloatingPanel(FileTabFloatingPanel panel, Point posRoot, double dragOffsetX, double dragOffsetY)
	{
		if (floatingLayer == null) return;
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;
		var floatingLayerPos = floatingLayer.TranslatePoint(new Point(0, 0), visualRoot);
		if (!floatingLayerPos.HasValue) return;

		var posInFloatingLayer = posRoot - floatingLayerPos.Value;
		var panelPos = new Point(posInFloatingLayer.X - dragOffsetX, posInFloatingLayer.Y - dragOffsetY);
		Canvas.SetLeft(panel, panelPos.X);
		Canvas.SetTop(panel, panelPos.Y);
	}

	public void TryDockFloatingPanel(FileTabFloatingPanel panel, Point posRoot)
	{
		if (fileTabStrip == null || floatingLayer == null) return;
		if (!IsPointOverFileTabStrip(posRoot)) return;

		floatingDocuments.Remove(panel.Document);
		if (floatingLayer.Children.Contains(panel))
			floatingLayer.Children.Remove(panel);
		if (!fileTabs.TryGetValue(panel.Document, out var tab))
			return;
		if (!fileTabStrip.Children.Contains(tab))
		{
			var index = GetInsertIndex(posRoot, true);
			if (index < 0 || index > fileTabStrip.Children.Count)
				index = fileTabStrip.Children.Count;
			fileTabStrip.Children.Insert(index, tab);
			ReorderDocument(panel.Document, index);
		}
		tab.SetFloating(false);
		ClearDockPreview();
		if (currentViewModel != null)
			currentViewModel.CurrentDocumentIndex = currentViewModel.OpenDocuments.IndexOf(panel.Document);
	}

	public void ReorderDockedTab(FileTabItem tab, Point posRoot)
	{
		if (fileTabStrip == null) return;
		var index = GetInsertIndex(posRoot, true);
		ReorderDocument(tab.Document, index);
		ClearDockPreview();
	}

	public void UpdateDockPreview(Point posRoot)
	{
		if (fileTabStrip == null) return;
		if (!IsPointOverFileTabStrip(posRoot))
		{
			ClearDockPreview();
			return;
		}

		var index = GetInsertIndex(posRoot, true);
		if (index < 0) index = 0;
		if (index > fileTabStrip.Children.Count) index = fileTabStrip.Children.Count;

		if (fileTabPreview == null)
		{
			fileTabPreview = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(64, 128, 255)),
				Width = 3,
				Margin = new Thickness(0)
			};
		}

		fileTabPreview.Height = Math.Max(6, fileTabStrip.Bounds.Height);
		if (!fileTabStrip.Children.Contains(fileTabPreview))
		{
			fileTabStrip.Children.Insert(index, fileTabPreview);
		}
		else
		{
			var currentIndex = fileTabStrip.Children.IndexOf(fileTabPreview);
			if (currentIndex != index)
			{
				fileTabStrip.Children.Remove(fileTabPreview);
				if (index > currentIndex) index--;
				fileTabStrip.Children.Insert(index, fileTabPreview);
			}
		}
	}

	public void ClearDockPreview()
	{
		if (fileTabStrip == null || fileTabPreview == null) return;
		if (fileTabStrip.Children.Contains(fileTabPreview))
			fileTabStrip.Children.Remove(fileTabPreview);
	}

	void ReorderDocument(Document doc, int newIndex)
	{
		if (currentViewModel == null) return;
		var oldIndex = currentViewModel.OpenDocuments.IndexOf(doc);
		if (oldIndex < 0) return;
		if (newIndex < 0) newIndex = 0;
		if (newIndex >= currentViewModel.OpenDocuments.Count)
			newIndex = currentViewModel.OpenDocuments.Count - 1;
		if (newIndex == oldIndex) return;
		currentViewModel.OpenDocuments.Move(oldIndex, newIndex);
		currentViewModel.CurrentDocumentIndex = newIndex;
	}

	int GetFirstDockedDocumentIndex()
	{
		if (currentViewModel == null) return -1;
		for (int i = 0; i < currentViewModel.OpenDocuments.Count; i++)
		{
			var doc = currentViewModel.OpenDocuments[i];
			if (!floatingDocuments.Contains(doc))
				return i;
		}
		return -1;
	}

	FileTabFloatingPanel GetOrCreateFloatingPanel(Document doc)
	{
		if (!floatingPanels.TryGetValue(doc, out var panel))
		{
			panel = new FileTabFloatingPanel(this, doc);
			floatingPanels[doc] = panel;
		}
		return panel;
	}

	bool IsPointOverFileTabStrip(Point posRoot)
	{
		if (fileTabStrip == null) return false;
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return false;
		var stripPos = fileTabStrip.TranslatePoint(new Point(0, 0), visualRoot);
		if (!stripPos.HasValue) return false;
		var rect = new Rect(stripPos.Value, fileTabStrip.Bounds.Size);
		return rect.Contains(posRoot);
	}

	int GetInsertIndex(Point posRoot, bool ignorePreview)
	{
		if (fileTabStrip == null) return 0;
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return 0;
		var stripPos = fileTabStrip.TranslatePoint(new Point(0, 0), visualRoot);
		if (!stripPos.HasValue) return 0;
		var local = posRoot - stripPos.Value;

		for (int i = 0; i < fileTabStrip.Children.Count; i++)
		{
			if (ignorePreview && ReferenceEquals(fileTabStrip.Children[i], fileTabPreview))
				continue;
			if (fileTabStrip.Children[i] is Control c)
			{
				var childPos = c.TranslatePoint(new Point(0, 0), fileTabStrip);
				if (childPos.HasValue)
				{
					var mid = childPos.Value.X + c.Bounds.Width / 2;
					if (local.X < mid)
						return i;
				}
			}
		}
		return fileTabStrip.Children.Count;
	}

	void UpdatePanelFileNames()
	{
		var name = currentViewModel?.SelectedDocument?.Name ?? string.Empty;
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

	void HookLayoutEvents()
	{
		HookDockHostLayoutEvents(leftDockHost);
		HookDockHostLayoutEvents(rightDockHost);
		HookDockHostLayoutEvents(bottomDockHost);
		HookPanelLayoutEvents(layersPanel);
		HookPanelLayoutEvents(propertiesPanel);
		HookPanelLayoutEvents(colorPanel);
		HookPanelLayoutEvents(brushesPanel);
		HookPanelLayoutEvents(historyPanel);
		HookPanelLayoutEvents(timelinePanel);
		HookSplitterEvents(leftDockSplitter);
		HookSplitterEvents(rightDockSplitter);
		HookSplitterEvents(bottomDockSplitter);
	}

	void HookDockHostLayoutEvents(DockHost? host)
	{
		if (host == null) return;
		host.LayoutChanged -= OnLayoutChanged;
		host.LayoutChanged += OnLayoutChanged;
	}

	void HookPanelLayoutEvents(DockablePanel? panel)
	{
		if (panel == null) return;
		panel.LayoutChanged -= OnLayoutChanged;
		panel.LayoutChanged += OnLayoutChanged;
	}

	void HookSplitterEvents(GridSplitter? splitter)
	{
		if (splitter == null) return;
		splitter.PointerReleased -= OnSplitterPointerReleased;
		splitter.PointerReleased += OnSplitterPointerReleased;
		splitter.PointerCaptureLost -= OnSplitterPointerCaptureLost;
		splitter.PointerCaptureLost += OnSplitterPointerCaptureLost;
	}

	void OnSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		ScheduleLayoutSave();
	}

	void OnSplitterPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		ScheduleLayoutSave();
	}

	void OnLayoutChanged(object? sender, EventArgs e)
	{
		ScheduleLayoutSave();
	}

	void LoadAndApplyLayout()
	{
		if (defaultLayoutConfig == null)
			defaultLayoutConfig = BuildLayoutConfig();
		workspaceProfiles = LoadWorkspaceProfiles();
		activeProfileName = workspaceProfiles.ActiveProfile;
		var config = GetProfileConfig(activeProfileName);
		if (config != null)
			ApplyLayoutConfig(config);
	}

	string GetLayoutConfigPath()
	{
		var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		return Path.Combine(root, "PanelsAva", "layout.json");
	}

	WorkspaceProfiles LoadWorkspaceProfiles()
	{
		var profiles = new WorkspaceProfiles();
		try
		{
			var path = GetLayoutConfigPath();
			if (File.Exists(path))
			{
				var json = File.ReadAllText(path);
				var existingProfiles = JsonSerializer.Deserialize<WorkspaceProfiles>(json);
				if (existingProfiles != null && existingProfiles.Profiles.Count > 0)
				{
					profiles = existingProfiles;
				}
				else
				{
					var legacy = JsonSerializer.Deserialize<LayoutConfig>(json);
					if (legacy != null)
					{
						profiles.Profiles[legacyProfileName] = legacy;
						profiles.ActiveProfile = legacyProfileName;
					}
				}
			}
		}
		catch
		{
		}
		profiles = EnsureDefaultProfile(profiles);
		WriteWorkspaceProfiles(profiles);
		return profiles;
	}

	WorkspaceProfiles EnsureDefaultProfile(WorkspaceProfiles profiles)
	{
		if (!profiles.Profiles.ContainsKey(defaultProfileName) && defaultLayoutConfig != null)
			profiles.Profiles[defaultProfileName] = defaultLayoutConfig;
		if (string.IsNullOrWhiteSpace(profiles.ActiveProfile) || !profiles.Profiles.ContainsKey(profiles.ActiveProfile))
			profiles.ActiveProfile = defaultProfileName;
		return profiles;
	}

	LayoutConfig? GetProfileConfig(string name)
	{
		if (workspaceProfiles == null) return null;
		if (workspaceProfiles.Profiles.TryGetValue(name, out var config))
			return config;
		if (IsDefaultProfile(name) && defaultLayoutConfig != null)
			return defaultLayoutConfig;
		return null;
	}

	bool IsDefaultProfile(string name)
	{
		return string.Equals(name, defaultProfileName, StringComparison.OrdinalIgnoreCase);
	}

	void WriteWorkspaceProfiles(WorkspaceProfiles profiles)
	{
		try
		{
			var path = GetLayoutConfigPath();
			var dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);
			var json = JsonSerializer.Serialize(profiles);
			File.WriteAllText(path, json);
		}
		catch
		{
		}
	}

	void ScheduleLayoutSave()
	{
		if (isApplyingLayout) return;
		if (layoutSaveTimer == null)
		{
			layoutSaveTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(300)
			};
			layoutSaveTimer.Tick += OnLayoutSaveTimerTick;
		}
		layoutSaveTimer.Stop();
		layoutSaveTimer.Start();
	}

	void OnLayoutSaveTimerTick(object? sender, EventArgs e)
	{
		if (layoutSaveTimer != null)
			layoutSaveTimer.Stop();
		SaveLayoutConfig();
	}

	void SaveLayoutConfig()
	{
		if (workspaceProfiles == null)
		{
			var config = BuildLayoutConfig();
			layoutConfig = config;
			return;
		}
		if (string.IsNullOrWhiteSpace(activeProfileName))
			activeProfileName = workspaceProfiles.ActiveProfile;
		var activeConfig = BuildLayoutConfig();
		layoutConfig = activeConfig;
		if (IsDefaultProfile(activeProfileName))
		{
			defaultLayoutConfig = activeConfig;
			workspaceProfiles.Profiles[defaultProfileName] = activeConfig;
			WriteWorkspaceProfiles(workspaceProfiles);
			return;
		}
		workspaceProfiles.Profiles[activeProfileName] = activeConfig;
		WriteWorkspaceProfiles(workspaceProfiles);
	}

	LayoutConfig BuildLayoutConfig()
	{
		var config = new LayoutConfig();
		config.LeftDockHost = leftDockHost?.GetLayout();
		config.RightDockHost = rightDockHost?.GetLayout();
		config.BottomDockHost = bottomDockHost?.GetLayout();
		config.LeftDockWidth = GetLeftDockWidth();
		config.RightDockWidth = GetRightDockWidth();
		config.BottomDockHeight = GetBottomDockHeight();
		if (toolbar != null)
			config.ToolbarPosition = toolbar.Position.ToString();

		var existingStates = new Dictionary<string, PanelState>();
		if (layoutConfig != null)
		{
			for (int i = 0; i < layoutConfig.Panels.Count; i++)
				existingStates[layoutConfig.Panels[i].Title] = layoutConfig.Panels[i];
		}

		var states = new Dictionary<string, PanelState>();
		ApplyDockHostStates(config.LeftDockHost, states, existingStates);
		ApplyDockHostStates(config.RightDockHost, states, existingStates);
		ApplyDockHostStates(config.BottomDockHost, states, existingStates);

		var panels = GetAllPanels();
		for (int i = 0; i < panels.Count; i++)
		{
			var panel = panels[i];
			if (panel.IsFloating)
			{
				var state = GetOrCreateState(panel.Title, states, existingStates, panel);
				state.IsHidden = false;
				state.IsFloating = true;
				state.IsTabbed = false;
				var left = Canvas.GetLeft(panel);
				var top = Canvas.GetTop(panel);
				state.FloatingLeft = double.IsNaN(left) ? 0 : left;
				state.FloatingTop = double.IsNaN(top) ? 0 : top;
				state.FloatingWidth = panel.Bounds.Width;
				state.FloatingHeight = panel.Bounds.Height;
			}
		}

		for (int i = 0; i < panels.Count; i++)
		{
			var panel = panels[i];
			if (!states.ContainsKey(panel.Title))
			{
				var state = GetOrCreateState(panel.Title, states, existingStates, panel);
				state.IsHidden = true;
			}
		}

		config.Panels = new List<PanelState>(states.Values);
		return config;
	}

	void ApplyDockHostStates(DockHostLayout? layout, Dictionary<string, PanelState> states, Dictionary<string, PanelState> existingStates)
	{
		if (layout == null) return;
		for (int i = 0; i < layout.Items.Count; i++)
		{
			var item = layout.Items[i];
			for (int j = 0; j < item.Panels.Count; j++)
			{
				var title = item.Panels[j];
				var state = GetOrCreateState(title, states, existingStates, null);
				state.IsHidden = false;
				state.IsFloating = false;
				state.IsTabbed = item.Panels.Count > 1;
				state.DockEdge = layout.DockEdge;
				state.DockIndex = i;
				state.TabIndex = j;
				state.WasActive = item.ActiveIndex == j;
				if (i < layout.ItemSizes.Count)
					state.DockedProportion = layout.ItemSizes[i];
			}
		}
	}

	PanelState GetOrCreateState(string title, Dictionary<string, PanelState> states, Dictionary<string, PanelState> existingStates, DockablePanel? panel)
	{
		if (states.TryGetValue(title, out var state))
			return state;
		if (existingStates.TryGetValue(title, out var existing))
		{
			state = new PanelState
			{
				Title = existing.Title,
				IsHidden = existing.IsHidden,
				IsFloating = existing.IsFloating,
				IsTabbed = existing.IsTabbed,
				DockEdge = existing.DockEdge,
				DockIndex = existing.DockIndex,
				TabIndex = existing.TabIndex,
				WasActive = existing.WasActive,
				FloatingLeft = existing.FloatingLeft,
				FloatingTop = existing.FloatingTop,
				FloatingWidth = existing.FloatingWidth,
				FloatingHeight = existing.FloatingHeight,
				DockedProportion = existing.DockedProportion
			};
			states[title] = state;
			return state;
		}
		state = new PanelState
		{
			Title = title
		};
		if (panel != null && panel.DockHost != null)
			state.DockEdge = panel.DockHost.DockEdge.ToString();
		states[title] = state;
		return state;
	}

	double GetLeftDockWidth()
	{
		if (mainGrid == null || mainGrid.ColumnDefinitions.Count < 2) return leftDockWidth.Value;
		var leftCol = mainGrid.ColumnDefinitions[1];
		return leftCol.Width.Value > 0 ? leftCol.Width.Value : leftDockWidth.Value;
	}

	double GetRightDockWidth()
	{
		if (mainGrid == null || mainGrid.ColumnDefinitions.Count < 6) return rightDockWidth.Value;
		var rightCol = mainGrid.ColumnDefinitions[5];
		return rightCol.Width.Value > 0 ? rightCol.Width.Value : rightDockWidth.Value;
	}

	double GetBottomDockHeight()
	{
		if (mainGrid == null || mainGrid.RowDefinitions.Count < 4) return bottomDockHeight.Value;
		var bottomRow = mainGrid.RowDefinitions[3];
		return bottomRow.Height.Value > 0 ? bottomRow.Height.Value : bottomDockHeight.Value;
	}

	void ApplyLayoutConfig(LayoutConfig config)
	{
		if (mainGrid == null) return;
		isApplyingLayout = true;
		try
		{
			layoutConfig = config;
			if (mainGrid.ColumnDefinitions.Count >= 6)
			{
				if (config.LeftDockWidth > 0)
					mainGrid.ColumnDefinitions[1].Width = new GridLength(config.LeftDockWidth, GridUnitType.Pixel);
				if (config.RightDockWidth > 0)
					mainGrid.ColumnDefinitions[5].Width = new GridLength(config.RightDockWidth, GridUnitType.Pixel);
				mainGrid.ColumnDefinitions[2].Width = leftSplitterWidth;
				mainGrid.ColumnDefinitions[4].Width = rightSplitterWidth;
			}
			if (mainGrid.RowDefinitions.Count >= 4)
			{
				if (config.BottomDockHeight > 0)
					mainGrid.RowDefinitions[3].Height = new GridLength(config.BottomDockHeight, GridUnitType.Pixel);
				mainGrid.RowDefinitions[2].Height = bottomSplitterHeight;
			}
			if (toolbar != null && !string.IsNullOrEmpty(config.ToolbarPosition))
			{
				if (Enum.TryParse<ToolbarPosition>(config.ToolbarPosition, out var pos))
				{
					toolbar.Position = pos;
					UpdateToolbarPosition(pos);
				}
			}

			ClearAllPanels();
			ApplyDockHostLayout(leftDockHost, config.LeftDockHost);
			ApplyDockHostLayout(rightDockHost, config.RightDockHost);
			ApplyDockHostLayout(bottomDockHost, config.BottomDockHost);
			ApplyFloatingPanels(config);
			UpdateDockHostSizes();
		}
		finally
		{
			isApplyingLayout = false;
		}
	}

	void ClearAllPanels()
	{
		var panels = GetAllPanels();
		for (int i = 0; i < panels.Count; i++)
			RemoveFromParent(panels[i]);
			
		leftDockHost?.ClearPanels();
		rightDockHost?.ClearPanels();
		bottomDockHost?.ClearPanels();
	}

	void RemoveFromParent(Control control)
	{
		if (control.Parent is Panel panel)
		{
			panel.Children.Remove(control);
			return;
		}
		if (control.Parent is ContentControl contentControl)
		{
			contentControl.Content = null;
			return;
		}
	}

	void ApplyDockHostLayout(DockHost? host, DockHostLayout? layout)
	{
		if (host == null) return;
		NormalizeItemSizes(layout);
		host.ApplyLayout(layout, FindPanelByTitle);
	}

	void NormalizeItemSizes(DockHostLayout? layout)
	{
		if (layout == null) return;
		while (layout.ItemSizes.Count < layout.Items.Count)
			layout.ItemSizes.Add(0);
		if (layout.ItemSizes.Count > layout.Items.Count)
			layout.ItemSizes.RemoveRange(layout.Items.Count, layout.ItemSizes.Count - layout.Items.Count);
	}

	PanelState? GetPanelStateFromConfig(LayoutConfig config, string title)
	{
		for (int i = 0; i < config.Panels.Count; i++)
		{
			if (config.Panels[i].Title == title)
				return config.Panels[i];
		}
		return null;
	}

	void RemovePanelFromDockHostLayout(DockHostLayout? layout, string title)
	{
		if (layout == null) return;
		for (int i = layout.Items.Count - 1; i >= 0; i--)
		{
			var item = layout.Items[i];
			int index = item.Panels.IndexOf(title);
			if (index >= 0)
			{
				item.Panels.RemoveAt(index);
				if (item.ActiveIndex >= item.Panels.Count)
					item.ActiveIndex = Math.Max(0, item.Panels.Count - 1);
				if (item.Panels.Count == 0)
				{
					layout.Items.RemoveAt(i);
					if (layout.ItemSizes.Count > i)
						layout.ItemSizes.RemoveAt(i);
				}
				else if (item.Panels.Count == 1)
				{
					item.ActiveIndex = 0;
				}
			}
		}
		// Renormalize proportions so remaining items sum to 1
		if (layout.ItemSizes.Count > 0)
		{
			double total = 0;
			for (int i = 0; i < layout.ItemSizes.Count; i++)
				total += layout.ItemSizes[i];
			if (total > 0)
			{
				for (int i = 0; i < layout.ItemSizes.Count; i++)
					layout.ItemSizes[i] = layout.ItemSizes[i] / total;
			}
			else
			{
				var equal = 1.0 / layout.ItemSizes.Count;
				for (int i = 0; i < layout.ItemSizes.Count; i++)
					layout.ItemSizes[i] = equal;
			}
		}
		NormalizeItemSizes(layout);
	}

	void ApplyFloatingPanels(LayoutConfig config)
	{
		if (floatingLayer == null) return;
		for (int i = 0; i < config.Panels.Count; i++)
		{
			var state = config.Panels[i];
			if (state.IsHidden || !state.IsFloating) continue;
			var panel = FindPanelByTitle(state.Title);
			if (panel == null) continue;
			panel.SetFloatingBounds(floatingLayer, state.FloatingLeft, state.FloatingTop, state.FloatingWidth, state.FloatingHeight);
		}
	}

	DockablePanel? FindPanelByTitle(string title)
	{
		var panels = GetAllPanels();
		for (int i = 0; i < panels.Count; i++)
		{
			if (panels[i].Title == title)
				return panels[i];
		}
		return null;
	}

	List<DockablePanel> GetAllPanels()
	{
		var list = new List<DockablePanel>();
		if (layersPanel != null) list.Add(layersPanel);
		if (propertiesPanel != null) list.Add(propertiesPanel);
		if (colorPanel != null) list.Add(colorPanel);
		if (brushesPanel != null) list.Add(brushesPanel);
		if (historyPanel != null) list.Add(historyPanel);
		if (timelinePanel != null) list.Add(timelinePanel);
		return list;
	}

	void HookDockHostEvents()
	{
		if (leftDockHost != null)
		{
			leftDockHost.DockedItemsChanged -= OnDockedItemsChanged;
			leftDockHost.DockedItemsChanged += OnDockedItemsChanged;
		}
		if (rightDockHost != null)
		{
			rightDockHost.DockedItemsChanged -= OnDockedItemsChanged;
			rightDockHost.DockedItemsChanged += OnDockedItemsChanged;
		}
		if (bottomDockHost != null)
		{
			bottomDockHost.DockedItemsChanged -= OnDockedItemsChanged;
			bottomDockHost.DockedItemsChanged += OnDockedItemsChanged;
		}
	}

	void OnDockedItemsChanged(object? sender, EventArgs e)
	{
		UpdateDockHostSizes();
	}

	void InitDockSizes()
	{
		if (mainGrid == null) return;
		if (mainGrid.ColumnDefinitions.Count >= 6)
		{
			var leftCol = mainGrid.ColumnDefinitions[1];
			var leftSplitCol = mainGrid.ColumnDefinitions[2];
			var rightCol = mainGrid.ColumnDefinitions[5];
			var rightSplitCol = mainGrid.ColumnDefinitions[4];
			leftDockWidth = leftCol.Width;
			rightDockWidth = rightCol.Width;
			leftSplitterWidth = leftSplitCol.Width;
			rightSplitterWidth = rightSplitCol.Width;
		}
		if (mainGrid.RowDefinitions.Count >= 4)
		{
			var splitRow = mainGrid.RowDefinitions[2];
			var bottomRow = mainGrid.RowDefinitions[3];
			bottomSplitterHeight = splitRow.Height;
			bottomDockHeight = bottomRow.Height;
		}
	}

	void UpdateDockHostSizes()
	{
		if (mainGrid == null) return;
		UpdateLeftDockSize();
		UpdateRightDockSize();
		UpdateBottomDockSize();
	}

	void UpdateLeftDockSize()
	{
		if (leftDockHost == null || mainGrid == null) return;
		if (mainGrid.ColumnDefinitions.Count < 3) return;
		var leftCol = mainGrid.ColumnDefinitions[1];
		var splitCol = mainGrid.ColumnDefinitions[2];
		var hasPanels = leftDockHost.HasPanels;
		if (hasPanels)
		{
			if (leftCol.Width.Value > 0)
				leftDockWidth = leftCol.Width;
			leftCol.MinWidth = leftDockMinWidth;
			leftCol.MaxWidth = leftDockMaxWidth;
			if (leftCol.Width.Value == 0)
				leftCol.Width = leftDockWidth;
			splitCol.Width = leftSplitterWidth;
			leftDockHost.PreviewDockWidth = leftDockWidth.Value;
			leftDockHost.PreviewDockHeight = leftDockHost.Bounds.Height;
			if (leftDockSplitter != null)
			{
				leftDockSplitter.IsVisible = true;
				leftDockSplitter.IsEnabled = true;
			}
		}
		else
		{
			if (leftCol.Width.Value > 0)
				leftDockWidth = leftCol.Width;
			leftCol.MinWidth = 0;
			leftCol.MaxWidth = double.MaxValue;
			leftCol.Width = new GridLength(0);
			splitCol.Width = new GridLength(0);
			leftDockHost.PreviewDockWidth = leftDockWidth.Value;
			leftDockHost.PreviewDockHeight = leftDockHost.Bounds.Height;
			if (leftDockSplitter != null)
			{
				leftDockSplitter.IsVisible = false;
				leftDockSplitter.IsEnabled = false;
			}
		}
	}

	void UpdateRightDockSize()
	{
		if (rightDockHost == null || mainGrid == null) return;
		if (mainGrid.ColumnDefinitions.Count < 6) return;
		var rightCol = mainGrid.ColumnDefinitions[5];
		var splitCol = mainGrid.ColumnDefinitions[4];
		var hasPanels = rightDockHost.HasPanels;
		if (hasPanels)
		{
			if (rightCol.Width.Value > 0)
				rightDockWidth = rightCol.Width;
			rightCol.MinWidth = rightDockMinWidth;
			rightCol.MaxWidth = rightDockMaxWidth;
			if (rightCol.Width.Value == 0)
				rightCol.Width = rightDockWidth;
			splitCol.Width = rightSplitterWidth;
			rightDockHost.PreviewDockWidth = rightDockWidth.Value;
			rightDockHost.PreviewDockHeight = rightDockHost.Bounds.Height;
			if (rightDockSplitter != null)
			{
				rightDockSplitter.IsVisible = true;
				rightDockSplitter.IsEnabled = true;
			}
		}
		else
		{
			if (rightCol.Width.Value > 0)
				rightDockWidth = rightCol.Width;
			rightCol.MinWidth = 0;
			rightCol.MaxWidth = double.MaxValue;
			rightCol.Width = new GridLength(0);
			splitCol.Width = new GridLength(0);
			rightDockHost.PreviewDockWidth = rightDockWidth.Value;
			rightDockHost.PreviewDockHeight = rightDockHost.Bounds.Height;
			if (rightDockSplitter != null)
			{
				rightDockSplitter.IsVisible = false;
				rightDockSplitter.IsEnabled = false;
			}
		}
	}

	void UpdateBottomDockSize()
	{
		if (bottomDockHost == null || mainGrid == null) return;
		if (mainGrid.RowDefinitions.Count < 4) return;
		var splitRow = mainGrid.RowDefinitions[2];
		var bottomRow = mainGrid.RowDefinitions[3];
		var hasPanels = bottomDockHost.HasPanels;
		if (hasPanels)
		{
			if (bottomRow.Height.Value > 0)
				bottomDockHeight = bottomRow.Height;
			bottomRow.MinHeight = bottomDockMinHeight;
			bottomRow.MaxHeight = bottomDockMaxHeight;
			if (bottomRow.Height.Value == 0)
				bottomRow.Height = bottomDockHeight;
			splitRow.Height = bottomSplitterHeight;
			bottomDockHost.PreviewDockWidth = bottomDockHost.Bounds.Width;
			bottomDockHost.PreviewDockHeight = bottomDockHeight.Value;
			if (bottomDockSplitter != null)
			{
				bottomDockSplitter.IsVisible = true;
				bottomDockSplitter.IsEnabled = true;
			}
		}
		else
		{
			if (bottomRow.Height.Value > 0)
				bottomDockHeight = bottomRow.Height;
			bottomRow.MinHeight = 0;
			bottomRow.MaxHeight = double.MaxValue;
			bottomRow.Height = new GridLength(0);
			splitRow.Height = new GridLength(0);
			bottomDockHost.PreviewDockWidth = bottomDockHost.Bounds.Width;
			bottomDockHost.PreviewDockHeight = bottomDockHeight.Value;
			if (bottomDockSplitter != null)
			{
				bottomDockSplitter.IsVisible = false;
				bottomDockSplitter.IsEnabled = false;
			}
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

	public bool ToggleWorkspaceLock()
	{
		isWorkspaceLocked = !isWorkspaceLocked;
		UpdatePanelFloatability();
		return isWorkspaceLocked;
	}

	void UpdatePanelFloatability()
	{
		var canFloat = !isWorkspaceLocked;
		if (layersPanel != null) layersPanel.CanFloat = canFloat;
		if (propertiesPanel != null) propertiesPanel.CanFloat = canFloat;
		if (colorPanel != null) colorPanel.CanFloat = canFloat;
		if (brushesPanel != null) brushesPanel.CanFloat = canFloat;
		if (historyPanel != null) historyPanel.CanFloat = canFloat;
		if (timelinePanel != null) timelinePanel.CanFloat = canFloat;
	}

	public IReadOnlyList<string> GetWorkspaceProfileNames()
	{
		if (workspaceProfiles == null) return Array.Empty<string>();
		var list = new List<string>();
		foreach (var pair in workspaceProfiles.Profiles)
		{
			if (IsDefaultProfile(pair.Key)) continue;
			list.Add(pair.Key);
		}
		list.Sort(StringComparer.OrdinalIgnoreCase);
		return list;
	}

	public bool SaveWorkspaceProfile(string name)
	{
		if (workspaceProfiles == null) return false;
		var trimmed = name?.Trim();
		if (string.IsNullOrWhiteSpace(trimmed)) return false;
		if (IsDefaultProfile(trimmed)) return false;
		var config = BuildLayoutConfig();
		workspaceProfiles.Profiles[trimmed] = config;
		workspaceProfiles.ActiveProfile = trimmed;
		activeProfileName = trimmed;
		layoutConfig = config;
		WriteWorkspaceProfiles(workspaceProfiles);
		return true;
	}

	public bool LoadWorkspaceProfile(string name)
	{
		if (workspaceProfiles == null) return false;
		if (string.IsNullOrWhiteSpace(name)) return false;
		if (!workspaceProfiles.Profiles.ContainsKey(name) && !IsDefaultProfile(name)) return false;
		activeProfileName = name;
		workspaceProfiles.ActiveProfile = name;
		var config = GetProfileConfig(name);
		if (config == null) return false;
		ApplyLayoutConfig(config);
		WriteWorkspaceProfiles(workspaceProfiles);
		return true;
	}

	public bool LoadDefaultWorkspace()
	{
		return LoadWorkspaceProfile(defaultProfileName);
	}

	public bool IsLayersPanelVisible => IsPanelVisible(layersPanel);
	public bool IsPropertiesPanelVisible => IsPanelVisible(propertiesPanel);
	public bool IsColorPanelVisible => IsPanelVisible(colorPanel);
	public bool IsBrushesPanelVisible => IsPanelVisible(brushesPanel);
	public bool IsHistoryPanelVisible => IsPanelVisible(historyPanel);
	public bool IsTimelinePanelVisible => IsPanelVisible(timelinePanel);

	public bool IsWorkspaceLocked => isWorkspaceLocked;

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

	PanelState? GetPanelState(string title)
	{
		if (layoutConfig == null) return null;
		for (int i = 0; i < layoutConfig.Panels.Count; i++)
		{
			if (layoutConfig.Panels[i].Title == title)
				return layoutConfig.Panels[i];
		}
		return null;
	}

	DockHost? GetDockHostByEdge(string edge)
	{
		if (edge == DockEdge.Left.ToString()) return leftDockHost;
		if (edge == DockEdge.Right.ToString()) return rightDockHost;
		if (edge == DockEdge.Bottom.ToString()) return bottomDockHost;
		return null;
	}

	void ApplyPanelStateToDockHost(DockablePanel panel, PanelState state, DockHost host)
	{
		var layout = host.GetLayout();
		NormalizeItemSizes(layout);
		int dockIndex = Math.Clamp(state.DockIndex, 0, layout.Items.Count);
		if (state.IsTabbed && dockIndex < layout.Items.Count)
		{
			var item = layout.Items[dockIndex];
			int tabIndex = Math.Clamp(state.TabIndex, 0, item.Panels.Count);
			item.Panels.Insert(tabIndex, panel.Title);
			if (state.WasActive)
				item.ActiveIndex = tabIndex;
		}
		else
		{
			layout.Items.Insert(dockIndex, new DockHostItemLayout
			{
				Panels = new List<string> { panel.Title },
				ActiveIndex = 0
			});
			layout.ItemSizes.Insert(dockIndex, state.DockedProportion > 0 ? state.DockedProportion : 1.0);
		}
		host.ApplyLayout(layout, FindPanelByTitle);
	}

	bool IsPanelVisible(DockablePanel? panel)
	{
		return panel != null && panel.Parent != null;
	}

	void HidePanel(DockablePanel? panel)
	{
		if (panel == null) return;

		// Capture current docked proportion for this panel so we can restore it later
		var prevConfig = BuildLayoutConfig();
		var prevState = GetPanelStateFromConfig(prevConfig, panel.Title);
		if (prevState != null)
		{
			prevState.IsHidden = true;
			// keep DockedProportion in prevState as is
			layoutConfig = prevConfig;
		}

		if (panel.DockHost != null && panel.Parent is Grid)
		{
			var host = panel.DockHost;
			var layout = host.GetLayout();
			RemovePanelFromDockHostLayout(layout, panel.Title);
			host.ApplyLayout(layout, FindPanelByTitle);
			ScheduleLayoutSave();
			return;
		}
		if (panel.Parent is DockHost parentHost)
		{
			var layout = parentHost.GetLayout();
			RemovePanelFromDockHostLayout(layout, panel.Title);
			parentHost.ApplyLayout(layout, FindPanelByTitle);
			ScheduleLayoutSave();
			return;
		}
		if (panel.Parent is Canvas canvas)
		{
			canvas.Children.Remove(panel);
			ScheduleLayoutSave();
			return;
		}
		if (panel.Parent is Panel parentPanel)
		{
			parentPanel.Children.Remove(panel);
			ScheduleLayoutSave();
			return;
		}
		if (panel.Parent is ContentControl contentControl)
		{
			contentControl.Content = null;
			ScheduleLayoutSave();
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
		var state = GetPanelState(panel.Title);
		if (state != null)
		{
			state.IsHidden = false;
			if (state.IsFloating && floatingLayer != null)
			{
				panel.SetFloatingBounds(floatingLayer, state.FloatingLeft, state.FloatingTop, state.FloatingWidth, state.FloatingHeight);
				ScheduleLayoutSave();
				return;
			}
			var host = GetDockHostByEdge(state.DockEdge);
			if (host != null)
			{
				ApplyPanelStateToDockHost(panel, state, host);
				ScheduleLayoutSave();
				return;
			}
		}
		if (panel.DockHost != null)
			panel.DockHost.AddPanel(panel);
		else if (leftDockHost != null)
			leftDockHost.AddPanel(panel);
		ScheduleLayoutSave();
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
