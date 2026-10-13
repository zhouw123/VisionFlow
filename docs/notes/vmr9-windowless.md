# VMR-9 Windowless 模式探索记录

## 背景

为了在视频上叠加 UI 元素（如 ROI 选择框），尝试把 `VideoRenderer` 从
VMR-9 Windowed 模式改为 Windowless 模式。Windowless 模式下 VMR-9 不
创建独立子窗口，直接把视频画到父窗口句柄上，理论上可以在视频之上
绘制其他 UI。

## 尝试与结果

**1. Windowless 模式配置成功**

`IVMRFilterConfig9.SetRenderingMode(VMR9Mode.Windowless)` 成功，
`SetNumberOfStreams(1)` 成功。

**2. 相机 YUY2 → VMR-9 Windowless 连接失败**

- 显式连接 (`ConnectDirect`)：`0x80040207` (VFW_E_NO_ACCEPTABLE_TYPES)
- 智能连接 (`ConnectAuto`)：`0x80040217` (VFW_E_CANNOT_CONNECT)

**3. 失败原因分析**

Windowless 模式下 VMR-9 要求输入为 RGB 格式（因为要把视频直接画到
GDI 上）。而系统里**没有标准的 YUY2→RGB DirectShow 转换器**：

- AVI Decompressor：只做 MJPG→RGB，不处理 YUY2
- Color Space Converter：输入只接受 RGB，不处理 YUV
- 其他系统转换器：都不提供 YUY2→RGB

而 YUY2→RGB 的转换恰恰是 OpenCV `CvtColor` 最擅长的。

## 结论

在**没有专门的 YUY2→RGB 转换器**的情况下，VMR-9 Windowless 模式
对 YUY2 输入不可用。

## 可选路径

**路径 A：MJPG + AVI Decompressor**

相机设置为 MJPG 格式，智能连接会自动插入 AVI Decompressor 完成
MJPG→RGB 转换，然后 VMR-9 Windowless 可以工作。缺点是 MJPG 的帧率
受相机 MJPG 能力限制，且多了一次解码开销。

**路径 B（推荐）：ImageRendererFilter**

放弃 VMR-9 的窗口模式，改用 `SampleGrabber + NullRenderer` 组合：
- SampleGrabber 抓帧（YUY2），通过事件送到 UI
- NullRenderer 作为图终点
- UI 层（WPF 的 Image 控件、WinForms 的 PictureBox）负责显示
- 帧数据可以自由地用 OpenCV 转换、处理、叠加 UI

这条路完全绕开 VMR-9 的格式限制，也是 WPF + ROI 场景的正确终点。

## 相关代码

Windowless 版本的实现已备份在：
`docs/snippets/VideoRenderer.Windowless.cs.txt`

如果未来需要重新启用，可以把这段代码恢复。