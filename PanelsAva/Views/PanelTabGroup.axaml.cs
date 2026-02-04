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
		RefreshTabStrip();
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

	public void ClosePanel(PanelTabItem item)
	{
		if (item.Tag == this)
		{
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}
		else if (item.Tag is PanelTabGroup panel)
		{
			TabGroup?.RemovePanel(panel);
			// Mark the panel as hidden
			panel.CloseRequested?.Invoke(panel, EventArgs.Empty);
			
			if (TabGroup.Panels.Count == 0)
			{
				// Group is empty, close the entire container
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

	/// <summary>Rebuilds the tab strip UI to match the current panel state: shows a single title bar for one panel, individual clickable tabs for multiple panels, and adds close buttons only when floating. Clears and repopulates the tab strip controls accordingly.</summary>
	public void RefreshTabStrip()
	{
		if (tabStrip == null) return;

		tabStrip.Children.Clear();

		if (TabGroup == null || TabGroup.Panels.Count == 0)
		{
			if (IsFloating)
			{
				var titleItem = new PanelTabItem
				{
					Title = this.Title,
					IsActive = true,
					IsCloseVisible = true,
					IsTab = true,
					ParentGroup = this,
					Tag = this
				};
				titleItem.Loaded += (s, e) => {
					var border = titleItem.FindControl<Border>("TabBorder");
					if (border != null) border.Tag = this;
				};
				tabStrip.Children.Add(titleItem);
			}
			return;
		}

		bool isSingle = TabGroup.Panels.Count == 1;
		if (isSingle)
		{
			var titleItem = new PanelTabItem
			{
				Title = this.Title,
				IsActive = true,
				IsCloseVisible = IsFloating,
				IsTab = false,
				ParentGroup = this,
				Tag = this
			};
			titleItem.Loaded += (s, e) => {
				var border = titleItem.FindControl<Border>("TabBorder");
				if (border != null) border.Tag = this;
			};
			tabStrip.Children.Add(titleItem);
		}
		else
		{
			for (int i = 0; i < TabGroup.Panels.Count; i++)
			{
				var panel = TabGroup.Panels[i];
				var isActive = i == TabGroup.ActiveIndex;
				var tabItem = new PanelTabItem
				{
					Title = panel.Title,
					IsActive = isActive,
					IsCloseVisible = IsFloating,
					IsTab = true,
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
	}

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
			var panelPos = this.TranslatePoint(new Point(0, 0), visualRoot);
			if (!panelPos.HasValue) return;
			var dragOffset = pressPointRoot - panelPos.Value;
			var offsetRatioX = this.Bounds.Width > 0 ? dragOffset.X / this.Bounds.Width : 0;
			if (border.Tag is PanelTabGroup panel && TabGroup != null)
			{
				mainView.dragManager.StartPotentialDrag(panel, (Pointer)e.Pointer, pressPointRoot, dragOffset.X, dragOffset.Y, offsetRatioX, border, panel.IsFloating);
			}
			else if (border.Tag == this)
			{
				mainView.dragManager.StartPotentialDrag(this, (Pointer)e.Pointer, pressPointRoot, dragOffset.X, dragOffset.Y, offsetRatioX, border, IsFloating);
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

}
