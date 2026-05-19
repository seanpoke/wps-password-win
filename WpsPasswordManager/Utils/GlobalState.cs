using System;
using System.Threading;
using WpsPasswordManager.Business;
using WpsPasswordManager.Monitor;

namespace WpsPasswordManager.Utils
{
    public class GlobalState
    {
        private static GlobalState _instance;
        private static readonly object _lock = new object();
        
        private string _serverIp;
        private int _serverPort;
        private string _username;
        private string _name;
        private string _token;
        private volatile bool _isLoggedIn;
        private string _publicKey;
        private string _keyVersion;
        private string _lastFailedFileName;

        private const string DEFAULT_PUBLIC_KEY = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEuY2/Hz7c7gM0O8P/8VYjDasWhdW4jyS99+Xwyghe+CVFko7KPeamzaOsUffIHQz0VAA8RH9MV1BYyuZAJ7X05Q==";
        private const string DEFAULT_KEY_VERSION = "default";
        
        // 私有构造函数
        private GlobalState() 
        {
            // 从本地存储加载数据
            LoadFromStorage();
        }
        
        /// <summary>
        /// 从本地存储加载数据
        /// </summary>
        private void LoadFromStorage()
        {
            try
            {
                // 加载配置信息
                var (serverIp, serverPort) = StorageManager.LoadConfig();
                if (!string.IsNullOrEmpty(serverIp) && serverPort > 0)
                {
                    _serverIp = serverIp;
                    _serverPort = serverPort;
                }
                
                // 加载用户信息
                var (username, name, token) = StorageManager.LoadUserInfo();
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(token))
                {
                    _username = username;
                    _name = name;
                    _token = token;
                    _isLoggedIn = true;
                }
                else
                {
                    _isLoggedIn = false;
                }
                
                // 加载密钥信息
                var (publicKey, keyVersion) = StorageManager.LoadKeyInfo();
                if (!string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(keyVersion))
                {
                    _publicKey = publicKey;
                    _keyVersion = keyVersion;
                }
                else
                {
                    // 使用默认值
                    _publicKey = DEFAULT_PUBLIC_KEY;
                    _keyVersion = DEFAULT_KEY_VERSION;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"从本地存储加载数据失败: {ex.Message}");
                _isLoggedIn = false;
                // 使用默认密钥信息
                _publicKey = DEFAULT_PUBLIC_KEY;
                _keyVersion = DEFAULT_KEY_VERSION;
            }
        }
        
        // 单例模式获取实例
        public static GlobalState Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new GlobalState();
                        }
                    }
                }
                return _instance;
            }
        }
        
        // 服务器IP
        public string ServerIp
        {
            get
            {
                lock (_lock)
                {
                    return _serverIp;
                }
            }
            set
            {
                lock (_lock)
                {
                    _serverIp = value;
                }
            }
        }
        
        // 服务器端口
        public int ServerPort
        {
            get
            {
                lock (_lock)
                {
                    return _serverPort;
                }
            }
            set
            {
                lock (_lock)
                {
                    _serverPort = value;
                }
            }
        }
        
        // 登录状态
        public bool IsLoggedIn
        {
            get
            {
                lock (_lock)
                {
                    return _isLoggedIn;
                }
            }
            set
            {
                lock (_lock)
                {
                    _isLoggedIn = value;
                }
            }
        }
        
        // Token
        public string Token
        {
            get
            {
                lock (_lock)
                {
                    return _token;
                }
            }
            set
            {
                lock (_lock)
                {
                    _token = value;
                }
            }
        }
        
        // Username
        public string Username
        {
            get
            {
                lock (_lock)
                {
                    return _username;
                }
            }
            set
            {
                lock (_lock)
                {
                    _username = value;
                }
            }
        }
        
        // Name
        public string Name
        {
            get
            {
                lock (_lock)
                {
                    return _name;
                }
            }
            set
            {
                lock (_lock)
                {
                    _name = value;
                }
            }
        }
        
        // PublicKey
        public string PublicKey
        {
            get
            {
                lock (_lock)
                {
                    return _publicKey;
                }
            }
            set
            {
                lock (_lock)
                {
                    _publicKey = value;
                }
            }
        }
        
        // KeyVersion
        public string KeyVersion
        {
            get
            {
                lock (_lock)
                {
                    return _keyVersion;
                }
            }
            set
            {
                lock (_lock)
                {
                    _keyVersion = value;
                }
            }
        }

        // LastFailedFileName - 上次文件识别失败的文件名
        public string LastFailedFileName
        {
            get
            {
                lock (_lock)
                {
                    return _lastFailedFileName;
                }
            }
            set
            {
                lock (_lock)
                {
                    _lastFailedFileName = value;
                }
            }
        }
        
        // 构建服务器地址
        public string GetServerAddress()
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(_serverIp) || _serverPort <= 0)
                {
                    throw new InvalidOperationException("服务器IP和端口未设置");
                }
                return $"http://{_serverIp}:{_serverPort}";
            }
        }
        
        // 重置状态
        public void Reset()
        {
            lock (_lock)
            {
                _username = null;
                _name = null;
                _token = null;
                _isLoggedIn = false;
                // 保留配置信息（serverIp和serverPort）
            }
        }
        
        /// <summary>
        /// 保存用户信息到本地存储
        /// </summary>
        public void SaveUserInfo()
        {
            lock (_lock)
            {
                if (_isLoggedIn && !string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_token))
                {
                    StorageManager.SaveUserInfo(_username, _name, _token);
                }
            }
        }
        
        /// <summary>
        /// 保存配置信息到本地存储
        /// </summary>
        public void SaveConfig()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_serverIp) && _serverPort > 0)
                {
                    StorageManager.SaveConfig(_serverIp, _serverPort);
                }
            }
        }
        
        /// <summary>
        /// 保存密钥信息到本地存储
        /// </summary>
        public void SaveKeyInfo()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_publicKey) && !string.IsNullOrEmpty(_keyVersion))
                {
                    StorageManager.SaveKeyInfo(_publicKey, _keyVersion);
                }
            }
        }
        
        /// <summary>
        /// 清除本地存储中的用户信息
        /// </summary>
        public void ClearUserInfo()
        {
            StorageManager.ClearUserInfo();
        }

        /// <summary>
        /// 清除所有资源（文件监听器和文件元数据）
        /// 当用户注销、登录失败或心跳续期失败时调用
        /// </summary>
        public void ClearAllResources()
        {
            lock (_lock)
            {
                FileMonitor.StopAllWatching();
                FileMetaFactory.Instance.CleanupAllFileMeta();
                Logger.Info("所有资源已清理完成");
            }
        }
    }
}