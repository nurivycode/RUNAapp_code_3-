using System.Text.Json;
using Microsoft.Maui.Storage;

namespace RUNAapp.Helpers;

public static class AppConfigHelper
{
    private static FirebaseConfig? _cachedFirebaseConfig;
    private static bool _configLoaded;

    public static async Task<FirebaseConfig?> GetFirebaseConfigAsync()
    {
        if (_configLoaded)
        {
            return _cachedFirebaseConfig;
        }

        _configLoaded = true;

        try
        {
            var secureConfig = await TryLoadFromSecureStorageAsync();
            if (secureConfig != null && !string.IsNullOrWhiteSpace(secureConfig.ApiKey))
            {
                System.Diagnostics.Debug.WriteLine($"Firebase config source: SecureStorage (key ends with {MaskKey(secureConfig.ApiKey)})");
                _cachedFirebaseConfig = secureConfig;
                return _cachedFirebaseConfig;
            }

            _cachedFirebaseConfig = await TryLoadFromPackagedFileAsync();
            if (_cachedFirebaseConfig != null && !string.IsNullOrWhiteSpace(_cachedFirebaseConfig.ApiKey))
            {
                System.Diagnostics.Debug.WriteLine($"Firebase config source: AppPackage (key ends with {MaskKey(_cachedFirebaseConfig.ApiKey)})");
            }
            return _cachedFirebaseConfig;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading appsettings.json: {ex.Message}");
            return null;
        }
    }

    public static void ResetCache()
    {
        _cachedFirebaseConfig = null;
        _configLoaded = false;
    }

    private static async Task<FirebaseConfig?> TryLoadFromSecureStorageAsync()
    {
        try
        {
            var apiKey = await SecureStorage.Default.GetAsync(Constants.FirebaseApiKeyStorage);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            return new FirebaseConfig
            {
                ApiKey = apiKey,
                AuthDomain = await SecureStorage.Default.GetAsync(Constants.FirebaseAuthDomainStorage) ?? string.Empty,
                ProjectId = await SecureStorage.Default.GetAsync(Constants.FirebaseProjectIdStorage) ?? string.Empty,
                StorageBucket = await SecureStorage.Default.GetAsync(Constants.FirebaseStorageBucketStorage) ?? string.Empty,
                MessagingSenderId = await SecureStorage.Default.GetAsync(Constants.FirebaseMessagingSenderIdStorage) ?? string.Empty,
                AppId = await SecureStorage.Default.GetAsync(Constants.FirebaseAppIdStorage) ?? string.Empty,
                MeasurementId = await SecureStorage.Default.GetAsync(Constants.FirebaseMeasurementIdStorage)
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading secure storage config: {ex.Message}");
            return null;
        }
    }

    private static async Task<FirebaseConfig?> TryLoadFromPackagedFileAsync()
    {
        var json = await TryReadPackagedFileAsync("appsettings.json")
                   ?? await TryReadPackagedFileAsync("Raw/appsettings.json");

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (settings?.Firebase == null)
        {
            return null;
        }

        return new FirebaseConfig
        {
            ApiKey = settings.Firebase.ApiKey ?? string.Empty,
            AuthDomain = settings.Firebase.AuthDomain ?? string.Empty,
            ProjectId = settings.Firebase.ProjectId ?? string.Empty,
            StorageBucket = settings.Firebase.StorageBucket ?? string.Empty,
            MessagingSenderId = settings.Firebase.MessagingSenderId ?? string.Empty,
            AppId = settings.Firebase.AppId ?? string.Empty,
            MeasurementId = settings.Firebase.MeasurementId
        };
    }

    private static async Task<string?> TryReadPackagedFileAsync(string path)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(path);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading packaged file '{path}': {ex.Message}");
            return null;
        }
    }

    private static string MaskKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < 4)
        {
            return "****";
        }

        return $"****{apiKey[^4..]}";
    }
}

public class AppSettings
{
    public FirebaseSettings? Firebase { get; set; }
}

public class FirebaseSettings
{
    public string? ApiKey { get; set; }
    public string? AuthDomain { get; set; }
    public string? ProjectId { get; set; }
    public string? StorageBucket { get; set; }
    public string? MessagingSenderId { get; set; }
    public string? AppId { get; set; }
    public string? MeasurementId { get; set; }
}
