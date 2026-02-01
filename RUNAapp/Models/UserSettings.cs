using System.Text.Json.Serialization;

namespace RUNAapp.Models;

/// <summary>
/// User settings stored in Firestore.
/// </summary>
public class UserSettings
{
    [JsonPropertyName("voiceFeedbackEnabled")]
    public bool VoiceFeedbackEnabled { get; set; } = true;

    [JsonPropertyName("hapticFeedbackEnabled")]
    public bool HapticFeedbackEnabled { get; set; } = true;

    [JsonPropertyName("sensitivityMode")]
    public string SensitivityMode { get; set; } = "medium";

    /// <summary>
    /// Creates default settings.
    /// </summary>
    public static UserSettings Default => new()
    {
        VoiceFeedbackEnabled = true,
        HapticFeedbackEnabled = true,
        SensitivityMode = "medium"
    };
}

/// <summary>
/// User profile stored in Firestore.
/// </summary>
public class UserProfile
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }
}

/// <summary>
/// Response from save settings API.
/// </summary>
public class SaveSettingsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("settings")]
    public UserSettings? Settings { get; set; }
}

/// <summary>
/// Response from save/get profile API.
/// </summary>
public class ProfileResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("profile")]
    public UserProfile? Profile { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
