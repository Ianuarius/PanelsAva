using Avalonia.Controls;
using Avalonia.VisualTree;
using System;

namespace PanelsAva.Views;

public partial class MainView : UserControl
{
	public MainView()
	{
		InitializeComponent();
		Loaded += OnLoaded;
	}

	void OnLoaded(object? sender, EventArgs e)
	{
		var leftDockHost = this.FindControl<DockHost>("LeftDockHost");
		var rightDockHost = this.FindControl<DockHost>("RightDockHost");
		var bottomDockHost = this.FindControl<DockHost>("BottomDockHost");
		var floatingLayer = FindFloatingLayer();
		
		if (leftDockHost != null && floatingLayer != null)
		{
			var layersPanel = new DockablePanel
			{
				Title = "Layers",
				Content = new LayersPanel(),
				DockHost = leftDockHost,
				FloatingLayer = floatingLayer
			};
			leftDockHost.AddPanel(layersPanel);

			var propertiesPanel = new DockablePanel
			{
				Title = "Properties",
				Content = new PropertiesPanel(),
				DockHost = leftDockHost,
				FloatingLayer = floatingLayer
			};
			leftDockHost.AddPanel(propertiesPanel);

			var colorPanel = new DockablePanel
			{
				Title = "Color",
				Content = new ColorPanel(),
				DockHost = leftDockHost,
				FloatingLayer = floatingLayer
			};
			leftDockHost.AddPanel(colorPanel);

			var brushesPanel = new DockablePanel
			{
				Title = "Brushes",
				Content = new BrushesPanel(),
				DockHost = leftDockHost,
				FloatingLayer = floatingLayer
			};
			leftDockHost.AddPanel(brushesPanel);
		}

		if (rightDockHost != null && floatingLayer != null)
		{
			var historyPanel = new DockablePanel
			{
				Title = "History",
				Content = new HistoryPanel(),
				DockHost = rightDockHost,
				FloatingLayer = floatingLayer
			};
			rightDockHost.AddPanel(historyPanel);
		}

		if (bottomDockHost != null && floatingLayer != null)
		{
			var timelinePanel = new DockablePanel
			{
				Title = "Timeline",
				Content = new TimelinePanel(),
				DockHost = bottomDockHost,
				FloatingLayer = floatingLayer
			};
			bottomDockHost.AddPanel(timelinePanel);
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
