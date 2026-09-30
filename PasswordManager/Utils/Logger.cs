using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;

namespace PasswordManager.Utils
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public static class Logger
    {
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        private static string _lastCleanupDate = "";
        private static readonly ConcurrentQueue<string> _fileWriteQueue = new ConcurrentQueue<string>();
        private static readonly AutoResetEvent _fileWaitEvent = new AutoResetEvent(false);
        private static Thread _fileWorkerThread;
        private static volatile bool _isRunning = true;

        private static readonly ConcurrentQueue<string> _logWindowQueue = new ConcurrentQueue<string>();
        private static readonly AutoResetEvent _windowWaitEvent = new AutoResetEvent(false);
        private static Thread _windowWorkerThread;
        private static volatile bool _windowPaused = false;
        private static Action<string> _logWindowUpdateCallback;

        private static volatile LogLevel _minLogLevel = GetInitialLogLevel();

        /// <summary>运行时调整日志级别（开发者模式开 → Debug，关 → Info）。</summary>
        public static void SetMinLevel(LogLevel level) => _minLogLevel = level;

        /// <summary>当前生效的日志级别。</summary>
        public static LogLevel CurrentLevel => _minLogLevel;

        // 初始值来自持久化状态（%AppData%\PasswordManager\log_record.state），跨启动保持上次选择
        private static volatile bool _fileLoggingEnabled = StorageManager.LoadLogRecordEnabled();

        /// <summary>开启/关闭日志落盘（仅控制文件写入；日志窗口实时显示不受影响）。</summary>
        public static void SetFileLoggingEnabled(bool enabled) => _fileLoggingEnabled = enabled;

        /// <summary>当前是否正在写日志文件。</summary>
        public static bool FileLoggingEnabled => _fileLoggingEnabled;

        private static LogLevel GetInitialLogLevel()
        {
            string envLogLevel = Environment.GetEnvironmentVariable("WPS_PASSWORD_LOG_LEVEL");
            if (!string.IsNullOrEmpty(envLogLevel))
            {
                if (Enum.TryParse<LogLevel>(envLogLevel, true, out LogLevel parsedLevel))
                {
                    return parsedLevel;
                }
            }
            // 默认 Info：DEBUG 日志不再写入（需要排查时设置环境变量 WPS_PASSWORD_LOG_LEVEL=Debug）
            return LogLevel.Info;
        }

        static Logger()
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            _fileWorkerThread = new Thread(ProcessFileWriteQueue)
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal,
                Name = "LogFileWorker"
            };
            _fileWorkerThread.Start();

            _windowWorkerThread = new Thread(ProcessLogWindowQueue)
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest,
                Name = "LogWindowWorker"
            };
            _windowWorkerThread.Start();
        }

        public static void SetLogWindowCallback(Action<string> callback)
        {
            _logWindowUpdateCallback = callback;
            if (callback != null)
            {
                _windowWaitEvent.Set();
            }
        }

        public static void Info(string message)
        {
            if (_minLogLevel > LogLevel.Info) return;
            EnqueueLog("INFO", message);
        }

        public static void Warning(string message)
        {
            if (_minLogLevel > LogLevel.Warning) return;
            EnqueueLog("WARNING", message);
        }

        public static void Error(string message)
        {
            if (_minLogLevel > LogLevel.Error) return;
            EnqueueLog("ERROR", message);
        }

        public static void Debug(string message)
        {
            if (_minLogLevel > LogLevel.Debug) return;
            EnqueueLog("DEBUG", message);
        }

        private static void EnqueueLog(string level, string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                Console.WriteLine(logEntry);

                if (_fileLoggingEnabled)
                {
                    _fileWriteQueue.Enqueue(logEntry);
                    _fileWaitEvent.Set();
                }

                if (!_windowPaused && _logWindowUpdateCallback != null)
                {
                    _logWindowQueue.Enqueue(logEntry);
                    _windowWaitEvent.Set();
                }
            }
            catch { }
        }

        private static void ProcessFileWriteQueue()
        {
            StringBuilder buffer = new StringBuilder();
            
            while (_isRunning)
            {
                try
                {
                    while (_fileWriteQueue.TryDequeue(out string logEntry))
                    {
                        buffer.AppendLine(logEntry);
                        
                        if (buffer.Length >= 8192)
                        {
                            WriteToFile(buffer.ToString());
                            buffer.Clear();
                        }
                    }

                    if (buffer.Length > 0)
                    {
                        WriteToFile(buffer.ToString());
                        buffer.Clear();
                    }
                }
                catch { }

                _fileWaitEvent.WaitOne(5000);
            }

            try
            {
                if (buffer.Length > 0)
                {
                    WriteToFile(buffer.ToString());
                }
            }
            catch { }
        }

        private static void ProcessLogWindowQueue()
        {
            StringBuilder buffer = new StringBuilder();
            int batchCount = 0;
            const int maxBatchSize = 50;

            while (_isRunning)
            {
                try
                {
                    buffer.Clear();
                    batchCount = 0;

                    while (_logWindowQueue.TryDequeue(out string logEntry) && batchCount < maxBatchSize)
                    {
                        buffer.AppendLine(logEntry);
                        batchCount++;
                    }

                    if (buffer.Length > 0 && _logWindowUpdateCallback != null)
                    {
                        try
                        {
                            _logWindowUpdateCallback(buffer.ToString());
                        }
                        catch { }
                    }
                }
                catch { }

                if (_logWindowQueue.IsEmpty)
                {
                    _windowWaitEvent.WaitOne();
                }
            }
        }

        /// <summary>当前日志文件：按天分文件（log_yyyy-MM-dd.log）。</summary>
        private static string CurrentLogFile => Path.Combine(LogDirectory, $"log_{DateTime.Now:yyyy-MM-dd}.log");

        private static void WriteToFile(string content)
        {
            try
            {
                string logFile = CurrentLogFile;

                // 每天首次写入时清理过期日志（保留最近 5 天）
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                if (_lastCleanupDate != today)
                {
                    _lastCleanupDate = today;
                    CleanupOldLogs();
                }

                // 单文件超 10MB 滚动为 .bak（避免单日超大文件）
                const long maxLogFileSize = 10 * 1024 * 1024;
                if (File.Exists(logFile))
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.Length > maxLogFileSize)
                    {
                        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                        string backupPath = Path.Combine(LogDirectory, $"log_{DateTime.Now:yyyy-MM-dd}_{timestamp}.bak");
                        File.Move(logFile, backupPath);
                    }
                }

                File.AppendAllText(logFile, content, Encoding.UTF8);
            }
            catch { }
        }

        /// <summary>
        /// 日志清理（按天分文件 + 保留 5 天）：删除 5 天前的 .log 与 .bak 日志文件。
        /// </summary>
        private static void CleanupOldLogs()
        {
            try
            {
                DateTime keepAfter = DateTime.Today.AddDays(-4);   // 保留今天+前4天共 5 天
                foreach (string file in Directory.GetFiles(LogDirectory, "log_*.log"))
                {
                    if (File.GetLastWriteTime(file).Date < keepAfter)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
                foreach (string file in Directory.GetFiles(LogDirectory, "log_*.bak"))
                {
                    if (File.GetLastWriteTime(file).Date < keepAfter)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
