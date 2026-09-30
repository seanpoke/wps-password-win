using System.Collections.Concurrent;
using PasswordManager.Utils;

namespace PasswordManager.Business
{
    public class AutoFillAttemptManager
    {
        private static AutoFillAttemptManager instance;
        private static readonly object lockObject = new object();
        
        private readonly ConcurrentDictionary<string, AutoFillAttemptRecord> attemptRecords;
        private readonly ConcurrentDictionary<string, byte> _userCancelledDocs;

        private AutoFillAttemptManager()
        {
            attemptRecords = new ConcurrentDictionary<string, AutoFillAttemptRecord>();
            _userCancelledDocs = new ConcurrentDictionary<string, byte>();
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

        /// <summary>用户在密码输入弹窗点了取消：后续轮询对该文档不再自动填充/弹窗（文档关闭时重置）。</summary>
        public void MarkUserCancelled(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }
            _userCancelledDocs.TryAdd(filePath, 0);
            Logger.Info($"已标记文档为用户取消自动填充: {filePath}");
        }

        /// <summary>该文档是否被用户取消过自动填充。</summary>
        public bool IsUserCancelled(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }
            return _userCancelledDocs.ContainsKey(filePath);
        }

        public void ResetAttempt(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            attemptRecords.TryRemove(filePath, out _);
            _userCancelledDocs.TryRemove(filePath, out _);
        }

        public void OnDocumentClosed(string filePath)
        {
            Logger.Info($"文档关闭行为触发，尝试清理自动填充记录: {filePath}");
            ResetAttempt(filePath);
        }

        public void RemoveAllRecords()
        {
            attemptRecords.Clear();
            Logger.Info("已清除所有自动填充尝试记录");
        }
    }
}