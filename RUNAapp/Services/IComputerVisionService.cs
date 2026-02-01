using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Computer vision service interface for object detection.
/// Uses ONNX Runtime for offline inference.
/// </summary>
public interface IComputerVisionService
{
    /// <summary>
    /// Gets whether the CV model is loaded and ready.
    /// </summary>
    bool IsModelLoaded { get; }
    
    /// <summary>
    /// Gets whether detection is currently active.
    /// </summary>
    bool IsDetecting { get; }
    
    /// <summary>
    /// Loads the ONNX model.
    /// </summary>
    Task LoadModelAsync();
    
    /// <summary>
    /// Starts continuous object detection from camera feed.
    /// </summary>
    void StartDetection();
    
    /// <summary>
    /// Stops object detection.
    /// </summary>
    void StopDetection();
    
    /// <summary>
    /// Processes a single camera frame for object detection.
    /// </summary>
    /// <param name="imageData">Raw image bytes.</param>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <returns>List of detected objects.</returns>
    Task<List<DetectedObject>> ProcessFrameAsync(byte[] imageData, int width, int height);
    
    /// <summary>
    /// Gets a spoken description of detected objects.
    /// </summary>
    /// <param name="detectedObjects">Objects to describe.</param>
    /// <returns>Human-readable description.</returns>
    string GetDetectionDescription(List<DetectedObject> detectedObjects);
    
    /// <summary>
    /// Event fired when objects are detected.
    /// </summary>
    event EventHandler<DetectionEventArgs>? ObjectsDetected;
    
    /// <summary>
    /// Event fired when a high-danger object is detected.
    /// </summary>
    event EventHandler<DetectedObject>? DangerAlert;
}

/// <summary>
/// Event arguments for object detection.
/// </summary>
public class DetectionEventArgs : EventArgs
{
    public List<DetectedObject> DetectedObjects { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
}
