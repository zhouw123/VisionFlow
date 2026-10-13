using System.Windows.Threading;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using VisionFlow.Transforms;
using VisionFlow.Transforms.Imaging;
using VisionFlow.WpfSamples.Controls;
using NormalizedRect = VisionFlow.WpfSamples.Controls.NormalizedRect;
using NormalizedQuad = VisionFlow.WpfSamples.Controls.NormalizedQuad;
using NormalizedPoint = VisionFlow.WpfSamples.Controls.NormalizedPoint;

namespace VisionFlow.WpfSamples.Coordination;

/// <summary>
/// Coordinates a RoiEditor with the active Transform.
/// 
/// Responsibilities:
///   - Forward sync: user drags the ROI, on release push the new region to the Transform.
///   - Reverse sync: user edits parameters in PropertyGrid, update the ROI editor.
///   - Mode switch: apply the preset matching the processing mode.
///   - Suppress loops during internal write-back.
/// 
/// All UI access is marshalled to the UI thread via the Dispatcher.
/// </summary>
public sealed class RoiCoordinator
{
    private readonly RoiEditor _editor;
    private readonly Dispatcher _dispatcher;
    private FrameComponent? _target;
    private bool _suppressReverseSync;

    public RoiCoordinator(RoiEditor editor, Dispatcher dispatcher)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _editor.RoiChangeCompleted += OnEditorChangeCompleted;
    }

    /// <summary>The Transform currently receiving ROI values. Null disables forward sync.</summary>
    public FrameComponent? Target
    {
        get => _target;
        set
        {
            if (ReferenceEquals(_target, value)) return;

            if (_target is not null)
            {
                try { _target.ParameterChanged -= OnTargetParameterChanged; } catch { }
            }

            _target = value;

            if (_target is not null)
            {
                _target.ParameterChanged += OnTargetParameterChanged;
            }
        }
    }

    /// <summary>Fired after a forward sync completes (Transform updated from editor).</summary>
    public event EventHandler? TargetUpdated;

    /// <summary>
    /// Apply the preset for the given processing mode.
    /// Called on mode switch, before assigning Target.
    /// </summary>
    public void ApplyPreset(string modeName)
    {
        if (modeName == "Perspective")
        {
            _editor.Mode = RoiEditMode.Quad;
            _editor.Quad = new NormalizedQuad(
                new NormalizedPoint(80.0 / 640, 40.0 / 480),
                new NormalizedPoint(560.0 / 640, 40.0 / 480),
                new NormalizedPoint(620.0 / 640, 440.0 / 480),
                new NormalizedPoint(20.0 / 640, 440.0 / 480));
        }
        else if (modeName == "Crop")
        {
            _editor.Mode = RoiEditMode.Rect;
            _editor.Rect = new NormalizedRect(
                160.0 / 640, 120.0 / 480, 320.0 / 640, 240.0 / 480);
        }
        else
        {
            _editor.Mode = RoiEditMode.Rect;
            _editor.Rect = NormalizedRect.FullFrame;
        }

        _editor.Redraw();
    }

    /// <summary>
    /// Re-apply current editor values to the target transform.
    /// Used by the Reset button.
    /// </summary>
    public void PushToTarget()
    {
        FireTargetUpdate();
    }

    // ---------- Forward ----------

    private void OnEditorChangeCompleted(object? sender, EventArgs e)
    {
        FireTargetUpdate();
    }

    private void FireTargetUpdate()
    {
        if (_target is null) return;

        int vw = _editor.VideoWidth;
        int vh = _editor.VideoHeight;
        if (vw <= 0 || vh <= 0) return;

        _suppressReverseSync = true;
        try
        {
            if (_target is CannyTransform canny)
            {
                var r = _editor.Rect;
                canny.SetRoi(
                    (int)Math.Round(r.X * vw),
                    (int)Math.Round(r.Y * vh),
                    (int)Math.Round(r.Width * vw),
                    (int)Math.Round(r.Height * vh));
            }
            else if (_target is CropTransform crop)
            {
                var r = _editor.Rect;
                crop.SetRegion(
                    (int)Math.Round(r.X * vw),
                    (int)Math.Round(r.Y * vh),
                    (int)Math.Round(r.Width * vw),
                    (int)Math.Round(r.Height * vh));
            }
            else if (_target is PerspectiveTransform persp)
            {
                var q = _editor.Quad;
                persp.SetSourcePoints(
                    (int)Math.Round(q.P0.X * vw), (int)Math.Round(q.P0.Y * vh),
                    (int)Math.Round(q.P1.X * vw), (int)Math.Round(q.P1.Y * vh),
                    (int)Math.Round(q.P2.X * vw), (int)Math.Round(q.P2.Y * vh),
                    (int)Math.Round(q.P3.X * vw), (int)Math.Round(q.P3.Y * vh));
            }
        }
        finally
        {
            _suppressReverseSync = false;
        }

        TargetUpdated?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Reverse ----------

    private void OnTargetParameterChanged(object? sender, ParameterChangedEventArgs e)
    {
        if (_suppressReverseSync) return;

        bool isRoiKey =
            e.Key == "RoiX" || e.Key == "RoiY" ||
            e.Key == "RoiWidth" || e.Key == "RoiHeight" ||
            e.Key == "X" || e.Key == "Y" ||
            e.Key == "Width" || e.Key == "Height" ||
            e.Key.StartsWith("Src");

        if (!isRoiKey) return;

        _dispatcher.InvokeAsync(SyncFromTarget,
            DispatcherPriority.Background);
    }

    /// <summary>
    /// Read the current ROI from the target transform and update the visual editor.
    /// Must run on the UI thread.
    /// </summary>
    private void SyncFromTarget()
    {
        if (_target is null) return;

        int vw = _editor.VideoWidth;
        int vh = _editor.VideoHeight;
        if (vw <= 0 || vh <= 0) return;

        if (_target is CannyTransform canny)
        {
            int x = (int)(canny.GetParameter("RoiX") ?? 0);
            int y = (int)(canny.GetParameter("RoiY") ?? 0);
            int w = (int)(canny.GetParameter("RoiWidth") ?? 0);
            int h = (int)(canny.GetParameter("RoiHeight") ?? 0);

            if (w == 0 && h == 0)
                _editor.Rect = NormalizedRect.FullFrame;
            else
                _editor.Rect = new NormalizedRect(
                    x / (double)vw, y / (double)vh,
                    w / (double)vw, h / (double)vh);

            _editor.Redraw();
        }
        else if (_target is CropTransform crop)
        {
            int x = (int)(crop.GetParameter("X") ?? 0);
            int y = (int)(crop.GetParameter("Y") ?? 0);
            int w = (int)(crop.GetParameter("Width") ?? vw);
            int h = (int)(crop.GetParameter("Height") ?? vh);
            _editor.Rect = new NormalizedRect(
                x / (double)vw, y / (double)vh,
                w / (double)vw, h / (double)vh);
            _editor.Redraw();
        }
        else if (_target is PerspectiveTransform persp)
        {
            int x0 = (int)(persp.GetParameter("SrcX0") ?? 0);
            int y0 = (int)(persp.GetParameter("SrcY0") ?? 0);
            int x1 = (int)(persp.GetParameter("SrcX1") ?? vw);
            int y1 = (int)(persp.GetParameter("SrcY1") ?? 0);
            int x2 = (int)(persp.GetParameter("SrcX2") ?? vw);
            int y2 = (int)(persp.GetParameter("SrcY2") ?? vh);
            int x3 = (int)(persp.GetParameter("SrcX3") ?? 0);
            int y3 = (int)(persp.GetParameter("SrcY3") ?? vh);
            _editor.Quad = new NormalizedQuad(
                new NormalizedPoint(x0 / (double)vw, y0 / (double)vh),
                new NormalizedPoint(x1 / (double)vw, y1 / (double)vh),
                new NormalizedPoint(x2 / (double)vw, y2 / (double)vh),
                new NormalizedPoint(x3 / (double)vw, y3 / (double)vh));
            _editor.Redraw();
        }
    }
}