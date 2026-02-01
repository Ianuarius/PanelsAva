using Avalonia.Media.Imaging;
using System.Collections.ObjectModel;

namespace PanelsAva;

public class Document
{
	public string Name { get; set; } = "Untitled";
	public Bitmap? Bitmap { get; set; }
	public ObservableCollection<object> Layers { get; set; } = new();
}
