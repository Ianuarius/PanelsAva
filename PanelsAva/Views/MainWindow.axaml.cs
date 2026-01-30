using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using PanelsAva.ViewModels;

namespace PanelsAva.Views;

public partial class MainWindow : Window
{
    readonly Dictionary<string, PanelFloatingWindow> floatingWindows = new();
    MainViewModel? viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (viewModel != null)
            Unhook(viewModel);

        viewModel = DataContext as MainViewModel;

        if (viewModel != null)
            Hook(viewModel);
    }

    void Hook(MainViewModel vm)
    {
        vm.PanelService.Panels.CollectionChanged += PanelsCollectionChanged;
        foreach (var panel in vm.PanelService.Panels)
            AttachPanel(panel);
        UpdateAllPanels();
    }

    void Unhook(MainViewModel vm)
    {
        vm.PanelService.Panels.CollectionChanged -= PanelsCollectionChanged;
        foreach (var panel in vm.PanelService.Panels)
            DetachPanel(panel);
        CloseAllFloating();
    }

    void PanelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (PanelViewModel panel in e.NewItems)
                AttachPanel(panel);
        }

        if (e.OldItems != null)
        {
            foreach (PanelViewModel panel in e.OldItems)
                DetachPanel(panel);
        }

        UpdateAllPanels();
    }

    void AttachPanel(PanelViewModel panel)
    {
        panel.PropertyChanged += PanelPropertyChanged;
    }

    void DetachPanel(PanelViewModel panel)
    {
        panel.PropertyChanged -= PanelPropertyChanged;
        CloseFloating(panel.Id);
    }

    void PanelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not PanelViewModel panel)
            return;

        if (e.PropertyName is nameof(PanelViewModel.IsFloating) or nameof(PanelViewModel.IsVisible) or nameof(PanelViewModel.FloatingBounds))
            UpdateFloating(panel);
    }

    void UpdateAllPanels()
    {
        if (viewModel == null)
            return;

        foreach (var panel in viewModel.PanelService.Panels)
            UpdateFloating(panel);
    }

    void UpdateFloating(PanelViewModel panel)
    {
        if (!panel.IsVisible || !panel.IsFloating)
        {
            CloseFloating(panel.Id);
            return;
        }

        if (floatingWindows.TryGetValue(panel.Id, out var existing))
        {
            ApplyBounds(existing, panel);
            return;
        }

        if (viewModel == null)
            return;

        var window = new PanelFloatingWindow(panel, viewModel.PanelService);

        window.Closing += (_, __) =>
        {
            panel.IsVisible = false;
            panel.IsFloating = false;
        };

        floatingWindows[panel.Id] = window;
        ApplyBounds(window, panel);
        window.Show();
        window.Activate();
    }

    void ApplyBounds(Window window, PanelViewModel panel)
    {
        if (panel.FloatingBounds is null)
            return;

        var bounds = ClampBounds(panel.FloatingBounds.Value);
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        window.Position = new PixelPoint((int)bounds.X, (int)bounds.Y);
    }

    Rect ClampBounds(Rect bounds)
    {
        var screen = Screens.ScreenFromPoint(new PixelPoint((int)bounds.X, (int)bounds.Y)) ?? Screens.Primary;
        if (screen == null)
            return bounds;

        var work = screen.WorkingArea;
        var width = Math.Min(bounds.Width, work.Width);
        var height = Math.Min(bounds.Height, work.Height);
        var x = Math.Clamp(bounds.X, work.X, work.X + work.Width - width);
        var y = Math.Clamp(bounds.Y, work.Y, work.Y + work.Height - height);
        return new Rect(x, y, width, height);
    }

    void CloseFloating(string panelId)
    {
        if (!floatingWindows.TryGetValue(panelId, out var window))
            return;

        floatingWindows.Remove(panelId);
        window.Close();
    }

    void CloseAllFloating()
    {
        foreach (var window in floatingWindows.Values)
            window.Close();
        floatingWindows.Clear();
    }
}
