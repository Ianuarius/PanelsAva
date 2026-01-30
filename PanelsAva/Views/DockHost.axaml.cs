using Avalonia.Controls;

namespace PanelsAva.Views;

public partial class DockHost : UserControl
{
	Border? preview;

	public DockHost()
	{
		InitializeComponent();
		preview = this.FindControl<Border>("Preview");
	}

	public void SetPreviewVisible(bool visible)
	{
		if (preview == null)
		{
			return;
		}

		preview.IsVisible = visible;
	}

	public void Dock(DockablePanel panel)
	{
		RemoveFromParent(panel);
		Content = panel;
		panel.SetFloating(false);
		SetPreviewVisible(false);
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
