using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RUNAapp.Helpers;
using RUNAapp.Models;
using RUNAapp.Services;

namespace RUNAapp.ViewModels;

/// <summary>
/// View model for the computer vision / obstacle detection page.
/// </summary>
public partial class VisionViewModel : BaseViewModel
{
    private readonly IComputerVisionService _visionService;
    private readonly ITextToSpeechService _ttsService;
    
    [ObservableProperty]
    private bool _isDetecting;
    
    [ObservableProperty]
    private bool _isModelLoaded;
    
    [ObservableProperty]
    private string _detectionStatus = "Loading model...";
    
    [ObservableProperty]
    private string _lastDetection = string.Empty;
    
    [ObservableProperty]
    private List<DetectedObject> _detectedObjects = new();
    
    [ObservableProperty]
    private int _detectionCount;
    
    [ObservableProperty]
    private BoundingBoxDrawable? _boundingBoxDrawable;
    
    public VisionViewModel(
        IComputerVisionService visionService,
        ITextToSpeechService ttsService)
    {
        _visionService = visionService;
        _ttsService = ttsService;
        
        Title = "Vision";
        
        // Initialize bounding box drawable
        BoundingBoxDrawable = new BoundingBoxDrawable();
        
        // Subscribe to detection events
        _visionService.ObjectsDetected += OnObjectsDetected;
        _visionService.DangerAlert += OnDangerAlert;
    }
    
    [RelayCommand]
    private async Task InitializeAsync()
    {
        await _ttsService.SpeakAsync("Vision mode. Loading obstacle detection.");
        
        await ExecuteAsync(async () =>
        {
            await _visionService.LoadModelAsync();
            IsModelLoaded = _visionService.IsModelLoaded;
            
            if (IsModelLoaded)
            {
                DetectionStatus = "Ready. Tap Start to begin detection.";
                await _ttsService.SpeakAsync("Obstacle detection ready. Tap the Start button to begin.");
            }
            else
            {
                DetectionStatus = "Model not available. Using basic detection.";
                await _ttsService.SpeakAsync("Vision model could not be loaded. Limited detection available.");
            }
        });
    }
    
    [RelayCommand]
    private async Task ToggleDetectionAsync()
    {
        if (IsDetecting)
        {
            await StopDetectionAsync();
        }
        else
        {
            await StartDetectionAsync();
        }
    }
    
    [RelayCommand]
    private async Task StartDetectionAsync()
    {
        if (!IsModelLoaded)
        {
            await _ttsService.SpeakAsync("Detection model is not loaded.");
            return;
        }
        
        _visionService.StartDetection();
        IsDetecting = true;
        DetectionStatus = "Scanning for obstacles...";
        
        await _ttsService.SpeakAsync("Detection started. Point your camera ahead.");
    }
    
    [RelayCommand]
    private async Task StopDetectionAsync()
    {
        _visionService.StopDetection();
        IsDetecting = false;
        DetectionStatus = "Detection stopped.";
        DetectedObjects = new List<DetectedObject>();
        
        await _ttsService.SpeakAsync("Detection stopped.");
    }
    
    [RelayCommand]
    private async Task GoBackAsync()
    {
        // Navigate immediately — OnDisappearing handles camera/detection cleanup
        await Shell.Current.GoToAsync("//Dashboard");
    }
    
    /// <summary>
    /// Called by the camera preview to process frames.
    /// </summary>
    public async Task ProcessCameraFrameAsync(byte[] imageData, int width, int height)
    {
        if (!IsDetecting)
            return;
        
        try
        {
            var detections = await _visionService.ProcessFrameAsync(imageData, width, height);
            
            // Update UI on main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DetectedObjects = detections;
                DetectionCount = detections.Count;
                
                // Update bounding box drawable
                if (BoundingBoxDrawable != null)
                {
                    BoundingBoxDrawable.DetectedObjects = detections;
                    BoundingBoxDrawable.PreviewWidth = width;
                    BoundingBoxDrawable.PreviewHeight = height;
                }
                
                if (detections.Count > 0)
                {
                    LastDetection = _visionService.GetDetectionDescription(detections);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Frame processing error: {ex.Message}");
        }
    }
    
    private async void OnObjectsDetected(object? sender, DetectionEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DetectedObjects = e.DetectedObjects;
                DetectionCount = e.DetectedObjects.Count;
                LastDetection = e.Description;

                // Update bounding box drawable
                if (BoundingBoxDrawable != null)
                {
                    BoundingBoxDrawable.DetectedObjects = e.DetectedObjects;
                }

                DetectionStatus = e.DetectedObjects.Count > 0
                    ? $"Detected {e.DetectedObjects.Count} object(s)"
                    : "Clear path";
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ObjectsDetected handler error: {ex.Message}");
        }
    }

    private async void OnDangerAlert(object? sender, DetectedObject obj)
    {
        try
        {
            var positionText = obj.Position switch
            {
                RelativePosition.Left => "on your left",
                RelativePosition.Right => "on your right",
                _ => "directly ahead"
            };

            var alertMessage = obj.DangerLevel == DangerLevel.Critical
                ? $"Stop! {obj.Label} {positionText}!"
                : $"Caution! {obj.Label} {positionText}.";

            await _ttsService.SpeakAlertAsync(alertMessage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DangerAlert handler error: {ex.Message}");
        }
    }
}
