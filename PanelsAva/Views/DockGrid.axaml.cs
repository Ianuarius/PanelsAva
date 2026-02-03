using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PanelsAva.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PanelsAva.Views;

public enum DockEdge
{
	None,
	Left,
	Right,
	Bottom
}

public class TabGroup
{
	public List<PanelTabGroup> Panels { get; } = new();
	public int ActiveIndex { get; set; }
	public PanelTabGroup ActivePanel => ActiveIndex >= 0 && ActiveIndex < Panels.Count ? Panels[ActiveIndex] : null!;

	public void AddPanel(PanelTabGroup panel)
	{
		if (!Panels.Contains(panel))
		{
			Panels.Add(panel);
			panel.TabGroup = this;
		}
	}

	public void RemovePanel(PanelTabGroup panel)
	{
		var index = Panels.IndexOf(panel);
		if (index >= 0)
		{
			Panels.RemoveAt(index);
			panel.TabGroup = null;
			if (ActiveIndex >= Panels.Count)
				ActiveIndex = Math.Max(0, Panels.Count - 1);
		}
	}

	public void SetActive(PanelTabGroup panel)
	{
		var index = Panels.IndexOf(panel);
		if (index >= 0)
			ActiveIndex = index;
	}
}

public partial class DockGrid : UserControl
{
	Grid? panelsGrid;
	List<TabGroup> dockedItems = new();
	int lastDockedItemsCount;

	public static readonly StyledProperty<bool> IsHorizontalProperty = AvaloniaProperty.Register<DockGrid, bool>(nameof(IsHorizontal), false);

	public bool IsHorizontal
	{
		get => GetValue(IsHorizontalProperty);
		set => SetValue(IsHorizontalProperty, value);
	}

	public bool HasPanels => dockedItems.Count > 0;

	public DockEdge DockEdge { get; set; }
	public double PreviewDockWidth { get; set; }
	public double PreviewDockHeight { get; set; }

	public event EventHandler? DockedItemsChanged;
	public event EventHandler? LayoutChanged;

	public DockGrid()
	{
		InitializeComponent();
		panelsGrid = this.FindControl<Grid>("PanelsGrid");
	}

	/// <summary>
	/// Captures the current docking layout by creating a DockGridLayout object that includes the dock edge,
	/// the list of docked tab groups with their active panels and panel titles, and the proportional sizes of each group.
	/// </summary>
	/// <returns>A DockGridLayout representing the current state of the dock grid.</returns>
	public DockGridLayout GetLayout()
	{
		var layout = new DockGridLayout
		{
			DockEdge = DockEdge.ToString()
		};

		for (int i = 0; i < dockedItems.Count; i++)
		{
			var tg = dockedItems[i];
			var item = new DockHostItemLayout
			{
				ActiveIndex = tg.ActiveIndex
			};
			for (int j = 0; j < tg.Panels.Count; j++)
				item.Panels.Add(tg.Panels[j].Title);
			layout.Items.Add(item);
		}

		layout.ItemSizes = GetItemSizes();
		return layout;
	}

	/// <summary>Applies a saved DockGridLayout to restore the dock grid state, reconstructing tab groups and panels using the provided resolvePanel function.</summary>
	/// <param name="layout">The layout configuration to apply.</param>
	/// <param name="resolvePanel">A function that takes a panel title string and returns the corresponding PanelTabGroup instance.</param>
	public void ApplyLayout(DockGridLayout? layout, Func<string, PanelTabGroup?> resolvePanel)
	{
		dockedItems.Clear();
		if (layout != null)
		{
			for (int i = 0; i < layout.Items.Count; i++)
			{
				var item = layout.Items[i];
				var tg = new TabGroup();
				for (int j = 0; j < item.Panels.Count; j++)
				{
					var panel = resolvePanel(item.Panels[j]);
					if (panel != null)
					{
						panel.DockGrid = this;
						tg.AddPanel(panel);
					}
				}
				if (tg.Panels.Count > 0)
				{
					tg.ActiveIndex = Math.Clamp(item.ActiveIndex, 0, Math.Max(0, tg.Panels.Count - 1));
					dockedItems.Add(tg);
				}
			}
		}
		RebuildGrid();
		ApplyItemSizes(layout);
	}

	public void ClearPanels()
	{
		dockedItems.Clear();
		RebuildGrid();
	}

	public void RemovePanel(PanelTabGroup panel)
	{
		RemovePanelFromDockedItems(panel);
		RebuildGrid();
	}

	void RemovePanelFromDockedItems(PanelTabGroup panel)
	{
		for (int i = 0; i < dockedItems.Count; i++)
		{
			var tg = dockedItems[i];
			if (tg.Panels.Contains(panel))
			{
				tg.RemovePanel(panel);
				if (tg.Panels.Count == 0)
					dockedItems.RemoveAt(i);
				break;
			}
		}
	}

	bool EnsurePanelNotDuplicated(PanelTabGroup panel)
	{
		for (int i = 0; i < dockedItems.Count; i++)
		{
			var tg = dockedItems[i];
			if (tg.Panels.Contains(panel))
				return true;
		}
		return false;
	}

	public void AddPanel(PanelTabGroup panel)
	{
		if (!EnsurePanelNotDuplicated(panel))
			dockedItems.Add(CreateTabGroup(panel));
		RebuildGrid();
	}

	public void Dock(PanelTabGroup panel, Point positionInHost)
	{
		if (panelsGrid == null) return;

		MainView.RemoveFromParent(panel);
		RemovePanelFromDockedItems(panel);

		var targetIndex = FindTargetIndex(positionInHost);
		dockedItems.Insert(targetIndex, CreateTabGroup(panel));
		RebuildGrid();
	}

	public void DockAsTab(PanelTabGroup panel, PanelTabGroup targetPanel)
	{
		if (panelsGrid == null) return;

		MainView.RemoveFromParent(panel);
		RemovePanelFromDockedItems(panel);

		for (int i = 0; i < dockedItems.Count; i++)
		{
			var tg = dockedItems[i];
			if (tg.Panels.Contains(targetPanel))
			{
				tg.AddPanel(panel);
				break;
			}
		}
		RebuildGrid();
	}

	public Rect GetDockPreviewRect(Point positionInHost)
	{
		double hostWidth = Bounds.Width > 0 ? Bounds.Width : PreviewDockWidth;
		double hostHeight = Bounds.Height > 0 ? Bounds.Height : PreviewDockHeight;
		if (hostWidth <= 0) hostWidth = Bounds.Width;
		if (hostHeight <= 0) hostHeight = Bounds.Height;
		if (hostWidth <= 0 || hostHeight <= 0) return new Rect(0, 0, 0, 0);

		int targetIndex = FindTargetIndex(positionInHost);
		int newCount = dockedItems.Count + 1;
		int splitterCount = newCount - 1;
		double splitterSize = 4;
		double totalSplitterSize = splitterCount * splitterSize;
		double availableSize = IsHorizontal ? hostWidth - totalSplitterSize : hostHeight - totalSplitterSize;
		if (availableSize <= 0) availableSize = IsHorizontal ? hostWidth : hostHeight;
		double panelSize = availableSize / newCount;
		if (IsHorizontal)
		{
			double x = targetIndex * (panelSize + splitterSize);
			return new Rect(x, 0, panelSize, hostHeight);
		}
		else
		{
			double y = targetIndex * (panelSize + splitterSize);
			return new Rect(0, y, hostWidth, panelSize);
		}
	}

	int FindTargetIndex(Point positionInHost)
	{
		var panelRects = GetPanelRectsInHost();
		if (panelRects.Count == 0) return 0;

		for (int i = 0; i < panelRects.Count; i++)
		{
			var rect = panelRects[i];
			if (IsHorizontal)
			{
				var midX = rect.X + rect.Width * 0.5;
				if (positionInHost.X < midX)
				{
					return i;
				}
			}
			else
			{
				var midY = rect.Y + rect.Height * 0.5;
				if (positionInHost.Y < midY)
				{
					return i;
				}
			}
		}

		return panelRects.Count;
	}

	List<Rect> GetPanelRectsInHost()
	{
		var rects = new List<Rect>();
		for (int i = 0; i < dockedItems.Count; i++)
		{
			var panel = dockedItems[i].ActivePanel;

			if (panel == null) continue;
			var topLeft = panel.TranslatePoint(new Point(0, 0), this);
			if (!topLeft.HasValue) continue;
			var size = panel.Bounds.Size;
			if (size.Width <= 0 || size.Height <= 0) continue;
			rects.Add(new Rect(topLeft.Value, size));
		}
		return rects;
	}

	/// <summary>Rebuilds the visual grid layout by clearing and reconstructing the panels grid with docked items, splitters, and updating the UI.</summary>
	public void RebuildGrid()
	{
		if (panelsGrid == null) return;

		panelsGrid.Children.Clear();
		if (IsHorizontal)
		{
			panelsGrid.ColumnDefinitions.Clear();
			panelsGrid.RowDefinitions.Clear();
		}
		else
		{
			panelsGrid.RowDefinitions.Clear();
			panelsGrid.ColumnDefinitions.Clear();
		}

		for (int i = 0; i < dockedItems.Count; i++)
		{
			if (i > 0)
				AddSplitter();

			var tabGroup = dockedItems[i];
			for (int j = 0; j < tabGroup.Panels.Count; j++)
				tabGroup.Panels[j].SetFloating(false);

			var activePanel = tabGroup.ActivePanel;
			if (activePanel != null)
			{
				AddPanelRowOrColumn(activePanel);
				ClearFloatingProperties(activePanel);
			}
			
			for (int j = 0; j < tabGroup.Panels.Count; j++)
				tabGroup.Panels[j].RefreshTabStrip();
		}
		panelsGrid.InvalidateMeasure();
		panelsGrid.InvalidateArrange();
		this.InvalidateMeasure();
		this.InvalidateArrange();

		if (lastDockedItemsCount != dockedItems.Count)
		{
			lastDockedItemsCount = dockedItems.Count;
			DockedItemsChanged?.Invoke(this, EventArgs.Empty);
		}
		LayoutChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Adds a grid splitter to the panels grid for resizing docked panels.</summary>
	void AddSplitter()
	{
		if (panelsGrid == null) return;

		double thickness = 4;
		var splitter = new GridSplitter
		{
			Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(100, 100, 100)),
			ResizeDirection = IsHorizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows
		};

		if (IsHorizontal)
		{
			panelsGrid.ColumnDefinitions.Add(new ColumnDefinition(thickness, GridUnitType.Pixel));
			splitter.Width = thickness;
			Grid.SetColumn(splitter, panelsGrid.ColumnDefinitions.Count - 1);
		}
		else
		{
			panelsGrid.RowDefinitions.Add(new RowDefinition(thickness, GridUnitType.Pixel));
			splitter.Height = thickness;
			Grid.SetRow(splitter, panelsGrid.RowDefinitions.Count - 1);
		}

		splitter.PointerReleased += SplitterOnPointerReleased;
		splitter.PointerCaptureLost += SplitterOnPointerCaptureLost;
		panelsGrid.Children.Add(splitter);
	}

	/// <summary>Adds a new column or row definition to the grid and places the panel in it, depending on the horizontal orientation.</summary>
	/// <param name="panel">The panel to add to the grid.</param>
	void AddPanelRowOrColumn(PanelTabGroup panel)
	{
		if (panelsGrid == null) return;

		if (IsHorizontal)
		{
			var colDef = new ColumnDefinition(1, GridUnitType.Star);
			colDef.MinWidth = 50;
			panelsGrid.ColumnDefinitions.Add(colDef);
			Grid.SetColumn(panel, panelsGrid.ColumnDefinitions.Count - 1);
		}
		else
		{
			var rowDef = new RowDefinition(1, GridUnitType.Star);
			rowDef.MinHeight = 50;
			panelsGrid.RowDefinitions.Add(rowDef);
			Grid.SetRow(panel, panelsGrid.RowDefinitions.Count - 1);
		}
		panelsGrid.Children.Add(panel);
	}

	/// <summary>Calculates the proportional sizes of docked items based on their current dimensions, returning a list of doubles where each value is the fraction (0-1) of total space each item occupies.</summary>
	/// <returns>A list of doubles summing to 1, each representing the proportional size of a docked item.</returns>
	List<double> GetItemSizes()
	{
		var sizes = new List<double>();
		var lengths = new List<double>();
		for (int i = 0; i < dockedItems.Count; i++)
		{
			var panel = dockedItems[i].ActivePanel;
			
			if (panel == null)
			{
				lengths.Add(1);
				continue;
			}

			var size = IsHorizontal ? panel.Bounds.Width : panel.Bounds.Height;
			if (size <= 0)
				size = 1;
			lengths.Add(size);
		}
		double total = lengths.Sum();
		for (int i = 0; i < lengths.Count; i++)
		{
			sizes.Add(total > 0 ? lengths[i] / total : 1.0 / lengths.Count);
		}
		return sizes;
	}

	TabGroup CreateTabGroup(PanelTabGroup panel)
	{
		var tg = new TabGroup();
		tg.AddPanel(panel);
		tg.ActiveIndex = 0;
		return tg;
	}

	/// <summary>Applies the proportional sizes from the layout to the grid column or row definitions.</summary>
	/// <param name="layout">The layout containing the item sizes to apply.</param>
	void ApplyItemSizes(DockGridLayout? layout)
	{
		if (layout == null || panelsGrid == null) return;
		if (layout.ItemSizes.Count == 0) return;
		if (IsHorizontal)
		{
			for (int i = 0; i < layout.ItemSizes.Count; i++)
			{
				int colIndex = i * 2;
				if (colIndex >= 0 && colIndex < panelsGrid.ColumnDefinitions.Count)
				{
					var size = layout.ItemSizes[i];
					panelsGrid.ColumnDefinitions[colIndex].Width = new GridLength(size > 0 ? size : 1, GridUnitType.Star);
				}
			}
		}
		else
		{
			for (int i = 0; i < layout.ItemSizes.Count; i++)
			{
				int rowIndex = i * 2;
				if (rowIndex >= 0 && rowIndex < panelsGrid.RowDefinitions.Count)
				{
					var size = layout.ItemSizes[i];
					panelsGrid.RowDefinitions[rowIndex].Height = new GridLength(size > 0 ? size : 1, GridUnitType.Star);
				}
			}
		}
	}

	void SplitterOnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		LayoutChanged?.Invoke(this, EventArgs.Empty);
	}

	void SplitterOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		LayoutChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Resets the panel's floating properties to prepare it for docked state.</summary>
	/// <param name="panel">The panel to clear floating properties from.</param>
	void ClearFloatingProperties(PanelTabGroup panel)
	{
		Canvas.SetLeft(panel, double.NaN);
		Canvas.SetTop(panel, double.NaN);
		panel.SetValue(Panel.ZIndexProperty, 0);
		panel.SetFloating(false);
		panel.ClearValue(Control.WidthProperty);
		panel.ClearValue(Control.HeightProperty);
	}
}
