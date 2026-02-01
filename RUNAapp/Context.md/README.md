# RUNA - Accessible Navigation Assistant

<div align="center">

![RUNA Logo](Resources/Images/logo.png)

**Voice-powered navigation for the visually impaired**

[![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-8.0-blue)](https://docs.microsoft.com/dotnet/maui/)
[![Android](https://img.shields.io/badge/Android-10.0+-green)](https://developer.android.com/)
[![License](https://img.shields.io/badge/License-Proprietary-red)]()

</div>

---

## 🎯 Overview

RUNA is an accessibility-first navigation application designed for blind and visually impaired users. It combines:

- **🎤 Voice Commands** - Natural language interaction powered by OpenAI
- **👁️ Obstacle Detection** - Real-time computer vision using ONNX models
- **🗺️ Navigation** - Turn-by-turn directions with voice guidance
- **🔊 Audio Feedback** - Text-to-speech for all interactions

---

## 🚀 Getting Started

### Prerequisites

1. **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Android SDK** (API 29+) - Via Android Studio or standalone
3. **Visual Studio 2022** or **VS Code** with C# Dev Kit

### Clone & Build

```bash
# Clone the repository
git clone https://github.com/your-org/RUNAapp.git
cd RUNAapp

# Restore packages
dotnet restore

# Build for Android
dotnet build -f net8.0-android -c Release

# The APK will be at:
# RUNAapp/bin/Release/net8.0-android/com.runa.navigation-Signed.apk
```

---

## 🔑 API Key Setup

RUNA requires API keys for full functionality. Keys are stored securely on-device using encrypted storage.

### Step 1: Get Your API Keys

#### OpenAI API Key
1. Go to [OpenAI Platform](https://platform.openai.com/api-keys)
2. Create a new API key
3. Copy the key (starts with `sk-`)

#### Firebase Configuration
1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Create a new project or use existing
3. Go to Project Settings → General
4. Copy these values:
   - API Key
   - Auth Domain
   - Project ID
   - Storage Bucket
   - Messaging Sender ID
   - App ID
   - Measurement ID (optional)

### Step 2: Enter Keys in App

**Option A: In-App Setup (Recommended)**
1. Launch the app
2. On first run, go to Setup page
3. Enter your API keys
4. Tap "Save Configuration"

**Option B: Programmatic Setup (Development)**

Create a file `RUNAapp/appsettings.Development.json`:

```json
{
  "OpenAI": {
    "ApiKey": "sk-your-openai-api-key-here"
  },
  "Firebase": {
    "ApiKey": "your-firebase-api-key",
    "AuthDomain": "your-project.firebaseapp.com",
    "ProjectId": "your-project-id",
    "StorageBucket": "your-project.appspot.com",
    "MessagingSenderId": "123456789",
    "AppId": "1:123456789:android:abc123",
    "MeasurementId": "G-XXXXXXXXXX"
  }
}
```

Then load in code (see `Helpers/SecureStorageHelper.cs`).

---

## 📁 Project Structure

```
RUNAapp/
├── Models/                 # Data models
│   ├── User.cs            # User & auth models
│   ├── DetectedObject.cs  # CV detection models
│   ├── NavigationRoute.cs # Route & navigation models
│   └── VoiceCommand.cs    # Voice & intent models
│
├── Services/               # Business logic
│   ├── AuthService.cs     # Firebase authentication
│   ├── OpenAIService.cs   # Whisper & GPT integration
│   ├── NavigationService.cs # OSRM routing
│   ├── ComputerVisionService.cs # ONNX inference
│   ├── VoiceAssistantService.cs # Voice command orchestration
│   └── TextToSpeechService.cs   # TTS wrapper
│
├── ViewModels/             # MVVM ViewModels
│   ├── BaseViewModel.cs
│   ├── DashboardViewModel.cs
│   ├── NavigationViewModel.cs
│   └── VisionViewModel.cs
│
├── Views/                  # XAML Pages
│   ├── WelcomePage.xaml
│   ├── LoginPage.xaml
│   ├── DashboardPage.xaml
│   ├── NavigationPage.xaml
│   └── VisionPage.xaml
│
├── Helpers/                # Utilities
│   ├── Constants.cs       # App-wide constants
│   ├── SecureStorageHelper.cs # Secure key storage
│   └── Converters.cs      # XAML value converters
│
├── Resources/
│   ├── Styles/
│   │   ├── Colors.xaml    # RUNA brand colors
│   │   └── Styles.xaml    # App styles
│   ├── Raw/
│   │   └── yolov8n.onnx   # CV model (add manually)
│   └── Fonts/
│
└── Platforms/
    └── Android/
        └── AndroidManifest.xml # Permissions
```

---

## 🎨 Brand Colors

| Color | Hex | Usage |
|-------|-----|-------|
| Primary (Dark Blue) | `#051F45` | Headers, primary buttons |
| Secondary (Yellow) | `#F2AA3E` | Accents, highlights |
| Accent (Green) | `#057758` | Success states (minimal) |
| White | `#FFFFFF` | Backgrounds |

---

## 📱 Features

### Voice Assistant
- Tap microphone to start
- Say commands like:
  - "Take me to [destination]"
  - "What's around me?"
  - "Stop navigation"
  - "Help"

### Navigation
- OpenStreetMap integration
- OSRM routing (walking profile)
- Turn-by-turn voice guidance
- Distance and ETA display

### Obstacle Detection
- Offline ONNX model inference
- Detects: people, vehicles, animals, obstacles
- Danger level assessment
- Audio alerts for hazards

---

## 🔧 Development

### Adding the CV Model

1. Download YOLOv8 nano ONNX:
   ```bash
   # Option 1: From Ultralytics
   pip install ultralytics
   yolo export model=yolov8n.pt format=onnx
   
   # Option 2: Pre-converted from HuggingFace
   # https://huggingface.co/models?search=yolov8
   ```

2. Place `yolov8n.onnx` in `Resources/Raw/`

3. The model will be loaded at runtime

### Building for Debug

```bash
# Debug build with logging
dotnet build -f net8.0-android -c Debug

# Install to connected device
adb install -r bin/Debug/net8.0-android/com.runa.navigation.apk
```

### Viewing Logs

```bash
# Filter for app logs
adb logcat -s mono-rt DOTNET

# All logs with timestamp
adb logcat -v time
```

---

## 🛡️ Security

- API keys stored in Android Keystore via SecureStorage
- No keys transmitted to third parties
- Firebase security rules should restrict database access
- All API calls use HTTPS

---

## 🗺️ Roadmap

- [ ] iOS support
- [ ] Offline maps
- [ ] Custom voice wake word
- [ ] Route saving
- [ ] Social features (share routes)
- [ ] Multi-language support

---

## 📄 License

Proprietary - RUNA Startup © 2026

---

## 🤝 Support

For issues or questions, contact the development team.
