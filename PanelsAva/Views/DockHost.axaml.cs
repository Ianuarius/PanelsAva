using Avalonia.Controls;
using Avalonia;
using System;
using System.Linq;
using System.Collections.Generic;
using Avalonia.VisualTree;

namespace PanelsAva.Views;

public partial class DockHost : UserControl
{
	Grid? panelsGrid;
	List<DockablePanel> dockedPanels = new();

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
		dockedPanels.Remove(panel);
		RebuildGrid();
	}

	public void AddPanel(DockablePanel panel)
	{
		if (!dockedPanels.Contains(panel))
			dockedPanels.Add(panel);
		RebuildGrid();
	}

	public void Dock(DockablePanel panel, Point positionInHost)
	{
		if (panelsGrid == null) return;

		RemoveFromParent(panel);
		dockedPanels.Remove(panel);

		var targetIndex = FindTargetIndex(positionInHost);
		dockedPanels.Insert(targetIndex, panel);
		RebuildGrid();
	}

	public Rect GetDockPreviewRect(Point positionInHost)
	{
		int targetIndex = FindTargetIndex(positionInHost);
		int newCount = dockedPanels.Count + 1;
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
		for (int i = 0; i < dockedPanels.Count; i++)
		{
			var panel = dockedPanels[i];
			var topLeft = panel.TranslatePoint(new Point(0, 0), this);
			if (!topLeft.HasValue) continue;
			var size = panel.Bounds.Size;
			if (size.Width <= 0 || size.Height <= 0) continue;
			rects.Add(new Rect(topLeft.Value, size));
		}
		return rects;
	}

	void RebuildGrid()
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

		for (int i = 0; i < dockedPanels.Count; i++)
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

			if (IsHorizontal)
			{
				var colDef = new ColumnDefinition(1, GridUnitType.Star);
				colDef.MinWidth = 50;
				panelsGrid.ColumnDefinitions.Add(colDef);
				var panel = dockedPanels[i];
				ClearFloatingProperties(panel);
				Grid.SetColumn(panel, panelsGrid.ColumnDefinitions.Count - 1);
				Grid.SetRow(panel, 0);
				panelsGrid.Children.Add(panel);
			}
			else
			{
				var rowDef = new RowDefinition(1, GridUnitType.Star);
				rowDef.MinHeight = 50;
				panelsGrid.RowDefinitions.Add(rowDef);
				var panel = dockedPanels[i];
				ClearFloatingProperties(panel);
				Grid.SetRow(panel, panelsGrid.RowDefinitions.Count - 1);
				Grid.SetColumn(panel, 0);
				panelsGrid.Children.Add(panel);
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
		panel.Width = double.NaN;
		panel.Height = double.NaN;
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
