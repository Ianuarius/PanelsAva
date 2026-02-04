using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using System;

namespace PanelsAva.Views;

public partial class PanelTabGroup : UserControl
{
	public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<PanelTabGroup, string>(nameof(Title), "Panel");
	public static readonly new StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<PanelTabGroup, object?>(nameof(Content));
	public static readonly StyledProperty<DockGrid?> DockGridProperty = AvaloniaProperty.Register<PanelTabGroup, DockGrid?>(nameof(DockGrid));
	public static readonly StyledProperty<Canvas?> FloatingLayerProperty = AvaloniaProperty.Register<PanelTabGroup, Canvas?>(nameof(FloatingLayer));
	public static readonly StyledProperty<bool> IsFloatingProperty = AvaloniaProperty.Register<PanelTabGroup, bool>(nameof(IsFloating));
	public static readonly StyledProperty<bool> CanFloatProperty = AvaloniaProperty.Register<PanelTabGroup, bool>(nameof(CanFloat), true);

	public TabGroup? TabGroup { get; set; }

	public event EventHandler? CloseRequested;
	public event EventHandler? LayoutChanged;

	StackPanel? tabStrip;
	MainView? mainView;

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

	public DockGrid? DockGrid
	{
		get => GetValue(DockGridProperty);
		set => SetValue(DockGridProperty, value);
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

	public bool CanFloat
	{
		get => GetValue(CanFloatProperty);
		set => SetValue(CanFloatProperty, value);
	}

	/// <summary>Initializes a new PanelTabGroup, setting up the component, data context, and locating the tab strip control.</summary>
	public PanelTabGroup()
	{
		InitializeComponent();
		DataContext = this;
		tabStrip = this.FindControl<StackPanel>("TabStrip");
	}

	/// <summary>Sets whether this panel tab group is floating or docked, updates the tab strip display, and notifies listeners of the layout change.</summary>
	/// <param name="floating">True to make the panel float freely on the screen; false to dock it in the grid layout.</param>
	public void SetFloating(bool floating)
	{
		IsFloating = floating;
		if (floating && TabGroup == null)
		{
			var floatingTabGroup = new TabGroup();
			floatingTabGroup.AddPanel(this);
			floatingTabGroup.ActiveIndex = 0;
		}
		RefreshTabStrip();
		LayoutChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Rebuilds the tab strip by creating a clickable tab for each panel in the TabGroup, marks the active panel, and shows close buttons on tabs only when the group is floating.</summary>
	public void RefreshTabStrip()
	{
		if (tabStrip == null) return;
		if (TabGroup == null) return;

		tabStrip.Children.Clear();

		for (int i = 0; i < TabGroup.Panels.Count; i++)
		{
			var panel = TabGroup.Panels[i];
			var isActive = i == TabGroup.ActiveIndex;
			var tabItem = new PanelTabItem
			{
				Title = panel.Title,
				IsActive = isActive,
				IsCloseVisible = IsFloating,
				ParentGroup = this,
				Tag = panel
			};
			tabItem.Loaded += (s, e) => {
				var border = tabItem.FindControl<Border>("TabBorder");
				if (border != null) border.Tag = panel;
			};
			tabStrip.Children.Add(tabItem);
		}
	}

	/// <summary>Handles left-click on a tab border to initiate dragging the associated panel, calculating the press position relative to the visual root, drag offset, and starting the potential drag operation.</summary>
	/// <param name="sender">The Border control of the tab that was pressed.</param>
	/// <param name="e">The pointer pressed event arguments containing press details.</param>
	public void TabOnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is Border border)
		{
			var e2 = e.GetCurrentPoint(border);
			if (!e2.Properties.IsLeftButtonPressed) return;
			if (mainView?.dragManager == null) return;
			var visualRoot = this.GetVisualRoot() as Visual;
			if (visualRoot == null) return;
			var pressPointRoot = e.GetPosition(visualRoot);
			var panelPos = border.TranslatePoint(new Point(0, 0), visualRoot);
			if (!panelPos.HasValue) return;
			var dragOffset = pressPointRoot - panelPos.Value;
			var offsetRatioX = this.Bounds.Width > 0 ? dragOffset.X / this.Bounds.Width : 0;
			if (border.Tag is PanelTabGroup panel && TabGroup != null)
			{
				mainView.dragManager.StartPotentialDrag(panel, (Pointer)e.Pointer, pressPointRoot, dragOffset.X, dragOffset.Y, offsetRatioX, border, panel.IsFloating);
			}
			e.Handled = true;
		}
	}

	public void TabOnPointerMoved(object? sender, PointerEventArgs e)
	{
		if (mainView?.dragManager == null) return;
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;
		mainView.dragManager.UpdateDrag(e.GetPosition(visualRoot));
		e.Handled = true;
	}

	public void TabOnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (mainView?.dragManager == null) return;
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;
		bool thresholdExceeded = mainView.dragManager.ThresholdExceeded;
		mainView.dragManager.EndDrag(e.GetPosition(visualRoot));
		if (!thresholdExceeded)
		{
			if (sender is Border border && border.Tag is PanelTabGroup panel && TabGroup != null)
			{
				TabGroup.SetActive(panel);
				RefreshTabStrip();
				DockGrid?.RebuildGrid();
			}
		}
		e.Handled = true;
	}

	public void TabOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		mainView?.dragManager?.HandleCaptureLost((Pointer)e.Pointer);
	}

	public void SetMainView(MainView view)
	{
		mainView = view;
	}

	public void RaiseLayoutChanged()
	{
		LayoutChanged?.Invoke(this, EventArgs.Empty);
	}

	public void ClosePanel(PanelTabItem item)
	{
		if (item.Tag is PanelTabGroup panel)
		{
			TabGroup?.RemovePanel(panel);
			panel.CloseRequested?.Invoke(panel, EventArgs.Empty);
			
			if (TabGroup.Panels.Count == 0)
			{
				CloseRequested?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				RefreshTabStrip();
				if (!IsFloating)
				{
					DockGrid?.RebuildGrid();
				}
			}
		}
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

	void MoveToFloatingLayer(Canvas layer, double left, double top)
	{
		MainView.RemoveFromParent(this);
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

}
