using Avalonia.Controls;
using PanelsAva.ViewModels;

namespace PanelsAva.Views;

public partial class LayersPanel : UserControl
{
	public LayersPanel()
	{
		InitializeComponent();
		DataContext = new LayersViewModel();
	}
}
