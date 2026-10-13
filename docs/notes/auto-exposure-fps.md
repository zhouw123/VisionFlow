# 自动曝光对帧率的影响

## 现象

相机 NETUM Camera，设置为 640x480 MJPG。枚举支持的格式：

```
640x480  MJPG   15.0 fps   ← 只有这一个变体
```

请求 15fps 后，实际运行帧率只有约 **5fps**。

## 诊断方法

在应用层加分层帧率统计，每秒打印一次：

```
[诊断] 流线程:   5 fps   处理:   5 fps   UI:   5 fps
```

三层都是一致的 5fps，说明：
- 不是处理慢
- 不是 UI 慢
- **相机确实只产出 5 帧/秒**

## 根因

**自动曝光**（Auto Exposure）导致。

自动曝光的工作机制：
- 环境光线不足时，驱动自动延长每帧的曝光时间，让画面更亮
- 帧周期 = 曝光时间 + 读取时间 + 传输时间
- 曝光时间一长，帧周期随之延长，帧率自然下降

相机支持“15fps”这个格式，但那是**在自动曝光没有主动降速的前提下**的能力。
光线不足时，驱动会优先保证画面亮度，牺牲帧率。

## 解决方案

**关闭自动曝光，切换到手动模式**，并设置一个固定曝光值：

```csharp
var camControl = camera.CameraControl;
var range = camControl.GetRange(CameraControlProperty.Exposure);
var current = camControl.Get(CameraControlProperty.Exposure);

int targetValue = current?.Value ?? range.Value.Default;

camControl.Set(
    CameraControlProperty.Exposure,
    targetValue,
    CameraControlFlags.Manual);
```

同时把**焦点**也切换到手动（自动对焦同样会导致帧率波动）：

```csharp
camControl.Set(
    CameraControlProperty.Focus,
    focusValue,
    CameraControlFlags.Manual);
```

切换后，帧率立即稳定在 15fps：

```
[诊断] 流线程:  15 fps   处理:  15 fps   UI:  15 fps
[诊断] 流线程:  15 fps   处理:  15 fps   UI:  15 fps
[诊断] 流线程:  15 fps   处理:  15 fps   UI:  15 fps
```

## 手动曝光的代价

曝光固定后，**环境光线变化会影响画面亮度**：
- 光线变暗 → 画面变暗
- 光线变亮 → 画面过曝

如果应用场景需要“亮度自适应但帧率稳定”，需要实现闭环控制：

```
每秒检查画面平均亮度
    ↓
如果偏暗 → 增加曝光值（加 1 档）
如果偏亮 → 减少曝光值（减 1 档）
    ↓
小步调整，避免频繁变动
```

这种自适应控制需要放在应用层，用 `CameraControl.Set` 周期性调用。

## 代码位置

完整实现在 `samples/DirectShowWrapper.Samples/Program.cs` 的“第二步：关闭自动曝光”部分。

## 相关接口

| 接口 | 用途 |
| :--- | :--- |
| `IAMCameraControl` | 曝光、焦点、变焦、光圈的控制 |
| `CameraControlProperty.Exposure` | 曝光属性 |
| `CameraControlFlags.Auto` | 自动模式标志 |
| `CameraControlFlags.Manual` | 手动模式标志 |

## 相关资源

- IAMCameraControl (Microsoft Docs): https://learn.microsoft.com/windows/win32/api/strmif/nn-strmif-iamcameracontrol
- UVC 相机扩展: https://www.usb.org/document-library/video-class-v15-document-set