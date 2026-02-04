using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;

namespace PanelsAva.Views;

public partial class PanelTabItem : UserControl
{
	public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<PanelTabItem, string>(nameof(Title));
	public static readonly StyledProperty<bool> IsActiveProperty = AvaloniaProperty.Register<PanelTabItem, bool>(nameof(IsActive));
	public static readonly StyledProperty<bool> IsCloseVisibleProperty = AvaloniaProperty.Register<PanelTabItem, bool>(nameof(IsCloseVisible));
	public static readonly StyledProperty<bool> IsTabProperty = AvaloniaProperty.Register<PanelTabItem, bool>(nameof(IsTab));
	public static readonly StyledProperty<PanelTabGroup> ParentGroupProperty = AvaloniaProperty.Register<PanelTabItem, PanelTabGroup>(nameof(ParentGroup));

	public PanelTabItem()
	{
		InitializeComponent();
		DataContext = this;
		CloseButton.Click += CloseButtonOnClick;
		TabBorder.PointerPressed += TabBorderOnPointerPressed;
		TabBorder.PointerMoved += TabBorderOnPointerMoved;
		TabBorder.PointerReleased += TabBorderOnPointerReleased;
		TabBorder.PointerCaptureLost += TabBorderOnPointerCaptureLost;
		UpdateBackground();
		UpdateCloseVisibility();
	}

	public string Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public bool IsActive
	{
		get => GetValue(IsActiveProperty);
		set => SetValue(IsActiveProperty, value);
	}

	public bool IsCloseVisible
	{
		get => GetValue(IsCloseVisibleProperty);
		set => SetValue(IsCloseVisibleProperty, value);
	}

	public bool IsTab
	{
		get => GetValue(IsTabProperty);
		set => SetValue(IsTabProperty, value);
	}

	public PanelTabGroup ParentGroup
	{
		get => GetValue(ParentGroupProperty);
		set => SetValue(ParentGroupProperty, value);
	}

	/// <summary>Store a reference to the underlying panel object for each tab so the UI can map a clicked/tabbed control back to the panel it represents.</summary>
	public object Tag { get; set; }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == IsActiveProperty)
		{
			UpdateBackground();
		}
		else if (change.Property == IsCloseVisibleProperty)
		{
			UpdateCloseVisibility();
		}
		else if (change.Property == IsTabProperty)
		{
			if (IsTab && TabBorder.ContextMenu == null)
			{
				var tabContextMenu = new ContextMenu();
				var tabCloseMenuItem = new MenuItem { Header = "Close" };
				tabCloseMenuItem.Click += (s, e) => ParentGroup?.ClosePanel(this);
				tabContextMenu.Items.Add(tabCloseMenuItem);
				TabBorder.ContextMenu = tabContextMenu;
			}
		}
	}

	private void UpdateBackground()
	{
		TabBorder.Background = IsActive ? new SolidColorBrush(Color.FromRgb(58, 58, 58)) : new SolidColorBrush(Color.FromRgb(42, 42, 42));
	}

	private void UpdateCloseVisibility()
	{
		CloseButton.IsVisible = IsCloseVisible;
	}

	private void CloseButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		ParentGroup?.ClosePanel(this);
	}

	private void TabBorderOnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		ParentGroup?.TabOnPointerPressed(TabBorder, e);
	}

	private void TabBorderOnPointerMoved(object? sender, PointerEventArgs e)
	{
		ParentGroup?.TabOnPointerMoved(TabBorder, e);
	}

	private void TabBorderOnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		ParentGroup?.TabOnPointerReleased(TabBorder, e);
	}

	private void TabBorderOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		ParentGroup?.TabOnPointerCaptureLost(TabBorder, e);
	}
}