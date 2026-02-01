using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Service interface for OpenAI API operations.
/// Handles speech transcription (Whisper) and intent classification (GPT).
/// </summary>
public interface IOpenAIService
{
    /// <summary>
    /// Transcribes audio to text using OpenAI Whisper API.
    /// </summary>
    /// <param name="audioData">Audio file bytes.</param>
    /// <param name="fileName">Name of the audio file with extension.</param>
    /// <param name="language">Optional language code (e.g., "en", "ru").</param>
    /// <returns>Transcribed text.</returns>
    Task<string> TranscribeAudioAsync(byte[] audioData, string fileName, string? language = null);
    
    /// <summary>
    /// Classifies the intent of a user's voice command.
    /// </summary>
    /// <param name="transcript">The transcribed text from the user.</param>
    /// <returns>Intent classification result.</returns>
    Task<IntentResult> ClassifyIntentAsync(string transcript);
    
    /// <summary>
    /// Gets a chat response from GPT for general queries.
    /// </summary>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="systemPrompt">Optional system prompt override.</param>
    /// <returns>GPT response text.</returns>
    Task<string> GetChatResponseAsync(string userMessage, string? systemPrompt = null);
}
