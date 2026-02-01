namespace RUNAapp.Models;

/// <summary>
/// Represents a voice command from the user.
/// </summary>
public class VoiceCommand
{
    /// <summary>
    /// The transcribed text from speech recognition.
    /// </summary>
    public string Transcript { get; set; } = string.Empty;
    
    /// <summary>
    /// Confidence score of the transcription (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; set; }
    
    /// <summary>
    /// Language code of the transcription.
    /// </summary>
    public string Language { get; set; } = "en";
    
    /// <summary>
    /// Timestamp when the command was recorded.
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Duration of the audio recording in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }
}

/// <summary>
/// Result of intent classification from OpenAI.
/// </summary>
public class IntentResult
{
    /// <summary>
    /// The classified intent action.
    /// </summary>
    public IntentAction Action { get; set; } = IntentAction.Unknown;
    
    /// <summary>
    /// Confidence of the classification (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; set; }
    
    /// <summary>
    /// Extracted parameters from the command.
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
    
    /// <summary>
    /// The response message to speak back to the user.
    /// </summary>
    public string Response { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the action requires further user input.
    /// </summary>
    public bool RequiresFollowUp { get; set; }
    
    /// <summary>
    /// Follow-up question if needed.
    /// </summary>
    public string? FollowUpQuestion { get; set; }
    
    /// <summary>
    /// The original transcript that was classified.
    /// </summary>
    public string OriginalTranscript { get; set; } = string.Empty;
}

/// <summary>
/// Available intent actions in the RUNA application.
/// </summary>
public enum IntentAction
{
    // Navigation intents
    NavigateTo,           // "Take me to [destination]"
    GetDirections,        // "How do I get to [destination]"
    StartNavigation,      // "Start navigation"
    StopNavigation,       // "Stop navigation"
    WhereAmI,            // "Where am I?"
    
    // Vision intents
    StartDetection,       // "Start looking" / "What's around me"
    StopDetection,        // "Stop looking"
    DescribeSurroundings, // "Describe what you see"
    
    // Information intents
    CheckStatus,          // "What's my status"
    GetHelp,             // "Help" / "What can you do"
    
    // Control intents
    RepeatLastMessage,    // "Repeat that"
    Confirm,             // "Yes" / "Confirm"
    Cancel,              // "Cancel" / "No"
    
    // Unknown/fallback
    Unknown              // Unrecognized intent
}

/// <summary>
/// OpenAI API request model for chat completions.
/// </summary>
public class OpenAIChatRequest
{
    public string Model { get; set; } = "gpt-4o-mini";
    public List<OpenAIChatMessage> Messages { get; set; } = new();
    public float Temperature { get; set; } = 0.3f;
    public int MaxTokens { get; set; } = 256;
    public OpenAIResponseFormat? ResponseFormat { get; set; }
}

public class OpenAIChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class OpenAIResponseFormat
{
    public string Type { get; set; } = "json_object";
}

/// <summary>
/// OpenAI API response model.
/// </summary>
public class OpenAIChatResponse
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public List<OpenAIChoice> Choices { get; set; } = new();
    public OpenAIUsage? Usage { get; set; }
}

public class OpenAIChoice
{
    public int Index { get; set; }
    public OpenAIChatMessage Message { get; set; } = new();
    public string? FinishReason { get; set; }
}

public class OpenAIUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

/// <summary>
/// OpenAI Whisper transcription request.
/// </summary>
public class WhisperTranscriptionResponse
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Backend API transcription response.
/// </summary>
public class BackendTranscribeResponse
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Backend API intent classification response.
/// </summary>
public class BackendClassifyIntentResponse
{
    public string Action { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public string Response { get; set; } = string.Empty;
    public bool RequiresFollowUp { get; set; }
    public string? FollowUpQuestion { get; set; }
}

/// <summary>
/// Intent classification response from OpenAI (JSON format).
/// </summary>
public class IntentClassificationResponse
{
    public string Action { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public string Response { get; set; } = string.Empty;
    public bool RequiresFollowUp { get; set; }
    public string? FollowUpQuestion { get; set; }
}
