using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
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
		if (layersPanel?.Content is LayersPanel layersPanelView && layersPanelView.DataContext is ViewModels.LayersViewModel layersVm)
			layersVm.CurrentFileName = name;
		if (propertiesPanel?.Content is PropertiesPanel propertiesPanelView && propertiesPanelView.DataContext is ViewModels.PropertiesViewModel propertiesVm)
			propertiesVm.CurrentFileName = name;
		if (colorPanel?.Content is ColorPanel colorPanelView && colorPanelView.DataContext is ViewModels.ColorViewModel colorVm)
			colorVm.CurrentFileName = name;
		if (brushesPanel?.Content is BrushesPanel brushesPanelView && brushesPanelView.DataContext is ViewModels.BrushesViewModel brushesVm)
			brushesVm.CurrentFileName = name;
		if (historyPanel?.Content is HistoryPanel historyPanelView && historyPanelView.DataContext is ViewModels.HistoryViewModel historyVm)
			historyVm.CurrentFileName = name;
		if (timelinePanel?.Content is TimelinePanel timelinePanelView && timelinePanelView.DataContext is ViewModels.TimelineViewModel timelineVm)
			timelineVm.CurrentFileName = name;
	}

	void HookLayoutEvents()
	{
		HookDockHostLayoutEvents(leftDockHost);
		HookDockHostLayoutEvents(rightDockHost);
		HookDockHostLayoutEvents(bottomDockHost);
		HookPanelLayoutEvents(layersPanel);
		HookPanelLayoutEvents(propertiesPanel);
		HookPanelLayoutEvents(colorPanel);
		HookPanelLayoutEvents(brushesPanel);
		HookPanelLayoutEvents(historyPanel);
		HookPanelLayoutEvents(timelinePanel);
		HookSplitterEvents(leftDockSplitter);
		HookSplitterEvents(rightDockSplitter);
		HookSplitterEvents(bottomDockSplitter);
	}

	void HookDockHostLayoutEvents(DockHost? host)
	{
		if (host == null) return;
		host.LayoutChanged -= OnLayoutChanged;
		host.LayoutChanged += OnLayoutChanged;
	}

	void HookPanelLayoutEvents(DockablePanel? panel)
	{
		if (panel == null) return;
		panel.LayoutChanged -= OnLayoutChanged;
		panel.LayoutChanged += OnLayoutChanged;
	}

	void HookSplitterEvents(GridSplitter? splitter)
	{
		if (splitter == null) return;
		splitter.PointerReleased -= OnSplitterPointerReleased;
		splitter.PointerReleased += OnSplitterPointerReleased;
		splitter.PointerCaptureLost -= OnSplitterPointerCaptureLost;
		splitter.PointerCaptureLost += OnSplitterPointerCaptureLost;
	}

	void OnSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		ScheduleLayoutSave();
	}

	void OnSplitterPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		ScheduleLayoutSave();
	}

	void OnLayoutChanged(object? sender, EventArgs e)
	{
		ScheduleLayoutSave();
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
		config.LeftDockHost = leftDockHost?.GetLayout();
		config.RightDockHost = rightDockHost?.GetLayout();
		config.BottomDockHost = bottomDockHost?.GetLayout();
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
		ApplyDockHostStates(config.LeftDockHost, states, existingStates);
		ApplyDockHostStates(config.RightDockHost, states, existingStates);
		ApplyDockHostStates(config.BottomDockHost, states, existingStates);

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
		return config;
	}

	void ApplyDockHostStates(DockHostLayout? layout, Dictionary<string, PanelState> states, Dictionary<string, PanelState> existingStates)
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

	PanelState GetOrCreateState(string title, Dictionary<string, PanelState> states, Dictionary<string, PanelState> existingStates, DockablePanel? panel)
	{
		if (states.TryGetValue(title, out var state))
			return state;
		if (existingStates.TryGetValue(title, out var existing))
		{
			state = new PanelState
			{
				Title = existing.Title,
				IsHidden = existing.IsHidden,
				IsFloating = existing.IsFloating,
				IsTabbed = existing.IsTabbed,
				DockEdge = existing.DockEdge,
				DockIndex = existing.DockIndex,
				TabIndex = existing.TabIndex,
				WasActive = existing.WasActive,
				FloatingLeft = existing.FloatingLeft,
				FloatingTop = existing.FloatingTop,
				FloatingWidth = existing.FloatingWidth,
				FloatingHeight = existing.FloatingHeight,
				DockedProportion = existing.DockedProportion
			};
			states[title] = state;
			return state;
		}
		state = new PanelState
		{
			Title = title
		};
		if (panel != null && panel.DockHost != null)
			state.DockEdge = panel.DockHost.DockEdge.ToString();
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
			ApplyDockHostLayout(leftDockHost, config.LeftDockHost);
			ApplyDockHostLayout(rightDockHost, config.RightDockHost);
			ApplyDockHostLayout(bottomDockHost, config.BottomDockHost);
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
			
		leftDockHost?.ClearPanels();
		rightDockHost?.ClearPanels();
		bottomDockHost?.ClearPanels();
	}

	void RemoveFromParent(Control control)
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

	void ApplyDockHostLayout(DockHost? host, DockHostLayout? layout)
	{
		if (host == null) return;
		NormalizeItemSizes(layout);
		host.ApplyLayout(layout, FindPanelByTitle);
	}

	void NormalizeItemSizes(DockHostLayout? layout)
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

	void RemovePanelFromDockHostLayout(DockHostLayout? layout, string title)
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

	DockablePanel? FindPanelByTitle(string title)
	{
		var panels = GetAllPanels();
		for (int i = 0; i < panels.Count; i++)
		{
			if (panels[i].Title == title)
				return panels[i];
		}
		return null;
	}

	List<DockablePanel> GetAllPanels()
	{
		var list = new List<DockablePanel>();
		if (layersPanel != null) list.Add(layersPanel);
		if (propertiesPanel != null) list.Add(propertiesPanel);
		if (colorPanel != null) list.Add(colorPanel);
		if (brushesPanel != null) list.Add(brushesPanel);
		if (historyPanel != null) list.Add(historyPanel);
		if (timelinePanel != null) list.Add(timelinePanel);
		return list;
	}

	void HookDockHostEvents()
	{
		if (leftDockHost != null)
		{
			leftDockHost.DockedItemsChanged -= OnDockedItemsChanged;
			leftDockHost.DockedItemsChanged += OnDockedItemsChanged;
		}
		if (rightDockHost != null)
		{
			rightDockHost.DockedItemsChanged -= OnDockedItemsChanged;
			rightDockHost.DockedItemsChanged += OnDockedItemsChanged;
		}
		if (bottomDockHost != null)
		{
			bottomDockHost.DockedItemsChanged -= OnDockedItemsChanged;
			bottomDockHost.DockedItemsChanged += OnDockedItemsChanged;
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
		UpdateLeftDockSize();
		UpdateRightDockSize();
		UpdateBottomDockSize();
	}

	void UpdateLeftDockSize()
	{
		if (leftDockHost == null || mainGrid == null) return;
		if (mainGrid.ColumnDefinitions.Count < 3) return;
		var leftCol = mainGrid.ColumnDefinitions[1];
		var splitCol = mainGrid.ColumnDefinitions[2];
		var hasPanels = leftDockHost.HasPanels;
		if (hasPanels)
		{
			if (leftCol.Width.Value > 0)
				leftDockWidth = leftCol.Width;
			leftCol.MinWidth = leftDockMinWidth;
			leftCol.MaxWidth = leftDockMaxWidth;
			if (leftCol.Width.Value == 0)
				leftCol.Width = leftDockWidth;
			splitCol.Width = leftSplitterWidth;
			leftDockHost.PreviewDockWidth = leftDockWidth.Value;
			leftDockHost.PreviewDockHeight = leftDockHost.Bounds.Height;
			if (leftDockSplitter != null)
			{
				leftDockSplitter.IsVisible = true;
				leftDockSplitter.IsEnabled = true;
			}
		}
		else
		{
			if (leftCol.Width.Value > 0)
				leftDockWidth = leftCol.Width;
			leftCol.MinWidth = 0;
			leftCol.MaxWidth = double.MaxValue;
			leftCol.Width = new GridLength(0);
			splitCol.Width = new GridLength(0);
			leftDockHost.PreviewDockWidth = leftDockWidth.Value;
			leftDockHost.PreviewDockHeight = leftDockHost.Bounds.Height;
			if (leftDockSplitter != null)
			{
				leftDockSplitter.IsVisible = false;
				leftDockSplitter.IsEnabled = false;
			}
		}
	}

	void UpdateRightDockSize()
	{
		if (rightDockHost == null || mainGrid == null) return;
		if (mainGrid.ColumnDefinitions.Count < 6) return;
		var rightCol = mainGrid.ColumnDefinitions[5];
		var splitCol = mainGrid.ColumnDefinitions[4];
		var hasPanels = rightDockHost.HasPanels;
		if (hasPanels)
		{
			if (rightCol.Width.Value > 0)
				rightDockWidth = rightCol.Width;
			rightCol.MinWidth = rightDockMinWidth;
			rightCol.MaxWidth = rightDockMaxWidth;
			if (rightCol.Width.Value == 0)
				rightCol.Width = rightDockWidth;
			splitCol.Width = rightSplitterWidth;
			rightDockHost.PreviewDockWidth = rightDockWidth.Value;
			rightDockHost.PreviewDockHeight = rightDockHost.Bounds.Height;
			if (rightDockSplitter != null)
			{
				rightDockSplitter.IsVisible = true;
				rightDockSplitter.IsEnabled = true;
			}
		}
		else
		{
			if (rightCol.Width.Value > 0)
				rightDockWidth = rightCol.Width;
			rightCol.MinWidth = 0;
			rightCol.MaxWidth = double.MaxValue;
			rightCol.Width = new GridLength(0);
			splitCol.Width = new GridLength(0);
			rightDockHost.PreviewDockWidth = rightDockWidth.Value;
			rightDockHost.PreviewDockHeight = rightDockHost.Bounds.Height;
			if (rightDockSplitter != null)
			{
				rightDockSplitter.IsVisible = false;
				rightDockSplitter.IsEnabled = false;
			}
		}
	}

	void UpdateBottomDockSize()
	{
		if (bottomDockHost == null || mainGrid == null) return;
		if (mainGrid.RowDefinitions.Count < 4) return;
		var splitRow = mainGrid.RowDefinitions[2];
		var bottomRow = mainGrid.RowDefinitions[3];
		var hasPanels = bottomDockHost.HasPanels;
		if (hasPanels)
		{
			if (bottomRow.Height.Value > 0)
				bottomDockHeight = bottomRow.Height;
			bottomRow.MinHeight = bottomDockMinHeight;
			bottomRow.MaxHeight = bottomDockMaxHeight;
			if (bottomRow.Height.Value == 0)
				bottomRow.Height = bottomDockHeight;
			splitRow.Height = bottomSplitterHeight;
			bottomDockHost.PreviewDockWidth = bottomDockHost.Bounds.Width;
			bottomDockHost.PreviewDockHeight = bottomDockHeight.Value;
			if (bottomDockSplitter != null)
			{
				bottomDockSplitter.IsVisible = true;
				bottomDockSplitter.IsEnabled = true;
			}
		}
		else
		{
			if (bottomRow.Height.Value > 0)
				bottomDockHeight = bottomRow.Height;
			bottomRow.MinHeight = 0;
			bottomRow.MaxHeight = double.MaxValue;
			bottomRow.Height = new GridLength(0);
			splitRow.Height = new GridLength(0);
			bottomDockHost.PreviewDockWidth = bottomDockHost.Bounds.Width;
			bottomDockHost.PreviewDockHeight = bottomDockHeight.Value;
			if (bottomDockSplitter != null)
			{
				bottomDockSplitter.IsVisible = false;
				bottomDockSplitter.IsEnabled = false;
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

	bool TogglePanel(DockablePanel? panel)
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
		if (layoutConfig == null) return null;
		for (int i = 0; i < layoutConfig.Panels.Count; i++)
		{
			if (layoutConfig.Panels[i].Title == title)
				return layoutConfig.Panels[i];
		}
		return null;
	}

	DockHost? GetDockHostByEdge(string edge)
	{
		if (edge == DockEdge.Left.ToString()) return leftDockHost;
		if (edge == DockEdge.Right.ToString()) return rightDockHost;
		if (edge == DockEdge.Bottom.ToString()) return bottomDockHost;
		return null;
	}

	void ApplyPanelStateToDockHost(DockablePanel panel, PanelState state, DockHost host)
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

	bool IsPanelVisible(DockablePanel? panel)
	{
		if (panel == null) return false;
		var state = GetPanelState(panel.Title);
		if (state?.IsHidden ?? false) return false;
		return panel.Parent != null || panel.TabGroup != null;
	}

	void HidePanel(DockablePanel? panel)
	{
		if (panel == null) return;

		var prevConfig = layoutConfig ?? BuildLayoutConfig();
		var prevState = GetPanelStateFromConfig(prevConfig, panel.Title);
		if (prevState != null)
		{
			prevState.IsHidden = true;
			layoutConfig = prevConfig;
			preserveLayoutOnSave = true;
		}

		if (panel.IsFloating)
		{
			if (panel.Parent is Canvas canvas)
			{
				canvas.Children.Remove(panel);
			}
			ScheduleLayoutSave();
			PanelVisibilityChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		if (panel.DockHost != null)
		{
			var host = panel.DockHost;
			var layout = host.GetLayout();
			RemovePanelFromDockHostLayout(layout, panel.Title);
			host.ApplyLayout(layout, FindPanelByTitle);
		}
		else if (panel.Parent is DockHost parentHost)
		{
			var layout = parentHost.GetLayout();
			RemovePanelFromDockHostLayout(layout, panel.Title);
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

		ScheduleLayoutSave();
		PanelVisibilityChanged?.Invoke(this, EventArgs.Empty);
	}

	void OnPanelCloseRequested(object? sender, EventArgs e)
	{
		if (sender is DockablePanel panel)
		{
			HidePanel(panel);
		}
	}

	void ShowPanel(DockablePanel? panel)
	{
		if (panel == null) return;
		var state = GetPanelState(panel.Title);
		if (state == null) return;

		state.IsHidden = false;
		preserveLayoutOnSave = true;

		if (state.IsFloating && floatingLayer != null)
		{
			panel.SetFloatingBounds(floatingLayer, state.FloatingLeft, state.FloatingTop, state.FloatingWidth, state.FloatingHeight);
			ScheduleLayoutSave();
			PanelVisibilityChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		var host = GetDockHostByEdge(state.DockEdge);
		if (host != null)
		{
			ApplyPanelStateToDockHost(panel, state, host);
			ScheduleLayoutSave();
			PanelVisibilityChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		if (leftDockHost != null)
			leftDockHost.AddPanel(panel);
		ScheduleLayoutSave();
		PanelVisibilityChanged?.Invoke(this, EventArgs.Empty);
	}
}
