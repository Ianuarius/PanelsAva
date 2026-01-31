using Avalonia.Controls;
using PanelsAva.ViewModels;

namespace PanelsAva.Views;

public partial class TimelinePanel : UserControl
{
	public TimelinePanel()
	{
		InitializeComponent();
		DataContext = new TimelineViewModel();
	}
}