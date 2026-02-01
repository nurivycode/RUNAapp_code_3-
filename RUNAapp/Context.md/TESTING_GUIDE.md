# 🧪 RUNA App Testing Guide

Complete step-by-step guide to test the RUNA application from build to deployment.

---

## 📋 Prerequisites Checklist

Before testing, ensure you have:

- ✅ .NET 10 SDK installed (you have this - `dotnet --version` should show 10.0.101)
- ✅ Android SDK (API 29+) installed
- ✅ Android device connected OR Android emulator running
- ✅ USB Debugging enabled on Android device (if using physical device)
- ✅ API keys ready (OpenAI + Firebase)

---

## 🔧 Step 1: Fix Build Configuration

**IDE: Use Cursor or VS Code (both work for this)**

The error you encountered is because .NET 10 SDK requires `net10.0-android` instead of `net8.0-android`. I've already updated the `.csproj` file, but let's verify:

1. Open `RUNAapp.csproj` in Cursor/VS Code
2. Verify line 4 shows: `<TargetFrameworks>net10.0-android</TargetFrameworks>`
3. If not, it's already fixed by me - proceed to Step 2

---

## 🏗️ Step 2: Restore NuGet Packages

**IDE: Terminal in Cursor (Ctrl+`) or VS Code (Ctrl+`)**

```powershell
# Navigate to project folder
cd C:\Users\IdeaPad\source\repos\RUNAapp\RUNAapp

# Restore packages
dotnet restore
```

**Expected Output:**
```
Восстановление завершено (XX с)
```

**If you see errors:**
- Check your internet connection
- Try: `dotnet nuget locals all --clear` then `dotnet restore` again

---

## 🛠️ Step 3: Build the Application

**IDE: Terminal in Cursor or VS Code**

```powershell
# Build for Release (smaller APK, optimized)
dotnet build -f net10.0-android -c Release

# OR Build for Debug (faster, includes debug symbols)
dotnet build -f net10.0-android -c Debug
```

**Expected Output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**If Build Fails:**
- Check error messages - they'll tell you what's missing
- Common issues:
  - Missing Android SDK → Install via Android Studio
  - Missing workloads → Run `dotnet workload install maui-android`
  - Package conflicts → Check the error message for specific packages

---

## 📱 Step 4: Connect Your Android Device

**IDE: Terminal (for ADB commands)**

### Option A: Physical Android Device (Recommended for Testing)

1. **Enable Developer Options:**
   - Go to Settings → About Phone
   - Tap "Build Number" 7 times
   - You'll see "You are now a developer!"

2. **Enable USB Debugging:**
   - Settings → Developer Options
   - Enable "USB Debugging"
   - Enable "Install via USB" (if available)

3. **Connect via USB:**
   ```powershell
   # Check if device is detected
   adb devices
   ```
   
   **Expected Output:**
   ```
   List of devices attached
   ABC123XYZ    device
   ```
   
   If you see `unauthorized`:
   - Check your phone screen for "Allow USB debugging?" prompt
   - Tap "Allow" and check "Always allow from this computer"

### Option B: Android Emulator

1. **Open Android Studio**
2. **Tools → Device Manager**
3. **Create Virtual Device** (if needed)
   - Choose a device (e.g., Pixel 5)
   - Choose system image (API 29+)
   - Finish setup
4. **Start the emulator**
5. **Verify connection:**
   ```powershell
   adb devices
   ```

---

## 📦 Step 5: Install APK on Device

**IDE: Terminal in Cursor or VS Code**

```powershell
# For Debug build
adb install -r bin\Debug\net10.0-android\com.runa.navigation-Signed.apk

# OR For Release build
adb install -r bin\Release\net10.0-android\com.runa.navigation-Signed.apk
```

**Expected Output:**
```
Performing Streamed Install
Success
```

**If Installation Fails:**
- `INSTALL_FAILED_UPDATE_INCOMPATIBLE` → Uninstall old version first: `adb uninstall com.runa.navigation`
- `INSTALL_FAILED_INSUFFICIENT_STORAGE` → Free up space on device
- `INSTALL_FAILED_PERMISSION_DENIED` → Enable "Install via USB" in Developer Options

---

## 🚀 Step 6: Launch and Configure App

**IDE: Your Android Device/Emulator**

### 6.1 First Launch

1. **Open RUNA app** on your device
2. You'll see the **Welcome screen**
3. Navigate to **Setup** (if there's a menu) or the app will prompt you

### 6.2 Configure API Keys

**Go to Setup Page:**

1. **Enter OpenAI API Key:**
   - Field: "OpenAI API"
   - Value: `sk-your-openai-api-key-here`
   - Tap "Save Configuration"

2. **Enter Firebase Configuration:**
   - API Key: `AIzaSyBlVqEmldkJD9FOb3_pGuKmgGNLhLP43OA`
   - Auth Domain: `runa-e1ddb.firebaseapp.com`
   - Project ID: `runa-e1ddb`
   - Storage Bucket: `runa-e1ddb.firebasestorage.app`
   - Messaging Sender ID: `628120110180`
   - App ID: `1:628120110180:web:8e09a6793f8b181657cb71`
   - Measurement ID: `G-YYJQBFH7ER` (optional)

3. **Tap "Save Configuration"**

4. **Verify:** Status should show "Configuration saved successfully!"

---

## 🧪 Step 7: Testing Core Features

**IDE: Your Android Device/Emulator + Terminal (for logs)**

### Test 1: Authentication Flow

**Steps:**
1. From Welcome screen → Tap "Create Account"
2. Enter email: `test@example.com`
3. Enter password: `test1234` (min 6 characters)
4. Confirm password: `test1234`
5. Tap "Create Account"

**Expected Result:**
- Success message spoken
- Navigate to Dashboard
- Your email shown in header

**Verify Logs (Optional):**
```powershell
adb logcat -s mono-rt DOTNET | findstr /i "auth signup"
```

---

### Test 2: Voice Assistant

**Steps:**
1. From Dashboard → Tap the **🎤 microphone button** (bottom center)
2. When status says "Listening..." → Say: **"What can you do?"**
3. Wait for processing
4. You should hear a response

**Expected Result:**
- Button changes to ⏹️ (stop icon)
- Status shows "Processing..."
- After processing: Status shows "Speaking..."
- You hear the assistant's response

**Verify Logs:**
```powershell
adb logcat -s mono-rt DOTNET | findstr /i "voice whisper openai"
```

**Common Issues:**
- No microphone permission → App should request it automatically
- No response → Check OpenAI API key is correct
- Timeout → Check internet connection

---

### Test 3: Navigation

**Steps:**
1. From Dashboard → Tap **🗺️ Navigate** card
2. In search box, type: **"Central Park"** (or any location)
3. Tap search button 🔍
4. Tap on a result
5. Tap **"Start"** button

**Expected Result:**
- Search results appear
- Route calculated (distance/time shown)
- Navigation starts
- Voice instructions play

**Verify Logs:**
```powershell
adb logcat -s mono-rt DOTNET | findstr /i "navigation osrm route"
```

**Common Issues:**
- No location permission → App requests it automatically
- "Location unavailable" → Enable GPS on device
- No search results → Check internet connection

---

### Test 4: Obstacle Detection (Vision)

**Note:** This requires the ONNX model file. Without it, detection will be disabled.

**Steps:**
1. From Dashboard → Tap **👁️ Vision** card
2. Wait for "Ready" status
3. Tap **"Start"** button
4. Point camera forward
5. Walk around (or move camera)

**Expected Result:**
- Status changes to "ACTIVE"
- Detection count shows number of objects
- Audio alerts for dangerous objects

**Verify Logs:**
```powershell
adb logcat -s mono-rt DOTNET | findstr /i "vision detection onnx"
```

**If Model Missing:**
- Status shows "Model not available"
- Detection button is disabled
- This is expected if you haven't added the ONNX model yet

---

## 📊 Step 8: View Application Logs

**IDE: Terminal in Cursor or VS Code**

### Real-time Logs (Recommended)

```powershell
# All app logs
adb logcat -s mono-rt DOTNET

# Filter for errors only
adb logcat -s mono-rt DOTNET *:E

# Filter for specific feature
adb logcat -s mono-rt DOTNET | findstr /i "voice navigation vision"
```

### Save Logs to File

```powershell
# Save all logs
adb logcat > runa_logs.txt

# Save filtered logs
adb logcat -s mono-rt DOTNET > runa_app_logs.txt
```

### Clear Logs

```powershell
adb logcat -c
```

---

## 🔍 Step 9: Debugging Common Issues

### Issue: App Crashes on Launch

**Check:**
1. View crash logs:
   ```powershell
   adb logcat -s AndroidRuntime *:F
   ```
2. Look for exception stack trace
3. Common causes:
   - Missing API keys → Add keys in Setup
   - Missing permissions → Check AndroidManifest.xml
   - Missing dependencies → Check NuGet packages restored

**Fix:**
- Check `bin/Debug/net10.0-android/` for detailed error messages
- Review crash logs for specific error

---

### Issue: Voice Assistant Not Working

**Check:**
1. Verify OpenAI API key is set
2. Check microphone permission:
   ```powershell
   adb shell dumpsys package com.runa.navigation | findstr permission
   ```
3. Test internet connection

**Fix:**
- Re-enter OpenAI API key in Setup
- Grant microphone permission in Android Settings → Apps → RUNA → Permissions

---

### Issue: Navigation Not Working

**Check:**
1. Verify location permission granted
2. Check GPS is enabled on device
3. Check internet connection (needed for geocoding)

**Fix:**
- Enable location in Android Settings → Apps → RUNA → Permissions
- Enable GPS on device (Location Services)

---

### Issue: Vision Detection Not Working

**Check:**
1. Verify ONNX model file exists in `Resources/Raw/yolov8n.onnx`
2. Check camera permission granted
3. Check logs for model loading errors

**Fix:**
- Add the ONNX model file (see README.md)
- Grant camera permission in Android Settings

---

## 🎯 Step 10: Performance Testing

### Check Memory Usage

```powershell
adb shell dumpsys meminfo com.runa.navigation
```

### Check CPU Usage

```powershell
adb shell top -n 1 | findstr com.runa
```

### Check Battery Impact

1. Android Settings → Battery → Battery Usage
2. Check RUNA app usage
3. Monitor for excessive drain

---

## 📝 Step 11: Testing Checklist

Use this checklist to verify all features:

- [ ] App installs successfully
- [ ] Setup page accessible
- [ ] API keys save correctly
- [ ] Welcome screen displays
- [ ] Sign up creates account
- [ ] Sign in works
- [ ] Dashboard loads
- [ ] Voice assistant responds
- [ ] Navigation search works
- [ ] Route calculation works
- [ ] Vision detection starts (if model available)
- [ ] Text-to-speech works
- [ ] Haptic feedback works
- [ ] App doesn't crash
- [ ] No memory leaks (test for 10+ minutes)

---

## 🛠️ Quick Reference: Which IDE for What?

| Task | Recommended IDE | Why |
|------|----------------|-----|
| **Code Editing** | Cursor | Best for AI-assisted development |
| **Build/Restore** | Terminal (any) | Command-line is fastest |
| **Debugging Code** | Visual Studio 2022 | Best debugger, breakpoints |
| **View Logs** | Terminal (any) | `adb logcat` is standard |
| **XAML Design** | Visual Studio 2022 | Visual designer |
| **Git Operations** | VS Code or Cursor | Both have good Git UI |
| **NuGet Package Management** | Visual Studio 2022 | GUI package manager |

**My Recommendation:**
- **Use Cursor** for code writing (AI assistance)
- **Use Terminal** for build/install/logs
- **Use Visual Studio 2022** only if you need the visual debugger

---

## 🚨 Emergency: Reset Everything

If something goes wrong:

```powershell
# Uninstall app
adb uninstall com.runa.navigation

# Clear build artifacts
dotnet clean

# Clear NuGet cache
dotnet nuget locals all --clear

# Restore and rebuild
dotnet restore
dotnet build -f net10.0-android -c Debug

# Reinstall
adb install -r bin\Debug\net10.0-android\com.runa.navigation-Signed.apk
```

---

## ✅ Next Steps After Testing

Once testing is successful:

1. **Add ONNX Model** for full CV functionality
2. **Test with real users** (your target audience)
3. **Gather feedback** on UX
4. **Iterate** on features
5. **Prepare for production** (sign APK, optimize)

---

**Happy Testing! 🎉**
