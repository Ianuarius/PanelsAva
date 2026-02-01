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

namespace PanelsAva.Views;

public partial class MainView : UserControl
{
	DockHost? leftDockHost;
	DockHost? rightDockHost;
	DockHost? bottomDockHost;
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
	double leftDockMinWidth;
	double leftDockMaxWidth;
	double rightDockMinWidth;
	double rightDockMaxWidth;
	double bottomDockMinHeight;
	double bottomDockMaxHeight;
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
		UpdateCanvas();
		UpdatePanelFileNames();
	}

	void UpdateCanvas()
	{
		if (canvasImage == null) return;
		if (currentViewModel == null) return;
		var currentDoc = currentViewModel.CurrentDocument;
		if (currentDoc != null && floatingDocuments.Contains(currentDoc))
		{
			var newIndex = GetFirstDockedDocumentIndex();
			if (newIndex != currentViewModel.CurrentDocumentIndex)
				currentViewModel.CurrentDocumentIndex = newIndex;
		}
		canvasImage.Source = currentViewModel.CurrentDocument?.Bitmap;
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
			var isActive = i == vm.CurrentDocumentIndex;

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
		ClearDockPreview();
	}

	void EnsureFloatingPanel(Document doc)
	{
		if (floatingLayer == null) return;
		var panel = GetOrCreateFloatingPanel(doc);
		if (!floatingLayer.Children.Contains(panel))
			floatingLayer.Children.Add(panel);
		panel.UpdateFromDocument();
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

	public void SelectDocument(Document doc)
	{
		if (currentViewModel == null) return;
		var index = currentViewModel.OpenDocuments.IndexOf(doc);
		if (index >= 0)
			currentViewModel.CurrentDocumentIndex = index;
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
		mainGrid = this.FindControl<Grid>("MainGrid");
		leftDockSplitter = this.FindControl<GridSplitter>("LeftDockSplitter");
		rightDockSplitter = this.FindControl<GridSplitter>("RightDockSplitter");
		bottomDockSplitter = this.FindControl<GridSplitter>("BottomDockSplitter");
		fileTabStrip = this.FindControl<StackPanel>("FileTabStrip");
		canvasImage = this.FindControl<Image>("CanvasImage");
		floatingLayer = FindFloatingLayer();
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
		if (mainGrid.ColumnDefinitions.Count >= 5)
		{
			var leftCol = mainGrid.ColumnDefinitions[0];
			var leftSplitCol = mainGrid.ColumnDefinitions[1];
			var rightCol = mainGrid.ColumnDefinitions[4];
			var rightSplitCol = mainGrid.ColumnDefinitions[3];
			leftDockWidth = leftCol.Width;
			rightDockWidth = rightCol.Width;
			leftSplitterWidth = leftSplitCol.Width;
			rightSplitterWidth = rightSplitCol.Width;
			leftDockMinWidth = leftCol.MinWidth;
			leftDockMaxWidth = leftCol.MaxWidth;
			rightDockMinWidth = rightCol.MinWidth;
			rightDockMaxWidth = rightCol.MaxWidth;
		}
		if (mainGrid.RowDefinitions.Count >= 3)
		{
			var splitRow = mainGrid.RowDefinitions[1];
			var bottomRow = mainGrid.RowDefinitions[2];
			bottomSplitterHeight = splitRow.Height;
			bottomDockHeight = bottomRow.Height;
			bottomDockMinHeight = bottomRow.MinHeight;
			bottomDockMaxHeight = bottomRow.MaxHeight;
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
		if (mainGrid.ColumnDefinitions.Count < 2) return;
		var leftCol = mainGrid.ColumnDefinitions[0];
		var splitCol = mainGrid.ColumnDefinitions[1];
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
			leftDockMinWidth = leftCol.MinWidth;
			leftDockMaxWidth = leftCol.MaxWidth;
			leftCol.MinWidth = 0;
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
		if (mainGrid.ColumnDefinitions.Count < 5) return;
		var rightCol = mainGrid.ColumnDefinitions[4];
		var splitCol = mainGrid.ColumnDefinitions[3];
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
			rightDockMinWidth = rightCol.MinWidth;
			rightDockMaxWidth = rightCol.MaxWidth;
			rightCol.MinWidth = 0;
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
		if (mainGrid.RowDefinitions.Count < 3) return;
		var splitRow = mainGrid.RowDefinitions[1];
		var bottomRow = mainGrid.RowDefinitions[2];
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
			bottomDockMinHeight = bottomRow.MinHeight;
			bottomDockMaxHeight = bottomRow.MaxHeight;
			bottomRow.MinHeight = 0;
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
