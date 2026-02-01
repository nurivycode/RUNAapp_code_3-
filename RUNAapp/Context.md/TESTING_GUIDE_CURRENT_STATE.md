# Testing Guide - Current State of RUNA App

## Quick Summary: What Works Right Now

**What WORKS without backend:**
- Sign up / Sign in (Firebase Auth)
- Dashboard with welcome message
- Navigation page (OSRM routing)
- Vision detection page (ONNX model)
- Voice assistant (if OpenAI key is set)
- Settings page UI (but settings won't save without backend)

**What NEEDS backend (Firebase Functions):**
- Saving user settings to Firestore
- Loading user settings from Firestore
- User profile display (email will show, but won't load from Firestore)

**Current Status:**
- BackendBaseUrl is empty = app uses direct OpenAI calls (if key is set)
- Settings page will show default values but can't save/load from database
- Everything else should work fine

---

## Step-by-Step Testing Instructions

### STEP 1: Build the App

Open terminal in Cursor (press Ctrl+`) and run:

```powershell
cd C:\Users\IdeaPad\work_A\RUNAapp\RUNAapp
dotnet build -f net8.0-android -c Debug
```

**What to expect:**
- Should see "Build succeeded"
- Takes 2-3 minutes

**If it fails:**
- Check if you have .NET 8 SDK: `dotnet --list-sdks`
- If missing, install from: https://dotnet.microsoft.com/download/dotnet/8.0

---

### STEP 2: Connect Your Android Phone

1. **On your phone:**
   - Settings → About Phone → Tap "Build Number" 7 times
   - Settings → Developer Options → Enable "USB Debugging"
   - Connect phone via USB

2. **In terminal, check connection:**
   ```powershell
   adb devices
   ```
   Should show: `ABC123XYZ    device` (your device ID)

---

### STEP 3: Install the App

```powershell
adb install -r bin\Debug\net8.0-android\com.runa.navigation-Signed.apk
```

**What to expect:**
- Should see "Success"
- App appears on your phone

**If it fails:**
```powershell
adb uninstall com.runa.navigation
adb install -r bin\Debug\net8.0-android\com.runa.navigation-Signed.apk
```

---

### STEP 4: Configure API Keys (First Time Only)

1. **Open RUNA app on your phone**
2. **Go to Setup page** (from Welcome screen or menu)
3. **Enter these values:**

   **OpenAI API Key:**
   - Get from: https://platform.openai.com/api-keys
   - Paste your key (starts with `sk-`)

   **Firebase Configuration:**
   - API Key: `AIzaSyBlVqEmldkJD9FOb3_pGuKmgGNLhLP43OA`
   - Auth Domain: `runa-e1ddb.firebaseapp.com`
   - Project ID: `runa-e1ddb`
   - Storage Bucket: `runa-e1ddb.firebasestorage.app`
   - Messaging Sender ID: `628120110180`
   - App ID: `1:628120110180:web:8e09a6793f8b181657cb71`
   - Measurement ID: `G-YYJQBFH7ER` (optional)

4. **Tap "Save Configuration"**

---

### STEP 5: Test What Works

#### Test 1: Sign Up / Sign In
1. Tap "SIGN UP" or "LOG IN"
2. Enter email and password
3. Should navigate to Dashboard
4. Should see "Welcome, {your-email-username}!"

**Expected:** ✅ Works - uses Firebase Auth directly

---

#### Test 2: Dashboard
1. Should see welcome message with your username
2. Should see 3 cards:
   - Navigation (triangle icon)
   - Vision Detection (eye icon)
   - Settings (gear icon)
3. Should see microphone button with "SPEAK" label

**Expected:** ✅ Works - all UI elements display correctly

---

#### Test 3: Settings Page
1. Tap Settings card
2. Should see:
   - Your email displayed
   - Voice Feedback toggle (default: ON)
   - Haptic Feedback toggle (default: ON)
   - Sensitivity Mode picker (default: medium)
3. Try toggling switches or changing sensitivity

**What happens:**
- ✅ UI works - toggles change
- ⚠️ Settings won't save to database (backend not configured)
- ⚠️ Settings reset to defaults when you close/reopen app

**Why:** BackendBaseUrl is empty, so FirestoreService can't connect to backend

---

#### Test 4: Navigation Page
1. Tap Navigation card
2. Search for a location (e.g., "Central Park")
3. Tap a result
4. Should show route on map

**Expected:** ✅ Works - uses OSRM public API directly

---

#### Test 5: Vision Detection Page
1. Tap Vision Detection card
2. Grant camera permission
3. Should see camera preview
4. Should detect objects (people, cars, etc.)

**Expected:** ✅ Works - uses local ONNX model

---

#### Test 6: Voice Assistant
1. Tap microphone button on Dashboard
2. Say something (e.g., "What can you do?")
3. Wait for response

**Expected:** ✅ Works if OpenAI key is set - uses direct OpenAI API

**If it fails:**
- Check if OpenAI API key is configured in Setup
- Check logs (see Step 6 below)

---

### STEP 6: View Logs (If Something Breaks)

Open a new terminal and run:

```powershell
# See all app logs
adb logcat -s mono-rt DOTNET

# See only errors
adb logcat -s mono-rt DOTNET *:E

# Save logs to file
adb logcat -s mono-rt DOTNET > logs.txt
```

**What to look for:**
- Errors about "Backend URL not configured" = normal (backend not set up)
- Errors about "OpenAI API key" = need to set key in Setup
- Errors about "Firestore" = backend not configured (expected)

---

## Understanding the Endpoints (Simple Explanation)

### What is an Endpoint?
An endpoint is like a function on a server that your app can call. Think of it like calling a phone number to get information.

### Current Setup:

**Your App (Frontend)** → Calls → **Backend Server (Firebase Functions)** → Calls → **Firestore Database**

But right now, the connection is broken because BackendBaseUrl is empty.

---

### The 7 Endpoints Explained:

#### 1. `/transcribe` - Converts speech to text
- **What it does:** Takes audio recording, sends to OpenAI Whisper, returns text
- **Status:** ✅ Works (uses direct OpenAI if backend not set)

#### 2. `/chat` - General AI chat
- **What it does:** Sends message to GPT, gets response
- **Status:** ✅ Works (uses direct OpenAI if backend not set)

#### 3. `/classifyIntent` - Understands voice commands
- **What it does:** Takes your voice command, figures out what you want (navigate, detect, etc.)
- **Status:** ✅ Works (uses direct OpenAI if backend not set)

#### 4. `/getUserProfile` - Gets your profile info
- **What it does:** Reads your email, display name from Firestore database
- **Status:** ❌ Doesn't work (needs backend URL configured)
- **What happens now:** Returns null, app shows email from Firebase Auth instead

#### 5. `/getUserSettings` - Gets your preferences
- **What it does:** Reads your settings (voice feedback, haptic, sensitivity) from Firestore
- **Status:** ❌ Doesn't work (needs backend URL configured)
- **What happens now:** Returns default settings (all ON, medium sensitivity)

#### 6. `/saveUserSettings` - Saves your preferences
- **What it does:** Saves your settings to Firestore database
- **Status:** ❌ Doesn't work (needs backend URL configured)
- **What happens now:** Settings change in UI but don't save, reset when app closes

#### 7. `/createUserProfile` - Creates your profile
- **What it does:** Creates/updates your profile in Firestore
- **Status:** ❌ Doesn't work (needs backend URL configured)

---

## What You'll See in Logs

When you open Settings page, you'll see in logs:

```
GetUserProfile response: Backend URL not configured
GetUserSettings response: Backend URL not configured, returning default settings
```

**This is NORMAL** - it means the app is working but can't connect to backend.

When you change settings:

```
SaveUserSettings response: Backend URL not configured
```

**This is NORMAL** - settings won't persist.

---

## How to Make Settings Work (Future Step)

To make settings save/load work, you need to:

1. **Deploy Firebase Functions:**
   ```powershell
   cd C:\Users\IdeaPad\work_A\RUNAapp\functions
   firebase deploy --only functions
   ```

2. **Get the backend URL:**
   - After deployment, Firebase will show: `https://us-central1-runa-e1ddb.cloudfunctions.net`
   - Copy this URL

3. **Set it in code:**
   - Open `RUNAapp/Helpers/Constants.cs`
   - Find line 46: `public const string BackendBaseUrl = "";`
   - Change to: `public const string BackendBaseUrl = "https://us-central1-runa-e1ddb.cloudfunctions.net";`
   - Rebuild and reinstall app

4. **Then settings will save/load!**

---

## Quick Test Checklist

- [ ] App builds successfully
- [ ] App installs on phone
- [ ] Can sign up / sign in
- [ ] Dashboard shows welcome message
- [ ] Settings page shows email
- [ ] Settings toggles work (but don't save)
- [ ] Navigation page works
- [ ] Vision detection works
- [ ] Voice assistant works (if OpenAI key set)
- [ ] Logs show "Backend URL not configured" (normal)

---

## Summary

**Right now, your app is 90% functional:**
- ✅ Authentication works
- ✅ Navigation works
- ✅ Vision detection works
- ✅ Voice assistant works (with OpenAI key)
- ✅ Settings UI works
- ❌ Settings persistence doesn't work (needs backend)

**The app works fine for testing everything except settings persistence. The backend is optional for most features, but required for saving user preferences.**
