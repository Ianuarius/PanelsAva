using System.Collections.Generic;

namespace PanelsAva.Models;

public class LayoutConfig
{
	public DockGridLayout? LeftDockGrid { get; set; }
	public DockGridLayout? RightDockGrid { get; set; }
	public DockGridLayout? BottomDockGrid { get; set; }
	public double LeftDockWidth { get; set; }
	public double RightDockWidth { get; set; }
	public double BottomDockHeight { get; set; }
	public string ToolbarPosition { get; set; } = "Top";
	public List<PanelState> Panels { get; set; } = new();
}

public class WorkspaceProfiles
{
	public string ActiveProfile { get; set; } = string.Empty;
	public Dictionary<string, LayoutConfig> Profiles { get; set; } = new();
}

/// <summary>Represents the layout configuration for a dock grid, including the dock edge (Left/Right/Bottom), the list of docked tab groups with their panel titles and active indices, and the proportional sizes (0-1) of each group summing to 1.</summary>
public class DockGridLayout
{
	public string DockEdge { get; set; } = string.Empty;
	public List<DockHostItemLayout> Items { get; set; } = new();
	public List<double> ItemSizes { get; set; } = new();
}

public class DockHostItemLayout
{
	public List<string> Panels { get; set; } = new();
	public int ActiveIndex { get; set; }
}

public class PanelState
{
	public string Title { get; set; } = string.Empty;
	public bool IsHidden { get; set; }
	public bool IsFloating { get; set; }
	public bool IsTabbed { get; set; }
	public string DockEdge { get; set; } = string.Empty;
	public int DockIndex { get; set; }
	public int TabIndex { get; set; }
	public bool WasActive { get; set; }
	public double FloatingLeft { get; set; }
	public double FloatingTop { get; set; }
	public double FloatingWidth { get; set; }
	public double FloatingHeight { get; set; }
	public double DockedProportion { get; set; }

	public void CopyFrom(PanelState other)
	{
		Title = other.Title;
		IsHidden = other.IsHidden;
		IsFloating = other.IsFloating;
		IsTabbed = other.IsTabbed;
		DockEdge = other.DockEdge;
		DockIndex = other.DockIndex;
		TabIndex = other.TabIndex;
		WasActive = other.WasActive;
		FloatingLeft = other.FloatingLeft;
		FloatingTop = other.FloatingTop;
		FloatingWidth = other.FloatingWidth;
		FloatingHeight = other.FloatingHeight;
		DockedProportion = other.DockedProportion;
	}
}
