using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using System;
using System.Linq;
using PanelsAva.Views;

namespace PanelsAva.Services;

public enum DragSourceType
{
	PanelTabGroup,
	FileTab,
	FloatingPanel,
	Toolbar
}

public class DragManager
{
	readonly Visual visualRoot;
	readonly Canvas floatingLayer;
	readonly MainView? mainView;
	bool isDragging;
	bool thresholdExceeded;
	object? dragSource;
	Pointer? currentPointer;
	Point pressPointRoot;
	double dragOffsetX;
	double dragOffsetY;
	double dragOffsetRatioX;
	Control? captureControl;
	bool wasFloating;
	Border? previewBorder;

	/// <summary>Creates a DragManager to handle dragging of panels and tabs, managing floating states and docking previews.</summary>
	/// <param name="visualRoot">The top-level visual element providing the coordinate system for drag position calculations and scaling factors.</param>
	/// <param name="floatingLayer">The canvas container where floating panels are rendered above other UI elements during drag operations.</param>
	/// <param name="mainView">Optional reference to the main view, required for FileTabItems, for coordinating floating tab creation, movement, and docking logic.</param>
	public DragManager(Visual visualRoot, Canvas floatingLayer, MainView? mainView = null)
	{
		this.visualRoot = visualRoot;
		this.floatingLayer = floatingLayer;
		this.mainView = mainView;
	}

	public bool IsDragging => isDragging;
	public bool ThresholdExceeded => thresholdExceeded;

	/// <summary>Initializes a potential drag operation, storing drag parameters and capturing the pointer to track movement.</summary>
	/// <param name="source">The UI element or object being dragged, such as a PanelTabGroup or FileTabItem.</param>
	/// <param name="pointer">The pointer device that initiated the drag gesture.</param>
	/// <param name="pressPoint">The initial pointer press position in root visual coordinates.</param>
	/// <param name="offsetX">Horizontal offset from the pointer to the drag element's origin.</param>
	/// <param name="offsetY">Vertical offset from the pointer to the drag element's origin.</param>
	/// <param name="offsetRatioX">Ratio-based horizontal offset for proportional dragging (e.g., based on element width).</param>
	/// <param name="capture">Optional control to capture pointer events on for reliable drag tracking.</param>
	/// <param name="floating">Indicates if the source element was already in a floating state before the drag.</param>
	public void StartPotentialDrag(object source, Pointer pointer, Point pressPoint, double offsetX, double offsetY, double offsetRatioX, Control? capture, bool floating)
	{
		if (isDragging) ReleaseDrag();
		dragSource = source;
		currentPointer = pointer;
		pressPointRoot = pressPoint;
		dragOffsetX = offsetX;
		dragOffsetY = offsetY;
		dragOffsetRatioX = offsetRatioX;
		captureControl = capture;
		wasFloating = floating;
		isDragging = true;
		thresholdExceeded = false;
		if (capture != null) pointer.Capture(capture);
	}

	/// <summary>Updates the drag operation with the current pointer position, checking drag threshold, moving floating elements, and showing docking previews.</summary>
	/// <param name="posRoot">The current pointer position in root visual coordinates.</param>
	public void UpdateDrag(Point posRoot)
	{
		if (!isDragging) return;
		var delta = posRoot - pressPointRoot;
		double scale = 1.0;
		if (visualRoot is IRenderRoot rr) scale = rr.RenderScaling;
		var threshold = 10 * scale;
		if (delta.X * delta.X + delta.Y * delta.Y < threshold * threshold) return;
		thresholdExceeded = true;

		if (dragSource is FileTabItem fileTab)
		{
			if (mainView != null && !fileTab.IsFloating)
			{
				mainView.BeginFloatingTab(fileTab, posRoot, currentPointer, dragOffsetX, dragOffsetY);
			}
			if (mainView != null)
			{
				mainView.MoveFloatingTab(fileTab, posRoot, dragOffsetX, dragOffsetY);
				mainView.UpdateDockPreview(posRoot);
			}
			return;
		}

		if (dragSource is PanelTabGroup panel)
		{
			if (!panel.IsFloating && panel.CanFloat)
			{
				if (panel.DockGrid != null) panel.DockGrid.RemovePanel(panel);
				var panelPosInRoot = panel.TranslatePoint(new Point(0, 0), visualRoot);
				var floatingLayerPosInRoot = floatingLayer.TranslatePoint(new Point(0, 0), visualRoot);
				if (panelPosInRoot.HasValue && floatingLayerPosInRoot.HasValue)
				{
					var panelPosInFloatingLayer = panelPosInRoot.Value - floatingLayerPosInRoot.Value;
					MainView.RemoveFromParent(panel);
					floatingLayer.Children.Add(panel);
					panel.SetValue(Panel.ZIndexProperty, 1);
					Canvas.SetLeft(panel, panelPosInFloatingLayer.X);
					Canvas.SetTop(panel, panelPosInFloatingLayer.Y);
				}
				else
				{
					MainView.RemoveFromParent(panel);
					floatingLayer.Children.Add(panel);
					panel.SetValue(Panel.ZIndexProperty, 1);
					Canvas.SetLeft(panel, 0);
					Canvas.SetTop(panel, 0);
				}
				var floatingTabGroup = new TabGroup();
				floatingTabGroup.AddPanel(panel);
				floatingTabGroup.ActiveIndex = 0;
				panel.SetFloating(true);
				panel.RaiseLayoutChanged();
			}
			if (panel.IsFloating)
			{
				var floatingLayerPos = floatingLayer.TranslatePoint(new Point(0, 0), visualRoot);
				if (floatingLayerPos.HasValue)
				{
					var dragOffset = new Point(panel.Bounds.Width * dragOffsetRatioX, dragOffsetY);
					var posInFloatingLayer = posRoot - floatingLayerPos.Value;
					var panelPos = posInFloatingLayer - dragOffset;
					Canvas.SetLeft(panel, panelPos.X);
					Canvas.SetTop(panel, panelPos.Y);
					panel.RaiseLayoutChanged();
				}
				var previewRect = ComputePanelPreview(panel, posRoot);
				if (previewRect != null) ShowPreview(previewRect);
				else ClearPreview();
			}
		}
	}

	public void EndDrag(Point posRoot)
	{
		if (!isDragging) return;
		try
		{
			if (dragSource is PanelTabGroup panel && panel.IsFloating)
			{
				var tabDropTarget = FindPanelAt(panel, posRoot);
				if (tabDropTarget != null)
				{
					var targetDockGrid = tabDropTarget.DockGrid;
					if (targetDockGrid != null)
					{
						if (panel.TabGroup != null)
						{
							panel.TabGroup.RemovePanel(panel);
						}
						panel.SetFloating(false);
						targetDockGrid.DockAsTab(panel, tabDropTarget);
						panel.DockGrid = targetDockGrid;
						panel.RefreshTabStrip();
					}
				}
				else
				{
					var targetDockGrid = FindDockGridAt(posRoot);
					if (targetDockGrid != null)
					{
						var posInDockGrid = targetDockGrid.TranslatePoint(new Point(0, 0), visualRoot);
						if (posInDockGrid.HasValue)
						{
							if (panel.TabGroup != null)
							{
								panel.TabGroup.RemovePanel(panel);
							}
							var relativePos = posRoot - posInDockGrid.Value;
							targetDockGrid.Dock(panel, relativePos);
							panel.DockGrid = targetDockGrid;
							panel.RefreshTabStrip();
						}
					}
				}
				panel.RaiseLayoutChanged();
			}
			else if (dragSource is FileTabItem fileTab)
			{
				if (thresholdExceeded && mainView != null)
					mainView.TryDockFloatingTab(fileTab, posRoot);
			}
		}
		finally
		{
			ReleaseDrag();
		}
	}

	/// <summary>
	/// Attempt to recover pointer capture when a captured control loses capture during a drag.
	/// If recovery succeeds, update internal captureControl; otherwise leave drag active so
	/// subsequent pointer events can resume or caller can cancel explicitly.
	/// </summary>
	public void HandleCaptureLost(Pointer pointer)
	{
		if (!isDragging) return;
		if (pointer == null) return;
		if (currentPointer == null || pointer != currentPointer) return;

		if (captureControl != null)
		{
			currentPointer?.Capture(captureControl);
			return;
		}

		if (dragSource is Control ctrl)
		{
			currentPointer?.Capture(ctrl);
			captureControl = ctrl;
			return;
		}

		if (floatingLayer is Control floatingCtrl)
		{
			currentPointer?.Capture(floatingCtrl);
			captureControl = floatingCtrl;
			return;
		}
	}

	/// <summary>Cleans up drag state by releasing pointer capture, clearing previews, and resetting all drag-related fields.</summary>
	void ReleaseDrag()
	{
		if (!isDragging) return;
		currentPointer?.Capture(null);
		ClearPreview();
		isDragging = false;
		thresholdExceeded = false;
		dragSource = null;
		currentPointer = null;
		captureControl = null;
	}

	PreviewRect? ComputePanelPreview(PanelTabGroup panel, Point posRoot)
	{
		var tabDropTarget = FindPanelAt(panel, posRoot);
		if (tabDropTarget != null)
		{
			var targetPos = tabDropTarget.TranslatePoint(new Point(0, 0), floatingLayer);
			if (targetPos.HasValue)
			{
				return new PreviewRect
				{
					Left = targetPos.Value.X,
					Top = targetPos.Value.Y,
					Width = tabDropTarget.Bounds.Width,
					Height = tabDropTarget.Bounds.Height
				};
			}
		}
		var targetDockGrid = FindDockGridAt(posRoot);
		if (targetDockGrid != null)
		{
			var dockTopLeft = targetDockGrid.TranslatePoint(new Point(0, 0), visualRoot);
			var dockPos = targetDockGrid.TranslatePoint(new Point(0, 0), floatingLayer);
			if (dockTopLeft.HasValue && dockPos.HasValue)
			{
				var relativePos = posRoot - dockTopLeft.Value;
				var previewRect = targetDockGrid.GetDockPreviewRect(relativePos);
				if (previewRect.Width > 0 && previewRect.Height > 0)
				{
					double offsetX = 0;
					double offsetY = 0;
					if (targetDockGrid.Bounds.Width <= 0 || targetDockGrid.Bounds.Height <= 0)
					{
						if (targetDockGrid.DockEdge == DockEdge.Right) offsetX = -previewRect.Width;
						else if (targetDockGrid.DockEdge == DockEdge.Bottom) offsetY = -previewRect.Height;
					}
					return new PreviewRect
					{
						Left = dockPos.Value.X + previewRect.X + offsetX,
						Top = dockPos.Value.Y + previewRect.Y + offsetY,
						Width = previewRect.Width,
						Height = previewRect.Height
					};
				}
			}
		}
		return null;
	}

	PanelTabGroup? FindPanelAt(PanelTabGroup source, Point posRoot)
	{
		var panels = visualRoot.GetVisualDescendants().OfType<PanelTabGroup>();
		foreach (var p in panels)
		{
			if (p == source || p.IsFloating) continue;
			var tabStrip = p.GetType().GetField("tabStrip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(p) as StackPanel;
			if (tabStrip == null || !tabStrip.IsVisible) continue;
			var tabStripTopLeft = tabStrip.TranslatePoint(new Point(0, 0), visualRoot);
			if (!tabStripTopLeft.HasValue) continue;
			var width = tabStrip.Bounds.Width;
			var height = tabStrip.Bounds.Height;
			if (width <= 0 || height <= 0) continue;
			var rect = new Rect(tabStripTopLeft.Value.X, tabStripTopLeft.Value.Y, width, height);
			if (rect.Contains(posRoot)) return p;
		}
		return null;
	}

	DockGrid? FindDockGridAt(Point posRoot)
	{
		var dockHosts = visualRoot.GetVisualDescendants().OfType<DockGrid>();
		foreach (var dh in dockHosts)
		{
			var dockTopLeft = dh.TranslatePoint(new Point(0, 0), visualRoot);
			if (dockTopLeft.HasValue)
			{
				var dockRect = new Rect(dockTopLeft.Value, dh.Bounds.Size);
				if (dockRect.Width <= 0 || dockRect.Height <= 0)
					dockRect = GetDockHotRect(dh, dockTopLeft.Value);
				if (dockRect.Contains(posRoot)) return dh;
			}
		}
		return null;
	}

	Rect GetDockHotRect(DockGrid dockHost, Point dockTopLeft)
	{
		double scale = 1.0;
		if (visualRoot is IRenderRoot rr) scale = rr.RenderScaling;
		double hotSize = 100 * scale;
		var rootBounds = visualRoot.Bounds;
		var height = dockHost.Bounds.Height > 0 ? dockHost.Bounds.Height : rootBounds.Height;
		var width = dockHost.Bounds.Width > 0 ? dockHost.Bounds.Width : rootBounds.Width;
		if (dockHost.DockEdge == DockEdge.Left) return new Rect(dockTopLeft.X, dockTopLeft.Y, hotSize, height);
		if (dockHost.DockEdge == DockEdge.Right) return new Rect(dockTopLeft.X - hotSize, dockTopLeft.Y, hotSize, height);
		if (dockHost.DockEdge == DockEdge.Bottom) return new Rect(dockTopLeft.X, dockTopLeft.Y - hotSize, width, hotSize);
		return new Rect(dockTopLeft, dockHost.Bounds.Size);
	}

	void ShowPreview(PreviewRect rect)
	{
		if (previewBorder == null)
		{
			previewBorder = new Border { Background = new SolidColorBrush(Colors.Blue), Opacity = 0.5 };
			previewBorder.SetValue(Panel.ZIndexProperty, 0);
			floatingLayer.Children.Add(previewBorder);
		}
		previewBorder.Width = rect.Width;
		previewBorder.Height = rect.Height;
		Canvas.SetLeft(previewBorder, rect.Left);
		Canvas.SetTop(previewBorder, rect.Top);
	}

	/// <summary>Removes the docking preview border from the floating layer and resets the preview reference.</summary>
	void ClearPreview()
	{
		if (previewBorder != null)
		{
			floatingLayer.Children.Remove(previewBorder);
			previewBorder = null;
		}
	}
}

public class PreviewRect
{
	public double Left { get; set; }
	public double Top { get; set; }
	public double Width { get; set; }
	public double Height { get; set; }
}

public class ToolbarPreviewRect
{
	public double Left { get; set; }
	public double Top { get; set; }
	public double Width { get; set; }
	public double Height { get; set; }
}
