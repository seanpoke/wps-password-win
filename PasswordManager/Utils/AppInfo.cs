using System;

namespace PasswordManager.Utils
{
    /// <summary>
    /// 客户端自身信息（与密钥版本 keyVersion 无关）。
    /// Version 用于 /config/version/check 上报，每次发版手动递增，并同步 csproj 的 &lt;Version&gt;。
    /// </summary>
    public static class AppInfo
    {
        public const string Version = "0.0.1";   // 语义化 x.y.z，与文档 current 对应（临时测试 FORCE 弹窗，验证后改回 1.0.0）
        public const string Platform = "win";
    }
}
