using System.Runtime.Versioning;

// DirectShow 是 Windows 专有技术，整个封装库仅在 Windows 上运行。
// 这个程序集级别的声明告诉平台兼容性分析器（CA1416）：
// 本程序集中的所有代码都只会在 Windows 上执行，
// 因此不需要对每个 Windows API 调用单独警告。
[assembly: SupportedOSPlatform("windows")]