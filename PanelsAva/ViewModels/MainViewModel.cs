using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using PanelsAva.Views;
using PanelsAva;
using Avalonia.Media.Imaging;
using System;
using System.IO;
using Avalonia.Platform;

namespace PanelsAva.ViewModels;

public class MainViewModel : ViewModelBase
{
	public ObservableCollection<Document> OpenDocuments { get; } = new();
	
	int currentDocumentIndex;
	public int CurrentDocumentIndex
	{
		get => currentDocumentIndex;
		set
		{
			if (SetProperty(ref currentDocumentIndex, value))
			{
				OnPropertyChanged(nameof(CurrentDocument));
			}
		}
	}

	public Document? CurrentDocument => CurrentDocumentIndex >= 0 && CurrentDocumentIndex < OpenDocuments.Count 
		? OpenDocuments[CurrentDocumentIndex] 
		: null;

	public MainViewModel()
	{
		var doc1 = new Document
		{
			Name = "File 1",
			Bitmap = LoadBitmap("Assets/file1.jpg")
		};
		var doc2 = new Document
		{
			Name = "File 2",
			Bitmap = LoadBitmap("Assets/file2.jpg")
		};
		
		OpenDocuments.Add(doc1);
		OpenDocuments.Add(doc2);
		CurrentDocumentIndex = 0;
	}

	Bitmap? LoadBitmap(string path)
	{
		try
		{
			var uri = new Uri($"avares://PanelsAva/{path}");
			if (AssetLoader.Exists(uri))
			{
				using var stream = AssetLoader.Open(uri);
				return new Bitmap(stream);
			}
			if (File.Exists(path))
				return new Bitmap(path);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Failed to load bitmap {path}: {ex.Message}");
		}
		return null;
	}
}
