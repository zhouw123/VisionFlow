using System.Runtime.Versioning;

// 测试项目调用主库的 Windows 专用 API（DirectShow 相关），
// 因此测试项目本身也仅在 Windows 上运行。
// 这个声明让平台兼容性分析器（CA1416）不再对每个调用点报警。
[assembly: SupportedOSPlatform("windows")]