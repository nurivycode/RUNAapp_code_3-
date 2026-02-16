using RUNAapp.Helpers;
using Microsoft.Maui.ApplicationModel;

namespace RUNAapp.Services;

/// <summary>
/// Text-to-speech service implementation using MAUI Essentials.
/// </summary>
public class TextToSpeechService : ITextToSpeechService
{
    private CancellationTokenSource? _speakCts;
    private float _speechRate = Constants.TtsSpeechRate;
    private float _pitch = Constants.TtsPitch;
    private Locale? _preferredEnglishLocale;
    private bool _localeResolved;
    
    public bool IsSpeaking { get; private set; }
    
    public async Task SpeakAsync(string text, bool interrupt = true)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        
        try
        {
            if (interrupt)
            {
                await StopSpeakingAsync();
            }
            
            _speakCts = new CancellationTokenSource();
            IsSpeaking = true;
            
            var options = new SpeechOptions
            {
                Pitch = _pitch,
                Volume = Constants.TtsVolume
            };

            options.Locale = await GetPreferredEnglishLocaleAsync();

            // Note: SpeechRate is not directly available in SpeechOptions
            // It uses the device default. We'd need platform-specific code for custom rate.
            
            await TextToSpeech.Default.SpeakAsync(text, options, _speakCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Speech was cancelled, that's okay
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TTS error: {ex.Message}");
        }
        finally
        {
            IsSpeaking = false;
        }
    }
    
    public async Task SpeakAlertAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        
        try
        {
            // Stop any current speech immediately
            await StopSpeakingAsync();
            
            // Trigger haptic feedback for alerts
            try
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            }
            catch
            {
                // Haptic feedback might not be available
            }
            
            _speakCts = new CancellationTokenSource();
            IsSpeaking = true;
            
            var options = new SpeechOptions
            {
                Pitch = 1.2f, // Slightly higher pitch for urgency
                Volume = 1.0f // Maximum volume for alerts
            };

            options.Locale = await GetPreferredEnglishLocaleAsync();

            await TextToSpeech.Default.SpeakAsync(text, options, _speakCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Speech was cancelled
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TTS alert error: {ex.Message}");
        }
        finally
        {
            IsSpeaking = false;
        }
    }
    
    public async Task StopSpeakingAsync()
    {
        if (_speakCts != null && !_speakCts.IsCancellationRequested)
        {
            _speakCts.Cancel();
            _speakCts.Dispose();
            _speakCts = null;
        }
        
        IsSpeaking = false;
        await Task.CompletedTask;
    }
    
    public void SetSpeechRate(float rate)
    {
        _speechRate = Math.Clamp(rate, 0.1f, 2.0f);
    }
    
    public void SetPitch(float pitch)
    {
        _pitch = Math.Clamp(pitch, 0.1f, 2.0f);
    }

    private async Task<Locale?> GetPreferredEnglishLocaleAsync()
    {
        if (_localeResolved)
            return _preferredEnglishLocale;

        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            _preferredEnglishLocale =
                locales.FirstOrDefault(locale => string.Equals(locale.Language, "en-US", StringComparison.OrdinalIgnoreCase)) ??
                locales.FirstOrDefault(locale => locale.Language.StartsWith("en-", StringComparison.OrdinalIgnoreCase)) ??
                locales.FirstOrDefault(locale => string.Equals(locale.Language, "en", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TTS locale resolve error: {ex.Message}");
        }
        finally
        {
            _localeResolved = true;
        }

        return _preferredEnglishLocale;
    }
}
