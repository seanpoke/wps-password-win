using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WpsPasswordManager.Utils;

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
                        Logger.Info($"文件已经处于监听状态: {filePath}");
                    return;
                }
                
                try
                {
                    // 创建文件系统监听器
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
        
        // 文件修改事件处理
        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            string changeType = "修改";
            DateTime changeTime = DateTime.Now;
            
            try
            {
                Logger.Info($"文件{changeType}事件: {filePath}");
                Logger.Info($"修改时间: {changeTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                Logger.Info($"修改类型: {changeType}");
                Logger.Info($"文件路径: {filePath}");
                Logger.Info("----------------------------------------");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件修改事件处理失败: {ex.Message}");
            }
        }
        
        // 文件创建事件处理
        private static void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            string changeType = "创建";
            DateTime changeTime = DateTime.Now;
            
            try
            {
                Logger.Info($"文件{changeType}事件: {filePath}");
                Logger.Info($"修改时间: {changeTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                Logger.Info($"修改类型: {changeType}");
                Logger.Info($"文件路径: {filePath}");
                Logger.Info("----------------------------------------");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件创建事件处理失败: {ex.Message}");
            }
        }
        
        // 文件删除事件处理
        private static void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            string changeType = "删除";
            DateTime changeTime = DateTime.Now;
            
            try
            {
                Logger.Info($"文件{changeType}事件: {filePath}");
                Logger.Info($"修改时间: {changeTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                Logger.Info($"修改类型: {changeType}");
                Logger.Info($"文件路径: {filePath}");
                
                // 从监控列表中移除
                StopWatchingFile(filePath);
                Logger.Info("----------------------------------------");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件删除事件处理失败: {ex.Message}");
            }
        }
        
        // 文件重命名事件处理
        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            string oldFilePath = e.OldFullPath;
            string newFilePath = e.FullPath;
            string changeType = "重命名";
            DateTime changeTime = DateTime.Now;
            
            try
            {
                Logger.Info($"文件{changeType}事件");
                Logger.Info($"修改时间: {changeTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                Logger.Info($"修改类型: {changeType}");
                Logger.Info($"旧文件路径: {oldFilePath}");
                Logger.Info($"新文件路径: {newFilePath}");
                
                // 停止监听旧文件，开始监听新文件
                StopWatchingFile(oldFilePath);
                StartWatchingFile(newFilePath);
                Logger.Info("----------------------------------------");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件重命名事件处理失败: {ex.Message}");
            }
        }
    }
}