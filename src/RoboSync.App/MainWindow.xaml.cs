using System.Collections.Specialized;
using System.Windows;

namespace RoboSync.App;

/// <summary>
/// Interaction logic for MainWindow.xaml. The only code-behind concern is auto-scrolling the
/// log list as new entries arrive; all application logic lives in the view model.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (LogList.ItemsSource is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged += OnLogCollectionChanged;
        }
    }

    private void OnLogCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[^1]);
        }
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }
}
