namespace WpsPasswordManager.Services.Routing
{
    /// <summary>
    /// API路由常量定义
    /// </summary>
    public static class ApiRoutes
    {
        /// <summary>
        /// 获取文档所属人接口
        /// </summary>
        public const string DocOwner = "/doc/owner";
        
        /// <summary>
        /// 获取文档密码接口
        /// </summary>
        public const string DocPassword = "/doc/password";
        
        /// <summary>
        /// 获取文档权限树接口
        /// </summary>
        public const string DocAuthTree = "/doc/auth/tree";
        
        /// <summary>
        /// 更新文档权限接口
        /// </summary>
        public const string DocAuthUpdate = "/doc/auth/update";
        
        /// <summary>
        /// 上报保存记录接口
        /// </summary>
        public const string DocSaveLog = "/doc/save/log";
        
        /// <summary>
        /// 用户登录接口
        /// </summary>
        public const string AccountLogin = "/account/login";
        
        /// <summary>
        /// 刷新token接口
        /// </summary>
        public const string AccountRefreshToken = "/account/refresh-token";
        
        /// <summary>
        /// 用户登出接口
        /// </summary>
        public const string AccountLogout = "/account/logout";
        
        /// <summary>
        /// 获取LDAP配置接口
        /// </summary>
        public const string ConfigLdap = "/config/ldap";
        
        /// <summary>
        /// 刷新配置接口
        /// </summary>
        public const string ConfigRefresh = "/config/refresh";
        
        /// <summary>
        /// 公钥加密接口
        /// </summary>
        public const string ConfigEncrypt = "/config/encrypt";
        
        /// <summary>
        /// 获取最新密钥信息接口
        /// </summary>
        public const string ConfigLatestKey = "/config/latest-key";
    }
}
