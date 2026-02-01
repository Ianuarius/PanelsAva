using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Rendering;
using Avalonia.Threading;
using System;
using System.Linq;
using System.Diagnostics;

namespace PanelsAva.Views;

public partial class DockablePanel : UserControl
{
	public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<DockablePanel, string>(nameof(Title), "Panel");
	public static readonly new StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<DockablePanel, object?>(nameof(Content));
	public static readonly StyledProperty<DockHost?> DockHostProperty = AvaloniaProperty.Register<DockablePanel, DockHost?>(nameof(DockHost));
	public static readonly StyledProperty<Canvas?> FloatingLayerProperty = AvaloniaProperty.Register<DockablePanel, Canvas?>(nameof(FloatingLayer));
	public static readonly StyledProperty<bool> IsFloatingProperty = AvaloniaProperty.Register<DockablePanel, bool>(nameof(IsFloating));

	public TabGroup? TabGroup { get; set; }

	public event EventHandler? CloseRequested;
	public event EventHandler? LayoutChanged;

	Border? titleBar;
	StackPanel? tabStrip;
	bool isDragging;
	bool isFloating;
	bool isTransitioningToFloat;
	Point pressPointRoot;
	double dragOffsetRatioX;
	double dragOffsetAbsoluteY;
	Point panelPosAtPressRoot;
	Pointer? currentPointer;
	Control? dragHandle;
	Control? previewBorder;
	DockablePanel? tabDropTarget;
	Button? closeButton;
	MenuItem? closeMenuItem;
	bool loggedMoveOnce = false;

	public DockablePanel()
	{
		InitializeComponent();
		DataContext = this;
		titleBar = this.FindControl<Border>("TitleBar");
		tabStrip = this.FindControl<StackPanel>("TabStrip");
		closeButton = this.FindControl<Button>("CloseButton");
		if (closeButton != null)
		{
			closeButton.Click += CloseButtonOnClick;
		}
		if (titleBar != null)
		{
			titleBar.PointerPressed += TitleBarOnPointerPressed;
			titleBar.PointerMoved += TitleBarOnPointerMoved;
			titleBar.PointerReleased += TitleBarOnPointerReleased;
			titleBar.PointerCaptureLost += TitleBarOnPointerCaptureLost;
			// Initially docked, so set context menu
			var contextMenu = new ContextMenu();
			closeMenuItem = new MenuItem { Header = "Close" };
			closeMenuItem.Click += CloseMenuItemOnClick;
			contextMenu.Items.Add(closeMenuItem);
			titleBar.ContextMenu = contextMenu;
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

	public bool IsFloating
	{
		get => GetValue(IsFloatingProperty);
		set => SetValue(IsFloatingProperty, value);
	}

	public void SetFloating(bool floating)
	{
		isFloating = floating;
		IsFloating = floating;
		
		if (floating)
		{
			TabGroup = null;
			RefreshTabStrip();
		}
		
		if (closeButton != null)
		{
			closeButton.IsVisible = floating;
		}
		
		if (titleBar != null)
		{
			if (floating)
			{
				titleBar.ContextMenu = null;
			}
			else
			{
				if (titleBar.ContextMenu == null)
				{
					var contextMenu = new ContextMenu();
					closeMenuItem = new MenuItem { Header = "Close" };
					closeMenuItem.Click += CloseMenuItemOnClick;
					contextMenu.Items.Add(closeMenuItem);
					titleBar.ContextMenu = contextMenu;
				}
			}
		}
		LayoutChanged?.Invoke(this, EventArgs.Empty);
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
		BeginDrag(titleBar, e);
	}

	void TitleBarOnPointerMoved(object? sender, PointerEventArgs e)
	{
		ContinueDrag(e);
	}

	void TitleBarOnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		EndDrag(e);
	}

	void TitleBarOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		DragCaptureLost(e);
	}

	void BeginDrag(Control handle, PointerPressedEventArgs e)
	{
		var e2 = e.GetCurrentPoint(handle);
		if (!e2.Properties.IsLeftButtonPressed) return;

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		pressPointRoot = e.GetPosition(visualRoot);

		var panelPos = this.TranslatePoint(new Point(0, 0), visualRoot);
		if (panelPos.HasValue)
		{
			panelPosAtPressRoot = panelPos.Value;
			var dragOffset = pressPointRoot - panelPosAtPressRoot;
			dragOffsetRatioX = this.Bounds.Width > 0 ? dragOffset.X / this.Bounds.Width : 0;
			dragOffsetAbsoluteY = dragOffset.Y;
		}

		Debug.WriteLine($"[BeginDrag] {Title}: pressPointRoot={pressPointRoot} panelPosAtPressRoot={panelPosAtPressRoot} dragOffsetRatioX={dragOffsetRatioX:F3} dragOffsetAbsoluteY={dragOffsetAbsoluteY:F1} handle={handle.GetType().Name}");

		isDragging = true;
		currentPointer = (Pointer)e.Pointer;
		dragHandle = handle;
		e.Pointer.Capture(handle);
		e.Handled = true;
	}

	void ContinueDrag(PointerEventArgs e)
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

	void EndDrag(PointerReleasedEventArgs e)
	{
		if (!isDragging) return;

		isDragging = false;
		currentPointer?.Capture(null);
		currentPointer = null;
		dragHandle = null;
		e.Pointer.Capture(null);

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		var posRoot = e.GetPosition(visualRoot);
		if (isFloating)
		{
			if (tabDropTarget != null)
			{
				var targetDockHost = tabDropTarget.DockHost;
				if (targetDockHost != null)
				{
					SetFloating(false);
					targetDockHost.DockAsTab(this, tabDropTarget);
					DockHost = targetDockHost;
				}
				tabDropTarget = null;
			}
			else
			{
				var targetDockHost = FindDockHostAt(posRoot, visualRoot);
				if (targetDockHost != null)
				{
					var posInDockHost = targetDockHost.TranslatePoint(new Point(0, 0), visualRoot);
					if (posInDockHost.HasValue)
					{
						var relativePos = posRoot - posInDockHost.Value;
						targetDockHost.Dock(this, relativePos);
						DockHost = targetDockHost;
					}
				}
			}
		}

		if (previewBorder != null)
		{
			FloatingLayer?.Children.Remove(previewBorder);
			previewBorder = null;
		}
		LayoutChanged?.Invoke(this, EventArgs.Empty);
	}

	void DragCaptureLost(PointerCaptureLostEventArgs e)
	{
		if (!isTransitioningToFloat)
		{
			currentPointer = null;
			dragHandle = null;
			isDragging = false;
			
			if (previewBorder != null)
			{
				FloatingLayer?.Children.Remove(previewBorder);
				previewBorder = null;
			}
		}
	}

	void BeginDragFromTab(PointerPressedEventArgs e, Control origTabBorder)
	{
		Control? handle = origTabBorder;
		if (handle == null)
		{
			handle = tabStrip;
			if (handle == null) handle = titleBar;
		}
		if (handle == null) return;
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot != null)
		{
			var handlePos = handle.TranslatePoint(new Point(0, 0), visualRoot);
			var panelPos = this.TranslatePoint(new Point(0, 0), visualRoot);
			Debug.WriteLine($"[BeginDragFromTab] {Title}: handle={handle.GetType().Name} handlePos={handlePos?.ToString() ?? "null"} panelPos={panelPos?.ToString() ?? "null"}");
		}
		BeginDrag(handle, e);
	}

	void BeginDragAfterActivate(Point pressPointRoot, IPointer pointer, double clickXInsideTab)
	{
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		Control? handle = null;
		if (tabStrip != null)
		{
			for (int i = 0; i < tabStrip.Children.Count; i++)
			{
				if (tabStrip.Children[i] is Border b && ReferenceEquals(b.Tag, this))
				{
					handle = b;
					break;
				}
			}
		}
		if (handle == null) handle = titleBar;
		if (handle == null) return;

		var panelPosInRoot = this.TranslatePoint(new Point(0, 0), visualRoot);
		if (!panelPosInRoot.HasValue) return;

		// Use titleBar position so the pointer lines up with the title bar when floating
		var titleBarPosInRoot = titleBar?.TranslatePoint(new Point(0, 0), visualRoot);
		if (!titleBarPosInRoot.HasValue) return;

// offsetX is distance from panel left to the click point inside the title bar (title bar offset + clickXInsideTab)
			var titleBarOffsetInPanelX = titleBarPosInRoot.Value.X - panelPosInRoot.Value.X;
			var offsetX = titleBarOffsetInPanelX + clickXInsideTab;
			dragOffsetRatioX = this.Bounds.Width > 0 ? offsetX / this.Bounds.Width : 0;
			dragOffsetAbsoluteY = pressPointRoot.Y - panelPosInRoot.Value.Y;
			this.pressPointRoot = pressPointRoot;
			panelPosAtPressRoot = panelPosInRoot.Value;

			Debug.WriteLine($"[BeginDragAfterActivate] {Title}: pressPointRoot={pressPointRoot} panelPosInRoot={panelPosInRoot.Value} titleBarPosInRoot={titleBarPosInRoot.Value} titleBarOffsetInPanelX={titleBarOffsetInPanelX:F1} clickXInsideTab={clickXInsideTab:F1} offsetX={offsetX:F1} dragOffsetRatioX={dragOffsetRatioX:F3} dragOffsetAbsoluteY={dragOffsetAbsoluteY:F1} handle={handle.GetType().Name}");
		isDragging = true;
		currentPointer = (Pointer)pointer;
		dragHandle = handle;
		pointer.Capture(handle);
	}

	void BeginFloating(Point posRoot, Visual visualRoot)
	{
		if (FloatingLayer == null) return;

		isTransitioningToFloat = true;
		loggedMoveOnce = false;
		
		if (DockHost != null)
		{
			DockHost.RemovePanel(this);
		}
		
		var panelPosInRoot = this.TranslatePoint(new Point(0, 0), visualRoot);
		var floatingLayerPosInRoot = FloatingLayer.TranslatePoint(new Point(0, 0), visualRoot);
		
		if (panelPosInRoot.HasValue && floatingLayerPosInRoot.HasValue)
		{
			var panelPosInFloatingLayer = panelPosInRoot.Value - floatingLayerPosInRoot.Value;
			Debug.WriteLine($"[BeginFloating] {Title}: panelPosInRoot={panelPosInRoot.Value} floatingLayerPosInRoot={floatingLayerPosInRoot.Value} panelPosInFloatingLayer={panelPosInFloatingLayer}");
			MoveToFloatingLayer(FloatingLayer, panelPosInFloatingLayer.X, panelPosInFloatingLayer.Y);
		}
		else
		{
			MoveToFloatingLayer(FloatingLayer, 0, 0);
		}
		
		SetFloating(true);
		currentPointer?.Capture(dragHandle ?? titleBar);
		isTransitioningToFloat = false;
		LayoutChanged?.Invoke(this, EventArgs.Empty);
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

		if (!loggedMoveOnce)
		{
			loggedMoveOnce = true;
			Debug.WriteLine($"[MoveFloating] {Title}: posRoot={posRoot} floatingLayerPos={floatingLayerPos.Value} posInFloatingLayer={posInFloatingLayer} panel.Bounds.Width={this.Bounds.Width} dragOffsetRatioX={dragOffsetRatioX:F3} dragOffsetAbsoluteY={dragOffsetAbsoluteY:F1} currentDragOffset={currentDragOffset}");
		}
		

		var panelPos = posInFloatingLayer - currentDragOffset;
		
		Canvas.SetLeft(this, panelPos.X);
		Canvas.SetTop(this, panelPos.Y);
		LayoutChanged?.Invoke(this, EventArgs.Empty);
	}

	void UpdateDockPreview(Point posRoot, Visual visualRoot)
	{
		if (FloatingLayer == null) return;

		tabDropTarget = FindPanelAt(posRoot, visualRoot);
		if (tabDropTarget != null)
		{
			var targetTopLeft = tabDropTarget.TranslatePoint(new Point(0, 0), visualRoot);
			var targetPos = tabDropTarget.TranslatePoint(new Point(0, 0), FloatingLayer);
			if (targetTopLeft.HasValue && targetPos.HasValue)
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

				previewBorder.Width = tabDropTarget.Bounds.Width;
				previewBorder.Height = tabDropTarget.Bounds.Height;
				Canvas.SetLeft(previewBorder, targetPos.Value.X);
				Canvas.SetTop(previewBorder, targetPos.Value.Y);
			}
			return;
		}

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
					double offsetX = 0;
					double offsetY = 0;
					if (targetDockHost.Bounds.Width <= 0 || targetDockHost.Bounds.Height <= 0)
					{
						if (targetDockHost.DockEdge == DockEdge.Right)
							offsetX = -previewRect.Width;
						else if (targetDockHost.DockEdge == DockEdge.Bottom)
							offsetY = -previewRect.Height;
					}
					Canvas.SetLeft(previewBorder, dockPos.Value.X + previewRect.X + offsetX);
					Canvas.SetTop(previewBorder, dockPos.Value.Y + previewRect.Y + offsetY);
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
				if (dockRect.Width <= 0 || dockRect.Height <= 0)
					dockRect = GetDockHotRect(dh, dockTopLeft.Value, visualRoot);
				if (dockRect.Contains(posRoot))
				{
					return dh;
				}
			}
		}
		return null;
	}

	Rect GetDockHotRect(DockHost dockHost, Point dockTopLeft, Visual visualRoot)
	{
		double scale = 1.0;
		if (visualRoot is IRenderRoot rr) scale = rr.RenderScaling;
		double hotSize = 100 * scale;
		var rootBounds = visualRoot.Bounds;
		var height = dockHost.Bounds.Height > 0 ? dockHost.Bounds.Height : rootBounds.Height;
		var width = dockHost.Bounds.Width > 0 ? dockHost.Bounds.Width : rootBounds.Width;

		if (dockHost.DockEdge == DockEdge.Left)
			return new Rect(dockTopLeft.X, dockTopLeft.Y, hotSize, height);
		if (dockHost.DockEdge == DockEdge.Right)
			return new Rect(dockTopLeft.X - hotSize, dockTopLeft.Y, hotSize, height);
		if (dockHost.DockEdge == DockEdge.Bottom)
			return new Rect(dockTopLeft.X, dockTopLeft.Y - hotSize, width, hotSize);

		return new Rect(dockTopLeft, dockHost.Bounds.Size);
	}

	DockablePanel? FindPanelAt(Point posRoot, Visual visualRoot)
	{
		var panels = visualRoot.GetVisualDescendants().OfType<DockablePanel>();
		foreach (var p in panels)
		{
			if (p == this) continue;
			if (p.isFloating) continue;

			var tabStripRect = GetTabStripRect(p, visualRoot);
			if (tabStripRect.HasValue && tabStripRect.Value.Contains(posRoot))
			{
				return p;
			}
		}
		return null;
	}

	Rect? GetTabStripRect(DockablePanel panel, Visual visualRoot)
	{
		var panelTopLeft = panel.TranslatePoint(new Point(0, 0), visualRoot);
		if (!panelTopLeft.HasValue) return null;

		double height = 0;
		if (panel.tabStrip != null && panel.tabStrip.Bounds.Height > 0)
			height = panel.tabStrip.Bounds.Height;
		else if (panel.titleBar != null && panel.titleBar.Bounds.Height > 0)
			height = panel.titleBar.Bounds.Height;
		else
			height = 18;

		var width = panel.Bounds.Width;
		if (width <= 0 || height <= 0) return null;

		return new Rect(panelTopLeft.Value.X, panelTopLeft.Value.Y, width, height);
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

	public void SetFloatingBounds(Canvas layer, double left, double top, double width, double height)
	{
		MoveToFloatingLayer(layer, left, top);
		if (width > 0)
			Width = width;
		if (height > 0)
			Height = height;
		SetFloating(true);
		LayoutChanged?.Invoke(this, EventArgs.Empty);
	}

	void CloseButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	void CloseMenuItemOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	public void RefreshTabStrip()
	{
		if (tabStrip == null || titleBar == null) return;

		tabStrip.Children.Clear();

		if (TabGroup == null || TabGroup.Panels.Count <= 1)
		{
			tabStrip.IsVisible = false;
			titleBar.IsVisible = true;
			return;
		}

		tabStrip.IsVisible = true;
		titleBar.IsVisible = false;

		for (int i = 0; i < TabGroup.Panels.Count; i++)
		{
			var panel = TabGroup.Panels[i];
			var isActive = i == TabGroup.ActiveIndex;

			var tabBorder = new Border
			{
				Background = new SolidColorBrush(isActive ? Color.FromRgb(58, 58, 58) : Color.FromRgb(42, 42, 42)),
				Padding = new Thickness(6, 0, 6, 0),
				Tag = panel
			};

			var tabText = new TextBlock
			{
				Text = panel.Title,
				FontSize = 12,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
				Foreground = new SolidColorBrush(Colors.White)
			};
			tabBorder.Child = tabText;

			tabBorder.PointerPressed += TabOnPointerPressed;
			tabBorder.PointerMoved += TabOnPointerMoved;
			tabBorder.PointerReleased += TabOnPointerReleased;
			tabBorder.PointerCaptureLost += TabOnPointerCaptureLost;

			tabStrip.Children.Add(tabBorder);
		}
	}

	void TabOnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is Border border && border.Tag is DockablePanel panel && TabGroup != null)
		{
			var e2 = e.GetCurrentPoint(border);
			if (e2.Properties.IsLeftButtonPressed)
			{
				var wasActive = TabGroup.ActivePanel == panel;
				if (wasActive)
				{
					var visualRoot = this.GetVisualRoot() as Visual;
					if (visualRoot != null)
					{
						var pressPointRoot = e.GetPosition(visualRoot);
						var panelPos = this.TranslatePoint(new Point(0,0), visualRoot);
						Debug.WriteLine($"[TabPress] {panel.Title}: ACTIVE press={pressPointRoot} panelPos={panelPos?.ToString() ?? "null"}");
					}
					panel.BeginDragFromTab(e, border);
				}
				else
				{
					var visualRoot = this.GetVisualRoot() as Visual;
					if (visualRoot == null) return;
					var pressPointRoot = e.GetPosition(visualRoot);
					var pressPointInBorder = e.GetPosition(border);
					var pointer = e.Pointer;

					TabGroup.SetActive(panel);
					DockHost?.RebuildGrid();
					Dispatcher.UIThread.InvokeAsync(() =>
					{
						panel.BeginDragAfterActivate(pressPointRoot, pointer, pressPointInBorder.X);
					}, DispatcherPriority.Render);
				}
				e.Handled = true;
			}
		}
	}

	void TabOnPointerMoved(object? sender, PointerEventArgs e)
	{
		ContinueDrag(e);
	}

	void TabOnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		EndDrag(e);
	}

	void TabOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		DragCaptureLost(e);
	}
}
