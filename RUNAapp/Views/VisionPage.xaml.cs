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
                LayoutParameters = new Android.Views.ViewGroup.LayoutParams(
                    Android.Views.ViewGroup.LayoutParams.MatchParent,
                    Android.Views.ViewGroup.LayoutParams.MatchParent)
            };

            // Set the preview view on the camera handler
            if (_cameraProvider is RUNAapp.Platforms.Android.CameraFrameHandler handler)
            {
                handler.SetPreviewView(_previewView);
            }

            // Wait for the MAUI Handler to be created (Handlers are lazy — not available during OnAppearing)
            var handlerRetries = 0;
            while (CameraPreviewContainer.Handler == null && handlerRetries < 50)
            {
                await Task.Delay(50); // 50ms * 50 = 2.5s max wait
                handlerRetries++;
            }

            // Add PreviewView to the container
            if (CameraPreviewContainer.Handler?.PlatformView is Android.Views.View containerView)
            {
                if (containerView is Android.Views.ViewGroup viewGroup)
                {
                    viewGroup.RemoveAllViews();
                    viewGroup.AddView(_previewView);

                    // Force Android to re-measure after adding native view
                    // (MAUI layout pass already completed before AddView)
                    viewGroup.RequestLayout();
                    viewGroup.Post(() => viewGroup.RequestLayout());

                    CameraPlaceholder.IsVisible = false;
                    System.Diagnostics.Debug.WriteLine("Camera preview attached to ViewGroup");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Container is {containerView.GetType().Name}, not ViewGroup");
                    CameraPlaceholder.IsVisible = true;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("CameraPreviewContainer Handler or PlatformView is null");
                CameraPlaceholder.IsVisible = true;
            }

            // Subscribe to frame events
            _cameraProvider.FrameAvailable += OnFrameAvailable;

            // Start the camera
            await _cameraProvider.StartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Camera setup error: {ex.Message}\n{ex.StackTrace}");
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

        // Unsubscribe from frame events immediately
        if (_cameraProvider != null)
        {
            _cameraProvider.FrameAvailable -= OnFrameAvailable;
        }

        // Stop detection synchronously (fast — just flips flags and disposes timer)
        if (BindingContext is VisionViewModel vm && vm.IsDetecting)
        {
            vm.StopDetectionCommand.Execute(null);
        }

        // Stop camera on background thread so page transition isn't blocked
        if (_cameraProvider != null)
        {
            var provider = _cameraProvider;
            _ = Task.Run(async () =>
            {
                try { await provider.StopAsync(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Camera stop error: {ex.Message}");
                }
            });
        }

#if ANDROID
        _previewView = null;
#endif
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
