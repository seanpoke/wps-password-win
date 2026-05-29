using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using PasswordManager.Utils;

namespace PasswordManager.Business
{
    public static class FileStateManager
    {
        private static readonly ConcurrentDictionary<string, FileTraits> _monitoredFiles = new ConcurrentDictionary<string, FileTraits>();

        private struct FileTraits
        {
            public DateTime LastWriteTime { get; set; }
            public string Hash { get; set; }
        }

        public static void RegisterFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                if (_monitoredFiles.ContainsKey(filePath))
                {
                    Logger.Debug($"[文件特征登记] 文件已登记，跳过重复登记: {Path.GetFileName(filePath)}");
                    return;
                }

                if (TryGetFileTraits(filePath, out FileTraits traits))
                {
                    _monitoredFiles[filePath] = traits;
                    Logger.Info($"[文件特征登记成功] 文件: {Path.GetFileName(filePath)}，初始哈希: {traits.Hash?.Substring(0, 8)}...");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[文件特征登记失败] 无法读取文件特征: {ex.Message}");
            }
        }

        public static void UnregisterFile(string filePath)
        {
            if (_monitoredFiles.TryRemove(filePath, out _))
            {
                Logger.Debug($"[文件特征注销] 文件: {Path.GetFileName(filePath)}");
            }
        }

        public static bool HasFileChanged(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                if (!_monitoredFiles.TryGetValue(filePath, out FileTraits savedTraits))
                {
                    Logger.Warning($"[文件特征检测] 文件未登记: {Path.GetFileName(filePath)}");
                    return false;
                }

                DateTime currentWriteTime = File.GetLastWriteTime(filePath);
                if (currentWriteTime == savedTraits.LastWriteTime)
                {
                    return false;
                }

                if (TryGetFileTraits(filePath, out FileTraits currentTraits))
                {
                    if (currentTraits.Hash != savedTraits.Hash)
                    {
                        _monitoredFiles[filePath] = currentTraits;
                        Logger.Info($"[文件特征检测] 文件内容已修改: {Path.GetFileName(filePath)} | 旧哈希: {savedTraits.Hash?.Substring(0, 8)}...({savedTraits.LastWriteTime:yyyy-MM-dd HH:mm:ss}) → 新哈希: {currentTraits.Hash?.Substring(0, 8)}...({currentTraits.LastWriteTime:yyyy-MM-dd HH:mm:ss})");
                        return true;
                    }
                    else
                    {
                        _monitoredFiles[filePath] = currentTraits;
                        return false;
                    }
                }
            }
            catch (IOException)
            {
                Logger.Debug($"[文件特征检测] 文件被锁定，跳过检测: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[文件特征检测] 检测文件状态时发生异常: {ex.Message}");
            }

            return false;
        }

        private static bool TryGetFileTraits(string filePath, out FileTraits traits)
        {
            traits = new FileTraits();
            if (!File.Exists(filePath)) return false;

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length == 0) return false;

                    using (var sha256 = SHA256.Create())
                    {
                        byte[] hashBytes = sha256.ComputeHash(stream);

                        traits.LastWriteTime = File.GetLastWriteTime(filePath);
                        traits.Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                        return true;
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        public static bool IsFileRegistered(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && _monitoredFiles.ContainsKey(filePath);
        }

        public static int GetRegisteredFileCount()
        {
            return _monitoredFiles.Count;
        }

        public static void ClearAll()
        {
            _monitoredFiles.Clear();
            Logger.Info("[文件特征管理] 已清理所有文件特征");
        }
    }
}