namespace RUNAapp.Services;

/// <summary>
/// Interface for providing real-time camera frames for computer vision processing.
/// Platform-specific implementations handle camera access and frame conversion.
/// </summary>
public interface ICameraFrameProvider
{
    /// <summary>
    /// Gets whether the camera is currently active and providing frames.
    /// </summary>
    bool IsActive { get; }
    
    /// <summary>
    /// Event fired when a new camera frame is available for processing.
    /// Parameters: frame bytes (RGBA format), width, height
    /// </summary>
    event EventHandler<FrameAvailableEventArgs>? FrameAvailable;
    
    /// <summary>
    /// Starts the camera and begins providing frames.
    /// </summary>
    Task StartAsync();
    
    /// <summary>
    /// Stops the camera and stops providing frames.
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// Event arguments for frame availability.
/// </summary>
public class FrameAvailableEventArgs : EventArgs
{
    public byte[] FrameBytes { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
}
