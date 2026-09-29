using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace PasswordManager.Utils
{
    /// <summary>
    /// 客户端自身信息（与密钥版本 keyVersion 无关）。
    /// 版本号单一来源为 PasswordManager.csproj 的 &lt;Version&gt;：MSBuild 构建时自动写入
    /// 程序集文件版本（FileVersion = x.y.z.0），运行时读取并取前三段作为语义化版本。
    /// 发版只需改 csproj 的 &lt;Version&gt;，无需再同步其它文件。
    /// </summary>
    public static class AppInfo
    {
        public static string Platform => "win";

        /// <summary>语义化版本（x.y.z），来源：csproj &lt;Version&gt; → FileVersion。</summary>
        public static string Version { get; } = GetVersion();

        private static string GetVersion()
        {
            try
            {
                // Environment.ProcessPath：.NET 6 起可用，单文件/普通发布均能取到 exe 路径
                string? path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path))
                    path = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(path))
                    return "0.0.0";

                var vi = FileVersionInfo.GetVersionInfo(path);
                var parts = (vi.FileVersion ?? "").Split('.');
                if (parts.Length >= 3)
                    return string.Join(".", parts.Take(3));   // 1.0.3.0 → "1.0.3"
            }
            catch
            {
                // 取不到版本时兜底，不影响启动
            }
            return "0.0.0";
        }
    }
}
