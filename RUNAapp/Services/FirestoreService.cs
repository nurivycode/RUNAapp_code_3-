using System.Net.Http.Json;
using System.Text.Json;
using RUNAapp.Helpers;
using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Firestore service implementation that calls Firebase Functions backend.
/// </summary>
public class FirestoreService : IFirestoreService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public FirestoreService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private static string GetBackendUrl()
    {
        // Backend URL is configured in Constants.cs
        return Constants.BackendBaseUrl;
    }

    public async Task<UserProfile?> GetUserProfileAsync(string idToken)
    {
        try
        {
            var backendUrl = GetBackendUrl();
            if (string.IsNullOrEmpty(backendUrl))
            {
                System.Diagnostics.Debug.WriteLine("Backend URL not configured");
                return null;
            }

            var url = $"{backendUrl}{Constants.FirestoreGetUserProfileEndpoint}";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {idToken}");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"GetUserProfile response: {content}");

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = JsonSerializer.Deserialize<ProfileResponse>(content, _jsonOptions);
                throw new FirestoreException(errorResponse?.Error ?? $"Failed to get user profile: {response.StatusCode}");
            }

            var profileResponse = JsonSerializer.Deserialize<ProfileResponse>(content, _jsonOptions);

            if (profileResponse == null)
                return null;

            // Handle both direct response and nested profile response
            return new UserProfile
            {
                UserId = profileResponse.UserId ?? profileResponse.Profile?.UserId ?? "",
                Email = profileResponse.Email ?? profileResponse.Profile?.Email ?? "",
                DisplayName = profileResponse.DisplayName ?? profileResponse.Profile?.DisplayName ?? "",
                CreatedAt = profileResponse.CreatedAt ?? profileResponse.Profile?.CreatedAt
            };
        }
        catch (FirestoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetUserProfile error: {ex.Message}");
            throw new FirestoreException($"Failed to get user profile: {ex.Message}", ex);
        }
    }

    public async Task<UserSettings> GetUserSettingsAsync(string idToken)
    {
        try
        {
            var backendUrl = GetBackendUrl();
            if (string.IsNullOrEmpty(backendUrl))
            {
                System.Diagnostics.Debug.WriteLine("Backend URL not configured, returning default settings");
                return UserSettings.Default;
            }

            var url = $"{backendUrl}{Constants.FirestoreGetUserSettingsEndpoint}";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {idToken}");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"GetUserSettings response: {content}");

            if (!response.IsSuccessStatusCode)
            {
                // Return default settings on error
                System.Diagnostics.Debug.WriteLine($"GetUserSettings failed: {response.StatusCode}");
                return UserSettings.Default;
            }

            var settings = JsonSerializer.Deserialize<UserSettings>(content, _jsonOptions);
            return settings ?? UserSettings.Default;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetUserSettings error: {ex.Message}");
            // Return default settings on error
            return UserSettings.Default;
        }
    }

    public async Task<bool> SaveUserSettingsAsync(string idToken, UserSettings settings)
    {
        try
        {
            var backendUrl = GetBackendUrl();
            if (string.IsNullOrEmpty(backendUrl))
            {
                System.Diagnostics.Debug.WriteLine("Backend URL not configured");
                return false;
            }

            var url = $"{backendUrl}{Constants.FirestoreSaveUserSettingsEndpoint}";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {idToken}");
            request.Content = JsonContent.Create(settings);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"SaveUserSettings response: {content}");

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"SaveUserSettings failed: {response.StatusCode}");
                return false;
            }

            var result = JsonSerializer.Deserialize<SaveSettingsResponse>(content, _jsonOptions);
            return result?.Success ?? false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveUserSettings error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CreateUserProfileAsync(string idToken, string? displayName = null)
    {
        try
        {
            var backendUrl = GetBackendUrl();
            if (string.IsNullOrEmpty(backendUrl))
            {
                System.Diagnostics.Debug.WriteLine("Backend URL not configured");
                return false;
            }

            var url = $"{backendUrl}{Constants.FirestoreCreateUserProfileEndpoint}";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {idToken}");

            if (!string.IsNullOrEmpty(displayName))
            {
                request.Content = JsonContent.Create(new { displayName });
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"CreateUserProfile response: {content}");

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"CreateUserProfile failed: {response.StatusCode}");
                return false;
            }

            var result = JsonSerializer.Deserialize<ProfileResponse>(content, _jsonOptions);
            return result?.Success ?? false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateUserProfile error: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Custom exception for Firestore errors.
/// </summary>
public class FirestoreException : Exception
{
    public FirestoreException(string message) : base(message) { }
    public FirestoreException(string message, Exception inner) : base(message, inner) { }
}
