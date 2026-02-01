using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RUNAapp.Services;

namespace RUNAapp.ViewModels;

/// <summary>
/// View model for the registration page.
/// </summary>
public partial class RegisterViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly ITextToSpeechService _ttsService;
    
    [ObservableProperty]
    private string _email = string.Empty;
    
    [ObservableProperty]
    private string _password = string.Empty;
    
    [ObservableProperty]
    private string _confirmPassword = string.Empty;
    
    public RegisterViewModel(IAuthService authService, ITextToSpeechService ttsService)
    {
        _authService = authService;
        _ttsService = ttsService;
        Title = "Create Account";
    }
    
    [RelayCommand]
    private async Task SignUpAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            SetError("Please enter your email address.");
            await _ttsService.SpeakAsync("Please enter your email address.");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(Password))
        {
            SetError("Please enter a password.");
            await _ttsService.SpeakAsync("Please enter a password.");
            return;
        }
        
        if (Password.Length < 6)
        {
            SetError("Password must be at least 6 characters.");
            await _ttsService.SpeakAsync("Password must be at least 6 characters.");
            return;
        }
        
        if (Password != ConfirmPassword)
        {
            SetError("Passwords do not match.");
            await _ttsService.SpeakAsync("Passwords do not match. Please try again.");
            return;
        }
        
        await ExecuteAsync(async () =>
        {
            await _ttsService.SpeakAsync("Creating your account...");
            
            var user = await _authService.SignUpAsync(Email, Password);
            
            await _ttsService.SpeakAsync("Account created successfully! Welcome to RUNA.");
            
            // Navigate to dashboard
            await Shell.Current.GoToAsync("//Dashboard");
        });
        
        if (HasError)
        {
            await _ttsService.SpeakAsync(ErrorMessage ?? "Registration failed.");
        }
    }
    
    [RelayCommand]
    private async Task GoToLoginAsync()
    {
        await Shell.Current.GoToAsync("//Login");
    }
}
