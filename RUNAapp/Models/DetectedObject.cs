namespace RUNAapp.Models;

/// <summary>
/// Represents an object detected by the computer vision model.
/// </summary>
public class DetectedObject
{
    /// <summary>
    /// The class label of the detected object (e.g., "person", "car", "bicycle").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score of the detection (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Bounding box coordinates (normalized 0-1 or pixel values).
    /// </summary>
    public BoundingBox BoundingBox { get; set; } = new();

    /// <summary>
    /// Estimated distance from the camera (if available).
    /// </summary>
    public float? EstimatedDistance { get; set; }

    /// <summary>
    /// Danger level assessment.
    /// </summary>
    public DangerLevel DangerLevel { get; set; } = DangerLevel.Low;

    /// <summary>
    /// Position relative to the user.
    /// </summary>
    public RelativePosition Position { get; set; } = RelativePosition.Center;

    /// <summary>
    /// Timestamp of detection.
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Bounding box coordinates for detected objects.
/// </summary>
public class BoundingBox
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    /// <summary>
    /// Gets the center X coordinate.
    /// </summary>
    public float CenterX => X + Width / 2;

    /// <summary>
    /// Gets the center Y coordinate.
    /// </summary>
    public float CenterY => Y + Height / 2;

    /// <summary>
    /// Gets the area of the bounding box.
    /// </summary>
    public float Area => Width * Height;
}

/// <summary>
/// Danger level for detected objects.
/// </summary>
public enum DangerLevel
{
    Low,      // No immediate concern
    Medium,   // User should be aware
    High,     // Approaching danger
    Critical  // Immediate action required
}

/// <summary>
/// Relative position of object to the user.
/// </summary>
public enum RelativePosition
{
    Left,
    Center,
    Right,
    Above,
    Below
}

/// <summary>
/// Classes of objects that the model can detect.
/// Focused on safety-relevant objects for navigation.
/// </summary>
public static class DetectionClasses
{
    // People
    public const string Person = "person";
    
    // Vehicles (high danger)
    public const string Car = "car";
    public const string Truck = "truck";
    public const string Bus = "bus";
    public const string Motorcycle = "motorcycle";
    public const string Bicycle = "bicycle";
    
    // Animals
    public const string Dog = "dog";
    public const string Cat = "cat";
    
    // Urban obstacles
    public const string TrafficLight = "traffic light";
    public const string StopSign = "stop sign";
    public const string FireHydrant = "fire hydrant";
    public const string Bench = "bench";
    
    /// <summary>
    /// High-priority objects that require immediate alerts.
    /// </summary>
    public static readonly string[] HighPriorityClasses = 
    {
        Car, Truck, Bus, Motorcycle, Bicycle, Person
    };
    
    /// <summary>
    /// Gets the danger level for a given object class.
    /// </summary>
    public static DangerLevel GetBaseDangerLevel(string label)
    {
        return label.ToLower() switch
        {
            Car or Truck or Bus => DangerLevel.High,
            Motorcycle or Bicycle => DangerLevel.Medium,
            Person => DangerLevel.Medium,
            Dog => DangerLevel.Medium,
            _ => DangerLevel.Low
        };
    }
}
