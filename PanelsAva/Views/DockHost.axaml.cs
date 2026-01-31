using Avalonia.Controls;
using Avalonia;
using System;

namespace PanelsAva.Views;

public partial class DockHost : UserControl
{
	public DockHost()
	{
		InitializeComponent();
	}

	public void Dock(DockablePanel panel)
	{
		RemoveFromParent(panel);
		Content = panel;
		panel.SetFloating(false);
	}

	static void RemoveFromParent(Control control)
	{
		if (control.Parent is Panel panel)
		{
			panel.Children.Remove(control);
			return;
		}

		if (control.Parent is Control parentControl)
		{
			var contentProp = parentControl.GetType().GetProperty("Content");
			if (contentProp != null && contentProp.CanWrite)
			{
				contentProp.SetValue(parentControl, null);
				return;
			}
		}
	}
}
