using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RUNAapp.Services;

namespace RUNAapp.ViewModels;

/// <summary>
/// View model for the welcome/landing page.
/// </summary>
public partial class WelcomeViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly ITextToSpeechService _ttsService;
    
    public WelcomeViewModel(IAuthService authService, ITextToSpeechService ttsService)
    {
        _authService = authService;
        _ttsService = ttsService;
        Title = "Welcome to RUNA";
    }
    
    [RelayCommand]
    private async Task InitializeAsync()
    {
        // Try to restore previous session
        var restored = await _authService.TryRestoreSessionAsync();
        
        if (restored)
        {
            await _ttsService.SpeakAsync("Welcome back to RUNA.");
            await Shell.Current.GoToAsync("//Dashboard");
        }
        else
        {
            await _ttsService.SpeakAsync(
                "Welcome to RUNA, your accessible navigation assistant. " +
                "Please enter your access code to continue.");
        }
    }
    
    [RelayCommand]
    private async Task GoToAccessCodeAsync()
    {
        await Shell.Current.GoToAsync("//AccessCode");
    }
}
