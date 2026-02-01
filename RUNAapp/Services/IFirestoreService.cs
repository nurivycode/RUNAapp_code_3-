using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Interface for Firestore operations (user profiles and settings).
/// </summary>
public interface IFirestoreService
{
    /// <summary>
    /// Gets the user profile from Firestore.
    /// </summary>
    /// <param name="idToken">Firebase ID token for authentication.</param>
    /// <returns>The user profile or null if not found.</returns>
    Task<UserProfile?> GetUserProfileAsync(string idToken);

    /// <summary>
    /// Gets user settings from Firestore.
    /// </summary>
    /// <param name="idToken">Firebase ID token for authentication.</param>
    /// <returns>The user settings or default settings if not found.</returns>
    Task<UserSettings> GetUserSettingsAsync(string idToken);

    /// <summary>
    /// Saves user settings to Firestore.
    /// </summary>
    /// <param name="idToken">Firebase ID token for authentication.</param>
    /// <param name="settings">The settings to save.</param>
    /// <returns>True if saved successfully.</returns>
    Task<bool> SaveUserSettingsAsync(string idToken, UserSettings settings);

    /// <summary>
    /// Creates or updates user profile in Firestore.
    /// </summary>
    /// <param name="idToken">Firebase ID token for authentication.</param>
    /// <param name="displayName">Optional display name.</param>
    /// <returns>True if created/updated successfully.</returns>
    Task<bool> CreateUserProfileAsync(string idToken, string? displayName = null);
}
