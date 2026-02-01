namespace PanelsAva.ViewModels;

public class ColorViewModel : ViewModelBase
{
	string currentFileName = "File 1";
	public string CurrentFileName
	{
		get => currentFileName;
		set => SetProperty(ref currentFileName, value);
	}
}