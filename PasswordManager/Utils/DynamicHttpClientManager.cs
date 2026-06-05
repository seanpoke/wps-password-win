using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace PasswordManager.Utils
{
    public class DynamicHttpClientManager
    {
        private static readonly object _lockObj = new object();
        private static HttpClient _httpClient;

        public static HttpClient GetClientForUserUrl(Uri targetUri)
        {
            string userHost = targetUri.Host;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (request, certificate, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None)
                    {
                        return true;
                    }

                    return true;
                }
            };

            HttpClient client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(10);

            return client;
        }

        public static HttpClient GetSharedClient()
        {
            if (_httpClient == null)
            {
                lock (_lockObj)
                {
                    if (_httpClient == null)
                    {
                        var handler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (request, certificate, chain, sslPolicyErrors) =>
                            {
                                if (sslPolicyErrors == SslPolicyErrors.None)
                                {
                                    return true;
                                }

                                return true;
                            }
                        };

                        _httpClient = new HttpClient(handler);
                        _httpClient.Timeout = TimeSpan.FromSeconds(30);
                    }
                }
            }

            return _httpClient;
        }

        public static void ResetClient()
        {
            lock (_lockObj)
            {
                _httpClient?.Dispose();
                _httpClient = null;
            }
        }

        public static HttpClient CreateClientWithTimeout(TimeSpan timeout)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (request, certificate, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None)
                    {
                        return true;
                    }

                    return true;
                }
            };

            HttpClient client = new HttpClient(handler);
            client.Timeout = timeout;

            return client;
        }
    }
}