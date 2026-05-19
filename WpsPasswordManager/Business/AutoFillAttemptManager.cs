using System.Collections.Concurrent;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Business
{
    public class AutoFillAttemptManager
    {
        private static AutoFillAttemptManager instance;
        private static readonly object lockObject = new object();
        
        private readonly ConcurrentDictionary<string, AutoFillAttemptRecord> attemptRecords;

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

        public void MarkAttempted(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Logger.Warning("MarkAttempted: 文件路径为空");
                return;
            }

            attemptRecords.TryAdd(filePath, new AutoFillAttemptRecord(filePath));
            Logger.Info($"已记录文档自动填充尝试: {filePath}");
        }

        public void ResetAttempt(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            attemptRecords.TryRemove(filePath, out _);
        }

        public void OnDocumentClosed(string filePath)
        {
            Logger.Info($"文档关闭事件触发，尝试清理自动填充记录: {filePath}");
            ResetAttempt(filePath);
        }

        public void RemoveAllRecords()
        {
            attemptRecords.Clear();
            Logger.Info("已清除所有自动填充尝试记录");
        }
    }
}