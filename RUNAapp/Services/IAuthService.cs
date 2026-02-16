using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Authentication service interface for Firebase Auth operations.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Gets the currently authenticated user, if any.
    /// </summary>
    User? CurrentUser { get; }
    
    /// <summary>
    /// Gets whether a user is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Event fired when authentication state changes.
    /// </summary>
    event EventHandler<User?>? AuthStateChanged;
    
    /// <summary>
    /// Signs up a new user with email and password.
    /// </summary>
    /// <param name="email">User's email address.</param>
    /// <param name="password">User's password.</param>
    /// <returns>The created user or throws an exception.</returns>
    Task<User> SignUpAsync(string email, string password);
    
    /// <summary>
    /// Signs in an existing user with email and password.
    /// </summary>
    /// <param name="email">User's email address.</param>
    /// <param name="password">User's password.</param>
    /// <returns>The authenticated user or throws an exception.</returns>
    Task<User> SignInAsync(string email, string password);

    /// <summary>
    /// Signs in a user using an access code and device binding.
    /// </summary>
    /// <param name="accessCode">Access code provided by the company.</param>
    /// <param name="deviceId">Stable device identifier.</param>
    /// <returns>The authenticated user or throws an exception.</returns>
    Task<User> SignInWithAccessCodeAsync(string accessCode, string deviceId);
    
    /// <summary>
    /// Signs out the current user.
    /// </summary>
    Task SignOutAsync();
    
    /// <summary>
    /// Sends a password reset email.
    /// </summary>
    /// <param name="email">User's email address.</param>
    Task SendPasswordResetEmailAsync(string email);
    
    /// <summary>
    /// Attempts to restore a previous session.
    /// </summary>
    /// <returns>True if session was restored, false otherwise.</returns>
    Task<bool> TryRestoreSessionAsync();
    
    /// <summary>
    /// Refreshes the authentication token.
    /// </summary>
    Task RefreshTokenAsync();
}
