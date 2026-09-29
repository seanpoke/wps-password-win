using System;
using System.Threading.Tasks;
using PasswordManager.Services.Routing;
using PasswordManager.Services.Request;
using PasswordManager.Utils;

namespace PasswordManager.Services
{
    /// <summary>
    /// 客户端版本检查：调用 /config/version/check（免 token，依赖已配置服务器）。
    /// 无需更新 / 服务器未配置 / 出错时返回 null。
    /// </summary>
    public static class VersionCheckService
    {
        public static async Task<VersionCheckInfo> CheckAsync()
        {
            try
            {
                var svc = RequestFactory.GetHttpRequestService();
                var resp = await svc.GetAsync<VersionCheckInfo>(
                    ApiRoutes.ConfigVersionCheck,
                    queryParams: new { platform = AppInfo.Platform, current = AppInfo.Version });
                if (resp != null && resp.status == 200 && resp.data != null)
                    return resp.data;
                return null;
            }
            catch (InvalidOperationException)
            {
                Logger.Info("版本检查跳过：服务器未配置");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"版本检查失败: {ex.Message}");
                return null;
            }
        }
    }
}
