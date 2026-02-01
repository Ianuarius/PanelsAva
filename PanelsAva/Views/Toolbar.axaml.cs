using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Rendering;
using System;

namespace PanelsAva.Views;

public enum ToolbarPosition
{
	Top,
	Left,
	Right,
	Bottom,
	Floating
}

public partial class Toolbar : UserControl
{
	public static readonly StyledProperty<Canvas?> FloatingLayerProperty = AvaloniaProperty.Register<Toolbar, Canvas?>(nameof(FloatingLayer));
	public static readonly StyledProperty<ToolbarPosition> PositionProperty = AvaloniaProperty.Register<Toolbar, ToolbarPosition>(nameof(Position), ToolbarPosition.Top);

	public event EventHandler<ToolbarPosition>? PositionChanged;

	Border? gripRect;
	StackPanel? buttonStack;
	Grid? mainGrid;
	bool isDragging;
	Point pressPointRoot;
	Point panelPosAtPressRoot;
	Pointer? currentPointer;
	Control? previewBorder;

	public Toolbar()
	{
		InitializeComponent();
		gripRect = this.FindControl<Border>("GripRect");
		buttonStack = this.FindControl<StackPanel>("ButtonStack");
		mainGrid = this.FindControl<Grid>("MainGrid");

		if (gripRect != null)
		{
			gripRect.PointerPressed += GripOnPointerPressed;
			gripRect.PointerMoved += GripOnPointerMoved;
			gripRect.PointerReleased += GripOnPointerReleased;
			gripRect.PointerCaptureLost += GripOnPointerCaptureLost;
		}

		UpdateOrientation();
	}

	public Canvas? FloatingLayer
	{
		get => GetValue(FloatingLayerProperty);
		set => SetValue(FloatingLayerProperty, value);
	}

	public ToolbarPosition Position
	{
		get => GetValue(PositionProperty);
		set
		{
			SetValue(PositionProperty, value);
			UpdateOrientation();
		}
	}

	void UpdateOrientation()
	{
		if (mainGrid == null || buttonStack == null || gripRect == null) return;

		bool isHorizontal = Position == ToolbarPosition.Top || Position == ToolbarPosition.Bottom || Position == ToolbarPosition.Floating;
		mainGrid.RowDefinitions.Clear();
		mainGrid.ColumnDefinitions.Clear();

		if (isHorizontal)
		{
			mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
			mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			Grid.SetRow(buttonStack, 0);
			Grid.SetColumn(buttonStack, 1);
			Grid.SetRow(gripRect, 0);
			Grid.SetColumn(gripRect, 0);
			buttonStack.Orientation = Avalonia.Layout.Orientation.Horizontal;
			gripRect.Width = 20;
			gripRect.Height = double.NaN;
		}
		else
		{
			mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
			mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			Grid.SetRow(gripRect, 0);
			Grid.SetColumn(gripRect, 0);
			Grid.SetRow(buttonStack, 1);
			Grid.SetColumn(buttonStack, 0);
			buttonStack.Orientation = Avalonia.Layout.Orientation.Vertical;
			gripRect.Width = double.NaN;
			gripRect.Height = 20;
		}

		foreach (var child in buttonStack.Children)
		{
			if (child is Button btn)
			{
				btn.Width = 40;
				btn.Height = 40;
			}
		}
	}

	void GripOnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		pressPointRoot = e.GetPosition(visualRoot);
		var panelPosInRoot = this.TranslatePoint(new Point(0, 0), visualRoot);
		if (panelPosInRoot.HasValue)
		{
			panelPosAtPressRoot = panelPosInRoot.Value;
		}

		isDragging = true;
		currentPointer = e.Pointer as Pointer;
		e.Pointer.Capture(gripRect);
		e.Handled = true;
	}

	void GripOnPointerMoved(object? sender, PointerEventArgs e)
	{
		if (!isDragging) return;

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		var posRoot = e.GetPosition(visualRoot);
		var delta = posRoot - pressPointRoot;
		double scale = 1.0;
		if (visualRoot is IRenderRoot rr) scale = rr.RenderScaling;
		
		var threshold = 10 * scale;
		if (delta.X * delta.X + delta.Y * delta.Y >= threshold * threshold)
		{
			UpdateDockPreview(posRoot, visualRoot);
		}

		e.Handled = true;
	}

	void GripOnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (!isDragging) return;

		isDragging = false;
		currentPointer?.Capture(null);
		currentPointer = null;
		e.Pointer.Capture(null);

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		var posRoot = e.GetPosition(visualRoot);
		var newPosition = DetectBorderPosition(posRoot, visualRoot);

		if (newPosition != Position)
		{
			Position = newPosition;
			PositionChanged?.Invoke(this, newPosition);
		}

		if (previewBorder != null)
		{
			FloatingLayer?.Children.Remove(previewBorder);
			previewBorder = null;
		}
	}

	void GripOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		currentPointer = null;
		isDragging = false;
		
		if (previewBorder != null)
		{
			FloatingLayer?.Children.Remove(previewBorder);
			previewBorder = null;
		}
	}

	void UpdateDockPreview(Point posRoot, Visual visualRoot)
	{
		var newPosition = DetectBorderPosition(posRoot, visualRoot);
		
		if (previewBorder == null)
		{
			previewBorder = new Border
			{
				Background = new SolidColorBrush(Color.Parse("#8080FF")),
				Opacity = 0.5,
				ZIndex = 0
			};
			FloatingLayer?.Children.Add(previewBorder);
		}

		var bounds = visualRoot.Bounds;
		const double toolbarSize = 40;

		switch (newPosition)
		{
			case ToolbarPosition.Top:
				Canvas.SetLeft(previewBorder, 0);
				Canvas.SetTop(previewBorder, 0);
				previewBorder.Width = bounds.Width;
				previewBorder.Height = toolbarSize;
				break;
			case ToolbarPosition.Bottom:
				Canvas.SetLeft(previewBorder, 0);
				Canvas.SetTop(previewBorder, bounds.Height - toolbarSize);
				previewBorder.Width = bounds.Width;
				previewBorder.Height = toolbarSize;
				break;
			case ToolbarPosition.Left:
				Canvas.SetLeft(previewBorder, 0);
				Canvas.SetTop(previewBorder, 0);
				previewBorder.Width = toolbarSize;
				previewBorder.Height = bounds.Height;
				break;
			case ToolbarPosition.Right:
				Canvas.SetLeft(previewBorder, bounds.Width - toolbarSize);
				Canvas.SetTop(previewBorder, 0);
				previewBorder.Width = toolbarSize;
				previewBorder.Height = bounds.Height;
				break;
		}
	}

	ToolbarPosition DetectBorderPosition(Point posRoot, Visual visualRoot)
	{
		var bounds = visualRoot.Bounds;
		const double edgeThreshold = 100;

		double distTop = posRoot.Y;
		double distBottom = bounds.Height - posRoot.Y;
		double distLeft = posRoot.X;
		double distRight = bounds.Width - posRoot.X;

		double minDist = Math.Min(Math.Min(distTop, distBottom), Math.Min(distLeft, distRight));

		if (minDist == distTop && distTop < edgeThreshold)
			return ToolbarPosition.Top;
		if (minDist == distBottom && distBottom < edgeThreshold)
			return ToolbarPosition.Bottom;
		if (minDist == distLeft && distLeft < edgeThreshold)
			return ToolbarPosition.Left;
		if (minDist == distRight && distRight < edgeThreshold)
			return ToolbarPosition.Right;

		return Position;
	}
}
