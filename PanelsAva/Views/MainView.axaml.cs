using Avalonia.Controls;
using Avalonia.VisualTree;
using System;

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

	public MainView()
	{
		InitializeComponent();
		Loaded += OnLoaded;
	}

	void OnLoaded(object? sender, EventArgs e)
	{
		leftDockHost = this.FindControl<DockHost>("LeftDockHost");
		rightDockHost = this.FindControl<DockHost>("RightDockHost");
		bottomDockHost = this.FindControl<DockHost>("BottomDockHost");
		floatingLayer = FindFloatingLayer();

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
			leftDockHost.AddPanel(layersPanel);

			propertiesPanel = new DockablePanel
			{
				Title = "Properties",
				Content = new PropertiesPanel(),
				DockHost = leftDockHost,
				FloatingLayer = floatingLayer
			};
			leftDockHost.AddPanel(propertiesPanel);

			colorPanel = new DockablePanel
			{
				Title = "Color",
				Content = new ColorPanel(),
				DockHost = leftDockHost,
				FloatingLayer = floatingLayer
			};
			leftDockHost.AddPanel(colorPanel);

			brushesPanel = new DockablePanel
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
			historyPanel = new DockablePanel
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
			timelinePanel = new DockablePanel
			{
				Title = "Timeline",
				Content = new TimelinePanel(),
				DockHost = bottomDockHost,
				FloatingLayer = floatingLayer
			};
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
