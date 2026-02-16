using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RUNAapp.Helpers;
using RUNAapp.Models;
using RUNAapp.Services;

namespace RUNAapp.ViewModels;

/// <summary>
/// View model for the main dashboard.
/// </summary>
public partial class DashboardViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IVoiceAssistantService _voiceService;
    private readonly ITextToSpeechService _ttsService;
    private readonly INavigationService _navigationService;
    private readonly IComputerVisionService _visionService;
    private readonly IOpenAIService _openAIService;
    
    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string _welcomeMessage = "Welcome!";

    [ObservableProperty]
    private string _currentLocation = "Getting location...";
    
    [ObservableProperty]
    private bool _isVoiceActive;
    
    [ObservableProperty]
    private string _voiceStatus = "Tap to speak";
    
    [ObservableProperty]
    private string _transcript = string.Empty;
    
    [ObservableProperty]
    private bool _isDetectionActive;
    
    [ObservableProperty]
    private bool _isDeveloperMode = Constants.EnableDeveloperMode;
    
    [ObservableProperty]
    private string _developerCommandText = string.Empty;
    
    public DashboardViewModel(
        IAuthService authService,
        IVoiceAssistantService voiceService,
        ITextToSpeechService ttsService,
        INavigationService navigationService,
        IComputerVisionService visionService,
        IOpenAIService openAIService)
    {
        _authService = authService;
        _voiceService = voiceService;
        _ttsService = ttsService;
        _navigationService = navigationService;
        _visionService = visionService;
        _openAIService = openAIService;
        
        Title = "RUNA";
        
        // Subscribe to voice assistant events
        _voiceService.StateChanged += OnVoiceStateChanged;
        _voiceService.TranscriptAvailable += OnTranscriptAvailable;
        _voiceService.IntentClassified += OnIntentClassified;
        _voiceService.ErrorOccurred += OnVoiceErrorOccurred;
        
        // Subscribe to vision events
        _visionService.DangerAlert += OnDangerAlert;
    }
    
    [RelayCommand]
    private async Task InitializeAsync()
    {
        UserEmail = _authService.CurrentUser?.Email ?? "Guest";

        // Set welcome message with username
        var username = UserEmail.Contains('@') ? UserEmail.Split('@')[0] : UserEmail;
        WelcomeMessage = $"Welcome, {username}!";

        // Greet user
        await _ttsService.SpeakAsync($"Welcome to RUNA, {username}. Tap the microphone button to give voice commands, or use the navigation and vision buttons below.");
        
        // Get current location
        await UpdateLocationAsync();
        
        // Load CV model in background
        _ = _visionService.LoadModelAsync();

        // Warm up voice backend (fire-and-forget) to reduce cold-start delays
        _ = _openAIService.WarmupAsync();
    }
    
    [RelayCommand]
    private async Task ToggleVoiceAssistantAsync()
    {
        if (_voiceService.IsListening)
        {
            // Stop and process
            await _voiceService.StopListeningAndProcessAsync();
        }
        else if (_voiceService.IsProcessing)
        {
            // Cancel
            await _voiceService.CancelAsync();
        }
        else
        {
            // Start listening
            await _voiceService.StartListeningAsync();
        }
    }
    
    [RelayCommand]
    private async Task GoToNavigationAsync()
    {
        await _ttsService.SpeakAsync("Opening navigation.");
        await Shell.Current.GoToAsync("//Navigation");
    }
    
    [RelayCommand]
    private async Task GoToVisionAsync()
    {
        await _ttsService.SpeakAsync("Opening obstacle detection.");
        await Shell.Current.GoToAsync("//Vision");
    }
    
    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _ttsService.SpeakAsync("Signing out.");
        await _authService.SignOutAsync();
        await Shell.Current.GoToAsync("//Welcome");
    }
    
    [RelayCommand]
    private async Task GoToSettingsAsync()
    {
        await _ttsService.SpeakAsync("Opening settings.");
        await Shell.Current.GoToAsync("//Settings");
    }
    
    [RelayCommand]
    private async Task GetHelpAsync()
    {
        var helpText = "RUNA helps you navigate safely. " +
                       "Use the microphone button to give voice commands like 'Take me to the park' or 'What's around me'. " +
                       "The Navigation button opens the map. " +
                       "The Vision button starts obstacle detection.";
        
        await _ttsService.SpeakAsync(helpText);
    }
    
    [RelayCommand]
    private async Task SubmitDeveloperCommandAsync()
    {
        var text = DeveloperCommandText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            await _ttsService.SpeakAsync("Enter a command first.");
            return;
        }
        
        DeveloperCommandText = string.Empty;
        await _voiceService.ProcessTextCommandAsync(text);
    }
    
    private async Task UpdateLocationAsync()
    {
        try
        {
            var location = await _navigationService.GetCurrentLocationAsync();
            if (location != null)
            {
                var address = await _navigationService.GetAddressAsync(location);
                CurrentLocation = address;
            }
        }
        catch (Exception ex)
        {
            CurrentLocation = "Location unavailable";
            System.Diagnostics.Debug.WriteLine($"Location error: {ex.Message}");
        }
    }
    
    private void OnVoiceStateChanged(object? sender, VoiceAssistantStateEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsVoiceActive = e.State == VoiceAssistantState.Listening || 
                           e.State == VoiceAssistantState.Processing;
            
            VoiceStatus = e.State switch
            {
                VoiceAssistantState.Listening => "Listening...",
                VoiceAssistantState.Processing => "Processing...",
                VoiceAssistantState.Speaking => "Speaking...",
                VoiceAssistantState.Error => e.Message ?? "Error",
                _ => "Tap to speak"
            };
        });
    }
    
    private void OnTranscriptAvailable(object? sender, string transcript)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Transcript = transcript;
        });
    }
    
    private async void OnIntentClassified(object? sender, IntentResult result)
    {
        await MainThread.InvokeOnMainThreadAsync(() => ExecuteIntentAsync(result));
    }
    
    private async Task ExecuteIntentAsync(IntentResult result)
    {
        switch (result.Action)
        {
            case IntentAction.NavigateTo:
            case IntentAction.GetDirections:
                if (result.Parameters.TryGetValue("destination", out var destination) &&
                    !string.IsNullOrWhiteSpace(destination))
                {
                    await Shell.Current.GoToAsync($"//Navigation?destination={Uri.EscapeDataString(destination)}");
                }
                else
                {
                    await _ttsService.SpeakAsync("Please say your destination.");
                }
                break;
            
            case IntentAction.StartNavigation:
                await Shell.Current.GoToAsync("//Navigation?action=start");
                break;
            
            case IntentAction.StopNavigation:
                if (_navigationService.IsNavigating)
                {
                    _navigationService.StopNavigation();
                    await _ttsService.SpeakAsync("Navigation stopped.");
                }
                else
                {
                    await Shell.Current.GoToAsync("//Navigation?action=stop");
                }
                break;
            
            case IntentAction.StartDetection:
            case IntentAction.DescribeSurroundings:
                await Shell.Current.GoToAsync("//Vision");
                break;
            
            case IntentAction.WhereAmI:
                await _ttsService.SpeakAsync($"You are currently at {CurrentLocation}");
                break;
            
            case IntentAction.RepeatLastMessage:
                if (!string.IsNullOrWhiteSpace(_voiceService.LastResponse))
                {
                    await _ttsService.SpeakAsync(_voiceService.LastResponse);
                }
                break;
            
            case IntentAction.GetHelp:
            case IntentAction.Unknown:
                await GetHelpAsync();
                break;
        }
    }
    
    private async void OnDangerAlert(object? sender, DetectedObject obj)
    {
        var positionText = obj.Position switch
        {
            RelativePosition.Left => "on your left",
            RelativePosition.Right => "on your right",
            _ => "ahead"
        };
        
        await _ttsService.SpeakAlertAsync($"Warning! {obj.Label} {positionText}!");
    }
    
    private async void OnVoiceErrorOccurred(object? sender, string errorMessage)
    {
        // Speak errors to user (critical for blind users)
        await _ttsService.SpeakAlertAsync($"Error: {errorMessage}");
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SetError(errorMessage);
            VoiceStatus = $"Error: {errorMessage}";
        });
    }
}
