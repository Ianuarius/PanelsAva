PanelsAva
=======

Dockable Photoshop-style panels (palettes) for Avalonia applications, designed for digital art creation with a layered canvas architecture.

[<img width="1280" height="720" alt="image" src="https://github.com/user-attachments/assets/6f46ae20-aad9-4622-b522-e403c583d6dc" />](https://www.youtube.com/watch?v=wjAVSGwB_X8)

Features
--------
- Dockable, floatable, resizeable panels and hosts similar to Photoshop palettes.
- File tabs that can be dragged out to float like panels, with a Canvas control displayed in floating windows.
- Simple MVVM-friendly structure: Views, ViewModels, Models, and Services provided.
- Dynamic canvas expansion when dock hosts are empty, with hot zones for re-docking panels.
- Persistent panel layouts: positions, orders, sizes, and floating states are automatically saved and restored across application sessions.
- Hidden panels can be reopened from the Window menu and will appear in their last known location, size (relative, more or less), and state.
- Layout can be locked/unlocked from the Window menu, which prevents panels from detaching from a dock.
- Layouts can be saved (auto-saved after first save) and loaded. You can also reset back to default workspace.
- Tool bar that you can dock to the sides of the window.
- Example panels included (Layers, Brushes, Color, History, Properties, Timeline). Placeholder content reacts to selected file.
- Example file tabs with sample images opened in a Canvas control.
- Example tool bar icons.

Quick start
-----------
Requirements
- .NET 8 SDK
- Avalonia UI (used by the project; dependencies are in the solution)

Build & run the desktop example

```powershell
dotnet build PanelsAva.sln
dotnet run --project PanelsAva.Desktop
```

What’s in this repository
-------------------------
- `PanelsAva/Views` — XAML views for each panel and host components (e.g. DockGrid.axaml, PanelTabGroup.axaml, MainWindow.axaml).
- `PanelsAva/ViewModels` — ViewModel implementations used by the sample panels.
- `PanelsAva/Models` — Model classes representing documents and layout configurations.
- `PanelsAva/Services` — Service classes for managing drag operations and other application services.
- `PanelsAva.Desktop` — Desktop entry project that launches the example app.

How to use these panels in your project
---------------------------------------
1. Copy the visual components you want from `PanelsAva/Views` into your project (for example, `DockGrid.axaml` and `PanelTabGroup.axaml`).
2. Copy the corresponding ViewModels (or adapt them) from `PanelsAva/ViewModels` to your app. Ensure any base classes like `ViewModelBase.cs` are included or mapped to your MVVM framework.
3. Copy the necessary Models from `PanelsAva/Models` (e.g., `Document.cs`, `LayoutConfig.cs`) and Services from `PanelsAva/Services` (e.g., `DragManager.cs`) to support the functionality.
4. Add DataTemplates or a ViewLocator so your ViewModels are associated with the Views in XAML.
5. Place the `DockGrid` control in your main window where you want the dockable area to appear. Create instances of `PanelTabGroup` (or your derived panel types) and set their DataContext to the appropriate ViewModel instances.

Example (conceptual)
--------------------
- Add `DockGrid` to your `MainWindow.axaml`.
- Create a `LayersPanelViewModel` instance and add a `PanelTabGroup` containing the `LayersPanel` view, binding its DataContext to the ViewModel.

Extending and creating new panels
---------------------------------
- To create a new panel, add an AXAML view (copy `PanelTabGroup.axaml` or one of the sample panels) and a matching ViewModel in the `ViewModels` folder.
- Implement the panel logic in the ViewModel and expose commands/properties for the UI.
- Use the existing panels as templates — most panels follow a simple pattern: a View + a ViewModel registered or created by the shell.

Core components
---------------
- **`PanelTabGroup`**: [PanelsAva/Views/PanelTabGroup.axaml.cs](PanelsAva/Views/PanelTabGroup.axaml.cs) — container for a dockable panel or a tab group. Handles rendering the title/tab strip and content, switching between floating and docked modes (`SetFloating`, `SetFloatingBounds`), and firing `CloseRequested` / `LayoutChanged` events. Pointer events on tabs are forwarded to the `DragManager` (via `MainView`) to implement dragging and dropping.
- **`PanelTabItem`**: [PanelsAva/Views/PanelTabItem.axaml.cs](PanelsAva/Views/PanelTabItem.axaml.cs) — a single tab UI used by `PanelTabGroup`. Tracks `IsActive`, close visibility, and whether it represents a tab or a single-title header, and delegates tab pointer events to its parent group.
- **`DockGrid`**: [PanelsAva/Views/DockGrid.axaml](PanelsAva/Views/DockGrid.axaml) / [PanelsAva/Views/DockGrid.axaml.cs](PanelsAva/Views/DockGrid.axaml.cs) — the control that manages docked tab groups. It computes drop previews, rebuilds grid columns/rows, manages splitters, and serializes its layout to `DockGridLayout`.
- **`DragManager`**: [PanelsAva/Services/DragManager.cs](PanelsAva/Services/DragManager.cs) — coordinates pointer dragging for tabs and panels: starts drags, enforces a movement threshold, promotes tabs to floating panels (moves UI into the floating `Canvas`), updates floating positions while dragging, shows translucent previews, and resolves drop targets on drag end.
- **`MainView.axaml.cs`**: [PanelsAva/Views/MainView.axaml.cs](PanelsAva/Views/MainView.axaml.cs) — central UI wiring: finds named controls on load, instantiates default panels, creates/owns the `DragManager`, coordinates floating/docking, and updates the canvas and file tabs.
- **`MainView` layout code**: [PanelsAva/Views/MainView.Layout.cs](PanelsAva/Views/MainView.Layout.cs) — top-level layout coordinator. It builds `LayoutConfig` from the current UI, applies saved configs, schedules saves, and ties together `DockGrid` hosts, the floating layer, and the toolbar position. It's the place where layout persistence and runtime docking logic meet.
- **`MainView` tab layer**: [PanelsAva/Views/MainView.Tabs.cs](PanelsAva/Views/MainView.Tabs.cs) — manages the file tab strip and floating file panels. It keeps `fileTabs`, `floatingPanels`, and `floatingDocuments` maps, creates/removes tab UI and floating panels, updates active states, performs `BeginFloatingTab` / `MoveFloatingTab` / `TryDockFloatingTab`, and draws the insertion preview when reordering docked file tabs.
- **LayoutConfig / WorkspaceProfiles / PanelState**: [PanelsAva/Models/LayoutConfig.cs](PanelsAva/Models/LayoutConfig.cs) — JSON-friendly classes used to persist the workspace layout: dock hosts, per-panel visibility/floating/tab state, and saved sizes. `WorkspaceProfiles` keeps named layout profiles.
- **`Document`**: [PanelsAva/Document.cs](PanelsAva/Document.cs) — holds a document's `Bitmap` and a simple `Layers` collection. It's the in-memory model for an open file.
- **File tabs**: `FileTabItem` and `FileTabFloatingPanel` ([PanelsAva/Views/FileTabItem.cs](PanelsAva/Views/FileTabItem.cs), [PanelsAva/Views/FileTabFloatingPanel.cs](PanelsAva/Views/FileTabFloatingPanel.cs)) — `FileTabItem` is the clickable tab representing an open `Document`. It starts drags, toggles active state, and asks `MainView` to select or float the document. `FileTabFloatingPanel` is the little floating preview window created when a file tab is dragged out; it shows a title bar and a preview image and can be moved/resized while floating.
- **`Toolbar`**: [PanelsAva/Views/Toolbar.axaml.cs](PanelsAva/Views/Toolbar.axaml.cs) — exposes `ToolbarPosition`, updates orientation (`UpdateOrientation()`), raises `PositionChanged`, and exposes a `FloatingLayer` property.
- **`MainViewModel`**: [PanelsAva/ViewModels/MainViewModel.cs](PanelsAva/ViewModels/MainViewModel.cs) — demo view-model holding `OpenDocuments`, `CurrentDocumentIndex`, and `SelectedDocument`; loads sample bitmaps and notifies the view on changes.

TODO
----
- Regression in reorganizing file tabs: dropped tab always goes to the end. Need to investigate.
- Closing and reopening panels in TabStrips seems a bit fragile and might sometimes break the panel open state. Need to investigate.
- Reorganize panels in tabstrip by dragging them to a specific location.

License
-------
CC BY 4.0
