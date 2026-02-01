using Android.Content;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Camera.Camera2;
using AndroidX.Core.Content;
using Java.Util.Concurrent;
using RUNAapp.Services;
using Android.Graphics;
using Android.Media;
using Java.Lang;

namespace RUNAapp.Platforms.Android;

/// <summary>
/// Android CameraX implementation for real-time frame processing.
/// Uses ImageAnalysis use case to get continuous frames for YOLO inference.
/// Uses Preview use case to display live camera feed.
/// </summary>
public class CameraFrameHandler : Java.Lang.Object, ICameraFrameProvider, ImageAnalysis.IAnalyzer
{
    private ProcessCameraProvider? _cameraProvider;
    private ImageAnalysis? _imageAnalysis;
    private Preview? _preview;
    private PreviewView? _previewView;
    private bool _isActive;
    private readonly Context _context;

    public bool IsActive => _isActive;

    public event EventHandler<FrameAvailableEventArgs>? FrameAvailable;

    public CameraFrameHandler()
    {
        _context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.ApplicationContext
            ?? throw new InvalidOperationException("Android context not available");
    }

    /// <summary>
    /// Set the PreviewView for displaying live camera feed.
    /// Must be called before StartAsync().
    /// </summary>
    public void SetPreviewView(PreviewView previewView)
    {
        _previewView = previewView;
    }

    public async Task StartAsync()
    {
        if (_isActive)
            return;

        try
        {
            // Get CameraX provider - GetInstance returns IListenableFuture<ProcessCameraProvider>
            var cameraProviderFuture = ProcessCameraProvider.GetInstance(_context);

            // Convert ListenableFuture to Task<ProcessCameraProvider>
            _cameraProvider = await Task.Run(() =>
            {
                var futureObj = cameraProviderFuture as Java.Lang.Object;
                if (futureObj == null)
                    throw new System.InvalidOperationException("Failed to get camera provider future");

                var getMethod = futureObj.Class.GetMethod("get");
                if (getMethod == null)
                    throw new System.InvalidOperationException("ListenableFuture.Get() method not found");

                var resultObj = getMethod.Invoke(futureObj, null);
                var result = resultObj as ProcessCameraProvider;
                if (result == null)
                    throw new System.InvalidOperationException($"Failed to cast result to ProcessCameraProvider. Got: {resultObj?.GetType().Name ?? "null"}");

                return result;
            });

            // Create ImageAnalysis use case for frame processing
            _imageAnalysis = new ImageAnalysis.Builder()
                .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)
                .SetOutputImageFormat(ImageAnalysis.OutputImageFormatYuv420888)
                .Build();

            // Set analyzer to receive frames
            var executor = Executors.NewSingleThreadExecutor();
            if (executor == null)
                throw new InvalidOperationException("Failed to create executor");
            _imageAnalysis.SetAnalyzer(executor, this);

            // Create Preview use case for live camera display
            _preview = new Preview.Builder().Build();

            // Connect preview to the PreviewView surface if available
            if (_previewView != null)
            {
                _preview.SetSurfaceProvider(_previewView.SurfaceProvider);
            }

            // Select back camera
            var cameraSelector = CameraSelector.DefaultBackCamera;

            // Bind use cases
            if (_cameraProvider == null)
                throw new InvalidOperationException("Camera provider is null");
            _cameraProvider.UnbindAll();

            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity as AndroidX.Lifecycle.ILifecycleOwner;
            if (activity == null)
                throw new InvalidOperationException("Lifecycle owner not available");

            // Bind both Preview and ImageAnalysis
            _cameraProvider.BindToLifecycle(
                activity,
                cameraSelector,
                _preview,
                _imageAnalysis);

            _isActive = true;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CameraX start error: {ex.Message}");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!_isActive)
            return;

        try
        {
            _cameraProvider?.UnbindAll();
            _imageAnalysis?.ClearAnalyzer();
            _imageAnalysis = null;
            _preview = null;
            _cameraProvider = null;
            _isActive = false;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CameraX stop error: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    public void Analyze(IImageProxy image)
    {
        try
        {
            if (!_isActive || FrameAvailable == null)
            {
                image.Close();
                return;
            }

            // Convert YUV_420_888 to RGB byte array
            var rgbBytes = ConvertYuvToRgb(image);

            if (rgbBytes != null && rgbBytes.Length > 0)
            {
                // Fire event on background thread (already off UI thread)
                FrameAvailable?.Invoke(this, new FrameAvailableEventArgs
                {
                    FrameBytes = rgbBytes,
                    Width = image.Width,
                    Height = image.Height
                });
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Frame analysis error: {ex.Message}");
        }
        finally
        {
            // Always close the image to release buffer
            image.Close();
        }
    }

    private byte[]? ConvertYuvToRgb(IImageProxy image)
    {
        try
        {
            var yBuffer = image.GetPlanes()[0].Buffer;
            var uBuffer = image.GetPlanes()[1].Buffer;
            var vBuffer = image.GetPlanes()[2].Buffer;

            int ySize = yBuffer.Remaining();
            int uSize = uBuffer.Remaining();
            int vSize = vBuffer.Remaining();

            byte[] yBytes = new byte[ySize];
            byte[] uBytes = new byte[uSize];
            byte[] vBytes = new byte[vSize];

            yBuffer.Get(yBytes);
            uBuffer.Get(uBytes);
            vBuffer.Get(vBytes);

            int width = image.Width;
            int height = image.Height;

            // Convert YUV_420_888 to RGB
            byte[] rgbBytes = new byte[width * height * 4]; // RGBA format

            int yIndex = 0;
            int uvIndex = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int yVal = yBytes[yIndex] & 0xFF;
                    int uVal = uBytes[uvIndex] & 0xFF;
                    int vVal = vBytes[uvIndex] & 0xFF;  // Fixed: was reading from uBytes

                    // YUV to RGB conversion
                    int r = (int)(yVal + 1.402 * (vVal - 128));
                    int g = (int)(yVal - 0.344 * (uVal - 128) - 0.714 * (vVal - 128));
                    int b = (int)(yVal + 1.772 * (uVal - 128));

                    // Clamp values
                    r = System.Math.Max(0, System.Math.Min(255, r));
                    g = System.Math.Max(0, System.Math.Min(255, g));
                    b = System.Math.Max(0, System.Math.Min(255, b));

                    int rgbIndex = (y * width + x) * 4;
                    rgbBytes[rgbIndex] = (byte)r;     // R
                    rgbBytes[rgbIndex + 1] = (byte)g; // G
                    rgbBytes[rgbIndex + 2] = (byte)b; // B
                    rgbBytes[rgbIndex + 3] = 255;     // A (opaque)

                    yIndex++;

                    // UV samples are subsampled (every 2 pixels)
                    if (x % 2 == 1)
                    {
                        uvIndex++;
                    }
                }

                // Skip UV row for odd rows (420 format)
                if (y % 2 == 0)
                {
                    uvIndex -= width / 2;
                }
            }

            return rgbBytes;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"YUV to RGB conversion error: {ex.Message}");
            return null;
        }
    }
}
