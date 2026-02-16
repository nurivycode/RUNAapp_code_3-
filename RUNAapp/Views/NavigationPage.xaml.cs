using System.ComponentModel;
using System.Linq;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using NetTopologySuite.Geometries;
using RUNAapp.Models;
using RUNAapp.ViewModels;

namespace RUNAapp.Views;

public partial class NavigationPage : ContentPage
{
    private const string RouteLayerName = "active-route";
    private const string CurrentLocationLayerName = "current-location";
    private const string DestinationLayerName = "destination-location";
    private readonly NavigationViewModel _viewModel;
    private string? _renderedRouteId;
    private bool _hasCenteredContext;

    public NavigationPage(NavigationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        InitializeMap();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.InitializeCommand.ExecuteAsync(null);
        RefreshMap();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private void InitializeMap()
    {
        var map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        NavigationMap.Map = map;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NavigationViewModel.CurrentRoute) &&
            e.PropertyName != nameof(NavigationViewModel.CurrentLocation) &&
            e.PropertyName != nameof(NavigationViewModel.IsNavigating))
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(RefreshMap);
    }

    private void RefreshMap()
    {
        var map = NavigationMap.Map;
        if (map == null)
            return;

        var route = _viewModel.CurrentRoute;
        if (route?.Id != _renderedRouteId)
        {
            RemoveLayerByName(map, RouteLayerName);

            if (route != null && route.RoutePoints.Count > 1)
            {
                map.Layers.Add(CreateRouteLayer(route));
                _renderedRouteId = route.Id;
                _hasCenteredContext = false;
            }
            else
            {
                _renderedRouteId = null;
                _hasCenteredContext = false;
            }
        }

        RemoveLayerByName(map, CurrentLocationLayerName);
        RemoveLayerByName(map, DestinationLayerName);

        if (_viewModel.CurrentLocation != null)
        {
            map.Layers.Add(CreatePointLayer(
                CurrentLocationLayerName,
                _viewModel.CurrentLocation,
                Mapsui.Styles.Color.Green));
        }

        if (route != null)
        {
            map.Layers.Add(CreatePointLayer(
                DestinationLayerName,
                route.Destination,
                Mapsui.Styles.Color.Red));
        }

        var focusCoordinate = _viewModel.CurrentLocation ?? route?.Origin ?? route?.Destination;
        if (focusCoordinate != null && !_hasCenteredContext)
        {
            CenterMap(focusCoordinate);
            _hasCenteredContext = true;
        }

        if (_viewModel.IsNavigating && _viewModel.CurrentLocation != null)
        {
            CenterMap(_viewModel.CurrentLocation);
        }
    }

    private static void RemoveLayerByName(Mapsui.Map map, string layerName)
    {
        var layer = map.Layers.FirstOrDefault(l => l.Name == layerName);
        if (layer != null)
        {
            map.Layers.Remove(layer);
        }
    }

    private static ILayer CreateRouteLayer(NavigationRoute route)
    {
        var coordinates = route.RoutePoints
            .Select(point =>
            {
                var projected = SphericalMercator.FromLonLat(point.Longitude, point.Latitude);
                return new Coordinate(projected.x, projected.y);
            })
            .ToArray();

        var lineString = new LineString(coordinates);
        var feature = new GeometryFeature
        {
            Geometry = lineString
        };
        feature.Styles.Add(new VectorStyle
        {
            Line = new Pen
            {
                Color = Mapsui.Styles.Color.FromArgb(255, 0, 102, 204),
                Width = 4
            }
        });

        return new MemoryLayer
        {
            Name = RouteLayerName,
            Features = new[] { feature }
        };
    }

    private static ILayer CreatePointLayer(string layerName, GeoCoordinate coordinate, Mapsui.Styles.Color color)
    {
        var projected = SphericalMercator.FromLonLat(coordinate.Longitude, coordinate.Latitude);
        var point = new NetTopologySuite.Geometries.Point(projected.x, projected.y);

        var feature = new GeometryFeature
        {
            Geometry = point
        };
        feature.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            Fill = new Mapsui.Styles.Brush(color),
            Outline = new Pen
            {
                Color = Mapsui.Styles.Color.White,
                Width = 2
            },
            SymbolScale = 0.6
        });

        return new MemoryLayer
        {
            Name = layerName,
            Features = new[] { feature }
        };
    }

    private void CenterMap(GeoCoordinate coordinate)
    {
        var map = NavigationMap.Map;
        if (map?.Navigator == null)
            return;

        var projected = SphericalMercator.FromLonLat(coordinate.Longitude, coordinate.Latitude);
        var point = new MPoint(projected.x, projected.y);
        map.Navigator.CenterOn(point);
        map.Navigator.ZoomToLevel(14);
    }
}
