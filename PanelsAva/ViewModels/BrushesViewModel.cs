namespace PanelsAva.ViewModels;

public class BrushesViewModel : ViewModelBase
{
	string currentFileName = "File 1";
	public string CurrentFileName
	{
		get => currentFileName;
		set => SetProperty(ref currentFileName, value);
	}
}