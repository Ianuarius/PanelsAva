using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using PanelsAva.Services;

namespace PanelsAva.Views;

public class FileTabItem : Border
{
	readonly MainView owner;
	readonly Document document;
	readonly TextBlock titleText;
	readonly Button closeButton;
	bool isActive;
	bool isFloating;

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
}
