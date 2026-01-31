using Avalonia.Controls;
using Avalonia;
using System;
using System.Linq;
using Avalonia.VisualTree;

namespace PanelsAva.Views;

public partial class DockHost : UserControl
{
	Grid? panelsGrid;

	public DockHost()
	{
		InitializeComponent();
		panelsGrid = this.FindControl<Grid>("PanelsGrid");
	}

	public void AddPanel(DockablePanel panel)
	{
		if (panelsGrid == null) return;

		var rowCount = panelsGrid.RowDefinitions.Count / 2;
		var newRowIndex = rowCount * 2;

		if (rowCount > 0)
		{
			panelsGrid.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
			var splitter = new GridSplitter
			{
				Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Transparent),
				ResizeDirection = GridResizeDirection.Rows,
				Height = 4
			};
			Grid.SetRow(splitter, newRowIndex - 1);
			panelsGrid.Children.Add(splitter);
		}

		panelsGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
		Grid.SetRow(panel, newRowIndex);
		panelsGrid.Children.Add(panel);
		panel.SetFloating(false);
	}

	public void Dock(DockablePanel panel, Point positionInHost)
	{
		if (panelsGrid == null) return;

		RemoveFromParent(panel);

		var targetRow = FindTargetRow(positionInHost);
		if (targetRow >= 0)
		{
			InsertPanelAtRow(panel, targetRow);
		}
		else
		{
			AddPanel(panel);
		}
	}

	int FindTargetRow(Point positionInHost)
	{
		if (panelsGrid == null) return -1;

		double cumulativeY = 0;
		for (int i = 0; i < panelsGrid.RowDefinitions.Count; i += 2)
		{
			var rowDef = panelsGrid.RowDefinitions[i];
			var panelInRow = panelsGrid.Children.FirstOrDefault(c => Grid.GetRow(c) == i) as DockablePanel;
			if (panelInRow == null) continue;

			var rowHeight = panelInRow.Bounds.Height;
			cumulativeY += rowHeight;

			if (positionInHost.Y < cumulativeY)
			{
				if (positionInHost.Y < cumulativeY - rowHeight / 2)
					return i;
				else
					return i + 2;
			}

			if (i + 1 < panelsGrid.RowDefinitions.Count)
				cumulativeY += 4;
		}

		return -1;
	}

	void InsertPanelAtRow(DockablePanel panel, int targetRow)
	{
		if (panelsGrid == null) return;

		for (int i = panelsGrid.Children.Count - 1; i >= 0; i--)
		{
			var child = panelsGrid.Children[i];
			var currentRow = Grid.GetRow(child);
			if (currentRow >= targetRow)
			{
				Grid.SetRow(child, currentRow + 2);
			}
		}

		if (targetRow > 0)
		{
			panelsGrid.RowDefinitions.Insert(targetRow, new RowDefinition(4, GridUnitType.Pixel));
			var splitter = new GridSplitter
			{
				Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Transparent),
				ResizeDirection = GridResizeDirection.Rows,
				Height = 4
			};
			Grid.SetRow(splitter, targetRow - 1);
			panelsGrid.Children.Add(splitter);
		}

		panelsGrid.RowDefinitions.Insert(targetRow, new RowDefinition(1, GridUnitType.Star));
		Grid.SetRow(panel, targetRow);
		panelsGrid.Children.Add(panel);
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
