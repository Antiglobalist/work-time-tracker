using System.Windows;
using WorkTimeTracking.ViewModels;

namespace WorkTimeTracking;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
