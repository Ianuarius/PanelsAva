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

		var targetIndex = FindTargetIndex(positionInHost);
		dockedPanels.Insert(targetIndex, panel);
		RebuildGrid();
	}

	int FindTargetIndex(Point positionInHost)
	{
		var panelCount = dockedPanels.Count;
		if (panelCount == 0) return 0;

		var totalHeight = Bounds.Height;
		if (totalHeight <= 0) return 0;

		var panelHeight = totalHeight / panelCount;
		double cumulativeY = 0;

		for (int i = 0; i < panelCount; i++)
		{
			cumulativeY += panelHeight;
			if (positionInHost.Y < cumulativeY)
			{
				return i;
			}
		}

		return panelCount; // append at end
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
