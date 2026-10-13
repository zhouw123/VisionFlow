using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VisionFlow.WpfSamples.Controls;

/// <summary>
/// Overlay editor for selecting a region on top of a video image.
/// 
/// Rect mode (R.2): corner/edge handles + interior move.
/// Quad mode (R.3): 4 corner handles + interior move + convexity check.
/// </summary>
public sealed class RoiEditor : Canvas
{
    // ---- Dependency Properties ----

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode), typeof(RoiEditMode), typeof(RoiEditor),
            new FrameworkPropertyMetadata(RoiEditMode.Rect,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnAnyChanged));

    public RoiEditMode Mode
    {
        get => (RoiEditMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static readonly DependencyProperty VideoWidthProperty =
        DependencyProperty.Register(
            nameof(VideoWidth), typeof(int), typeof(RoiEditor),
            new FrameworkPropertyMetadata(640,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnAnyChanged));

    public int VideoWidth
    {
        get => (int)GetValue(VideoWidthProperty);
        set => SetValue(VideoWidthProperty, value);
    }

    public static readonly DependencyProperty VideoHeightProperty =
        DependencyProperty.Register(
            nameof(VideoHeight), typeof(int), typeof(RoiEditor),
            new FrameworkPropertyMetadata(480,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnAnyChanged));

    public int VideoHeight
    {
        get => (int)GetValue(VideoHeightProperty);
        set => SetValue(VideoHeightProperty, value);
    }

    public static readonly DependencyProperty RectProperty =
        DependencyProperty.Register(
            nameof(Rect), typeof(NormalizedRect), typeof(RoiEditor),
            new FrameworkPropertyMetadata(NormalizedRect.FullFrame,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnAnyChanged));

    public NormalizedRect Rect
    {
        get => (NormalizedRect)GetValue(RectProperty);
        set => SetValue(RectProperty, value);
    }

    public static readonly DependencyProperty QuadProperty =
        DependencyProperty.Register(
            nameof(Quad), typeof(NormalizedQuad), typeof(RoiEditor),
            new FrameworkPropertyMetadata(NormalizedQuad.FullFrame,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnAnyChanged));

    public NormalizedQuad Quad
    {
        get => (NormalizedQuad)GetValue(QuadProperty);
        set => SetValue(QuadProperty, value);
    }

    // ---- Events ----

    public event EventHandler? RoiChanged;
    public event EventHandler? RoiChangeCompleted;

    // ---- Visuals ----

    private const double RectHandleSize = 8;
    private const double QuadHandleSize = 10;
    private const double HitRadiusPx = 12;
    private const double MinSize = 0.02;

    private readonly Rectangle _rectFill;
    private readonly Rectangle _rectBorder;
    private readonly Rectangle[] _rectHandles = new Rectangle[8];

    private readonly Polygon _quadFill;
    private readonly Polygon _quadBorder;
    private readonly Ellipse[] _quadHandles = new Ellipse[4];

    private static readonly Brush FillBrush;
    private static readonly Brush BorderBrush = Brushes.Yellow;
    private static readonly Brush HandleFill = Brushes.White;
    private static readonly Brush HandleStroke = Brushes.DimGray;

    static RoiEditor()
    {
        var fill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0x00));
        fill.Freeze();
        FillBrush = fill;
        HandleFill.Freeze();
        HandleStroke.Freeze();
    }

    // ---- Rect interaction state ----

    private enum RectDragMode
    {
        None, Move,
        Left, Right, Top, Bottom,
        TopLeft, TopRight, BottomLeft, BottomRight,
    }

    private RectDragMode _dragMode = RectDragMode.None;
    private NormalizedRect _dragStartRect;
    private double _dragStartNx;
    private double _dragStartNy;

    // ---- Quad interaction state ----

    private int _quadDragCorner = -1;              // 0..3 or -1 (none)
    private bool _quadMoving;                      // interior drag
    private NormalizedQuad _dragStartQuad;
    private double _quadStartNx;
    private double _quadStartNy;

    public RoiEditor()
    {
        Background = Brushes.Transparent;
        ClipToBounds = true;

        _rectFill = new Rectangle { Fill = FillBrush, IsHitTestVisible = false };
        _rectBorder = new Rectangle
        {
            Stroke = BorderBrush, StrokeThickness = 2,
            Fill = null, IsHitTestVisible = false,
        };
        Children.Add(_rectFill);
        Children.Add(_rectBorder);

        for (int i = 0; i < 8; i++)
        {
            _rectHandles[i] = new Rectangle
            {
                Width = RectHandleSize, Height = RectHandleSize,
                Fill = HandleFill, Stroke = HandleStroke, StrokeThickness = 1,
                IsHitTestVisible = false,
            };
            Children.Add(_rectHandles[i]);
        }

        _quadFill = new Polygon { Fill = FillBrush, IsHitTestVisible = false };
        _quadBorder = new Polygon
        {
            Stroke = BorderBrush, StrokeThickness = 2,
            Fill = null, IsHitTestVisible = false,
        };
        Children.Add(_quadFill);
        Children.Add(_quadBorder);

        for (int i = 0; i < 4; i++)
        {
            _quadHandles[i] = new Ellipse
            {
                Width = QuadHandleSize, Height = QuadHandleSize,
                Fill = HandleFill, Stroke = HandleStroke, StrokeThickness = 1,
                IsHitTestVisible = false,
            };
            Children.Add(_quadHandles[i]);
        }

        SizeChanged += (s, e) => Redraw();
        Loaded += (s, e) => Redraw();
    }

    private static void OnAnyChanged(DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is RoiEditor editor) editor.Redraw();
    }

    // ---- Coordinate helpers ----

    private (double ox, double oy, double w, double h) GetDisplayRect()
    {
        double cw = ActualWidth, ch = ActualHeight;
        if (cw <= 0 || ch <= 0 || VideoWidth <= 0 || VideoHeight <= 0)
            return (0, 0, cw, ch);

        double imageAspect = VideoWidth / (double)VideoHeight;
        double ctrlAspect = cw / ch;

        double dw, dh, ox, oy;
        if (ctrlAspect > imageAspect)
        {
            dh = ch; dw = dh * imageAspect; ox = (cw - dw) / 2; oy = 0;
        }
        else
        {
            dw = cw; dh = dw / imageAspect; ox = 0; oy = (ch - dh) / 2;
        }
        return (ox, oy, dw, dh);
    }

    private Point NormalizedToScreen(double nx, double ny)
    {
        var (ox, oy, w, h) = GetDisplayRect();
        return new Point(ox + nx * w, oy + ny * h);
    }

    public (double nx, double ny) ScreenToNormalized(double x, double y)
    {
        var (ox, oy, w, h) = GetDisplayRect();
        if (w <= 0 || h <= 0) return (0, 0);
        return ((x - ox) / w, (y - oy) / h);
    }

    // ---- Drawing ----

    public void Redraw()
    {
        if (Mode == RoiEditMode.Rect)
        {
            SetRectVisualsVisible(true);
            SetQuadVisualsVisible(false);
            DrawRect();
        }
        else
        {
            SetRectVisualsVisible(false);
            SetQuadVisualsVisible(true);
            DrawQuad();
        }
    }

    private void SetRectVisualsVisible(bool visible)
    {
        var v = visible ? Visibility.Visible : Visibility.Collapsed;
        _rectFill.Visibility = v;
        _rectBorder.Visibility = v;
        for (int i = 0; i < 8; i++) _rectHandles[i].Visibility = v;
    }

    private void SetQuadVisualsVisible(bool visible)
    {
        var v = visible ? Visibility.Visible : Visibility.Collapsed;
        _quadFill.Visibility = v;
        _quadBorder.Visibility = v;
        for (int i = 0; i < 4; i++) _quadHandles[i].Visibility = v;
    }

    private void DrawRect()
    {
        var p0 = NormalizedToScreen(Rect.X, Rect.Y);
        var p1 = NormalizedToScreen(Rect.Right, Rect.Bottom);

        double x = p0.X, y = p0.Y;
        double w = Math.Max(0, p1.X - p0.X);
        double h = Math.Max(0, p1.Y - p0.Y);

        Canvas.SetLeft(_rectFill, x); Canvas.SetTop(_rectFill, y);
        _rectFill.Width = w; _rectFill.Height = h;

        Canvas.SetLeft(_rectBorder, x); Canvas.SetTop(_rectBorder, y);
        _rectBorder.Width = w; _rectBorder.Height = h;

        double hs = RectHandleSize, half = hs / 2;
        var pos = new (double cx, double cy)[]
        {
            (x, y), (x + w / 2, y), (x + w, y), (x + w, y + h / 2),
            (x + w, y + h), (x + w / 2, y + h), (x, y + h), (x, y + h / 2),
        };
        for (int i = 0; i < 8; i++)
        {
            Canvas.SetLeft(_rectHandles[i], pos[i].cx - half);
            Canvas.SetTop(_rectHandles[i], pos[i].cy - half);
        }
    }

    private void DrawQuad()
    {
        var screenPts = new PointCollection(4);
        for (int i = 0; i < 4; i++)
        {
            var np = Quad[i];
            screenPts.Add(NormalizedToScreen(np.X, np.Y));
        }

        _quadFill.Points = screenPts;
        _quadBorder.Points = screenPts;

        double half = QuadHandleSize / 2;
        for (int i = 0; i < 4; i++)
        {
            Canvas.SetLeft(_quadHandles[i], screenPts[i].X - half);
            Canvas.SetTop(_quadHandles[i], screenPts[i].Y - half);
        }
    }

    // ---- Mouse dispatch ----

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos = e.GetPosition(this);

        if (Mode == RoiEditMode.Rect)
        {
            var (nx, ny) = ScreenToNormalized(pos.X, pos.Y);
            var mode = HitTestRect(pos.X, pos.Y);
            if (mode == RectDragMode.None) return;

            _dragMode = mode;
            _dragStartRect = Rect;
            _dragStartNx = nx;
            _dragStartNy = ny;

            CaptureMouse();
            e.Handled = true;
        }
        else // Quad
        {
            int corner = HitTestQuadCorner(pos.X, pos.Y);
            bool moving = corner < 0 && HitTestQuadInterior(pos.X, pos.Y);
            if (corner < 0 && !moving) return;

            _quadDragCorner = corner;
            _quadMoving = moving;

            var (nx, ny) = ScreenToNormalized(pos.X, pos.Y);
            _dragStartQuad = Quad;
            _quadStartNx = nx;
            _quadStartNy = ny;

            CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);

        if (Mode == RoiEditMode.Rect)
        {
            if (_dragMode == RectDragMode.None) { UpdateCursorRect(pos); return; }

            var (nx, ny) = ScreenToNormalized(pos.X, pos.Y);
            ApplyRectDrag(nx, ny);
            Redraw();
            RaiseRoiChanged();
        }
        else // Quad
        {
            if (_quadDragCorner < 0 && !_quadMoving) { UpdateCursorQuad(pos); return; }

            var (nx, ny) = ScreenToNormalized(pos.X, pos.Y);
            ApplyQuadDrag(nx, ny);
            Redraw();
            RaiseRoiChanged();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (Mode == RoiEditMode.Rect)
        {
            if (_dragMode == RectDragMode.None) return;
            ReleaseMouseCapture();
            _dragMode = RectDragMode.None;
        }
        else
        {
            if (_quadDragCorner < 0 && !_quadMoving) return;
            ReleaseMouseCapture();
            _quadDragCorner = -1;
            _quadMoving = false;
        }

        Cursor = Cursors.Arrow;
        RaiseRoiChangeCompleted();
        e.Handled = true;
    }

    // ---- Rect hit testing / drag ----

    private RectDragMode HitTestRect(double screenX, double screenY)
    {
        var (ox, oy, dw, dh) = GetDisplayRect();
        if (dw <= 0 || dh <= 0) return RectDragMode.None;

        double x = ox + Rect.X * dw;
        double y = oy + Rect.Y * dh;
        double w = Rect.Width * dw;
        double h = Rect.Height * dh;
        double right = x + w;
        double bottom = y + h;

        var anchors = new (double cx, double cy, RectDragMode mode)[]
        {
            (x, y, RectDragMode.TopLeft),
            (x + w / 2, y, RectDragMode.Top),
            (right, y, RectDragMode.TopRight),
            (right, y + h / 2, RectDragMode.Right),
            (right, bottom, RectDragMode.BottomRight),
            (x + w / 2, bottom, RectDragMode.Bottom),
            (x, bottom, RectDragMode.BottomLeft),
            (x, y + h / 2, RectDragMode.Left),
        };

        double bestDistSq = HitRadiusPx * HitRadiusPx;
        RectDragMode best = RectDragMode.None;
        foreach (var (cx, cy, m) in anchors)
        {
            double dx = screenX - cx, dy = screenY - cy;
            double d2 = dx * dx + dy * dy;
            if (d2 <= bestDistSq) { bestDistSq = d2; best = m; }
        }
        if (best != RectDragMode.None) return best;

        if (screenX >= x && screenX <= right && screenY >= y && screenY <= bottom)
            return RectDragMode.Move;

        return RectDragMode.None;
    }

    private void UpdateCursorRect(Point pos)
    {
        var mode = HitTestRect(pos.X, pos.Y);
        Cursor = mode switch
        {
            RectDragMode.None => Cursors.Arrow,
            RectDragMode.Move => Cursors.SizeAll,
            RectDragMode.Left or RectDragMode.Right => Cursors.SizeWE,
            RectDragMode.Top or RectDragMode.Bottom => Cursors.SizeNS,
            RectDragMode.TopLeft or RectDragMode.BottomRight => Cursors.SizeNWSE,
            RectDragMode.TopRight or RectDragMode.BottomLeft => Cursors.SizeNESW,
            _ => Cursors.Arrow,
        };
    }

    private void ApplyRectDrag(double nx, double ny)
    {
        var r = _dragStartRect;

        switch (_dragMode)
        {
            case RectDragMode.Move:
            {
                double dx = nx - _dragStartNx, dy = ny - _dragStartNy;
                double newX = Clamp(r.X + dx, 0, 1 - r.Width);
                double newY = Clamp(r.Y + dy, 0, 1 - r.Height);
                Rect = new NormalizedRect(newX, newY, r.Width, r.Height);
                break;
            }
            case RectDragMode.Left:
            {
                double newX = Clamp(nx, 0, r.Right - MinSize);
                Rect = new NormalizedRect(newX, r.Y, r.Right - newX, r.Height);
                break;
            }
            case RectDragMode.Right:
            {
                double newRight = Clamp(nx, r.X + MinSize, 1);
                Rect = new NormalizedRect(r.X, r.Y, newRight - r.X, r.Height);
                break;
            }
            case RectDragMode.Top:
            {
                double newY = Clamp(ny, 0, r.Bottom - MinSize);
                Rect = new NormalizedRect(r.X, newY, r.Width, r.Bottom - newY);
                break;
            }
            case RectDragMode.Bottom:
            {
                double newBottom = Clamp(ny, r.Y + MinSize, 1);
                Rect = new NormalizedRect(r.X, r.Y, r.Width, newBottom - r.Y);
                break;
            }
            case RectDragMode.TopLeft:
            {
                double newX = Clamp(nx, 0, r.Right - MinSize);
                double newY = Clamp(ny, 0, r.Bottom - MinSize);
                Rect = new NormalizedRect(newX, newY, r.Right - newX, r.Bottom - newY);
                break;
            }
            case RectDragMode.TopRight:
            {
                double newRight = Clamp(nx, r.X + MinSize, 1);
                double newY = Clamp(ny, 0, r.Bottom - MinSize);
                Rect = new NormalizedRect(r.X, newY, newRight - r.X, r.Bottom - newY);
                break;
            }
            case RectDragMode.BottomLeft:
            {
                double newX = Clamp(nx, 0, r.Right - MinSize);
                double newBottom = Clamp(ny, r.Y + MinSize, 1);
                Rect = new NormalizedRect(newX, r.Y, r.Right - newX, newBottom - r.Y);
                break;
            }
            case RectDragMode.BottomRight:
            {
                double newRight = Clamp(nx, r.X + MinSize, 1);
                double newBottom = Clamp(ny, r.Y + MinSize, 1);
                Rect = new NormalizedRect(r.X, r.Y, newRight - r.X, newBottom - r.Y);
                break;
            }
        }
    }

    // ---- Quad hit testing / drag ----

    private int HitTestQuadCorner(double screenX, double screenY)
    {
        var (ox, oy, dw, dh) = GetDisplayRect();
        if (dw <= 0 || dh <= 0) return -1;

        double bestDistSq = HitRadiusPx * HitRadiusPx;
        int best = -1;
        for (int i = 0; i < 4; i++)
        {
            var np = Quad[i];
            double cx = ox + np.X * dw;
            double cy = oy + np.Y * dh;
            double dx = screenX - cx, dy = screenY - cy;
            double d2 = dx * dx + dy * dy;
            if (d2 <= bestDistSq) { bestDistSq = d2; best = i; }
        }
        return best;
    }

    private bool HitTestQuadInterior(double screenX, double screenY)
    {
        // Ray-casting point-in-polygon for the 4 vertices
        var pts = new (double x, double y)[4];
        var (ox, oy, dw, dh) = GetDisplayRect();
        if (dw <= 0 || dh <= 0) return false;

        for (int i = 0; i < 4; i++)
        {
            var np = Quad[i];
            pts[i] = (ox + np.X * dw, oy + np.Y * dh);
        }

        bool inside = false;
        for (int i = 0, j = 3; i < 4; j = i++)
        {
            if (((pts[i].y > screenY) != (pts[j].y > screenY)) &&
                (screenX < (pts[j].x - pts[i].x) * (screenY - pts[i].y) /
                          (pts[j].y - pts[i].y) + pts[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private void UpdateCursorQuad(Point pos)
    {
        if (HitTestQuadCorner(pos.X, pos.Y) >= 0)
        {
            Cursor = Cursors.Cross;
        }
        else if (HitTestQuadInterior(pos.X, pos.Y))
        {
            Cursor = Cursors.SizeAll;
        }
        else
        {
            Cursor = Cursors.Arrow;
        }
    }

    private void ApplyQuadDrag(double nx, double ny)
    {
        var q = _dragStartQuad;

        if (_quadMoving)
        {
            double dx = nx - _quadStartNx;
            double dy = ny - _quadStartNy;

            // Clamp delta so no vertex leaves [0, 1]
            double minDx = -Min(q.P0.X, Min(q.P1.X, Min(q.P2.X, q.P3.X)));
            double maxDx = 1 - Max(q.P0.X, Max(q.P1.X, Max(q.P2.X, q.P3.X)));
            double minDy = -Min(q.P0.Y, Min(q.P1.Y, Min(q.P2.Y, q.P3.Y)));
            double maxDy = 1 - Max(q.P0.Y, Max(q.P1.Y, Max(q.P2.Y, q.P3.Y)));

            dx = Clamp(dx, minDx, maxDx);
            dy = Clamp(dy, minDy, maxDy);

            var candidate = new NormalizedQuad(
                new NormalizedPoint(q.P0.X + dx, q.P0.Y + dy),
                new NormalizedPoint(q.P1.X + dx, q.P1.Y + dy),
                new NormalizedPoint(q.P2.X + dx, q.P2.Y + dy),
                new NormalizedPoint(q.P3.X + dx, q.P3.Y + dy));

            if (candidate.IsConvex()) Quad = candidate;
            return;
        }

        if (_quadDragCorner < 0 || _quadDragCorner > 3) return;

        double cnx = Clamp(nx, 0, 1);
        double cny = Clamp(ny, 0, 1);
        var newPt = new NormalizedPoint(cnx, cny);

        var updated = q.WithPoint(_quadDragCorner, newPt);
        if (updated.IsConvex()) Quad = updated;
    }

    private static double Min(double a, double b) => Math.Min(a, b);
    private static double Max(double a, double b) => Math.Max(a, b);
    private static double Clamp(double v, double min, double max)
        => v < min ? min : (v > max ? max : v);

    // ---- Event risers ----

    internal void RaiseRoiChanged() => RoiChanged?.Invoke(this, EventArgs.Empty);
    internal void RaiseRoiChangeCompleted() => RoiChangeCompleted?.Invoke(this, EventArgs.Empty);
}