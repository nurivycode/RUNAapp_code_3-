using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RUNAapp.Helpers;
using RUNAapp.Services;

namespace RUNAapp.ViewModels;

public partial class SetupViewModel : BaseViewModel
{
    private readonly ITextToSpeechService _ttsService;
    
    [ObservableProperty]
    private string _firebaseApiKey = string.Empty;
    
    [ObservableProperty]
    private string _firebaseAuthDomain = string.Empty;
    
    [ObservableProperty]
    private string _firebaseProjectId = string.Empty;
    
    [ObservableProperty]
    private string _firebaseStorageBucket = string.Empty;
    
    [ObservableProperty]
    private string _firebaseMessagingSenderId = string.Empty;
    
    [ObservableProperty]
    private string _firebaseAppId = string.Empty;
    
    [ObservableProperty]
    private string _firebaseMeasurementId = string.Empty;
    
    [ObservableProperty]
    private bool _isConfigured;
    
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    
    public SetupViewModel(ITextToSpeechService ttsService)
    {
        _ttsService = ttsService;
        Title = "API Setup";
    }
    
    [RelayCommand]
    private async Task InitializeAsync()
    {
        // Check if already configured
        IsConfigured = await SecureStorageHelper.IsFirebaseConfiguredAsync();
        
        if (IsConfigured)
        {
            StatusMessage = "Firebase configuration is saved.";
            
            // Load existing values (masked)
            var firebaseConfig = await SecureStorageHelper.GetFirebaseConfigAsync();
            if (firebaseConfig != null)
            {
                FirebaseApiKey = "••••••••" + firebaseConfig.ApiKey[^4..];
                FirebaseAuthDomain = firebaseConfig.AuthDomain;
                FirebaseProjectId = firebaseConfig.ProjectId;
            }
            
        }
        else
        {
            await _ttsService.SpeakAsync("Please enter your Firebase settings to configure the application.");
        }
    }
    
    [RelayCommand]
    private async Task SaveConfigurationAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(FirebaseApiKey) || FirebaseApiKey.StartsWith("••"))
            {
                SetError("Please enter your Firebase API key.");
                return;
            }
            
            // Save Firebase config
            await SecureStorageHelper.SetFirebaseConfigAsync(
                FirebaseApiKey,
                FirebaseAuthDomain,
                FirebaseProjectId,
                FirebaseStorageBucket,
                FirebaseMessagingSenderId,
                FirebaseAppId,
                string.IsNullOrEmpty(FirebaseMeasurementId) ? null : FirebaseMeasurementId
            );
            
            IsConfigured = true;
            StatusMessage = "Firebase configuration saved.";
            
            await _ttsService.SpeakAsync("Configuration saved. You can now use the application.");
            
            // Navigate to welcome
            await Shell.Current.GoToAsync("//Welcome");
        });
    }
    
    [RelayCommand]
    private async Task ClearConfigurationAsync()
    {
        SecureStorageHelper.ClearAll();
        
        FirebaseApiKey = string.Empty;
        FirebaseAuthDomain = string.Empty;
        FirebaseProjectId = string.Empty;
        FirebaseStorageBucket = string.Empty;
        FirebaseMessagingSenderId = string.Empty;
        FirebaseAppId = string.Empty;
        FirebaseMeasurementId = string.Empty;
        
        IsConfigured = false;
        StatusMessage = "Configuration cleared.";
        
        await _ttsService.SpeakAsync("All settings have been cleared.");
    }
    
    [RelayCommand]
    private async Task SkipSetupAsync()
    {
        await _ttsService.SpeakAsync("Skipping setup. Some features may not work without Firebase settings.");
        await Shell.Current.GoToAsync("//Welcome");
    }
}
