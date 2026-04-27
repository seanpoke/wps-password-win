namespace WpsPasswordManager.Services.Request
{
    /// <summary>
    /// 请求服务工厂
    /// </summary>
    public class RequestFactory
    {
        private static HttpRequestService _httpRequestService;
        private static readonly object _lockObj = new object();
        
        private RequestFactory() { }
        
        /// <summary>
        /// 获取HTTP请求服务实例
        /// </summary>
        /// <returns>HTTP请求服务实例</returns>
        public static HttpRequestService GetHttpRequestService()
        {
            if (_httpRequestService == null)
            {
                lock (_lockObj)
                {
                    if (_httpRequestService == null)
                    {
                        _httpRequestService = new HttpRequestService();
                    }
                }
            }
            
            return _httpRequestService;
        }
    }
}
