using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Forms;
using WpsPasswordManager.Monitor;
using WpsPasswordManager.Business;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.UI
{
    public class FloatingButtonManager : IDisposable
    {
        private readonly ConcurrentQueue<ButtonCommand> _commandQueue = new ConcurrentQueue<ButtonCommand>();
        private readonly AutoResetEvent _commandSignal = new AutoResetEvent(false);
        private Thread _commandProcessorThread;
        private readonly SynchronizationContext _uiSyncContext;
        
        private volatile bool _isRunning = false;
        private volatile bool _isButtonVisible = false;
        private volatile IntPtr _currentDialogHandle = IntPtr.Zero;
        
        private readonly FloatingButton _floatingButton;
        private readonly object _stateLock = new object();

        public enum CommandType
        {
            Show,
            Hide,
            UpdatePosition,
            UpdateFileMeta,
            Exit
        }

        public class ButtonCommand
        {
            public CommandType Type { get; set; }
            public IntPtr DialogHandle { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public FileMeta FileMeta { get; set; }
        }

        public event EventHandler<ButtonStateChangedEventArgs> ButtonStateChanged;

        public class ButtonStateChangedEventArgs : EventArgs
        {
            public bool IsVisible { get; set; }
            public IntPtr DialogHandle { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public FloatingButtonManager(FloatingButton floatingButton, SynchronizationContext uiSyncContext)
        {
            _floatingButton = floatingButton ?? throw new ArgumentNullException(nameof(floatingButton));
            _uiSyncContext = uiSyncContext ?? SynchronizationContext.Current;
        }

        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            _commandProcessorThread = new Thread(CommandProcessorLoop)
            {
                IsBackground = true,
                Name = "FloatingButtonCommandProcessor"
            };
            _commandProcessorThread.Start();
            
            Logger.Info("FloatingButtonManager 已启动");
        }

        private void CommandProcessorLoop()
        {
            Logger.Info("命令处理线程已启动");
            
            while (_isRunning)
            {
                try
                {
                    _commandSignal.WaitOne(1000);
                    
                    while (_commandQueue.TryDequeue(out var command))
                    {
                        ProcessCommand(command);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"命令处理线程错误: {ex.Message}");
                }
            }
            
            Logger.Info("命令处理线程已退出");
        }

        private void ProcessCommand(ButtonCommand command)
        {
            switch (command.Type)
            {
                case CommandType.Show:
                    _uiSyncContext.Post(_ => 
                    {
                        try
                        {
                            _floatingButton.CurrentFileMeta = command.FileMeta;
                            _floatingButton.ShowAtDialog(command.DialogHandle);
                            UpdateState(true, command.DialogHandle);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"显示悬浮按钮失败: {ex.Message}");
                        }
                    }, null);
                    break;

                case CommandType.Hide:
                    _uiSyncContext.Post(_ => 
                    {
                        try
                        {
                            _floatingButton.HideButton();
                            UpdateState(false, IntPtr.Zero);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"隐藏悬浮按钮失败: {ex.Message}");
                        }
                    }, null);
                    break;

                case CommandType.UpdatePosition:
                    _uiSyncContext.Post(_ => 
                    {
                        try
                        {
                            _floatingButton.Location = new System.Drawing.Point(command.X, command.Y);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"更新悬浮按钮位置失败: {ex.Message}");
                        }
                    }, null);
                    break;

                case CommandType.UpdateFileMeta:
                    _uiSyncContext.Post(_ => 
                    {
                        try
                        {
                            _floatingButton.CurrentFileMeta = command.FileMeta;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"更新文件元数据失败: {ex.Message}");
                        }
                    }, null);
                    break;

                case CommandType.Exit:
                    _isRunning = false;
                    break;
            }
        }

        private void UpdateState(bool isVisible, IntPtr dialogHandle)
        {
            lock (_stateLock)
            {
                _isButtonVisible = isVisible;
                _currentDialogHandle = dialogHandle;
            }
            
            OnButtonStateChanged(isVisible, dialogHandle);
        }

        private void OnButtonStateChanged(bool isVisible, IntPtr dialogHandle)
        {
            try
            {
                ButtonStateChanged?.Invoke(this, new ButtonStateChangedEventArgs
                {
                    IsVisible = isVisible,
                    DialogHandle = dialogHandle,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"触发状态变更事件失败: {ex.Message}");
            }
        }

        public void ShowButton(IntPtr dialogHandle, FileMeta fileMeta = null)
        {
            _commandQueue.Enqueue(new ButtonCommand
            {
                Type = CommandType.Show,
                DialogHandle = dialogHandle,
                FileMeta = fileMeta
            });
            _commandSignal.Set();
            
            Logger.Debug($"已提交显示命令，对话框句柄: {dialogHandle}");
        }

        public void HideButton()
        {
            _commandQueue.Enqueue(new ButtonCommand { Type = CommandType.Hide });
            _commandSignal.Set();
            
            Logger.Debug("已提交隐藏命令");
        }

        public void UpdateButtonPosition(int x, int y)
        {
            _commandQueue.Enqueue(new ButtonCommand
            {
                Type = CommandType.UpdatePosition,
                X = x,
                Y = y
            });
            _commandSignal.Set();
        }

        public void UpdateFileMeta(FileMeta fileMeta)
        {
            _commandQueue.Enqueue(new ButtonCommand
            {
                Type = CommandType.UpdateFileMeta,
                FileMeta = fileMeta
            });
            _commandSignal.Set();
        }

        public void Dispose()
        {
            Logger.Info("正在停止 FloatingButtonManager");
            
            _isRunning = false;
            _commandQueue.Enqueue(new ButtonCommand { Type = CommandType.Exit });
            _commandSignal.Set();

            if (_commandProcessorThread != null && _commandProcessorThread.IsAlive)
            {
                if (_commandProcessorThread.Join(TimeSpan.FromSeconds(2)))
                {
                    Logger.Info("命令处理线程已正常退出");
                }
                else
                {
                    Logger.Warning("命令处理线程强制退出");
                }
            }

            _commandSignal.Dispose();
            Logger.Info("FloatingButtonManager 已停止");
        }
    }
}