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
		if (dockedPanels.Count == 0)
		{
			return new Rect(0, 0, Bounds.Width, Bounds.Height);
		}

		var panelRects = GetPanelRectsInHost();
		if (panelRects.Count == 0)
		{
			return new Rect(0, 0, Bounds.Width, Bounds.Height);
		}

		for (int i = 0; i < panelRects.Count; i++)
		{
			var rect = panelRects[i];
			var midY = rect.Y + rect.Height * 0.5;
			if (positionInHost.Y < midY)
			{
				var height = rect.Height * 0.5;
				return new Rect(0, rect.Y, Bounds.Width, height);
			}
		}

		var lastRect = panelRects[panelRects.Count - 1];
		var lastHeight = lastRect.Height * 0.5;
		return new Rect(0, lastRect.Y + lastRect.Height - lastHeight, Bounds.Width, lastHeight);
	}

	int FindTargetIndex(Point positionInHost)
	{
		var panelRects = GetPanelRectsInHost();
		if (panelRects.Count == 0) return 0;

		for (int i = 0; i < panelRects.Count; i++)
		{
			var rect = panelRects[i];
			var midY = rect.Y + rect.Height * 0.5;
			if (positionInHost.Y < midY)
			{
				return i;
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
		panelsGrid.RowDefinitions.Clear();

		for (int i = 0; i < dockedPanels.Count; i++)
		{
			if (i > 0)
			{
				panelsGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
				var splitter = new GridSplitter
				{
					Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Transparent),
					ResizeDirection = GridResizeDirection.Rows,
					Height = 4
				};
				Grid.SetRow(splitter, panelsGrid.RowDefinitions.Count - 1);
				panelsGrid.Children.Add(splitter);
			}

			panelsGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
			var panel = dockedPanels[i];
			Grid.SetRow(panel, panelsGrid.RowDefinitions.Count - 1);
			panelsGrid.Children.Add(panel);
			ClearFloatingProperties(panel);
		}
		panelsGrid.InvalidateMeasure();
		panelsGrid.InvalidateArrange();
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
