using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Avalonia;
using System;

namespace PanelsAva.Views;

public class FileTabFloatingPanel : Border
{
	readonly MainView owner;
	readonly Document document;
	readonly Border titleBar;
	readonly TextBlock titleText;
	readonly Button closeButton;
	readonly Image image;
	bool isDragging;
	bool isActive;
	double dragOffsetX;
	double dragOffsetY;
	Pointer? currentPointer;

	public FileTabFloatingPanel(MainView owner, Document document)
	{
		this.owner = owner;
		this.document = document;

		Background = new SolidColorBrush(Color.FromRgb(28, 28, 28));
		BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
		BorderThickness = new Thickness(1);
		Width = 420;
		Height = 300;

		var grid = new Grid
		{
			RowDefinitions = new RowDefinitions("20,*")
		};

		titleBar = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(50, 50, 70)),
			Padding = new Thickness(6, 0, 6, 0)
		};

		var titleGrid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,Auto")
		};

		titleText = new TextBlock
		{
			Text = document.Name,
			FontSize = 12,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Left,
			Foreground = new SolidColorBrush(Colors.White)
		};

		closeButton = new Button
		{
			Content = "✖",
			FontSize = 10,
			Width = 18,
			Height = 18,
			Padding = new Thickness(0),
			Margin = new Thickness(4, 0, -2, 0),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0),
			Foreground = new SolidColorBrush(Colors.White)
		};

		titleGrid.Children.Add(titleText);
		titleGrid.Children.Add(closeButton);
		Grid.SetColumn(closeButton, 1);
		titleBar.Child = titleGrid;

		image = new Image
		{
			Stretch = Stretch.Uniform
		};

		grid.Children.Add(titleBar);
		grid.Children.Add(image);
		Grid.SetRow(image, 1);

		Child = grid;

		closeButton.Click += CloseButtonOnClick;
		closeButton.PointerPressed += CloseButtonOnPointerPressed;

		titleBar.PointerPressed += TitleBarOnPointerPressed;
		titleBar.PointerMoved += TitleBarOnPointerMoved;
		titleBar.PointerReleased += TitleBarOnPointerReleased;
		titleBar.PointerCaptureLost += TitleBarOnPointerCaptureLost;

		var contextMenu = new ContextMenu();
		var closeMenuItem = new MenuItem { Header = "Close" };
		closeMenuItem.Click += CloseMenuItemOnClick;
		contextMenu.Items.Add(closeMenuItem);
		ContextMenu = contextMenu;

		UpdateFromDocument();
	}

	public Document Document => document;

	public void UpdateFromDocument()
	{
		titleText.Text = document.Name;
		image.Source = document.Bitmap;
	}

	public void SetActive(bool active)
	{
		isActive = active;
		titleBar.Background = new SolidColorBrush(isActive ? Color.FromRgb(70, 70, 100) : Color.FromRgb(50, 50, 70));
	}

	public void BeginExternalDrag(IPointer pointer, Point posRoot, double offsetX, double offsetY)
	{
		dragOffsetX = offsetX;
		dragOffsetY = offsetY;
		isDragging = true;
		currentPointer = (Pointer)pointer;
		pointer.Capture(titleBar);
		owner.MoveFloatingPanel(this, posRoot, dragOffsetX, dragOffsetY);
	}

	void CloseButtonOnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		e.Handled = true;
	}

	void CloseButtonOnClick(object? sender, EventArgs e)
	{
		owner.CloseDocument(document);
	}

	void CloseMenuItemOnClick(object? sender, EventArgs e)
	{
		owner.CloseDocument(document);
	}

	void TitleBarOnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		var e2 = e.GetCurrentPoint(titleBar);
		if (!e2.Properties.IsLeftButtonPressed) return;

		owner.SelectDocument(document, false);

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		var pressPointRoot = e.GetPosition(visualRoot);
		var panelPos = this.TranslatePoint(new Point(0, 0), visualRoot);
		if (panelPos.HasValue)
		{
			dragOffsetX = pressPointRoot.X - panelPos.Value.X;
			dragOffsetY = pressPointRoot.Y - panelPos.Value.Y;
		}

		isDragging = true;
		currentPointer = (Pointer)e.Pointer;
		e.Pointer.Capture(titleBar);
		e.Handled = true;
	}

	void TitleBarOnPointerMoved(object? sender, PointerEventArgs e)
	{
		if (!isDragging) return;
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;
		var posRoot = e.GetPosition(visualRoot);
		owner.MoveFloatingPanel(this, posRoot, dragOffsetX, dragOffsetY);
		owner.UpdateDockPreview(posRoot);
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
		owner.TryDockFloatingPanel(this, posRoot);
		owner.ClearDockPreview();
		e.Handled = true;
	}

	void TitleBarOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		isDragging = false;
		currentPointer = null;
		owner.ClearDockPreview();
	}
}
