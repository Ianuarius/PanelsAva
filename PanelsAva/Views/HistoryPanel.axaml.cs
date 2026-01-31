using Avalonia.Controls;
using PanelsAva.ViewModels;

namespace PanelsAva.Views;

public partial class HistoryPanel : UserControl
{
	public HistoryPanel()
	{
		InitializeComponent();
		DataContext = new HistoryViewModel();
	}
}