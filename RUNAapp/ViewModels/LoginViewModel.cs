using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RUNAapp.Services;

namespace RUNAapp.ViewModels;

/// <summary>
/// View model for the login page.
/// </summary>
public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly ITextToSpeechService _ttsService;
    
    [ObservableProperty]
    private string _email = string.Empty;
    
    [ObservableProperty]
    private string _password = string.Empty;
    
    public LoginViewModel(IAuthService authService, ITextToSpeechService ttsService)
    {
        _authService = authService;
        _ttsService = ttsService;
        Title = "Sign In";
    }
    
    [RelayCommand]
    private async Task SignInAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            SetError("Please enter your email address.");
            await _ttsService.SpeakAsync("Please enter your email address.");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(Password))
        {
            SetError("Please enter your password.");
            await _ttsService.SpeakAsync("Please enter your password.");
            return;
        }
        
        await ExecuteAsync(async () =>
        {
            await _ttsService.SpeakAsync("Signing in...");
            
            var user = await _authService.SignInAsync(Email, Password);
            
            await _ttsService.SpeakAsync($"Welcome back! Signed in as {user.Email}");
            
            // Navigate to dashboard
            await Shell.Current.GoToAsync("//Dashboard");
        });
        
        if (HasError)
        {
            await _ttsService.SpeakAsync(ErrorMessage ?? "Sign in failed.");
        }
    }
    
    [RelayCommand]
    private async Task ForgotPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            SetError("Please enter your email address first.");
            await _ttsService.SpeakAsync("Please enter your email address to reset your password.");
            return;
        }
        
        await ExecuteAsync(async () =>
        {
            await _authService.SendPasswordResetEmailAsync(Email);
            await _ttsService.SpeakAsync("Password reset email sent. Please check your inbox.");
        });
    }
    
    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync("//Register");
    }
}
