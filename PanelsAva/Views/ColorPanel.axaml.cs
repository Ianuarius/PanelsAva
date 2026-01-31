using Avalonia.Controls;
using PanelsAva.ViewModels;

namespace PanelsAva.Views;

public partial class ColorPanel : UserControl
{
	public ColorPanel()
	{
		InitializeComponent();
		DataContext = new ColorViewModel();
	}
}