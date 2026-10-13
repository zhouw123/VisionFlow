# DirectShowLib fork API 差异记录

## 背景

本项目使用 **DirectShowLib.NET (Sascha-L fork)**，目标框架 .NET 8/9。
NuGet: `DirectShowLib.Net` 3.0.0

这个 fork 与原始 DirectShowLib 在若干 API 上存在差异。本文记录实际遇到的
所有差异和解决方式，供后续维护参考。

## 差异列表

### 1. IPin.Connect 的第二个参数

**原版**：`int Connect(IPin pReceivePin, IntPtr pmt)`

**此 fork**：`int Connect(IPin pReceivePin, AMMediaType pmt)`

**解决**：传 `null` 表示“不指定媒体类型”。

```csharp
_pin.Connect(target._pin, null);
```

**相关文件**：`Filters/Pin.cs`

---

### 2. 缺少 Clsid 常量类

**原版**：提供 `Clsid` 静态类，包含常用 CLSID 常量。

**此 fork**：没有这个类。

**解决**：自建 `DirectShowClsIds` 静态类，集中管理常用 CLSID。

```csharp
public static class DirectShowClsIds
{
    public static readonly Guid FilterGraph =
        new Guid("E436EBB3-524F-11CE-9F53-0020AF0BA770");
    // ...
}
```

**相关文件**：`Infrastructure/DirectShowClsIds.cs`

---

### 3. IPropertyBag.Read 的第三个参数

**原版**：`int Read(string propName, out object value, IntPtr errorLog)`

**此 fork**：`int Read(string propName, out object value, IErrorLog errorLog)`

**解决**：传 `null!`。

```csharp
var hr = bag.Read(propertyName, out value, null!);
```

**相关文件**：`Devices/DeviceEnumerator.cs`

---

### 4. BitmapInfoHeader 字段差异

| 字段 | 原版类型 | 此 fork 类型 | 备注 |
| :--- | :--- | :--- | :--- |
| `Planes` | `ushort` | `short` | 需要 `(short)` 转换 |
| `BitCount` | `ushort` | `short` | 需要 `(short)` 转换 |
| `Compression` | `uint` | `int` | 字面量不加 `u` 后缀 |
| `SizeImage` | `uint` | **不存在** | 移除该字段赋值 |

**相关文件**：`Infrastructure/MediaTypeHelper.cs`、`Filters/ImageRendererFilter.cs`

---

### 5. VideoInfoHeader 内存布局与原生不一致

**现象**：从 `AMMediaType.formatPtr` 解析宽度时，读到 `0x4`、`0x16` 等假值。

**原因**：DirectShow 实际返回的是 `VIDEOINFOHEADER2`，其 `bmiHeader` 起始偏移
是 72 字节，而 `VIDEOINFOHEADER` 是 48 字节。fork 的托管结构定义可能与
原生不一致。

**解决**：自建 `VideoInfoParser`，**直接按内存偏移量读取**，不依赖托管结构。

```csharp
// VIDEOINFOHEADER:  bmiHeader 在偏移 48
// VIDEOINFOHEADER2: bmiHeader 在偏移 72
int bmiOffset = (mediaType.formatType == FormatTypeVideoInfo2) ? 72 : 48;

int width = Marshal.ReadInt32(bmiPtr, 4);
int height = Marshal.ReadInt32(bmiPtr, 8);
long avgTimePerFrame = Marshal.ReadInt64(formatPtr, 40);
```

**相关文件**：`Infrastructure/VideoInfoParser.cs`

---

### 6. GetRange 的最后一个参数

**原版**：`int GetRange(..., out int caps)`

**此 fork**：`int GetRange(..., out CameraControlFlags caps)`（或 `VideoProcAmpFlags`）

**解决**：用 `Convert.ToInt32(caps)`。

```csharp
return new PropertyRange(min, max, step, def, Convert.ToInt32(caps));
```

**相关文件**：`Devices/CameraControl.cs`、`Devices/VideoProcAmp.cs`

---

### 7. VMR9AspectRatioMode 枚举成员名不匹配

**现象**：`VMR9AspectRatioMode.Letterbox` 编译错误。

**原因**：Windows SDK 里是 `VMR9ARMode_LetterBox`（大写 B），fork 里可能
完全没定义或命名不同。

**解决**：用整数值 `(VMR9AspectRatioMode)1`。

```csharp
control.SetAspectRatioMode((VMR9AspectRatioMode)1); // VMR9ARMode_LetterBox
```

**相关文件**：`Filters/VideoRenderer.cs`（Windowless 版，现已停用）

---

### 8. 若干接口参数的 [NotNull] 注解比实际更严格

**现象**：传 `null` 给某些 COM 接口方法时，编译报 `CS8625`。

**解决**：用 `null!` 显式标记“这是有意的 null”。

```csharp
_moniker.BindToObject(null!, null!, ref iid, out var obj);
moniker.BindToStorage(null!, null!, ref iid, out bagObj);
```

**相关文件**：`Devices/DeviceInfo.cs`、`Devices/DeviceEnumerator.cs`

---

### 9. DsRect 构造签名

**现象**：`new DsRect(left, top, right, bottom)` 可能参数类型不同。

**解决**：按 fork 的实际签名调整。通常是 `(int left, int top, int right, int bottom)`。

---

## 应对策略

遇到 fork 差异时，按这个顺序处理：

1. **先看编译错误**：错误信息里会指出实际类型和方法签名
2. **看 fork 源码或 XML 文档**：了解实际的 API 定义
3. **优先用整数值绕过命名差异**：如 `(VMR9AspectRatioMode)1`
4. **必要时自建工具类**：如 `VideoInfoParser` 绕过结构布局问题
5. **记录到本文件**：每解决一个差异，就加一条

## 升级 fork 时的检查清单

如果将来升级 DirectShowLib fork，需要重新验证：

- [ ] `IPin.Connect` 的第二个参数类型
- [ ] `BitmapInfoHeader` 的字段类型（`Planes`、`BitCount`、`Compression`）
- [ ] `BitmapInfoHeader.SizeImage` 是否存在
- [ ] `GetRange` 最后一个参数类型
- [ ] `VideoInfoHeader` 的内存布局是否与原生一致
- [ ] `Clsid` 常量类是否存在
- [ ] `VMR9AspectRatioMode` 等枚举的成员名