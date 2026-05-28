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
        private static readonly string LogFilePath = Path.Combine(LogDirectory, "wps_password_manager.log");
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
#if DEBUG
            return LogLevel.Info;
#else
            return LogLevel.Info;
#endif
        }

        public static LogLevel MinLogLevel
        {
            get => _minLogLevel;
            set => _minLogLevel = value;
        }

        public static void SetLogLevel(LogLevel level)
        {
            _minLogLevel = level;
            Logger.Info($"日志级别已设置为: {level}");
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

        public static void PauseLogWindow()
        {
            _windowPaused = true;
        }

        public static void ResumeLogWindow()
        {
            _windowPaused = false;
            _windowWaitEvent.Set();
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
                _fileWriteQueue.Enqueue(logEntry);
                _fileWaitEvent.Set();

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

        private static void WriteToFile(string content)
        {
            try
            {
                const long maxLogFileSize = 10 * 1024 * 1024;
                
                if (File.Exists(LogFilePath))
                {
                    FileInfo fileInfo = new FileInfo(LogFilePath);
                    if (fileInfo.Length > maxLogFileSize)
                    {
                        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                        string backupPath = Path.Combine(LogDirectory, $"log_{timestamp}.bak");
                        File.Move(LogFilePath, backupPath);
                    }
                }
                
                File.AppendAllText(LogFilePath, content, Encoding.UTF8);
            }
            catch { }
        }
    }
}
