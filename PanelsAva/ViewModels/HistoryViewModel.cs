namespace PanelsAva.ViewModels;

public class HistoryViewModel : ViewModelBase
{
	string currentFileName = "File 1";
	public string CurrentFileName
	{
		get => currentFileName;
		set => SetProperty(ref currentFileName, value);
	}
}