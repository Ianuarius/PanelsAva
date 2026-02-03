using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Avalonia;
using System;
using PanelsAva.Services;

namespace PanelsAva.Views;

public class FileTabFloatingPanel : Border
{
	readonly MainView owner;
	readonly Document document;
	readonly Border titleBar;
	readonly TextBlock titleText;
	readonly Button closeButton;
	readonly Image image;
	bool isActive;

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
