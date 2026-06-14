using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace RoboSync.App;

/// <summary>Interaction logic for AboutWindow.xaml.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
