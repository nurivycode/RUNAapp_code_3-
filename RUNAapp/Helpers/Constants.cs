namespace RUNAapp.Helpers;

/// <summary>
/// Application-wide constants and configuration keys.
/// </summary>
public static class Constants
{
    // ═══════════════════════════════════════════════════════════════════════
    // Secure Storage Keys - Used to store/retrieve API keys securely
    // ═══════════════════════════════════════════════════════════════════════
    
    public const string OpenAIApiKeyStorage = "openai_api_key";
    public const string FirebaseApiKeyStorage = "firebase_api_key";
    public const string FirebaseAuthTokenStorage = "firebase_auth_token";
    public const string FirebaseRefreshTokenStorage = "firebase_refresh_token";
    public const string FirebaseUserIdStorage = "firebase_user_id";
    
    // ═══════════════════════════════════════════════════════════════════════
    // Firebase Configuration Keys
    // ═══════════════════════════════════════════════════════════════════════
    
    public const string FirebaseAuthDomainStorage = "firebase_auth_domain";
    public const string FirebaseProjectIdStorage = "firebase_project_id";
    public const string FirebaseStorageBucketStorage = "firebase_storage_bucket";
    public const string FirebaseMessagingSenderIdStorage = "firebase_messaging_sender_id";
    public const string FirebaseAppIdStorage = "firebase_app_id";
    public const string FirebaseMeasurementIdStorage = "firebase_measurement_id";
    
    // ═══════════════════════════════════════════════════════════════════════
    // Firebase REST API Endpoints
    // ═══════════════════════════════════════════════════════════════════════
    
    public const string FirebaseAuthBaseUrl = "https://identitytoolkit.googleapis.com/v1";
    public const string FirebaseSignUpEndpoint = "/accounts:signUp";
    public const string FirebaseSignInEndpoint = "/accounts:signInWithPassword";
    public const string FirebaseRefreshTokenEndpoint = "https://securetoken.googleapis.com/v1/token";
    public const string FirebaseUserInfoEndpoint = "/accounts:lookup";
    public const string FirebasePasswordResetEndpoint = "/accounts:sendOobCode";
    
    // ═══════════════════════════════════════════════════════════════════════
    // Backend API Configuration (Firebase Functions)
    // ═══════════════════════════════════════════════════════════════════════
    // Set this to your deployed Firebase Functions URL
    // Format: https://[region]-[project-id].cloudfunctions.net
    // Example: https://us-central1-runa-e1ddb.cloudfunctions.net
    public const string BackendBaseUrl = "https://us-central1-runa-e1ddb.cloudfunctions.net"; // Set after deploying Firebase Functions

    // Firestore API Endpoints (via Firebase Functions)
    public const string FirestoreGetUserProfileEndpoint = "/getUserProfile";
    public const string FirestoreGetUserSettingsEndpoint = "/getUserSettings";
    public const string FirestoreSaveUserSettingsEndpoint = "/saveUserSettings";
    public const string FirestoreCreateUserProfileEndpoint = "/createUserProfile";
    
    // ═══════════════════════════════════════════════════════════════════════
    // OpenAI API Configuration (Direct - used if BackendBaseUrl is empty)
    // ═══════════════════════════════════════════════════════════════════════
    
    public const string OpenAIBaseUrl = "https://api.openai.com/v1";
    public const string OpenAIWhisperEndpoint = "/audio/transcriptions";
    public const string OpenAIChatEndpoint = "/chat/completions";
    public const string OpenAIWhisperModel = "whisper-1";
    public const string OpenAIChatModel = "gpt-4o-mini";
    
    // ═══════════════════════════════════════════════════════════════════════
    // API Keys - Use SecureStorage or appsettings.json in production
    // ═══════════════════════════════════════════════════════════════════════
    // NOTE: These are placeholders. In production, load from SecureStorage or appsettings.json
    // See SetupViewModel.cs for secure key storage implementation
    public const string HardcodedOpenAIApiKey = "YOUR_OPENAI_API_KEY_HERE"; // Replace with actual key or load from SecureStorage
    public const string HardcodedFirebaseApiKey = "YOUR_FIREBASE_API_KEY_HERE"; // Replace with actual key or load from SecureStorage
    public const string HardcodedFirebaseAuthDomain = "your-project.firebaseapp.com"; // Replace with your Firebase project domain
    public const string HardcodedFirebaseProjectId = "your-project-id"; // Replace with your Firebase project ID
    public const string HardcodedFirebaseStorageBucket = "your-project.appspot.com"; // Replace with your Firebase storage bucket
    public const string HardcodedFirebaseMessagingSenderId = "YOUR_SENDER_ID_HERE"; // Replace with your Firebase messaging sender ID
    public const string HardcodedFirebaseAppId = "1:628120110180:web:8e09a6793f8b181657cb71";
    public const string HardcodedFirebaseMeasurementId = "G-YYJQBFH7ER";
    
    // ═══════════════════════════════════════════════════════════════════════
    // OSRM (Open Source Routing Machine) Configuration
    // ═══════════════════════════════════════════════════════════════════════
    
    // Public demo server - for production, host your own OSRM instance
    public const string OsrmBaseUrl = "https://router.project-osrm.org";
    public const string OsrmRouteEndpoint = "/route/v1/foot"; // Walking profile
    
    // ═══════════════════════════════════════════════════════════════════════
    // Nominatim (OpenStreetMap Geocoding) Configuration
    // ═══════════════════════════════════════════════════════════════════════
    
    public const string NominatimBaseUrl = "https://nominatim.openstreetmap.org";
    public const string NominatimSearchEndpoint = "/search";
    public const string NominatimReverseEndpoint = "/reverse";
    
    // ═══════════════════════════════════════════════════════════════════════
    // Computer Vision Configuration
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// ONNX model file name (stored in Resources/Raw).
    /// Using YOLOv8 nano for fast mobile inference.
    /// </summary>
    public const string OnnxModelFileName = "yolov8n.onnx";
    
    /// <summary>
    /// Minimum confidence threshold for object detection.
    /// </summary>
    public const float DetectionConfidenceThreshold = 0.5f;
    
    /// <summary>
    /// Input image size for the model.
    /// </summary>
    public const int ModelInputSize = 640;
    
    /// <summary>
    /// Frame processing interval in milliseconds.
    /// </summary>
    public const int FrameProcessingIntervalMs = 500;
    
    // ═══════════════════════════════════════════════════════════════════════
    // Voice Assistant Configuration
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Silence duration to auto-stop recording (in seconds).
    /// </summary>
    public const double SilenceTimeoutSeconds = 2.0;
    
    /// <summary>
    /// Maximum recording duration (in seconds).
    /// </summary>
    public const double MaxRecordingDurationSeconds = 30.0;
    
    /// <summary>
    /// Audio sample rate for recording.
    /// </summary>
    public const int AudioSampleRate = 16000;
    
    // ═══════════════════════════════════════════════════════════════════════
    // Text-to-Speech Configuration
    // ═══════════════════════════════════════════════════════════════════════
    
    public const float TtsSpeechRate = 1.0f;
    public const float TtsPitch = 1.0f;
    public const float TtsVolume = 1.0f;
    
    // ═══════════════════════════════════════════════════════════════════════
    // Application Preferences Keys
    // ═══════════════════════════════════════════════════════════════════════
    
    public const string PrefFirstLaunch = "first_launch";
    public const string PrefLanguage = "language";
    public const string PrefVoiceFeedbackEnabled = "voice_feedback_enabled";
    public const string PrefHapticFeedbackEnabled = "haptic_feedback_enabled";
    public const string PrefDetectionSensitivity = "detection_sensitivity";
}

/// <summary>
/// Intent classification system prompt for OpenAI.
/// </summary>
public static class IntentPrompts
{
    public const string SystemPrompt = @"You are RUNA, a voice assistant for a navigation app designed for blind users.
Your task is to classify the user's intent and extract relevant parameters.

Available actions:
- NavigateTo: User wants to navigate to a destination. Extract 'destination' parameter.
- GetDirections: User wants directions to a place. Extract 'destination' parameter.
- StartNavigation: User wants to start the current navigation.
- StopNavigation: User wants to stop navigation.
- WhereAmI: User asks about their current location.
- StartDetection: User wants to start obstacle detection / know surroundings.
- StopDetection: User wants to stop obstacle detection.
- DescribeSurroundings: User wants a description of what's around them.
- CheckStatus: User wants to know app status.
- GetHelp: User asks for help or what the app can do.
- RepeatLastMessage: User wants to hear the last message again.
- Confirm: User confirms an action (yes, okay, sure).
- Cancel: User cancels an action (no, cancel, stop).
- Unknown: Cannot determine intent.

Respond in JSON format:
{
  ""action"": ""ActionName"",
  ""confidence"": 0.95,
  ""parameters"": { ""destination"": ""value"" },
  ""response"": ""Friendly response to speak to the user"",
  ""requiresFollowUp"": false,
  ""followUpQuestion"": null
}

Be concise and helpful. Remember the user is blind, so provide clear audio feedback.";
}
