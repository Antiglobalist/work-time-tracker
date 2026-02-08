using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkTimeTracking.Services;

namespace WorkTimeTracking.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableObject _currentView = null!;

    [ObservableProperty]
    private int _selectedMenuIndex;

    public MainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.CurrentViewChanged += () =>
        {
            CurrentView = _navigationService.CurrentView;
        };

        _navigationService.NavigateTo<TimerViewModel>();
        SelectedMenuIndex = 0;
    }

    [RelayCommand]
    private void NavigateToTimer()
    {
        _navigationService.NavigateTo<TimerViewModel>();
        SelectedMenuIndex = 0;
    }

    [RelayCommand]
    private void NavigateToHistory()
    {
        _navigationService.NavigateTo<HistoryViewModel>();
        SelectedMenuIndex = 1;
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        _navigationService.NavigateTo<SettingsViewModel>();
        SelectedMenuIndex = 2;
    }
}
