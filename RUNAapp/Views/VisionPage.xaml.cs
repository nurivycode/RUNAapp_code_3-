using RUNAapp.ViewModels;
using RUNAapp.Services;
using Microsoft.Maui.ApplicationModel;
#if ANDROID
using AndroidX.Camera.View;
using Android.Widget;
using Microsoft.Maui.Platform;
#endif

namespace RUNAapp.Views;

public partial class VisionPage : ContentPage
{
    private readonly ICameraFrameProvider? _cameraProvider;
#if ANDROID
    private PreviewView? _previewView;
#endif

    public VisionPage(VisionViewModel viewModel, ICameraFrameProvider? cameraProvider)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _cameraProvider = cameraProvider;

        // Set bounding box drawable
        if (viewModel.BoundingBoxDrawable != null)
        {
            BoundingBoxOverlay.Drawable = viewModel.BoundingBoxDrawable;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Request camera permission
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                // Show placeholder if permission denied
                CameraPlaceholder.IsVisible = true;
                if (BindingContext is VisionViewModel vm)
                {
                    await vm.InitializeCommand.ExecuteAsync(null);
                }
                return;
            }
        }

        // Setup camera preview and start frame provider
        await SetupCameraAsync();

        if (BindingContext is VisionViewModel vm2)
        {
            await vm2.InitializeCommand.ExecuteAsync(null);
        }
    }

    private async Task SetupCameraAsync()
    {
        if (_cameraProvider == null)
        {
            CameraPlaceholder.IsVisible = true;
            return;
        }

#if ANDROID
        try
        {
            // Create Android PreviewView for live camera feed
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity == null)
            {
                CameraPlaceholder.IsVisible = true;
                return;
            }

            _previewView = new PreviewView(activity)
            {
                LayoutParameters = new FrameLayout.LayoutParams(
                    FrameLayout.LayoutParams.MatchParent,
                    FrameLayout.LayoutParams.MatchParent)
            };

            // Set the preview view on the camera handler
            if (_cameraProvider is RUNAapp.Platforms.Android.CameraFrameHandler handler)
            {
                handler.SetPreviewView(_previewView);
            }

            // Add PreviewView to the container
            if (CameraPreviewContainer.Handler?.PlatformView is Android.Views.View containerView)
            {
                if (containerView is Android.Views.ViewGroup viewGroup)
                {
                    viewGroup.RemoveAllViews();
                    viewGroup.AddView(_previewView);
                }
            }

            // Subscribe to frame events
            _cameraProvider.FrameAvailable += OnFrameAvailable;

            // Start the camera
            await _cameraProvider.StartAsync();
            CameraPlaceholder.IsVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Camera setup error: {ex.Message}");
            CameraPlaceholder.IsVisible = true;
        }
#else
        // Non-Android platforms - show placeholder
        CameraPlaceholder.IsVisible = true;
        await Task.CompletedTask;
#endif
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        // Unsubscribe from frame events
        if (_cameraProvider != null)
        {
            _cameraProvider.FrameAvailable -= OnFrameAvailable;
            await _cameraProvider.StopAsync();
        }

#if ANDROID
        _previewView = null;
#endif

        // Stop detection when leaving page
        if (BindingContext is VisionViewModel vm && vm.IsDetecting)
        {
            vm.StopDetectionCommand.Execute(null);
        }
    }

    private void OnFrameAvailable(object? sender, FrameAvailableEventArgs e)
    {
        // Process frame with YOLO
        if (BindingContext is VisionViewModel viewModel && viewModel.IsDetecting)
        {
            // Process frame asynchronously (already off UI thread from CameraX)
            _ = viewModel.ProcessCameraFrameAsync(e.FrameBytes, e.Width, e.Height);
        }
    }
}
