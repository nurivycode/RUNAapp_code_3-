namespace RUNAapp.Services;

/// <summary>
/// Text-to-speech service interface for audio feedback.
/// </summary>
public interface ITextToSpeechService
{
    /// <summary>
    /// Gets whether speech is currently playing.
    /// </summary>
    bool IsSpeaking { get; }
    
    /// <summary>
    /// Speaks the given text.
    /// </summary>
    /// <param name="text">Text to speak.</param>
    /// <param name="interrupt">If true, stops any current speech first.</param>
    Task SpeakAsync(string text, bool interrupt = true);
    
    /// <summary>
    /// Speaks a high-priority alert (louder, faster).
    /// </summary>
    /// <param name="text">Alert text to speak.</param>
    Task SpeakAlertAsync(string text);
    
    /// <summary>
    /// Stops any current speech.
    /// </summary>
    Task StopSpeakingAsync();
    
    /// <summary>
    /// Sets the speech rate.
    /// </summary>
    /// <param name="rate">Rate from 0.0 to 2.0, where 1.0 is normal.</param>
    void SetSpeechRate(float rate);
    
    /// <summary>
    /// Sets the speech pitch.
    /// </summary>
    /// <param name="pitch">Pitch from 0.0 to 2.0, where 1.0 is normal.</param>
    void SetPitch(float pitch);
}
