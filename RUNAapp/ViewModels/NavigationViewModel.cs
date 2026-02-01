using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RUNAapp.Models;
using RUNAapp.Services;

namespace RUNAapp.ViewModels;

/// <summary>
/// View model for the navigation page.
/// </summary>
[QueryProperty(nameof(Destination), "destination")]
public partial class NavigationViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly ITextToSpeechService _ttsService;
    
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
    
    [ObservableProperty]
    private GeoCoordinate? _currentLocation;
    
    [ObservableProperty]
    private List<GeocodingResult> _searchResults = new();
    
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
    
    partial void OnDestinationChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            SearchQuery = value;
            _ = SearchAndNavigateAsync();
        }
    }
    
    [RelayCommand]
    private async Task InitializeAsync()
    {
        await _ttsService.SpeakAsync("Navigation mode. Enter a destination or say where you want to go.");
        
        // Get current location
        await UpdateCurrentLocationAsync();
    }
    
    [RelayCommand]
    private async Task SearchLocationAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await _ttsService.SpeakAsync("Please enter a destination.");
            return;
        }
        
        await ExecuteAsync(async () =>
        {
            await _ttsService.SpeakAsync($"Searching for {SearchQuery}");
            
            SearchResults = await _navigationService.SearchLocationAsync(SearchQuery);
            
            if (SearchResults.Count == 0)
            {
                await _ttsService.SpeakAsync("No results found. Please try a different search.");
            }
            else if (SearchResults.Count == 1)
            {
                await _ttsService.SpeakAsync($"Found {SearchResults[0].DisplayName}. Calculating route.");
                await CalculateRouteToAsync(SearchResults[0]);
            }
            else
            {
                await _ttsService.SpeakAsync($"Found {SearchResults.Count} results. Please select one.");
            }
        });
    }
    
    [RelayCommand]
    private async Task SelectDestinationAsync(GeocodingResult destination)
    {
        await CalculateRouteToAsync(destination);
    }
    
    [RelayCommand]
    private async Task StartNavigationAsync()
    {
        if (CurrentRoute == null)
        {
            await _ttsService.SpeakAsync("Please select a destination first.");
            return;
        }
        
        _navigationService.StartNavigation(CurrentRoute);
        IsNavigating = true;
        
        var firstStep = CurrentRoute.Steps.FirstOrDefault();
        if (firstStep != null)
        {
            await _ttsService.SpeakAsync($"Starting navigation to {CurrentRoute.DestinationName}. {firstStep.VoiceInstruction}");
        }
    }
    
    [RelayCommand]
    private async Task StopNavigationAsync()
    {
        _navigationService.StopNavigation();
        IsNavigating = false;
        CurrentInstruction = string.Empty;
        
        await _ttsService.SpeakAsync("Navigation stopped.");
    }
    
    [RelayCommand]
    private async Task GetCurrentStepAsync()
    {
        if (!IsNavigating || CurrentRoute == null)
        {
            await _ttsService.SpeakAsync("Navigation is not active.");
            return;
        }
        
        var step = CurrentRoute.Steps.ElementAtOrDefault(_navigationService.CurrentStepIndex);
        if (step != null)
        {
            await _ttsService.SpeakAsync(step.VoiceInstruction);
        }
    }
    
    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (IsNavigating)
        {
            _navigationService.StopNavigation();
        }
        
        await Shell.Current.GoToAsync("..");
    }
    
    private async Task SearchAndNavigateAsync()
    {
        await SearchLocationAsync();
        
        if (SearchResults.Count > 0)
        {
            await CalculateRouteToAsync(SearchResults[0]);
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
                await _ttsService.SpeakAsync("Unable to get your current location.");
                return;
            }
            
            CurrentRoute = await _navigationService.CalculateRouteAsync(
                CurrentLocation, 
                destination.Location);
            
            CurrentRoute.DestinationName = destination.DisplayName;
            
            UpdateRouteDisplay();
            
            await _ttsService.SpeakAsync(
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
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (e.HasArrived)
            {
                IsNavigating = false;
                CurrentInstruction = "You have arrived!";
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
            if (e.DistanceToNextStep < 50)
            {
                await _ttsService.SpeakAsync(e.Instruction);
            }
        }
        
        if (e.HasArrived)
        {
            await _ttsService.SpeakAsync("You have arrived at your destination.");
        }
    }
}
