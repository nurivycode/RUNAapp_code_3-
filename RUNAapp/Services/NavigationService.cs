using System.Net.Http.Json;
using System.Text.Json;
using RUNAapp.Helpers;
using RUNAapp.Models;

namespace RUNAapp.Services;

/// <summary>
/// Navigation service using OSRM for routing and Nominatim for geocoding.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private CancellationTokenSource? _navigationCts;
    private Timer? _locationTimer;
    
    public NavigationRoute? CurrentRoute { get; private set; }
    public bool IsNavigating { get; private set; }
    public int CurrentStepIndex { get; private set; }
    
    public event EventHandler<NavigationEventArgs>? NavigationUpdated;
    
    public NavigationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RUNA Navigation App");
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }
    
    public async Task<GeoCoordinate?> GetCurrentLocationAsync()
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
            var location = await Geolocation.Default.GetLocationAsync(request);
            
            if (location != null)
            {
                return new GeoCoordinate(location.Latitude, location.Longitude);
            }
            
            return null;
        }
        catch (FeatureNotSupportedException)
        {
            throw new NavigationException("GPS is not supported on this device.");
        }
        catch (FeatureNotEnabledException)
        {
            throw new NavigationException("Please enable GPS/Location services.");
        }
        catch (PermissionException)
        {
            throw new NavigationException("Location permission is required.");
        }
        catch (Exception ex)
        {
            throw new NavigationException($"Unable to get location: {ex.Message}");
        }
    }
    
    public async Task<List<GeocodingResult>> SearchLocationAsync(string query)
    {
        var url = $"{Constants.NominatimBaseUrl}{Constants.NominatimSearchEndpoint}" +
                  $"?q={Uri.EscapeDataString(query)}&format=json&limit=5&addressdetails=1";
        
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
            throw new NavigationException($"Geocoding failed: {response.StatusCode}");
        
        var content = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<List<NominatimResult>>(content, _jsonOptions);
        
        return results?.Select(r => new GeocodingResult
        {
            DisplayName = r.DisplayName,
            Location = new GeoCoordinate(
                double.Parse(r.Lat), 
                double.Parse(r.Lon)),
            Type = r.Type,
            Importance = r.Importance
        }).ToList() ?? new List<GeocodingResult>();
    }
    
    public async Task<string> GetAddressAsync(GeoCoordinate coordinate)
    {
        var url = $"{Constants.NominatimBaseUrl}{Constants.NominatimReverseEndpoint}" +
                  $"?lat={coordinate.Latitude}&lon={coordinate.Longitude}&format=json";
        
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
            return "Unknown location";
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<NominatimResult>(content, _jsonOptions);
        
        return result?.DisplayName ?? "Unknown location";
    }
    
    public async Task<NavigationRoute> CalculateRouteAsync(GeoCoordinate origin, GeoCoordinate destination)
    {
        // OSRM expects coordinates in lon,lat format
        var url = $"{Constants.OsrmBaseUrl}{Constants.OsrmRouteEndpoint}/" +
                  $"{origin.Longitude},{origin.Latitude};" +
                  $"{destination.Longitude},{destination.Latitude}" +
                  "?overview=full&geometries=polyline&steps=true";
        
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
            throw new NavigationException($"Route calculation failed: {response.StatusCode}");
        
        var content = await response.Content.ReadAsStringAsync();
        var osrmResponse = JsonSerializer.Deserialize<OsrmRouteResponse>(content, _jsonOptions);
        
        if (osrmResponse?.Code != "Ok" || osrmResponse.Routes.Count == 0)
            throw new NavigationException("No route found to destination.");
        
        var osrmRoute = osrmResponse.Routes[0];
        
        var route = new NavigationRoute
        {
            Origin = origin,
            Destination = destination,
            DistanceMeters = osrmRoute.Distance,
            DurationSeconds = osrmRoute.Duration,
            EncodedPolyline = osrmRoute.Geometry
        };
        
        // Parse steps
        int stepNum = 1;
        foreach (var leg in osrmRoute.Legs)
        {
            foreach (var step in leg.Steps)
            {
                var maneuverType = $"{step.Maneuver.Type}" +
                    (string.IsNullOrEmpty(step.Maneuver.Modifier) ? "" : $"-{step.Maneuver.Modifier}");
                
                route.Steps.Add(new RouteStep
                {
                    StepNumber = stepNum++,
                    Instruction = GenerateInstruction(step),
                    DistanceMeters = step.Distance,
                    DurationSeconds = step.Duration,
                    ManeuverType = maneuverType,
                    Location = new GeoCoordinate(
                        step.Maneuver.Location[1], 
                        step.Maneuver.Location[0]),
                    StreetName = string.IsNullOrEmpty(step.Name) ? null : step.Name
                });
            }
        }
        
        // Decode polyline for map display
        route.RoutePoints = DecodePolyline(osrmRoute.Geometry);
        
        return route;
    }
    
    public async Task<NavigationRoute> CalculateRouteToAsync(string destinationName)
    {
        // Get current location
        var currentLocation = await GetCurrentLocationAsync();
        if (currentLocation == null)
            throw new NavigationException("Unable to get current location.");
        
        // Search for destination
        var searchResults = await SearchLocationAsync(destinationName);
        if (searchResults.Count == 0)
            throw new NavigationException($"Could not find '{destinationName}'.");
        
        var destination = searchResults[0];
        
        // Calculate route
        var route = await CalculateRouteAsync(currentLocation, destination.Location);
        route.DestinationName = destination.DisplayName;
        
        return route;
    }
    
    public void StartNavigation(NavigationRoute route)
    {
        StopNavigation();
        
        CurrentRoute = route;
        CurrentStepIndex = 0;
        IsNavigating = true;
        _navigationCts = new CancellationTokenSource();
        
        // Start location tracking
        _locationTimer = new Timer(async _ => await UpdateNavigationAsync(), 
            null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        
        NavigationUpdated?.Invoke(this, new NavigationEventArgs
        {
            Route = route,
            CurrentStepIndex = 0,
            Instruction = route.Steps.FirstOrDefault()?.VoiceInstruction
        });
    }
    
    public void StopNavigation()
    {
        _navigationCts?.Cancel();
        _locationTimer?.Dispose();
        _locationTimer = null;
        CurrentRoute = null;
        IsNavigating = false;
        CurrentStepIndex = 0;
    }
    
    private async Task UpdateNavigationAsync()
    {
        if (!IsNavigating || CurrentRoute == null)
            return;
        
        try
        {
            var currentLocation = await GetCurrentLocationAsync();
            if (currentLocation == null)
                return;
            
            // Check distance to destination
            var distanceToDestination = currentLocation.DistanceTo(CurrentRoute.Destination);
            if (distanceToDestination < 20) // Within 20 meters of destination
            {
                NavigationUpdated?.Invoke(this, new NavigationEventArgs
                {
                    Route = CurrentRoute,
                    CurrentStepIndex = CurrentStepIndex,
                    CurrentLocation = currentLocation,
                    HasArrived = true,
                    Instruction = "You have arrived at your destination."
                });
                
                StopNavigation();
                return;
            }
            
            // Check if we should advance to next step
            if (CurrentStepIndex < CurrentRoute.Steps.Count - 1)
            {
                var nextStep = CurrentRoute.Steps[CurrentStepIndex + 1];
                var distanceToNextStep = currentLocation.DistanceTo(nextStep.Location);
                
                if (distanceToNextStep < 15) // Within 15 meters of next step
                {
                    CurrentStepIndex++;
                }
            }
            
            var currentStep = CurrentRoute.Steps[CurrentStepIndex];
            var distanceToStep = currentLocation.DistanceTo(currentStep.Location);
            
            NavigationUpdated?.Invoke(this, new NavigationEventArgs
            {
                Route = CurrentRoute,
                CurrentStepIndex = CurrentStepIndex,
                CurrentLocation = currentLocation,
                DistanceToNextStep = distanceToStep,
                Instruction = currentStep.VoiceInstruction
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation update error: {ex.Message}");
        }
    }
    
    private static string GenerateInstruction(OsrmStep step)
    {
        var direction = step.Maneuver.Modifier switch
        {
            "left" => "left",
            "right" => "right",
            "slight left" => "slightly left",
            "slight right" => "slightly right",
            "sharp left" => "sharp left",
            "sharp right" => "sharp right",
            "uturn" => "around",
            _ => ""
        };
        
        var action = step.Maneuver.Type switch
        {
            "turn" => $"Turn {direction}",
            "depart" => "Start heading",
            "arrive" => "You have arrived",
            "merge" => $"Merge {direction}",
            "fork" => $"Take the {direction} fork",
            "roundabout" => $"Take the roundabout",
            _ => $"Continue {direction}"
        };
        
        if (!string.IsNullOrEmpty(step.Name))
        {
            action += $" onto {step.Name}";
        }
        
        return action;
    }
    
    /// <summary>
    /// Decodes a Google-encoded polyline string to coordinates.
    /// </summary>
    private static List<GeoCoordinate> DecodePolyline(string encoded)
    {
        var poly = new List<GeoCoordinate>();
        int index = 0;
        int lat = 0;
        int lng = 0;
        
        while (index < encoded.Length)
        {
            int b;
            int shift = 0;
            int result = 0;
            
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            
            int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lat += dlat;
            
            shift = 0;
            result = 0;
            
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            
            int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lng += dlng;
            
            poly.Add(new GeoCoordinate(lat / 1e5, lng / 1e5));
        }
        
        return poly;
    }
}

/// <summary>
/// Nominatim geocoding result model.
/// </summary>
public class NominatimResult
{
    public string Lat { get; set; } = string.Empty;
    public string Lon { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Importance { get; set; }
}

/// <summary>
/// Custom exception for navigation errors.
/// </summary>
public class NavigationException : Exception
{
    public NavigationException(string message) : base(message) { }
    public NavigationException(string message, Exception inner) : base(message, inner) { }
}
