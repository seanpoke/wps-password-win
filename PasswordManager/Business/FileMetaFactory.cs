using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using PasswordManager.Utils;

namespace PasswordManager.Business
{
    public class FileMetaFactory
    {
        private static FileMetaFactory instance;
        private static readonly object lockObject = new object();
        private ConcurrentDictionary<string, FileMeta> fileMetaMap;
        
        // 用于等待元数据初始化完成的事件字典
        private ConcurrentDictionary<string, AutoResetEvent> initWaitEvents;

        private long pluginOperationTimestamp;
        private const long PLUGIN_OPERATION_TIMEOUT = 1000L;
        
        // 等待初始化的超时时间（5秒）
        private const int INIT_WAIT_TIMEOUT = 5000;

        private FileMetaFactory()
        {
            fileMetaMap = new ConcurrentDictionary<string, FileMeta>();
            initWaitEvents = new ConcurrentDictionary<string, AutoResetEvent>();
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

            string normalizedPath = filePath.ToLowerInvariant();
            fileMetaMap.TryGetValue(normalizedPath, out FileMeta fileMeta);
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
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }
            string normalizedPath = filePath.ToLowerInvariant();
            if (fileMetaMap.TryRemove(normalizedPath, out _))
            {
                Logger.Info($"清理文件 {normalizedPath} 的元数据");
                AutoFillAttemptManager.Instance.ResetAttempt(normalizedPath);
                RemoveInitWaitEvent(normalizedPath);
            }
        }

        public void CleanupAllFileMeta()
        {
            fileMetaMap.Clear();
            Logger.Info("清理所有文件元数据");
            AutoFillAttemptManager.Instance.RemoveAllRecords();
            // 清理所有等待事件
            foreach (var kvp in initWaitEvents)
            {
                kvp.Value.Dispose();
            }
            initWaitEvents.Clear();
        }

        public List<string> GetAllFilePaths()
        {
            return fileMetaMap.Keys.ToList();
        }

        /// <summary>
        /// 等待文件元数据初始化完成
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>true表示初始化完成，false表示超时</returns>
        public bool WaitForInit(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Logger.Warning("WaitForInit: 文件路径为空");
                return false;
            }

            // 如果已经有元数据，直接返回
            if (HasFileMeta(filePath))
            {
                Logger.Info($"WaitForInit: 文件 {filePath} 元数据已存在，无需等待");
                return true;
            }

            Logger.Info($"WaitForInit: 等待文件 {filePath} 的元数据初始化...");
            
            // 获取或创建等待事件
            AutoResetEvent waitEvent = initWaitEvents.GetOrAdd(filePath, _ => new AutoResetEvent(false));
            
            try
            {
                // 等待初始化完成（最多等待5秒）
                bool initialized = waitEvent.WaitOne(INIT_WAIT_TIMEOUT);
                
                if (initialized)
                {
                    Logger.Info($"WaitForInit: 文件 {filePath} 元数据初始化完成");
                }
                else
                {
                    Logger.Warning($"WaitForInit: 文件 {filePath} 元数据初始化等待超时 ({INIT_WAIT_TIMEOUT}ms)");
                }
                
                return initialized;
            }
            finally
            {
                // 无论成功与否，清理等待事件（避免内存泄漏）
                RemoveInitWaitEvent(filePath);
            }
        }

        /// <summary>
        /// 发出元数据初始化完成的信号
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void SignalInitComplete(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            if (initWaitEvents.TryGetValue(filePath, out AutoResetEvent waitEvent))
            {
                Logger.Info($"SignalInitComplete: 通知文件 {filePath} 的元数据初始化完成");
                waitEvent.Set();
            }
            else
            {
                Logger.Debug($"SignalInitComplete: 未找到文件 {filePath} 的等待事件，可能没有等待者");
            }
        }

        /// <summary>
        /// 移除等待事件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        private void RemoveInitWaitEvent(string filePath)
        {
            if (initWaitEvents.TryRemove(filePath, out AutoResetEvent waitEvent))
            {
                waitEvent.Dispose();
                Logger.Debug($"RemoveInitWaitEvent: 清理文件 {filePath} 的等待事件");
            }
        }

        public bool HasFileMeta(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }
            string normalizedPath = filePath.ToLowerInvariant();
            return fileMetaMap.ContainsKey(normalizedPath);
        }

        public int GetFileMetaCount()
        {
            return fileMetaMap.Count;
        }

        public void AddFileMeta(FileMeta fileMeta)
        {
            if (fileMeta != null && !string.IsNullOrEmpty(fileMeta.FilePath))
            {
                string normalizedPath = fileMeta.FilePath.ToLowerInvariant();
                fileMeta.FilePath = normalizedPath;
                fileMetaMap.TryAdd(normalizedPath, fileMeta);
                Logger.Info($"添加文件元数据: {normalizedPath}");
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