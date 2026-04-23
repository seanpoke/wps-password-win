using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

            return fileMetaMap[filePath];
        }

        public void UpdatePendingPassword(string filePath, string password)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(password))
            {
                return;
            }

            var fileMeta = GetFileMeta(filePath);
            fileMeta.AddPendingPassword(password);
            Logger.Info($"更新文件 {filePath} 的待定密码: {password}");
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
    }
}