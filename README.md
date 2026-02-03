PanelsAva
=======

Dockable Photoshop-style panels (palettes) for Avalonia applications.

[<img width="1280" height="720" alt="image" src="https://github.com/user-attachments/assets/6f46ae20-aad9-4622-b522-e403c583d6dc" />](https://www.youtube.com/watch?v=wjAVSGwB_X8)

Features
--------
- Dockable, floatable, resizeable panels and hosts similar to Photoshop palettes.
- File tabs that can be dragged out to float like panels, with a Canvas control displayed in floating windows.
- Simple MVVM-friendly structure: Views + ViewModels provided.
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
- `PanelsAva.Desktop` — Desktop entry project that launches the example app.

How to use these panels in your project
---------------------------------------
1. Copy the visual components you want from `PanelsAva/Views` into your project (for example, `DockGrid.axaml` and `PanelTabGroup.axaml`).
2. Copy the corresponding ViewModels (or adapt them) from `PanelsAva/ViewModels` to your app. Ensure any base classes like `ViewModelBase.cs` are included or mapped to your MVVM framework.
3. Add DataTemplates or a ViewLocator so your ViewModels are associated with the Views in XAML.
4. Place the `DockGrid` control in your main window where you want the dockable area to appear. Create instances of `PanelTabGroup` (or your derived panel types) and set their DataContext to the appropriate ViewModel instances.

Example (conceptual)
--------------------
- Add `DockGrid` to your `MainWindow.axaml`.
- Create a `LayersPanelViewModel` instance and add a `PanelTabGroup` containing the `LayersPanel` view, binding its DataContext to the ViewModel.

Extending and creating new panels
---------------------------------
- To create a new panel, add an AXAML view (copy `PanelTabGroup.axaml` or one of the sample panels) and a matching ViewModel in the `ViewModels` folder.
- Implement the panel logic in the ViewModel and expose commands/properties for the UI.
- Use the existing panels as templates — most panels follow a simple pattern: a View + a ViewModel registered or created by the shell.

Where to look in this repo
--------------------------
- Main window and host implementation: [PanelsAva/Views/MainWindow.axaml](PanelsAva/Views/MainWindow.axaml)
- Dock host control: [PanelsAva/Views/DockGrid.axaml](PanelsAva/Views/DockGrid.axaml)
- Dockable panel example: [PanelsAva/Views/PanelTabGroup.axaml](PanelsAva/Views/PanelTabGroup.axaml)
- Example ViewModels: [PanelsAva/ViewModels](PanelsAva/ViewModels)

TODO
----
- Reorganize panels in tabstrip by dragging them to a specific location.
- Closing and reopening panels in TabStrips seems a bit fragile and might sometimes break the panel open state. Need to investigate properly at some point.

License
-------
CC BY 4.0
