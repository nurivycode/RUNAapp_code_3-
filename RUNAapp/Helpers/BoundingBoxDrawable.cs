using Microsoft.Maui.Graphics;
using RUNAapp.Models;

namespace RUNAapp.Helpers;

/// <summary>
/// Drawable for rendering bounding boxes on detected objects.
/// </summary>
public class BoundingBoxDrawable : IDrawable
{
    public List<DetectedObject> DetectedObjects { get; set; } = new();
    public float PreviewWidth { get; set; }
    public float PreviewHeight { get; set; }
    
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (DetectedObjects == null || DetectedObjects.Count == 0)
            return;
        
        foreach (var obj in DetectedObjects)
        {
            if (obj.BoundingBox == null)
                continue;
            
            // Scale bounding box to preview size
            var x = obj.BoundingBox.X * (dirtyRect.Width / PreviewWidth);
            var y = obj.BoundingBox.Y * (dirtyRect.Height / PreviewHeight);
            var width = obj.BoundingBox.Width * (dirtyRect.Width / PreviewWidth);
            var height = obj.BoundingBox.Height * (dirtyRect.Height / PreviewHeight);
            
            // Choose color based on danger level
            var color = obj.DangerLevel switch
            {
                DangerLevel.Critical => Colors.Red,
                DangerLevel.High => Colors.Orange,
                _ => Colors.Yellow
            };
            
            // Draw bounding box rectangle
            canvas.StrokeColor = color;
            canvas.StrokeSize = 3;
            canvas.DrawRectangle(x, y, width, height);
            
            // Draw label background
            var labelText = $"{obj.Label} {(int)(obj.Confidence * 100)}%";
            var labelSize = canvas.GetStringSize(labelText, Microsoft.Maui.Graphics.Font.Default, 14);
            var labelRect = new RectF(x, y - 20, labelSize.Width + 8, 18);
            
            canvas.FillColor = color.WithAlpha(0.8f);
            canvas.FillRectangle(labelRect);
            
            // Draw label text
            canvas.FontColor = Colors.White;
            canvas.FontSize = 12;
            canvas.DrawString(labelText, labelRect, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
