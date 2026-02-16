namespace RUNAapp.Helpers;

public static class SecureStorageHelper
{
    public static async Task<string?> GetFirebaseApiKeyAsync()
    {
        var config = await AppConfigHelper.GetFirebaseConfigAsync();
        return config?.ApiKey;
    }

    public static async Task SetFirebaseApiKeyAsync(string apiKey)
    {
        await SecureStorage.Default.SetAsync(Constants.FirebaseApiKeyStorage, apiKey);
        AppConfigHelper.ResetCache();
    }

    public static async Task SetFirebaseConfigAsync(
        string apiKey,
        string authDomain,
        string projectId,
        string storageBucket,
        string messagingSenderId,
        string appId,
        string? measurementId = null)
    {
        await SecureStorage.Default.SetAsync(Constants.FirebaseApiKeyStorage, apiKey);
        await SecureStorage.Default.SetAsync(Constants.FirebaseAuthDomainStorage, authDomain);
        await SecureStorage.Default.SetAsync(Constants.FirebaseProjectIdStorage, projectId);
        await SecureStorage.Default.SetAsync(Constants.FirebaseStorageBucketStorage, storageBucket);
        await SecureStorage.Default.SetAsync(Constants.FirebaseMessagingSenderIdStorage, messagingSenderId);
        await SecureStorage.Default.SetAsync(Constants.FirebaseAppIdStorage, appId);

        if (!string.IsNullOrEmpty(measurementId))
        {
            await SecureStorage.Default.SetAsync(Constants.FirebaseMeasurementIdStorage, measurementId);
        }

        AppConfigHelper.ResetCache();
    }

    public static async Task<FirebaseConfig?> GetFirebaseConfigAsync()
    {
        return await AppConfigHelper.GetFirebaseConfigAsync();
    }

    public static async Task SaveAuthTokensAsync(string idToken, string refreshToken, string userId)
    {
        await SecureStorage.Default.SetAsync(Constants.FirebaseAuthTokenStorage, idToken);
        await SecureStorage.Default.SetAsync(Constants.FirebaseRefreshTokenStorage, refreshToken);
        await SecureStorage.Default.SetAsync(Constants.FirebaseUserIdStorage, userId);
    }

    public static async Task<(string? IdToken, string? RefreshToken, string? UserId)> GetAuthTokensAsync()
    {
        try
        {
            var idToken = await SecureStorage.Default.GetAsync(Constants.FirebaseAuthTokenStorage);
            var refreshToken = await SecureStorage.Default.GetAsync(Constants.FirebaseRefreshTokenStorage);
            var userId = await SecureStorage.Default.GetAsync(Constants.FirebaseUserIdStorage);

            return (idToken, refreshToken, userId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting auth tokens: {ex.Message}");
            return (null, null, null);
        }
    }

    public static async Task ClearAuthTokensAsync()
    {
        SecureStorage.Default.Remove(Constants.FirebaseAuthTokenStorage);
        SecureStorage.Default.Remove(Constants.FirebaseRefreshTokenStorage);
        SecureStorage.Default.Remove(Constants.FirebaseUserIdStorage);
        await Task.CompletedTask;
    }

    public static async Task<bool> IsFirebaseConfiguredAsync()
    {
        var config = await GetFirebaseConfigAsync();
        return config != null && !string.IsNullOrEmpty(config.ApiKey);
    }

    public static async Task<string> GetOrCreateDeviceIdAsync()
    {
        string? deviceId = null;

#if ANDROID
        try
        {
            deviceId = Android.Provider.Settings.Secure.GetString(
                Android.App.Application.Context.ContentResolver,
                Android.Provider.Settings.Secure.AndroidId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading Android ID: {ex.Message}");
        }
#endif

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            var existing = await SecureStorage.Default.GetAsync(Constants.DeviceIdStorage);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            deviceId = Guid.NewGuid().ToString("N");
        }

        await SecureStorage.Default.SetAsync(Constants.DeviceIdStorage, deviceId);
        return deviceId;
    }

    public static void ClearAll()
    {
        SecureStorage.Default.RemoveAll();
        AppConfigHelper.ResetCache();
    }
}

public class FirebaseConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string AuthDomain { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string StorageBucket { get; set; } = string.Empty;
    public string MessagingSenderId { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string? MeasurementId { get; set; }
}
