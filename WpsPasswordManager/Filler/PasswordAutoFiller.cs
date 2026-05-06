using System;
using System.Threading;
using WpsPasswordManager.Locator;
using WpsPasswordManager.Monitor;
using WpsPasswordManager.Simulator;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Filler
{
    public class PasswordAutoFiller
    {
        private readonly QtWindowLocator _windowLocator;
        private readonly WpsMonitor _monitor;
        private readonly InputSimulator _inputSimulator;

        private const int MaxRetries = 3;
        private const int RetryDelayMs = 200;

        public PasswordAutoFiller()
        {
            _windowLocator = new QtWindowLocator();
            _monitor = new WpsMonitor();
            _inputSimulator = new InputSimulator();
        }

        public bool FillDecryptPassword(string password)
        {
            Logger.Info("开始执行解密密码填充");

            for (int retry = 0; retry < MaxRetries; retry++)
            {
                IntPtr dialogHandle = _windowLocator.FindPasswordDialog();
                if (dialogHandle == IntPtr.Zero)
                {
                    Logger.Warning($"第 {retry + 1}/{MaxRetries} 次尝试未找到密码对话框");
                    Thread.Sleep(RetryDelayMs);
                    continue;
                }

                if (!_windowLocator.IsDecryptDialog(dialogHandle))
                {
                    Logger.Warning($"第 {retry + 1}/{MaxRetries} 次尝试找到的不是解密对话框");
                    Thread.Sleep(RetryDelayMs);
                    continue;
                }

                if (TryFillPasswordByKeyboard(dialogHandle, password, false))
                {
                    Logger.Info("解密密码填充成功");
                    return true;
                }

                Thread.Sleep(RetryDelayMs);
            }

            Logger.Error($"经过 {MaxRetries} 次尝试仍无法完成解密密码填充");
            return false;
        }

        public bool FillEncryptPassword(string password)
        {
            Logger.Info("开始执行加密密码填充");

            for (int retry = 0; retry < MaxRetries; retry++)
            {
                IntPtr dialogHandle = _windowLocator.FindPasswordDialog();
                if (dialogHandle == IntPtr.Zero)
                {
                    Logger.Warning($"第 {retry + 1}/{MaxRetries} 次尝试未找到密码对话框");
                    Thread.Sleep(RetryDelayMs);
                    continue;
                }

                if (!_windowLocator.IsEncryptDialog(dialogHandle))
                {
                    Logger.Warning($"第 {retry + 1}/{MaxRetries} 次尝试找到的不是加密对话框");
                    Thread.Sleep(RetryDelayMs);
                    continue;
                }

                if (TryFillPasswordByKeyboard(dialogHandle, password, true))
                {
                    Logger.Info("加密密码填充成功");
                    return true;
                }

                Thread.Sleep(RetryDelayMs);
            }

            Logger.Error($"经过 {MaxRetries} 次尝试仍无法完成加密密码填充");
            return false;
        }

        private bool TryFillPasswordByKeyboard(IntPtr dialogHandle, string password, bool isEncrypt)
        {
            try
            {
                Logger.Debug("尝试通过键盘导航方式填充密码");

                InputSimulator.SetForegroundWindow(dialogHandle);
                Thread.Sleep(300);

                if (isEncrypt)
                {
                    return TryFillEncryptPasswordByControl(dialogHandle, password);
                }
                else
                {
                    return TryFillDecryptPasswordByControl(dialogHandle, password);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"键盘导航填充失败: {ex.Message}");
                return false;
            }
        }

        private bool TryFillEncryptPasswordByControl(IntPtr dialogHandle, string password)
        {
            try
            {
                Logger.Debug("处理密码加密窗口填充 - 使用UI Automation");
                return FillPasswordByUIAutomation(dialogHandle, password, "打开文件密码", "再次输入密码");
            }
            catch (Exception ex)
            {
                Logger.Error($"加密密码填充失败: {ex.Message}");
                return false;
            }
        }
        
        private bool FillPasswordByUIAutomation(IntPtr dialogHandle, string password, string firstFieldName, string secondFieldName)
        {
            try
            {
                Logger.Debug($"尝试使用UI Automation填充密码到 '{firstFieldName}' 和 '{secondFieldName}'");
                
                System.Reflection.Assembly uiaClient = null;
                System.Reflection.Assembly uiaTypes = null;
                
                try
                {
                    uiaClient = System.Reflection.Assembly.Load("UIAutomationClient, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    Logger.Debug("成功加载UIAutomationClient程序集");
                }
                catch
                {
                    try
                    {
                        uiaClient = System.Reflection.Assembly.Load("UIAutomationClient");
                        Logger.Debug("成功加载UIAutomationClient程序集（无版本）");
                    }
                    catch
                    {
                        Logger.Warning("无法加载UIAutomationClient程序集");
                        return false;
                    }
                }
                
                try
                {
                    uiaTypes = System.Reflection.Assembly.Load("UIAutomationTypes, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    Logger.Debug("成功加载UIAutomationTypes程序集");
                }
                catch
                {
                    try
                    {
                        uiaTypes = System.Reflection.Assembly.Load("UIAutomationTypes");
                        Logger.Debug("成功加载UIAutomationTypes程序集（无版本）");
                    }
                    catch
                    {
                        Logger.Warning("无法加载UIAutomationTypes程序集");
                        return false;
                    }
                }
                
                Type automationElementType = uiaClient.GetType("System.Windows.Automation.AutomationElement");
                if (automationElementType == null)
                {
                    Logger.Warning("无法获取AutomationElement类型");
                    return false;
                }
                
                object dialogElement = automationElementType.GetMethod("FromHandle").Invoke(null, new object[] { dialogHandle });
                if (dialogElement == null)
                {
                    Logger.Warning("无法获取对话框的AutomationElement");
                    return false;
                }
                
                Type treeScopeType = uiaTypes.GetType("System.Windows.Automation.TreeScope");
                if (treeScopeType == null)
                {
                    Logger.Warning("无法获取TreeScope类型");
                    return false;
                }
                object treeScopeDescendants = System.Enum.Parse(treeScopeType, "Descendants");
                
                Type controlTypeType = uiaTypes.GetType("System.Windows.Automation.ControlType");
                if (controlTypeType == null)
                {
                    Logger.Warning("无法获取ControlType类型");
                    return false;
                }
                
                System.Reflection.FieldInfo editField = controlTypeType.GetField("Edit");
                if (editField == null)
                {
                    Logger.Warning("无法获取Edit字段");
                    return false;
                }
                object editControlType = editField.GetValue(null);
                
                Type propertyConditionType = uiaClient.GetType("System.Windows.Automation.PropertyCondition");
                if (propertyConditionType == null)
                {
                    propertyConditionType = uiaTypes.GetType("System.Windows.Automation.PropertyCondition");
                    if (propertyConditionType == null)
                    {
                        Logger.Warning("无法获取PropertyCondition类型");
                        return false;
                    }
                }
                
                object controlTypeProperty = null;
                System.Reflection.PropertyInfo controlTypePropertyInfo = automationElementType.GetProperty("ControlTypeProperty");
                if (controlTypePropertyInfo != null)
                {
                    controlTypeProperty = controlTypePropertyInfo.GetValue(null);
                }
                else
                {
                    System.Reflection.FieldInfo controlTypePropertyField = automationElementType.GetField("ControlTypeProperty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (controlTypePropertyField != null)
                    {
                        controlTypeProperty = controlTypePropertyField.GetValue(null);
                    }
                    else
                    {
                        Logger.Warning("无法获取ControlTypeProperty");
                        return false;
                    }
                }
                
                object editCondition = Activator.CreateInstance(propertyConditionType, new object[] { controlTypeProperty, editControlType });
                if (editCondition == null)
                {
                    Logger.Warning("无法创建编辑控件条件");
                    return false;
                }
                
                System.Reflection.MethodInfo findAllMethod = automationElementType.GetMethod("FindAll");
                if (findAllMethod == null)
                {
                    Logger.Warning("无法获取FindAll方法");
                    return false;
                }
                
                object editElements = findAllMethod.Invoke(dialogElement, new object[] { treeScopeDescendants, editCondition });
                if (editElements == null)
                {
                    Logger.Warning("无法找到编辑控件");
                    return false;
                }
                
                System.Reflection.PropertyInfo countProperty = editElements.GetType().GetProperty("Count");
                if (countProperty == null)
                {
                    Logger.Warning("无法获取编辑控件数量");
                    return false;
                }
                
                int count = (int)countProperty.GetValue(editElements);
                Logger.Debug($"UI Automation找到 {count} 个编辑控件");
                
                System.Reflection.MethodInfo getItemMethod = editElements.GetType().GetMethod("get_Item");
                if (getItemMethod == null)
                {
                    Logger.Warning("无法获取get_Item方法");
                    return false;
                }
                
                object firstField = null;
                object secondField = null;
                
                for (int i = 0; i < count; i++)
                {
                    object editElement = getItemMethod.Invoke(editElements, new object[] { i });
                    if (editElement == null)
                        continue;
                    
                    System.Reflection.PropertyInfo currentProperty = automationElementType.GetProperty("Current");
                    if (currentProperty != null)
                    {
                        object current = currentProperty.GetValue(editElement);
                        if (current != null)
                        {
                            System.Reflection.PropertyInfo nameProperty = current.GetType().GetProperty("Name");
                            if (nameProperty != null)
                            {
                                string name = (string)nameProperty.GetValue(current);
                                Logger.Debug($"UI Automation编辑控件 #{i} 名称: {name}");
                                
                                if (name != null)
                                {
                                    if (name.Contains(firstFieldName) && firstField == null)
                                    {
                                        firstField = editElement;
                                        Logger.Info($"UI Automation找到第一个目标输入框: {name}");
                                    }
                                    else if (name.Contains(secondFieldName) && secondField == null)
                                    {
                                        secondField = editElement;
                                        Logger.Info($"UI Automation找到第二个目标输入框: {name}");
                                    }
                                }
                            }
                        }
                    }
                }
                
                if (firstField == null)
                {
                    Logger.Warning($"UI Automation未找到 '{firstFieldName}' 输入框");
                    return false;
                }
                
                Type valuePatternType = uiaClient.GetType("System.Windows.Automation.ValuePattern");
                if (valuePatternType == null)
                {
                    Logger.Warning("无法获取ValuePattern类型");
                    return false;
                }
                
                object valuePatternProperty = null;
                System.Reflection.PropertyInfo valuePatternPropertyInfo = valuePatternType.GetProperty("Pattern");
                if (valuePatternPropertyInfo != null)
                {
                    valuePatternProperty = valuePatternPropertyInfo.GetValue(null);
                }
                else
                {
                    System.Reflection.FieldInfo valuePatternField = valuePatternType.GetField("Pattern", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (valuePatternField != null)
                    {
                        valuePatternProperty = valuePatternField.GetValue(null);
                    }
                    else
                    {
                        Logger.Warning("无法获取ValuePattern.Pattern");
                        return false;
                    }
                }
                
                System.Reflection.MethodInfo setFocusMethod = automationElementType.GetMethod("SetFocus");
                if (setFocusMethod == null)
                {
                    Logger.Warning("无法获取SetFocus方法");
                    return false;
                }
                
                setFocusMethod.Invoke(firstField, null);
                Thread.Sleep(100);
                
                System.Reflection.MethodInfo tryGetCurrentPatternMethod = automationElementType.GetMethod("TryGetCurrentPattern");
                if (tryGetCurrentPatternMethod == null)
                {
                    Logger.Warning("无法获取TryGetCurrentPattern方法");
                    return false;
                }
                
                object[] firstPatternParams = new object[2];
                firstPatternParams[0] = valuePatternProperty;
                firstPatternParams[1] = default(object);
                bool gotFirstPattern = (bool)tryGetCurrentPatternMethod.Invoke(firstField, firstPatternParams);
                
                if (!gotFirstPattern)
                {
                    Logger.Warning("无法获取第一个输入框的ValuePattern");
                    return false;
                }
                
                object firstValuePattern = firstPatternParams[1];
                if (firstValuePattern == null)
                {
                    Logger.Warning("第一个输入框的ValuePattern为空");
                    return false;
                }
                
                System.Reflection.MethodInfo setValueMethod = firstValuePattern.GetType().GetMethod("SetValue", new Type[] { typeof(string) });
                if (setValueMethod == null)
                {
                    Logger.Warning("无法获取SetValue方法");
                    return false;
                }
                
                setValueMethod.Invoke(firstValuePattern, new object[] { password });
                Thread.Sleep(150);
                Logger.Info($"UI Automation已填充第一个密码输入框");
                
                if (secondField != null)
                {
                    setFocusMethod.Invoke(secondField, null);
                    Thread.Sleep(100);
                    
                    object[] secondPatternParams = new object[2];
                    secondPatternParams[0] = valuePatternProperty;
                    secondPatternParams[1] = default(object);
                    bool gotSecondPattern = (bool)tryGetCurrentPatternMethod.Invoke(secondField, secondPatternParams);
                    
                    if (!gotSecondPattern)
                    {
                        Logger.Warning("无法获取第二个输入框的ValuePattern");
                        return false;
                    }
                    
                    object secondValuePattern = secondPatternParams[1];
                    if (secondValuePattern == null)
                    {
                        Logger.Warning("第二个输入框的ValuePattern为空");
                        return false;
                    }
                    
                    System.Reflection.MethodInfo setValueMethod2 = secondValuePattern.GetType().GetMethod("SetValue", new Type[] { typeof(string) });
                    if (setValueMethod2 == null)
                    {
                        Logger.Warning("无法获取第二个输入框的SetValue方法");
                        return false;
                    }
                    
                    setValueMethod2.Invoke(secondValuePattern, new object[] { password });
                    Thread.Sleep(150);
                    Logger.Info($"UI Automation已填充第二个密码输入框");
                }
                
                Logger.Info("UI Automation密码填充成功");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"UI Automation填充密码失败: {ex.Message}");
                return false;
            }
        }

        private bool TryFillDecryptPasswordByControl(IntPtr dialogHandle, string password)
        {
            try
            {
                Logger.Debug("处理解密窗口填充 - 使用控件定位");

                IntPtr passwordEdit = _monitor.FindPasswordEdit(dialogHandle);
                if (passwordEdit != IntPtr.Zero)
                {
                    Logger.Info($"找到密码输入框: {passwordEdit}");

                    _inputSimulator.SimulateMouseClick(passwordEdit);
                    Thread.Sleep(100);

                    _inputSimulator.ClearInput(passwordEdit);
                    Thread.Sleep(50);

                    _inputSimulator.SimulatePasswordInput(passwordEdit, password);
                    Thread.Sleep(150);

                    _inputSimulator.SimulateEnterKey();
                    Thread.Sleep(200);

                    Logger.Info("解密密码填充成功（使用控件定位）");
                    return true;
                }
                else
                {
                    Logger.Warning("未找到密码输入框，回退到Tab键导航");
                    return TryFillDecryptPasswordByTab(dialogHandle, password);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"控件定位填充失败，回退到Tab键导航: {ex.Message}");
                return TryFillDecryptPasswordByTab(dialogHandle, password);
            }
        }

        private bool TryFillDecryptPasswordByTab(IntPtr dialogHandle, string password)
        {
            try
            {
                Logger.Debug("处理解密窗口填充 - 使用Tab键导航");

                _inputSimulator.SimulateMouseClick(dialogHandle);
                Thread.Sleep(200);

                for (int i = 0; i < 3; i++)
                {
                    _inputSimulator.SimulateTabKey();
                    Thread.Sleep(100);
                }

                Thread.Sleep(100);

                _inputSimulator.SimulateTextInput(password);
                Thread.Sleep(200);

                _inputSimulator.SimulateEnterKey();
                Thread.Sleep(300);

                Logger.Info("解密密码填充成功（使用Tab键导航）");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Tab键导航填充失败: {ex.Message}");
                return false;
            }
        }

        public bool IsPasswordDialogPresent()
        {
            return _windowLocator.FindPasswordDialog() != IntPtr.Zero;
        }

        public bool IsDecryptDialogPresent()
        {
            IntPtr dialog = _windowLocator.FindPasswordDialog();
            return dialog != IntPtr.Zero && _windowLocator.IsDecryptDialog(dialog);
        }

        public bool IsEncryptDialogPresent()
        {
            IntPtr dialog = _windowLocator.FindPasswordDialog();
            return dialog != IntPtr.Zero && _windowLocator.IsEncryptDialog(dialog);
        }

        public void LogDialogInfo()
        {
            IntPtr dialog = _windowLocator.FindPasswordDialog();
            if (dialog != IntPtr.Zero)
            {
                Logger.Debug($"找到对话框: {dialog}, 标题: {_windowLocator.GetWindowTitle(dialog)}");
            }
            else
            {
                Logger.Debug("未找到密码对话框");
            }
        }
    }
}