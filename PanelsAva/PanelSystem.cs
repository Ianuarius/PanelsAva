using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using PanelsAva.ViewModels;

namespace PanelsAva;

public enum DockZone
{
    Left,
    Right,
    Bottom,
    Center
}

public interface IPanelPersistence
{
    Task<PanelLayoutState?> LoadAsync();
    Task SaveAsync(PanelLayoutState state);
}

public class PanelLayoutState
{
    public List<PanelLayoutItem> Panels { get; set; } = [];
}

public class PanelLayoutItem
{
    public string Id { get; set; } = "";
    public bool IsVisible { get; set; }
    public bool IsCollapsed { get; set; }
    public bool IsFloating { get; set; }
    public DockZone DockZone { get; set; }
    public Rect? FloatingBounds { get; set; }
}

public class PanelFilePersistence : IPanelPersistence
{
    readonly string filePath;
    readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PanelFilePersistence(string appName)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
        Directory.CreateDirectory(dir);
        filePath = Path.Combine(dir, "panel-layout.json");
    }

    public async Task<PanelLayoutState?> LoadAsync()
    {
        if (!File.Exists(filePath))
            return null;

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<PanelLayoutState>(stream, options);
    }

    public async Task SaveAsync(PanelLayoutState state)
    {
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, state, options);
    }
}

public class PanelService
{
    readonly IPanelPersistence persistence;

    public PanelService(IPanelPersistence persistence)
    {
        this.persistence = persistence;
    }

    public ObservableCollection<PanelViewModel> Panels { get; } = new();

    public void Register(PanelViewModel panel)
    {
        panel.Connect(this);
        Panels.Add(panel);
    }

    public PanelViewModel? Get(string id) => Panels.FirstOrDefault(panel => panel.Id == id);

    public void Toggle(string id)
    {
        var panel = Get(id);
        if (panel == null)
            return;

        panel.IsVisible = !panel.IsVisible;
        if (!panel.IsVisible)
            panel.IsFloating = false;
    }

    public void Show(string id)
    {
        var panel = Get(id);
        if (panel == null)
            return;

        panel.IsVisible = true;
    }

    public void Hide(string id)
    {
        var panel = Get(id);
        if (panel == null)
            return;

        panel.IsVisible = false;
        panel.IsFloating = false;
    }

    public void Dock(PanelViewModel panel, DockZone zone)
    {
        panel.DockZone = zone;
        panel.IsFloating = false;
        panel.IsVisible = true;
    }

    public void Float(PanelViewModel panel)
    {
        panel.IsFloating = true;
        panel.IsVisible = true;
    }

    public async Task LoadAsync()
    {
        var state = await persistence.LoadAsync();
        if (state == null)
            return;

        ApplyLayout(state);
    }

    public async Task SaveAsync()
    {
        var state = BuildLayout();
        await persistence.SaveAsync(state);
    }

    PanelLayoutState BuildLayout()
    {
        var state = new PanelLayoutState();
        foreach (var panel in Panels)
        {
            state.Panels.Add(new PanelLayoutItem
            {
                Id = panel.Id,
                IsVisible = panel.IsVisible,
                IsCollapsed = panel.IsCollapsed,
                IsFloating = panel.IsFloating,
                DockZone = panel.DockZone,
                FloatingBounds = panel.FloatingBounds
            });
        }

        return state;
    }

    void ApplyLayout(PanelLayoutState state)
    {
        foreach (var item in state.Panels)
        {
            var panel = Get(item.Id);
            if (panel == null)
                continue;

            panel.IsVisible = item.IsVisible;
            panel.IsCollapsed = item.IsCollapsed;
            panel.IsFloating = item.IsFloating;
            panel.DockZone = item.DockZone;
            panel.FloatingBounds = item.FloatingBounds;
        }
    }

    public void UpdateFloatingBounds(PanelViewModel panel, Rect bounds)
    {
        panel.FloatingBounds = bounds;
    }
}