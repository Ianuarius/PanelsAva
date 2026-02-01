using System.Collections.Generic;

namespace PanelsAva.Models;

public class LayoutConfig
{
	public DockHostLayout? LeftDockHost { get; set; }
	public DockHostLayout? RightDockHost { get; set; }
	public DockHostLayout? BottomDockHost { get; set; }
	public double LeftDockWidth { get; set; }
	public double RightDockWidth { get; set; }
	public double BottomDockHeight { get; set; }
	public List<PanelState> Panels { get; set; } = new();
}

public class DockHostLayout
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
}
