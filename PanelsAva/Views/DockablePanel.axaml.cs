using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Rendering;

namespace PanelsAva.Views;

public partial class DockablePanel : UserControl
{
	public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<DockablePanel, string>(nameof(Title), "Panel");
	public static readonly StyledProperty<string> PlaceholderProperty = AvaloniaProperty.Register<DockablePanel, string>(nameof(Placeholder), "Placeholder");
	public static readonly StyledProperty<DockHost?> DockHostProperty = AvaloniaProperty.Register<DockablePanel, DockHost?>(nameof(DockHost));
	public static readonly StyledProperty<Canvas?> FloatingLayerProperty = AvaloniaProperty.Register<DockablePanel, Canvas?>(nameof(FloatingLayer));

	Border? titleBar;
	bool isDragging;
	bool isFloating;
	Point pressPointRoot;
	Point dragOffset;

	public DockablePanel()
	{
		InitializeComponent();
		titleBar = this.FindControl<Border>("TitleBar");
		if (titleBar != null)
		{
			titleBar.PointerPressed += TitleBarOnPointerPressed;
			titleBar.PointerMoved += TitleBarOnPointerMoved;
			titleBar.PointerReleased += TitleBarOnPointerReleased;
			titleBar.PointerCaptureLost += TitleBarOnPointerCaptureLost;
		}
	}

	public string Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public string Placeholder
	{
		get => GetValue(PlaceholderProperty);
		set => SetValue(PlaceholderProperty, value);
	}

	public DockHost? DockHost
	{
		get => GetValue(DockHostProperty);
		set => SetValue(DockHostProperty, value);
	}

	public Canvas? FloatingLayer
	{
		get => GetValue(FloatingLayerProperty);
		set => SetValue(FloatingLayerProperty, value);
	}

	public void SetFloating(bool floating)
	{
		isFloating = floating;
	}

	void TitleBarOnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (titleBar == null)
		{
			return;
		}

		var point = e.GetCurrentPoint(titleBar);
		if (!point.Properties.IsLeftButtonPressed)
		{
			return;
		}

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null)
		{
			return;
		}

		pressPointRoot = e.GetPosition(visualRoot);
		var topLeft = this.TranslatePoint(new Point(0, 0), visualRoot);
		if (topLeft.HasValue)
		{
			dragOffset = pressPointRoot - topLeft.Value;
		}

		isDragging = true;
		e.Pointer.Capture(titleBar);
		e.Handled = true;
	}

	void TitleBarOnPointerMoved(object? sender, PointerEventArgs e)
	{
		if (!isDragging)
		{
			return;
		}

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null)
		{
			return;
		}

		var posRoot = e.GetPosition(visualRoot);
		var delta = posRoot - pressPointRoot;
		double scale = 1.0;
		if (visualRoot is IRenderRoot rr)
		{
			scale = rr.RenderScaling;
		}
		var threshold = 10 * scale;
		if (!isFloating)
		{
			if (delta.X * delta.X + delta.Y * delta.Y >= threshold * threshold)
			{
				BeginFloating(posRoot, visualRoot);
			}
		}

		if (isFloating)
		{
			MoveFloating(posRoot);
			UpdateDockPreview(posRoot, visualRoot);
		}
	}

	void TitleBarOnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (!isDragging)
		{
			return;
		}

		isDragging = false;
		// release capture
		e.Pointer.Capture(null);

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null)
		{
			return;
		}

		var posRoot = e.GetPosition(visualRoot);
		if (isFloating && DockHost != null && IsOverDockHost(posRoot, visualRoot))
		{
			DockHost.Dock(this);
		}

		DockHost?.SetPreviewVisible(false);
	}

	void TitleBarOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		isDragging = false;
		DockHost?.SetPreviewVisible(false);
	}

	void BeginFloating(Point posRoot, Visual visualRoot)
	{
		if (FloatingLayer == null)
		{
			return;
		}

		var topLeft = this.TranslatePoint(new Point(0, 0), visualRoot);
		if (topLeft.HasValue)
		{
			dragOffset = posRoot - topLeft.Value;
		}

		MoveToFloatingLayer(FloatingLayer, topLeft ?? new Point(0, 0));
		isFloating = true;
	}

	void MoveFloating(Point posRoot)
	{
		if (FloatingLayer == null)
		{
			return;
		}

		var left = posRoot.X - dragOffset.X;
		var top = posRoot.Y - dragOffset.Y;
		Canvas.SetLeft(this, left);
		Canvas.SetTop(this, top);
	}

	void UpdateDockPreview(Point posRoot, Visual visualRoot)
	{
		if (DockHost == null)
		{
			return;
		}

		DockHost.SetPreviewVisible(IsOverDockHost(posRoot, visualRoot));
	}

	bool IsOverDockHost(Point posRoot, Visual visualRoot)
	{
		if (DockHost == null)
		{
			return false;
		}

		var dockTopLeft = DockHost.TranslatePoint(new Point(0, 0), visualRoot);
		if (!dockTopLeft.HasValue)
		{
			return false;
		}

		var dockRect = new Rect(dockTopLeft.Value, DockHost.Bounds.Size);
		return dockRect.Contains(posRoot);
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

	void MoveToFloatingLayer(Canvas layer, Point topLeft)
	{
		RemoveFromParent(this);
		layer.Children.Add(this);
		Canvas.SetLeft(this, topLeft.X);
		Canvas.SetTop(this, topLeft.Y);
	}
}
