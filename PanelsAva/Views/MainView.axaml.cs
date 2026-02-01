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
