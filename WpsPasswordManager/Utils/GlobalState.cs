using System;
using System.Threading;

namespace WpsPasswordManager.Utils
{
    public class GlobalState
    {
        private static GlobalState _instance;
        private static readonly object _lock = new object();
        
        private string _serverIp;
        private int _serverPort;
        private string _username;
        private string _token;
        private bool _isLoggedIn;
        
        // 私有构造函数
        private GlobalState() { }
        
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
                _serverIp = null;
                _serverPort = 0;
                _username = null;
                _token = null;
                _isLoggedIn = false;
            }
        }
    }
}