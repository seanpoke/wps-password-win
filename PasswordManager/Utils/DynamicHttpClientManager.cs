using System;
using System.Net.Http;

namespace PasswordManager.Utils
{
    /// <summary>
    /// HttpClient 共享管理。
    /// 注意：证书回调无条件返回 true（跳过证书校验），为内网自签名证书场景的有意行为。
    /// </summary>
    public class DynamicHttpClientManager
    {
        private static readonly object _lockObj = new object();
        private static HttpClient _httpClient;

        private static HttpClientHandler CreateHandler()
        {
            // 无条件接受证书：内网自签名场景的有意行为（若需恢复校验，删除该回调即可）
            return new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
        }

        public static HttpClient GetSharedClient()
        {
            if (_httpClient == null)
            {
                lock (_lockObj)
                {
                    if (_httpClient == null)
                    {
                        _httpClient = new HttpClient(CreateHandler());
                        _httpClient.Timeout = TimeSpan.FromSeconds(30);
                    }
                }
            }

            return _httpClient;
        }

        public static HttpClient CreateClientWithTimeout(TimeSpan timeout)
        {
            HttpClient client = new HttpClient(CreateHandler());
            client.Timeout = timeout;

            return client;
        }
    }
}
