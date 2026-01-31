using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
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
	bool isTransitioningToFloat;
	Point pressPointRoot;
	double dragOffsetRatioX;
	double dragOffsetAbsoluteY;
	Point panelPosAtPressRoot;
	Pointer? currentPointer;
	bool wasOverDockHost;
	Control? previewBorder;

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

		var e2 = e.GetCurrentPoint(titleBar);
		if (!e2.Properties.IsLeftButtonPressed)
		{
			return;
		}

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null)
		{
			return;
		}

		pressPointRoot = e.GetPosition(visualRoot);
		var panelPos = this.TranslatePoint(new Point(0, 0), visualRoot);
		if (panelPos.HasValue)
		{
			panelPosAtPressRoot = panelPos.Value;
			var dragOffset = pressPointRoot - panelPosAtPressRoot;
			dragOffsetRatioX = this.Bounds.Width > 0 ? dragOffset.X / this.Bounds.Width : 0;
			dragOffsetAbsoluteY = dragOffset.Y;
		}

		isDragging = true;
		wasOverDockHost = false;
		currentPointer = (Pointer)e.Pointer;
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
				MoveFloating(posRoot);
			}
		}
		else
		{
			MoveFloating(posRoot);
			UpdateDockPreview(posRoot, visualRoot);
		}

		e.Handled = true;
	}

	void TitleBarOnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (!isDragging)
		{
			return;
		}

		isDragging = false;
		currentPointer?.Capture(null);
		currentPointer = null;
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

		// Hide preview
		if (previewBorder != null)
		{
			FloatingLayer?.Children.Remove(previewBorder);
			previewBorder = null;
		}
	}

	void TitleBarOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		if (!isTransitioningToFloat)
		{
			currentPointer = null;
			isDragging = false;
			// Hide preview
			if (previewBorder != null)
			{
				FloatingLayer?.Children.Remove(previewBorder);
				previewBorder = null;
			}
		}
	}

	void BeginFloating(Point posRoot, Visual visualRoot)
	{
		if (FloatingLayer == null)
		{
			return;
		}

		isTransitioningToFloat = true;
		
		var panelPosInRoot = this.TranslatePoint(new Point(0, 0), visualRoot);
		var floatingLayerPosInRoot = FloatingLayer.TranslatePoint(new Point(0, 0), visualRoot);
		
		if (panelPosInRoot.HasValue && floatingLayerPosInRoot.HasValue)
		{
			var panelPosInFloatingLayer = panelPosInRoot.Value - floatingLayerPosInRoot.Value;
			MoveToFloatingLayer(FloatingLayer, panelPosInFloatingLayer.X, panelPosInFloatingLayer.Y);
		}
		else
		{
			MoveToFloatingLayer(FloatingLayer, 0, 0);
		}
		
		isFloating = true;
		currentPointer?.Capture(titleBar);
		isTransitioningToFloat = false;
	}

	void MoveFloating(Point posRoot)
	{
		if (FloatingLayer == null)
		{
			return;
		}

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null)
		{
			return;
		}

		var floatingLayerPos = FloatingLayer.TranslatePoint(new Point(0, 0), visualRoot);
		if (!floatingLayerPos.HasValue)
		{
			return;
		}

		var currentDragOffset = new Point(
			this.Bounds.Width * dragOffsetRatioX,
			dragOffsetAbsoluteY
		);
		
		var posInFloatingLayer = posRoot - floatingLayerPos.Value;
		var panelPos = posInFloatingLayer - currentDragOffset;
		
		Canvas.SetLeft(this, panelPos.X);
		Canvas.SetTop(this, panelPos.Y);
	}

	void UpdateDockPreview(Point posRoot, Visual visualRoot)
	{
		if (DockHost == null || FloatingLayer == null)
		{
			return;
		}

		var isOver = IsOverDockHost(posRoot, visualRoot);
		if (isOver != wasOverDockHost)
		{
			wasOverDockHost = isOver;
		}

		if (isOver)
		{
			if (previewBorder == null)
			{
				var dockPos = DockHost.TranslatePoint(new Point(0, 0), FloatingLayer);
				if (dockPos.HasValue)
				{
					previewBorder = new Border
					{
						Background = new SolidColorBrush(Colors.Blue),
						Opacity = 0.5,
						Width = DockHost.Bounds.Width,
						Height = DockHost.Bounds.Height
					};
					previewBorder.SetValue(Panel.ZIndexProperty, 0);
					Canvas.SetLeft(previewBorder, dockPos.Value.X);
					Canvas.SetTop(previewBorder, dockPos.Value.Y);
					FloatingLayer.Children.Add(previewBorder);
				}
			}
		}
		else
		{
			if (previewBorder != null)
			{
				FloatingLayer.Children.Remove(previewBorder);
				previewBorder = null;
			}
		}
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
		var contains = dockRect.Contains(posRoot);
		return contains;
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

	void MoveToFloatingLayer(Canvas layer, double left, double top)
	{
		RemoveFromParent(this);
		layer.Children.Add(this);
		this.SetValue(Panel.ZIndexProperty, 1);
		Canvas.SetLeft(this, left);
		Canvas.SetTop(this, top);
	}
}
