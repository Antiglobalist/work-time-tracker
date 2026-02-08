using CommunityToolkit.Mvvm.ComponentModel;

namespace WorkTimeTracking.Services;

public interface INavigationService
{
    ObservableObject CurrentView { get; }
    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;
    event Action? CurrentViewChanged;
}
