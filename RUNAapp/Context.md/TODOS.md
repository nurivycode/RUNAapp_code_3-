# RUNA Project TODOs

## Current Status (2026-02-01)

**SDK:** .NET 10.0.101, MAUI 8.0.91
**Target:** net10.0-android
**Backend:** Deployed at https://us-central1-runa-e1ddb.cloudfunctions.net

---

## Build & Deploy

```powershell
cd C:\Users\IdeaPad\work_A\RUNAapp\RUNAapp
dotnet build -f net10.0-android -c Debug
adb install -r bin\Debug\net10.0-android\com.runa.navigation-Signed.apk
```

---

## Testing Checklist

- [ ] App launches without crash
- [ ] Login/signup works (Firebase Auth)
- [ ] Dashboard displays correctly
- [ ] Voice assistant: tap mic, speak, get response
- [ ] Vision page: camera preview visible
- [ ] Vision page: start detection, see bounding boxes
- [ ] Settings: changes persist after restart

---

## What's Working

- Authentication (Firebase Auth REST API)
- Voice assistant (backend transcription + intent)
- Navigation (OSM/Nominatim + OSRM)
- Computer Vision (YOLOv8 ONNX model included)
- Camera Preview (CameraX with PreviewView)
- Settings persistence (Firestore via backend)
- Text-to-speech feedback
- UI/UX complete

---

## Known Limitations

- **OSM/OSRM:** Public servers are rate-limited; for production, host your own
- **Camera:** Android only (iOS not implemented)
- **Maps:** Text directions only, no visual map display yet

---

## Future Enhancements

- [ ] Maps display (Mapsui integration)
- [ ] Route saving
- [ ] iOS camera support
- [ ] Performance optimization for low-end devices
