using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace WorkTimeTracking.Services;

public class NavigationService : INavigationService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private ObservableObject _currentView = null!;

    public ObservableObject CurrentView
    {
        get => _currentView;
        private set
        {
            _currentView = value;
            CurrentViewChanged?.Invoke();
        }
    }

    public event Action? CurrentViewChanged;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject
    {
        if (_currentView is IDisposable disposable)
            disposable.Dispose();

        // Do not resolve disposable transient view models from the root provider:
        // it tracks them until the application exits, even after navigation away.
        var viewModel = ActivatorUtilities.CreateInstance<TViewModel>(_serviceProvider);
        CurrentView = viewModel;
    }

    public void Dispose()
    {
        if (_currentView is IDisposable disposable)
            disposable.Dispose();

        _currentView = null!;
        CurrentViewChanged = null;
    }
}
