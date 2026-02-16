using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Navigation service interface for route calculation and location services.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets the current GPS location.
    /// </summary>
    Task<GeoCoordinate?> GetCurrentLocationAsync();
    
    /// <summary>
    /// Searches for a location by name using Nominatim geocoding.
    /// </summary>
    /// <param name="query">Search query (address, place name, etc.).</param>
    /// <returns>List of matching locations.</returns>
    Task<List<GeocodingResult>> SearchLocationAsync(string query, GeoCoordinate? userLocation = null);
    
    /// <summary>
    /// Gets address information for coordinates (reverse geocoding).
    /// </summary>
    /// <param name="coordinate">Location coordinates.</param>
    /// <returns>Address information.</returns>
    Task<string> GetAddressAsync(GeoCoordinate coordinate);
    
    /// <summary>
    /// Calculates a walking route from origin to destination using OSRM.
    /// </summary>
    /// <param name="origin">Starting point.</param>
    /// <param name="destination">End point.</param>
    /// <returns>Calculated route.</returns>
    Task<NavigationRoute> CalculateRouteAsync(GeoCoordinate origin, GeoCoordinate destination);
    
    /// <summary>
    /// Calculates a route from current location to a named destination.
    /// </summary>
    /// <param name="destinationName">Name of the destination.</param>
    /// <returns>Calculated route.</returns>
    Task<NavigationRoute> CalculateRouteToAsync(string destinationName);
    
    /// <summary>
    /// Starts active navigation with location tracking.
    /// </summary>
    /// <param name="route">The route to navigate.</param>
    void StartNavigation(NavigationRoute route);
    
    /// <summary>
    /// Stops active navigation.
    /// </summary>
    void StopNavigation();
    
    /// <summary>
    /// Gets the current navigation route.
    /// </summary>
    NavigationRoute? CurrentRoute { get; }
    
    /// <summary>
    /// Gets whether navigation is currently active.
    /// </summary>
    bool IsNavigating { get; }
    
    /// <summary>
    /// Gets the current step in navigation.
    /// </summary>
    int CurrentStepIndex { get; }
    
    /// <summary>
    /// Event fired when navigation state changes.
    /// </summary>
    event EventHandler<NavigationEventArgs>? NavigationUpdated;
}

/// <summary>
/// Geocoding result from Nominatim.
/// </summary>
public class GeocodingResult
{
    public string DisplayName { get; set; } = string.Empty;
    public GeoCoordinate Location { get; set; } = new();
    public string Type { get; set; } = string.Empty;
    public double Importance { get; set; }
}

/// <summary>
/// Event arguments for navigation updates.
/// </summary>
public class NavigationEventArgs : EventArgs
{
    public NavigationRoute? Route { get; set; }
    public int CurrentStepIndex { get; set; }
    public GeoCoordinate? CurrentLocation { get; set; }
    public double DistanceToNextStep { get; set; }
    public string? Instruction { get; set; }
    public bool HasArrived { get; set; }
}
