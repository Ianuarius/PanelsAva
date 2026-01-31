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
		var dockHost = this.FindControl<DockHost>("LeftDockHost");
		var floatingLayer = FindFloatingLayer();
		
		if (dockHost != null && floatingLayer != null)
		{
			var layersPanel = new DockablePanel
			{
				Title = "Layers",
				Content = new LayersPanel(),
				DockHost = dockHost,
				FloatingLayer = floatingLayer
			};
			dockHost.AddPanel(layersPanel);

			var propertiesPanel = new DockablePanel
			{
				Title = "Properties",
				Content = new PropertiesPanel(),
				DockHost = dockHost,
				FloatingLayer = floatingLayer
			};
			dockHost.AddPanel(propertiesPanel);
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
