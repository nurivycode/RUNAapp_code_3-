# RUNA Application Technical Specification for .NET MAUI Migration

## Executive Summary

This document provides a comprehensive technical specification for migrating the RUNA navigation application for blind users from React Native Expo to .NET MAUI. The application provides voice-controlled navigation, real-time computer vision obstacle detection, and AI-powered intent classification. This specification documents the complete technical stack, architectural patterns, known issues, API integrations, and migration requirements.

## Application Overview

RUNA is a navigation application designed specifically for blind and visually impaired users. The application uses voice commands for navigation, real-time camera-based obstacle detection, and AI-powered intent understanding. The application operates on Android platforms with future iOS support planned. The core functionality centers around accessibility features including text-to-speech feedback, haptic responses, and voice-controlled interaction patterns.

## Technical Stack - Current Implementation

The application is built using React Native 0.81.5 with Expo framework version 54.0.30. The development language is TypeScript. The application architecture follows a service-oriented pattern with separate service modules for authentication, voice processing, computer vision, navigation, and AI integration.

Authentication is handled through Firebase Authentication using the Firebase JavaScript SDK version 12.7.0. User sessions are managed through Zustand state management library version 4.5.7. The authentication store implements Firebase-compatible patterns with onAuthStateChanged listeners for session persistence.

Database operations use Firestore from the Firebase SDK. All Firebase configuration is loaded from environment variables through Expo Constants, with production builds using EAS secrets for secure key management. The Firebase configuration includes API key, authentication domain, project identifier, storage bucket, messaging sender identifier, application identifier, and optional measurement identifier for analytics.

AI capabilities are implemented through OpenAI API integration. The application uses OpenAI Whisper API for speech-to-text transcription, sending recorded audio files to the transcription endpoint. Intent classification uses OpenAI GPT-4o-mini model through the chat completions API endpoint. The OpenAI integration uses standard HTTP fetch requests with bearer token authentication. API keys are managed through environment variables and EAS secrets in production builds.

Computer vision functionality uses TensorFlow.js version 4.22.0 with the COCO-SSD object detection model version 2.2.3. The model is loaded from CDN at runtime with timeout and retry logic for production reliability. Object detection targets specific classes including person, car, truck, bus, motorcycle, bicycle, dog, and cat. Detected objects are analyzed for danger assessment using OpenAI API integration. The computer vision service processes camera frames, converts them to TensorFlow tensors, runs inference, filters results by confidence threshold and screen position, and generates audio alerts through text-to-speech.

Media capabilities include camera access through expo-camera version 17.0.10, audio recording through expo-av version 16.0.8, and text-to-speech through expo-speech version 14.0.8. Camera permissions are requested at runtime, and the camera component provides frame capture functionality for computer vision processing. Audio recording is used for speech recognition, capturing audio in M4A format and uploading to OpenAI Whisper API. Text-to-speech provides audio feedback for all user interactions with configurable language, pitch, and rate settings.

Navigation functionality uses expo-router version 6.0.21 for screen routing and navigation. The application structure follows a grouped routing pattern with authentication routes, main application routes, and nested navigation routes. Maps integration uses react-native-maps version 1.20.1 for displaying routes and locations. Location services use expo-location version 19.0.8 for GPS positioning with fine location permissions and background location capabilities.

User interface components use react-native-paper version 5.14.5 as the primary component library. The application implements accessibility features including AccessibleButton and AccessibleCard components with proper accessibility labels and hints. Haptic feedback is provided through expo-haptics version 15.0.8 for tactile responses to user actions. Gesture handling uses react-native-gesture-handler version 2.28.0.

Build and deployment use Expo Application Services EAS Build for cloud-based Android APK generation. Build profiles include development, preview, and production configurations. Environment variables are injected through EAS secrets for production builds. The build process handles Android APK generation with proper signing and optimization.

## Service Architecture

The application follows a modular service architecture pattern. The authentication service provides sign up, sign in, sign out, password reset, and current user retrieval functions. All authentication operations use Firebase Authentication SDK with comprehensive error handling and user-friendly error messages. The service exports functions for integration with React components.

The voice assistant service orchestrates voice command processing workflow. The service manages speech recognition initiation, audio capture, transcription through OpenAI Whisper API, intent classification through OpenAI GPT API, command execution, and response generation. The service maintains internal state for active session tracking and timeout management. Callbacks are provided for transcript updates, processing status, response text, completion events, and error handling.

Speech recognition service handles audio recording and transcription. The service uses expo-av for audio recording in M4A format. Recorded audio is uploaded to OpenAI Whisper API transcription endpoint with proper form data encoding. The service supports language parameter configuration, currently set to English for debugging purposes. Error handling includes specific messages for authentication failures, rate limiting, and server errors.

Computer vision service manages TensorFlow.js model initialization, object detection, danger analysis, and alert generation. Model loading implements timeout protection with sixty-second limit and retry logic with three attempts. The service filters detected objects by confidence threshold, target class membership, and screen position. Danger analysis uses OpenAI API to assess object threat level with urgency classification. Alert generation combines object information with danger analysis to create user notifications.

Navigation service handles route calculation, destination geocoding, and navigation state management. The service integrates with mapping APIs for route generation. Route builder functionality allows users to create navigation routes to destinations. Route history and saved routes are stored in Firestore database. Active navigation tracking provides real-time location updates and route guidance.

Profile service manages user profile data stored in Firestore. User preferences, settings, and application configuration are stored in user document collections. The service provides functions for reading and updating user profile information.

## API Integration Details

OpenAI API integration uses two endpoints. The Whisper API transcription endpoint accepts multipart form data with audio file, model parameter set to whisper-1, and language parameter. The endpoint requires bearer token authentication in the Authorization header. Response format is JSON with text field containing transcribed content. Error responses include status codes for authentication failures, rate limiting, and server errors.

The OpenAI Chat Completions API is used for intent classification. The endpoint accepts JSON request body with model parameter set to gpt-4o-mini, messages array containing system prompt and user input, temperature parameter for response consistency, max tokens limit, and response format specification for JSON object output. The system prompt defines available actions, expected response format, and example interactions. The API response contains choices array with message content containing JSON-formatted intent classification.

Firebase Authentication API is accessed through the Firebase JavaScript SDK. The SDK provides methods for creating user accounts with email and password, signing in with credentials, signing out, sending password reset emails, sending email verification, and accessing current authenticated user. The SDK handles token management, session persistence through AsyncStorage, and automatic token refresh. Error codes are mapped to user-friendly error messages.

Firestore database operations use the Firebase JavaScript SDK Firestore module. The SDK provides document and collection references, query capabilities, real-time listeners, and batch operations. User data, routes, and application state are stored in Firestore collections. Security rules should be configured to protect user data. The SDK handles offline persistence and synchronization automatically.

## Environment Configuration

Development environment uses dotenv package for loading environment variables from .env file. The .env file contains OpenAI API key, Firebase API key, Firebase authentication domain, Firebase project identifier, Firebase storage bucket, Firebase messaging sender identifier, Firebase application identifier, and optional Firebase measurement identifier. The file must be UTF-8 encoded for proper parsing.

Production builds use EAS secrets for environment variable management. Secrets are configured through EAS CLI with scope set to project level. Secrets are injected into build process and accessible through Expo Constants extra configuration. The app.config.js file loads environment variables from process.env and makes them available through Constants.expoConfig.extra object.

Firebase configuration validation occurs at application startup. Required fields are checked, and missing configuration triggers error logging. The validation does not throw errors to prevent application crashes, instead logging warnings for debugging purposes.

## Known Issues and Technical Debt

Authentication flow experiences crashes on login in production APK builds. The issue persists even after reverting to original authentication code using getCurrentUser pattern. This suggests the problem is not in application code logic but rather in build configuration, dependency linking, or native module integration. The crash occurs immediately after successful login when navigating to main application route. Root cause analysis indicates potential issues with Firebase native SDK initialization, Expo Router navigation timing, or EAS build cache problems.

Voice assistant service contained dead code with missing import statement. The stopSpeaking function attempted to use Speech module from expo-speech without importing the module, which would cause runtime crashes. This function was removed as TTS functionality is handled through useTTS hook in component layer. The service architecture correctly delegates TTS to component level, but legacy code remained.

Computer vision model loading experiences timeout issues in some production builds. The TensorFlow.js model download from CDN can fail due to network conditions, large file size, or CDN availability. The service implements retry logic with three attempts and exponential backoff, but timeout duration may need adjustment for slower network conditions. Model initialization timeout is set to thirty seconds, and model loading timeout is set to sixty seconds.

Image tensor conversion from camera frames presents challenges in React Native TensorFlow.js environment. The tf.browser.decodeImage function is not available in React Native context, requiring alternative image decoding approaches. Current implementation attempts multiple fallback methods for image tensor creation, but this area requires further optimization for production reliability.

Speech recognition occasionally produces duplicate transcription results. This was resolved by ensuring single callback invocation and proper cleanup of recognition sessions. The issue was related to multiple event listeners being registered or improper session management.

OpenAI API integration experienced intermittent failures with error responses not being properly handled. Error handling was improved with specific status code checks for authentication failures, rate limiting, and server errors. Response format specification was added to ensure consistent JSON parsing.

Text-to-speech output was configured for Russian language initially, causing issues with English debugging. Language configuration was updated to English for development and debugging purposes. The useTTS hook provides language configuration through expo-speech options.

Application state persistence through Firebase Authentication requires proper listener setup. Initial implementation used synchronous getCurrentUser check which fails to wait for Firebase to restore authentication state from AsyncStorage. The application should use onAuthStateChanged listener to properly handle authentication state restoration after application restart.

Production APK builds do not display console logs in standard logcat output. React Native logs use ReactNativeJS tag, requiring specific logcat filtering. Application crashes in production do not provide visible error messages without proper crash reporting integration. Firebase Crashlytics integration would provide better production debugging capabilities.

## User Interface Patterns

The application implements accessibility-first design patterns. All interactive elements include accessibility labels and hints for screen reader compatibility. Text-to-speech provides audio feedback for all user actions and system responses. Haptic feedback provides tactile confirmation for button presses and important events.

Navigation flow follows authentication gate pattern. Unauthenticated users are redirected to welcome screen with sign up and sign in options. Authenticated users access main application dashboard. The dashboard provides access to navigation features, vision detection, and settings. Voice assistant is accessible through floating microphone button on dashboard.

Voice interaction pattern uses modal overlay for voice assistant interface. The modal displays transcription text, processing status indicators, and response messages. Visual feedback includes animated microphone icon during listening state. The interface is designed for accessibility with large touch targets and clear visual hierarchy.

Camera interface for computer vision provides live preview with detection overlay. Detected objects are highlighted with bounding boxes and labels. The interface includes toggle controls for starting and stopping detection. Detection alerts are provided through audio announcements and haptic feedback.

## Data Models and State Management

User authentication state is managed through Zustand store with Firebase integration. The store maintains current user object, loading state, and error state. Authentication state changes are observed through Firebase onAuthStateChanged listener. The store is initialized at module load time, which may cause timing issues with Firebase initialization.

Navigation state includes current route, destination, waypoints, and active navigation status. Route data is stored in Firestore with user association. Route history maintains timestamped entries for recent navigation sessions. Saved routes allow users to store frequently used destinations.

User profile data includes email address, display preferences, accessibility settings, and application configuration. Profile data is stored in Firestore user document. The profile service provides read and update operations with proper error handling.

Computer vision state tracks active detection status, current detections, and alert history. Detection results are processed in real-time with debouncing to prevent alert spam. Alert history maintains recent detections to avoid duplicate notifications.

## Security Considerations

API keys are stored securely using EAS secrets for production builds and environment variables for development. Keys are never committed to version control. The .gitignore file excludes .env files to prevent accidental exposure.

Firebase security rules should be configured to restrict database access to authenticated users only. User documents should be accessible only by the document owner. Route data should be associated with user identifiers for proper access control.

OpenAI API keys are transmitted securely over HTTPS. Audio files uploaded to Whisper API are processed according to OpenAI data usage policies. User voice data should be handled in compliance with privacy regulations.

Authentication tokens are managed securely by Firebase SDK with automatic token refresh. Tokens are stored in secure storage on device. Session persistence uses AsyncStorage which provides reasonable security for mobile applications.

## Performance Considerations

Computer vision processing requires significant computational resources. TensorFlow.js model inference runs on CPU in React Native environment, which may impact application responsiveness. Model loading from CDN requires network connectivity and adequate bandwidth. Detection frame rate should be throttled to balance accuracy and performance.

Audio recording and transcription involve file upload operations that depend on network connectivity. File sizes should be optimized to minimize upload time and data usage. Audio quality settings balance file size with transcription accuracy requirements.

OpenAI API calls introduce latency for both transcription and intent classification. The application implements proper loading states and timeout handling. Response caching could improve performance for common queries, though current implementation does not include caching.

Navigation route calculation may require geocoding API calls which introduce latency. Route data should be cached when possible to improve user experience. Background location updates should be optimized to balance accuracy with battery consumption.

## Testing and Quality Assurance

Production APK testing is performed through direct installation on physical devices. EAS Build generates APK files that are downloaded and installed manually. USB debugging and ADB logcat can be used for debugging, though React Native logs require specific filtering.

Unit testing framework is not currently implemented. Service functions could benefit from unit tests for critical business logic. Integration testing would validate API integration and error handling paths.

Manual testing workflow includes authentication flow validation, voice command processing, computer vision detection, and navigation functionality. Regression testing should be performed after code changes to ensure existing functionality remains intact.

Error logging and monitoring in production is limited. Integration of crash reporting service such as Firebase Crashlytics would improve production debugging capabilities. Application analytics could provide insights into usage patterns and error rates.

## Migration Requirements for .NET MAUI

The migration to .NET MAUI requires mapping all React Native components and services to MAUI equivalents. The development language changes from TypeScript to C#. Framework changes from React Native to .NET MAUI with XAML or C# markup for user interface definition.

Firebase integration migrates from JavaScript SDK to .NET SDK. The Firebase.Auth and Firebase.Firestore NuGet packages provide equivalent functionality. Configuration moves from environment variables to appsettings.json or secure storage mechanisms. Authentication patterns use async/await patterns native to C#.

OpenAI API integration uses HttpClient class from .NET framework instead of fetch API. HTTP request patterns are similar but use C# async/await syntax. JSON serialization uses System.Text.Json or Newtonsoft.Json. Error handling follows C# exception patterns.

Computer vision requires model format conversion. TensorFlow.js models must be converted to TensorFlow Lite format or ONNX format for .NET MAUI compatibility. TensorFlow Lite .NET bindings or ONNX Runtime .NET provide inference capabilities. Model conversion tools are available but require additional setup and configuration.

Camera access uses MAUI MediaElement or platform-specific camera APIs. The MAUI framework provides camera integration patterns. Image processing uses System.Drawing or platform-specific image manipulation libraries.

Navigation uses MAUI Shell navigation patterns instead of expo-router. Route definitions use Shell navigation URI patterns. Navigation state management uses MAUI navigation service and dependency injection patterns.

Maps integration uses MAUI Maps control or Google Maps SDK for .NET MAUI. The MAUI Maps control provides cross-platform mapping capabilities. Google Maps integration requires platform-specific configuration.

Text-to-speech uses platform-specific TTS APIs through MAUI platform abstraction. Android uses Android.Speech.Tts APIs. iOS uses AVSpeechSynthesizer. The implementation requires platform-specific code or community libraries.

Audio recording uses platform-specific APIs through MAUI platform abstraction. Android uses MediaRecorder APIs. iOS uses AVAudioRecorder. Community libraries may provide cross-platform abstractions.

Build and deployment uses dotnet CLI for local builds or CI/CD pipelines. APK generation uses Android build tools through .NET MAUI build process. Code signing requires Android keystore configuration. Deployment to physical devices uses ADB install or direct APK installation.

State management can use MAUI dependency injection with view models, community state management libraries, or custom state management patterns. The MVVM pattern is common in MAUI applications.

User interface migration requires converting React components to MAUI pages and views. React Native Paper components map to MAUI controls or custom implementations. Accessibility features use MAUI accessibility properties and platform accessibility APIs.

## API Compatibility Verification

All API integrations are compatible with .NET MAUI. OpenAI API uses standard HTTP REST endpoints which work with HttpClient. Firebase provides official .NET SDK with full feature parity. Computer vision requires model format conversion but inference is possible through TensorFlow Lite or ONNX Runtime.

API keys remain the same across platforms. Storage mechanism changes from environment variables to appsettings.json or secure storage, but keys themselves are identical. Authentication flows and API authentication patterns remain consistent.

Network requests use standard HTTP protocols compatible with .NET HttpClient. JSON serialization is native to .NET framework. Error handling patterns adapt to C# exception model but API error responses remain the same.

## Development Workflow Considerations

Current development workflow uses Expo Go for rapid iteration and EAS Build for production APKs. MAUI development workflow uses Visual Studio or VS Code with .NET MAUI extensions. Local builds are faster than cloud builds but require Android SDK and build tools installation.

Testing workflow remains similar with direct APK installation on physical devices. Emulator usage is optional and not required for basic testing. ADB can be used for log access and debugging, though MAUI logs use standard .NET logging patterns.

Version control and code management remain consistent. Git workflow and branching strategies can be maintained. Code review processes and collaboration patterns adapt to C# codebase but principles remain the same.

Deployment pipeline can use GitHub Actions, Azure DevOps, or other CI/CD platforms with .NET MAUI build steps. APK signing and distribution follow standard Android deployment patterns. App store distribution requires standard Android app submission process.

## Conclusion

The RUNA application is architected with clear service boundaries and modular design patterns that facilitate migration to .NET MAUI. All core APIs and integrations are compatible with .NET MAUI platform. The primary migration challenges involve UI framework differences, model format conversion for computer vision, and development tooling changes. The technical foundation supports successful migration with appropriate planning and execution