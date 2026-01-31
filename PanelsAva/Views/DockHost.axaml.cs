using Avalonia.Controls;
using Avalonia;
using System;
using System.Linq;
using System.Collections.Generic;
using Avalonia.VisualTree;

namespace PanelsAva.Views;

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

	public static readonly StyledProperty<bool> IsHorizontalProperty = AvaloniaProperty.Register<DockHost, bool>(nameof(IsHorizontal), false);

	public bool IsHorizontal
	{
		get => GetValue(IsHorizontalProperty);
		set => SetValue(IsHorizontalProperty, value);
	}

	public DockHost()
	{
		InitializeComponent();
		panelsGrid = this.FindControl<Grid>("PanelsGrid");
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
		int targetIndex = FindTargetIndex(positionInHost);
		int newCount = dockedItems.Count + 1;
		int splitterCount = newCount - 1;
		double splitterSize = 4;
		double totalSplitterSize = splitterCount * splitterSize;
		double availableSize = IsHorizontal ? Bounds.Width - totalSplitterSize : Bounds.Height - totalSplitterSize;
		if (availableSize <= 0) availableSize = IsHorizontal ? Bounds.Width : Bounds.Height;
		double panelSize = availableSize / newCount;
		if (IsHorizontal)
		{
			double x = targetIndex * (panelSize + splitterSize);
			return new Rect(x, 0, panelSize, Bounds.Height);
		}
		else
		{
			double y = targetIndex * (panelSize + splitterSize);
			return new Rect(0, y, Bounds.Width, panelSize);
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
						activePanel.RefreshTabStrip();
					}
					else
					{
						var rowDef = new RowDefinition(1, GridUnitType.Star);
						rowDef.MinHeight = 50;
						panelsGrid.RowDefinitions.Add(rowDef);
						Grid.SetRow(activePanel, panelsGrid.RowDefinitions.Count - 1);
						panelsGrid.Children.Add(activePanel);
						ClearFloatingProperties(activePanel);
						activePanel.RefreshTabStrip();
					}
				}
			}
		}
		panelsGrid.InvalidateMeasure();
		panelsGrid.InvalidateArrange();
		this.InvalidateMeasure();
		this.InvalidateArrange();
	}

	void ClearFloatingProperties(DockablePanel panel)
	{
		Canvas.SetLeft(panel, double.NaN);
		Canvas.SetTop(panel, double.NaN);
		panel.SetValue(Panel.ZIndexProperty, 0);
		panel.SetFloating(false);
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
