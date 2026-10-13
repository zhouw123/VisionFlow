# VisionFlow

为 **USB 相机采集 + 图像处理** 场景设计的 .NET 桌面应用框架。

**核心理念**：DirectShow 只负责采集，转码、处理、渲染、分流全部在 C# 层用 OpenCV 完成。
框架内置流式处理管线、参数化机制、交互式 ROI 编辑器和一套常用的图像处理组件。

---

## 项目用途

本项目面向以下典型场景：

- **工业视觉**：USB 相机的实时采集 + 检测/测量/识别，结果在原始画面上叠加显示
- **教学演示**：直观展示各种图像处理算法的效果，参数可实时调整
- **快速原型**：需要从相机拿到数据、做处理、显示结果，而不想深陷 DirectShow 的 COM 细节
- **定制开发**：作为基础框架，在此之上扩展自定义的处理组件、数据源或输出方式

---

## 核心特性

### 流式框架

- 三种组件抽象：`SourceComponent`（源）/ `TransformComponent`（转换）/ `SinkComponent`（输出）
- 每个组件独立线程 + 队列，天然并行
- 支持任意拓扑：一条链、分支（一分二）、多路汇聚
- 可配置背压策略（`DropOldest` 保持流畅 / `Block` 保证不丢帧）

### 参数化机制

- 每个组件通过 `RegisterParameter` 暴露运行时参数
- 参数类型自动映射到编辑器（数字框、下拉框、颜色选择器、复选框）
- 支持双向同步：UI 修改 → 组件即时生效；组件内部变更 → UI 自动刷新

### 交互式 ROI 编辑器

- `Rect` 模式：轴对齐矩形（用于裁剪、区域处理）
- `Quad` 模式：四点四边形（用于透视校正）
- 归一化坐标输出（`[0, 1]`），与视频分辨率解耦
- 拖角 / 拖边 / 拖内部，带边界约束和凸性检查

### 双视频显示

- 上方：原始视频流
- 下方：处理后视频流
- 两路并行，直观对比处理前后效果

### 结构化数据通道

- 处理组件通过 `PipelineContext` 把分析结果（如边缘像素数、检测框坐标）发送到 UI
- 与图像数据独立：图像可丢帧求流畅，结构化数据不丢

### 图像处理组件库

- 12+ 个开箱即用的 Transform：色彩空间转换、二值化、形态学、边缘检测、轮廓、透视、裁剪、锐化、模糊、降噪、梯度
- 同时支持 BGR 和灰度输入

---

## 架构总览

```
应用层（WPF / WinForms）
        ↓
流式框架（Streaming）
  SourceComponent / TransformComponent / SinkComponent
  StreamingGraph / PipelineContext / IParameterized
        ↓
具体组件
  Sources/        DirectShowCameraSource
  Transforms/     色彩、边缘、形态学、透视、裁剪...
  Sinks/          Delegate / Null / Snapshot
        ↓
DirectShow 封装（仅 Source 使用）
  FilterGraph / SampleGrabber / 设备枚举 / 参数控制
```

**分层职责**：

| 层 | 职责 |
| :--- | :--- |
| **Infrastructure** | HRESULT、COM、媒体类型、FrameId |
| **Devices** | 设备枚举、StreamConfig、相机参数控制 |
| **Filters** | DirectShow 过滤器基类与具体实现 |
| **Graph** | FilterGraph 生命周期管理 |
| **Integration** | FrameData、PipelineContext、PipelineResult |
| **Streaming** | 流式框架核心（图、组件、参数化） |
| **Sources** | 具体数据源 |
| **Transforms** | 图像处理组件 |
| **Sinks** | 输出组件 |

---

## 目录结构

```
VisionFlow/
├── VisionFlow.sln                       解决方案
├── README.md                            本文件
├── Directory.Build.props                统一编译配置
│
├── src/VisionFlow/                      主库
│   ├── Infrastructure/                  HRESULT、COM、媒体类型、FrameId
│   │   ├── HResultHelper.cs             HRESULT 检查与异常转换
│   │   ├── ComHelper.cs                 COM 对象安全释放
│   │   ├── PinHelper.cs                 Pin 枚举与查询
│   │   ├── MediaTypeHelper.cs           媒体类型构造与释放
│   │   ├── VideoInfoParser.cs           VIDEOINFOHEADER 内存解析
│   │   ├── DirectShowClsIds.cs          系统 CLSID 常量
│   │   ├── FrameId.cs                   帧身份（帧号 + 采样时间）
│   │   └── PlatformAttributes.cs        Windows 平台声明
│   │
│   ├── Devices/                         设备与参数服务
│   │   ├── DeviceEnumerator.cs          相机枚举
│   │   ├── DeviceInfo.cs                设备信息
│   │   ├── VideoFormat.cs               格式描述
│   │   ├── StreamConfig.cs              分辨率/帧率枚举与设置
│   │   ├── CameraControl.cs             曝光、对焦、变焦
│   │   ├── VideoProcAmp.cs              亮度、对比度、白平衡
│   │   ├── PropertyRange.cs             参数范围
│   │   └── PropertyPage.cs              相机原生属性页
│   │
│   ├── Filters/                         DirectShow 过滤器封装
│   │   ├── FilterBase.cs                过滤器基类
│   │   ├── Pin.cs                       Pin 封装
│   │   ├── CaptureFilterBase.cs         捕获过滤器基类
│   │   ├── TransformFilterBase.cs       转换过滤器基类
│   │   ├── RendererFilterBase.cs        渲染过滤器基类
│   │   ├── UsbCameraFilter.cs           USB 相机
│   │   ├── VideoRenderer.cs             VMR-9 渲染器
│   │   ├── SampleGrabberFilter.cs       抓帧过滤器
│   │   ├── NullRendererFilter.cs        空渲染器
│   │   ├── SmartTeeFilter.cs            分流器（备选）
│   │   └── ColorSpaceConverterFilter.cs 色彩空间转换
│   │
│   ├── Graph/                           图协调
│   │   ├── FilterGraph.cs               过滤器图管理
│   │   └── MediaEventPump.cs            媒体事件订阅
│   │
│   ├── Integration/                     数据单元
│   │   ├── FrameData.cs                 帧数据
│   │   ├── FrameFormats.cs              自定义格式 GUID
│   │   ├── PipelineContext.cs           共享上下文
│   │   ├── PipelineResult.cs            结构化结果
│   │   └── BackpressurePolicy.cs        背压策略
│   │
│   ├── Streaming/                       流式框架
│   │   ├── IFrameSource.cs              源接口
│   │   ├── IFrameTransform.cs           转换接口
│   │   ├── IFrameSink.cs                输出接口
│   │   ├── FrameComponent.cs            组件基类
│   │   ├── SourceComponent.cs           源基类
│   │   ├── TransformComponent.cs        转换基类
│   │   ├── SinkComponent.cs             输出基类
│   │   ├── StreamingGraph.cs            图构建与生命周期
│   │   ├── ComponentErrorEventArgs.cs   错误事件
│   │   ├── Parameters/                  参数化机制
│   │   │   ├── IParameterized.cs
│   │   │   ├── ParameterKind.cs
│   │   │   ├── ParameterDescriptor.cs
│   │   │   ├── ParameterBinding.cs
│   │   │   ├── ParameterChangedEventArgs.cs
│   │   │   └── EnumOption.cs
│   │   └── ComponentState.cs
│   │
│   ├── Sources/                         具体数据源
│   │   └── DirectShowCameraSource.cs    USB 相机源
│   │
│   ├── Transforms/                      图像处理组件
│   │   ├── MjpegDecodeTransform.cs      MJPG 解码
│   │   ├── Yuy2ToBgrTransform.cs        YUY2 解码
│   │   ├── CannyTransform.cs            边缘检测 + ROI + 统计
│   │   ├── EdgeStatistics.cs            边缘统计结果
│   │   └── Imaging/                     图像处理算法
│   │       ├── ColorConvertTransform.cs 色彩空间转换
│   │       ├── ThresholdTransform.cs    二值化
│   │       ├── GradientTransform.cs     梯度（Sobel/Laplacian）
│   │       ├── ContourTransform.cs      轮廓检测 + 多边形逼近
│   │       ├── PerspectiveTransform.cs  四点透视变换
│   │       ├── CropTransform.cs         裁剪
│   │       ├── SharpenTransform.cs      锐化
│   │       ├── BlurTransform.cs         模糊
│   │       ├── DenoiseTransform.cs      降噪
│   │       └── MorphologyTransform.cs   形态学运算
│   │
│   └── Sinks/                           输出组件
│       ├── DelegateSink.cs              委托式输出
│       ├── NullSink.cs                  空输出
│       └── SnapshotSink.cs              抓图输出
│
├── tests/VisionFlow.Tests/              单元测试
│   ├── Infrastructure/
│   ├── Devices/
│   ├── Filters/
│   ├── Integration/
│   ├── Sinks/
│   ├── Sources/
│   ├── Streaming/
│   └── Transforms/
│
└── samples/
    ├── VisionFlow.Samples/              WinForms 示例
    │   ├── Program.cs                   控制台沙盒（14 种模式）
    │   └── ParameterPanel.cs            手写参数面板
    │
    └── VisionFlow.WpfSamples/           WPF 示例
        ├── App.xaml
        ├── MainWindow.xaml              双视频 + 参数面板布局
        ├── MainWindow.xaml.cs           编排者
        ├── Controls/                    通用 UI 控件
        │   ├── RoiEditor.cs             ROI 交互编辑器
        │   ├── RoiEditMode.cs
        │   ├── NormalizedPoint.cs
        │   ├── NormalizedRect.cs
        │   ├── NormalizedQuad.cs
        │   └── CompactColorPicker.cs    紧凑型颜色选择器
        ├── Parameters/                  PropertyGrid 参数化
        │   ├── ParameterPropertyGrid.cs 自定义 PropertyGrid
        │   ├── ParameterPropertyItem.cs
        │   ├── ParameterProxy.cs        参数代理（绑定源）
        │   └── Editors/
        │       ├── ParameterEnumPropertyEditor.cs
        │       ├── ParameterColorPropertyEditor.cs
        │       └── ParameterPlainTextEditor.cs
        ├── Modes/                       处理模式定义
        │   ├── ProcessingMode.cs
        │   └── ProcessingModes.cs
        ├── Rendering/                   帧渲染
        │   └── FrameRenderer.cs         帧转 BitmapSource + 节流
        └── Coordination/                ROI 与 Transform 联动
            └── RoiCoordinator.cs
```

---

## 快速上手

### 最小示例

```csharp
using VisionFlow.Devices;
using VisionFlow.Sinks;
using VisionFlow.Sources;
using VisionFlow.Streaming;
using VisionFlow.Transforms;
using VisionFlow.Transforms.Imaging;

// 1. 枚举相机
var devices = DeviceEnumerator.EnumerateVideoInputDevices();
var source = new DirectShowCameraSource(devices[0]);
source.TrySetFormat(MediaSubType.MJPG, 640, 480);

// 2. 创建处理链
var decode  = new MjpegDecodeTransform();
var gray    = new ColorConvertTransform();
var canny   = new CannyTransform();

// 3. 输出组件
var sink = new DelegateSink((frame, ctx) =>
{
    // frame.SubType == MediaSubType.RGB24
    // frame.Data 是 BGR 字节流，可包装为 OpenCV Mat
});

// 4. 连接：source -> decode -> gray -> canny -> sink
source.LinkTo(decode).LinkTo(gray).LinkTo(canny).LinkTo(sink);

// 5. 启动
using var graph = StreamingGraph.From(source);
graph.Start();

// ... 运行 ...

graph.Stop();
```

### 分流（一路处理，多路消费）

```csharp
canny.LinkTo(displaySink);   // 显示分支
canny.LinkTo(snapshotSink);  // 抓图分支
```

### 运行时调参

```csharp
canny.SetParameter("Threshold1", 100);
var current = canny.GetParameter("Threshold1");
```

### 结构化数据

在 Transform 内部：

```csharp
context.Set(new EdgeStatistics { EdgePixelCount = n }, input.Id);
```

在应用层：

```csharp
graph.Context.ResultProduced += (s, e) =>
{
    if (e.Value is EdgeStatistics stats)
    {
        Dispatcher.InvokeAsync(() => UpdateUi(stats));
    }
};
```

---

## 关键概念

| 概念 | 作用 |
| :--- | :--- |
| `FrameData` | 流式管线中流动的数据单元，携带 FrameId、宽高、格式、字节 |
| `FrameId` | 帧身份：帧号 + 采样时间 + 真实时间，跨组件唯一标识 |
| `PipelineContext` | 跨组件共享上下文，结构化数据的唯一通道 |
| `StreamingGraph` | 图的生命周期管理：Start / Stop / Dispose |
| `IParameterized` | 组件暴露的参数接口，UI 通过它生成控件 |
| `ParameterProxy` | 参数与 WPF Binding 的适配层（绑定路径恒为 "Value"） |
| `BackpressurePolicy` | 队列满时的策略：DropOldest（流畅）/ Block（不丢） |
| `RoiEditor` | 交互式 ROI 编辑器，输出归一化坐标 |

---

## 内置组件

### Sources（数据源）

| 类 | 说明 |
| :--- | :--- |
| `DirectShowCameraSource` | USB 相机，基于 DirectShow + SampleGrabber |

### Transforms（处理组件）

| 类 | 输入 | 输出 | 参数 |
| :--- | :--- | :--- | :--- |
| `MjpegDecodeTransform` | MJPG | BGR | 无 |
| `Yuy2ToBgrTransform` | YUY2 | BGR | 无 |
| `ColorConvertTransform` | BGR/GRAY | 可选 BGR/灰度/RGB/HSV/YCrCb/YUV | Target |
| `CannyTransform` | BGR/GRAY | 同输入 | 阈值、颜色、厚度、ROI |
| `ThresholdTransform` | BGR/GRAY | GRAY | 方法、阈值、块大小 |
| `GradientTransform` | BGR/GRAY | GRAY | 方法、核大小、缩放 |
| `ContourTransform` | BGR/GRAY | BGR | 边缘方法、面积、逼近精度、颜色 |
| `PerspectiveTransform` | BGR | BGR | 源四点、输出尺寸 |
| `CropTransform` | BGR/GRAY | 同输入 | X/Y/宽/高 |
| `SharpenTransform` | BGR/GRAY | 同输入 | 强度、核大小 |
| `BlurTransform` | BGR/GRAY | 同输入 | 类型、核大小、Sigma |
| `DenoiseTransform` | BGR/GRAY | 同输入 | 类型、强度、处理分辨率 |
| `MorphologyTransform` | BGR/GRAY | 同输入 | 运算、形状、核大小、迭代 |

### Sinks（输出组件）

| 类 | 说明 |
| :--- | :--- |
| `DelegateSink` | 接受用户委托，每帧回调，最灵活 |
| `NullSink` | 消费帧但不处理，用于分流场景 |
| `SnapshotSink` | 按请求保存当前帧为图片（PNG/JPEG/BMP） |

---

## WPF 示例

`samples/VisionFlow.WpfSamples/` 演示一个完整应用：

### 界面布局

```
┌─────────────────────────────────────┬──────────────┐
│  原始视频（含交互式 ROI 覆盖层）      │  实时分析     │
├─────────────────────────────────────┼──────────────┤
│  处理后视频                          │  参数面板     │
│                                     │  (PropertyGrid)│
├─────────────────────────────────────┤              │
│  状态栏：帧号 / 尺寸 / FPS            │              │
└─────────────────────────────────────┴──────────────┘
```

### 功能

- **14 种处理模式**：原图、灰度、边缘检测、二值化、轮廓、锐化、模糊、降噪、形态学膨胀/梯度、梯度、HSV、裁剪、透视变换
- **交互式 ROI**：在原始视频上拖动鼠标调整区域；不同模式自动切换 Rect/Quad
- **参数面板**：HandyControl PropertyGrid，根据组件参数自动生成控件；支持双向同步
- **实时分析**：结构化数据（如边缘像素数、处理耗时）实时刷新
- **抓图**：抓取处理后的一帧保存到 `Snapshots/` 目录
- **相机属性**：弹出驱动提供的原生属性页（曝光、亮度等）

### 使用要点

- 切换模式时，ROI 编辑器自动应用该模式的预设（如 Crop 的中央矩形、Perspective 的梯形）
- 拖动 ROI 后，处理结果立即更新；反之，在参数面板修改坐标，ROI 视觉框也同步
- 按 `Reset ROI` 恢复到预设；按 `Clear ROI` 清除到全图

---

## 技术栈

| 组件 | 用途 |
| :--- | :--- |
| .NET 8 | 目标框架（主库 `net8.0`，示例 `net8.0-windows`） |
| DirectShowLib.Net 3.0.0 | DirectShow COM 互操作（Sascha-L fork） |
| OpenCvSharp4 | 图像处理（含 runtime.win） |
| HandyControl 3.5.1 | WPF 控件库（PropertyGrid、主题） |
| Microsoft.Extensions.Logging | 日志抽象 |
| xUnit | 单元测试 |

---

## 已知限制

- **仅 Windows**：DirectShow 是 Windows 专有技术
- **依赖 DirectShowLib fork**：使用 Sascha-L 的 .NET 8/9 兼容分支
- **需要手动管理相机曝光**：自动曝光可能导致帧率下降（详见 `docs/notes/auto-exposure-fps.md`）
- **MJPG 格式推荐**：高分辨率下 YUY2 可能超出 USB 带宽
- **暂无音频支持**：仅处理视频流
- **暂无多相机**：当前示例只连接一个相机（框架本身支持多图）

---

## 相关文档

| 文档 | 内容 |
| :--- | :--- |
| `docs/notes/ForkApiDifferences.md` | DirectShowLib fork 与原版的 API 差异记录 |
| `docs/notes/auto-exposure-fps.md` | 自动曝光对帧率的影响及解决方案 |
| `docs/notes/vmr9-windowless.md` | VMR-9 Windowless 模式的技术探索 |

---

## 许可

内部项目，未对外发布。使用请与作者联系。