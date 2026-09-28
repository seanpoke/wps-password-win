using System;

namespace PasswordManager.Utils
{
    /// <summary>
    /// 服务器地址解析工具（仅保留 ExtractHost 链路；协议/端口解析随功能移除已删除）。
    /// </summary>
    public class UrlParser
    {
        public static Uri ParseUserAddress(string userInput)
        {
            string address = userInput.Trim();

            if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                address = "https://" + address;
            }

            if (Uri.TryCreate(address, UriKind.Absolute, out Uri validatedUri))
            {
                return validatedUri;
            }

            throw new ArgumentException("服务器地址格式输入有误，请重新输入。");
        }

        /// <summary>
        /// 解析用户输入的域名并确定请求协议。
        /// 无协议 → 自动补 https://；显式协议 → 仅允许 https（allowHttp=true 时额外允许 http，开发者模式）；
        /// 其他协议 → 报错。
        /// </summary>
        public static bool TryResolveDomain(string userInput, bool allowHttp, out string host, out string protocol, out string error)
        {
            host = "";
            protocol = "https";
            error = null;

            string address = (userInput ?? "").Trim();
            if (address.Length == 0)
            {
                error = "请输入域名。";
                return false;
            }

            int schemeIdx = address.IndexOf("://", StringComparison.Ordinal);
            if (schemeIdx > 0)
            {
                string scheme = address.Substring(0, schemeIdx).ToLowerInvariant();
                if (scheme == "https")
                {
                    protocol = "https";
                }
                else if (scheme == "http")
                {
                    if (!allowHttp)
                    {
                        error = "仅支持 https:// 协议。";
                        return false;
                    }
                    protocol = "http";
                }
                else
                {
                    error = allowHttp ? "仅支持 http:// 或 https:// 协议。" : "仅支持 https:// 协议。";
                    return false;
                }
                address = address.Substring(schemeIdx + 3);
            }

            // 端口由独立输入框提供：去掉路径与末尾端口，仅保留主机
            int slash = address.IndexOf('/');
            if (slash >= 0) address = address.Substring(0, slash);
            int colon = address.LastIndexOf(':');
            if (colon > 0) address = address.Substring(0, colon);

            host = address.Trim();
            if (host.Length == 0 || Uri.CheckHostName(host) == UriHostNameType.Unknown)
            {
                error = "服务器地址格式输入有误，请重新输入。";
                return false;
            }
            return true;
        }

        public static string ExtractHost(string userInput)
        {
            try
            {
                Uri uri = ParseUserAddress(userInput);
                return uri.Host;
            }
            catch
            {
                string address = userInput.Trim();

                address = address.Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                                .Replace("https://", "", StringComparison.OrdinalIgnoreCase);

                int colonIndex = address.IndexOf(':');
                if (colonIndex > 0)
                {
                    return address.Substring(0, colonIndex);
                }

                int slashIndex = address.IndexOf('/');
                if (slashIndex > 0)
                {
                    return address.Substring(0, slashIndex);
                }

                return address;
            }
        }
    }
}
