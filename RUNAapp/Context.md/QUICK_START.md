# 🚀 RUNA Quick Start Guide

**Clear, step-by-step instructions with IDE recommendations for each step.**

---

## ⚠️ STEP 0: Fix the Build Error FIRST

You encountered this error:
```
error NETSDK1202: рабочая нагрузка "net8.0-android" не поддерживается
```

### ✅ RECOMMENDED FIX: Install .NET 8 SDK

**IDE: Web Browser (to download)**

1. **Go to:** https://dotnet.microsoft.com/download/dotnet/8.0
2. **Download:** ".NET 8 SDK" (Windows x64)
3. **Install:** Run the installer (takes 2 minutes)
4. **Verify:**
   ```powershell
   dotnet --list-sdks
   ```
   You should see both 8.0.x and 10.0.101

**Then change the project to use .NET 8:**

**IDE: Cursor (open RUNAapp.csproj file)**

1. Open `RUNAapp.csproj`
2. Find line 4 that says: `<TargetFrameworks>net10.0-android</TargetFrameworks>`
3. Change it to: `<TargetFrameworks>net8.0-android</TargetFrameworks>`
4. Save the file

---

## 📋 STEP 1: Restore Packages

**IDE: Terminal in Cursor (press `` Ctrl+` `` to open terminal)**

```powershell
cd C:\Users\IdeaPad\source\repos\RUNAapp\RUNAapp
dotnet restore
```

**What to expect:**
- Downloads all NuGet packages
- Takes 1-2 minutes
- Should see "Восстановление завершено" (Restore completed)

**If it fails:**
- Check internet connection
- Try: `dotnet nuget locals all --clear` then `dotnet restore` again

---

## 🏗️ STEP 2: Build the App

**IDE: Terminal in Cursor (same terminal as Step 1)**

```powershell
dotnet build -f net8.0-android -c Debug
```

**What to expect:**
- Compiles all code
- Takes 2-3 minutes (first time)
- Should see "Build succeeded"

**If build fails:**
- Read the error message - it tells you what's missing
- Common: Missing Android SDK → Install via Android Studio
- Common: Missing workload → Run `dotnet workload install maui-android`

---

## 📱 STEP 3: Connect Android Device

**IDE: Terminal (for ADB commands)**

### Option A: Physical Device (Recommended)

1. **On your phone:**
   - Settings → About Phone → Tap "Build Number" 7 times
   - Settings → Developer Options → Enable "USB Debugging"
   - Connect phone via USB

2. **In terminal:**
   ```powershell
   adb devices
   ```
   Should show your device (like: `ABC123XYZ    device`)

### Option B: Emulator

1. **Open Android Studio**
2. **Tools → Device Manager → Start emulator**
3. **In terminal:**
   ```powershell
   adb devices
   ```
   Should show emulator

---

## 📦 STEP 4: Install APK

**IDE: Terminal in Cursor**

```powershell
adb install -r bin\Debug\net8.0-android\com.runa.navigation-Signed.apk
```

**What to expect:**
- Installs app on device
- Should see "Success"

**If it fails:**
- Uninstall old version first: `adb uninstall com.runa.navigation`
- Then install again

---

## ⚙️ STEP 5: Configure API Keys

**IDE: Your Android Device (the app itself)**

1. **Open RUNA app** on your phone
2. **Find Setup page** (might be in menu or Welcome screen)
3. **Enter API keys:**

   **OpenAI:**
   - Field: "OpenAI API"
   - Value: Your OpenAI API key (starts with `sk-`)

   **Firebase:**
   - API Key: `AIzaSyBlVqEmldkJD9FOb3_pGuKmgGNLhLP43OA`
   - Auth Domain: `runa-e1ddb.firebaseapp.com`
   - Project ID: `runa-e1ddb`
   - Storage Bucket: `runa-e1ddb.firebasestorage.app`
   - Messaging Sender ID: `628120110180`
   - App ID: `1:628120110180:web:8e09a6793f8b181657cb71`
   - Measurement ID: `G-YYJQBFH7ER` (optional)

4. **Tap "Save Configuration"**

---

## 🧪 STEP 6: Test Basic Features

**IDE: Your Android Device**

### Test 1: Sign Up
1. Tap "Create Account"
2. Enter email + password
3. Should navigate to Dashboard

### Test 2: Voice Assistant
1. Tap the 🎤 microphone button
2. Say: "What can you do?"
3. Should hear a response

### Test 3: Navigation
1. Tap 🗺️ Navigate card
2. Search for "Central Park"
3. Tap a result
4. Should show route

---

## 📊 STEP 7: View Logs (If Something Breaks)

**IDE: Terminal in Cursor**

```powershell
# View all app logs
adb logcat -s mono-rt DOTNET

# View only errors
adb logcat -s mono-rt DOTNET *:E

# Save logs to file
adb logcat -s mono-rt DOTNET > logs.txt
```

---

## 🎯 IDE Quick Reference

| Task | Use This IDE | Why |
|------|--------------|-----|
| **Edit Code** | **Cursor** | AI assistance helps a lot |
| **Build/Restore** | **Terminal** (in Cursor) | Fastest, simple commands |
| **View Logs** | **Terminal** | `adb logcat` is standard |
| **Debug Code** | **Visual Studio 2022** | Best debugger (optional) |
| **Edit XAML** | **Cursor** | Works fine, or VS 2022 for visual designer |
| **Git** | **Cursor** | Built-in Git UI works well |

**My Recommendation:**
- **Use Cursor for everything** (code, terminal, Git)
- **Only use Visual Studio 2022** if you need the visual debugger

---

## ❓ Common Questions

**Q: Which IDE should I use?**
A: Cursor is perfect for this project. Use terminal in Cursor for builds.

**Q: Build failed - what do I do?**
A: Read the error message. It tells you exactly what's missing. Usually it's Android SDK or workloads.

**Q: App crashes on launch?**
A: Check logs with `adb logcat`. Usually missing API keys or permissions.

**Q: Can I use VS Code instead?**
A: Yes! VS Code works fine. Install C# extension and .NET MAUI extension.

---

## ✅ Success Checklist

- [ ] .NET 8 SDK installed
- [ ] Project builds successfully
- [ ] Device connected (shows in `adb devices`)
- [ ] APK installed on device
- [ ] API keys configured
- [ ] App launches without crashing
- [ ] Can sign up/create account
- [ ] Voice assistant responds
- [ ] Navigation search works

---

**You're all set! 🎉**

For detailed testing instructions, see `TESTING_GUIDE.md`
For build error solutions, see `BUILD_FIX.md`
