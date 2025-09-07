using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.UI;

public partial class ClientPanel : Panel
{
	public ClientPanel()
	{
		InitializeComponent();
		ListBoxTemp.ItemsSource = new string[]
		{
			"user 1",
			"user 2",
			"user 3"
		}.OrderBy(x => x);
	}
}