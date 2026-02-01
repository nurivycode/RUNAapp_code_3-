using System.Net.Http.Json;
using System.Text.Json;
using RUNAapp.Helpers;
using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Firebase Authentication service using REST API.
/// </summary>
public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private User? _currentUser;
    
    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;
    
    public event EventHandler<User?>? AuthStateChanged;
    
    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<User> SignUpAsync(string email, string password)
    {
        var apiKey = await SecureStorageHelper.GetFirebaseApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("Firebase API key not configured. Please set up your API keys first.");
        
        var url = $"{Constants.FirebaseAuthBaseUrl}{Constants.FirebaseSignUpEndpoint}?key={apiKey}";
        
        var request = new
        {
            email,
            password,
            returnSecureToken = true
        };
        
        var response = await _httpClient.PostAsJsonAsync(url, request);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<FirebaseErrorResponse>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            throw new AuthException(ParseFirebaseError(error?.Error?.Message ?? "Unknown error"));
        }
        
        var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (authResponse == null)
            throw new AuthException("Failed to parse authentication response");
        
        // Save tokens
        await SecureStorageHelper.SaveAuthTokensAsync(
            authResponse.IdToken, 
            authResponse.RefreshToken, 
            authResponse.LocalId);
        
        // Create user object
        _currentUser = new User
        {
            Uid = authResponse.LocalId,
            Email = authResponse.Email,
            EmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };
        
        AuthStateChanged?.Invoke(this, _currentUser);
        
        return _currentUser;
    }
    
    public async Task<User> SignInAsync(string email, string password)
    {
        var apiKey = await SecureStorageHelper.GetFirebaseApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("Firebase API key not configured. Please set up your API keys first.");
        
        var url = $"{Constants.FirebaseAuthBaseUrl}{Constants.FirebaseSignInEndpoint}?key={apiKey}";
        
        var request = new
        {
            email,
            password,
            returnSecureToken = true
        };
        
        var response = await _httpClient.PostAsJsonAsync(url, request);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<FirebaseErrorResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            throw new AuthException(ParseFirebaseError(error?.Error?.Message ?? "Unknown error"));
        }
        
        var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (authResponse == null)
            throw new AuthException("Failed to parse authentication response");
        
        // Save tokens
        await SecureStorageHelper.SaveAuthTokensAsync(
            authResponse.IdToken,
            authResponse.RefreshToken,
            authResponse.LocalId);
        
        // Create user object
        _currentUser = new User
        {
            Uid = authResponse.LocalId,
            Email = authResponse.Email,
            EmailVerified = true, // Assume verified for sign-in
            LastLoginAt = DateTime.UtcNow
        };
        
        AuthStateChanged?.Invoke(this, _currentUser);
        
        return _currentUser;
    }
    
    public async Task SignOutAsync()
    {
        await SecureStorageHelper.ClearAuthTokensAsync();
        _currentUser = null;
        AuthStateChanged?.Invoke(this, null);
    }
    
    public async Task SendPasswordResetEmailAsync(string email)
    {
        var apiKey = await SecureStorageHelper.GetFirebaseApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("Firebase API key not configured.");
        
        var url = $"{Constants.FirebaseAuthBaseUrl}{Constants.FirebasePasswordResetEndpoint}?key={apiKey}";
        
        var request = new
        {
            requestType = "PASSWORD_RESET",
            email
        };
        
        var response = await _httpClient.PostAsJsonAsync(url, request);
        
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var error = JsonSerializer.Deserialize<FirebaseErrorResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            throw new AuthException(ParseFirebaseError(error?.Error?.Message ?? "Unknown error"));
        }
    }
    
    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var (idToken, refreshToken, userId) = await SecureStorageHelper.GetAuthTokensAsync();
            
            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(userId))
                return false;
            
            // Try to refresh the token
            await RefreshTokenAsync();
            
            // Get user info
            var apiKey = await SecureStorageHelper.GetFirebaseApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
                return false;
            
            var (newIdToken, _, _) = await SecureStorageHelper.GetAuthTokensAsync();
            
            var url = $"{Constants.FirebaseAuthBaseUrl}{Constants.FirebaseUserInfoEndpoint}?key={apiKey}";
            
            var request = new { idToken = newIdToken };
            var response = await _httpClient.PostAsJsonAsync(url, request);
            
            if (!response.IsSuccessStatusCode)
                return false;
            
            var content = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<JsonElement>(content);
            
            if (userInfo.TryGetProperty("users", out var users) && users.GetArrayLength() > 0)
            {
                var user = users[0];
                _currentUser = new User
                {
                    Uid = user.GetProperty("localId").GetString() ?? "",
                    Email = user.GetProperty("email").GetString() ?? "",
                    EmailVerified = user.TryGetProperty("emailVerified", out var ev) && ev.GetBoolean(),
                    LastLoginAt = DateTime.UtcNow
                };
                
                AuthStateChanged?.Invoke(this, _currentUser);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error restoring session: {ex.Message}");
            return false;
        }
    }
    
    public async Task RefreshTokenAsync()
    {
        var apiKey = await SecureStorageHelper.GetFirebaseApiKeyAsync();
        var (_, refreshToken, _) = await SecureStorageHelper.GetAuthTokensAsync();
        
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(refreshToken))
            throw new AuthException("No refresh token available");
        
        var url = $"{Constants.FirebaseRefreshTokenEndpoint}?key={apiKey}";
        
        var request = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        });
        
        var response = await _httpClient.PostAsync(url, request);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            throw new AuthException("Failed to refresh token");
        
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);
        
        var newIdToken = tokenResponse.GetProperty("id_token").GetString() ?? "";
        var newRefreshToken = tokenResponse.GetProperty("refresh_token").GetString() ?? "";
        var userId = tokenResponse.GetProperty("user_id").GetString() ?? "";
        
        await SecureStorageHelper.SaveAuthTokensAsync(newIdToken, newRefreshToken, userId);
    }
    
    private static string ParseFirebaseError(string errorCode)
    {
        return errorCode switch
        {
            "EMAIL_EXISTS" => "An account with this email already exists.",
            "INVALID_EMAIL" => "Please enter a valid email address.",
            "WEAK_PASSWORD" => "Password should be at least 6 characters.",
            "EMAIL_NOT_FOUND" => "No account found with this email.",
            "INVALID_PASSWORD" => "Incorrect password. Please try again.",
            "USER_DISABLED" => "This account has been disabled.",
            "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many attempts. Please try again later.",
            "INVALID_LOGIN_CREDENTIALS" => "Invalid email or password.",
            _ => $"Authentication error: {errorCode}"
        };
    }
}

/// <summary>
/// Custom exception for authentication errors.
/// </summary>
public class AuthException : Exception
{
    public AuthException(string message) : base(message) { }
    public AuthException(string message, Exception inner) : base(message, inner) { }
}
