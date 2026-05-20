using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WpsPasswordManager.Monitor;
using WpsPasswordManager.Utils;
using WpsPasswordManager.Business;

namespace WpsPasswordManager.UI
{
    public class FloatingButton : Form
    {
        private Button _generateButton;
        private Button _extractPasswordButton;
        private Button _authButton;
        private IntPtr _passwordEditHandle;
        private WpsMonitor _monitor;
        private bool _isVisible = false;
        private FileMeta _currentFileMeta;
        private IntPtr _parentDialogHandle = IntPtr.Zero;

        public FileMeta CurrentFileMeta
        {
            get => _currentFileMeta;
            set
            {
                _currentFileMeta = value;
                UpdateAuthButtonVisibility();
            }
        }

        // Win32 API 定义
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        // 常量定义
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public event EventHandler GeneratePasswordClicked;

        public FloatingButton(WpsMonitor monitor)
        {
            _monitor = monitor;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // 配置表单
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = false;
            this.ShowInTaskbar = false;
            this.TransparencyKey = System.Drawing.Color.Magenta;
            this.BackColor = System.Drawing.Color.Magenta;
            this.StartPosition = FormStartPosition.Manual;

            // 创建生成密码按钮
            _generateButton = new Button
            {
                Text = "生成密码",
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(0, 120, 212),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold),
                Size = new System.Drawing.Size(90, 32),
                Location = new System.Drawing.Point(0, 0),
                Cursor = System.Windows.Forms.Cursors.Hand,
                UseVisualStyleBackColor = false,
                FlatAppearance = { BorderSize = 0 }
            };
            _generateButton.Click += (sender, e) => GeneratePasswordClicked?.Invoke(this, EventArgs.Empty);
            _generateButton.MouseEnter += (sender, e) => _generateButton.BackColor = System.Drawing.Color.FromArgb(26, 115, 232);
            _generateButton.MouseLeave += (sender, e) => _generateButton.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
            _generateButton.MouseDown += (sender, e) => _generateButton.BackColor = System.Drawing.Color.FromArgb(0, 90, 170);
            _generateButton.MouseUp += (sender, e) => _generateButton.BackColor = System.Drawing.Color.FromArgb(26, 115, 232);

            // 创建提取密码按钮
            _extractPasswordButton = new Button
            {
                Text = "提取密码",
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(0, 150, 136),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold),
                Size = new System.Drawing.Size(90, 32),
                Location = new System.Drawing.Point(0, 36),
                Cursor = System.Windows.Forms.Cursors.Hand,
                UseVisualStyleBackColor = false,
                FlatAppearance = { BorderSize = 0 }
            };
            _extractPasswordButton.Click += ExtractPasswordButton_Click;
            _extractPasswordButton.MouseEnter += (sender, e) => _extractPasswordButton.BackColor = System.Drawing.Color.FromArgb(0, 170, 156);
            _extractPasswordButton.MouseLeave += (sender, e) => _extractPasswordButton.BackColor = System.Drawing.Color.FromArgb(0, 150, 136);
            _extractPasswordButton.MouseDown += (sender, e) => _extractPasswordButton.BackColor = System.Drawing.Color.FromArgb(0, 120, 110);
            _extractPasswordButton.MouseUp += (sender, e) => _extractPasswordButton.BackColor = System.Drawing.Color.FromArgb(0, 170, 156);

            // 创建文档权限按钮
            _authButton = new Button
            {
                Text = "文档权限",
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(156, 39, 176),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold),
                Size = new System.Drawing.Size(90, 32),
                Location = new System.Drawing.Point(0, 72),
                Cursor = System.Windows.Forms.Cursors.Hand,
                Visible = false,
                UseVisualStyleBackColor = false,
                FlatAppearance = { BorderSize = 0 }
            };
            _authButton.Click += AuthButton_Click;
            _authButton.MouseEnter += (sender, e) => _authButton.BackColor = System.Drawing.Color.FromArgb(176, 59, 196);
            _authButton.MouseLeave += (sender, e) => _authButton.BackColor = System.Drawing.Color.FromArgb(156, 39, 176);
            _authButton.MouseDown += (sender, e) => _authButton.BackColor = System.Drawing.Color.FromArgb(126, 29, 146);
            _authButton.MouseUp += (sender, e) => _authButton.BackColor = System.Drawing.Color.FromArgb(176, 59, 196);

            this.Controls.Add(_generateButton);
            this.Controls.Add(_extractPasswordButton);
            this.Controls.Add(_authButton);
            this.Size = new System.Drawing.Size(90, 104);
        }

        private void AuthButton_Click(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户点击了文档权限按钮");
                if (_currentFileMeta != null && !string.IsNullOrEmpty(_currentFileMeta.Uid))
                {
                    AuthTreeForm authForm = new AuthTreeForm(_currentFileMeta.Uid);
                    authForm.ShowDialog();
                }
                else
                {
                    ShowNotification("文档ID为空，无法获取权限信息");
                    Logger.Warning("文档ID为空，无法获取权限信息");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"打开文档权限对话框时出错: {ex.Message}");
                ShowNotification("打开权限对话框时出错");
            }
        }

        private void UpdateAuthButtonVisibility()
        {
            if (_authButton != null)
            {
                bool shouldShow = _currentFileMeta != null && _currentFileMeta.WriteAuth;
                _authButton.Visible = shouldShow;
                
                if (shouldShow)
                {
                    this.Size = new System.Drawing.Size(90, 104);
                }
                else
                {
                    this.Size = new System.Drawing.Size(90, 68);
                }
                
                Logger.Debug($"文档权限按钮显示状态更新: {shouldShow}");
            }
        }

        private void ExtractPasswordButton_Click(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户点击了提取密码按钮");
                // 查找密码对话框
                IntPtr dialogHandle = _monitor.FindPasswordDialog();
                Logger.Info($"找到密码对话框: {dialogHandle}");
                if (dialogHandle != IntPtr.Zero)
                {
                    // 直接调用Program中的GetPasswordFromDialog方法
                    string password = Program.GetPasswordFromDialog(dialogHandle);
                    Logger.Info($"获取到密码: '{password}'");
                    if (!string.IsNullOrEmpty(password))
                    {
                        // 复制到剪贴板
                        Clipboard.SetText(password);
                        // 显示成功提示
                        ShowNotification("密码已复制到剪贴板");
                        Logger.Info("密码已提取并复制到剪贴板");
                    }
                    else
                    {
                        ShowNotification("未找到密码或密码为空");
                        Logger.Warning("未找到密码或密码为空，无法提取");
                    }
                }
                else
                {
                    ShowNotification("未找到密码对话框");
                    Logger.Warning("未找到密码对话框，无法提取密码");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"提取密码时出错: {ex.Message}");
                ShowNotification("提取密码时出错");
            }
        }

        // 从密码对话框获取密码（使用UI Automation）
        private string GetPasswordFromDialog(IntPtr dialogHandle)
        {
            try
            {
                Logger.Info("FloatingButton: 尝试从密码对话框获取密码");
                
                // 使用UI Automation获取密码
                string password = GetPasswordUsingUIAutomation(dialogHandle);
                if (!string.IsNullOrEmpty(password))
                {
                    Logger.Info($"FloatingButton: 通过UI Automation获取到密码: {password}");
                    return password;
                }
                
                Logger.Warning("FloatingButton: 无法通过UI Automation获取密码");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Error($"FloatingButton: 获取密码时出错: {ex.Message}");
                return string.Empty;
            }
        }

        // 使用UI Automation获取密码
        private string GetPasswordUsingUIAutomation(IntPtr dialogHandle)
        {
            try
            {
                Logger.Info("FloatingButton: 开始使用UI Automation获取密码");
                
                // 尝试加载UIAutomationClient程序集
                System.Reflection.Assembly uiaClient = null;
                try
                {
                    uiaClient = System.Reflection.Assembly.Load("UIAutomationClient, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    Logger.Info("FloatingButton: 成功加载UIAutomationClient程序集");
                }
                catch (Exception ex)
                {
                    Logger.Error($"FloatingButton: 加载UIAutomationClient程序集时出错: {ex.Message}");
                    
                    // 尝试加载UIAutomationClient的不同版本
                    try
                    {
                        uiaClient = System.Reflection.Assembly.Load("UIAutomationClient");
                        Logger.Info("FloatingButton: 成功加载UIAutomationClient程序集（无版本）");
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error($"FloatingButton: 加载UIAutomationClient程序集（无版本）时出错: {ex2.Message}");
                        return string.Empty;
                    }
                }
                
                if (uiaClient == null)
                {
                    Logger.Warning("FloatingButton: 无法加载UIAutomationClient程序集");
                    return string.Empty;
                }
                
                // 尝试加载UIAutomationTypes程序集
                System.Reflection.Assembly uiaTypes = null;
                try
                {
                    uiaTypes = System.Reflection.Assembly.Load("UIAutomationTypes, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    Logger.Info("FloatingButton: 成功加载UIAutomationTypes程序集");
                }
                catch (Exception ex)
                {
                    Logger.Error($"FloatingButton: 加载UIAutomationTypes程序集时出错: {ex.Message}");
                    
                    // 尝试加载UIAutomationTypes的不同版本
                    try
                    {
                        uiaTypes = System.Reflection.Assembly.Load("UIAutomationTypes");
                        Logger.Info("FloatingButton: 成功加载UIAutomationTypes程序集（无版本）");
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error($"FloatingButton: 加载UIAutomationTypes程序集（无版本）时出错: {ex2.Message}");
                        return string.Empty;
                    }
                }
                
                if (uiaTypes == null)
                {
                    Logger.Warning("FloatingButton: 无法加载UIAutomationTypes程序集");
                    return string.Empty;
                }
                
                // 获取AutomationElement类
                Type automationElementClass = uiaClient.GetType("System.Windows.Automation.AutomationElement");
                if (automationElementClass == null)
                {
                    Logger.Warning("FloatingButton: 无法获取AutomationElement类");
                    return string.Empty;
                }
                Logger.Info("FloatingButton: 成功获取AutomationElement类");
                
                // 获取FromHandle方法
                System.Reflection.MethodInfo fromHandleMethod = automationElementClass.GetMethod("FromHandle", new Type[] { typeof(IntPtr) });
                if (fromHandleMethod == null)
                {
                    Logger.Warning("FloatingButton: 无法获取FromHandle方法");
                    return string.Empty;
                }
                Logger.Info("FloatingButton: 成功获取FromHandle方法");
                
                // 创建AutomationElement
                object dialogElement = fromHandleMethod.Invoke(null, new object[] { dialogHandle });
                if (dialogElement == null)
                {
                    Logger.Warning("FloatingButton: 无法创建对话框的AutomationElement");
                    return string.Empty;
                }
                Logger.Info("FloatingButton: 成功创建对话框的AutomationElement");
                
                // 获取TreeScope枚举
                Type treeScopeType = uiaTypes.GetType("System.Windows.Automation.TreeScope");
                if (treeScopeType == null)
                {
                    Logger.Warning("FloatingButton: 无法获取TreeScope枚举");
                    return string.Empty;
                }
                Logger.Info("FloatingButton: 成功获取TreeScope枚举");
                
                // 获取TreeScope.Descendants值
                object treeScopeDescendants = System.Enum.Parse(treeScopeType, "Descendants");
                Logger.Info("FloatingButton: 成功获取TreeScope.Descendants值");
                
                // 获取PropertyCondition类
                Type propertyConditionClass = uiaClient.GetType("System.Windows.Automation.PropertyCondition");
                if (propertyConditionClass == null)
                {
                    Logger.Warning("FloatingButton: 无法获取PropertyCondition类");
                    return string.Empty;
                }
                Logger.Info("FloatingButton: 成功获取PropertyCondition类");
                
                // 获取ControlType类
                Type controlTypeClass = uiaTypes.GetType("System.Windows.Automation.ControlType");
                if (controlTypeClass == null)
                {
                    Logger.Warning("FloatingButton: 无法获取ControlType类");
                    return string.Empty;
                }
                Logger.Info("FloatingButton: 成功获取ControlType类");
                
                // 获取ControlType.Edit属性
                System.Reflection.PropertyInfo editProperty = controlTypeClass.GetProperty("Edit");
                if (editProperty == null)
                {
                    Logger.Warning("FloatingButton: 无法获取ControlType.Edit属性");
                    return string.Empty;
                }
                object editControlType = editProperty.GetValue(null);
                Logger.Info("FloatingButton: 成功获取ControlType.Edit属性");
                
                // 获取AutomationElement.ControlTypeProperty属性
                System.Reflection.PropertyInfo controlTypeProperty = automationElementClass.GetProperty("ControlTypeProperty");
                if (controlTypeProperty == null)
                {
                    Logger.Warning("FloatingButton: 无法获取ControlTypeProperty属性");
                    return string.Empty;
                }
                object controlTypePropertyValue = controlTypeProperty.GetValue(null);
                Logger.Info("FloatingButton: 成功获取ControlTypeProperty属性");
                
                // 创建PropertyCondition（查找编辑控件）
                System.Reflection.ConstructorInfo propertyConditionConstructor = propertyConditionClass.GetConstructor(new Type[] { controlTypeProperty.PropertyType, typeof(object) });
                if (propertyConditionConstructor == null)
                {
                    Logger.Warning("FloatingButton: 无法获取PropertyCondition构造函数");
                    return string.Empty;
                }
                object editCondition = propertyConditionConstructor.Invoke(new object[] { controlTypePropertyValue, editControlType });
                Logger.Info("FloatingButton: 成功创建PropertyCondition");
                
                // 获取FindAll方法
                System.Reflection.MethodInfo findAllMethod = automationElementClass.GetMethod("FindAll", new Type[] { treeScopeType, propertyConditionClass });
                if (findAllMethod == null)
                {
                    Logger.Warning("FloatingButton: 无法获取FindAll方法");
                    return string.Empty;
                }
                Logger.Info("FloatingButton: 成功获取FindAll方法");
                
                // 查找所有编辑控件
                object editElements = findAllMethod.Invoke(dialogElement, new object[] { treeScopeDescendants, editCondition });
                if (editElements == null)
                {
                    Logger.Warning("FloatingButton: 未找到编辑控件");
                    return string.Empty;
                }
                Logger.Info("FloatingButton: 成功找到编辑控件");
                
                // 获取Count属性
                System.Reflection.PropertyInfo countProperty = editElements.GetType().GetProperty("Count");
                if (countProperty == null)
                {
                    Logger.Warning("FloatingButton: 无法获取Count属性");
                    return string.Empty;
                }
                int editCount = (int)countProperty.GetValue(editElements);
                Logger.Info($"FloatingButton: 找到 {editCount} 个编辑控件");
                
                // 遍历编辑控件，查找密码输入框
                for (int i = 0; i < editCount; i++)
                {
                    // 获取Current属性
                    System.Reflection.MethodInfo getMethod = editElements.GetType().GetMethod("get_Item", new Type[] { typeof(int) });
                    if (getMethod == null)
                    {
                        Logger.Warning("FloatingButton: 无法获取get_Item方法");
                        continue;
                    }
                    object editElement = getMethod.Invoke(editElements, new object[] { i });
                    if (editElement == null)
                    {
                        continue;
                    }
                    
                    // 获取Current属性
                    System.Reflection.PropertyInfo currentProperty = editElement.GetType().GetProperty("Current");
                    if (currentProperty == null)
                    {
                        Logger.Warning("FloatingButton: 无法获取Current属性");
                        continue;
                    }
                    object current = currentProperty.GetValue(editElement);
                    if (current == null)
                    {
                        continue;
                    }
                    
                    // 获取Name属性
                    System.Reflection.PropertyInfo nameProperty = current.GetType().GetProperty("Name");
                    if (nameProperty == null)
                    {
                        Logger.Warning("FloatingButton: 无法获取Name属性");
                        continue;
                    }
                    string name = nameProperty.GetValue(current) as string;
                    Logger.Info($"FloatingButton: 编辑控件 #{i} 名称: {name}");
                    
                    // 检查是否为密码输入框（包含"密码"字样）
                    if (name != null && name.Contains("密码"))
                    {
                        Logger.Info($"FloatingButton: 找到【打开文件密码】输入框，开始获取内容，实际名称: {name}");
                        
                        // 尝试获取ValuePattern
                        Type valuePatternClass = uiaClient.GetType("System.Windows.Automation.ValuePattern");
                        if (valuePatternClass != null)
                        {
                            Logger.Info("FloatingButton: 成功获取ValuePattern类");
                            // 获取ValuePattern.Pattern属性
                            System.Reflection.PropertyInfo valuePatternProperty = valuePatternClass.GetProperty("Pattern");
                            if (valuePatternProperty != null)
                            {
                                Logger.Info("FloatingButton: 成功获取ValuePattern.Pattern属性");
                                object valuePatternPattern = valuePatternProperty.GetValue(null);
                                
                                // 获取GetCurrentPattern方法
                                System.Reflection.MethodInfo getCurrentPatternMethod = automationElementClass.GetMethod("GetCurrentPattern", new Type[] { valuePatternProperty.PropertyType });
                                if (getCurrentPatternMethod != null)
                                {
                                    Logger.Info("FloatingButton: 成功获取GetCurrentPattern方法");
                                    try
                                    {
                                        object valuePattern = getCurrentPatternMethod.Invoke(editElement, new object[] { valuePatternPattern });
                                        if (valuePattern != null)
                                        {
                                            Logger.Info("FloatingButton: 成功获取ValuePattern");
                                            // 获取Value属性
                                            System.Reflection.PropertyInfo valueProperty = valuePattern.GetType().GetProperty("Current");
                                            if (valueProperty != null)
                                            {
                                                Logger.Info("FloatingButton: 成功获取ValuePattern.Current属性");
                                                object valueCurrent = valueProperty.GetValue(valuePattern);
                                                if (valueCurrent != null)
                                                {
                                                    Logger.Info("FloatingButton: 成功获取ValuePattern.Current值");
                                                    System.Reflection.PropertyInfo valueProperty2 = valueCurrent.GetType().GetProperty("Value");
                                                    if (valueProperty2 != null)
                                                    {
                                                        Logger.Info("FloatingButton: 成功获取Value属性");
                                                        string password = valueProperty2.GetValue(valueCurrent) as string;
                                                        if (!string.IsNullOrEmpty(password))
                                                        {
                                                            Logger.Info($"FloatingButton: 从Qt输入框 #{i} 使用ValuePattern获取到密码: {password}");
                                                            return password;
                                                        }
                                                        else
                                                        {
                                                            Logger.Info("FloatingButton: 获取到的密码为空");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Logger.Warning("FloatingButton: 无法获取Value属性");
                                                    }
                                                }
                                                else
                                                {
                                                    Logger.Warning("FloatingButton: ValuePattern.Current值为null");
                                                }
                                            }
                                            else
                                            {
                                                Logger.Warning("FloatingButton: 无法获取ValuePattern.Current属性");
                                            }
                                        }
                                        else
                                        {
                                            Logger.Warning("FloatingButton: ValuePattern为null");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error($"FloatingButton: 获取ValuePattern时出错: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Logger.Warning("FloatingButton: 无法获取GetCurrentPattern方法");
                                }
                            }
                            else
                            {
                                Logger.Warning("FloatingButton: 无法获取ValuePattern.Pattern属性");
                            }
                        }
                        else
                        {
                            Logger.Warning("FloatingButton: 无法获取ValuePattern类");
                        }
                    }
                }
                
                Logger.Warning("FloatingButton: 未找到包含'密码'字样的输入框");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Error($"FloatingButton: 使用UI Automation获取密码时出错: {ex.Message}");
                return string.Empty;
            }
        }

        private void ShowNotification(string message)
        {
            // 创建并显示临时通知表单
            NotificationForm notificationForm = new NotificationForm();
            notificationForm.ShowNotification(message);
        }

        public new void Show()
        {
            base.Show();
            if (!_isVisible)
            {
                _isVisible = true;
            }
        }

        public void ShowAtPasswordBox(IntPtr passwordEditHandle)
        {
            _passwordEditHandle = passwordEditHandle;
            if (passwordEditHandle == IntPtr.Zero)
            {
                Logger.Warning("密码输入框句柄为空，无法显示悬浮按钮");
                this.Hide();
                return;
            }

            // 获取密码框位置
            WpsMonitor.RECT rect = _monitor.GetWindowRect(passwordEditHandle);
            float dpiScale = _monitor.GetDpiScale();

            // 计算按钮位置（密码框右侧5px）
            int x = (int)(rect.Right + 5 * dpiScale);
            int y = (int)(rect.Top * dpiScale);

            // 只有在位置发生变化时才更新位置
            if (this.Location.X != x || this.Location.Y != y)
            {
                Logger.Debug($"显示悬浮按钮，位置: X={x}, Y={y}, DPI缩放: {dpiScale}");
                this.Location = new System.Drawing.Point(x, y);
            }
            Show(); // 调用重写的 Show 方法
        }

        public void ShowAtDialog(IntPtr dialogHandle)
        {
            if (dialogHandle == IntPtr.Zero)
            {
                Logger.Warning("对话框句柄为空，无法显示悬浮按钮");
                this.Hide();
                return;
            }

            // 获取对话框位置（相对于屏幕）
            WpsMonitor.RECT rect = _monitor.GetWindowRect(dialogHandle);
            float dpiScale = _monitor.GetDpiScale();

            // 计算按钮位置（对话框右侧，距离右边框5px，垂直居中）
            int x = (int)(rect.Right + 5 * dpiScale);
            int y = (int)(rect.Top + (rect.Bottom - rect.Top - this.Height) / 2 * dpiScale);

            // 只有在位置发生变化时才更新位置
            if (this.Location.X != x || this.Location.Y != y)
            {
                Logger.Debug($"显示悬浮按钮在对话框右侧，位置: X={x}, Y={y}, DPI缩放: {dpiScale}");
                this.Location = new System.Drawing.Point(x, y);
            }

            // 设置按钮在对话框之后显示，确保层级同步
            SetWindowPos(this.Handle, dialogHandle, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            Logger.Debug($"设置悬浮按钮在对话框 {dialogHandle} 之后显示");

            Show();
        }

        public void HideButton()
        {
            this.Hide();
            _isVisible = false;
            _parentDialogHandle = IntPtr.Zero;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            // 失去焦点时隐藏
            // this.Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 可以添加自定义绘制逻辑
        }
    }
}