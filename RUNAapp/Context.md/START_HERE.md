# 🎯 START HERE - RUNA App Setup

**Quick navigation for your current situation:**

---

## ❌ You Encountered This Error:

```
error NETSDK1202: рабочая нагрузка "net8.0-android" не поддерживается
```

**Translation:** ".NET 8.0-android workload is not supported"

**Reason:** You have .NET 10 SDK, but the project targets .NET 8.0

---

## ✅ SOLUTION: Choose One Option

### 🎯 OPTION 1: Install .NET 8 SDK (RECOMMENDED)

**Why:** Most stable, all packages tested, fewer issues

**Steps:**
1. **Download .NET 8 SDK:** https://dotnet.microsoft.com/download/dotnet/8.0
2. **Install it** (you can have both .NET 8 and .NET 10 installed)
3. **Change project file:**

   **IDE: Cursor (open `RUNAapp.csproj`)**
   
   - Find line 4: `<TargetFrameworks>net8.0-android</TargetFrameworks>`
   - Make sure it says `net8.0-android` (not `net10.0-android`)
   - Save file

4. **Build:**
   ```powershell
   dotnet restore
   dotnet build -f net8.0-android -c Debug
   ```

---

### ⚠️ OPTION 2: Use .NET 10 (Experimental)

**Why:** If you don't want to install .NET 8 SDK

**Steps:**
1. **Keep project file as-is** (already set to net10.0-android)
2. **Try building:**
   ```powershell
   dotnet restore
   dotnet build -f net10.0-android -c Debug
   ```
3. **If it fails:** Some packages might not support .NET 10 yet → Use Option 1

---

## 📚 Next Steps After Fixing Build

1. **Read:** `QUICK_START.md` - Step-by-step setup with IDE recommendations
2. **Read:** `TESTING_GUIDE.md` - Complete testing instructions
3. **Read:** `BUILD_FIX.md` - Detailed build error solutions

---

## 🛠️ IDE Recommendations

| Task | Use This |
|------|----------|
| **Edit Code** | **Cursor** (you're using it now - perfect!) |
| **Build/Restore** | **Terminal in Cursor** (press `` Ctrl+` ``) |
| **View Logs** | **Terminal in Cursor** |
| **Debug (optional)** | Visual Studio 2022 (only if you need breakpoints) |

**Bottom Line:** Use Cursor for everything. Terminal commands work the same everywhere.

---

## ✅ Quick Checklist

- [ ] Fixed build error (choose Option 1 or 2 above)
- [ ] `dotnet restore` succeeds
- [ ] `dotnet build` succeeds  
- [ ] Android device connected (`adb devices` shows device)
- [ ] APK installed on device
- [ ] API keys configured in app
- [ ] App launches successfully

---

**Need help? Check these files:**
- `QUICK_START.md` - Quick setup guide
- `TESTING_GUIDE.md` - Detailed testing steps
- `BUILD_FIX.md` - Build error solutions

**Ready to start?** → Open `QUICK_START.md` next! 🚀
