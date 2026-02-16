using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RUNAapp.Helpers;
using RUNAapp.Models;
using RUNAapp.Services;

namespace RUNAapp.ViewModels;

/// <summary>
/// View model for the Settings page.
/// </summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IFirestoreService _firestoreService;
    private readonly ITextToSpeechService _ttsService;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string _userDisplayName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoiceFeedbackDisabled))]
    private bool _voiceFeedbackEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HapticFeedbackDisabled))]
    private bool _hapticFeedbackEnabled = true;

    [ObservableProperty]
    private string _sensitivityMode = "medium";

    public bool VoiceFeedbackDisabled => !VoiceFeedbackEnabled;
    public bool HapticFeedbackDisabled => !HapticFeedbackEnabled;

    private bool _settingsLoaded;
    private bool _hasUnsavedChanges;

    public SettingsViewModel(
        IAuthService authService,
        IFirestoreService firestoreService,
        ITextToSpeechService ttsService)
    {
        _authService = authService;
        _firestoreService = firestoreService;
        _ttsService = ttsService;

        Title = "Settings";

        // Track changes to settings
        PropertyChanged += (s, e) =>
        {
            if (_settingsLoaded && (
                e.PropertyName == nameof(VoiceFeedbackEnabled) ||
                e.PropertyName == nameof(HapticFeedbackEnabled) ||
                e.PropertyName == nameof(SensitivityMode)))
            {
                _hasUnsavedChanges = true;
            }
        };
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await ExecuteAsync(async () =>
        {
            _settingsLoaded = false;

            // Get user email from auth service
            var user = _authService.CurrentUser;
            UserEmail = user?.Email ?? "Not signed in";
            UserDisplayName = user?.Email?.Split('@')[0] ?? "User";

            // Announce page
            await _ttsService.SpeakAsync("Settings page. Loading your preferences.");

            // Try to get auth token for Firestore calls
            var (idToken, _, _) = await SecureStorageHelper.GetAuthTokensAsync();

            if (!string.IsNullOrEmpty(idToken))
            {
                // Fire both requests in parallel to halve cold-start wait
                var profileTask = _firestoreService.GetUserProfileAsync(idToken);
                var settingsTask = _firestoreService.GetUserSettingsAsync(idToken);

                // Await profile
                try
                {
                    var profile = await profileTask;
                    if (profile != null)
                    {
                        UserEmail = profile.Email;
                        UserDisplayName = profile.DisplayName;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading profile: {ex.Message}");
                    SetError($"Error getting user profile: {ex.Message}");
                }

                // Await settings
                try
                {
                    var settings = await settingsTask;
                    VoiceFeedbackEnabled = settings.VoiceFeedbackEnabled;
                    HapticFeedbackEnabled = settings.HapticFeedbackEnabled;
                    SensitivityMode = settings.SensitivityMode;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                    // Use defaults on error
                    VoiceFeedbackEnabled = true;
                    HapticFeedbackEnabled = true;
                    SensitivityMode = "medium";
                }
            }
            else
            {
                // Use local preferences as fallback
                VoiceFeedbackEnabled = Preferences.Default.Get(Constants.PrefVoiceFeedbackEnabled, true);
                HapticFeedbackEnabled = Preferences.Default.Get(Constants.PrefHapticFeedbackEnabled, true);
                SensitivityMode = Preferences.Default.Get(Constants.PrefDetectionSensitivity, "medium");
            }

            _settingsLoaded = true;
            _hasUnsavedChanges = false;

            await _ttsService.SpeakAsync("Settings loaded.");
        });
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!_hasUnsavedChanges)
            return;

        try
        {
            // Save to local preferences first (offline support)
            Preferences.Default.Set(Constants.PrefVoiceFeedbackEnabled, VoiceFeedbackEnabled);
            Preferences.Default.Set(Constants.PrefHapticFeedbackEnabled, HapticFeedbackEnabled);
            Preferences.Default.Set(Constants.PrefDetectionSensitivity, SensitivityMode);

            // Try to save to Firestore
            var (idToken, _, _) = await SecureStorageHelper.GetAuthTokensAsync();

            if (!string.IsNullOrEmpty(idToken))
            {
                var settings = new UserSettings
                {
                    VoiceFeedbackEnabled = VoiceFeedbackEnabled,
                    HapticFeedbackEnabled = HapticFeedbackEnabled,
                    SensitivityMode = SensitivityMode
                };

                var saved = await _firestoreService.SaveUserSettingsAsync(idToken, settings);

                if (!saved)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to save settings to Firestore");
                }
            }

            _hasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        // Save settings before leaving
        await SaveSettingsAsync();

        await _ttsService.SpeakAsync("Going back to dashboard.");
        await Shell.Current.GoToAsync("//Dashboard");
    }

    [RelayCommand]
    private void DismissError()
    {
        base.ClearError();
    }

    partial void OnVoiceFeedbackEnabledChanged(bool value)
    {
        if (_settingsLoaded && VoiceFeedbackEnabled)
        {
            // Provide audio feedback when voice is enabled
            _ = _ttsService.SpeakAsync(value ? "Voice feedback enabled." : "Voice feedback disabled.");
        }
    }

    partial void OnHapticFeedbackEnabledChanged(bool value)
    {
        if (_settingsLoaded)
        {
            // Provide haptic feedback when haptic is enabled
            if (value && HapticFeedback.Default.IsSupported)
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }

            if (VoiceFeedbackEnabled)
            {
                _ = _ttsService.SpeakAsync(value ? "Haptic feedback enabled." : "Haptic feedback disabled.");
            }
        }
    }

    partial void OnSensitivityModeChanged(string value)
    {
        if (_settingsLoaded && VoiceFeedbackEnabled)
        {
            _ = _ttsService.SpeakAsync($"Detection sensitivity set to {value}.");
        }
    }
}
