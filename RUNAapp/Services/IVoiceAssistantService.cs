using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Voice assistant service interface for voice command processing.
/// </summary>
public interface IVoiceAssistantService
{
    /// <summary>
    /// Gets whether the assistant is currently listening.
    /// </summary>
    bool IsListening { get; }
    
    /// <summary>
    /// Gets whether the assistant is processing a command.
    /// </summary>
    bool IsProcessing { get; }
    
    /// <summary>
    /// Gets the last spoken response.
    /// </summary>
    string? LastResponse { get; }
    
    /// <summary>
    /// Starts listening for voice commands.
    /// </summary>
    Task StartListeningAsync();
    
    /// <summary>
    /// Stops listening and processes the recorded audio.
    /// </summary>
    Task<IntentResult?> StopListeningAndProcessAsync();
    
    /// <summary>
    /// Cancels the current listening session without processing.
    /// </summary>
    Task CancelAsync();
    
    /// <summary>
    /// Processes a typed command through the same intent pipeline as voice.
    /// Useful for developer testing and fallback input.
    /// </summary>
    Task<IntentResult?> ProcessTextCommandAsync(string commandText);
    
    /// <summary>
    /// Event fired when listening state changes.
    /// </summary>
    event EventHandler<VoiceAssistantStateEventArgs>? StateChanged;
    
    /// <summary>
    /// Event fired when transcription is available.
    /// </summary>
    event EventHandler<string>? TranscriptAvailable;
    
    /// <summary>
    /// Event fired when an intent is classified.
    /// </summary>
    event EventHandler<IntentResult>? IntentClassified;
    
    /// <summary>
    /// Event fired when an error occurs.
    /// </summary>
    event EventHandler<string>? ErrorOccurred;
}

/// <summary>
/// Voice assistant state event arguments.
/// </summary>
public class VoiceAssistantStateEventArgs : EventArgs
{
    public VoiceAssistantState State { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Voice assistant states.
/// </summary>
public enum VoiceAssistantState
{
    Idle,
    Listening,
    Processing,
    Speaking,
    Error
}
