namespace PanelsAva.ViewModels;

public class LayersViewModel : ViewModelBase
{
	string currentFileName = "File 1";
	public string CurrentFileName
	{
		get => currentFileName;
		set => SetProperty(ref currentFileName, value);
	}
}
