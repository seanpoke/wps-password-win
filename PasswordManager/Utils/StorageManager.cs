using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace PasswordManager.Utils
{
    public class StorageManager
    {
        private static readonly string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PasswordManager");
        private static readonly string _configFile = Path.Combine(_appDataPath, "config.json");
        private static readonly string _userFile = Path.Combine(_appDataPath, "user.json");
        private static readonly string _keyFile = Path.Combine(_appDataPath, "keyinfo.json");
        private static readonly string _encryptionKey = "PasswordManager_EncryptionKey";

        static StorageManager()
        {
            // 确保应用数据目录存在
            if (!Directory.Exists(_appDataPath))
            {
                Directory.CreateDirectory(_appDataPath);
            }
        }

        #region 配置信息存储

        /// <summary>
        /// 保存配置信息到本地存储
        /// </summary>
        public static void SaveConfig(string serverIp, int serverPort)
        {
            try
            {
                var configData = new
                {
                    serverIp = serverIp,
                    serverPort = serverPort
                };

                string jsonContent = JsonSerializer.Serialize(configData);
                File.WriteAllText(_configFile, jsonContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error($"保存配置信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从本地存储读取配置信息
        /// </summary>
        public static (string serverIp, int serverPort) LoadConfig()
        {
            try
            {
                if (File.Exists(_configFile))
                {
                    string jsonContent = File.ReadAllText(_configFile, Encoding.UTF8);
                    var configData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                    string serverIp = configData.GetProperty("serverIp").GetString();
                    int serverPort = configData.GetProperty("serverPort").GetInt32();

                    return (serverIp, serverPort);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"读取配置信息失败: {ex.Message}");
            }

            return (null, 0);
        }

        #endregion

        #region 用户信息存储

        /// <summary>
        /// 保存用户信息到本地存储
        /// </summary>
        public static void SaveUserInfo(string username, string name, string role, string token)
        {
            try
            {
                var userData = new
                {
                    username = username,
                    name = name,
                    role = role,
                    token = Encrypt(token),
                    lastLoginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string jsonContent = JsonSerializer.Serialize(userData);
                File.WriteAllText(_userFile, jsonContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error($"保存用户信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从本地存储读取用户信息
        /// </summary>
        public static (string username, string name, string role, string token) LoadUserInfo()
        {
            try
            {
                if (File.Exists(_userFile))
                {
                    string jsonContent = File.ReadAllText(_userFile, Encoding.UTF8);
                    var userData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                    string username = userData.GetProperty("username").GetString();
                    string name = userData.GetProperty("name").GetString();
                    string role = userData.TryGetProperty("role", out var roleElement) ? roleElement.GetString() : null;
                    string token = Decrypt(userData.GetProperty("token").GetString());

                    return (username, name, role, token);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"读取用户信息失败: {ex.Message}");
            }

            return (null, null, null, null);
        }

        /// <summary>
        /// 清除本地存储中的用户信息
        /// </summary>
        public static void ClearUserInfo()
        {
            try
            {
                if (File.Exists(_userFile))
                {
                    File.Delete(_userFile);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"清除用户信息失败: {ex.Message}");
            }
        }

        #endregion

        #region 密钥信息存储

        /// <summary>
        /// 保存密钥信息到本地存储
        /// </summary>
        public static void SaveKeyInfo(string publicKey, string keyVersion)
        {
            try
            {
                var keyData = new
                {
                    publicKey = publicKey,
                    keyVersion = keyVersion,
                    lastUpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string jsonContent = JsonSerializer.Serialize(keyData);
                File.WriteAllText(_keyFile, jsonContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error($"保存密钥信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从本地存储读取密钥信息
        /// </summary>
        public static (string publicKey, string keyVersion) LoadKeyInfo()
        {
            try
            {
                if (File.Exists(_keyFile))
                {
                    string jsonContent = File.ReadAllText(_keyFile, Encoding.UTF8);
                    var keyData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                    string publicKey = keyData.GetProperty("publicKey").GetString();
                    string keyVersion = keyData.GetProperty("keyVersion").GetString();

                    return (publicKey, keyVersion);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"读取密钥信息失败: {ex.Message}");
            }

            return (null, null);
        }

        /// <summary>
        /// 清除本地存储中的密钥信息
        /// </summary>
        public static void ClearKeyInfo()
        {
            try
            {
                if (File.Exists(_keyFile))
                {
                    File.Delete(_keyFile);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"清除密钥信息失败: {ex.Message}");
            }
        }

        #endregion

        #region 加密/解密方法

        /// <summary>
        /// 简单的加密方法
        /// </summary>
        private static string Encrypt(string plainText)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(_encryptionKey.PadRight(32).Substring(0, 32));
                aesAlg.IV = new byte[16];

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        /// <summary>
        /// 简单的解密方法
        /// </summary>
        private static string Decrypt(string cipherText)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(_encryptionKey.PadRight(32).Substring(0, 32));
                aesAlg.IV = new byte[16];

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }

        #endregion
    }
}