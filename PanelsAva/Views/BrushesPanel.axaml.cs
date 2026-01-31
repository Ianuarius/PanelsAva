using Avalonia.Controls;
using PanelsAva.ViewModels;

namespace PanelsAva.Views;

public partial class BrushesPanel : UserControl
{
	public BrushesPanel()
	{
		InitializeComponent();
		DataContext = new BrushesViewModel();
	}
}