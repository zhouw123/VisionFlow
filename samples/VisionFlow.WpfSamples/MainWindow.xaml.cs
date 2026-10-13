using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DirectShowLib;
using VisionFlow.Devices;
using VisionFlow.Integration;
using VisionFlow.Sinks;
using VisionFlow.Sources;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using VisionFlow.Transforms;
using VisionFlow.Transforms.Imaging;
using VisionFlow.WpfSamples.Controls;
using VisionFlow.WpfSamples.Parameters;
using VisionFlow.WpfSamples.Modes;
using VisionFlow.WpfSamples.Rendering;
using NormalizedRect = VisionFlow.WpfSamples.Controls.NormalizedRect;
using NormalizedQuad = VisionFlow.WpfSamples.Controls.NormalizedQuad;
using NormalizedPoint = VisionFlow.WpfSamples.Controls.NormalizedPoint;

namespace VisionFlow.WpfSamples;

public partial class MainWindow : System.Windows.Window
{
    private readonly DeviceInfo? _device;

    private DirectShowCameraSource? _source;
    private MjpegDecodeTransform? _decode;
    private List<FrameComponent> _activeTransforms = new();

    // Two display sinks
    private DelegateSink? _displaySinkOriginal;
    private DelegateSink? _displaySinkProcessed;
    private SnapshotSink? _snapshotSink;

    private StreamingGraph? _graph;
    private CannyTransform? _cannyForRoi;
    private FrameComponent? _roiTarget;
    private bool _suppressRoiSync;

    private DateTime _startTime = DateTime.Now;
    private FrameRenderer? _frameRendererOriginal;
    private FrameRenderer? _frameRendererProcessed;



    public MainWindow()
    {
        InitializeComponent();

        var devices = DeviceEnumerator.EnumerateVideoInputDevices();
        if (devices.Count == 0)
        {
            MessageBox.Show("No camera found.");
            Application.Current.Shutdown();
            return;
        }

        _device = devices[0];
        for (int i = 1; i < devices.Count; i++) devices[i].Dispose();

        var modes = ProcessingModes.All;
        foreach (var m in modes) ModeCombo.Items.Add(m);
        ModeCombo.SelectedIndex = 1;  // Gray by default

        CameraPropsButton.Click += OnCameraPropsClick;
        ClearRoiButton.Click += OnClearRoiClick;
        ResetRoiButton.Click += OnResetRoiClick;
        ModeCombo.SelectionChanged += OnModeChanged;

        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;
    }






    private void OnWindowLoaded(object? sender, RoutedEventArgs e) => StartGraph();

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        StopGraph();
        try { _device?.Dispose(); } catch { }
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        StartGraph();
    }

    private void StartGraph()
    {
        if (_device is null) return;

        Cursor = Cursors.Wait;
        try
        {
            StopGraph();

            // Initialize frame renderers AFTER StopGraph (which no longer nulls them)
            _frameRendererOriginal = new FrameRenderer(Dispatcher,
                bmp => VideoImageOriginal.Source = bmp);
            _frameRendererProcessed = new FrameRenderer(Dispatcher,
                bmp => VideoImageProcessed.Source = bmp);


            _source = new DirectShowCameraSource(_device);
            _source.TrySetFormat(MediaSubType.MJPG, 640, 480);
            _decode = new MjpegDecodeTransform();

            var mode = (ProcessingMode)ModeCombo.SelectedItem!;
            _activeTransforms = new List<FrameComponent>(mode.Build());

            _cannyForRoi = _activeTransforms.OfType<CannyTransform>().FirstOrDefault();

            // Find the ROI-related transform for this mode
            _roiTarget = _activeTransforms.FirstOrDefault(t =>
                t is CannyTransform || t is CropTransform || t is PerspectiveTransform);

            // Configure the ROI editor's mode and initial values
            ConfigureRoiEditorForMode(mode.Name);

            // Two display sinks
            _displaySinkOriginal = new DelegateSink(OnFrameOriginal,
                name: "DisplayOriginal", queueCapacity: 4,
                policy: BackpressurePolicy.DropOldest);

            _displaySinkProcessed = new DelegateSink(OnFrameProcessed,
                name: "DisplayProcessed", queueCapacity: 4,
                policy: BackpressurePolicy.DropOldest);

            // Snapshot sink (snapshots the processed branch)
            var snapshotDir = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Snapshots");
            _snapshotSink = new SnapshotSink(snapshotDir);

            // Link:
            //   source -> decode -> displaySinkOriginal
            //                   \-> [transforms...] -> displaySinkProcessed
            //                                       -> snapshotSink
            _source.LinkTo(_decode);
            _decode.LinkTo(_displaySinkOriginal);

            FrameComponent tail = _decode;
            foreach (var t in _activeTransforms)
            {
                tail.LinkTo(t);
                tail = t;
            }
            tail.LinkTo(_displaySinkProcessed);
            tail.LinkTo(_snapshotSink);

            _graph = StreamingGraph.From(_source);
            _graph.Start();

            if (_graph.Context is not null)
            {
                _graph.Context.ResultProduced += OnResultProduced;
            }

            RebuildParameterPanel();

            // Subscribe to the ROI editor's completion event
            RoiEditControl.RoiChangeCompleted -= OnRoiChangeCompleted;
            RoiEditControl.RoiChangeCompleted += OnRoiChangeCompleted;

            // Subscribe to the transform's parameter changes for reverse sync
            if (_roiTarget is not null)
            {
                _roiTarget.ParameterChanged -= OnTransformParameterChanged;
                _roiTarget.ParameterChanged += OnTransformParameterChanged;
            }

            _startTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Start failed:\n{ex}", "Error");
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private void StopGraph()
    {
        if (_graph is not null)
        {
            try
            {
                if (_graph.Context is not null)
                    _graph.Context.ResultProduced -= OnResultProduced;
            }
            catch { }

            try { _graph.Stop(); } catch { }
            try { _graph.Dispose(); } catch { }
            _graph = null;
        }

        _source = null;
        _decode = null;
        _displaySinkOriginal = null;
        _displaySinkProcessed = null;
        _snapshotSink = null;
        // FrameRenderer instances are intentionally NOT nulled here;
        // they are reused across mode switches (created once in StartGraph).
        _activeTransforms.Clear();
        if (_roiTarget is not null)
        {
            try { _roiTarget.ParameterChanged -= OnTransformParameterChanged; } catch { }
        }

        _cannyForRoi = null;
        _roiTarget = null;
    }

    private void RebuildParameterPanel()
    {
        // Set ROI editor mode based on the current processing mode
        var mode = ModeCombo.SelectedItem as ProcessingMode;
        if (mode is not null)
        {
            RoiEditControl.Mode = mode.Name switch
            {
                "Perspective" => RoiEditMode.Quad,
                _ => RoiEditMode.Rect,
            };
        }

        var parameterized = _activeTransforms
            .OfType<IParameterized>()
            .ToArray();

        if (parameterized.Length == 0)
        {
            ParamGrid.SetComponents(null);
            EmptyHintText.Visibility = Visibility.Visible;
            return;
        }

        ParamGrid.SetComponents(parameterized);
        EmptyHintText.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Set the RoiEditor's mode and initial value based on the processing mode.
    /// Presets mirror those used in the mode factory methods.
    /// </summary>
    private void ConfigureRoiEditorForMode(string modeName)
    {
        if (modeName == "Perspective")
        {
            RoiEditControl.Mode = RoiEditMode.Quad;
            RoiEditControl.Quad = new NormalizedQuad(
                new NormalizedPoint(80.0 / 640, 40.0 / 480),
                new NormalizedPoint(560.0 / 640, 40.0 / 480),
                new NormalizedPoint(620.0 / 640, 440.0 / 480),
                new NormalizedPoint(20.0 / 640, 440.0 / 480));
        }
        else if (modeName == "Crop")
        {
            RoiEditControl.Mode = RoiEditMode.Rect;
            RoiEditControl.Rect = new NormalizedRect(
                160.0 / 640, 120.0 / 480, 320.0 / 640, 240.0 / 480);
        }
        else
        {
            RoiEditControl.Mode = RoiEditMode.Rect;
            RoiEditControl.Rect = NormalizedRect.FullFrame;
        }

        RoiEditControl.Redraw();
    }

    /// <summary>
    /// When the user finishes editing the ROI, push the new value to the
    /// active Transform in pixel coordinates.
    /// </summary>
    private void OnRoiChangeCompleted(object? sender, EventArgs e)
    {
        if (_roiTarget is null) return;

        _suppressRoiSync = true;
        try
        {

        int vw = RoiEditControl.VideoWidth;
        int vh = RoiEditControl.VideoHeight;
        if (vw <= 0 || vh <= 0) return;

        if (_roiTarget is CannyTransform canny)
        {
            var r = RoiEditControl.Rect;
            canny.SetRoi(
                (int)Math.Round(r.X * vw),
                (int)Math.Round(r.Y * vh),
                (int)Math.Round(r.Width * vw),
                (int)Math.Round(r.Height * vh));
        }
        else if (_roiTarget is CropTransform crop)
        {
            var r = RoiEditControl.Rect;
            crop.SetRegion(
                (int)Math.Round(r.X * vw),
                (int)Math.Round(r.Y * vh),
                (int)Math.Round(r.Width * vw),
                (int)Math.Round(r.Height * vh));
        }
        else if (_roiTarget is PerspectiveTransform persp)
        {
            var q = RoiEditControl.Quad;
            persp.SetSourcePoints(
                (int)Math.Round(q.P0.X * vw), (int)Math.Round(q.P0.Y * vh),
                (int)Math.Round(q.P1.X * vw), (int)Math.Round(q.P1.Y * vh),
                (int)Math.Round(q.P2.X * vw), (int)Math.Round(q.P2.Y * vh),
                (int)Math.Round(q.P3.X * vw), (int)Math.Round(q.P3.Y * vh));
        }

        // The PropertyGrid should now refresh automatically via ParameterChanged
        // events, but we can force a rebuild if needed.
        // RebuildParameterPanel();
        }
        finally
        {
            _suppressRoiSync = false;
        }
    }

    /// <summary>
    /// Called when the ROI transform's parameter changes (e.g. from PropertyGrid editing).
    /// Updates the visual ROI editor to reflect the new values.
    /// </summary>
    private void OnTransformParameterChanged(object? sender, ParameterChangedEventArgs e)
    {
        if (_suppressRoiSync) return;

        // Only react to ROI-related keys
        bool isRoiKey =
            e.Key == "RoiX" || e.Key == "RoiY" ||
            e.Key == "RoiWidth" || e.Key == "RoiHeight" ||
            e.Key == "X" || e.Key == "Y" ||
            e.Key == "Width" || e.Key == "Height" ||
            e.Key.StartsWith("Src");

        if (!isRoiKey) return;

        // Dispatch to UI thread (ParameterChanged may fire from any thread)
        Dispatcher.InvokeAsync(SyncRoiEditorFromTransform,
            System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Read the current ROI from the transform and update the visual editor.
    /// </summary>
    private void SyncRoiEditorFromTransform()
    {
        if (_roiTarget is null) return;

        int vw = RoiEditControl.VideoWidth;
        int vh = RoiEditControl.VideoHeight;
        if (vw <= 0 || vh <= 0) return;

        if (_roiTarget is CannyTransform canny)
        {
            int x = (int)(canny.GetParameter("RoiX") ?? 0);
            int y = (int)(canny.GetParameter("RoiY") ?? 0);
            int w = (int)(canny.GetParameter("RoiWidth") ?? 0);
            int h = (int)(canny.GetParameter("RoiHeight") ?? 0);

            // If all zero, treat as full frame
            if (w == 0 && h == 0)
            {
                RoiEditControl.Rect = NormalizedRect.FullFrame;
            }
            else
            {
                RoiEditControl.Rect = new NormalizedRect(
                    x / (double)vw, y / (double)vh,
                    w / (double)vw, h / (double)vh);
            }
            RoiEditControl.Redraw();
        }
        else if (_roiTarget is CropTransform crop)
        {
            int x = (int)(crop.GetParameter("X") ?? 0);
            int y = (int)(crop.GetParameter("Y") ?? 0);
            int w = (int)(crop.GetParameter("Width") ?? vw);
            int h = (int)(crop.GetParameter("Height") ?? vh);
            RoiEditControl.Rect = new NormalizedRect(
                x / (double)vw, y / (double)vh,
                w / (double)vw, h / (double)vh);
            RoiEditControl.Redraw();
        }
        else if (_roiTarget is PerspectiveTransform persp)
        {
            int x0 = (int)(persp.GetParameter("SrcX0") ?? 0);
            int y0 = (int)(persp.GetParameter("SrcY0") ?? 0);
            int x1 = (int)(persp.GetParameter("SrcX1") ?? vw);
            int y1 = (int)(persp.GetParameter("SrcY1") ?? 0);
            int x2 = (int)(persp.GetParameter("SrcX2") ?? vw);
            int y2 = (int)(persp.GetParameter("SrcY2") ?? vh);
            int x3 = (int)(persp.GetParameter("SrcX3") ?? 0);
            int y3 = (int)(persp.GetParameter("SrcY3") ?? vh);
            RoiEditControl.Quad = new NormalizedQuad(
                new NormalizedPoint(x0 / (double)vw, y0 / (double)vh),
                new NormalizedPoint(x1 / (double)vw, y1 / (double)vh),
                new NormalizedPoint(x2 / (double)vw, y2 / (double)vh),
                new NormalizedPoint(x3 / (double)vw, y3 / (double)vh));
            RoiEditControl.Redraw();
        }
    }

    /// <summary>
    /// Reset the ROI editor back to the preset for the current mode.
    /// </summary>
    private void OnResetRoiClick(object sender, RoutedEventArgs e)
    {
        var mode = ModeCombo.SelectedItem as ProcessingMode;
        if (mode is null) return;

        ConfigureRoiEditorForMode(mode.Name);

        // Push the reset value to the transform
        OnRoiChangeCompleted(this, EventArgs.Empty);
    }
    private void OnCameraPropsClick(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var shown = _source?.ShowPropertyPage(hwnd) ?? false;
        if (!shown)
        {
            MessageBox.Show("Camera property page not available.");
        }
    }

    private void OnClearRoiClick(object sender, RoutedEventArgs e)
    {
        // ClearRoiVisual();
        _cannyForRoi?.ClearRoi();
        RebuildParameterPanel();
    }

    // ---------- Frame handlers ----------

    private void OnFrameOriginal(FrameData frame, PipelineContext ctx)
    {
        if (_frameRendererOriginal is null) return;

        int w = frame.Width, h = frame.Height;

        // Update RoiEditor's video dimensions on the UI thread
        // (RoiEditor is a DependencyObject; must not be touched from worker threads).
        Dispatcher.InvokeAsync(() =>
        {
            if (RoiEditControl.VideoWidth != w || RoiEditControl.VideoHeight != h)
            {
                RoiEditControl.VideoWidth = w;
                RoiEditControl.VideoHeight = h;
            }
        });

        _frameRendererOriginal.Render(frame);

        var frameNo = _frameRendererOriginal.FrameCount;
        var processed = _frameRendererProcessed?.FrameCount ?? 0;
        var elapsed = DateTime.Now - _startTime;
        double fps = elapsed.TotalSeconds > 0 ? frameNo / elapsed.TotalSeconds : 0;

        // StatusText is also a DependencyObject; update via Dispatcher.
        Dispatcher.InvokeAsync(() =>
        {
            StatusText.Text =
                $"Frame {frameNo,6}  " +
                $"Original {w}x{h}  " +
                $"Processed: {processed}  " +
                $"{fps,5:F1} fps";
        });
    }

    private void OnFrameProcessed(FrameData frame, PipelineContext ctx)
    {
        _frameRendererProcessed?.Render(frame);
    }

    private void OnResultProduced(object? sender, PipelineResult e)
    {
        if (e.Value is not EdgeStatistics stats) return;

        Dispatcher.InvokeAsync(() =>
        {
            var roiText = stats.RoiActive
                ? $"({stats.RoiX},{stats.RoiY}) {stats.RoiWidth}x{stats.RoiHeight}"
                : "full";
            StatsText.Text =
                $"Frame:      {stats.FrameNumber}\n" +
                $"Edge px:    {stats.EdgePixelCount}\n" +
                $"Time:       {stats.ProcessingMs:F2} ms\n" +
                $"ROI:        {roiText}";
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    // ---------- ROI (on the original image) ----------

    

    

    

    

    

}