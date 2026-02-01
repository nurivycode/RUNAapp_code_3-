using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using RUNAapp.Helpers;
using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Computer vision service using ONNX Runtime for object detection.
/// Optimized for offline operation on mobile devices.
/// </summary>
public class ComputerVisionService : IComputerVisionService, IDisposable
{
    private InferenceSession? _session;
    private readonly object _sessionLock = new();
    private DateTime _lastDetectionTime = DateTime.MinValue;
    private readonly HashSet<string> _recentAlerts = new();
    private Timer? _alertCooldownTimer;
    
    public bool IsModelLoaded => _session != null;
    public bool IsDetecting { get; private set; }
    
    public event EventHandler<DetectionEventArgs>? ObjectsDetected;
    public event EventHandler<DetectedObject>? DangerAlert;
    
    // COCO class labels (for YOLOv8 or similar models)
    private static readonly string[] CocoLabels = 
    {
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck",
        "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench",
        "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra",
        "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
        "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove",
        "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup",
        "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
        "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch",
        "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse",
        "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink",
        "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier",
        "toothbrush"
    };
    
    public async Task LoadModelAsync()
    {
        if (_session != null)
            return;
        
        try
        {
            // Load model from app resources
            var modelPath = await GetModelPathAsync();
            
            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
            {
                System.Diagnostics.Debug.WriteLine("ONNX model not found. CV features will be limited.");
                return;
            }
            
            var options = new SessionOptions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            
            // Use CPU execution provider (most compatible)
            // For production, consider NNAPI for Android
            lock (_sessionLock)
            {
                _session = new InferenceSession(modelPath, options);
            }
            
            System.Diagnostics.Debug.WriteLine("ONNX model loaded successfully.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading ONNX model: {ex.Message}");
            throw new ComputerVisionException($"Failed to load model: {ex.Message}");
        }
    }
    
    public void StartDetection()
    {
        if (!IsModelLoaded)
        {
            System.Diagnostics.Debug.WriteLine("Cannot start detection: model not loaded.");
            return;
        }
        
        IsDetecting = true;
        _recentAlerts.Clear();
        
        // Clear alert cooldowns every 3 seconds
        _alertCooldownTimer = new Timer(_ => 
        {
            _recentAlerts.Clear();
        }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }
    
    public void StopDetection()
    {
        IsDetecting = false;
        _alertCooldownTimer?.Dispose();
        _alertCooldownTimer = null;
        _recentAlerts.Clear();
    }
    
    public async Task<List<DetectedObject>> ProcessFrameAsync(byte[] imageData, int width, int height)
    {
        if (!IsModelLoaded || !IsDetecting)
            return new List<DetectedObject>();
        
        // Rate limiting - process at most every N ms
        var now = DateTime.UtcNow;
        if ((now - _lastDetectionTime).TotalMilliseconds < Constants.FrameProcessingIntervalMs)
            return new List<DetectedObject>();
        
        _lastDetectionTime = now;
        
        return await Task.Run(() => RunInference(imageData, width, height));
    }
    
    private List<DetectedObject> RunInference(byte[] imageData, int width, int height)
    {
        var detectedObjects = new List<DetectedObject>();
        
        try
        {
            if (_session == null)
                return detectedObjects;
            
            // Preprocess image to tensor
            var inputTensor = PreprocessImage(imageData, width, height);
            
            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", inputTensor)
            };
            
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
            lock (_sessionLock)
            {
                if (_session == null)
                    return detectedObjects;
                results = _session.Run(inputs);
            }
            
            // Parse output
            var output = results.First().AsTensor<float>();
            detectedObjects = ParseYoloOutput(output, width, height);
            
            results.Dispose();
            
            // Apply danger assessment
            foreach (var obj in detectedObjects)
            {
                AssessDanger(obj);
            }
            
            // Fire events
            if (detectedObjects.Count > 0)
            {
                var description = GetDetectionDescription(detectedObjects);
                
                ObjectsDetected?.Invoke(this, new DetectionEventArgs
                {
                    DetectedObjects = detectedObjects,
                    Description = description
                });
                
                // Check for danger alerts
                foreach (var obj in detectedObjects.Where(o => 
                    o.DangerLevel >= DangerLevel.High && 
                    !_recentAlerts.Contains(o.Label)))
                {
                    _recentAlerts.Add(obj.Label);
                    DangerAlert?.Invoke(this, obj);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Inference error: {ex.Message}");
        }
        
        return detectedObjects;
    }
    
    private DenseTensor<float> PreprocessImage(byte[] imageData, int width, int height)
    {
        // Create input tensor [1, 3, 640, 640] for YOLOv8
        var inputSize = Constants.ModelInputSize;
        var tensor = new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize });
        
        // Simple resize and normalize
        // In production, use proper image processing library
        var scaleX = (float)width / inputSize;
        var scaleY = (float)height / inputSize;
        
        for (int y = 0; y < inputSize; y++)
        {
            for (int x = 0; x < inputSize; x++)
            {
                var srcX = (int)(x * scaleX);
                var srcY = (int)(y * scaleY);
                var idx = (srcY * width + srcX) * 4; // RGBA
                
                if (idx + 2 < imageData.Length)
                {
                    // Normalize to 0-1 and assign to RGB channels
                    tensor[0, 0, y, x] = imageData[idx] / 255f;     // R
                    tensor[0, 1, y, x] = imageData[idx + 1] / 255f; // G
                    tensor[0, 2, y, x] = imageData[idx + 2] / 255f; // B
                }
            }
        }
        
        return tensor;
    }
    
    private List<DetectedObject> ParseYoloOutput(Tensor<float> output, int originalWidth, int originalHeight)
    {
        var detections = new List<DetectedObject>();
        
        // YOLOv8 output shape: [1, 84, 8400] or similar
        // 84 = 4 bbox coords + 80 class probabilities
        
        var dimensions = output.Dimensions.ToArray();
        if (dimensions.Length < 2)
            return detections;
        
        var numClasses = 80;
        var numDetections = dimensions[^1];
        
        for (int i = 0; i < numDetections; i++)
        {
            // Get best class
            float maxProb = 0;
            int maxClass = 0;
            
            for (int c = 0; c < numClasses; c++)
            {
                var prob = output[0, 4 + c, i];
                if (prob > maxProb)
                {
                    maxProb = prob;
                    maxClass = c;
                }
            }
            
            if (maxProb < Constants.DetectionConfidenceThreshold)
                continue;
            
            // Get bounding box (center x, center y, width, height)
            var cx = output[0, 0, i];
            var cy = output[0, 1, i];
            var w = output[0, 2, i];
            var h = output[0, 3, i];
            
            // Convert to corner format and scale
            var scaleX = originalWidth / (float)Constants.ModelInputSize;
            var scaleY = originalHeight / (float)Constants.ModelInputSize;
            
            var detection = new DetectedObject
            {
                Label = maxClass < CocoLabels.Length ? CocoLabels[maxClass] : $"class_{maxClass}",
                Confidence = maxProb,
                BoundingBox = new BoundingBox
                {
                    X = (cx - w / 2) * scaleX,
                    Y = (cy - h / 2) * scaleY,
                    Width = w * scaleX,
                    Height = h * scaleY
                }
            };
            
            // Determine relative position
            var centerX = detection.BoundingBox.CenterX / originalWidth;
            detection.Position = centerX < 0.33f ? RelativePosition.Left :
                                 centerX > 0.66f ? RelativePosition.Right :
                                 RelativePosition.Center;
            
            // Only include safety-relevant objects
            if (DetectionClasses.HighPriorityClasses.Contains(detection.Label) ||
                detection.BoundingBox.Area > originalWidth * originalHeight * 0.1f)
            {
                detections.Add(detection);
            }
        }
        
        // Non-max suppression (simple version)
        detections = ApplyNMS(detections, 0.5f);
        
        return detections;
    }
    
    private static List<DetectedObject> ApplyNMS(List<DetectedObject> detections, float iouThreshold)
    {
        var result = new List<DetectedObject>();
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();
        
        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);
            
            sorted = sorted.Where(d => 
                d.Label != best.Label || 
                IoU(d.BoundingBox, best.BoundingBox) < iouThreshold
            ).ToList();
        }
        
        return result;
    }
    
    private static float IoU(BoundingBox a, BoundingBox b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        
        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var union = a.Area + b.Area - intersection;
        
        return union > 0 ? intersection / union : 0;
    }
    
    private void AssessDanger(DetectedObject obj)
    {
        // Base danger from object type
        obj.DangerLevel = DetectionClasses.GetBaseDangerLevel(obj.Label);
        
        // Increase danger for large/close objects
        if (obj.BoundingBox.Area > 0.3f * Constants.ModelInputSize * Constants.ModelInputSize)
        {
            obj.DangerLevel = obj.DangerLevel < DangerLevel.Critical 
                ? (DangerLevel)((int)obj.DangerLevel + 1) 
                : DangerLevel.Critical;
        }
        
        // Center objects are more dangerous (directly in path)
        if (obj.Position == RelativePosition.Center && obj.DangerLevel >= DangerLevel.Medium)
        {
            obj.DangerLevel = obj.DangerLevel < DangerLevel.Critical
                ? (DangerLevel)((int)obj.DangerLevel + 1)
                : DangerLevel.Critical;
        }
    }
    
    public string GetDetectionDescription(List<DetectedObject> detectedObjects)
    {
        if (detectedObjects.Count == 0)
            return "No obstacles detected.";
        
        var descriptions = new List<string>();
        
        // Group by danger level
        var critical = detectedObjects.Where(o => o.DangerLevel == DangerLevel.Critical).ToList();
        var high = detectedObjects.Where(o => o.DangerLevel == DangerLevel.High).ToList();
        var others = detectedObjects.Where(o => o.DangerLevel < DangerLevel.High).ToList();
        
        if (critical.Any())
        {
            descriptions.Add($"Warning! {DescribeObjects(critical)} directly ahead!");
        }
        
        if (high.Any())
        {
            descriptions.Add($"Caution: {DescribeObjects(high)} nearby.");
        }
        
        if (others.Any() && descriptions.Count == 0)
        {
            descriptions.Add($"Detected: {DescribeObjects(others)}.");
        }
        
        return string.Join(" ", descriptions);
    }
    
    private static string DescribeObjects(List<DetectedObject> objects)
    {
        var grouped = objects.GroupBy(o => o.Label)
            .Select(g => g.Count() > 1 ? $"{g.Count()} {g.Key}s" : $"a {g.Key}");
        
        var list = grouped.ToList();
        
        return list.Count switch
        {
            0 => "nothing",
            1 => list[0],
            2 => $"{list[0]} and {list[1]}",
            _ => $"{string.Join(", ", list.Take(list.Count - 1))}, and {list.Last()}"
        };
    }
    
    private async Task<string> GetModelPathAsync()
    {
        try
        {
            // Copy model from app package to cache for ONNX Runtime access
            var cacheDir = FileSystem.Current.CacheDirectory;
            var modelPath = Path.Combine(cacheDir, Constants.OnnxModelFileName);
            
            if (!File.Exists(modelPath))
            {
                // Try to load from app resources
                using var stream = await FileSystem.Current.OpenAppPackageFileAsync(Constants.OnnxModelFileName);
                using var fileStream = File.Create(modelPath);
                await stream.CopyToAsync(fileStream);
            }
            
            return modelPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting model path: {ex.Message}");
            return string.Empty;
        }
    }
    
    public void Dispose()
    {
        StopDetection();
        lock (_sessionLock)
        {
            _session?.Dispose();
            _session = null;
        }
    }
}

/// <summary>
/// Custom exception for computer vision errors.
/// </summary>
public class ComputerVisionException : Exception
{
    public ComputerVisionException(string message) : base(message) { }
    public ComputerVisionException(string message, Exception inner) : base(message, inner) { }
}
