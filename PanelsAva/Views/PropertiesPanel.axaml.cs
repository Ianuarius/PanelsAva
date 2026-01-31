using Avalonia.Controls;
using PanelsAva.ViewModels;

namespace PanelsAva.Views;

public partial class PropertiesPanel : UserControl
{
	public PropertiesPanel()
	{
		InitializeComponent();
		DataContext = new PropertiesViewModel();
	}
}
