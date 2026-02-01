namespace RUNAapp.Models;

/// <summary>
/// Represents a navigation route from origin to destination.
/// </summary>
public class NavigationRoute
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Starting location coordinates.
    /// </summary>
    public GeoCoordinate Origin { get; set; } = new();
    
    /// <summary>
    /// Destination location coordinates.
    /// </summary>
    public GeoCoordinate Destination { get; set; } = new();
    
    /// <summary>
    /// Human-readable destination name.
    /// </summary>
    public string DestinationName { get; set; } = string.Empty;
    
    /// <summary>
    /// Total distance in meters.
    /// </summary>
    public double DistanceMeters { get; set; }
    
    /// <summary>
    /// Estimated duration in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }
    
    /// <summary>
    /// List of route steps/instructions.
    /// </summary>
    public List<RouteStep> Steps { get; set; } = new();
    
    /// <summary>
    /// Encoded polyline for the route (for map display).
    /// </summary>
    public string? EncodedPolyline { get; set; }
    
    /// <summary>
    /// Decoded route coordinates.
    /// </summary>
    public List<GeoCoordinate> RoutePoints { get; set; } = new();
    
    /// <summary>
    /// When the route was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets formatted distance string.
    /// </summary>
    public string FormattedDistance
    {
        get
        {
            if (DistanceMeters < 1000)
                return $"{DistanceMeters:F0} meters";
            return $"{DistanceMeters / 1000:F1} kilometers";
        }
    }
    
    /// <summary>
    /// Gets formatted duration string.
    /// </summary>
    public string FormattedDuration
    {
        get
        {
            var span = TimeSpan.FromSeconds(DurationSeconds);
            if (span.TotalHours >= 1)
                return $"{span.Hours} hours {span.Minutes} minutes";
            return $"{span.Minutes} minutes";
        }
    }
}

/// <summary>
/// Represents a single step in a navigation route.
/// </summary>
public class RouteStep
{
    /// <summary>
    /// Step number in the route.
    /// </summary>
    public int StepNumber { get; set; }
    
    /// <summary>
    /// Navigation instruction (e.g., "Turn left on Main Street").
    /// </summary>
    public string Instruction { get; set; } = string.Empty;
    
    /// <summary>
    /// Distance for this step in meters.
    /// </summary>
    public double DistanceMeters { get; set; }
    
    /// <summary>
    /// Duration for this step in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }
    
    /// <summary>
    /// Maneuver type (e.g., "turn-left", "turn-right", "straight").
    /// </summary>
    public string ManeuverType { get; set; } = string.Empty;
    
    /// <summary>
    /// Location where this step starts.
    /// </summary>
    public GeoCoordinate Location { get; set; } = new();
    
    /// <summary>
    /// Street/road name if available.
    /// </summary>
    public string? StreetName { get; set; }
    
    /// <summary>
    /// Voice-friendly instruction for TTS.
    /// </summary>
    public string VoiceInstruction => GenerateVoiceInstruction();
    
    private string GenerateVoiceInstruction()
    {
        var distance = DistanceMeters < 100 
            ? $"{DistanceMeters:F0} meters" 
            : $"{DistanceMeters / 100:F0} hundred meters";
            
        return ManeuverType.ToLower() switch
        {
            "turn-left" => $"In {distance}, turn left{(StreetName != null ? $" onto {StreetName}" : "")}",
            "turn-right" => $"In {distance}, turn right{(StreetName != null ? $" onto {StreetName}" : "")}",
            "straight" => $"Continue straight for {distance}",
            "arrive" => "You have arrived at your destination",
            "depart" => $"Start by heading {distance}",
            _ => Instruction
        };
    }
}

/// <summary>
/// Geographic coordinate.
/// </summary>
public class GeoCoordinate
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    public GeoCoordinate() { }
    
    public GeoCoordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }
    
    /// <summary>
    /// Calculates distance to another coordinate in meters using Haversine formula.
    /// </summary>
    public double DistanceTo(GeoCoordinate other)
    {
        const double EarthRadiusMeters = 6371000;
        
        var lat1Rad = Latitude * Math.PI / 180;
        var lat2Rad = other.Latitude * Math.PI / 180;
        var deltaLat = (other.Latitude - Latitude) * Math.PI / 180;
        var deltaLon = (other.Longitude - Longitude) * Math.PI / 180;
        
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
                
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return EarthRadiusMeters * c;
    }
    
    public override string ToString() => $"{Latitude:F6}, {Longitude:F6}";
}

/// <summary>
/// OSRM route response model.
/// </summary>
public class OsrmRouteResponse
{
    public string Code { get; set; } = string.Empty;
    public List<OsrmRoute> Routes { get; set; } = new();
    public List<OsrmWaypoint> Waypoints { get; set; } = new();
}

public class OsrmRoute
{
    public string Geometry { get; set; } = string.Empty;
    public List<OsrmLeg> Legs { get; set; } = new();
    public double Distance { get; set; }
    public double Duration { get; set; }
}

public class OsrmLeg
{
    public List<OsrmStep> Steps { get; set; } = new();
    public double Distance { get; set; }
    public double Duration { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class OsrmStep
{
    public string Geometry { get; set; } = string.Empty;
    public OsrmManeuver Maneuver { get; set; } = new();
    public double Distance { get; set; }
    public double Duration { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
}

public class OsrmManeuver
{
    public string Type { get; set; } = string.Empty;
    public string Modifier { get; set; } = string.Empty;
    public List<double> Location { get; set; } = new();
    public int BearingBefore { get; set; }
    public int BearingAfter { get; set; }
}

public class OsrmWaypoint
{
    public string Name { get; set; } = string.Empty;
    public List<double> Location { get; set; } = new();
    public double Distance { get; set; }
}
