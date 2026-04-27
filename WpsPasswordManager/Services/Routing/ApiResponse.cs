namespace WpsPasswordManager.Services.Routing
{
    /// <summary>
    /// API统一响应结构
    /// </summary>
    public class ApiResponse<T>
    {
        /// <summary>
        /// 响应消息
        /// </summary>
        public string message { get; set; }
        
        /// <summary>
        /// 响应状态码
        /// </summary>
        public int status { get; set; }
        
        /// <summary>
        /// 响应数据
        /// </summary>
        public T data { get; set; }
    }
    
    /// <summary>
    /// 文档所有者信息
    /// </summary>
    public class DocOwnerInfo
    {
        /// <summary>
        /// 文档所属账号
        /// </summary>
        public string ownerAccount { get; set; }
        
        /// <summary>
        /// 文档所属名称
        /// </summary>
        public string ownerName { get; set; }
        
        /// <summary>
        /// 当前用户是否有读权限
        /// </summary>
        public bool readAuth { get; set; }
        
        /// <summary>
        /// 当前用户是否有写权限
        /// </summary>
        public bool writeAuth { get; set; }
    }
    
    /// <summary>
    /// 文档密码信息
    /// </summary>
    public class DocPasswordInfo
    {
        /// <summary>
        /// 解密后的文档密码明文
        /// </summary>
        public string password { get; set; }
    }
    
    /// <summary>
    /// 登录响应信息
    /// </summary>
    public class LoginInfo
    {
        /// <summary>
        /// 访问令牌
        /// </summary>
        public string token { get; set; }
        
        /// <summary>
        /// 用户账号
        /// </summary>
        public string account { get; set; }
        
        /// <summary>
        /// 用户姓名
        /// </summary>
        public string name { get; set; }
    }
    
    /// <summary>
    /// LDAP配置信息
    /// </summary>
    public class LdapConfig
    {
        /// <summary>
        /// LDAP服务器地址
        /// </summary>
        public string url { get; set; }
        
        /// <summary>
        /// LDAP基础DN
        /// </summary>
        public string @base { get; set; }
        
        /// <summary>
        /// LDAP管理员账号
        /// </summary>
        public string username { get; set; }
        
        /// <summary>
        /// LDAP组织树根节点列表
        /// </summary>
        public string[] trees { get; set; }
    }
    
    /// <summary>
    /// 加密响应信息
    /// </summary>
    public class EncryptInfo
    {
        /// <summary>
        /// 原始明文
        /// </summary>
        public string original { get; set; }
        
        /// <summary>
        /// ECC加密后的密文
        /// </summary>
        public string encrypted { get; set; }
    }
}
