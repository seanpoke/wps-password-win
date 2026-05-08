using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using WpsPasswordManager.Utils;
using WpsPasswordManager.Business;

namespace WpsPasswordManager.Monitor
{
    public class FileMonitor
    {
        // 存储已监听的文件路径及其对应的监听器
        private static Dictionary<string, FileSystemWatcher> _fileWatchers = new Dictionary<string, FileSystemWatcher>();
        
        // 线程安全的锁对象
        private static readonly object _lockObject = new object();
        
        /// <summary>
        /// 检查文件是否已被监听
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否已被监听</returns>
        public static bool IsFileBeingWatched(string filePath)
        {
            lock (_lockObject)
            {
                return _fileWatchers.ContainsKey(filePath);
            }
        }
        
        /// <summary>
        /// 开始监听文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static void StartWatchingFile(string filePath)
        {
            StartWatchingFile(filePath, true);
        }
        
        /// <summary>
        /// 开始监听文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="logEnabled">是否启用日志</param>
        public static void StartWatchingFile(string filePath, bool logEnabled)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                if (logEnabled)
                    Logger.Warning("文件路径为空，无法启动监听");
                return;
            }
            
            if (!File.Exists(filePath))
            {
                if (logEnabled)
                    Logger.Warning($"文件不存在: {filePath}");
                return;
            }
            
            lock (_lockObject)
            {
                // 检查文件是否已经被监听
                if (_fileWatchers.ContainsKey(filePath))
                {
                    if (logEnabled)
                        return;
                }
                
                try
                {
                    string directoryPath = Path.GetDirectoryName(filePath);
                    string fileName = Path.GetFileName(filePath);
                    
                    FileSystemWatcher watcher = new FileSystemWatcher
                    {
                        Path = directoryPath,
                        Filter = fileName,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                        EnableRaisingEvents = true
                    };
                    
                    // 订阅文件修改事件
                    watcher.Changed += OnFileChanged;
                    watcher.Created += OnFileCreated;
                    watcher.Deleted += OnFileDeleted;
                    watcher.Renamed += OnFileRenamed;
                    
                    // 添加到监控列表
                    _fileWatchers.Add(filePath, watcher);
                    
                    if (logEnabled)
                        Logger.Info($"开始监听文件: {filePath}");
                }
                catch (Exception ex)
                {
                    if (logEnabled)
                        Logger.Error($"启动文件监听失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 停止监听文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static void StopWatchingFile(string filePath)
        {
            lock (_lockObject)
            {
                if (_fileWatchers.TryGetValue(filePath, out FileSystemWatcher watcher))
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                        _fileWatchers.Remove(filePath);
                        Logger.Info($"停止监听文件: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"停止文件监听失败: {ex.Message}");
                    }
                }
                else
                {
                    Logger.Warning($"文件未被监听: {filePath}");
                }
            }
        }
        
        /// <summary>
        /// 停止所有文件监听
        /// </summary>
        public static void StopAllWatching()
        {
            lock (_lockObject)
            {
                foreach (var watcher in _fileWatchers.Values)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"停止文件监听失败: {ex.Message}");
                    }
                }
                
                _fileWatchers.Clear();
                Logger.Info("停止所有文件监听");
            }
        }
        
        /// <summary>
        /// 获取当前监听的文件列表
        /// </summary>
        /// <returns>监听的文件路径列表</returns>
        public static List<string> GetWatchedFiles()
        {
            lock (_lockObject)
            {
                return _fileWatchers.Keys.ToList();
            }
        }
        
        // 文件修改事件处理 - 仅设置IsModify标志，不执行写入操作
        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            
            try
            {
                Logger.Info($"文件修改事件: {filePath}");

                if (FileMetaFactory.Instance.IsPluginOperation())
                {
                    Logger.Info($"跳过由插件引起的文件修改事件: {filePath}");
                    return;
                }

                // 获取文件元数据并设置IsModify标志
                FileMeta fileMeta = FileMetaFactory.Instance.GetFileMeta(filePath);
                if (fileMeta != null)
                {
                    fileMeta.IsModify = true;
                    Logger.Info($"已标记文件 {filePath} 的元数据为已修改");
                }
                else
                {
                    Logger.Warning($"未找到文件 {filePath} 的元数据");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"文件修改事件处理失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 判断文件是否被锁定（正在被其他进程写入）
        /// </summary>
        /// <param name="file">文件信息</param>
        /// <returns>true表示文件被锁定，false表示文件可访问</returns>
        public static bool IsFileLocked(FileInfo file)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 100;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        return false;
                    }
                }
                catch (IOException ex)
                {
                    if (retry < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                    else
                    {
                        Logger.Debug($"文件 {file.FullName} 被锁定，尝试了 {maxRetries} 次，最后异常: {ex.Message}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"检测文件锁定状态时发生未知异常: {ex.Message}");
                    return true;
                }
            }

            return true;
        }
        
        // 文件创建事件处理
        private static void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            DateTime changeTime = DateTime.Now;
            
            try
            {
                Logger.Info($"文件创建事件: {filePath}");
                Logger.Info($"修改时间: {changeTime.ToString("yyyy-MM-dd HH:mm:ss")}");
            }
            catch (Exception ex)
            {
                Logger.Error($"文件创建事件处理失败: {ex.Message}");
            }
        }
        
        // 文件删除事件处理
        private static void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            DateTime changeTime = DateTime.Now;
            
            try
            {
                Logger.Info($"文件删除事件: {filePath}");
                Logger.Info($"修改时间: {changeTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                
                // 从监控列表中移除
                StopWatchingFile(filePath);
                
                // 清理文件元数据缓存
                FileMetaFactory.Instance.CleanupFileMeta(filePath);
            }
            catch (Exception ex)
            {
                Logger.Error($"文件删除事件处理失败: {ex.Message}");
            }
        }
        
        // 文件重命名事件处理
        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            string oldFilePath = e.OldFullPath;
            string newFilePath = e.FullPath;
            DateTime changeTime = DateTime.Now;
            
            try
            {
                Logger.Info($"文件重命名事件");
                Logger.Info($"修改时间: {changeTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                Logger.Info($"旧文件路径: {oldFilePath}");
                Logger.Info($"新文件路径: {newFilePath}");
                
                // 停止监听旧文件，开始监听新文件
                StopWatchingFile(oldFilePath);
                StartWatchingFile(newFilePath);
            }
            catch (Exception ex)
            {
                Logger.Error($"文件重命名事件处理失败: {ex.Message}");
            }
        }
    }
}