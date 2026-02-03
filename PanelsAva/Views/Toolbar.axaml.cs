using Avalonia;
using Avalonia.Controls;
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
	MainView? mainView;

	public Toolbar()
	{
		InitializeComponent();
		gripRect = this.FindControl<Border>("GripRect");
		buttonStack = this.FindControl<StackPanel>("ButtonStack");
		mainGrid = this.FindControl<Grid>("MainGrid");

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

	public void SetMainView(MainView view)
	{
		mainView = view;
	}
}
