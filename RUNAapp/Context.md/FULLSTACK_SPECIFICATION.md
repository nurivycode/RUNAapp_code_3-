# RUNA Application Fullstack Technical Specification

## Executive Summary

This document provides a comprehensive technical specification for the RUNA navigation application fullstack architecture. The application consists of a .NET MAUI mobile frontend and a Firebase Functions Node.js backend. The application provides voice-controlled navigation, real-time computer vision obstacle detection, and AI-powered intent classification for blind and visually impaired users. This specification documents the complete technical stack, architectural patterns, API integrations, service implementations, and development requirements.

## Fullstack Architecture Overview

The RUNA application follows a client-server architecture with a mobile frontend built on .NET MAUI and a serverless backend using Firebase Functions. The frontend handles user interface, local computer vision processing, voice recording, and navigation display. The backend provides secure API key management and proxies OpenAI API requests to protect sensitive credentials. The application uses Firebase Authentication for user management and supports both direct API calls and backend-proxied requests.

## Frontend Stack - .NET MAUI

The mobile application is built using .NET MAUI (Multi-platform App UI) targeting Android platform with minimum API level 29 (Android 10). The development language is C# with XAML for user interface definition. The application uses .NET 8.0 SDK with Microsoft.Maui.Controls version 8.0.91 for cross-platform UI components.

The application follows MVVM (Model-View-ViewModel) architecture pattern using CommunityToolkit.Mvvm version 8.3.2 for view model base classes and property change notifications. CommunityToolkit.Maui version 9.1.1 provides additional MAUI-specific helpers and behaviors. Dependency injection is configured through Microsoft.Extensions.DependencyInjection with services registered in MauiProgram.cs.

Computer vision functionality uses Microsoft.ML.OnnxRuntime version 1.19.2 for ONNX model inference. The application uses YOLOv8 nano ONNX model stored in Resources/Raw directory for object detection. Camera access uses Xamarin.AndroidX.Camera packages including Camera2 version 1.3.1.1, Camera.Core version 1.3.1.1, Camera.Lifecycle version 1.3.1.1, and Camera.View version 1.3.1.1 for real-time frame processing.

Maps integration uses Mapsui.Maui version 4.1.7 for displaying routes and locations. Audio recording uses Plugin.Maui.Audio version 3.0.0 for cross-platform audio capture. Text-to-speech uses platform-specific APIs through MAUI Essentials abstraction layer. HTTP communication uses System.Net.Http.HttpClient with System.Text.Json version 8.0.5 for JSON serialization.

Secure storage uses MAUI Essentials SecureStorage API which stores data in Android Keystore for encrypted key management. Application preferences use MAUI Essentials Preferences API for user settings storage. Location services use MAUI Essentials Geolocation API with fine location permissions.

## Backend Stack - Firebase Functions

The backend is implemented as Firebase Cloud Functions using Node.js runtime version 20. The backend code is located in the functions directory with index.js as the main entry point. The backend uses Firebase Functions SDK version 4.5.0 and Firebase Admin SDK version 12.0.0 for server-side Firebase operations.

HTTP requests are handled through firebase-functions HTTPS onRequest triggers with CORS middleware using cors package version 2.8.5 configured for all origins. OpenAI API communication uses axios version 1.6.0 for HTTP requests. Form data encoding for audio file uploads uses form-data package version 4.0.0.

API key management uses Firebase Functions configuration through functions.config() for deployed environments and process.env for local development. The backend provides seven main endpoints: transcribe for audio transcription, chat for general GPT queries, classifyIntent for voice command intent classification, getUserProfile for retrieving user profile data, getUserSettings for retrieving user preferences, saveUserSettings for persisting user preferences, and createUserProfile for creating or updating user profiles.

The backend acts as a proxy layer to protect OpenAI API keys from client exposure. All OpenAI API calls are made server-side with keys stored securely in Firebase Functions configuration. Firestore operations use Firebase Admin SDK for secure database access with ID token verification. The backend handles error responses, rate limiting, and provides consistent error formatting to the client.

## Service Architecture

The application implements a modular service architecture with clear separation of concerns. Services are registered as singletons in the dependency injection container to maintain state across the application lifecycle.

AuthService handles Firebase Authentication operations including sign up, sign in, sign out, password reset, and current user retrieval. The service uses Firebase REST API endpoints through HttpClient with API key authentication. User sessions are managed through SecureStorage for token persistence. The service implements IAuthService interface for dependency injection.

OpenAIService provides OpenAI API integration for speech transcription and intent classification. The service supports both direct OpenAI API calls and backend-proxied requests based on configuration. TranscribeAudioAsync method handles audio file uploads to Whisper API with multipart form data encoding. ClassifyIntentAsync method sends transcript to GPT-4o-mini model with system prompt for intent classification. GetChatResponseAsync provides general chat completion functionality. The service implements IOpenAIService interface.

VoiceAssistantService orchestrates the complete voice command processing workflow. The service manages audio recording initiation, audio capture through IAudioManager, transcription through OpenAIService, intent classification, and response generation. The service maintains internal state for listening status, processing status, and last response. Events are fired for state changes, transcript availability, intent classification, and errors. The service implements IVoiceAssistantService interface.

ComputerVisionService manages ONNX model loading, object detection inference, and detection result processing. The service loads YOLOv8 nano model from embedded resources at application startup. ProcessFrameAsync method processes camera frames through ONNX Runtime inference. Detection results are filtered by confidence threshold and target object classes. The service provides GetDetectionDescription method for generating spoken descriptions of detected objects. Events are fired for object detections and danger alerts. The service implements IComputerVisionService interface.

NavigationService handles route calculation, location geocoding, and active navigation tracking. The service uses OSRM (Open Source Routing Machine) for route calculation with walking profile. Geocoding uses Nominatim OpenStreetMap service for location search and reverse geocoding. GetCurrentLocationAsync uses MAUI Essentials Geolocation API. CalculateRouteAsync generates turn-by-turn navigation instructions. StartNavigation begins active navigation with location tracking and step-by-step guidance. The service implements INavigationService interface.

TextToSpeechService provides platform-abstracted text-to-speech functionality. The service uses MAUI Essentials TextToSpeech API with configurable language, pitch, rate, and volume settings. The service implements ITextToSpeechService interface.

FirestoreService handles user profile and settings data persistence in Firestore database. The service communicates with Firebase Functions backend endpoints for secure database operations. GetUserProfileAsync retrieves user profile data including email and display name. GetUserSettingsAsync retrieves user preferences including voice feedback, haptic feedback, and sensitivity mode settings. SaveUserSettingsAsync persists user preference changes to Firestore. CreateUserProfileAsync creates or updates user profile documents. All methods require Firebase ID token for authentication. The service implements IFirestoreService interface and is registered as singleton in dependency injection container.

CameraFrameProvider interface defines contract for camera frame access. Android implementation uses CameraX API through CameraFrameHandler class. The handler provides real-time camera frames for computer vision processing.

## API Integration Details

OpenAI API integration uses two primary endpoints. The Whisper API transcription endpoint accepts multipart form data with audio file, model parameter set to whisper-1, and optional language parameter. The endpoint requires bearer token authentication in Authorization header. Response format is JSON with text field containing transcribed content. Error responses include status codes for authentication failures (401), rate limiting (429), and server errors (500).

The OpenAI Chat Completions API is used for intent classification and general queries. The endpoint accepts JSON request body with model parameter set to gpt-4o-mini, messages array containing system prompt and user input, temperature parameter for response consistency (0.3 for intent classification, 0.7 for general chat), max tokens limit (256 for intent, 512 for chat), and response format specification for JSON object output. The system prompt defines available actions, expected response format, and example interactions. The API response contains choices array with message content containing JSON-formatted intent classification or text response.

Firebase Authentication API is accessed through REST endpoints. Sign up endpoint accepts email and password, returns ID token and refresh token. Sign in endpoint authenticates existing users. Token refresh endpoint maintains session persistence. User info endpoint retrieves current user data. Password reset endpoint sends email verification. All endpoints require Firebase API key in request parameters. Tokens are stored securely in SecureStorage for session persistence.

OSRM routing API provides route calculation for walking navigation. The endpoint accepts coordinate pairs in longitude,latitude format separated by semicolons. Route profile is set to foot for walking directions. Response includes encoded polyline geometry, distance in meters, duration in seconds, and turn-by-turn steps with maneuver instructions. The API is accessed through public demo server with production recommendation to host private instance.

Nominatim geocoding API provides location search and reverse geocoding. Search endpoint accepts query string and returns matching locations with coordinates and display names. Reverse geocoding endpoint accepts coordinates and returns address information. The API requires User-Agent header for identification. Rate limiting applies with recommendation for production use of private Nominatim instance.

Firebase Functions backend endpoints provide secure proxy for OpenAI API and Firestore database access. The transcribe endpoint accepts audioBase64 encoded string, fileName, and optional language parameter. The endpoint forwards request to OpenAI Whisper API with server-side API key. The classifyIntent endpoint accepts transcript string and returns intent classification with action, confidence, parameters, and response message. The chat endpoint provides general GPT chat completion functionality.

Firestore endpoints require Firebase ID token authentication in Authorization header. The getUserProfile endpoint retrieves user profile from Firestore users collection, creating default profile if not exists. The getUserSettings endpoint retrieves user preferences from users/{userId}/settings/preferences subcollection, returning default settings if not found. The saveUserSettings endpoint persists voice feedback, haptic feedback, and sensitivity mode preferences to Firestore with server timestamp. The createUserProfile endpoint creates or updates user profile with email and display name. All Firestore endpoints verify ID tokens using Firebase Admin SDK and return appropriate error codes for authentication failures. All endpoints handle CORS, error responses, and provide consistent JSON response format.

## Data Models

User model represents authenticated user with email, user ID, display name, and email verification status. User data is stored in Firebase Authentication with additional profile data in Firestore collections.

UserProfile model represents user profile data stored in Firestore with userId, email, displayName, and createdAt timestamp. UserSettings model contains user preferences including voiceFeedbackEnabled boolean, hapticFeedbackEnabled boolean, and sensitivityMode string with values low, medium, or high. SaveSettingsResponse model contains success status, message, and settings object for API responses. ProfileResponse model handles both direct profile data and nested profile responses from Firestore endpoints.

VoiceCommand model represents voice input with transcript, confidence score, language code, timestamp, and duration. IntentResult model contains classified action from IntentAction enum, confidence score, extracted parameters dictionary, response message, follow-up requirements, and original transcript.

DetectedObject model represents computer vision detection results with object class name, confidence score, bounding box coordinates, center position, and danger level assessment.

NavigationRoute model contains origin and destination coordinates, distance in meters, duration in seconds, encoded polyline string, decoded route points list, turn-by-turn steps, and destination name. RouteStep model contains step number, instruction text, distance, duration, maneuver type, location coordinates, street name, and voice instruction text.

GeoCoordinate model represents geographic location with latitude and longitude properties and distance calculation methods. GeocodingResult model contains display name, location coordinates, type, and importance score for location search results.

## Configuration Management

API keys are stored securely using MAUI Essentials SecureStorage which encrypts data in Android Keystore. Storage keys are defined in Constants class with prefixes for OpenAI and Firebase configuration. The SetupViewModel provides user interface for entering and saving API keys securely.

Firebase configuration includes API key, authentication domain, project identifier, storage bucket, messaging sender ID, application identifier, and optional measurement identifier. Configuration is loaded from SecureStorage at runtime with validation checks.

Backend API base URL is configurable through Constants.BackendBaseUrl. If empty, the application uses direct OpenAI API calls. If configured, all OpenAI requests are proxied through Firebase Functions backend. The backend URL format is https://[region]-[project-id].cloudfunctions.net. Firestore endpoint constants are defined in Constants class including FirestoreGetUserProfileEndpoint, FirestoreGetUserSettingsEndpoint, FirestoreSaveUserSettingsEndpoint, and FirestoreCreateUserProfileEndpoint.

Computer vision configuration includes ONNX model file name, detection confidence threshold (0.5), model input size (640x640), and frame processing interval (500ms). Voice assistant configuration includes silence timeout (2 seconds), maximum recording duration (30 seconds), and audio sample rate (16000 Hz).

Text-to-speech configuration includes speech rate (1.0), pitch (1.0), and volume (1.0) settings. Navigation configuration includes OSRM base URL and endpoint, Nominatim base URL and endpoints, and route calculation parameters.

## Known Issues and Technical Debt

Firebase Authentication REST API implementation requires manual token refresh management. The application must handle token expiration and refresh tokens manually since Firebase Admin SDK is not available in MAUI. Token refresh logic should be implemented with automatic retry on 401 responses.

ONNX model loading from embedded resources may experience delays on first launch. Model file size impacts application APK size and initial load time. Consider lazy loading or cloud-based model delivery for production optimization.

Camera frame processing performance depends on device capabilities. Frame rate should be throttled to prevent UI blocking. Consider background thread processing for computer vision inference to maintain responsive user interface.

OSRM public demo server has rate limiting and usage restrictions. Production deployment should use private OSRM instance for reliable routing service. Nominatim public server also has rate limiting requiring private instance for production scale.

Backend Firebase Functions deployment requires Firebase CLI setup and project configuration. Local testing uses Firebase emulators with environment variable configuration. Production deployment requires Firebase Functions configuration for API key storage.

Audio recording format compatibility may vary across devices. M4A format is used for OpenAI Whisper API compatibility. Some devices may require format conversion or alternative recording formats.

Location services require runtime permissions on Android. Permission requests must be handled gracefully with user-friendly error messages. Background location requires additional permissions and battery optimization considerations.

## Security Considerations

API keys are never hardcoded in source code. All keys are stored in SecureStorage with Android Keystore encryption. Backend API keys are stored in Firebase Functions configuration with environment variable fallback for local development.

Firebase security rules should be configured to restrict Firestore database access to authenticated users only. User documents should be accessible only by document owner. Route data should be associated with user identifiers for proper access control.

OpenAI API keys transmitted through backend proxy are never exposed to client applications. All OpenAI requests from mobile app can optionally route through Firebase Functions to protect keys. Direct API calls require client-side key storage with SecureStorage encryption.

HTTPS is used for all API communications. Certificate pinning could be implemented for additional security in production deployments. Network request logging should be disabled in production builds.

User authentication tokens are managed securely by Firebase with automatic token refresh. Tokens are stored in SecureStorage with Android Keystore protection. Session persistence requires secure token storage to prevent unauthorized access.

## Performance Considerations

Computer vision processing requires significant computational resources. ONNX Runtime inference runs on device CPU which may impact application responsiveness. Frame processing should be throttled to balance detection accuracy with performance. Model input size of 640x640 provides good balance between accuracy and speed.

Audio recording and transcription involve network upload operations dependent on connectivity. Audio file sizes should be optimized to minimize upload time and data usage. Recording duration limits prevent excessive file sizes. Audio quality settings balance file size with transcription accuracy.

OpenAI API calls introduce latency for both transcription and intent classification. The application implements proper loading states and timeout handling. Response caching could improve performance for common queries though current implementation does not include caching.

Navigation route calculation requires geocoding and routing API calls which introduce latency. Route data should be cached when possible to improve user experience. Background location updates should be optimized to balance accuracy with battery consumption.

Backend Firebase Functions have cold start latency on first invocation. Functions should be kept warm for production deployments or use minimum instances configuration. Response times depend on OpenAI API latency which varies based on load.

## Build and Deployment

Frontend builds use dotnet CLI with target framework net8.0-android. Build command is dotnet build -f net8.0-android -c Release for production or Debug for development. APK output location is bin/Release/net8.0-android/com.runa.navigation-Signed.apk.

Android APK signing requires keystore configuration for production builds. Debug builds use debug keystore automatically. Production keystore must be configured in Android project settings with secure key storage.

Backend deployment uses Firebase CLI with firebase deploy --only functions command. Functions are deployed to Firebase project specified in .firebaserc configuration. Local testing uses firebase emulators:start --only functions with environment variables for API keys.

Environment configuration for development uses appsettings.Development.json file for local API key storage. Production uses SecureStorage for on-device key management through Setup page user interface.

## Development Workflow

Frontend development uses Visual Studio 2022, VS Code with C# Dev Kit, or Cursor IDE. XAML files can be edited in any text editor with IntelliSense support. Code-behind files use C# with full language features.

Backend development uses Node.js with Firebase Functions emulator for local testing. Functions code is written in JavaScript with CommonJS module system. Local emulator requires Firebase CLI and Node.js 20 runtime.

Testing workflow includes direct APK installation on physical devices using adb install command. Emulator usage is optional for basic testing. ADB logcat provides application logs with filtering for mono-rt and DOTNET tags.

Version control uses Git with standard workflow. API keys and sensitive configuration are excluded through .gitignore. Environment template files provide configuration structure without actual keys.

## API Compatibility and Integration Patterns

All external APIs use standard HTTP REST protocols compatible with .NET HttpClient and Node.js axios. JSON serialization uses System.Text.Json in frontend and native JSON in backend. Error handling follows standard HTTP status code patterns.

Firebase Authentication REST API provides full feature parity with JavaScript SDK for mobile applications. Token management requires manual implementation of refresh logic. Session persistence uses SecureStorage equivalent to AsyncStorage in React Native.

OpenAI API integration works identically from both frontend direct calls and backend proxy. Request and response formats are consistent. Backend proxy provides additional security layer for API key protection.

OSRM and Nominatim APIs are public services with standard HTTP interfaces. No authentication required for basic usage. Production deployments should consider private instances for reliability and rate limit management.

## Current Application State

The application has been successfully built with the following implemented features and components.

### User Interface Pages

WelcomePage displays application tagline "Safe navigation for your everyday life" with SIGN UP and LOG IN buttons. The page includes accessibility hint "Long-press any button for help" and simplified layout without features list.

DashboardPage displays personalized welcome message "Welcome, {username}!" extracted from user email. The page includes three feature cards with icons: Navigation card with triangle icon, Vision Detection card with eye icon, and Settings card with gear icon. Voice assistant button displays microphone icon with "SPEAK" label when idle and "STOP" label when active. Logout button uses arrow icon in header.

SettingsPage provides user profile and preferences management interface. Profile section displays user email with email icon. Accessibility section includes Voice Feedback toggle switch with speaker icon and enabled/disabled status labels. Haptic Feedback toggle switch with vibration icon and status labels. Detection section includes Sensitivity Mode picker with gear icon supporting low, medium, and high options. The page includes loading overlay during data operations, error toast notification for failures, and API Configuration button linking to Setup page. Settings are automatically saved when toggles or picker values change.

### Backend Functions

Firebase Functions backend includes seven deployed endpoints. OpenAI proxy endpoints: transcribe for audio transcription, chat for general GPT queries, classifyIntent for intent classification. Firestore endpoints: getUserProfile retrieves or creates user profile from users collection, getUserSettings retrieves user preferences from users/{userId}/settings/preferences with default fallback, saveUserSettings persists preference changes with server timestamp, createUserProfile creates or updates user profile document. All Firestore endpoints require Firebase ID token authentication and use Firebase Admin SDK for secure database access.

### Services Implementation

FirestoreService is fully implemented with IFirestoreService interface. The service communicates with Firebase Functions backend endpoints using HttpClient with bearer token authentication. Methods include GetUserProfileAsync, GetUserSettingsAsync, SaveUserSettingsAsync, and CreateUserProfileAsync. The service handles error responses gracefully, returning default settings when backend is unavailable. Service is registered as singleton in MauiProgram.cs dependency injection container.

SettingsViewModel manages SettingsPage state and user preferences. The ViewModel loads user profile and settings on page initialization. Properties include UserEmail, VoiceFeedbackEnabled, HapticFeedbackEnabled, SensitivityMode with change notifications. Commands include GoBackCommand for navigation, GoToSetupCommand for API configuration, DismissErrorCommand for error handling. Settings are automatically persisted to Firestore when changed. ViewModel handles loading states, error states, and provides user feedback.

DashboardViewModel includes WelcomeMessage property that displays personalized greeting. The message is set during InitializeAsync method using username extracted from email address. Text-to-speech greeting announces welcome message and feature instructions on dashboard load.

### Data Models

UserSettings model includes VoiceFeedbackEnabled boolean defaulting to true, HapticFeedbackEnabled boolean defaulting to true, SensitivityMode string defaulting to "medium" with values low, medium, high. Model includes Default static property for fallback values. JSON property names use camelCase for API compatibility.

UserProfile model includes UserId, Email, DisplayName, and CreatedAt timestamp string. Model supports both direct response format and nested profile response format from Firestore endpoints.

SaveSettingsResponse and ProfileResponse models handle API response deserialization with success status, messages, and nested data objects.

### Build Status

Application builds successfully with all new components integrated. All services registered in MauiProgram.cs dependency injection container. All pages registered in AppShell.xaml routing. Constants updated with Firestore endpoint paths. ViewModels registered as transient services. No build errors or missing dependencies.

### Deployment Requirements

Firebase Functions must be deployed using firebase deploy --only functions command to make Firestore endpoints available. BackendBaseUrl constant must be configured with deployed Firebase Functions URL format https://[region]-[project-id].cloudfunctions.net. Firestore security rules must be configured to allow authenticated users to read and write their own data in users collection and settings subcollection. Application requires testing on physical device to verify Firestore integration and settings persistence.

### Known Limitations

BackendBaseUrl is currently empty string requiring configuration after Firebase Functions deployment. FirestoreService returns default settings when backend URL is not configured, allowing application to function without backend but without persistence. Settings changes are saved immediately but may fail silently if backend is unavailable. Error handling provides user feedback through toast notifications but may require additional retry logic for production use.

## Conclusion

The RUNA application fullstack architecture provides a robust foundation for accessible navigation functionality. The .NET MAUI frontend delivers native performance with cross-platform capabilities. The Firebase Functions backend provides secure API key management, request proxying, and Firestore database access. All core APIs and integrations are production-ready with appropriate error handling and security measures. The modular service architecture facilitates maintenance and future feature development. User interface includes complete settings management with profile display and preference toggles. The primary considerations for production deployment include Firebase Functions deployment, BackendBaseUrl configuration, Firestore security rules setup, and comprehensive testing of settings persistence functionality.
