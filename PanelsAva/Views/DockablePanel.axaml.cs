using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Rendering;
using System;
using System.Linq;

namespace PanelsAva.Views;

public partial class DockablePanel : UserControl
{
	public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<DockablePanel, string>(nameof(Title), "Panel");
	public static readonly new StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<DockablePanel, object?>(nameof(Content));
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

	public new object? Content
	{
		get => GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
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

	protected override Size MeasureOverride(Size availableSize)
	{
		var size = base.MeasureOverride(availableSize);
		if (double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height))
		{
			var width = double.IsInfinity(availableSize.Width) ? Math.Max(size.Width, 200) : size.Width;
			var height = double.IsInfinity(availableSize.Height) ? Math.Max(size.Height, 120) : size.Height;
			return new Size(width, height);
		}
		return availableSize;
	}

	void TitleBarOnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (titleBar == null) return;

		var e2 = e.GetCurrentPoint(titleBar);
		if (!e2.Properties.IsLeftButtonPressed) return;

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		pressPointRoot = e.GetPosition(visualRoot);

		// Calculates the drag offset when the pointer is pressed on the title bar. Translates the panel's top-left corner to the visual root's (window) coordinate space, computes the difference from the press point, and stores a proportional X offset (relative to panel width) and absolute Y offset for smooth dragging in DockablePanel.
		var panelPos = this.TranslatePoint(new Point(0, 0), visualRoot);
		if (panelPos.HasValue)
		{
			panelPosAtPressRoot = panelPos.Value;
			var dragOffset = pressPointRoot - panelPosAtPressRoot;
			dragOffsetRatioX = this.Bounds.Width > 0 ? dragOffset.X / this.Bounds.Width : 0;
			dragOffsetAbsoluteY = dragOffset.Y;
		}

		isDragging = true;
		currentPointer = (Pointer)e.Pointer;
		// Ensures all subsequent pointer events (like move and release) are routed to titleBar during dragging, even if the pointer leaves the title bar area.
		e.Pointer.Capture(titleBar);
		e.Handled = true;
	}

	void TitleBarOnPointerMoved(object? sender, PointerEventArgs e)
	{
		if (!isDragging) return;

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		var posRoot = e.GetPosition(visualRoot);
		var delta = posRoot - pressPointRoot;
		double scale = 1.0;
		if (visualRoot is IRenderRoot rr) scale = rr.RenderScaling;
		
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
		if (!isDragging) return;

		isDragging = false;
		currentPointer?.Capture(null);
		currentPointer = null;
		e.Pointer.Capture(null);

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		var posRoot = e.GetPosition(visualRoot);
		if (isFloating)
		{
			var targetDockHost = FindDockHostAt(posRoot, visualRoot);
			if (targetDockHost != null)
			{
				var posInDockHost = targetDockHost.TranslatePoint(new Point(0, 0), visualRoot);
				if (posInDockHost.HasValue)
				{
					var relativePos = posRoot - posInDockHost.Value;
					targetDockHost.Dock(this, relativePos);
					DockHost = targetDockHost; // Update the associated DockHost
				}
			}
		}

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
			
			if (previewBorder != null)
			{
				FloatingLayer?.Children.Remove(previewBorder);
				previewBorder = null;
			}
		}
	}

	void BeginFloating(Point posRoot, Visual visualRoot)
	{
		if (FloatingLayer == null) return;

		isTransitioningToFloat = true;
		
		if (DockHost != null)
		{
			DockHost.RemovePanel(this);
		}
		
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
		if (FloatingLayer == null) return;

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		var floatingLayerPos = FloatingLayer.TranslatePoint(new Point(0, 0), visualRoot);
		if (!floatingLayerPos.HasValue) return;

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
		if (FloatingLayer == null) return;

		var targetDockHost = FindDockHostAt(posRoot, visualRoot);
		if (targetDockHost != null)
		{
			var dockTopLeft = targetDockHost.TranslatePoint(new Point(0, 0), visualRoot);
			var dockPos = targetDockHost.TranslatePoint(new Point(0, 0), FloatingLayer);
			if (dockTopLeft.HasValue && dockPos.HasValue)
			{
				var relativePos = posRoot - dockTopLeft.Value;
				var previewRect = targetDockHost.GetDockPreviewRect(relativePos);
				if (previewRect.Width > 0 && previewRect.Height > 0)
				{
					if (previewBorder == null)
					{
						previewBorder = new Border
						{
							Background = new SolidColorBrush(Colors.Blue),
							Opacity = 0.5
						};
						previewBorder.SetValue(Panel.ZIndexProperty, 0);
						FloatingLayer.Children.Add(previewBorder);
					}

					previewBorder.Width = previewRect.Width;
					previewBorder.Height = previewRect.Height;
					Canvas.SetLeft(previewBorder, dockPos.Value.X + previewRect.X);
					Canvas.SetTop(previewBorder, dockPos.Value.Y + previewRect.Y);
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

	DockHost? FindDockHostAt(Point posRoot, Visual visualRoot)
	{
		var dockHosts = visualRoot.GetVisualDescendants().OfType<DockHost>();
		foreach (var dh in dockHosts)
		{
			var dockTopLeft = dh.TranslatePoint(new Point(0, 0), visualRoot);
			if (dockTopLeft.HasValue)
			{
				var dockRect = new Rect(dockTopLeft.Value, dh.Bounds.Size);
				if (dockRect.Contains(posRoot))
				{
					return dh;
				}
			}
		}
		return null;
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
