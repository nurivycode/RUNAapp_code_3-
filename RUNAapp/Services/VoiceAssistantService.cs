using Plugin.Maui.Audio;
using RUNAapp.Helpers;
using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Voice assistant service implementation.
/// Orchestrates audio recording, transcription, and intent classification.
/// </summary>
public class VoiceAssistantService : IVoiceAssistantService
{
    private readonly IOpenAIService _openAIService;
    private readonly ITextToSpeechService _ttsService;
    private readonly IAudioManager _audioManager;
    
    private IAudioRecorder? _recorder;
    private CancellationTokenSource? _recordingCts;
    private DateTime _recordingStartTime;
    
    public bool IsListening { get; private set; }
    public bool IsProcessing { get; private set; }
    public string? LastResponse { get; private set; }
    
    public event EventHandler<VoiceAssistantStateEventArgs>? StateChanged;
    public event EventHandler<string>? TranscriptAvailable;
    public event EventHandler<IntentResult>? IntentClassified;
    public event EventHandler<string>? ErrorOccurred;
    
    public VoiceAssistantService(
        IOpenAIService openAIService,
        ITextToSpeechService ttsService,
        IAudioManager audioManager)
    {
        _openAIService = openAIService;
        _ttsService = ttsService;
        _audioManager = audioManager;
    }
    
    public async Task StartListeningAsync()
    {
        if (IsListening || IsProcessing)
            return;
        
        try
        {
            // Check microphone permission
            var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Microphone>();
                if (status != PermissionStatus.Granted)
                {
                    OnError("Microphone permission is required for voice commands.");
                    return;
                }
            }
            
            IsListening = true;
            _recordingStartTime = DateTime.UtcNow;
            _recordingCts = new CancellationTokenSource();
            
            OnStateChanged(VoiceAssistantState.Listening, "Listening...");
            
            // Provide audio feedback
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            
            // Start recording
            _recorder = _audioManager.CreateRecorder();
            await _recorder.StartAsync();
            
            // Set up timeout for max recording duration
            _ = Task.Delay(TimeSpan.FromSeconds(Constants.MaxRecordingDurationSeconds), _recordingCts.Token)
                .ContinueWith(async _ =>
                {
                    if (IsListening)
                    {
                        await StopListeningAndProcessAsync();
                    }
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
        catch (Exception ex)
        {
            IsListening = false;
            OnError($"Failed to start listening: {ex.Message}");
        }
    }
    
    public async Task<IntentResult?> StopListeningAndProcessAsync()
    {
        if (!IsListening || _recorder == null)
            return null;
        
        try
        {
            IsListening = false;
            IsProcessing = true;
            
            _recordingCts?.Cancel();
            
            OnStateChanged(VoiceAssistantState.Processing, "Processing...");
            
            // Stop recording
            var audioSource = await _recorder.StopAsync();
            
            if (audioSource == null)
            {
                OnError("No audio recorded.");
                return null;
            }
            
            // Get audio data
            byte[] audioData;
            using (var stream = audioSource.GetAudioStream())
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                audioData = memoryStream.ToArray();
            }
            
            if (audioData.Length < 1000)
            {
                OnError("Recording too short. Please try again.");
                return null;
            }
            
            // Calculate recording duration
            var duration = (DateTime.UtcNow - _recordingStartTime).TotalSeconds;
            
            // Transcribe audio
            var transcript = await _openAIService.TranscribeAudioAsync(
                audioData, 
                "recording.m4a",
                "en"); // Default to English
            
            if (string.IsNullOrWhiteSpace(transcript))
            {
                OnError("Could not understand. Please try again.");
                return null;
            }
            
            TranscriptAvailable?.Invoke(this, transcript);
            
            // Classify intent
            var intentResult = await _openAIService.ClassifyIntentAsync(transcript);
            
            // Speak response (if empty, use default message)
            var responseText = intentResult.Response;
            if (string.IsNullOrWhiteSpace(responseText))
            {
                // Default response if GPT didn't provide one
                responseText = $"Understood: {intentResult.Action}. ";
                if (intentResult.Parameters.Count > 0)
                {
                    responseText += string.Join(", ", intentResult.Parameters.Values);
                }
            }
            
            OnStateChanged(VoiceAssistantState.Speaking, responseText);
            LastResponse = responseText;
            await _ttsService.SpeakAsync(responseText);
            
            IntentClassified?.Invoke(this, intentResult);
            
            OnStateChanged(VoiceAssistantState.Idle);
            
            return intentResult;
        }
        catch (Exception ex)
        {
            OnError($"Processing error: {ex.Message}");
            return null;
        }
        finally
        {
            IsProcessing = false;
            IsListening = false;
        }
    }
    
    public async Task CancelAsync()
    {
        try
        {
            _recordingCts?.Cancel();
            
            if (_recorder != null && IsListening)
            {
                await _recorder.StopAsync();
            }
        }
        catch
        {
            // Ignore errors during cancellation
        }
        finally
        {
            IsListening = false;
            IsProcessing = false;
            OnStateChanged(VoiceAssistantState.Idle, "Cancelled");
        }
    }
    
    private void OnStateChanged(VoiceAssistantState state, string? message = null)
    {
        StateChanged?.Invoke(this, new VoiceAssistantStateEventArgs
        {
            State = state,
            Message = message
        });
    }
    
    private void OnError(string message)
    {
        IsListening = false;
        IsProcessing = false;
        OnStateChanged(VoiceAssistantState.Error, message);
        ErrorOccurred?.Invoke(this, message);
    }
}
