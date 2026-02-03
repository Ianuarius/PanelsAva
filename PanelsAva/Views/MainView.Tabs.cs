using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;

namespace PanelsAva.Views;

public partial class MainView
{
	void RefreshFileTabStrip()
	{
		if (fileTabStrip == null) return;
		if (currentViewModel == null) return;
		var vm = currentViewModel;

		var removeList = new List<Document>();
		foreach (var pair in fileTabs)
		{
			if (!vm.OpenDocuments.Contains(pair.Key))
				removeList.Add(pair.Key);
		}
		foreach (var doc in removeList)
			RemoveTabForDocument(doc);

		fileTabStrip.Children.Clear();

		for (int i = 0; i < vm.OpenDocuments.Count; i++)
		{
			var doc = vm.OpenDocuments[i];
			var isActive = doc == vm.SelectedDocument;

			if (!fileTabs.TryGetValue(doc, out var tab))
			{
				tab = new FileTabItem(this, doc);
				fileTabs[doc] = tab;
			}

			tab.SetActive(isActive);
			if (floatingDocuments.Contains(doc))
			{
				EnsureFloatingPanel(doc);
				continue;
			}
			tab.SetFloating(false);
			fileTabStrip.Children.Add(tab);
		}
		UpdateFloatingPanelActiveStates();
		ClearDockPreview();
	}

	void EnsureFloatingPanel(Document doc)
	{
		if (floatingLayer == null) return;
		var panel = GetOrCreateFloatingPanel(doc);
		if (!floatingLayer.Children.Contains(panel))
			floatingLayer.Children.Add(panel);
		panel.UpdateFromDocument();
		panel.SetActive(currentViewModel?.SelectedDocument == doc);
	}

	void UpdateFloatingPanelActiveStates()
	{
		if (currentViewModel == null) return;
		foreach (var pair in floatingPanels)
			pair.Value.SetActive(pair.Key == currentViewModel.SelectedDocument);
	}

	void RemoveTabForDocument(Document doc)
	{
		if (!fileTabs.TryGetValue(doc, out var tab)) return;
		if (fileTabStrip != null && fileTabStrip.Children.Contains(tab))
			fileTabStrip.Children.Remove(tab);
		fileTabs.Remove(doc);
		floatingDocuments.Remove(doc);
		if (floatingPanels.TryGetValue(doc, out var panel))
		{
			if (floatingLayer != null && floatingLayer.Children.Contains(panel))
				floatingLayer.Children.Remove(panel);
			floatingPanels.Remove(doc);
		}
	}

	public void SelectDocument(Document doc, bool updateCanvas)
	{
		if (currentViewModel == null) return;
		currentViewModel.SelectedDocument = doc;
		if (updateCanvas)
		{
			var index = currentViewModel.OpenDocuments.IndexOf(doc);
			if (index >= 0)
				currentViewModel.CurrentDocumentIndex = index;
		}
	}

	public void SelectDocument(Document doc)
	{
		SelectDocument(doc, true);
	}

	public void CloseDocument(Document doc)
	{
		if (currentViewModel == null) return;
		var index = currentViewModel.OpenDocuments.IndexOf(doc);
		if (index < 0) return;
		RemoveTabForDocument(doc);
		currentViewModel.OpenDocuments.RemoveAt(index);
		if (currentViewModel.OpenDocuments.Count == 0)
		{
			currentViewModel.CurrentDocumentIndex = -1;
			return;
		}
		if (currentViewModel.CurrentDocumentIndex > index)
			currentViewModel.CurrentDocumentIndex -= 1;
		else if (currentViewModel.CurrentDocumentIndex == index)
			currentViewModel.CurrentDocumentIndex = Math.Min(index, currentViewModel.OpenDocuments.Count - 1);
	}

	public void BeginFloatingTab(FileTabItem tab, Point posRoot, IPointer? pointer, double dragOffsetX, double dragOffsetY)
	{
		if (floatingLayer == null || fileTabStrip == null) return;
		if (!floatingDocuments.Contains(tab.Document))
			floatingDocuments.Add(tab.Document);
		if (fileTabStrip.Children.Contains(tab))
			fileTabStrip.Children.Remove(tab);

		var panel = GetOrCreateFloatingPanel(tab.Document);
		if (!floatingLayer.Children.Contains(panel))
			floatingLayer.Children.Add(panel);
		panel.UpdateFromDocument();
		tab.SetFloating(true);

		if (pointer != null)
		{
			if (floatingLayer != null)
				pointer.Capture(floatingLayer);
			else
				pointer.Capture(null);
		}

		if (currentViewModel != null && currentViewModel.CurrentDocument == tab.Document)
		{
			currentViewModel.CurrentDocumentIndex = GetFirstDockedDocumentIndex();
		}

		if (currentViewModel != null)
			currentViewModel.SelectedDocument = tab.Document;
	}

	public void MoveFloatingTab(FileTabItem tab, Point posRoot, double dragOffsetX, double dragOffsetY)
	{
		if (!floatingPanels.TryGetValue(tab.Document, out var panel)) return;
		MoveFloatingPanel(panel, posRoot, dragOffsetX, dragOffsetY);
	}

	public void TryDockFloatingTab(FileTabItem tab, Point posRoot)
	{
		if (!floatingPanels.TryGetValue(tab.Document, out var panel)) return;
		TryDockFloatingPanel(panel, posRoot);
	}

	public void MoveFloatingPanel(FileTabFloatingPanel panel, Point posRoot, double dragOffsetX, double dragOffsetY)
	{
		if (floatingLayer == null) return;
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return;
		var floatingLayerPos = floatingLayer.TranslatePoint(new Point(0, 0), visualRoot);
		if (!floatingLayerPos.HasValue) return;

		var posInFloatingLayer = posRoot - floatingLayerPos.Value;
		var panelPos = new Point(posInFloatingLayer.X - dragOffsetX, posInFloatingLayer.Y - dragOffsetY);
		Canvas.SetLeft(panel, panelPos.X);
		Canvas.SetTop(panel, panelPos.Y);
	}

	public void TryDockFloatingPanel(FileTabFloatingPanel panel, Point posRoot)
	{
		if (fileTabStrip == null || floatingLayer == null) return;
		if (!IsPointOverFileTabStrip(posRoot)) return;

		floatingDocuments.Remove(panel.Document);
		if (floatingLayer.Children.Contains(panel))
			floatingLayer.Children.Remove(panel);
		if (!fileTabs.TryGetValue(panel.Document, out var tab))
			return;
		if (!fileTabStrip.Children.Contains(tab))
		{
			var index = GetInsertIndex(posRoot, true);
			if (index < 0 || index > fileTabStrip.Children.Count)
				index = fileTabStrip.Children.Count;
			fileTabStrip.Children.Insert(index, tab);
			ReorderDocumentByDockedIndex(panel.Document, index);
		}
		tab.SetFloating(false);
		ClearDockPreview();
		if (currentViewModel != null)
			currentViewModel.CurrentDocumentIndex = currentViewModel.OpenDocuments.IndexOf(panel.Document);
	}

	public void ReorderDockedTab(FileTabItem tab, Point posRoot)
	{
		if (fileTabStrip == null) return;
		var index = GetInsertIndex(posRoot, true);
		ReorderDocumentByDockedIndex(tab.Document, index);
		ClearDockPreview();
	}

	public void UpdateDockPreview(Point posRoot)
	{
		if (fileTabStrip == null) return;
		if (!IsPointOverFileTabStrip(posRoot))
		{
			ClearDockPreview();
			return;
		}

		var index = GetInsertIndex(posRoot, true);
		if (index < 0) index = 0;
		if (index > fileTabStrip.Children.Count) index = fileTabStrip.Children.Count;

		if (fileTabPreview == null)
		{
			fileTabPreview = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(64, 128, 255)),
				Width = 3,
				Margin = new Thickness(0)
			};
		}

		fileTabPreview.Height = Math.Max(6, fileTabStrip.Bounds.Height);
		if (!fileTabStrip.Children.Contains(fileTabPreview))
		{
			fileTabStrip.Children.Insert(index, fileTabPreview);
		}
		else
		{
			var currentIndex = fileTabStrip.Children.IndexOf(fileTabPreview);
			if (currentIndex != index)
			{
				fileTabStrip.Children.Remove(fileTabPreview);
				if (index > currentIndex) index--;
				fileTabStrip.Children.Insert(index, fileTabPreview);
			}
		}
	}

	public void ClearDockPreview()
	{
		if (fileTabStrip == null || fileTabPreview == null) return;
		if (fileTabStrip.Children.Contains(fileTabPreview))
			fileTabStrip.Children.Remove(fileTabPreview);
	}

	void ReorderDocumentByDockedIndex(Document doc, int dockedIndex)
	{
		if (currentViewModel == null) return;
		var oldIndex = currentViewModel.OpenDocuments.IndexOf(doc);
		if (oldIndex < 0) return;
		var targetIndex = GetOpenIndexForDockedInsert(doc, dockedIndex);
		if (targetIndex > oldIndex) targetIndex--;
		if (targetIndex < 0) targetIndex = 0;
		if (targetIndex >= currentViewModel.OpenDocuments.Count)
			targetIndex = currentViewModel.OpenDocuments.Count - 1;
		if (targetIndex == oldIndex) return;
		currentViewModel.OpenDocuments.Move(oldIndex, targetIndex);
		currentViewModel.CurrentDocumentIndex = targetIndex;
	}

	int GetOpenIndexForDockedInsert(Document doc, int dockedIndex)
	{
		if (currentViewModel == null) return dockedIndex;
		int dockedCount = 0;
		for (int i = 0; i < currentViewModel.OpenDocuments.Count; i++)
		{
			var d = currentViewModel.OpenDocuments[i];
			if (d == doc) continue;
			if (floatingDocuments.Contains(d)) continue;
			if (dockedCount == dockedIndex) return i;
			dockedCount++;
		}
		return currentViewModel.OpenDocuments.Count;
	}

	int GetFirstDockedDocumentIndex()
	{
		if (currentViewModel == null) return -1;
		for (int i = 0; i < currentViewModel.OpenDocuments.Count; i++)
		{
			var doc = currentViewModel.OpenDocuments[i];
			if (!floatingDocuments.Contains(doc))
				return i;
		}
		return -1;
	}

	FileTabFloatingPanel GetOrCreateFloatingPanel(Document doc)
	{
		if (!floatingPanels.TryGetValue(doc, out var panel))
		{
			panel = new FileTabFloatingPanel(this, doc);
			floatingPanels[doc] = panel;
		}
		return panel;
	}

	bool IsPointOverFileTabStrip(Point posRoot)
	{
		if (fileTabStrip == null) return false;
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return false;
		var stripPos = fileTabStrip.TranslatePoint(new Point(0, 0), visualRoot);
		if (!stripPos.HasValue) return false;
		var rect = new Rect(stripPos.Value, fileTabStrip.Bounds.Size);
		return rect.Contains(posRoot);
	}

	int GetInsertIndex(Point posRoot, bool ignorePreview)
	{
		if (fileTabStrip == null) return 0;
		var visualRoot = this.GetVisualRoot() as Visual;
		if (visualRoot == null) return 0;
		var stripPos = fileTabStrip.TranslatePoint(new Point(0, 0), visualRoot);
		if (!stripPos.HasValue) return 0;
		var local = posRoot - stripPos.Value;

		for (int i = 0; i < fileTabStrip.Children.Count; i++)
		{
			if (ignorePreview && ReferenceEquals(fileTabStrip.Children[i], fileTabPreview))
				continue;
			if (fileTabStrip.Children[i] is Control c)
			{
				var childPos = c.TranslatePoint(new Point(0, 0), fileTabStrip);
				if (childPos.HasValue)
				{
					var mid = childPos.Value.X + c.Bounds.Width / 2;
					if (local.X < mid)
						return i;
				}
			}
		}
		return fileTabStrip.Children.Count;
	}
}
