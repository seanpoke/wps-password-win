using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PasswordManager.Utils;
using PasswordManager.Services.Routing;

namespace PasswordManager.Services.Request
{
    /// <summary>
    /// HTTP请求服务
    /// </summary>
    public class HttpRequestService
    {
        private static readonly HttpClient _httpClient;
        
        static HttpRequestService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }
        
        /// <summary>
        /// 执行GET请求
        /// </summary>
        /// <typeparam name="T">响应数据类型</typeparam>
        /// <param name="endpoint">接口路径</param>
        /// <param name="token">访问令牌</param>
        /// <param name="queryParams">查询参数</param>
        /// <returns>响应结果</returns>
        public async Task<ApiResponse<T>> GetAsync<T>(string endpoint, string token = null, object queryParams = null)
        {
            try
            {
                string serverAddress = GlobalState.Instance.GetServerAddress();
                string url = $"{serverAddress}{endpoint}";
                
                // 添加查询参数
                if (queryParams != null)
                {
                    var queryString = new StringBuilder();
                    var properties = queryParams.GetType().GetProperties();
                    
                    foreach (var property in properties)
                    {
                        var value = property.GetValue(queryParams);
                        if (value != null)
                        {
                            if (queryString.Length == 0)
                            {
                                queryString.Append("?");
                            }
                            else
                            {
                                queryString.Append("&");
                            }
                            queryString.Append($"{property.Name}={Uri.EscapeDataString(value.ToString())}");
                        }
                    }
                    
                    url += queryString.ToString();
                }
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                
                // 添加请求头
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Add("token", token);
                }
                
                Logger.Info($"发送GET请求: {url}");
                var response = await _httpClient.SendAsync(request);
                
                string responseContent = await response.Content.ReadAsStringAsync();
                Logger.Debug($"GET请求响应: {responseContent}");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<ApiResponse<T>>(responseContent);
                    return result;
                }
                else
                {
                    Logger.Error($"GET请求失败: {response.StatusCode}, {responseContent}");
                    throw new HttpRequestException($"请求失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"GET请求异常: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 执行POST请求
        /// </summary>
        /// <typeparam name="T">响应数据类型</typeparam>
        /// <param name="endpoint">接口路径</param>
        /// <param name="data">请求数据</param>
        /// <param name="token">访问令牌</param>
        /// <returns>响应结果</returns>
        public async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object data, string token = null)
        {
            try
            {
                string serverAddress = GlobalState.Instance.GetServerAddress();
                string url = $"{serverAddress}{endpoint}";
                
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                
                // 添加请求头
                request.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
                
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Add("token", token);
                }
                
                Logger.Info($"发送POST请求: {url}");
                var response = await _httpClient.SendAsync(request);
                
                string responseContent = await response.Content.ReadAsStringAsync();
                Logger.Debug($"POST请求响应: {responseContent}");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<ApiResponse<T>>(responseContent);
                    return result;
                }
                else
                {
                    Logger.Error($"POST请求失败: {response.StatusCode}, {responseContent}");
                    throw new HttpRequestException($"请求失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"POST请求异常: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 执行PUT请求
        /// </summary>
        /// <typeparam name="T">响应数据类型</typeparam>
        /// <param name="endpoint">接口路径</param>
        /// <param name="data">请求数据</param>
        /// <param name="token">访问令牌</param>
        /// <returns>响应结果</returns>
        public async Task<ApiResponse<T>> PutAsync<T>(string endpoint, object data, string token = null)
        {
            try
            {
                string serverAddress = GlobalState.Instance.GetServerAddress();
                string url = $"{serverAddress}{endpoint}";
                
                var request = new HttpRequestMessage(HttpMethod.Put, url);
                
                // 添加请求头
                request.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
                
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Add("token", token);
                }
                
                Logger.Info($"发送PUT请求: {url}");
                var response = await _httpClient.SendAsync(request);
                
                string responseContent = await response.Content.ReadAsStringAsync();
                Logger.Debug($"PUT请求响应: {responseContent}");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<ApiResponse<T>>(responseContent);
                    return result;
                }
                else
                {
                    Logger.Error($"PUT请求失败: {response.StatusCode}, {responseContent}");
                    throw new HttpRequestException($"请求失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"PUT请求异常: {ex.Message}");
                throw;
            }
        }
    }
}
