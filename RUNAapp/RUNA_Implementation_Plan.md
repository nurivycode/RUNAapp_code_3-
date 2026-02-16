# RUNA: Technical Implementation Plan

## Accessibility Navigation App for Visually Impaired Users — Astana, Kazakhstan

**Version:** 1.0
**Date:** February 2026
**Target:** .NET MAUI Android, 3-4GB RAM devices, 1-hour battery life, 100 users year one
**Test Deployment:** 3-4 days, 1 test user walking real streets

---

# PART 1: SYSTEM COMPONENTS

---

## 1.1 Device CV (Computer Vision)

### A. Computational Profile

**Model:** YOLOv8n ONNX, 416x416 input, COCO 80 classes

| Metric | BEFORE Optimization | AFTER Optimization | Method |
|--------|---------------------|-------------------|--------|
| **Inference CPU time** | 180-220ms/frame (CPU, 4 threads, Snap 665) | 80-110ms/frame (NNAPI delegate) | NNAPI execution provider, cached compilation |
| **CPU sustained load** | 45% across 4 cores at 2fps | 15-20% across 4 cores at 2fps | NNAPI offloads to DSP/GPU; adaptive frame rate |
| **Peak RAM (per frame)** | ~10MB (new Bitmap + byte[] + tensor per frame) | ~3.5MB (pooled buffers) | Pre-allocated bitmap pool, reuse input tensor array |
| **Sustained RAM** | 180-250MB (model + leaked allocations) | 75-90MB (model + fixed pools) | Fix memory leaks, single OnnxRuntime session |
| **Model RAM** | 12-15MB (fp32 ONNX) | 12-15MB (fp32) or 3.5MB (int8) | INT8 quantization optional if accuracy holds |
| **Battery draw** | ~250mW at 2fps (CPU) + frame capture overhead | ~100-120mW at 2fps (NNAPI) + adaptive | NNAPI + drop to 0.5fps when stationary |
| **Battery % per hour** | ~6-8% (CV only, CPU mode) | ~2.5-3.5% (CV only, NNAPI+adaptive) | Measured against 4000mAh typical battery |
| **Network** | 0 KB (on-device) | 0 KB normal; ~50-80KB/frame in fallback mode | JPEG compressed frame upload only in fallback |

**Frame Rate Strategy:**

| User State | Frame Rate | Detection Cycle | Battery Impact |
|------------|-----------|----------------|----------------|
| Walking (GPS speed > 0.5 m/s) | 2 fps | 500ms between frames | Baseline |
| Stationary (GPS speed < 0.5 m/s for 5s) | 0.5 fps | 2000ms between frames | 75% reduction |
| Thermal throttle (>43C) | 0 fps (offload) | Backend handles at 0.5fps | ~0% device CV |
| Battery saver (<25%) | 0 fps (offload) | Backend handles at 0.5fps | ~0% device CV |

### B. Data Structures

**Input Pipeline:**

```
CameraX ImageProxy (YUV_420_888)
    Size: 416x416x1.5 = 260 KB (set via ImageAnalysis.Builder.setTargetResolution)
    Source: CameraX ImageAnalysis use case, rear camera

→ YUV-to-RGB Conversion
    Method: libyuv via JNI (CameraX .toBitmap() as fallback)
    Cost: 2-5ms (libyuv) or 8-15ms (.toBitmap())
    Output: ARGB_8888 Bitmap, 416x416x4 = 693 KB

→ Bitmap-to-Float-Tensor Normalization
    Method: Pre-allocated float[] buffer, pixel-by-pixel RGB extraction
    Cost: 3-5ms
    Output: float[1, 3, 416, 416] = 2.08 MB (fp32)

→ OnnxRuntime InferenceSession.Run()
    Cost: 80-110ms (NNAPI) or 180-220ms (CPU)
    Output: float[1, 84, 8400] = 2.82 MB (raw predictions)

→ NMS (Non-Maximum Suppression)
    Method: Custom C# NMS on output tensor
    Cost: 1-3ms (typically <50 post-NMS detections)
    Output: List<Detection> {classId, confidence, x, y, w, h}
    Size: ~2 KB (typically 5-20 detections after filtering)

→ Danger Filter
    Method: Apply tier classification + center-60% + distance zones
    Cost: <1ms
    Output: List<DangerAlert> {class, tier, direction, zone, ttsText}
    Size: <1 KB
```

**Memory Pooling Strategy:**

| Buffer | Size | Lifetime | Reuse Pattern |
|--------|------|----------|---------------|
| `_rgbBitmapPool` | 693 KB | App lifetime | Single instance, overwritten each frame |
| `_inputTensorPool` | 2.08 MB | App lifetime | Single float[], refilled each frame |
| `_outputTensorPool` | 2.82 MB | App lifetime | Single float[], overwritten by ONNX runtime |
| `_nmsResultPool` | 4 KB (50 detections max) | App lifetime | Clear-and-reuse list |

Total pre-allocated pool: **5.6 MB fixed** vs current behavior of **~10 MB allocated and GC'd per frame**.

### C. Threading Model

| Operation | Thread | Justification |
|-----------|--------|---------------|
| CameraX frame delivery | CameraX executor (dedicated thread) | CameraX owns this; never block it |
| YUV→RGB conversion | ThreadPool (Task.Run) | CPU-bound, must not block camera |
| Tensor normalization | Same ThreadPool task | Sequential with conversion |
| ONNX inference | Same ThreadPool task | OnnxRuntime is thread-safe per-session |
| NMS + danger filter | Same ThreadPool task | <5ms, keeps results on same thread |
| TTS dispatch | UI thread (MainThread.BeginInvokeOnMainThread) | Android TTS requires main thread init |
| Vibration dispatch | UI thread | Vibrator service requires Context |

**Synchronization:**

| Primitive | Purpose | Location |
|-----------|---------|----------|
| `SemaphoreSlim(1,1)` | Ensure only one frame processes at a time | Entry of ProcessFrame |
| `Interlocked.Exchange` | Swap latest frame reference without lock | Camera callback → processing handoff |
| `CancellationTokenSource` | Cancel in-flight inference on app pause/stop | Linked to lifecycle events |
| `volatile bool _isProcessing` | Skip frames if previous still processing | Camera callback guard |

**Race Condition Risks:**

| Risk | Scenario | Mitigation |
|------|----------|------------|
| Double-process same frame | Camera delivers frame while previous still in ONNX | `volatile _isProcessing` flag + `SemaphoreSlim` |
| TTS queue corruption | Multiple danger alerts dispatched to TTS simultaneously | `QUEUE_FLUSH` mode for critical alerts; serial queue for warnings |
| Bitmap recycling during use | GC collects bitmap while ONNX reads it | Pool is held by static reference; never eligible for GC |
| Session disposal during inference | App lifecycle pause triggers Dispose while Run() active | `CancellationToken` + lock around session disposal |

### D. Failure Modes

| Failure | Impact | Fallback Behavior | Recovery |
|---------|--------|-------------------|----------|
| ONNX inference throws exception | No detections for that frame | Log error, skip frame, retry next frame. After 3 consecutive failures: restart OnnxRuntime session. After 5: switch to backend CV fallback. | Auto-recover on next successful inference |
| NNAPI compilation fails | Falls back to CPU (slower) | OnnxRuntime auto-falls back to CPU ExecutionProvider. Log warning. No user notification needed — still functional. | Restart app to retry NNAPI |
| Camera permission denied | No frames at all | TTS: "Camera permission required for obstacle detection." Navigation-only mode. | Prompt permission re-request on next app open |
| Camera hardware failure | No frames | TTS: "Camera unavailable." Navigation-only mode with audio warnings. | Detect via CameraX error callback; retry after 10s |
| OOM during inference | App crash | Pre-flight check: if `Runtime.getRuntime().freeMemory() < 20MB`, skip frame and force GC. | Reduce frame rate; if persistent, switch to backend fallback |
| Thermal throttle (>43C) | Device may kill background processes | Proactive: stop CV at 43C, start backend fallback. TTS: "Device warm, switching to cloud detection." | Monitor temp every 10s; resume on-device when <40C (3C hysteresis) |

### E. Testability

**Isolation Testing:**

| Test Type | Method | Success Criteria |
|-----------|--------|-----------------|
| Inference accuracy | Feed 100 pre-captured Astana street images through pipeline | mAP@0.5 > 0.35 on COCO-relevant classes; 0 missed cars at <10m |
| Inference speed | Benchmark 50 frames, measure p50/p95 latency | p50 < 150ms (NNAPI), p95 < 200ms |
| Memory stability | Run 1000 frames, monitor heap via `Debug.GetTotalMemory()` | No sustained growth >5MB over 1000 frames |
| Pooling correctness | Verify buffer contents don't leak between frames | Hash of empty regions stays zero after processing |
| Danger filter | Unit test with synthetic Detection lists | 100% of CRITICAL class detections produce alerts; 0 alerts from IRRELEVANT classes |

**Mock Points:**

| Interface | Mock For | Purpose |
|-----------|----------|---------|
| `ICameraFrameSource` | Replace CameraX with pre-recorded frames | Test without camera hardware |
| `IInferenceEngine` | Replace OnnxRuntime with deterministic outputs | Test NMS + danger filter logic |
| `IAlertDispatcher` | Record TTS/vibration calls without hardware | Verify alert content and timing |
| `IThermalMonitor` | Inject fake temperature readings | Test offload trigger logic |

---

## 1.2 Device Navigation

### A. Computational Profile

| Metric | BEFORE Optimization | AFTER Optimization | Method |
|--------|---------------------|-------------------|--------|
| **GPS polling CPU** | Continuous listener (~3% CPU) | 5-second interval polling (~0.5% CPU) | `requestLocationUpdates` with 5000ms minInterval |
| **Route calculation CPU** | 0% device (server-side OSRM) | 0% device | Stays server-side |
| **Route guidance CPU** | ~2% (continuous distance checks) | ~1% (event-driven at GPS updates) | Calculate next instruction only on GPS tick |
| **Peak RAM** | ~15MB (full GeoJSON route in memory) | ~3MB (compressed polyline + current/next 3 steps) | Store only active route segment |
| **Sustained RAM** | ~15MB | ~3MB | Discard passed route segments |
| **Battery (GPS)** | ~4-5% per hour (continuous high-accuracy) | ~2.5-3% per hour (5s interval, balanced accuracy) | `PRIORITY_BALANCED_POWER_ACCURACY` |
| **Network per route request** | 15-50KB response (uncompressed) | 4-12KB (gzip compressed) | Accept-Encoding: gzip |
| **Network frequency** | 1 request per route start + recalculation | Same | No change needed |

**GPS Configuration:**

| Parameter | Value | Justification |
|-----------|-------|---------------|
| Provider | Fused Location Provider (Google Play Services) | Best accuracy/power tradeoff |
| Min interval | 5000ms | Walking speed (~1.4 m/s) = 7m between updates; sufficient for 100m turn announcements |
| Min displacement | 3 meters | Avoid spurious updates when stationary |
| Accuracy priority | BALANCED_POWER_ACCURACY | ~40m accuracy is sufficient for street-level navigation |
| Timeout per fix | 10000ms | If no fix in 10s, use last known location |

### B. Data Structures

**Route Data Model:**

```
OSRM Route Response (from server):
    Size: 4-12 KB (gzip)
    Format: JSON with encoded polyline geometry

→ Parse to RouteModel:
    - List<RouteStep> steps (8-20 for 2km urban route)
    - Each step: {instruction, distance_m, bearing, streetName, geometry[]}
    - Total parsed size: ~3 KB in memory

→ Active Navigation State:
    - currentStepIndex: int
    - distanceToNextTurn: float (meters)
    - currentBearing: float (degrees)
    - isOffRoute: bool (>30m from nearest route point)
    - lastAnnouncedDistance: int (tracks which distance markers spoken)
```

**Turn Announcement Logic:**

| Distance to Turn | Action | TTS Text |
|-----------------|--------|----------|
| 100m | First announcement | "In 100 meters, turn {direction}" |
| 50m | Reminder | "In 50 meters, turn {direction}" |
| 15m | Final prompt | "Turn {direction} now" |
| Passed turn point | Confirm or recalculate | "You should have turned. Recalculate?" |
| Off-route (>30m) | Prompt user | "You seem off route. Recalculate?" |

### C. Threading Model

| Operation | Thread | Justification |
|-----------|--------|---------------|
| GPS updates | Fused Location callback (main looper) | Android API requirement |
| Distance/bearing calculation | GPS callback thread (inline, <1ms) | Trivial math, no thread switch needed |
| Route request (HTTP) | ThreadPool (async/await) | Network I/O |
| Turn announcement decision | GPS callback thread (inline) | Simple comparison |
| TTS dispatch | UI thread (post from GPS callback) | TTS service binding |

**Synchronization:**

| Primitive | Purpose |
|-----------|---------|
| `lock(_routeStateLock)` | Protect `_currentStepIndex` and `_isOffRoute` from concurrent GPS update + recalculation |
| `volatile bool _isRecalculating` | Prevent multiple simultaneous recalculation requests |

### D. Failure Modes

| Failure | Impact | Fallback Behavior | Recovery |
|---------|--------|-------------------|----------|
| GPS signal lost | No location updates | TTS: "GPS signal lost." Continue with last known position for 30s. After 30s: "Still no GPS. Stop and wait, or continue last direction?" | Resume on next GPS fix; no recalculation unless >30m drift |
| OSRM server unreachable | Cannot calculate new routes | TTS: "Network lost." (max 2 times). Continue with last cached route. Navigation-only affected. | Retry on next user-initiated route request or voice command |
| OSRM rate limited (429) | Route request fails | Wait 5s, retry once. If still 429: TTS: "Route service busy, try again in a minute." | Exponential backoff: 5s, 15s, 45s |
| Route response parse failure | Corrupt route data | Log error. TTS: "Could not understand route. Try again?" | User re-requests via voice |
| Off-route detection false positive | Unnecessary recalculation prompt | 30m threshold + require 3 consecutive off-route GPS fixes before prompting | Auto-cancels if next GPS fix is on-route |

### E. Testability

| Test Type | Method | Success Criteria |
|-----------|--------|-----------------|
| Turn announcement timing | Feed synthetic GPS coordinates along known route | All turns announced at 100m, 50m, 15m (+-10m tolerance) |
| Off-route detection | Feed GPS coordinates 35m from route | Off-route prompt triggers after 3 consecutive fixes (15s) |
| Route parsing | Feed 10 real OSRM responses from Astana area | All parsed without exception; step count matches expected |
| Network failure resilience | Kill network mid-navigation | TTS announces network loss; navigation continues with cached route |
| Battery impact | 30-minute GPS session with profiler | <2% battery drain from GPS alone |

**Mock Points:**

| Interface | Mock For |
|-----------|----------|
| `ILocationProvider` | Inject synthetic GPS coordinates for testing routes |
| `IRouteService` | Return cached OSRM responses without network |
| `INavigationAnnouncer` | Record all TTS calls for assertion |

---

## 1.3 Device Voice

### A. Computational Profile

| Metric | BEFORE Optimization | AFTER Optimization | Method |
|--------|---------------------|-------------------|--------|
| **Mic capture CPU** | ~2% during recording | ~2% during recording | No change needed (hardware codec) |
| **Audio encoding CPU** | ~5% (WAV raw) | ~3% (OGG Opus compression) | Opus codec for smaller upload |
| **TTS playback CPU** | ~5-15% (Google TTS synthesis) | ~5-15% | No change needed; runs in separate process |
| **TTS latency** | 300-800ms (cold engine) | 50-150ms (warm engine) | Initialize TTS engine at app start; keep warm |
| **Peak RAM (recording)** | ~5MB (raw audio buffer) | ~2MB (streaming compression) | Stream-compress to Opus during recording |
| **Network (upload)** | 160KB per 5s clip (WAV) | 15-20KB per 5s clip (OGG Opus) | Opus at 16kHz mono, 24kbps |
| **Network (Whisper API)** | ~20KB upload + 1KB response | ~20KB upload + 1KB response | No change |
| **Whisper API latency** | 1.5-3.0s for 5s clip | 1.0-2.0s (pre-warmed endpoint) | Cloud Run min-instance keeps connection warm |
| **Battery per voice command** | ~0.1% per command cycle | ~0.08% | Marginal; dominated by network radio |

**TTS Priority System:**

| Priority | Use Case | Queue Behavior | Example |
|----------|----------|---------------|---------|
| CRITICAL | Danger alert | `QUEUE_FLUSH` — interrupts all speech | "Car ahead, 3 meters" |
| HIGH | Turn announcement | `QUEUE_FLUSH` if no critical pending | "In 100 meters, turn left" |
| NORMAL | Voice command response | `QUEUE_ADD` | "Starting navigation to Khan Shatyr" |
| LOW | Status update | `QUEUE_ADD`, skip if queue >2 items | "Network restored" |

### B. Data Structures

**Voice Command Pipeline:**

```
Microphone Input (Android AudioRecord)
    Format: PCM 16-bit, 16kHz, mono
    Buffer: 3200 bytes per 100ms chunk
    Duration: Record until silence detected (max 10s) or button release

→ Silence Detection (on-device)
    Method: RMS amplitude < threshold for 1.5s
    Cost: <1ms per chunk
    Output: Complete audio segment

→ OGG Opus Compression (on-device)
    Method: OpusEncoder via Concentus (.NET Opus library)
    Cost: ~3ms per 100ms chunk (real-time capable)
    Output: OGG Opus file, ~15-20KB for 5s clip

→ Upload to Voice API (network)
    Endpoint: POST /api/voice/transcribe
    Latency: 200-400ms upload (Central Asia to GCP)
    Size: 15-20KB

→ Whisper Transcription (server)
    Cost: 1.0-2.0s server processing
    Output: { "text": "Navigate to Khan Shatyr", "language": "ru" }

→ Intent Classification (server)
    Method: Deterministic pattern matching first, GPT-4o-mini fallback
    Cost: <5ms (deterministic) or 300-500ms (GPT fallback)
    Output: { "intent": "navigate", "destination": "Khan Shatyr" }

→ Response to Device
    Size: <1KB JSON
    Total voice command round-trip: 2.0-4.5s
```

**Intent Classification Hierarchy:**

| Priority | Method | Patterns | Latency | Cost |
|----------|--------|----------|---------|------|
| 1st | Regex/keyword matching | "navigate to X", "stop", "repeat", "where am I", "recalculate" | <5ms | $0 |
| 2nd | GPT-4o-mini classification | Ambiguous utterances not matching patterns | 300-500ms | $0.00015/request |
| 3rd | Fallback | Unclassifiable → "didn't understand, try again" | 0ms | $0 |

**Known Intents:**

| Intent | Trigger Phrases (RU/EN) | Action |
|--------|------------------------|--------|
| `navigate` | "navigate to...", "go to...", "route to..." | Geocode destination → calculate route → start navigation |
| `stop` | "stop", "cancel", "enough" | Stop navigation, confirm with vibration |
| `repeat` | "repeat", "what did you say", "again" | Re-speak last TTS message |
| `where_am_i` | "where am I", "current location" | Geocode current GPS → speak address |
| `recalculate` | "recalculate", "new route", "yes" (after off-route prompt) | Recalculate route from current position |
| `help` | "help", "what can I do" | Speak available commands list |

### C. Threading Model

| Operation | Thread |
|-----------|--------|
| AudioRecord capture | Dedicated audio thread (Android requirement) |
| Silence detection | Audio thread (inline, <1ms) |
| Opus encoding | Audio thread (inline, real-time capable) |
| HTTP upload | ThreadPool (async) |
| TTS speech | UI thread (TTS engine bound to main looper) |
| Button press handler | UI thread |

**Synchronization:**

| Primitive | Purpose |
|-----------|---------|
| `SemaphoreSlim(1,1)` | Prevent overlapping voice commands (one at a time) |
| `CancellationTokenSource` | Cancel in-flight transcription if user presses stop |
| `volatile bool _isListening` | Guard against double-start of AudioRecord |

### D. Failure Modes

| Failure | Impact | Fallback Behavior | Recovery |
|---------|--------|-------------------|----------|
| Whisper API timeout | No transcription result | TTS: "Didn't understand, please try again." Log timeout. | User retries voice command |
| Whisper returns gibberish | Wrong intent | Deterministic classifier rejects unknown patterns → "Didn't understand." | User retries |
| Network loss during upload | Command lost | TTS: "Network lost." (max 2 times). Voice commands disabled until network returns. CV stays active. | Auto-retry when connectivity restored (Connectivity.NetworkAccess check) |
| Microphone permission denied | No voice input | TTS: "Microphone permission needed for voice commands." Fall back to button-only mode. | Re-prompt permission on next app open |
| TTS engine not initialized | No speech output | Fallback: Vibration patterns only for critical alerts. Log error. | Retry TTS init every 30s |
| Kazakh language not available in TTS | Garbled or missing speech | Detect via `isLanguageAvailable(Locale("kk","KZ"))`. Fall back to Russian TTS. | Check for updated TTS data on each app start |

### E. Testability

| Test Type | Method | Success Criteria |
|-----------|--------|-----------------|
| Intent classification accuracy | Feed 50 pre-recorded audio clips through pipeline | >90% correct intent classification |
| Deterministic classifier coverage | Unit test with 100 phrase variations | >80% of common phrases handled without GPT fallback |
| TTS interrupt latency | Measure time from CRITICAL alert to TTS `onStart` | <200ms from danger detection to first audio |
| Voice round-trip time | End-to-end measurement from button press to TTS response | p50 < 3.5s, p95 < 5s |
| Silence detection | Feed audio with known silence gaps | Detection within 200ms of actual silence |

**Mock Points:**

| Interface | Mock For |
|-----------|----------|
| `IAudioRecorder` | Inject pre-recorded audio bytes |
| `ITranscriptionService` | Return deterministic transcription results |
| `IIntentClassifier` | Return specific intents for testing downstream actions |
| `ITtsEngine` | Record speech calls without audio output |

---

## 1.4 Backend Services

### A. Computational Profile

**Per-Service Breakdown:**

| Service | CPU per Request | RAM | Latency Budget | Requests/min (1 user) |
|---------|----------------|-----|----------------|----------------------|
| Navigation API | 50-100ms (route proxy + cache) | 128MB | 500ms total | 0.2 (one per route start + recalc) |
| Voice API | 100-200ms (Whisper proxy + intent) | 256MB | 3000ms total | 1-2 (during active voice use) |
| CV Fallback API | 200-500ms (inference on server) | 512MB-1GB | 1000ms total | 30 (during fallback at 0.5fps) |
| Geocoding API | 50ms (Nominatim proxy + cache) | 128MB | 300ms total | 0.1 (per destination search) |

**Monthly Compute Cost (Testing Phase: 1 user, 2hr/day):**

| Service | Requests/day | vCPU-seconds/day | Memory GB-seconds/day | Cost/month |
|---------|-------------|-------------------|----------------------|------------|
| Navigation API | 5 | 0.5 | 0.06 | ~$0.01 |
| Voice API | 60 | 12 | 3.0 | ~$0.15 |
| CV Fallback API | 0 (normal) / 3600 (fallback) | 0 / 1800 | 0 / 900 | ~$0 / ~$2.50 |
| Geocoding API | 5 | 0.25 | 0.03 | ~$0.01 |
| **Min instance (1 always-on)** | — | — | — | **~$25-40** |
| **OpenAI API** | 60 Whisper + 12 GPT | — | — | **~$1.50** |
| **Cloud SQL (PostgreSQL)** | — | — | — | **~$10** |
| **Total testing phase** | | | | **~$37-55/month** |

### B. Data Structures

See Part 4 for detailed endpoint schemas and database schema.

### C. Threading Model

Cloud Run services are stateless and handle concurrency via container instances. No custom threading required.

| Service | Concurrency Setting | Justification |
|---------|-------------------|---------------|
| Navigation API | 80 concurrent requests per instance | Lightweight proxy |
| Voice API | 10 concurrent requests per instance | Each holds Whisper API connection |
| CV Fallback API | 1 concurrent request per instance | GPU memory per inference |

### D. Failure Modes

| Failure | Impact | Fallback Behavior | Recovery |
|---------|--------|-------------------|----------|
| Cloud Run cold start | 1.5-3.5s delay on first request | Min-instance=1 eliminates for primary service. Accept cold start for CV Fallback (rare). | Pre-warm via Cloud Scheduler ping every 10min |
| OpenAI API down | No transcription or GPT intent | Deterministic intent classifier handles common patterns. TTS: "Voice service temporarily unavailable." | Retry with exponential backoff; alert developer via Cloud Monitoring |
| OSRM demo server down | No route calculation | TTS: "Route service unavailable." Cache last successful route. | Retry every 30s; long-term: migrate to self-hosted Valhalla |
| Cloud SQL down | No session/user data persistence | App continues with in-memory state. Routes still calculated. Logs buffered locally. | Cloud SQL HA replica (future); for now, accept brief outage |
| Cloud Run out of budget | All backend services stop | Device continues with on-device CV only. No voice, no new routes. TTS: "Cloud services paused." | Budget alert at 80% threshold via Cloud Billing |

### E. Testability

| Test Type | Method | Success Criteria |
|-----------|--------|-----------------|
| Navigation API end-to-end | `curl` with Astana coordinates | Valid OSRM route returned in <1s |
| Voice API classification | POST 10 pre-recorded audio clips | All correctly classified |
| CV Fallback API latency | POST test JPEG frame | Detection results returned in <1.5s |
| Cold start measurement | Deploy fresh revision, measure first request | <3s for Python, <500ms for Go |
| Cost monitoring | Run simulated 2-hour session, check billing | Within $50-100/month budget |

---

## 1.5 Database (Cloud SQL PostgreSQL)

### A. Computational Profile

| Metric | Value | Notes |
|--------|-------|-------|
| Instance type | db-f1-micro (shared vCPU, 614MB RAM) | Sufficient for <100 users |
| Storage | 10GB SSD | Minimal; route data is transient |
| IOPS | 300 read, 300 write (burstable) | More than enough |
| CPU per query | <5ms for indexed lookups | All queries use indexes |
| Connection overhead | 20-50ms per new connection | Use connection pooling (5 connections) |
| Monthly cost | ~$7-10 | db-f1-micro pricing |

### B. Data Structures

See Part 4: Database Schema section for complete schema definition.

**Query Patterns:**

| Query | Frequency | Expected Time | Index Used |
|-------|-----------|--------------|------------|
| Get user preferences by device_id | On app start (1/session) | <5ms | `idx_users_device_id` |
| Insert navigation session | On route start (1-5/hour) | <5ms | Primary key |
| Insert route step log | On each turn (10-20/route) | <3ms | Primary key |
| Insert crash/error log | On error (rare) | <5ms | Primary key |
| Get user's recent routes | On "where was I going" (rare) | <10ms | `idx_sessions_user_device` |

### C. Threading Model

Cloud SQL handles connection pooling internally. Backend services use a connection pool of 5 connections per instance.

### D. Failure Modes

| Failure | Impact | Fallback Behavior | Recovery |
|---------|--------|-------------------|----------|
| Connection timeout | Cannot read user preferences | Use default preferences. Log error. | Retry on next API call |
| Database full (10GB) | Cannot insert new data | Automatic cleanup: delete sessions older than 30 days | Cloud alert + manual cleanup |
| Instance restart | 30-60s downtime | Backend services retry connections with exponential backoff | Automatic PostgreSQL restart |

### E. Testability

| Test Type | Method | Success Criteria |
|-----------|--------|-----------------|
| Schema validity | Run migration script on empty database | All tables created without errors |
| Query performance | `EXPLAIN ANALYZE` on all expected queries | All queries <10ms |
| Connection pooling | 10 concurrent requests to backend | No connection exhaustion errors |
| Data retention | Insert old records, run cleanup job | Records >30 days deleted correctly |

---

# PART 2: DATA FLOWS

---

## 2.1 Button Press → Voice Command

```
User presses button (short press)
    → UI Thread: Detect press type (0ms)
    → [DECISION: press duration]
        → < 2 seconds (short press): Voice Command Flow
            → UI Thread: Vibration feedback (50ms pulse) (0ms dispatch)
            → UI Thread: TTS QUEUE_FLUSH → "Listening" (50-150ms)
            → Audio Thread: Start AudioRecord (16kHz, mono) (10ms)
            → Audio Thread: Stream audio chunks to Opus encoder (real-time)
            → Audio Thread: Silence detection per chunk (<1ms each)
            → [DECISION: silence > 1.5s OR button released OR 10s max]
                → Stop recording
                → Audio Thread: Finalize OGG Opus file (2ms)
                    → Size: 15-20KB for 5s clip
            → ThreadPool: POST /api/voice/transcribe (15-20KB upload)
                → Network latency: 200-400ms (Astana→GCP)
                → Whisper processing: 1.0-2.0s
                → Intent classification: <5ms (deterministic) or 300-500ms (GPT)
                → Response: {intent, parameters, responseText} (<1KB)
                → Total server time: 1.3-2.5s
            → ThreadPool: Execute intent action
                → [DECISION: intent type]
                    → "navigate": POST /api/navigation/route → parse route → start nav
                    → "stop": Cancel navigation → vibration confirm
                    → "repeat": Re-speak last TTS message
                    → "where_am_i": Reverse geocode current GPS → speak address
                    → "recalculate": Recalculate from current position
                    → UNKNOWN: TTS "Didn't understand"
            → UI Thread: TTS speak response (50-150ms to first audio)
        → >= 2 seconds (long press): Stop Navigation
            → UI Thread: Vibration feedback (200ms pulse) at 2s mark (0ms dispatch)
            → UI Thread: Cancel active navigation (0ms)
            → UI Thread: TTS "Navigation stopped" (50-150ms)
            → No network call needed

Total short-press round-trip: 2.5-5.0s (button → response speech)
Total long-press round-trip: 2.0s (button hold → confirmation vibration)
```

**Failure Injection Points:**

| Point | What If | Behavior |
|-------|---------|----------|
| Network drops during upload | OGG file upload fails | TTS: "Network lost." Retry once after 3s. If still failed, discard. |
| Whisper returns empty text | Silent recording or background noise | TTS: "Didn't understand, please try again." |
| Intent execution fails (e.g., geocode fails) | Destination not found | TTS: "Couldn't find that location. Please try again with more detail." |
| TTS engine crashes | No audible response | Vibration fallback: 3 short pulses = error. Log crash. |

---

## 2.2 Navigation Active (Continuous)

```
GPS Update arrives (every 5 seconds)
    → GPS Callback Thread: Receive {lat, lon, accuracy, speed} (0ms)
    → GPS Callback Thread: Calculate distance to next turn (<1ms)
        → Input: current position + next step geometry
        → Method: Haversine distance to nearest route polyline point
        → Output: distanceToNextTurn (meters), currentBearing (degrees)
    → [DECISION: isOffRoute? (distance to route > 30m, 3 consecutive fixes)]
        → YES:
            → UI Thread: TTS "You seem off route. Recalculate?" (50-150ms)
            → Wait for voice confirmation ("yes" / "no")
                → "yes": POST /api/navigation/route from current GPS (2-4s)
                → "no": Continue with current route
        → NO:
            → [DECISION: distance to next turn matches announcement threshold?]
                → 100m (+-10m): TTS "In 100 meters, turn {direction}" (50-150ms)
                → 50m (+-5m): TTS "In 50 meters, turn {direction}" (50-150ms)
                → 15m (+-5m): TTS "Turn {direction} now" (50-150ms)
                → Past turn (>20m beyond turn point):
                    → TTS "You may have passed the turn. Recalculate?" (50-150ms)
                → None of the above: No announcement (silent tick)
    → [CONCURRENT: Detection alert from CV pipeline]
        → [DECISION: alert priority vs current TTS]
            → CRITICAL alert: QUEUE_FLUSH → interrupt TTS → speak danger
            → WARNING alert: QUEUE_ADD → speak after current TTS finishes

Data flow per GPS tick:
    Input: 32 bytes (lat/lon/accuracy/speed as doubles)
    Processing: <1ms
    Output: 0 bytes (no network) or TTS text string (<100 bytes)
    Network: 0 bytes (route already cached)
```

---

## 2.3 Detection Alert (CV Pipeline → User)

```
CameraX delivers frame
    → CameraX Executor: ImageProxy arrives (0ms, hardware-delivered)
    → [DECISION: _isProcessing?]
        → YES: Drop frame, return immediately
        → NO: Set _isProcessing = true
    → ThreadPool: Acquire SemaphoreSlim (0ms if free)
    → ThreadPool: YUV→RGB conversion (2-5ms, 693KB)
    → ThreadPool: Normalize to tensor (3-5ms, 2.08MB)
    → ThreadPool: OnnxRuntime.Run() (80-110ms NNAPI / 180-220ms CPU)
    → ThreadPool: NMS postprocessing (1-3ms)
    → ThreadPool: Danger Filter
        → Input: List<Detection> (5-20 items, ~2KB)
        → Process each detection:
            → Lookup class in tier map (O(1) dictionary lookup)
            → [DECISION: tier classification]
                → IRRELEVANT/INFORMATIONAL: Discard
                → CRITICAL (Tier 1): Keep regardless of screen position
                → WARNING (Tier 2): Keep only if in center 60%
                → INFO (Tier 3): Keep only if in center 60%, no vibration
            → Calculate direction: left/center/right from bbox center X
            → Calculate distance zone from bbox height ratio:
                → bbox height > 60% frame height → 0-2m CRITICAL ZONE
                → bbox height 20-60% → 2-5m WARNING ZONE
                → bbox height < 20% → 5m+ AWARENESS ZONE
            → [DECISION: movement tracking]
                → Compare bbox center with previous frame's detection (same class)
                → If delta > threshold: object is approaching → escalate priority
        → Output: List<DangerAlert> (0-5 items, <1KB)
    → [DECISION: any CRITICAL alerts (0-2m zone)?]
        → YES:
            → UI Thread: Vibration (300ms continuous buzz) (0ms dispatch)
            → UI Thread: TTS QUEUE_FLUSH → "{class} {direction}, very close" (50-150ms)
            → [DECISION: is this a "crucial object" needing backend description?]
                → YES (multiple detections, or traffic light needing signal reading):
                    → ThreadPool: POST /api/cv/analyze with JPEG frame (50-80KB)
                    → Wait 0.5-2.0s for detailed description
                    → UI Thread: TTS QUEUE_ADD → detailed description
                → NO: Simple alert sufficient
        → NO, but WARNING alerts (2-5m zone)?
            → UI Thread: Vibration (100ms single pulse) (0ms dispatch)
            → UI Thread: TTS QUEUE_ADD → "{class} ahead, {distance}" (50-150ms)
        → NO alerts at all: No output, release semaphore
    → ThreadPool: Release SemaphoreSlim
    → Set _isProcessing = false

Total per-frame latency (NNAPI): 90-130ms
Total per-frame latency (CPU): 190-240ms
Memory per frame: 5.6MB (pooled, constant)
```

---

## 2.4 Network Loss

```
Connectivity.ConnectivityChanged event fires (NetworkAccess == None)
    → UI Thread: Set _isNetworkAvailable = false (0ms)
    → UI Thread: _networkLossAnnouncementCount++ (0ms)
    → [DECISION: _networkLossAnnouncementCount <= 2]
        → YES: TTS "Network lost" (50-150ms)
        → NO: Silent (don't spam user)
    → All components react:
        → CV Pipeline: Continues normally (on-device, no network needed)
        → Navigation:
            → Route already cached in memory → continue guidance
            → Recalculation disabled → "Cannot recalculate without network"
            → Off-route detection still works (local GPS comparison)
        → Voice Commands:
            → Disabled entirely (requires Whisper API)
            → Button press → TTS "Voice commands unavailable without network"
        → Backend CV Fallback:
            → If currently in fallback mode: CRITICAL
            → Must switch back to on-device CV immediately
            → Even if device is hot → reduce to 0.5fps on-device, accept thermal risk
            → TTS: "Switching to device detection"
        → Error Logging:
            → Buffer errors locally (in-memory list, max 100 entries)
            → Flush to server when network restored

Network Restoration:
    → Connectivity.ConnectivityChanged event (NetworkAccess == Internet)
    → UI Thread: Set _isNetworkAvailable = true
    → Reset _networkLossAnnouncementCount = 0
    → TTS: "Network restored" (once)
    → Flush buffered error logs to server
    → Voice commands re-enabled
    → If was in backend fallback before network loss: do NOT auto-return to fallback
        → Let normal thermal/battery triggers decide
```

---

## 2.5 Device Overheat

```
ThermalMonitor check (every 10 seconds via Timer)
    → ThreadPool: Read device temperature
        → Android API: PowerManager.THERMAL_STATUS_* (API 29+)
        → Fallback: Read /sys/class/thermal/thermal_zone*/temp
    → [DECISION: temperature check with hysteresis]
        → temp > 43°C AND currently on-device CV:
            → TRIGGER OFFLOAD
            → Set _isThermalOffloaded = true
            → UI Thread: TTS "Device warm, switching to cloud detection" (50-150ms)
            → Stop CameraX ImageAnalysis (0ms)
            → Start reduced-rate frame capture for backend:
                → CameraX still captures, but at 0.5fps
                → JPEG compress frame: quality=60, resize to 416x416 → 50-80KB
                → POST /api/cv/analyze every 2 seconds
                → Backend returns detections in 0.5-1.5s
                → Apply same danger filter on response
            → [DECISION: network available?]
                → YES: Proceed with backend fallback
                → NO: Stay on-device at 0.5fps, accept thermal risk
                    → TTS: "Running at reduced speed"
        → temp < 40°C AND currently offloaded (3°C hysteresis):
            → RETURN TO ON-DEVICE
            → Set _isThermalOffloaded = false
            → TTS: "Device cooled, resuming device detection" (50-150ms)
            → Restart on-device CV pipeline at normal rate
            → Stop backend frame uploads
        → temp 40-43°C:
            → No change (hysteresis zone)
            → If on-device: continue on-device
            → If offloaded: continue offloaded

Battery Check (every 60 seconds):
    → Same flow as thermal, but trigger at <25% battery
    → Hysteresis: return to on-device at >30% (if thermal also OK)
    → TTS: "Battery low, switching to cloud detection"

Inference Time Check (every frame):
    → Track last 10 inference times in circular buffer
    → If average > 250ms sustained (10 consecutive frames):
        → Trigger offload (same flow as thermal)
        → TTS: "Detection slowed, switching to cloud"
    → Hysteresis: return when average < 150ms for 10 frames
```

---

# PART 3: SMART OFFLOAD ARCHITECTURE

---

## 3.1 Trigger Conditions (Precise Thresholds)

| Trigger | Threshold (Enter Fallback) | Threshold (Return to Device) | Hysteresis Band | Check Interval |
|---------|---------------------------|------------------------------|-----------------|----------------|
| Device temperature | > 43°C | < 40°C | 3°C | Every 10 seconds |
| Battery level | < 25% | > 30% | 5% | Every 60 seconds |
| Inference latency | > 250ms avg (10 frames) | < 150ms avg (10 frames) | 100ms | Every frame |
| CPU load | > 85% sustained (30s) | < 60% sustained (30s) | 25% | Every 10 seconds |
| Network availability | Required for fallback | — | — | Event-driven |

**Combined Logic (pseudocode):**

```
FUNCTION shouldOffload():
    IF NOT networkAvailable: RETURN false  // Can't offload without network
    IF deviceTemp > 43: RETURN true
    IF batteryLevel < 25: RETURN true
    IF avgInferenceTime > 250ms: RETURN true
    IF cpuLoad > 85% for 30s: RETURN true
    RETURN false

FUNCTION shouldReturnToDevice():
    IF NOT _isThermalOffloaded: RETURN false  // Not currently offloaded
    IF deviceTemp > 40: RETURN false  // Still too warm
    IF batteryLevel < 30: RETURN false  // Still too low
    IF cpuLoad > 60%: RETURN false  // Still loaded
    RETURN true
```

**State Machine:**

```
States: ON_DEVICE, OFFLOADING, OFFLOADED, RETURNING

ON_DEVICE:
    → shouldOffload() → OFFLOADING
    → Frame processed normally on device

OFFLOADING:
    → Stop on-device inference
    → Start backend frame uploads
    → Verify first backend response received
    → → OFFLOADED (on success)
    → → ON_DEVICE (on failure, with warning TTS)

OFFLOADED:
    → Backend handles CV
    → shouldReturnToDevice() → RETURNING
    → Network lost → ON_DEVICE (emergency, reduced fps)

RETURNING:
    → Start on-device inference
    → Verify first on-device inference succeeds
    → Stop backend frame uploads
    → → ON_DEVICE
```

---

## 3.2 Backend CV Fallback Behavior

### Frame Upload Protocol

| Parameter | Value | Justification |
|-----------|-------|---------------|
| Frame rate in fallback | 0.5 fps (1 frame every 2 seconds) | Balance between safety coverage and bandwidth/cost |
| Image format | JPEG, quality=60 | Best size/quality ratio for object detection |
| Resolution | 416x416 | Match on-device model input; no server-side resize needed |
| Frame size | 50-80 KB per frame | Measured JPEG at q60 for urban scenes |
| Bandwidth usage | 25-40 KB/s sustained | Manageable on LTE (typical 1-10 Mbps in Astana) |
| Upload method | HTTP POST, multipart/form-data | Simple, reliable, no WebSocket complexity |

### Latency Budget

| Step | Time | Cumulative |
|------|------|-----------|
| JPEG compression on device | 10-20ms | 20ms |
| Upload (80KB on LTE, Astana→GCP) | 100-300ms | 320ms |
| Server YOLO inference (GPU) | 50-100ms | 420ms |
| Server NMS + response | 10-20ms | 440ms |
| Download response (<1KB) | 50-100ms | 540ms |
| Device-side danger filter | <1ms | 541ms |
| TTS dispatch | 50-150ms | 691ms |
| **Total worst case** | | **~700ms-1.2s** |

Maximum acceptable delay for safety: **1.5 seconds**. At 0.5fps, a user walking at 1.4 m/s covers 2.8m between frames. Combined with 1.2s processing delay, worst-case detection happens when object is 2.8+1.7=4.5m closer than detected. This is within the 5m warning zone but pushes into 2m critical zone for fast-approaching objects.

**Mitigation:** In fallback mode, increase the warning zone threshold. Treat 5-8m as "critical" instead of 2-5m.

### Cost Calculation

| Component | Rate | Per Hour (1 user at 0.5fps) |
|-----------|------|---------------------------|
| Cloud Run vCPU (0.5 vCPU for inference) | $0.0000240/vCPU-s | 1800 requests × 0.15s × 0.5 = $0.0032 |
| Cloud Run memory (1GB for model) | $0.0000025/GB-s | 1800 × 0.15s × 1 = $0.00068 |
| Cloud Run requests | $0.40/1M | 1800 / 1M × $0.40 = $0.00072 |
| Network egress (1KB responses) | $0.12/GB | 1.8MB / 1GB × $0.12 = $0.00022 |
| **Total per hour per user** | | **~$0.005** |
| **Total per hour (worst case GPU inference)** | | **~$0.05-0.10** |

At testing phase (1 user, maybe 30min/day in fallback): **<$2/month**.
At 100 users (assume 10% in fallback, 15min/day each): **~$15-25/month**.

---

## 3.3 Buzzing + Delayed Description Pattern

### Immediate Response (On-Device, <200ms)

```
Detection enters danger filter with CRITICAL or WARNING classification

→ Immediate vibration (device vibrator hardware):
    CRITICAL (0-2m): 300ms continuous buzz
    WARNING (2-5m):  100ms single pulse
    APPROACH (movement toward user): Double pulse (100ms-pause-100ms)

→ Immediate short TTS:
    Format: "{class} {direction}"
    Examples:
        "Car left"
        "Dog ahead"
        "Bicycle right"
        "Obstacle ahead"  (for uncommon classes)
    Duration: 0.5-1.0s speech
```

### Delayed Description (Backend, 1-3s after initial alert)

```
[DECISION: Does this detection warrant a detailed description?]

Criteria for sending to backend:
    1. Object persists for >= 2 consecutive frames (1 second at 2fps)
    2. Object is in CRITICAL zone (0-2m) OR
    3. Object is a "crucial class" requiring context
    4. No identical description spoken in last 10 seconds (dedup)

IF criteria met:
    → Capture current frame as JPEG (50-80KB)
    → POST /api/cv/describe {image, detections[], userContext}
    → Backend runs higher-capability analysis:
        → Confirm YOLOv8n detection with server-side model
        → Generate contextual description
    → Response: {description: "White sedan approaching from left, about 3 meters away, moving slowly"}
    → TTS QUEUE_ADD (after current short alert finishes)
```

### Crucial Objects Definition

| COCO Class | Gets Detailed Description? | Reason |
|------------|---------------------------|--------|
| car | YES — if in 0-5m zone | Describe color, direction, speed, parked vs moving |
| bus | YES — if in 0-5m zone | Describe if stopped at bus stop vs moving |
| truck | YES — if in 0-5m zone | Describe size, direction |
| motorcycle | YES — if in 0-5m zone | Describe direction and speed |
| bicycle | YES — if in 0-2m zone | Describe if parked or riding toward user |
| train | YES — always | Always provide maximum context for trains |
| traffic light | YES — always | Attempt to describe signal color (red/green) |
| person | YES — if count > 2 in center | Describe crowd density and path clearance |
| dog | YES — if in 0-2m zone | Describe size, leash status if visible |
| stop sign | YES — when first encountered | Confirm road boundary |
| fire hydrant | NO | Simple "obstacle at knee level" sufficient |
| bench | NO | Simple "bench on path" sufficient |
| chair | NO | Simple "chair obstacle" sufficient |
| All others | NO | Simple alert sufficient |

---

# PART 4: BACKEND ARCHITECTURE

---

## 4.1 Service Specifications

### Navigation API (Cloud Run)

| Attribute | Value |
|-----------|-------|
| **Language** | Go |
| **Justification** | 200-400ms cold start (vs 1.5-3.5s Python). Minimal dependencies. Excellent HTTP performance. |
| **RAM** | 128MB |
| **CPU** | 0.5 vCPU |
| **Timeout** | 10s |
| **Concurrency** | 80 |
| **Min instances** | 1 (eliminates cold start for primary service) |
| **Max instances** | 3 |
| **Scale-up trigger** | >60% CPU utilization |

**Endpoints:**

| Method | Path | Request | Response | External Calls |
|--------|------|---------|----------|----------------|
| POST | `/api/navigation/route` | `{origin: {lat, lon}, destination: {lat, lon}}` | `{steps: [{instruction, distance_m, bearing, geometry}], total_distance_m, total_time_s}` | OSRM demo server (1 call) |
| POST | `/api/navigation/geocode` | `{query: "Khan Shatyr", lang: "ru"}` | `{results: [{name, lat, lon, display_name}]}` | Nominatim (1 call) |
| GET | `/api/navigation/health` | — | `{status: "ok", osrm: "ok", timestamp}` | OSRM (1 ping) |

**Cost per 1000 requests:** ~$0.005 (compute) + $0 (OSRM is free) + $0 (Nominatim is free with attribution)

**Caching Strategy:**
- Geocoding results: Cache in PostgreSQL for 30 days (same query → same result)
- Route results: No caching (origin changes each time)

---

### Voice API (Cloud Run)

| Attribute | Value |
|-----------|-------|
| **Language** | Go |
| **Justification** | Fast cold start. OpenAI SDK available for Go. Deterministic classifier is simple string matching. |
| **RAM** | 256MB |
| **CPU** | 1 vCPU |
| **Timeout** | 15s (Whisper can be slow) |
| **Concurrency** | 10 |
| **Min instances** | 0 (voice is user-initiated, not continuous; accept cold start) |
| **Max instances** | 5 |
| **Scale-up trigger** | >70% CPU or pending request queue >5 |

**Endpoints:**

| Method | Path | Request | Response | External Calls |
|--------|------|---------|----------|----------------|
| POST | `/api/voice/transcribe` | multipart: `audio` (OGG Opus file, 15-20KB) + `lang` hint | `{text, language, intent, parameters, responseText}` | OpenAI Whisper (1 call) + optionally GPT-4o-mini (1 call) |
| GET | `/api/voice/health` | — | `{status: "ok", openai: "ok"}` | OpenAI models list (1 call) |

**Intent Classification Pipeline (server-side):**

```
1. Receive transcribed text from Whisper

2. Deterministic classifier (regex patterns, <5ms):
   Patterns (Russian + English):
     /навигация|маршрут|route|navigate|go to|иди к/i → "navigate"
     /стоп|остановить|stop|cancel|хватит/i → "stop"
     /повтори|repeat|again|еще раз/i → "repeat"
     /где я|where am i|местоположение/i → "where_am_i"
     /пересчитай|recalculate|новый маршрут/i → "recalculate"
     /помощь|help|что ты умеешь/i → "help"
     /да|yes|ок|okay/i → "confirm" (context-dependent)
     /нет|no|не надо/i → "deny" (context-dependent)

3. If no pattern match → GPT-4o-mini classification:
   System prompt: "Classify the user's intent..."
   Temperature: 0 (deterministic)
   Max tokens: 50
   Cost: ~$0.00015 per request
   Latency: 300-500ms

4. If GPT also fails → "unknown" intent
   Response: "Didn't understand, please try again"
```

**Cost per 1000 requests:**
- Compute: ~$0.005
- Whisper API: 1000 × 5s avg = 83 minutes × $0.006/min = **$0.50**
- GPT-4o-mini (assume 20% fallback): 200 × $0.00015 = **$0.03**
- Total: **~$0.54 per 1000 voice commands**

---

### CV Fallback API (Cloud Run)

| Attribute | Value |
|-----------|-------|
| **Language** | Python |
| **Justification** | Best ecosystem for ONNX/ML inference. Cold start acceptable (fallback is rare, min-instances=0). |
| **RAM** | 1GB (model + inference buffers) |
| **CPU** | 2 vCPU |
| **Timeout** | 10s |
| **Concurrency** | 1 (single inference per instance) |
| **Min instances** | 0 (fallback is rare; accept 3-5s cold start) |
| **Max instances** | 3 |
| **Scale-up trigger** | Any queued request (concurrency=1) |

**Endpoints:**

| Method | Path | Request | Response | External Calls |
|--------|------|---------|----------|----------------|
| POST | `/api/cv/analyze` | multipart: `image` (JPEG, 50-80KB) | `{detections: [{class, confidence, bbox, direction, distance_zone}]}` | None (on-server inference) |
| POST | `/api/cv/describe` | multipart: `image` (JPEG) + `detections` (JSON) + `context` | `{description: "White sedan approaching from left..."}` | GPT-4o-mini (1 call for description generation) |
| GET | `/api/cv/health` | — | `{status: "ok", model_loaded: true}` | None |

**Server-side Model:**
- Same YOLOv8n ONNX model as device (consistency)
- Runs on CPU (Cloud Run doesn't support GPU; adequate for 0.5fps)
- Inference time on 2 vCPU: 50-100ms per frame

**Cost per 1000 requests:**
- Compute: 1000 × 0.15s × 2vCPU × $0.0000240 = **$0.007**
- Memory: 1000 × 0.15s × 1GB × $0.0000025 = **$0.0004**
- GPT-4o-mini (describe endpoint, assume 10% of requests): 100 × $0.00015 = **$0.015**
- Total: **~$0.02 per 1000 fallback frames**

---

## 4.2 Database Schema

```sql
-- Users table: stores device-level preferences
CREATE TABLE users (
    id              SERIAL PRIMARY KEY,
    device_id       VARCHAR(64) NOT NULL UNIQUE,  -- Android device fingerprint
    preferred_lang  VARCHAR(5) DEFAULT 'ru',       -- 'ru', 'kk', 'en'
    tts_speed       FLOAT DEFAULT 1.0,             -- 0.5 to 2.0
    alert_volume    VARCHAR(10) DEFAULT 'normal',  -- 'low', 'normal', 'high'
    danger_zones    JSONB DEFAULT '{"critical_m": 2, "warning_m": 5}',
    created_at      TIMESTAMP DEFAULT NOW(),
    updated_at      TIMESTAMP DEFAULT NOW()
);
CREATE INDEX idx_users_device_id ON users(device_id);

-- Navigation sessions: one row per route
CREATE TABLE navigation_sessions (
    id              SERIAL PRIMARY KEY,
    user_device_id  VARCHAR(64) NOT NULL,
    origin_lat      DOUBLE PRECISION NOT NULL,
    origin_lon      DOUBLE PRECISION NOT NULL,
    dest_lat        DOUBLE PRECISION NOT NULL,
    dest_lon        DOUBLE PRECISION NOT NULL,
    dest_name       VARCHAR(256),
    total_distance_m FLOAT,
    total_time_s    FLOAT,
    status          VARCHAR(20) DEFAULT 'active',  -- 'active', 'completed', 'cancelled'
    started_at      TIMESTAMP DEFAULT NOW(),
    ended_at        TIMESTAMP,
    recalculations  INT DEFAULT 0
);
CREATE INDEX idx_sessions_user_device ON navigation_sessions(user_device_id, started_at DESC);

-- Detection events: sampled logging (not every frame)
CREATE TABLE detection_events (
    id              SERIAL PRIMARY KEY,
    session_id      INT REFERENCES navigation_sessions(id),
    detected_class  VARCHAR(30) NOT NULL,
    confidence      FLOAT NOT NULL,
    danger_tier     SMALLINT NOT NULL,  -- 1=critical, 2=warning, 3=info
    distance_zone   VARCHAR(10),        -- 'critical', 'warning', 'awareness'
    direction       VARCHAR(10),        -- 'left', 'center', 'right'
    user_lat        DOUBLE PRECISION,
    user_lon        DOUBLE PRECISION,
    was_offloaded   BOOLEAN DEFAULT FALSE,
    detected_at     TIMESTAMP DEFAULT NOW()
);
CREATE INDEX idx_detections_session ON detection_events(session_id);
CREATE INDEX idx_detections_class ON detection_events(detected_class, detected_at);

-- Error logs: crash and error tracking
CREATE TABLE error_logs (
    id              SERIAL PRIMARY KEY,
    user_device_id  VARCHAR(64),
    component       VARCHAR(30) NOT NULL,  -- 'cv', 'navigation', 'voice', 'backend'
    error_type      VARCHAR(50) NOT NULL,
    error_message   TEXT,
    stack_trace     TEXT,
    device_temp_c   FLOAT,
    battery_pct     FLOAT,
    ram_used_mb     FLOAT,
    occurred_at     TIMESTAMP DEFAULT NOW()
);
CREATE INDEX idx_errors_component ON error_logs(component, occurred_at DESC);

-- Geocoding cache
CREATE TABLE geocoding_cache (
    id              SERIAL PRIMARY KEY,
    query_text      VARCHAR(256) NOT NULL,
    query_lang      VARCHAR(5) NOT NULL,
    result_name     VARCHAR(256),
    result_lat      DOUBLE PRECISION,
    result_lon      DOUBLE PRECISION,
    result_display  VARCHAR(512),
    cached_at       TIMESTAMP DEFAULT NOW()
);
CREATE UNIQUE INDEX idx_geocache_query ON geocoding_cache(query_text, query_lang);
```

**Data Retention Policy:**

| Table | Retention | Cleanup Method | Justification |
|-------|-----------|---------------|---------------|
| users | Indefinite | Manual | Small table, <100 rows |
| navigation_sessions | 90 days | Scheduled Cloud Function weekly | Historical routes not needed long-term |
| detection_events | 30 days | Scheduled Cloud Function weekly | Analytics only; high volume |
| error_logs | 60 days | Scheduled Cloud Function weekly | Debugging window |
| geocoding_cache | 30 days | Scheduled Cloud Function weekly | Stale results |

**Migration Strategy:**

All schema changes use sequential migration files:
```
migrations/
  001_initial_schema.sql
  002_add_column.sql
  ...
```

Each migration:
- Is idempotent (uses `IF NOT EXISTS`, `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`)
- Runs in a transaction
- Never drops columns in production (mark deprecated, remove after 2 releases)
- Adding an index: use `CREATE INDEX CONCURRENTLY` to avoid table locks

---

## 4.3 Infrastructure Cost Model

### Testing Phase (1 user, 2 hr/day)

| Item | Monthly Cost | Notes |
|------|-------------|-------|
| Cloud Run — Navigation API (1 min instance, 0.5 vCPU, 128MB) | $18 | Always-on for zero cold start |
| Cloud Run — Voice API (0 min instances) | $0.50 | ~60 requests/day, cold start OK |
| Cloud Run — CV Fallback API (0 min instances) | $0.10 | Rarely triggered during testing |
| Cloud SQL — db-f1-micro, 10GB SSD | $9 | PostgreSQL micro instance |
| OpenAI — Whisper API | $1.50 | ~60 transcriptions/day × 30 days |
| OpenAI — GPT-4o-mini (intent + describe) | $0.50 | ~20% fallback rate |
| Network egress | $0.10 | Minimal data transfer |
| Cloud Monitoring + Logging | $0 | Free tier sufficient |
| **Total** | **~$30-35/month** |

### Launch Phase (100 users, avg 1 hr/day)

| Item | Monthly Cost | Notes |
|------|-------------|-------|
| Cloud Run — Navigation API (1 min instance, scale to 3) | $25 | Handles concurrent route requests |
| Cloud Run — Voice API (1 min instance, scale to 5) | $45 | ~100 users × 20 voice commands/session |
| Cloud Run — CV Fallback API (0 min, scale to 3) | $15 | ~10% of users in fallback, 15 min/day |
| Cloud SQL — db-g1-small (0.5 vCPU, 1.7GB RAM) | $25 | Upgrade from micro for concurrent connections |
| OpenAI — Whisper API | $36 | 100 users × 20 commands/day × 5s avg |
| OpenAI — GPT-4o-mini | $5 | Intent fallback + descriptions |
| Network egress | $2 | More users, still minimal per-user |
| Cloud Monitoring + Logging | $5 | Basic alerting |
| **Total** | **~$160-180/month** |

### Breaking Points

| User Count | Component That Breaks First | Upgrade Needed | New Monthly Cost |
|------------|---------------------------|----------------|-----------------|
| 50 users | Cloud SQL db-f1-micro (connection limit) | Upgrade to db-g1-small | +$16/month |
| 200 users | OSRM demo server (rate limits) | Self-host OSRM or use Valhalla on Cloud Run | +$30-50/month |
| 500 users | Cloud Run CV Fallback (concurrent thermal offloads) | Add Cloud Run GPU instances or pre-emptible VMs | +$100-200/month |
| 1000 users | OpenAI API costs ($360/month Whisper alone) | On-device Whisper Tiny or batch transcription | Saves ~$250/month |

### Cost Optimization Strategies

| Strategy | Savings | When to Implement |
|----------|---------|-------------------|
| Geocoding cache in PostgreSQL | ~$0 (Nominatim is free, but saves latency) | Phase 2 (already in schema) |
| Voice command batching (record → upload, not streaming) | Already implemented | Phase 1 |
| CV Fallback frame skip (skip frames with no new detections) | 30-50% reduction in fallback API calls | Phase 4 |
| Cloud Run region: asia-south1 (Mumbai) | 20-30% lower latency from Astana vs us-central1 | Phase 2 |
| Reserved Cloud SQL (1-year commitment) | 25% discount | When committed to production |
| Switch GPT-4o-mini to local regex for 95% of intents | Eliminates most GPT costs | Phase 3 |

---

# PART 5: MIGRATION PLAN

---

## Phase 1: Emergency Triage (Fix 6 Critical Blockers)

**Objective:** Make the existing app stop crashing. No new features.
**Duration:** 8-12 hours
**Blockers:** None (first phase)
**Validation:** App runs for 30 minutes on a walking route without crashing
**Rollback:** Git revert to pre-triage commit

### Step 1.1: Fix 10MB/frame Memory Allocation

| Attribute | Detail |
|-----------|--------|
| **File/location** | CV processing class, method that converts camera frame to tensor |
| **Current behavior** | Allocates new `byte[]`, new `Bitmap`, and new `float[]` on every frame. At 2fps = 20MB/s allocation pressure. GC pauses cause frame drops and eventual OOM. |
| **Target behavior** | Pre-allocate three pooled buffers at initialization: `_rgbBytes` (693KB), `_inputTensor` (2.08MB), `_nmsResults` (4KB). Reuse on every frame via clear-and-fill pattern. |
| **Risk** | If buffer sizes are wrong, ONNX will throw dimension mismatch. Verify tensor shape matches model export. |
| **Verification** | Run 100 frames in debug mode. `GC.GetTotalMemory(false)` should not grow by more than 5MB over baseline. |

### Step 1.2: Fix async void Crashes

| Attribute | Detail |
|-----------|--------|
| **File/location** | All event handlers using `async void` — camera callback, button handlers, GPS callbacks |
| **Current behavior** | `async void` methods throw unhandled exceptions that crash the app silently on Android. No stack trace in logs. |
| **Target behavior** | Wrap every `async void` handler body in `try/catch`. Log exception details to in-memory error buffer. Never let exceptions propagate from `async void`. |
| **Risk** | Swallowed exceptions could hide real bugs. Mitigation: always log caught exceptions with full stack trace. |
| **Verification** | Force an exception in each handler (e.g., null reference). App should log error and continue, not crash. |

### Step 1.3: Re-enable Navigation Voice

| Attribute | Detail |
|-----------|--------|
| **File/location** | Navigation service class, turn announcement method |
| **Current behavior** | TTS calls for turn announcements are commented out or disabled. Navigation calculates turns but never speaks them. |
| **Target behavior** | Re-enable TTS.Speak() calls in the turn announcement logic. Use `QUEUE_ADD` for normal turns, `QUEUE_FLUSH` for "turn now" urgency. Initialize TTS engine in OnCreate with Russian locale. |
| **Risk** | TTS engine may not be initialized when first announcement fires. Mitigation: guard with `_ttsReady` flag set in `OnInit` callback. |
| **Verification** | Start navigation to a known Astana address. Walk toward first turn. Hear "In 100 meters, turn left" (or equivalent). |

### Step 1.4: Fix Fire-and-Forget Task Queue

| Attribute | Detail |
|-----------|--------|
| **File/location** | Any location using `Task.Run()` or `_ = SomeAsyncMethod()` without awaiting |
| **Current behavior** | Tasks are launched without tracking. If they throw, exceptions are lost. If app pauses, tasks continue running and access disposed resources. |
| **Target behavior** | Replace fire-and-forget with: (a) `await` where possible, (b) `Task.Run().ContinueWith(t => LogError(t.Exception))` where fire-and-forget is intentional, (c) cancel via `CancellationToken` linked to app lifecycle. |
| **Risk** | Adding `await` may change timing/ordering of operations. Test each change individually. |
| **Verification** | Set breakpoint on error logger. Intentionally cause failures in background tasks. Verify all exceptions are logged. |

### Step 1.5: Fix Thread-Unsafe Collections

| Attribute | Detail |
|-----------|--------|
| **File/location** | Any `List<T>`, `Dictionary<TK,TV>` accessed from multiple threads (camera thread + UI thread + GPS thread) |
| **Current behavior** | Standard collections used across threads without synchronization. Causes intermittent `InvalidOperationException` ("Collection was modified during enumeration") and corrupt state. |
| **Target behavior** | Replace with `ConcurrentDictionary`, `ConcurrentQueue`, or guard with `lock` statements. For detection results list: use `Interlocked.Exchange` to swap entire list atomically. |
| **Risk** | Locks can cause deadlocks if acquired in inconsistent order. Mitigation: establish lock ordering (always acquire in alphabetical order of lock object names). |
| **Verification** | Run app for 10 minutes while toggling between camera/navigation/voice. No collection-modified exceptions in log. |

### Step 1.6: Fix Shared HttpClient Corruption

| Attribute | Detail |
|-----------|--------|
| **File/location** | HttpClient usage throughout the app |
| **Current behavior** | Multiple `new HttpClient()` instances created per request, OR single instance with `DefaultRequestHeaders` modified concurrently from different threads. Socket exhaustion and header corruption. |
| **Target behavior** | Single `HttpClient` instance per base URL, created at app startup with `AndroidMessageHandler`. No modification of `DefaultRequestHeaders` after creation. Per-request headers via `HttpRequestMessage`. |
| **Risk** | If existing code relies on changing `BaseAddress` or default headers per request, those calls must be refactored. |
| **Verification** | Make 20 rapid API calls (voice + navigation concurrently). No `HttpRequestException` or socket errors in log. |

---

## Phase 2: Backend Infrastructure (Deploy Cloud Run + Database)

**Objective:** Standing backend services accessible from the app.
**Duration:** 6-8 hours
**Blockers:** Phase 1 complete (app must be stable to test against backend)
**Validation:** `curl` each endpoint successfully from developer laptop + from the Android device on LTE
**Rollback:** `gcloud run services delete` for each service; `gcloud sql instances delete` for database

### Step 2.1: Set Up GCP Project

| Attribute | Detail |
|-----------|--------|
| **Action** | Create GCP project "runa-nav". Enable Cloud Run, Cloud SQL, Cloud Build APIs. Set billing budget alert at $80/month. |
| **Verification** | `gcloud projects describe runa-nav` returns project info. Billing alert configured in console. |

### Step 2.2: Deploy Cloud SQL PostgreSQL

| Attribute | Detail |
|-----------|--------|
| **Action** | Create db-f1-micro instance in asia-south1 (lowest latency from Astana). Run `001_initial_schema.sql` migration. Create application user with limited privileges. |
| **Risk** | asia-south1 may not have db-f1-micro. Fallback: us-central1 with ~100ms additional latency. |
| **Verification** | Connect via `psql` from Cloud Shell. Run `SELECT 1;`. Verify all tables exist via `\dt`. |

### Step 2.3: Deploy Navigation API

| Attribute | Detail |
|-----------|--------|
| **Action** | Create Go service with `/api/navigation/route`, `/api/navigation/geocode`, `/api/navigation/health`. Deploy to Cloud Run with min-instances=1. |
| **Risk** | OSRM demo server may be slow/down during deployment testing. Have a fallback test with cached response. |
| **Verification** | `curl -X POST https://nav-api-xxx.run.app/api/navigation/route -d '{"origin":{"lat":51.128,"lon":71.430},"destination":{"lat":51.143,"lon":71.462}}'` returns valid route JSON. |

### Step 2.4: Deploy Voice API

| Attribute | Detail |
|-----------|--------|
| **Action** | Create Go service with `/api/voice/transcribe`. Set OPENAI_API_KEY as Cloud Run secret. Deploy with min-instances=0. |
| **Risk** | OpenAI API key exposure. Mitigation: use Cloud Run Secrets, never in source code. |
| **Verification** | `curl -X POST -F "audio=@test.ogg" https://voice-api-xxx.run.app/api/voice/transcribe` returns transcription JSON. |

### Step 2.5: Deploy CV Fallback API

| Attribute | Detail |
|-----------|--------|
| **Action** | Create Python service with `/api/cv/analyze` and `/api/cv/describe`. Bundle YOLOv8n ONNX model in container. Deploy with min-instances=0, 1GB RAM, 2 vCPU. |
| **Risk** | Python cold start with ONNX model loading: 5-15s. Acceptable since fallback is rare. |
| **Verification** | `curl -X POST -F "image=@test_street.jpg" https://cv-api-xxx.run.app/api/cv/analyze` returns detection JSON. |

### Step 2.6: Wire App to Backend

| Attribute | Detail |
|-----------|--------|
| **Action** | Replace existing Firebase Functions URLs with Cloud Run URLs. Update HttpClient to use new endpoints. Add API key header for basic auth. |
| **Risk** | Breaking existing voice/navigation if new endpoints have different response format. Mitigation: match existing response schema exactly. |
| **Verification** | App voice command "navigate to Khan Shatyr" returns route and starts navigation using new backend. |

---

## Phase 3: Helper Extraction (Refactor for Testability)

**Objective:** Extract monolithic service methods into injectable, testable helpers.
**Duration:** 6-8 hours
**Blockers:** Phase 1 complete (stable app to refactor safely)
**Validation:** Unit tests pass for each extracted helper. App still works end-to-end.
**Rollback:** Git revert. Helpers are internal refactoring, no external dependencies change.

### Step 3.1: Extract ICameraFrameSource

| Attribute | Detail |
|-----------|--------|
| **Current behavior** | Camera setup, frame capture, and processing are in one monolithic class. |
| **Target behavior** | `ICameraFrameSource` interface with `OnFrameAvailable(ImageProxy)` event. Implementations: `CameraXFrameSource` (production) and `FileFrameSource` (testing with pre-captured images). |
| **Verification** | `FileFrameSource` feeds 10 test images through CV pipeline. Detections match expected results. |

### Step 3.2: Extract IInferenceEngine

| Attribute | Detail |
|-----------|--------|
| **Current behavior** | OnnxRuntime session creation, tensor prep, and inference are inline in the frame processing method. |
| **Target behavior** | `IInferenceEngine` with `List<Detection> Detect(float[] tensor)`. Implementations: `OnnxInferenceEngine` (production) and `MockInferenceEngine` (returns predetermined detections). |
| **Verification** | Unit test: `MockInferenceEngine` returns 3 detections → danger filter produces correct alerts. |

### Step 3.3: Extract IAlertDispatcher

| Attribute | Detail |
|-----------|--------|
| **Current behavior** | TTS and vibration calls are scattered throughout CV, navigation, and voice code. |
| **Target behavior** | `IAlertDispatcher` with `Speak(text, priority)`, `Vibrate(pattern)`, `InterruptAndSpeak(text)`. Implementations: `DeviceAlertDispatcher` (production) and `RecordingAlertDispatcher` (records all calls for testing). |
| **Verification** | During 1-minute test walk, `RecordingAlertDispatcher` captures all expected alerts in correct order. |

### Step 3.4: Extract ILocationProvider

| Attribute | Detail |
|-----------|--------|
| **Current behavior** | GPS setup and location callbacks are embedded in navigation service. |
| **Target behavior** | `ILocationProvider` with `OnLocationChanged(Location)` event. Implementations: `FusedLocationProvider` (production) and `SimulatedLocationProvider` (feeds coordinates from GPX file). |
| **Verification** | `SimulatedLocationProvider` replays a known Astana walking route. All turn announcements trigger at correct locations. |

### Step 3.5: Extract IThermalMonitor

| Attribute | Detail |
|-----------|--------|
| **Current behavior** | No thermal monitoring exists yet. |
| **Target behavior** | `IThermalMonitor` with `float GetTemperature()` and `ThermalState GetState()`. Implementations: `AndroidThermalMonitor` (reads system thermal) and `MockThermalMonitor` (returns configurable temperature). |
| **Verification** | `MockThermalMonitor` set to 44C → offload trigger fires. Set to 39C → return trigger fires. |

---

## Phase 4: Smart Offload Implementation

**Objective:** Add automatic CV offloading when device is stressed.
**Duration:** 8-10 hours
**Blockers:** Phase 2 (backend CV API deployed) + Phase 3 (IThermalMonitor extracted)
**Validation:** Artificially trigger thermal condition → CV shifts to backend → detections still arrive → cool down → CV returns to device
**Rollback:** Set `_smartOffloadEnabled = false` flag. App runs on-device only (pre-offload behavior).

### Step 4.1: Implement Thermal/Battery Monitor

| Attribute | Detail |
|-----------|--------|
| **Action** | Implement `AndroidThermalMonitor` using `PowerManager.THERMAL_STATUS_*` (API 29+) with fallback to `/sys/class/thermal/`. Add battery level check via `BatteryManager`. Start monitoring timer (10s interval). |
| **Risk** | Thermal zone path varies by manufacturer. Test on target device and add manufacturer-specific paths. |
| **Verification** | Run CPU-intensive task to heat device. Monitor logs for temperature readings updating every 10s. |

### Step 4.2: Implement Offload State Machine

| Attribute | Detail |
|-----------|--------|
| **Action** | Create `OffloadStateMachine` with states: ON_DEVICE, OFFLOADING, OFFLOADED, RETURNING. Implement transition logic per Part 3.1 thresholds. Wire to CV pipeline to start/stop on-device inference. |
| **Risk** | Race condition between state transitions and frame processing. Mitigation: state changes are atomic via `Interlocked.CompareExchange`. |
| **Verification** | Inject 44C via `MockThermalMonitor` → state transitions to OFFLOADING → OFFLOADED. Inject 39C → RETURNING → ON_DEVICE. Log each transition. |

### Step 4.3: Implement Backend Frame Upload

| Attribute | Detail |
|-----------|--------|
| **Action** | When in OFFLOADED state, capture frame at 0.5fps, JPEG compress at q60, POST to `/api/cv/analyze`. Parse response and feed through existing danger filter. |
| **Risk** | Network latency may cause frame backlog. Mitigation: skip frame if previous upload still in flight (fire-and-check, not fire-and-forget). |
| **Verification** | Force offload. Walk past a parked car. Backend returns "car" detection. Device speaks "Car ahead." |

### Step 4.4: Implement Buzzing + Delayed Description

| Attribute | Detail |
|-----------|--------|
| **Action** | For crucial objects (per Part 3.3 table), implement two-phase alert: (1) immediate vibration + short TTS from on-device detection, (2) if object persists for 2+ frames, send frame to `/api/cv/describe` for detailed description. Queue detailed description as TTS QUEUE_ADD. |
| **Risk** | Delayed description may arrive after user has passed the object. Mitigation: cancel pending description if object disappears from frame. |
| **Verification** | Walk toward a parked car. Hear "Car ahead" immediately. 2 seconds later, hear detailed description "Parked white sedan on your left, about 3 meters." |

---

## Phase 5: Performance Optimization

**Objective:** Meet battery and performance targets for 1-hour walking sessions.
**Duration:** 6-8 hours
**Blockers:** Phase 1 (stable app) + Phase 3 (extracted interfaces for measurement)
**Validation:** 30-minute walking session: battery drop < 4%, no thermal throttle, sustained 2fps CV
**Rollback:** Each optimization is independent. Disable individually via config flags.

### Step 5.1: Enable NNAPI Execution

| Attribute | Detail |
|-----------|--------|
| **Current behavior** | OnnxRuntime uses CPU ExecutionProvider (180-220ms/frame). |
| **Target behavior** | Add NNAPI ExecutionProvider as primary, CPU as fallback. Cache compiled model on first run to avoid 1-5s recompilation. |
| **Before** | 180-220ms/frame, 45% CPU at 2fps |
| **After** | 80-110ms/frame, 15-20% CPU at 2fps |
| **Risk** | NNAPI may not support all YOLOv8n ops on target device. OnnxRuntime will fall back to CPU for unsupported ops (partial acceleration). |
| **Verification** | Log inference time for 100 frames. p50 should be <120ms. Check NNAPI compilation via `adb logcat | grep NNAPI`. |

### Step 5.2: Implement Adaptive Frame Rate

| Attribute | Detail |
|-----------|--------|
| **Current behavior** | Fixed 2fps regardless of user movement. |
| **Target behavior** | 2fps when walking (GPS speed > 0.5 m/s), 0.5fps when stationary (GPS speed < 0.5 m/s for 5s). Smooth transition with 5s hysteresis. |
| **Before** | 2fps constant = baseline battery drain |
| **After** | ~1.2fps average (assuming 40% stationary time) = 40% battery reduction from CV |
| **Risk** | GPS speed may be noisy near 0.5 m/s threshold. Mitigation: require 3 consecutive readings below threshold before switching. |
| **Verification** | Stand still for 10 seconds → log shows frame rate dropped to 0.5fps. Start walking → within 5 seconds, frame rate returns to 2fps. |

### Step 5.3: Optimize CameraX Configuration

| Attribute | Detail |
|-----------|--------|
| **Current behavior** | Camera captures at default resolution (1080p or higher), software resize to 416x416. |
| **Target behavior** | Set `ImageAnalysis.Builder().setTargetResolution(Size(416, 416))`. CameraX picks closest sensor output and HAL downsamples in hardware. Eliminates software resize entirely. |
| **Before** | 1080p capture (6.2MB) + software resize (5-12ms) |
| **After** | ~480p capture (HAL selected) + minimal resize (<2ms) |
| **Risk** | Some devices may not support low resolution in ImageAnalysis. CameraX handles gracefully by picking nearest supported size. |
| **Verification** | Log `ImageProxy.width` and `ImageProxy.height` in analyze callback. Should be close to 416 (exact value depends on device). |

### Step 5.4: Implement GPS Power Optimization

| Attribute | Detail |
|-----------|--------|
| **Current behavior** | Continuous GPS listener, possibly with high accuracy priority. |
| **Target behavior** | `PRIORITY_BALANCED_POWER_ACCURACY` with 5000ms interval and 3m minimum displacement. |
| **Before** | ~4-5% battery per hour from GPS |
| **After** | ~2.5-3% battery per hour from GPS |
| **Verification** | 30-minute session with battery profiler. GPS contribution < 1.5% in that period. |

---

## Phase 6: Testing Instrumentation

**Objective:** Enable remote debugging and crash tracking for the 3-4 day test deployment.
**Duration:** 3-4 hours
**Blockers:** Phase 1 (app must be stable enough to add instrumentation without introducing new crashes)
**Validation:** Developer can see real-time logs and crash reports from test user's device
**Rollback:** Remove logging calls. No external dependencies to clean up.

### Step 6.1: Implement Structured Error Logging

| Attribute | Detail |
|-----------|--------|
| **Action** | Create `ErrorLogger` service that buffers errors in-memory (max 100) and flushes to backend `POST /api/logs/errors` every 60 seconds or on app pause. Each error includes: component, error_type, message, stack_trace, device_temp, battery_pct, ram_used. |
| **Verification** | Force an error → check PostgreSQL `error_logs` table → error appears within 60 seconds. |

### Step 6.2: Implement Session Telemetry

| Attribute | Detail |
|-----------|--------|
| **Action** | On route start: insert navigation_session row. On each detection event (sampled at 1 per 10 seconds, not every frame): insert detection_event row. On route end: update session status to 'completed'. |
| **Verification** | Complete a short navigation session → check database → session row with start/end times, 3-5 detection events. |

### Step 6.3: Add Developer Observation Mode

| Attribute | Detail |
|-----------|--------|
| **Action** | Add a "developer overlay" toggle (hidden: triple-tap on app title). When enabled, shows: current FPS, inference time, detection count, thermal state, battery %, offload state, last TTS message. Overlay is semi-transparent text in top-left corner. |
| **Risk** | Overlay may interfere with camera preview. Mitigation: use Android `WindowManager` overlay, not MAUI view. |
| **Verification** | Enable overlay → walk with app → all metrics update in real-time. |

### Step 6.4: Add Remote Log Viewing

| Attribute | Detail |
|-----------|--------|
| **Action** | Add backend endpoint `GET /api/logs/recent?device_id=X&minutes=30` that returns recent error logs and session data for a specific device. Developer can check from laptop during walking test. |
| **Verification** | Test user walks for 5 minutes → developer queries endpoint from laptop → sees real-time session data and any errors. |

---

# PART 6: LONG-TERM EVOLUTION

---

## 6.1 Scaling Checkpoints

### At 10 Users

| Component | What Breaks | Fix Required | Cost Impact |
|-----------|------------|-------------|-------------|
| Cloud SQL db-f1-micro | 25 max connections; 10 concurrent users with connection pooling may hit limit during bursts | Increase connection pool to 10; or upgrade to db-g1-small | +$0 (pool) or +$16/month (upgrade) |
| OSRM demo server | 10 concurrent route requests may trigger rate limiting | Add request queuing with 1s delay between requests | +$0 |
| Cloud Run scaling | Auto-scaling handles 10 users fine with current settings | No change | +$0 |
| OpenAI API | ~600 voice commands/day; well within rate limits | No change | +$5/month |

### At 100 Users

| Component | What Breaks | Fix Required | Cost Impact |
|-----------|------------|-------------|-------------|
| OSRM demo server | 100 concurrent route requests; definitely rate-limited or banned | **Self-host OSRM on Cloud Run** (2GB RAM instance with Kazakhstan OSM extract) or **migrate to Valhalla** | +$30-50/month |
| Cloud SQL | db-f1-micro insufficient for concurrent writes (detection_events) | Upgrade to db-g1-small (0.5 vCPU, 1.7GB RAM) | +$16/month |
| Cloud Run CV Fallback | If 10% of users trigger fallback simultaneously: 10 concurrent inference requests | Set max-instances=10 for CV Fallback API | +$20/month (burst cost) |
| OpenAI API | ~6000 voice commands/day; $36/month Whisper alone | Consider batching or on-device pre-filtering (reject obvious noise before sending) | Saves ~$10/month |
| **Total additional** | | | **+$75-100/month** |
| **Total at 100 users** | | | **~$235-280/month** |

### At 1000 Users

| Component | What Breaks | Fix Required | Cost Impact |
|-----------|------------|-------------|-------------|
| Architecture | Stateless Cloud Run hits concurrent request limits | Add Redis for session caching; consider WebSocket for continuous navigation updates instead of polling | +$50/month (Redis) |
| OpenAI costs | $360/month Whisper + $50/month GPT | **On-device Whisper Tiny** eliminates transcription cost; keep GPT for complex intents only | Saves ~$300/month |
| Cloud SQL | Needs dedicated instance (db-custom-1-4096) | Upgrade; add read replica for log queries | +$100/month |
| OSRM/Valhalla | Dedicated routing instance needed | Dedicated VM or managed service | +$80/month |
| Monitoring | Need proper APM (error rates, latency percentiles) | Add Cloud Monitoring with custom metrics | +$30/month |
| **Total at 1000 users** | | | **~$500-700/month** |

---

## 6.2 Technical Debt Roadmap

### OSRM → Valhalla Migration

| Attribute | Detail |
|-----------|-------|
| **When** | Phase 2 or when OSRM demo server becomes unreliable (whichever comes first) |
| **Why** | Valhalla: turn-by-turn instructions are more detailed, supports pedestrian routing mode natively, can run on Cloud Run without external dependency, has active maintenance |
| **Duration** | 1 week (as specified in requirements) |
| **What breaks during migration** | Route response format changes. Step instructions have different structure. Polyline encoding may differ (Valhalla uses different precision). |
| **Migration steps** | 1. Deploy Valhalla Cloud Run service with Kazakhstan OSM tiles. 2. Create response adapter that translates Valhalla format to current app's expected format. 3. A/B test both backends for 1 day. 4. Switch over. 5. Remove OSRM code. |
| **Rollback** | Keep OSRM endpoint URL as config. Switch back by changing config, no code change. |

### Firebase → PostgreSQL Auth Migration

| Attribute | Detail |
|-----------|-------|
| **When** | At 100+ users, when Firebase free tier limits are approached, or when user accounts (not just device IDs) become necessary |
| **Why** | Current system uses device_id only (no user accounts). Firebase is used only for Cloud Functions (being replaced by Cloud Run). Once Cloud Functions are removed, Firebase has no role. |
| **What breaks** | Nothing if device_id based auth continues. If adding user accounts: need password hashing (bcrypt), JWT tokens, registration flow. |
| **Decision** | Defer until user accounts are actually needed. Device-ID auth is sufficient for MVP and 100 users. |

### Minimum Device Requirements Increase

| Timeline | Min RAM | Justification |
|----------|---------|---------------|
| 2026 (current) | 3 GB | Target demographic may have older devices; Kazakhstan mid-range phone market |
| 2027 | 4 GB | Drop 3GB support if <10% of user base. Enables larger models, background services. |
| 2028+ | 6 GB | If on-device Whisper (500MB model) becomes standard. Only if accessibility-focused device programs provide hardware. |

---

## 6.3 Offline Capability Path

### Current State

| Component | Online Required | Offline Behavior |
|-----------|----------------|-------------------|
| CV (YOLOv8n) | No | Fully functional on-device |
| Navigation routing | Yes (OSRM/Valhalla) | **Cannot calculate new routes** |
| Navigation guidance | No (once route cached) | Continue with cached route |
| Voice commands | Yes (Whisper API) | **Completely disabled** |
| TTS output | No (system TTS) | Fully functional |
| Error logging | Yes (backend API) | Buffer locally, flush later |

### Next (3-6 months): Route Caching

| Feature | Implementation | Storage Cost |
|---------|---------------|-------------|
| Cache last 10 routes | SQLite local database with route JSON | ~500KB |
| Cache frequent destinations | User marks "favorites" → pre-fetch routes from current location on app start | ~200KB |
| Offline route replay | If current GPS near a cached route origin, offer "Use saved route to {destination}?" | 0 additional |
| Cache geocoding results | Store last 20 geocoded destinations locally | ~10KB |

**User experience:** "Network lost. You have a saved route to Khan Shatyr from nearby. Use it?"

### Future (6-12 months): On-Device Whisper

| Attribute | Detail |
|-----------|-------|
| **Model** | Whisper Tiny (39M parameters, ~150MB on disk, ~200MB RAM) |
| **Inference time** | 1.5-3.0s for 5s clip on Snapdragon 6xx (CPU) |
| **Tradeoff** | +200MB RAM permanently. Worse accuracy than Whisper API (especially Russian). But enables fully offline voice commands. |
| **Decision point** | Implement when: (a) target devices have 6GB+ RAM, OR (b) Whisper Tiny Russian accuracy improves, OR (c) user demand for offline voice is strong |
| **Partial approach** | On-device Whisper for simple commands only ("stop", "repeat", "yes", "no"). Backend Whisper for complex commands ("navigate to..."). Saves 80% of API calls while keeping accuracy for navigation queries. |

### Far Future (12+ months): Full Offline Navigation

| Feature | Dependency |
|---------|-----------|
| Offline routing engine | Valhalla compiled for Android NDK, with Kazakhstan OSM tiles (~200MB) |
| Offline geocoding | Local Nominatim with Kazakhstan gazetteer (~100MB) |
| Offline voice | On-device Whisper Tiny + local intent classifier |
| **Total offline storage** | ~500MB additional app size |

This is viable but significantly increases app size. Recommend as an optional "offline pack" download.

---

# EXECUTIVE SUMMARY

---

## Current State

RUNA is a .NET MAUI Android app providing navigation assistance for visually impaired users in Astana, Kazakhstan. It combines on-device computer vision (YOLOv8n for obstacle detection), OSRM-based routing, and OpenAI-powered voice commands. The app is **currently non-functional for real-world use** due to six critical software defects:

1. **Memory hemorrhage**: 10MB allocated and discarded per camera frame at 2fps, causing GC pauses and eventual out-of-memory crashes on the target 3-4GB RAM devices.
2. **Silent crashes**: `async void` event handlers swallow exceptions, making the app crash without diagnostics on Android.
3. **Muted navigation**: Turn-by-turn voice announcements are disabled, rendering navigation useless for blind users.
4. **Orphaned tasks**: Fire-and-forget async operations access disposed resources after app lifecycle changes.
5. **Corrupt collections**: Thread-unsafe data structures cause intermittent crashes when camera, GPS, and voice threads race.
6. **Network corruption**: Improperly shared HttpClient instances cause socket exhaustion and header corruption.

The backend runs on Firebase Cloud Functions with no database, no error tracking, and no cost controls. The OSRM routing depends on a public demo server with undocumented rate limits. There is no fallback mechanism if the device overheats or runs low on battery during a walking session.

---

## Target State

A stable, battery-efficient app that safely guides a visually impaired user through Astana streets for 1-hour walking sessions on a 3-4GB RAM Android device:

- **On-device CV at 2fps** (walking) / 0.5fps (stationary) using NNAPI-accelerated YOLOv8n, consuming ~100mW and ~80MB RAM with pre-allocated memory pools
- **Smart Offload** that automatically shifts CV processing to a backend Cloud Run service when the device exceeds 43C, drops below 25% battery, or inference slows beyond 250ms — with 3C/5%/100ms hysteresis for smooth return
- **Three-tier danger alerting**: immediate vibration + short TTS for critical objects (cars, buses, bicycles), center-60% filtering for static obstacles (benches, fire hydrants), and delayed backend descriptions for complex scenes
- **Reliable navigation** with turn announcements at 100m/50m/15m, off-route detection at 30m deviation, and voice-confirmed recalculation
- **Voice commands** via OpenAI Whisper with deterministic intent classification handling 80%+ of commands at zero GPT cost, falling back to GPT-4o-mini for ambiguous utterances
- **Backend on Google Cloud Run** (Go for Navigation/Voice APIs, Python for CV Fallback) with Cloud SQL PostgreSQL, costing $30-55/month during testing and $160-180/month at 100 users
- **Offline resilience**: CV stays active without network, navigation continues on cached route, voice gracefully degrades with clear user communication (max 2 "network lost" announcements)

---

## Critical Path (3-4 Day Test Deployment)

The test deployment has a 3-4 day window with 1 test user walking real streets. The critical path is:

| Day | Phase | Hours | Outcome |
|-----|-------|-------|---------|
| **Day 1** | Phase 1: Emergency Triage | 8-12h | App stops crashing. Memory stable. Navigation speaks turns. |
| **Day 2 AM** | Phase 2: Backend Infrastructure | 4-6h | Cloud Run services live. App connected to new backend. |
| **Day 2 PM** | Phase 6: Testing Instrumentation | 3-4h | Developer can monitor test user remotely. Error logs flow to database. |
| **Day 3** | Phase 5: Performance Optimization (critical items only) | 4-6h | NNAPI enabled. Adaptive frame rate. GPS optimized. Battery target achievable. |
| **Day 3-4** | **Test Walk** | 2-3h/day | 1 test user walks real Astana streets. Developer observes via telemetry overlay and remote logs. |

**Phases 3 (Helper Extraction) and 4 (Smart Offload) are deferred past the test deployment.** They improve architecture quality and add fallback capability, but the core MVP can function without them for a 3-4 day test. Smart Offload becomes essential before the 100-user launch.

**Minimum viable test criteria:**
- App runs for 30 minutes continuously without crashing
- Turn announcements are spoken at each navigation waypoint
- Obstacle detection speaks "car ahead" when a car is within 5 meters
- Voice command "navigate to Khan Shatyr" produces a valid route
- Battery drain < 15% per hour (total app)

---

## Expected Outcomes

### Test Phase (Days 3-4)

| Metric | Target | Measurement Method |
|--------|--------|--------------------|
| Crash-free session length | >30 minutes | Error log count = 0 critical crashes |
| Turn announcement accuracy | 100% of turns spoken | Developer observation + TTS log |
| Obstacle detection rate | >80% of cars within 5m detected | Developer walking alongside, counting |
| Voice command success rate | >70% of commands understood | Log intent classification results |
| Battery per hour | <15% | Android battery stats |
| User safety incidents | 0 | Developer observation |

### Post-Test Optimization (Weeks 2-4)

After incorporating test feedback, implement Phases 3-4:

| Metric | Target | Method |
|--------|--------|--------|
| Battery per hour | <10% | NNAPI + adaptive frame rate + GPS optimization |
| RAM sustained | <120MB | Memory pools + extracted services |
| Thermal resilience | No throttle in 1hr session, or graceful offload | Smart Offload state machine |
| Voice round-trip | <4s p50 | Optimized audio encoding + warm min-instance |
| Monthly cloud cost | <$55 (testing) / <$180 (100 users) | Go services + connection pooling + caching |

### Risk Factors

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| OSRM demo server goes down during test | Medium | High (no navigation) | Pre-cache 3-5 test routes before test day; have backup addresses ready |
| Target device doesn't support NNAPI for all YOLOv8n ops | Medium | Medium (slower inference, still works) | OnnxRuntime auto-falls back to CPU; test NNAPI on target device before test day |
| Kazakh TTS not available on test device | High | Low (fallback to Russian) | Detect at startup, default to Russian TTS |
| OpenAI API rate limit during test | Low | Medium (no voice) | Pre-test with 100 rapid requests to verify limits; have fallback commands ready |
| Astana winter conditions degrade CV accuracy | Medium | Medium (missed detections) | Lower confidence threshold to 0.25 during test; accept higher false positive rate |

---

*End of Implementation Plan*
