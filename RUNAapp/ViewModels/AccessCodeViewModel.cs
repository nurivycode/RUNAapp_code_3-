using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RUNAapp.Helpers;
using RUNAapp.Services;

namespace RUNAapp.ViewModels;

public partial class AccessCodeViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly ITextToSpeechService _ttsService;

    [ObservableProperty]
    private string _accessCode = string.Empty;

    public AccessCodeViewModel(IAuthService authService, ITextToSpeechService ttsService)
    {
        _authService = authService;
        _ttsService = ttsService;
        Title = "Access Code";
    }

    [RelayCommand]
    private async Task SubmitAccessCodeAsync()
    {
        var trimmed = (AccessCode ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            SetError("Please enter your access code.");
            await _ttsService.SpeakAsync("Please enter your access code.");
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _ttsService.SpeakAsync("Verifying access code.");

            var deviceId = await SecureStorageHelper.GetOrCreateDeviceIdAsync();
            await _authService.SignInWithAccessCodeAsync(trimmed, deviceId);

            await _ttsService.SpeakAsync("Access granted. Welcome to RUNA.");
            await Shell.Current.GoToAsync("//Dashboard");
        });

        if (HasError)
        {
            await _ttsService.SpeakAsync(ErrorMessage ?? "Access denied.");
        }
    }
}
