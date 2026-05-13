using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Business
{
    public class AutoFillAttemptManager
    {
        private static AutoFillAttemptManager instance;
        private static readonly object lockObject = new object();
        
        private readonly ConcurrentDictionary<string, AutoFillAttemptRecord> attemptRecords;
        
        private const int MAX_RECORDS = 1000;
        private const int CLEANUP_THRESHOLD = 50;

        private AutoFillAttemptManager()
        {
            attemptRecords = new ConcurrentDictionary<string, AutoFillAttemptRecord>();
        }

        public static AutoFillAttemptManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        if (instance == null)
                        {
                            instance = new AutoFillAttemptManager();
                        }
                    }
                }
                return instance;
            }
        }

        public bool HasAttempted(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }
            return attemptRecords.ContainsKey(filePath);
        }

        public AutoFillAttemptRecord GetRecord(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }
            
            attemptRecords.TryGetValue(filePath, out AutoFillAttemptRecord record);
            return record;
        }

        public bool AddAttempt(string filePath, string fileUid = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Logger.Warning("AddAttempt: 文件路径为空");
                return false;
            }

            EnsureCapacity();

            var newRecord = new AutoFillAttemptRecord(filePath, fileUid, false, null);
            bool added = attemptRecords.TryAdd(filePath, newRecord);
            
            if (added)
            {
                Logger.Info($"已记录文档自动填充尝试: {filePath}");
            }
            else
            {
                attemptRecords.TryGetValue(filePath, out AutoFillAttemptRecord existingRecord);
                if (existingRecord != null)
                {
                    existingRecord.IncrementAttemptCount();
                    Logger.Info($"文档 {filePath} 自动填充尝试次数增加，当前次数: {existingRecord.AttemptCount}");
                }
            }
            
            return added;
        }

        public void MarkSuccess(string filePath, string passwordHint = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            if (attemptRecords.TryGetValue(filePath, out AutoFillAttemptRecord record))
            {
                record.MarkSuccess(passwordHint);
                Logger.Info($"文档 {filePath} 自动填充成功");
            }
        }

        public bool RemoveRecord(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            bool removed = attemptRecords.TryRemove(filePath, out _);
            if (removed)
            {
                Logger.Info($"已移除文档 {filePath} 的自动填充尝试记录");
            }
            return removed;
        }

        public int RemoveRecordsByUid(string fileUid)
        {
            if (string.IsNullOrEmpty(fileUid))
            {
                return 0;
            }

            int removedCount = 0;
            var keysToRemove = attemptRecords
                .Where(kv => kv.Value.FileUid == fileUid)
                .Select(kv => kv.Key)
                .ToList();

            foreach (string key in keysToRemove)
            {
                if (attemptRecords.TryRemove(key, out _))
                {
                    removedCount++;
                    Logger.Info($"通过UID移除文档 {key} 的自动填充尝试记录");
                }
            }

            return removedCount;
        }

        public void RemoveAllRecords()
        {
            attemptRecords.Clear();
            Logger.Info("已清除所有自动填充尝试记录");
        }

        public int GetRecordCount()
        {
            return attemptRecords.Count;
        }

        public IEnumerable<AutoFillAttemptRecord> GetAllRecords()
        {
            return attemptRecords.Values.ToList();
        }

        public void OnDocumentClosed(string filePath)
        {
            Logger.Info($"文档关闭事件触发，尝试清理自动填充记录: {filePath}");
            
            RemoveRecord(filePath);
        }

        public void OnDocumentClosedByUid(string fileUid)
        {
            Logger.Info($"文档关闭事件触发(通过UID)，尝试清理自动填充记录: {fileUid}");
            
            RemoveRecordsByUid(fileUid);
        }

        private void EnsureCapacity()
        {
            if (attemptRecords.Count >= MAX_RECORDS)
            {
                var oldestRecords = attemptRecords
                    .OrderBy(kv => kv.Value.AttemptTime)
                    .Take(CLEANUP_THRESHOLD)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (string key in oldestRecords)
                {
                    attemptRecords.TryRemove(key, out _);
                }
                
                Logger.Info($"自动清理了 {oldestRecords.Count} 条过期的自动填充尝试记录");
            }
        }

        public void CleanupExpiredRecords(TimeSpan maxAge)
        {
            DateTime cutoffTime = DateTime.Now.Subtract(maxAge);
            var expiredKeys = attemptRecords
                .Where(kv => kv.Value.AttemptTime < cutoffTime)
                .Select(kv => kv.Key)
                .ToList();

            int removedCount = 0;
            foreach (string key in expiredKeys)
            {
                if (attemptRecords.TryRemove(key, out _))
                {
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                Logger.Info($"清理了 {removedCount} 条过期的自动填充尝试记录");
            }
        }
    }
}