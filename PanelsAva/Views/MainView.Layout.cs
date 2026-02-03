using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Collections;
using PanelsAva.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PanelsAva.Views;

public partial class MainView
{
	void UpdatePanelFileNames()
	{
		var name = currentViewModel?.SelectedDocument?.Name ?? string.Empty;
		var panelUpdates = new[]
		{
			(panel: layersPanel, viewType: typeof(LayersPanel), vmType: typeof(ViewModels.LayersViewModel)),
			(panel: propertiesPanel, viewType: typeof(PropertiesPanel), vmType: typeof(ViewModels.PropertiesViewModel)),
			(panel: colorPanel, viewType: typeof(ColorPanel), vmType: typeof(ViewModels.ColorViewModel)),
			(panel: brushesPanel, viewType: typeof(BrushesPanel), vmType: typeof(ViewModels.BrushesViewModel)),
			(panel: historyPanel, viewType: typeof(HistoryPanel), vmType: typeof(ViewModels.HistoryViewModel)),
			(panel: timelinePanel, viewType: typeof(TimelinePanel), vmType: typeof(ViewModels.TimelineViewModel))
		};

		for (int i = 0; i < panelUpdates.Length; i++)
		{
			var update = panelUpdates[i];
			if (update.panel?.Content?.GetType() == update.viewType && update.panel.Content is Control view && view.DataContext?.GetType() == update.vmType)
			{
				var prop = update.vmType.GetProperty("CurrentFileName");
				prop?.SetValue(view.DataContext, name);
			}
		}
	}

	void HookLayoutEvents()
	{
		var hosts = new[] { leftDockGrid, rightDockGrid, bottomDockGrid };
		for (int i = 0; i < hosts.Length; i++)
		{
			if (hosts[i] != null)
			{
				hosts[i]!.LayoutChanged -= OnLayoutChanged;
				hosts[i]!.LayoutChanged += OnLayoutChanged;
			}
		}

		var panels = new[] { layersPanel, propertiesPanel, colorPanel, brushesPanel, historyPanel, timelinePanel };
		for (int i = 0; i < panels.Length; i++)
		{
			if (panels[i] != null)
			{
				panels[i]!.LayoutChanged -= OnLayoutChanged;
				panels[i]!.LayoutChanged += OnLayoutChanged;
			}
		}

		var splitters = new[] { leftDockSplitter, rightDockSplitter, bottomDockSplitter };
		for (int i = 0; i < splitters.Length; i++)
		{
			if (splitters[i] != null)
			{
				splitters[i]!.PointerReleased -= OnSplitterPointerReleased;
				splitters[i]!.PointerReleased += OnSplitterPointerReleased;
				splitters[i]!.PointerCaptureLost -= OnSplitterPointerCaptureLost;
				splitters[i]!.PointerCaptureLost += OnSplitterPointerCaptureLost;
			}
		}
	}

	void OnSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		SyncLayoutConfig(true, true);
	}

	void OnSplitterPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		SyncLayoutConfig(true, true);
	}

	void OnLayoutChanged(object? sender, EventArgs e)
	{
		SyncLayoutConfig(true, true);
	}

	void SyncLayoutConfig(bool notify, bool scheduleSave)
	{
		if (isApplyingLayout) return;
		var config = BuildLayoutConfig();
		layoutConfig = config;
		if (scheduleSave)
			ScheduleLayoutSave();
		if (notify)
			PanelVisibilityChanged?.Invoke(this, EventArgs.Empty);
	}

	void LoadAndApplyLayout()
	{
		if (defaultLayoutConfig == null)
			defaultLayoutConfig = BuildLayoutConfig();
		workspaceProfiles = LoadWorkspaceProfiles();
		activeProfileName = workspaceProfiles.ActiveProfile;
		var config = GetProfileConfig(activeProfileName);
		if (config != null)
			ApplyLayoutConfig(config);
	}

	string GetLayoutConfigPath()
	{
		var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		return Path.Combine(root, "PanelsAva", "layout.json");
	}

	WorkspaceProfiles LoadWorkspaceProfiles()
	{
		var profiles = new WorkspaceProfiles();
		try
		{
			var path = GetLayoutConfigPath();
			if (File.Exists(path))
			{
				var json = File.ReadAllText(path);
				var existingProfiles = JsonSerializer.Deserialize<WorkspaceProfiles>(json);
				if (existingProfiles != null && existingProfiles.Profiles.Count > 0)
				{
					profiles = existingProfiles;
				}
				else
				{
					var legacy = JsonSerializer.Deserialize<LayoutConfig>(json);
					if (legacy != null)
					{
						profiles.Profiles[legacyProfileName] = legacy;
						profiles.ActiveProfile = legacyProfileName;
					}
				}
			}
		}
		catch
		{
		}
		profiles = EnsureDefaultProfile(profiles);
		WriteWorkspaceProfiles(profiles);
		return profiles;
	}

	WorkspaceProfiles EnsureDefaultProfile(WorkspaceProfiles profiles)
	{
		if (!profiles.Profiles.ContainsKey(defaultProfileName) && defaultLayoutConfig != null)
			profiles.Profiles[defaultProfileName] = defaultLayoutConfig;
		if (string.IsNullOrWhiteSpace(profiles.ActiveProfile) || !profiles.Profiles.ContainsKey(profiles.ActiveProfile))
			profiles.ActiveProfile = defaultProfileName;
		return profiles;
	}

	LayoutConfig? GetProfileConfig(string name)
	{
		if (workspaceProfiles == null) return null;
		if (workspaceProfiles.Profiles.TryGetValue(name, out var config))
			return config;
		if (IsDefaultProfile(name) && defaultLayoutConfig != null)
			return defaultLayoutConfig;
		return null;
	}

	bool IsDefaultProfile(string name)
	{
		return string.Equals(name, defaultProfileName, StringComparison.OrdinalIgnoreCase);
	}

	void WriteWorkspaceProfiles(WorkspaceProfiles profiles)
	{
		try
		{
			var path = GetLayoutConfigPath();
			var dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);
			var json = JsonSerializer.Serialize(profiles);
			File.WriteAllText(path, json);
		}
		catch
		{
		}
	}

	void ScheduleLayoutSave()
	{
		if (isApplyingLayout) return;
		if (layoutSaveTimer == null)
		{
			layoutSaveTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(300)
			};
			layoutSaveTimer.Tick += OnLayoutSaveTimerTick;
		}
		layoutSaveTimer.Stop();
		layoutSaveTimer.Start();
	}

	void OnLayoutSaveTimerTick(object? sender, EventArgs e)
	{
		if (layoutSaveTimer != null)
			layoutSaveTimer.Stop();
		SaveLayoutConfig();
	}

	void SaveLayoutConfig()
	{
		if (preserveLayoutOnSave && layoutConfig != null)
		{
			preserveLayoutOnSave = false;
			if (workspaceProfiles == null)
				return;
			if (string.IsNullOrWhiteSpace(activeProfileName))
				activeProfileName = workspaceProfiles.ActiveProfile;
			if (IsDefaultProfile(activeProfileName))
			{
				defaultLayoutConfig = layoutConfig;
				workspaceProfiles.Profiles[defaultProfileName] = layoutConfig;
				WriteWorkspaceProfiles(workspaceProfiles);
				return;
			}
			workspaceProfiles.Profiles[activeProfileName] = layoutConfig;
			WriteWorkspaceProfiles(workspaceProfiles);
			return;
		}
		if (workspaceProfiles == null)
		{
			var config = BuildLayoutConfig();
			layoutConfig = config;
			return;
		}
		if (string.IsNullOrWhiteSpace(activeProfileName))
			activeProfileName = workspaceProfiles.ActiveProfile;
		var activeConfig = BuildLayoutConfig();
		layoutConfig = activeConfig;
		if (IsDefaultProfile(activeProfileName))
		{
			defaultLayoutConfig = activeConfig;
			workspaceProfiles.Profiles[defaultProfileName] = activeConfig;
			WriteWorkspaceProfiles(workspaceProfiles);
			return;
		}
		workspaceProfiles.Profiles[activeProfileName] = activeConfig;
		WriteWorkspaceProfiles(workspaceProfiles);
	}

	LayoutConfig BuildLayoutConfig()
	{
		var config = new LayoutConfig();
		config.LeftDockGrid = leftDockGrid?.GetLayout();
		config.RightDockGrid = rightDockGrid?.GetLayout();
		config.BottomDockGrid = bottomDockGrid?.GetLayout();
		config.LeftDockWidth = GetLeftDockWidth();
		config.RightDockWidth = GetRightDockWidth();
		config.BottomDockHeight = GetBottomDockHeight();
		if (toolbar != null)
			config.ToolbarPosition = toolbar.Position.ToString();

		var existingStates = new Dictionary<string, PanelState>();
		if (layoutConfig != null)
		{
			for (int i = 0; i < layoutConfig.Panels.Count; i++)
				existingStates[layoutConfig.Panels[i].Title] = layoutConfig.Panels[i];
		}

		var states = new Dictionary<string, PanelState>();
		ApplyDockGridStates(config.LeftDockGrid, states, existingStates);
		ApplyDockGridStates(config.RightDockGrid, states, existingStates);
		ApplyDockGridStates(config.BottomDockGrid, states, existingStates);

		var panels = GetAllPanels();
		for (int i = 0; i < panels.Count; i++)
		{
			var panel = panels[i];
			if (panel.IsFloating)
			{
				var state = GetOrCreateState(panel.Title, states, existingStates, panel);
				state.IsHidden = false;
				state.IsFloating = true;
				state.IsTabbed = false;
				var left = Canvas.GetLeft(panel);
				var top = Canvas.GetTop(panel);
				state.FloatingLeft = double.IsNaN(left) ? 0 : left;
				state.FloatingTop = double.IsNaN(top) ? 0 : top;
				state.FloatingWidth = panel.Bounds.Width;
				state.FloatingHeight = panel.Bounds.Height;
			}
		}

		for (int i = 0; i < panels.Count; i++)
		{
			var panel = panels[i];
			if (!states.ContainsKey(panel.Title))
			{
				var state = GetOrCreateState(panel.Title, states, existingStates, panel);
				state.IsHidden = true;
			}
		}

		config.Panels = new List<PanelState>(states.Values);
		RebuildPanelStateCache();
		return config;
	}

	void ApplyDockGridStates(DockGridLayout? layout, Dictionary<string, PanelState> states, Dictionary<string, PanelState> existingStates)
	{
		if (layout == null) return;
		for (int i = 0; i < layout.Items.Count; i++)
		{
			var item = layout.Items[i];
			for (int j = 0; j < item.Panels.Count; j++)
			{
				var title = item.Panels[j];
				var state = GetOrCreateState(title, states, existingStates, null);
				state.IsHidden = false;
				state.IsFloating = false;
				state.IsTabbed = item.Panels.Count > 1;
				state.DockEdge = layout.DockEdge;
				state.DockIndex = i;
				state.TabIndex = j;
				state.WasActive = item.ActiveIndex == j;
				if (i < layout.ItemSizes.Count)
					state.DockedProportion = layout.ItemSizes[i];
			}
		}
	}

	PanelState GetOrCreateState(string title, Dictionary<string, PanelState> states, Dictionary<string, PanelState> existingStates, PanelTabGroup? panel)
	{
		if (states.TryGetValue(title, out var state))
			return state;
		if (existingStates.TryGetValue(title, out var existing))
		{
			state = new PanelState();
			state.CopyFrom(existing);
			states[title] = state;
			return state;
		}
		state = new PanelState
		{
			Title = title
		};
		if (panel != null && panel.DockGrid != null)
			state.DockEdge = panel.DockGrid.DockEdge.ToString();
		states[title] = state;
		return state;
	}

	double GetLeftDockWidth()
	{
		if (mainGrid == null || mainGrid.ColumnDefinitions.Count < 2) return leftDockWidth.Value;
		var leftCol = mainGrid.ColumnDefinitions[1];
		return leftCol.Width.Value > 0 ? leftCol.Width.Value : leftDockWidth.Value;
	}

	double GetRightDockWidth()
	{
		if (mainGrid == null || mainGrid.ColumnDefinitions.Count < 6) return rightDockWidth.Value;
		var rightCol = mainGrid.ColumnDefinitions[5];
		return rightCol.Width.Value > 0 ? rightCol.Width.Value : rightDockWidth.Value;
	}

	double GetBottomDockHeight()
	{
		if (mainGrid == null || mainGrid.RowDefinitions.Count < 4) return bottomDockHeight.Value;
		var bottomRow = mainGrid.RowDefinitions[3];
		return bottomRow.Height.Value > 0 ? bottomRow.Height.Value : bottomDockHeight.Value;
	}

	void ApplyLayoutConfig(LayoutConfig config)
	{
		if (mainGrid == null) return;
		isApplyingLayout = true;
		try
		{
			layoutConfig = config;
			RebuildPanelStateCache();
			if (mainGrid.ColumnDefinitions.Count >= 6)
			{
				if (config.LeftDockWidth > 0)
					mainGrid.ColumnDefinitions[1].Width = new GridLength(config.LeftDockWidth, GridUnitType.Pixel);
				if (config.RightDockWidth > 0)
					mainGrid.ColumnDefinitions[5].Width = new GridLength(config.RightDockWidth, GridUnitType.Pixel);
				mainGrid.ColumnDefinitions[2].Width = leftSplitterWidth;
				mainGrid.ColumnDefinitions[4].Width = rightSplitterWidth;
			}
			if (mainGrid.RowDefinitions.Count >= 4)
			{
				if (config.BottomDockHeight > 0)
					mainGrid.RowDefinitions[3].Height = new GridLength(config.BottomDockHeight, GridUnitType.Pixel);
				mainGrid.RowDefinitions[2].Height = bottomSplitterHeight;
			}
			if (toolbar != null && !string.IsNullOrEmpty(config.ToolbarPosition))
			{
				if (Enum.TryParse<ToolbarPosition>(config.ToolbarPosition, out var pos))
				{
					toolbar.Position = pos;
					UpdateToolbarPosition(pos);
				}
			}

			ClearAllPanels();
			ApplyDockGridLayout(leftDockGrid, config.LeftDockGrid);
			ApplyDockGridLayout(rightDockGrid, config.RightDockGrid);
			ApplyDockGridLayout(bottomDockGrid, config.BottomDockGrid);
			ApplyFloatingPanels(config);
			UpdateDockHostSizes();
		}
		finally
		{
			isApplyingLayout = false;
		}
	}

	void ClearAllPanels()
	{
		var panels = GetAllPanels();
		for (int i = 0; i < panels.Count; i++)
			RemoveFromParent(panels[i]);
			
		leftDockGrid?.ClearPanels();
		rightDockGrid?.ClearPanels();
		bottomDockGrid?.ClearPanels();
	}

	void ApplyDockGridLayout(DockGrid? host, DockGridLayout? layout)
	{
		if (host == null) return;
		NormalizeItemSizes(layout);
		host.ApplyLayout(layout, FindPanelByTitle);
	}

	void NormalizeItemSizes(DockGridLayout? layout)
	{
		if (layout == null) return;
		while (layout.ItemSizes.Count < layout.Items.Count)
			layout.ItemSizes.Add(0);
		if (layout.ItemSizes.Count > layout.Items.Count)
			layout.ItemSizes.RemoveRange(layout.Items.Count, layout.ItemSizes.Count - layout.Items.Count);
	}

	PanelState? GetPanelStateFromConfig(LayoutConfig config, string title)
	{
		for (int i = 0; i < config.Panels.Count; i++)
		{
			if (config.Panels[i].Title == title)
				return config.Panels[i];
		}
		return null;
	}

	void RebuildPanelStateCache()
	{
		panelStateCache.Clear();
		if (layoutConfig != null)
		{
			for (int i = 0; i < layoutConfig.Panels.Count; i++)
			{
				var panel = layoutConfig.Panels[i];
				panelStateCache[panel.Title] = panel;
			}
		}
	}

	void RemovePanelFromDockGridLayout(DockGridLayout? layout, string title)
	{
		if (layout == null) return;
		for (int i = layout.Items.Count - 1; i >= 0; i--)
		{
			var item = layout.Items[i];
			int index = item.Panels.IndexOf(title);
			if (index >= 0)
			{
				item.Panels.RemoveAt(index);
				if (item.ActiveIndex >= item.Panels.Count)
					item.ActiveIndex = Math.Max(0, item.Panels.Count - 1);
				if (item.Panels.Count == 0)
				{
					layout.Items.RemoveAt(i);
					if (layout.ItemSizes.Count > i)
						layout.ItemSizes.RemoveAt(i);
				}
				else if (item.Panels.Count == 1)
				{
					item.ActiveIndex = 0;
				}
			}
		}
		if (layout.ItemSizes.Count > 0)
		{
			double total = 0;
			for (int i = 0; i < layout.ItemSizes.Count; i++)
				total += layout.ItemSizes[i];
			if (total > 0)
			{
				for (int i = 0; i < layout.ItemSizes.Count; i++)
					layout.ItemSizes[i] = layout.ItemSizes[i] / total;
			}
			else
			{
				var equal = 1.0 / layout.ItemSizes.Count;
				for (int i = 0; i < layout.ItemSizes.Count; i++)
					layout.ItemSizes[i] = equal;
			}
		}
		NormalizeItemSizes(layout);
	}

	void ApplyFloatingPanels(LayoutConfig config)
	{
		if (floatingLayer == null) return;
		for (int i = 0; i < config.Panels.Count; i++)
		{
			var state = config.Panels[i];
			if (state.IsHidden || !state.IsFloating) continue;
			var panel = FindPanelByTitle(state.Title);
			if (panel == null) continue;
			panel.SetFloatingBounds(floatingLayer, state.FloatingLeft, state.FloatingTop, state.FloatingWidth, state.FloatingHeight);
		}
	}

	PanelTabGroup? FindPanelByTitle(string title)
	{
		var panels = GetAllPanels();
		for (int i = 0; i < panels.Count; i++)
		{
			if (panels[i].Title == title)
				return panels[i];
		}
		return null;
	}

	List<PanelTabGroup> GetAllPanels()
	{
		var list = new List<PanelTabGroup>();
		if (layersPanel != null) list.Add(layersPanel);
		if (propertiesPanel != null) list.Add(propertiesPanel);
		if (colorPanel != null) list.Add(colorPanel);
		if (brushesPanel != null) list.Add(brushesPanel);
		if (historyPanel != null) list.Add(historyPanel);
		if (timelinePanel != null) list.Add(timelinePanel);
		return list;
	}

	void HookDockGridEvents()
	{
		if (leftDockGrid != null)
		{
			leftDockGrid.DockedItemsChanged -= OnDockedItemsChanged;
			leftDockGrid.DockedItemsChanged += OnDockedItemsChanged;
		}
		if (rightDockGrid != null)
		{
			rightDockGrid.DockedItemsChanged -= OnDockedItemsChanged;
			rightDockGrid.DockedItemsChanged += OnDockedItemsChanged;
		}
		if (bottomDockGrid != null)
		{
			bottomDockGrid.DockedItemsChanged -= OnDockedItemsChanged;
			bottomDockGrid.DockedItemsChanged += OnDockedItemsChanged;
		}
	}

	void OnDockedItemsChanged(object? sender, EventArgs e)
	{
		UpdateDockHostSizes();
	}

	void InitDockSizes()
	{
		if (mainGrid == null) return;
		if (mainGrid.ColumnDefinitions.Count >= 6)
		{
			var leftCol = mainGrid.ColumnDefinitions[1];
			var leftSplitCol = mainGrid.ColumnDefinitions[2];
			var rightCol = mainGrid.ColumnDefinitions[5];
			var rightSplitCol = mainGrid.ColumnDefinitions[4];
			leftDockWidth = leftCol.Width;
			rightDockWidth = rightCol.Width;
			leftSplitterWidth = leftSplitCol.Width;
			rightSplitterWidth = rightSplitCol.Width;
		}
		if (mainGrid.RowDefinitions.Count >= 4)
		{
			var splitRow = mainGrid.RowDefinitions[2];
			var bottomRow = mainGrid.RowDefinitions[3];
			bottomSplitterHeight = splitRow.Height;
			bottomDockHeight = bottomRow.Height;
		}
	}

	void UpdateDockHostSizes()
	{
		if (mainGrid == null) return;
		UpdateDockSize(leftDockGrid, mainGrid.ColumnDefinitions, 1, 2, leftDockSplitter, leftDockMinWidth, leftDockMaxWidth, ref leftDockWidth, leftSplitterWidth, true);
		UpdateDockSize(rightDockGrid, mainGrid.ColumnDefinitions, 5, 4, rightDockSplitter, rightDockMinWidth, rightDockMaxWidth, ref rightDockWidth, rightSplitterWidth, true);
		UpdateDockSize(bottomDockGrid, mainGrid.RowDefinitions, 3, 2, bottomDockSplitter, bottomDockMinHeight, bottomDockMaxHeight, ref bottomDockHeight, bottomSplitterHeight, false);
	}

	void UpdateDockSize<T>(DockGrid? host, AvaloniaList<T> definitions, int dockIndex, int splitterIndex, GridSplitter? splitter, double minSize, double maxSize, ref GridLength cachedSize, GridLength splitterSize, bool isColumn) where T : DefinitionBase
	{
		if (host == null || definitions.Count <= Math.Max(dockIndex, splitterIndex)) return;

		var dockDef = definitions[dockIndex];
		var splitDef = definitions[splitterIndex];
		var hasPanels = host.HasPanels;

		if (isColumn)
		{
			var col = (ColumnDefinition)(object)dockDef;
			var splitCol = (ColumnDefinition)(object)splitDef;
			if (hasPanels)
			{
				if (col.Width.Value > 0)
					cachedSize = col.Width;
				col.MinWidth = minSize;
				col.MaxWidth = maxSize;
				if (col.Width.Value == 0)
					col.Width = cachedSize;
				splitCol.Width = splitterSize;
				host.PreviewDockWidth = cachedSize.Value;
				host.PreviewDockHeight = host.Bounds.Height;
				if (splitter != null)
				{
					splitter.IsVisible = true;
					splitter.IsEnabled = true;
				}
			}
			else
			{
				if (col.Width.Value > 0)
					cachedSize = col.Width;
				col.MinWidth = 0;
				col.MaxWidth = double.MaxValue;
				col.Width = new GridLength(0);
				splitCol.Width = new GridLength(0);
				host.PreviewDockWidth = cachedSize.Value;
				host.PreviewDockHeight = host.Bounds.Height;
				if (splitter != null)
				{
					splitter.IsVisible = false;
					splitter.IsEnabled = false;
				}
			}
		}
		else
		{
			var row = (RowDefinition)(object)dockDef;
			var splitRow = (RowDefinition)(object)splitDef;
			if (hasPanels)
			{
				if (row.Height.Value > 0)
					cachedSize = row.Height;
				row.MinHeight = minSize;
				row.MaxHeight = maxSize;
				if (row.Height.Value == 0)
					row.Height = cachedSize;
				splitRow.Height = splitterSize;
				host.PreviewDockWidth = host.Bounds.Width;
				host.PreviewDockHeight = cachedSize.Value;
				if (splitter != null)
				{
					splitter.IsVisible = true;
					splitter.IsEnabled = true;
				}
			}
			else
			{
				if (row.Height.Value > 0)
					cachedSize = row.Height;
				row.MinHeight = 0;
				row.MaxHeight = double.MaxValue;
				row.Height = new GridLength(0);
				splitRow.Height = new GridLength(0);
				host.PreviewDockWidth = host.Bounds.Width;
				host.PreviewDockHeight = cachedSize.Value;
				if (splitter != null)
				{
					splitter.IsVisible = false;
					splitter.IsEnabled = false;
				}
			}
		}
	}

	public bool ToggleLayersPanel()
	{
		return TogglePanel(layersPanel);
	}

	public bool TogglePropertiesPanel()
	{
		return TogglePanel(propertiesPanel);
	}

	public bool ToggleColorPanel()
	{
		return TogglePanel(colorPanel);
	}

	public bool ToggleBrushesPanel()
	{
		return TogglePanel(brushesPanel);
	}

	public bool ToggleHistoryPanel()
	{
		return TogglePanel(historyPanel);
	}

	public bool ToggleTimelinePanel()
	{
		return TogglePanel(timelinePanel);
	}

	public bool ToggleWorkspaceLock()
	{
		isWorkspaceLocked = !isWorkspaceLocked;
		UpdatePanelFloatability();
		return isWorkspaceLocked;
	}

	void UpdatePanelFloatability()
	{
		var canFloat = !isWorkspaceLocked;
		if (layersPanel != null) layersPanel.CanFloat = canFloat;
		if (propertiesPanel != null) propertiesPanel.CanFloat = canFloat;
		if (colorPanel != null) colorPanel.CanFloat = canFloat;
		if (brushesPanel != null) brushesPanel.CanFloat = canFloat;
		if (historyPanel != null) historyPanel.CanFloat = canFloat;
		if (timelinePanel != null) timelinePanel.CanFloat = canFloat;
	}

	public IReadOnlyList<string> GetWorkspaceProfileNames()
	{
		if (workspaceProfiles == null) return Array.Empty<string>();
		var list = new List<string>();
		foreach (var pair in workspaceProfiles.Profiles)
		{
			if (IsDefaultProfile(pair.Key)) continue;
			list.Add(pair.Key);
		}
		list.Sort(StringComparer.OrdinalIgnoreCase);
		return list;
	}

	public bool SaveWorkspaceProfile(string name)
	{
		if (workspaceProfiles == null) return false;
		var trimmed = name?.Trim();
		if (string.IsNullOrWhiteSpace(trimmed)) return false;
		if (IsDefaultProfile(trimmed)) return false;
		var config = BuildLayoutConfig();
		workspaceProfiles.Profiles[trimmed] = config;
		workspaceProfiles.ActiveProfile = trimmed;
		activeProfileName = trimmed;
		layoutConfig = config;
		WriteWorkspaceProfiles(workspaceProfiles);
		return true;
	}

	public bool LoadWorkspaceProfile(string name)
	{
		if (workspaceProfiles == null) return false;
		if (string.IsNullOrWhiteSpace(name)) return false;
		if (!workspaceProfiles.Profiles.ContainsKey(name) && !IsDefaultProfile(name)) return false;
		activeProfileName = name;
		workspaceProfiles.ActiveProfile = name;
		var config = GetProfileConfig(name);
		if (config == null) return false;
		ApplyLayoutConfig(config);
		WriteWorkspaceProfiles(workspaceProfiles);
		return true;
	}

	public bool LoadDefaultWorkspace()
	{
		return LoadWorkspaceProfile(defaultProfileName);
	}

	public bool IsLayersPanelVisible => IsPanelVisible(layersPanel);
	public bool IsPropertiesPanelVisible => IsPanelVisible(propertiesPanel);
	public bool IsColorPanelVisible => IsPanelVisible(colorPanel);
	public bool IsBrushesPanelVisible => IsPanelVisible(brushesPanel);
	public bool IsHistoryPanelVisible => IsPanelVisible(historyPanel);
	public bool IsTimelinePanelVisible => IsPanelVisible(timelinePanel);

	public bool IsWorkspaceLocked => isWorkspaceLocked;

	bool TogglePanel(PanelTabGroup? panel)
	{
		if (panel == null) return false;
		if (IsPanelVisible(panel))
		{
			HidePanel(panel);
			return false;
		}
		ShowPanel(panel);
		return true;
	}

	PanelState? GetPanelState(string title)
	{
		if (panelStateCache.TryGetValue(title, out var state))
			return state;
		return null;
	}

	DockGrid? GetDockHostByEdge(string edge)
	{
		if (edge == DockEdge.Left.ToString()) return leftDockGrid;
		if (edge == DockEdge.Right.ToString()) return rightDockGrid;
		if (edge == DockEdge.Bottom.ToString()) return bottomDockGrid;
		return null;
	}

	void ApplyPanelStateToDockHost(PanelTabGroup panel, PanelState state, DockGrid host)
	{
		var layout = host.GetLayout();
		NormalizeItemSizes(layout);
		int dockIndex = Math.Clamp(state.DockIndex, 0, layout.Items.Count);
		if (state.IsTabbed)
		{
			DockHostItemLayout? targetItem = null;
			if (dockIndex < layout.Items.Count)
			{
				var candidate = layout.Items[dockIndex];
				if (DockItemMatchesState(candidate, state))
					targetItem = candidate;
			}
			if (targetItem == null)
			{
				targetItem = new DockHostItemLayout
				{
					Panels = new List<string>(),
					ActiveIndex = 0
				};
				layout.Items.Insert(dockIndex, targetItem);
				layout.ItemSizes.Insert(dockIndex, state.DockedProportion > 0 ? state.DockedProportion : 1.0);
			}
			int tabIndex = Math.Clamp(state.TabIndex, 0, targetItem.Panels.Count);
			if (!targetItem.Panels.Contains(panel.Title))
				targetItem.Panels.Insert(tabIndex, panel.Title);
			if (state.WasActive)
				targetItem.ActiveIndex = Math.Clamp(tabIndex, 0, Math.Max(0, targetItem.Panels.Count - 1));
			else if (targetItem.ActiveIndex >= targetItem.Panels.Count)
				targetItem.ActiveIndex = Math.Max(0, targetItem.Panels.Count - 1);
		}
		else
		{
			layout.Items.Insert(dockIndex, new DockHostItemLayout
			{
				Panels = new List<string> { panel.Title },
				ActiveIndex = 0
			});
			layout.ItemSizes.Insert(dockIndex, state.DockedProportion > 0 ? state.DockedProportion : 1.0);
		}
		host.ApplyLayout(layout, FindPanelByTitle);
	}

	bool DockItemMatchesState(DockHostItemLayout item, PanelState state)
	{
		for (int i = 0; i < item.Panels.Count; i++)
		{
			var panelState = GetPanelState(item.Panels[i]);
			if (panelState == null) continue;
			if (panelState.DockEdge == state.DockEdge && panelState.DockIndex == state.DockIndex)
				return true;
		}
		return false;
	}

	bool IsPanelVisible(PanelTabGroup? panel)
	{
		if (panel == null) return false;
		var state = GetPanelState(panel.Title);
		if (state?.IsHidden ?? false) return false;
		return panel.Parent != null || panel.TabGroup != null;
	}

	public void HidePanel(PanelTabGroup? panel)
	{
		if (panel == null) return;
		SyncLayoutConfig(false, false);
		var currentState = GetPanelState(panel.Title);
		if (currentState != null)
			currentState.IsHidden = true;
		preserveLayoutOnSave = true;

		if (panel.IsFloating)
		{
			if (panel.Parent is Canvas canvas)
				canvas.Children.Remove(panel);
			SyncLayoutConfig(true, true);
			return;
		}

		if (panel.DockGrid != null)
		{
			var host = panel.DockGrid;
			var layout = host.GetLayout();
			RemovePanelFromDockGridLayout(layout, panel.Title);
			host.ApplyLayout(layout, FindPanelByTitle);
		}
		else if (panel.Parent is DockGrid parentHost)
		{
			var layout = parentHost.GetLayout();
			RemovePanelFromDockGridLayout(layout, panel.Title);
			parentHost.ApplyLayout(layout, FindPanelByTitle);
		}
		else
		{
			var parent = panel.Parent;
			if (parent is Panel panelParent)
			{
				panelParent.Children.Remove(panel);
			}
			else if (parent is ContentControl contentControl)
			{
				contentControl.Content = null;
			}
		}

		SyncLayoutConfig(true, true);
	}

	void OnPanelCloseRequested(object? sender, EventArgs e)
	{
		if (sender is PanelTabGroup panel)
		{
			// If this panel is part of a TabGroup, just mark it as hidden
			// The UI update is handled by the TabGroup management
			if (panel.TabGroup != null)
			{
				var state = GetPanelState(panel.Title);
				if (state != null)
				{
					state.IsHidden = true;
					preserveLayoutOnSave = true;
				}
				SyncLayoutConfig(true, true);
				return;
			}
			
			// Otherwise, use the normal hide logic
			HidePanel(panel);
		}
	}

	void ShowPanel(PanelTabGroup? panel)
	{
		if (panel == null) return;
		SyncLayoutConfig(false, false);
		var state = GetPanelState(panel.Title);
		if (state == null) return;
		state.IsHidden = false;
		preserveLayoutOnSave = true;

		if (state.IsFloating && floatingLayer != null)
		{
			panel.SetFloatingBounds(floatingLayer, state.FloatingLeft, state.FloatingTop, state.FloatingWidth, state.FloatingHeight);
			SyncLayoutConfig(true, true);
			return;
		}

		var host = GetDockHostByEdge(state.DockEdge);
		if (host != null)
		{
			ApplyPanelStateToDockHost(panel, state, host);
			SyncLayoutConfig(true, true);
			return;
		}

		if (leftDockGrid != null)
			leftDockGrid.AddPanel(panel);
		SyncLayoutConfig(true, true);
	}

	public static void RemoveFromParent(Control control)
	{
		if (control.Parent is Panel panel)
		{
			panel.Children.Remove(control);
			return;
		}
		if (control.Parent is ContentControl contentControl)
		{
			contentControl.Content = null;
			return;
		}
	}
}
