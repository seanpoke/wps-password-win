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
                return null;
            }

            var fileMeta = GetFileMeta(filePath);
            if (fileMeta.HasPendingPasswords())
            {
                // 这里可以添加密码验证逻辑
                // 暂时返回第一个待定密码
                string password = fileMeta.PendingPasswordList.FirstOrDefault();
                Logger.Info($"获取文件 {filePath} 的写入密码: {password}");
                return password;
            }

            return fileMeta.CurrentPassword;
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
            if (string.IsNullOrEmpty(filePath) && fileMetaMap.TryRemove(filePath, out _))
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
            // 获取当前时间戳（毫秒级）
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            // 生成GUID
            string guid = Guid.NewGuid().ToString();
            // 组合时间戳和GUID，用下划线连接
            string uid = $"{timestamp}_{guid}";
            Logger.Info($"创建新的uid: '{uid}'");
            return uid;
        }
    }
}