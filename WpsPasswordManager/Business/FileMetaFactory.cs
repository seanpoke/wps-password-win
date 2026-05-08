using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Business
{
    public class FileMetaFactory
    {
        private static FileMetaFactory instance;
        private static readonly object lockObject = new object();
        private ConcurrentDictionary<string, FileMeta> fileMetaMap;

        private long pluginOperationTimestamp;
        private const long PLUGIN_OPERATION_TIMEOUT = 1000L;

        private FileMetaFactory()
        {
            fileMetaMap = new ConcurrentDictionary<string, FileMeta>();
        }

        public static FileMetaFactory Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        if (instance == null)
                        {
                            instance = new FileMetaFactory();
                        }
                    }
                }
                return instance;
            }
        }

        public FileMeta GetFileMeta(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            fileMetaMap.TryGetValue(filePath, out FileMeta fileMeta);
            return fileMeta;
        }

        public void UpdatePendingPassword(string filePath, string password)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(password))
            {
                Logger.Warning($"UpdatePendingPassword 参数无效: filePath={filePath}, password={password}");
                return;
            }

            var fileMeta = GetFileMeta(filePath);
            if (fileMeta == null)
            {
                Logger.Warning($"未找到文件 {filePath} 的元数据，无法更新待定密码");
                return;
            }

            const int maxPasswordCount = 5;

            lock (fileMeta)
            {
                bool added = fileMeta.PendingPasswordList.Add(password);
                if (added)
                {
                    Logger.Info($"成功添加文件 {filePath} 的待定密码: {password}");

                    // 如果超过最大数量，移除多余的元素（保留字典序最大的5个）
                    while (fileMeta.PendingPasswordList.Count > maxPasswordCount)
                    {
                        // 获取字典序最小的元素并移除
                        string removedPassword = fileMeta.PendingPasswordList.Min;
                        fileMeta.PendingPasswordList.Remove(removedPassword);
                        Logger.Info($"文件 {filePath} 的待定密码数量超过{maxPasswordCount}个，已移除: {removedPassword}");
                    }
                }
                else
                {
                    Logger.Info($"文件 {filePath} 的待定密码已存在: {password}");
                }

                // 打印 fileMeta 的 JSON 内容
                string fileMetaJson = JsonSerializer.Serialize(fileMeta, new JsonSerializerOptions { WriteIndented = true });
                Logger.Info($"FileMeta 内容:\n{fileMetaJson}");
            }
        }

        public string GetWritePassword(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Logger.Warning($"GetWritePassword: 文件路径为空");
                return null;
            }

            var fileMeta = GetFileMeta(filePath);
            if (fileMeta == null)
            {
                Logger.Warning($"GetWritePassword: 未找到文件 {filePath} 的元数据");
                return null;
            }

            var pendingPasswords = fileMeta.PendingPasswordList;
            var currentPassword = fileMeta.CurrentPassword;

            if (pendingPasswords == null || pendingPasswords.Count == 0)
            {
                Logger.Info($"[时间戳: {DateTimeOffset.Now.ToUnixTimeMilliseconds()}] GetWritePassword: 无待定密码，使用当前密码执行写入操作");
                return currentPassword;
            }

            if (!filePath.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    bool isEncrypted = OfficeEncryptUtils.IsFileEncrypted(filePath);
                    
                    if (isEncrypted)
                    {
                        Logger.Info($"[时间戳: {DateTimeOffset.Now.ToUnixTimeMilliseconds()}] GetWritePassword: 文件已加密，开始轮询验证密码");
                    }
                    else
                    {
                        Logger.Info($"[时间戳: {DateTimeOffset.Now.ToUnixTimeMilliseconds()}] GetWritePassword: 文档未检测到加密状态或检测失败，但有待定密码，尝试验证");
                    }

                    foreach (var pendingPassword in pendingPasswords)
                    {
                        Logger.Debug($"[时间戳: {DateTimeOffset.Now.ToUnixTimeMilliseconds()}] GetWritePassword: 验证待定密码是否能打开文件: '{pendingPassword}'");

                        if (OfficeEncryptUtils.VerifyPassword(filePath, pendingPassword))
                        {
                            Logger.Info($"[时间戳: {DateTimeOffset.Now.ToUnixTimeMilliseconds()}] GetWritePassword: 待定密码验证成功，使用该密码");
                            return pendingPassword;
                        }
                        else
                        {
                            Logger.Warning($"[时间戳: {DateTimeOffset.Now.ToUnixTimeMilliseconds()}] GetWritePassword: 待定密码验证失败: '{pendingPassword}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[时间戳: {DateTimeOffset.Now.ToUnixTimeMilliseconds()}] GetWritePassword: 验证密码时发生异常: {ex.Message}");
                }
            }
            else
            {
                Logger.Info($"[时间戳: {DateTimeOffset.Now.ToUnixTimeMilliseconds()}] GetWritePassword: Content URI，使用第一个待定密码");
                return pendingPasswords.FirstOrDefault();
            }

            Logger.Error($"[时间戳: {DateTimeOffset.Now.ToUnixTimeMilliseconds()}] GetWritePassword: 所有待定密码验证失败，使用当前密码");
            return currentPassword;
        }

        public void UpdateCurrentPassword(string filePath, string password)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var fileMeta = GetFileMeta(filePath);
            fileMeta.CurrentPassword = password;
            fileMeta.ClearPendingPasswords();
            Logger.Info($"更新文件 {filePath} 的当前密码: {password}");
        }

        public void CleanupFileMeta(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && fileMetaMap.TryRemove(filePath, out _))
            {
                Logger.Info($"清理文件 {filePath} 的元数据");
            }
        }

        public void CleanupAllFileMeta()
        {
            fileMetaMap.Clear();
            Logger.Info("清理所有文件元数据");
        }

        public bool HasFileMeta(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && fileMetaMap.ContainsKey(filePath);
        }

        public int GetFileMetaCount()
        {
            return fileMetaMap.Count;
        }

        public void AddFileMeta(FileMeta fileMeta)
        {
            if (fileMeta != null && !string.IsNullOrEmpty(fileMeta.FilePath))
            {
                fileMetaMap.TryAdd(fileMeta.FilePath, fileMeta);
                Logger.Info($"添加文件元数据: {fileMeta.FilePath}");
            }
        }

        /// <summary>
        /// 创建唯一标识符uid
        /// 按照uid生成规则.md的要求：时间戳_guid
        /// </summary>
        public string CreateUid()
        {
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            string guid = Guid.NewGuid().ToString();
            string uid = $"{timestamp}_{guid}";
            Logger.Info($"创建新的uid: '{uid}'");
            return uid;
        }

        /// <summary>
        /// 设置插件操作标志
        /// 在执行插件写操作前调用，用于标记即将执行的操作为插件操作
        /// </summary>
        public void SetPluginOperation(bool operating)
        {
            if (operating)
            {
                pluginOperationTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                Logger.Info($"[时间戳: {pluginOperationTimestamp}] SetPluginOperation: 设置插件操作标志");
            }
        }

        /// <summary>
        /// 检查是否为插件操作
        /// 通过时间戳判断当前时间距离上次设置插件操作标志是否在超时时间内
        /// </summary>
        /// <returns>true表示当前事件是由插件操作引起的，应跳过处理</returns>
        public bool IsPluginOperation()
        {
            long timestamp = pluginOperationTimestamp;
            long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            bool isPluginOp = currentTime - timestamp <= PLUGIN_OPERATION_TIMEOUT;
            if (isPluginOp)
            {
                Logger.Info($"[时间戳: {currentTime}] IsPluginOperation: 检测到插件操作，时间戳差: {currentTime - timestamp}ms");
            }
            return isPluginOp;
        }
    }
}