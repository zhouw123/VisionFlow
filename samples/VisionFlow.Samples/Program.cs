using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DirectShowLib;
using VisionFlow.Devices;
using VisionFlow.Integration;
using VisionFlow.Sinks;
using VisionFlow.Sources;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using VisionFlow.Transforms;
using VisionFlow.Transforms.Imaging;
using OpenCvSharp;

namespace VisionFlow.Samples;

internal static class Program
{
    private static int _uiUpdatePending;

    /// <summary>处理模式定义。</summary>
    private sealed class ProcessingMode
    {
        public string Name { get; }
        public Func<IReadOnlyList<FrameComponent>> BuildTransforms { get; }
        public ProcessingMode(string name, Func<IReadOnlyList<FrameComponent>> build)
        {
            Name = name;
            BuildTransforms = build;
        }
        public override string ToString() => Name;
    }

    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Console.WriteLine("VisionFlow - 图像处理沙盒");
        Console.WriteLine("==================================");

        var devices = DeviceEnumerator.EnumerateVideoInputDevices();
        if (devices.Count == 0)
        {
            Console.WriteLine("未找到相机，退出。");
            return 0;
        }

        var device = devices[0];
        for (int i = 1; i < devices.Count; i++) devices[i].Dispose();

        Console.WriteLine($"使用相机: {device.Name}");

        // ---- UI ----
        using var form = new Form
        {
            Text = "VisionFlow - 图像处理沙盒",
            ClientSize = new System.Drawing.Size(1200, 700),
            StartPosition = FormStartPosition.CenterScreen,
            MinimumSize = new System.Drawing.Size(900, 500),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
        form.Controls.Add(layout);

        var videoPanel = new Panel { Dock = DockStyle.Fill };
        var pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = System.Drawing.Color.Black,
        };
        var statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            Text = "等待帧...",
            Font = new System.Drawing.Font("Consolas", 9F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0),
        };

        var buttonBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = System.Drawing.Color.FromArgb(240, 240, 240),
        };

        var modeLabel = new Label
        {
            Text = "处理模式:",
            Left = 8,
            Top = 9,
            Width = 70,
            Height = 20,
        };

        var modeCombo = new ComboBox
        {
            Left = 80,
            Top = 6,
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };

        var camPropsButton = new Button
        {
            Text = "相机属性...",
            Left = 296,
            Top = 5,
            Width = 100,
            Height = 26,
        };

        var snapshotButton = new Button
        {
            Text = "抓图",
            Left = 406,
            Top = 5,
            Width = 80,
            Height = 26,
        };

        var openFolderButton = new Button
        {
            Text = "打开目录",
            Left = 496,
            Top = 5,
            Width = 90,
            Height = 26,
        };

        buttonBar.Controls.Add(modeLabel);
        buttonBar.Controls.Add(modeCombo);
        buttonBar.Controls.Add(camPropsButton);
        buttonBar.Controls.Add(snapshotButton);
        buttonBar.Controls.Add(openFolderButton);

        videoPanel.Controls.Add(pictureBox);
        videoPanel.Controls.Add(statusLabel);
        videoPanel.Controls.Add(buttonBar);
        layout.Controls.Add(videoPanel, 0, 0);

        var paramPanelHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        layout.Controls.Add(paramPanelHost, 1, 0);

        // ---- 处理模式定义 ----
        var modes = new List<ProcessingMode>
        {
            new ProcessingMode("原图", () => Array.Empty<FrameComponent>()),

            new ProcessingMode("灰度", () => new FrameComponent[]
            {
                MakeColorConvert("灰度"),
            }),

            new ProcessingMode("边缘检测（Canny）", () => new FrameComponent[]
            {
                MakeColorConvert("灰度"),
                new CannyTransform(),
            }),

            new ProcessingMode("二值化（OTSU）", () => new FrameComponent[]
            {
                new ThresholdTransform { },
            }),

            new ProcessingMode("轮廓检测", () => new FrameComponent[]
            {
                MakeColorConvert("灰度"),
                new CannyTransform(),
                new ContourTransform(),
            }),

            new ProcessingMode("锐化", () => new FrameComponent[]
            {
                new SharpenTransform(),
            }),

            new ProcessingMode("高斯模糊", () => new FrameComponent[]
            {
                new BlurTransform(),
            }),

            new ProcessingMode("降噪（双边）", () => new FrameComponent[]
            {
                new DenoiseTransform(),
            }),

            new ProcessingMode("形态学：膨胀", () => new FrameComponent[]
            {
                MakeColorConvert("灰度"),
                MakeMorphology("Dilate"),
            }),

            new ProcessingMode("形态学：梯度", () => new FrameComponent[]
            {
                MakeColorConvert("灰度"),
                MakeMorphology("Gradient"),
            }),

            new ProcessingMode("梯度（Sobel）", () => new FrameComponent[]
            {
                new GradientTransform(),
            }),

            new ProcessingMode("HSV 色彩空间", () => new FrameComponent[]
            {
                MakeColorConvert("HSV"),
            }),

            new ProcessingMode("裁剪（中央区域）", () => new FrameComponent[]
            {
                MakeCrop(),
            }),

            new ProcessingMode("透视变换（梯形校正）", () => new FrameComponent[]
            {
                MakePerspective(),
            }),
        };

        foreach (var m in modes) modeCombo.Items.Add(m);

        // ---- 运行时状态 ----
        DirectShowCameraSource? source = null;
        MjpegDecodeTransform? decode = null;
        List<FrameComponent> activeTransforms = new();
        DelegateSink? displaySink = null;
        SnapshotSink? snapshotSink = null;
        StreamingGraph? graph = null;
        DateTime startTime = DateTime.Now;
        long displayFrameCount = 0;

        // 显示 Sink（每帧更新 UI）
        void CreateDisplaySink()
        {
            displaySink = new DelegateSink((frame, ctx) =>
            {
                try
                {
                    var bitmap = FrameToBitmap(frame);
                    if (bitmap is null) return;

                    long frameNo = Interlocked.Increment(ref displayFrameCount);


                    if (Interlocked.CompareExchange(ref _uiUpdatePending, 1, 0) == 0)
                    {
                        form.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                var old = pictureBox.Image;
                                pictureBox.Image = bitmap;
                                old?.Dispose();

                                var elapsed = DateTime.Now - startTime;
                                double fps = frameNo / elapsed.TotalSeconds;
                                statusLabel.Text =
                                    $"帧 {frameNo,6}  " +
                                    $"{frame.Width}x{frame.Height}  " +
                                    $"{fps,5:F1} fps  " +
                                    $"模式: {modeCombo.SelectedItem}";
                            }
                            finally
                            {
                                Interlocked.Exchange(ref _uiUpdatePending, 0);
                            }
                        }));
                    }
                    else
                    {
                        bitmap.Dispose();
                    }
                }
                catch { }
            }, name: "DisplaySink", queueCapacity: 4,
               policy: BackpressurePolicy.DropOldest);
        }

        // 启动一个完整的图
        void StartGraph()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Cursor.Current = Cursors.WaitCursor;

                // 停旧图
                StopAndDisposeGraph();


                // 创建 Source 和 Decode
                source = new DirectShowCameraSource(device);
                source.TrySetFormat(MediaSubType.MJPG, 640, 480);

                decode = new MjpegDecodeTransform();

                // 创建当前模式的 Transform
                var selectedMode = (ProcessingMode)modeCombo.SelectedItem!;
                activeTransforms = new List<FrameComponent>(selectedMode.BuildTransforms());

                // 创建 Sink
                CreateDisplaySink();

                var snapshotDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Snapshots");
                snapshotSink = new SnapshotSink(snapshotDir);

                // 连接：source → decode → [transforms...] → displaySink
                FrameComponent tail = decode;
                source.LinkTo(decode);
                foreach (var t in activeTransforms)
                {
                    tail.LinkTo(t);
                    tail = t;
                }
                tail.LinkTo(displaySink!);

                // 从尾部 Sink 到 SnapshotSink 加一条分支
                tail.LinkTo(snapshotSink);

                // 建图
                graph = StreamingGraph.From(source);


                graph.Start();


                startTime = DateTime.Now;
                displayFrameCount = 0;

                // 重建参数面板
                RebuildParameterPanel();

                Console.WriteLine($"[模式] {selectedMode.Name}，" +
                    $"图含 {graph.ComponentCount} 个组件，总耗时 {sw.ElapsedMilliseconds}ms");

            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败：\n{ex}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        void StopAndDisposeGraph()
        {
            if (graph is not null)
            {
                try { graph.Stop(); } catch { }
                try { graph.Dispose(); } catch { }
                graph = null;
            }

            source = null;
            decode = null;
            displaySink = null;
            snapshotSink = null;
            activeTransforms.Clear();

            // 清空参数面板
            paramPanelHost.Controls.Clear();
        }

        void RebuildParameterPanel()
        {
            paramPanelHost.Controls.Clear();

            var parameterized = activeTransforms
                .OfType<IParameterized>()
                .ToArray();

            if (parameterized.Length == 0)
            {
                paramPanelHost.Controls.Add(new Label
                {
                    Text = "当前模式无参数",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = System.Drawing.Color.Gray,
                });
                return;
            }

            var panel = new ParameterPanel(parameterized)
            {
                Dock = DockStyle.Fill,
            };
            paramPanelHost.Controls.Add(panel);
        }

        // ---- 事件绑定 ----
        modeCombo.SelectedIndex = 0;
        modeCombo.SelectedIndexChanged += (s, e) =>
        {
            if (form.IsHandleCreated)
                StartGraph();
        };

        camPropsButton.Click += (s, e) =>
        {
            var shown = source?.ShowPropertyPage(form.Handle) ?? false;
            if (!shown)
            {
                MessageBox.Show("相机未启动或不支持属性页。", "提示");
            }
        };

        snapshotButton.Click += (s, e) =>
        {
            snapshotSink?.RequestSnapshot();
            Console.WriteLine("[抓图] 已请求。");
        };

        openFolderButton.Click += (s, e) =>
        {
            try
            {
                var dir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Snapshots");
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                });
            }
            catch { }
        };

        form.Shown += (s, e) =>
        {
            // 首次启动
            StartGraph();
        };

        form.FormClosed += (s, e) =>
        {
            StopAndDisposeGraph();
            device.Dispose();
        };

        Application.Run(form);
        return 0;
    }

    // ---- 辅助 ----

    private static ColorConvertTransform MakeColorConvert(string targetName)
    {
        var t = new ColorConvertTransform();
        t.SetParameter("Target", targetName switch
        {
            "灰度" => 1,
            "RGB" => 2,
            "HSV" => 3,
            "YCrCb" => 4,
            "YUV" => 5,
            _ => 0,
        });
        return t;
    }

    /// <summary>创建预配置的裁剪：从中央裁出 320x240 区域。</summary>
    private static CropTransform MakeCrop()
    {
        var t = new CropTransform();
        // 默认图像 640x480，从中央裁出 320x240
        t.SetRegion(160, 120, 320, 240);
        return t;
    }

    /// <summary>
    /// 创建预配置的透视变换：把一个梯形区域拉直成 640x480。
    /// 源点默认把图像上部的两点向内收，模拟"从斜角看矩形"的画面校正。
    /// 用参数面板调 SrcX0/SrcY0 等可以看到实时效果。
    /// </summary>
    private static PerspectiveTransform MakePerspective()
    {
        var t = new PerspectiveTransform();
        // 假设输入 640x480，四个角稍微向内收，形成梯形
        // 点0 = 左上，点1 = 右上，点2 = 右下，点3 = 左下
        t.SetSourcePoints(
            80, 40,     // 左上
            560, 40,    // 右上
            620, 440,   // 右下
            20, 440);   // 左下
        t.SetDestinationSize(640, 480);
        return t;
    }

    private static MorphologyTransform MakeMorphology(string opName)
    {
        var t = new MorphologyTransform();
        t.SetParameter("Operation", opName switch
        {
            "Erode" => 0,
            "Dilate" => 1,
            "Open" => 2,
            "Close" => 3,
            "Gradient" => 4,
            "TopHat" => 5,
            "BlackHat" => 6,
            _ => 0,
        });
        return t;
    }

    private static System.Drawing.Bitmap? FrameToBitmap(FrameData frame)
    {
        int w = frame.Width, h = frame.Height;
        if (w <= 0 || h <= 0) return null;

        var sub = frame.SubType;

        // 通道数
        int channels = FrameFormats.GetChannelCount(sub);
        if (channels == 0) return null;

        int expected = w * h * channels;
        if (frame.Data.Length < expected) return null;

        // 用 OpenCV 统一转成 BGR
        using var bgr = new Mat();
        unsafe
        {
            fixed (byte* pSrc = frame.Data)
            {
                var srcType = channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
                using var src = Mat.FromPixelData(h, w, srcType, (IntPtr)pSrc);

                if (sub == FrameFormats.Gray8)
                    Cv2.CvtColor(src, bgr, ColorConversionCodes.GRAY2BGR);
                else if (sub == FrameFormats.Rgb8)
                    Cv2.CvtColor(src, bgr, ColorConversionCodes.RGB2BGR);
                else if (sub == FrameFormats.Hsv8)
                    Cv2.CvtColor(src, bgr, ColorConversionCodes.HSV2BGR);
                else if (sub == FrameFormats.YCrCb8)
                    Cv2.CvtColor(src, bgr, ColorConversionCodes.YCrCb2BGR);
                else if (sub == FrameFormats.Yuv8)
                    Cv2.CvtColor(src, bgr, ColorConversionCodes.YUV2BGR);
                else
                    src.CopyTo(bgr);  // BGR 已经是 BGR
            }
        }

        if (bgr.Empty()) return null;

        // BGR Mat → Bitmap
        var bitmap = new System.Drawing.Bitmap(w, h, PixelFormat.Format24bppRgb);
        var data = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, w, h),
            ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            int rowBytes = w * 3;
            unsafe
            {
                byte* pSrc = (byte*)bgr.Data.ToPointer();
                byte* pDst = (byte*)data.Scan0.ToPointer();
                if (stride == rowBytes)
                {
                    Buffer.MemoryCopy(pSrc, pDst, (long)stride * h, (long)rowBytes * h);
                }
                else
                {
                    for (int y = 0; y < h; y++)
                    {
                        Buffer.MemoryCopy(pSrc + y * rowBytes, pDst + y * stride,
                            stride, rowBytes);
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }
}