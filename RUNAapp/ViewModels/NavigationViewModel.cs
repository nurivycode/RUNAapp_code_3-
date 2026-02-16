using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RUNAapp.Helpers;
using RUNAapp.Models;
using RUNAapp.Services;

namespace RUNAapp.ViewModels;

/// <summary>
/// View model for the navigation page.
/// </summary>
[QueryProperty(nameof(Destination), "destination")]
[QueryProperty(nameof(NavigationAction), "action")]
public partial class NavigationViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly ITextToSpeechService _ttsService;
    private string _lastSpokenInstruction = string.Empty;
    private DateTime _lastInstructionSpokenAt = DateTime.MinValue;
    private static readonly TimeSpan InstructionRepeatCooldown = TimeSpan.FromSeconds(10);
    private static readonly bool NavigationVoiceFeedbackEnabled = Constants.EnableNavigationVoiceFeedback;
    
    [ObservableProperty]
    private string _destination = string.Empty;
    
    [ObservableProperty]
    private string _searchQuery = string.Empty;
    
    [ObservableProperty]
    private NavigationRoute? _currentRoute;
    
    [ObservableProperty]
    private string _currentInstruction = string.Empty;
    
    [ObservableProperty]
    private string _distanceRemaining = string.Empty;
    
    [ObservableProperty]
    private string _timeRemaining = string.Empty;
    
    [ObservableProperty]
    private bool _isNavigating;

    public bool IsNotNavigating => !IsNavigating;
    
    [ObservableProperty]
    private GeoCoordinate? _currentLocation;
    
    [ObservableProperty]
    private List<GeocodingResult> _searchResults = new();
    
    [ObservableProperty]
    private bool _isDeveloperMode = Constants.EnableDeveloperMode;
    
    [ObservableProperty]
    private string _navigationAction = string.Empty;
    
    public NavigationViewModel(
        INavigationService navigationService,
        ITextToSpeechService ttsService)
    {
        _navigationService = navigationService;
        _ttsService = ttsService;
        
        Title = "Navigation";
        
        // Subscribe to navigation updates
        _navigationService.NavigationUpdated += OnNavigationUpdated;
    }

    partial void OnIsNavigatingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotNavigating));
    }
    
    partial void OnDestinationChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            SearchQuery = value;
            _ = SearchAndNavigateAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    System.Diagnostics.Debug.WriteLine($"SearchAndNavigate failed: {t.Exception?.InnerException?.Message}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
    
    partial void OnNavigationActionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        
        _ = HandleNavigationActionAsync(value).ContinueWith(t =>
        {
            if (t.IsFaulted)
                System.Diagnostics.Debug.WriteLine($"HandleNavigationAction failed: {t.Exception?.InnerException?.Message}");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
    
    [RelayCommand]
    private async Task InitializeAsync()
    {
        await SpeakNavigationAsync("Navigation mode. Enter a destination or say where you want to go.");
        
        // Get current location
        await UpdateCurrentLocationAsync();
    }
    
    [RelayCommand]
    private async Task SearchLocationAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await SpeakNavigationAsync("Please enter a destination.");
            return;
        }
        
        await ExecuteAsync(async () =>
        {
            await SpeakNavigationAsync($"Searching for {SearchQuery}");
            
            SearchResults = await _navigationService.SearchLocationAsync(SearchQuery, CurrentLocation);

            if (SearchResults.Count == 0)
            {
                await SpeakNavigationAsync("No results found. Please try a different search.");
            }
            else
            {
                await SpeakNavigationAsync($"Found {SearchResults.Count} results. Please select one.");
            }
        });
    }
    
    [RelayCommand]
    private async Task SelectDestinationAsync(GeocodingResult? destination)
    {
        if (destination == null)
            return;

        await CalculateRouteToAsync(destination);
    }
    
    [RelayCommand]
    private async Task StartNavigationAsync()
    {
        if (CurrentRoute == null)
        {
            await SpeakNavigationAsync("Please select a destination first.");
            return;
        }
        
        _navigationService.StartNavigation(CurrentRoute);
        IsNavigating = true;
        
        var firstStep = CurrentRoute.Steps.FirstOrDefault();
        if (firstStep != null)
        {
            _lastSpokenInstruction = firstStep.VoiceInstruction;
            _lastInstructionSpokenAt = DateTime.UtcNow;
            await SpeakNavigationAsync($"Starting navigation to {CurrentRoute.DestinationName}. {firstStep.VoiceInstruction}");
        }
    }
    
    [RelayCommand]
    private async Task StopNavigationAsync()
    {
        _navigationService.StopNavigation();
        IsNavigating = false;
        CurrentInstruction = string.Empty;
        _lastSpokenInstruction = string.Empty;
        _lastInstructionSpokenAt = DateTime.MinValue;
        
        await SpeakNavigationAsync("Navigation stopped.");
    }
    
    [RelayCommand]
    private async Task GetCurrentStepAsync()
    {
        if (!IsNavigating || CurrentRoute == null)
        {
            await SpeakNavigationAsync("Navigation is not active.");
            return;
        }
        
        var step = CurrentRoute.Steps.ElementAtOrDefault(_navigationService.CurrentStepIndex);
        if (step != null)
        {
            await SpeakNavigationAsync(step.VoiceInstruction);
        }
    }
    
    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (IsNavigating)
        {
            _navigationService.StopNavigation();
        }
        
        await Shell.Current.GoToAsync("//Dashboard");
    }
    
    private async Task SearchAndNavigateAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;
        
        await ExecuteAsync(async () =>
        {
            SearchResults = await _navigationService.SearchLocationAsync(SearchQuery, CurrentLocation);
            if (SearchResults.Count == 0)
            {
                await SpeakNavigationAsync("No matching destination found.");
                return;
            }
            
            if (SearchResults.Count == 1)
            {
                await SpeakNavigationAsync($"Found {SearchResults[0].DisplayName}. Calculating route.");
                await CalculateRouteToAsync(SearchResults[0]);
                return;
            }
            
            await SpeakNavigationAsync($"I found {SearchResults.Count} options. Please choose one.");
        });
    }
    
    private async Task HandleNavigationActionAsync(string action)
    {
        var normalized = action.Trim().ToLowerInvariant();
        NavigationAction = string.Empty;
        
        switch (normalized)
        {
            case "start":
                await StartNavigationAsync();
                break;
            case "stop":
                await StopNavigationAsync();
                break;
        }
    }
    
    private async Task CalculateRouteToAsync(GeocodingResult destination)
    {
        await ExecuteAsync(async () =>
        {
            if (CurrentLocation == null)
            {
                await UpdateCurrentLocationAsync();
            }
            
            if (CurrentLocation == null)
            {
                await SpeakNavigationAsync("Unable to get your current location.");
                return;
            }
            
            CurrentRoute = await _navigationService.CalculateRouteAsync(
                CurrentLocation, 
                destination.Location);
            
            CurrentRoute.DestinationName = destination.DisplayName;
            
            UpdateRouteDisplay();
            
            await SpeakNavigationAsync(
                $"Route calculated to {destination.DisplayName}. " +
                $"Distance: {CurrentRoute.FormattedDistance}. " +
                $"Estimated time: {CurrentRoute.FormattedDuration}. " +
                "Say 'Start navigation' to begin.");
        });
    }
    
    private async Task UpdateCurrentLocationAsync()
    {
        try
        {
            CurrentLocation = await _navigationService.GetCurrentLocationAsync();
        }
        catch (Exception ex)
        {
            SetError($"Location error: {ex.Message}");
        }
    }
    
    private void UpdateRouteDisplay()
    {
        if (CurrentRoute == null)
            return;
        
        DistanceRemaining = CurrentRoute.FormattedDistance;
        TimeRemaining = CurrentRoute.FormattedDuration;
        CurrentInstruction = CurrentRoute.Steps.FirstOrDefault()?.Instruction ?? string.Empty;
    }
    
    private async void OnNavigationUpdated(object? sender, Services.NavigationEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (e.Route != null)
                {
                    CurrentRoute = e.Route;
                }

                IsNavigating = _navigationService.IsNavigating;

                if (e.HasArrived)
                {
                    IsNavigating = false;
                    CurrentInstruction = "You have arrived!";
                    _lastSpokenInstruction = string.Empty;
                    _lastInstructionSpokenAt = DateTime.MinValue;
                }
                else if (e.Instruction != null)
                {
                    CurrentInstruction = e.Instruction;
                }

                CurrentLocation = e.CurrentLocation;

                // Update remaining distance/time
                if (CurrentRoute != null && e.CurrentLocation != null)
                {
                    var remainingDistance = e.CurrentLocation.DistanceTo(CurrentRoute.Destination);
                    DistanceRemaining = remainingDistance < 1000
                        ? $"{remainingDistance:F0} m"
                        : $"{remainingDistance / 1000:F1} km";
                }
            });

            // Speak instruction when step changes
            if (e.Instruction != null && !e.HasArrived)
            {
                // Only speak when approaching the next turn (within 50 meters)
                if (e.DistanceToNextStep < 50 && ShouldSpeakInstruction(e.Instruction))
                {
                    await SpeakNavigationAsync(e.Instruction);
                }
            }

            if (e.HasArrived)
            {
                await SpeakNavigationAsync("You have arrived at your destination.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation update handler error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    private bool ShouldSpeakInstruction(string instruction)
    {
        var now = DateTime.UtcNow;
        var isSameInstruction = string.Equals(
            instruction,
            _lastSpokenInstruction,
            StringComparison.OrdinalIgnoreCase);

        if (isSameInstruction && now - _lastInstructionSpokenAt < InstructionRepeatCooldown)
            return false;

        _lastSpokenInstruction = instruction;
        _lastInstructionSpokenAt = now;
        return true;
    }

    private async Task SpeakNavigationAsync(string message)
    {
        if (!NavigationVoiceFeedbackEnabled || string.IsNullOrWhiteSpace(message))
            return;

        await _ttsService.SpeakAsync(message);
    }
}
