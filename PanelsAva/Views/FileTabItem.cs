using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.VisualTree;
using System;
using PanelsAva;
using Avalonia;

namespace PanelsAva.Views;

public class FileTabItem : Border
{
	readonly MainView owner;
	readonly Document document;
	readonly TextBlock titleText;
	readonly Button closeButton;
	bool isActive;
	bool isDragging;
	bool isFloating;
	Point pressPointRoot;
	Point tabPosAtPressRoot;
	double dragOffsetX;
	double dragOffsetY;
	Pointer? currentPointer;
	bool movedWhileDocked;

	public FileTabItem(MainView owner, Document document)
	{
		this.owner = owner;
		this.document = document;

		Padding = new Thickness(6, 0, 6, 0);
		BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 50));
		BorderThickness = new Thickness(1, 0, 1, 0);
		Background = new SolidColorBrush(Color.FromRgb(50, 50, 70));

		var grid = new Grid
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
			Width = 16,
			Height = 16,
			Padding = new Thickness(0),
			Margin = new Thickness(4, 0, 0, 0),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0),
			Foreground = new SolidColorBrush(Colors.White)
		};

		grid.Children.Add(titleText);
		grid.Children.Add(closeButton);
		Grid.SetColumn(closeButton, 1);
		Child = grid;

		closeButton.Click += CloseButtonOnClick;
		closeButton.PointerPressed += CloseButtonOnPointerPressed;

		PointerPressed += TabOnPointerPressed;
		PointerMoved += TabOnPointerMoved;
		PointerReleased += TabOnPointerReleased;
		PointerCaptureLost += TabOnPointerCaptureLost;

		var contextMenu = new ContextMenu();
		var closeMenuItem = new MenuItem { Header = "Close" };
		closeMenuItem.Click += CloseMenuItemOnClick;
		contextMenu.Items.Add(closeMenuItem);
		ContextMenu = contextMenu;
	}

	public Document Document => document;

	public void SetActive(bool active)
	{
		isActive = active;
		Background = new SolidColorBrush(isActive ? Color.FromRgb(70, 70, 100) : Color.FromRgb(50, 50, 70));
	}

	public void SetFloating(bool floating)
	{
		isFloating = floating;
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

	void TabOnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		var e2 = e.GetCurrentPoint(this);
		if (!e2.Properties.IsLeftButtonPressed) return;

		owner.SelectDocument(document, true);

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		pressPointRoot = e.GetPosition(visualRoot);
		var tabPos = this.TranslatePoint(new Point(0, 0), visualRoot);
		if (tabPos.HasValue)
		{
			tabPosAtPressRoot = tabPos.Value;
			dragOffsetX = pressPointRoot.X - tabPosAtPressRoot.X;
			dragOffsetY = pressPointRoot.Y - tabPosAtPressRoot.Y;
		}

		isDragging = true;
		movedWhileDocked = false;
		currentPointer = (Pointer)e.Pointer;
		e.Pointer.Capture(this);
		e.Handled = true;
	}

	void TabOnPointerMoved(object? sender, PointerEventArgs e)
	{
		if (!isDragging) return;

		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;

		var posRoot = e.GetPosition(visualRoot);
		var delta = posRoot - pressPointRoot;
		double scale = 1.0;
		var threshold = 10 * scale;
		if (!isFloating)
		{
			if (delta.X * delta.X + delta.Y * delta.Y >= threshold * threshold)
			{
				owner.BeginFloatingTab(this, posRoot, e.Pointer, dragOffsetX, dragOffsetY);
				isFloating = true;
				owner.MoveFloatingTab(this, posRoot, dragOffsetX, dragOffsetY);
			}
			else
			{
				if (Math.Abs(delta.X) > threshold)
					movedWhileDocked = true;
			}
		}
		else
		{
			owner.MoveFloatingTab(this, posRoot, dragOffsetX, dragOffsetY);
		}

		owner.UpdateDockPreview(posRoot);

		e.Handled = true;
	}

	void TabOnPointerReleased(object? sender, PointerReleasedEventArgs e)
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
			owner.TryDockFloatingTab(this, posRoot);
		}
		else if (movedWhileDocked)
		{
			owner.ReorderDockedTab(this, posRoot);
		}
		owner.ClearDockPreview();

		e.Handled = true;
	}

	void TabOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		isDragging = false;
		currentPointer = null;
		owner.ClearDockPreview();
	}
}
