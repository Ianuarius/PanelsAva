using Avalonia.Controls;
using Avalonia;
using System;
using System.Linq;
using System.Collections.Generic;
using Avalonia.VisualTree;
using System.Diagnostics;
using Avalonia.Input;
using PanelsAva.Models;

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
	public List<DockablePanel> Panels { get; } = new();
	public int ActiveIndex { get; set; }
	public DockablePanel ActivePanel => ActiveIndex >= 0 && ActiveIndex < Panels.Count ? Panels[ActiveIndex] : null!;

	public void AddPanel(DockablePanel panel)
	{
		if (!Panels.Contains(panel))
		{
			Panels.Add(panel);
			panel.TabGroup = this;
		}
	}

	public void RemovePanel(DockablePanel panel)
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

	public void SetActive(DockablePanel panel)
	{
		var index = Panels.IndexOf(panel);
		if (index >= 0)
			ActiveIndex = index;
	}
}

public partial class DockHost : UserControl
{
	Grid? panelsGrid;
	List<object> dockedItems = new();
	int lastDockedItemsCount;

	public static readonly StyledProperty<bool> IsHorizontalProperty = AvaloniaProperty.Register<DockHost, bool>(nameof(IsHorizontal), false);

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

	public DockHost()
	{
		InitializeComponent();
		panelsGrid = this.FindControl<Grid>("PanelsGrid");
	}

	public DockHostLayout GetLayout()
	{
		var layout = new DockHostLayout
		{
			DockEdge = DockEdge.ToString()
		};

		for (int i = 0; i < dockedItems.Count; i++)
		{
			if (dockedItems[i] is DockablePanel panel)
			{
				layout.Items.Add(new DockHostItemLayout
				{
					Panels = new List<string> { panel.Title },
					ActiveIndex = 0
				});
			}
			else if (dockedItems[i] is TabGroup tg)
			{
				var item = new DockHostItemLayout
				{
					ActiveIndex = tg.ActiveIndex
				};
				for (int j = 0; j < tg.Panels.Count; j++)
					item.Panels.Add(tg.Panels[j].Title);
				layout.Items.Add(item);
			}
		}

		layout.ItemSizes = GetItemSizes();
		return layout;
	}

	public void ApplyLayout(DockHostLayout? layout, Func<string, DockablePanel?> resolvePanel)
	{
		dockedItems.Clear();
		if (layout != null)
		{
			for (int i = 0; i < layout.Items.Count; i++)
			{
				var item = layout.Items[i];
				var panels = new List<DockablePanel>();
				for (int j = 0; j < item.Panels.Count; j++)
				{
					var panel = resolvePanel(item.Panels[j]);
					if (panel != null)
					{
						panel.DockHost = this;
						panels.Add(panel);
					}
				}
				if (panels.Count == 1)
				{
					dockedItems.Add(panels[0]);
				}
				else if (panels.Count > 1)
				{
					var tg = new TabGroup();
					for (int j = 0; j < panels.Count; j++)
						tg.AddPanel(panels[j]);
					tg.ActiveIndex = Math.Clamp(item.ActiveIndex, 0, Math.Max(0, panels.Count - 1));
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

	public void RemovePanel(DockablePanel panel)
	{
		for (int i = 0; i < dockedItems.Count; i++)
		{
			if (dockedItems[i] is DockablePanel p && p == panel)
			{
				dockedItems.RemoveAt(i);
				break;
			}
			else if (dockedItems[i] is TabGroup tg)
			{
				if (tg.Panels.Contains(panel))
				{
					tg.RemovePanel(panel);
					if (tg.Panels.Count == 1)
					{
						var remainingPanel = tg.Panels[0];
						tg.RemovePanel(remainingPanel);
						dockedItems[i] = remainingPanel;
					}
					else if (tg.Panels.Count == 0)
					{
						dockedItems.RemoveAt(i);
					}
					break;
				}
			}
		}
		RebuildGrid();
	}

	public void AddPanel(DockablePanel panel)
	{
		bool found = false;
		for (int i = 0; i < dockedItems.Count; i++)
		{
			if (dockedItems[i] is DockablePanel p && p == panel)
			{
				found = true;
				break;
			}
			else if (dockedItems[i] is TabGroup tg && tg.Panels.Contains(panel))
			{
				found = true;
				break;
			}
		}
		if (!found)
			dockedItems.Add(panel);
		RebuildGrid();
	}

	public void Dock(DockablePanel panel, Point positionInHost)
	{
		if (panelsGrid == null) return;

		RemoveFromParent(panel);
		RemovePanel(panel);

		var targetIndex = FindTargetIndex(positionInHost);
		dockedItems.Insert(targetIndex, panel);
		RebuildGrid();
	}

	public void DockAsTab(DockablePanel panel, DockablePanel targetPanel)
	{
		if (panelsGrid == null) return;

		RemoveFromParent(panel);
		RemovePanel(panel);

		for (int i = 0; i < dockedItems.Count; i++)
		{
			if (dockedItems[i] is DockablePanel p && p == targetPanel)
			{
				var newTabGroup = new TabGroup();
				newTabGroup.AddPanel(targetPanel);
				newTabGroup.AddPanel(panel);
				newTabGroup.ActiveIndex = 0;
				dockedItems[i] = newTabGroup;
				break;
			}
			else if (dockedItems[i] is TabGroup tg && tg.Panels.Contains(targetPanel))
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
			DockablePanel? panel = null;
			if (dockedItems[i] is DockablePanel p)
				panel = p;
			else if (dockedItems[i] is TabGroup tg)
				panel = tg.ActivePanel;

			if (panel == null) continue;
			var topLeft = panel.TranslatePoint(new Point(0, 0), this);
			if (!topLeft.HasValue) continue;
			var size = panel.Bounds.Size;
			if (size.Width <= 0 || size.Height <= 0) continue;
			rects.Add(new Rect(topLeft.Value, size));
		}
		return rects;
	}

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
			{
				if (IsHorizontal)
				{
					panelsGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
					var splitter = new GridSplitter
					{
						Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(100, 100, 100)),
						ResizeDirection = GridResizeDirection.Columns,
						Width = 4
					};
					splitter.PointerReleased += SplitterOnPointerReleased;
					splitter.PointerCaptureLost += SplitterOnPointerCaptureLost;
					Grid.SetColumn(splitter, panelsGrid.ColumnDefinitions.Count - 1);
					panelsGrid.Children.Add(splitter);
				}
				else
				{
					panelsGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
					var splitter = new GridSplitter
					{
						Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(100, 100, 100)),
						ResizeDirection = GridResizeDirection.Rows,
						Height = 4
					};
					splitter.PointerReleased += SplitterOnPointerReleased;
					splitter.PointerCaptureLost += SplitterOnPointerCaptureLost;
					Grid.SetRow(splitter, panelsGrid.RowDefinitions.Count - 1);
					panelsGrid.Children.Add(splitter);
				}
			}

			if (dockedItems[i] is DockablePanel panel)
			{
				if (IsHorizontal)
				{
					var colDef = new ColumnDefinition(1, GridUnitType.Star);
					colDef.MinWidth = 50;
					panelsGrid.ColumnDefinitions.Add(colDef);
					Grid.SetColumn(panel, panelsGrid.ColumnDefinitions.Count - 1);
					panelsGrid.Children.Add(panel);
					ClearFloatingProperties(panel);
					panel.RefreshTabStrip();
				}
				else
				{
					var rowDef = new RowDefinition(1, GridUnitType.Star);
					rowDef.MinHeight = 50;
					panelsGrid.RowDefinitions.Add(rowDef);
					Grid.SetRow(panel, panelsGrid.RowDefinitions.Count - 1);
					panelsGrid.Children.Add(panel);
					ClearFloatingProperties(panel);
					panel.RefreshTabStrip();
				}
			}
			else if (dockedItems[i] is TabGroup tabGroup)
			{
				for (int j = 0; j < tabGroup.Panels.Count; j++)
				{
					var panelToClear = tabGroup.Panels[j];
					panelToClear.SetFloating(false);
				}

				var activePanel = tabGroup.ActivePanel;
				if (activePanel != null)
				{
					if (IsHorizontal)
					{
						var colDef = new ColumnDefinition(1, GridUnitType.Star);
						colDef.MinWidth = 50;
						panelsGrid.ColumnDefinitions.Add(colDef);
						Grid.SetColumn(activePanel, panelsGrid.ColumnDefinitions.Count - 1);
						panelsGrid.Children.Add(activePanel);
						ClearFloatingProperties(activePanel);
					}
					else
					{
						var rowDef = new RowDefinition(1, GridUnitType.Star);
						rowDef.MinHeight = 50;
						panelsGrid.RowDefinitions.Add(rowDef);
						Grid.SetRow(activePanel, panelsGrid.RowDefinitions.Count - 1);
						panelsGrid.Children.Add(activePanel);
						ClearFloatingProperties(activePanel);
					}
				}
				
				for (int j = 0; j < tabGroup.Panels.Count; j++)
				{
					tabGroup.Panels[j].RefreshTabStrip();
				}
			}
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

	List<double> GetItemSizes()
	{
		var sizes = new List<double>();
		var lengths = new List<double>();
		for (int i = 0; i < dockedItems.Count; i++)
		{
			DockablePanel? panel = null;
			if (dockedItems[i] is DockablePanel p)
				panel = p;
			else if (dockedItems[i] is TabGroup tg)
				panel = tg.ActivePanel;
			
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

	void ApplyItemSizes(DockHostLayout? layout)
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

	void ClearFloatingProperties(DockablePanel panel)
	{
		Canvas.SetLeft(panel, double.NaN);
		Canvas.SetTop(panel, double.NaN);
		panel.SetValue(Panel.ZIndexProperty, 0);
		panel.SetFloating(false);
		panel.ClearValue(Control.WidthProperty);
		panel.ClearValue(Control.HeightProperty);
	}

	static void RemoveFromParent(Control control)
	{
		if (control.Parent is Panel panel)
		{
			panel.Children.Remove(control);
			return;
		}

		if (control.Parent is ContentControl parentControl)
		{
			parentControl.Content = null;
			return;
		}
	}
}
