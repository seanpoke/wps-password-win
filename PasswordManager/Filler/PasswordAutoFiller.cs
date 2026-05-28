using System;
using System.Threading;
using PasswordManager.Locator;
using PasswordManager.Monitor;
using PasswordManager.Utils;

namespace PasswordManager.Filler
{
    public class PasswordAutoFiller
    {
        private readonly QtWindowLocator _windowLocator;
        private readonly WpsMonitor _monitor;

        public PasswordAutoFiller()
        {
            _windowLocator = new QtWindowLocator();
            _monitor = new WpsMonitor();
        }

        public bool FillDecryptPassword(string password)
        {
            Logger.Info("开始执行解密密码填充");

            IntPtr dialogHandle = _monitor.FindPasswordDialog();
            if (dialogHandle == IntPtr.Zero)
            {
                Logger.Warning("未找到密码对话框");
                return false;
            }

            if (!_windowLocator.IsDecryptDialog(dialogHandle))
            {
                Logger.Warning("找到的不是解密对话框");
                return false;
            }

            return FillPasswordByUIAutomation(dialogHandle, password, false);
        }

        public bool FillEncryptPassword(string password)
        {
            Logger.Info("开始执行加密密码填充");

            IntPtr dialogHandle = _monitor.FindPasswordDialog();
            if (dialogHandle == IntPtr.Zero)
            {
                Logger.Warning("未找到密码对话框");
                return false;
            }

            if (!_windowLocator.IsEncryptDialog(dialogHandle))
            {
                Logger.Warning("找到的不是加密对话框");
                return false;
            }

            return FillPasswordByUIAutomation(dialogHandle, password, true);
        }

        private bool FillPasswordByUIAutomation(IntPtr dialogHandle, string password, bool isEncrypt)
        {
            try
            {
                Logger.Debug("尝试使用UI Automation填充密码");

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
                    catch (Exception ex)
                    {
                        Logger.Warning($"无法加载UIAutomationClient程序集: {ex.Message}");
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
                    catch (Exception ex)
                    {
                        Logger.Warning($"无法加载UIAutomationTypes程序集: {ex.Message}");
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

                if (controlTypeProperty == null)
                {
                    Logger.Warning("ControlTypeProperty为空");
                    return false;
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

                string firstFieldName = isEncrypt ? "打开文件密码" : "请输入密码";
                string secondFieldName = isEncrypt ? "再次输入密码" : "";

                for (int i = 0; i < count; i++)
                {
                    object editElement = getItemMethod.Invoke(editElements, new object[] { i });
                    if (editElement == null) continue;

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
                                    else if (!string.IsNullOrEmpty(secondFieldName) && name.Contains(secondFieldName) && secondField == null)
                                    {
                                        secondField = editElement;
                                        Logger.Info($"UI Automation找到第二个目标输入框: {name}");
                                    }
                                    else if (name.Contains("Password") && firstField == null)
                                    {
                                        firstField = editElement;
                                        Logger.Info($"UI Automation通过Password关键词找到输入框: {name}");
                                    }
                                }
                            }
                        }
                    }
                }

                if (firstField == null)
                {
                    if (count > 0)
                    {
                        firstField = getItemMethod.Invoke(editElements, new object[] { 0 });
                        Logger.Info("UI Automation使用第一个编辑控件");
                    }
                    else
                    {
                        Logger.Warning("UI Automation未找到输入框");
                        return false;
                    }
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

                if (valuePatternProperty == null)
                {
                    Logger.Warning("ValuePattern.Property为空");
                    return false;
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
                firstPatternParams[1] = null;
                bool gotFirstPattern = (bool)tryGetCurrentPatternMethod.Invoke(firstField, firstPatternParams);

                if (!gotFirstPattern || firstPatternParams[1] == null)
                {
                    Logger.Warning("无法获取第一个输入框的ValuePattern");
                    return false;
                }

                object firstValuePattern = firstPatternParams[1];
                System.Reflection.MethodInfo setValueMethod = firstValuePattern.GetType().GetMethod("SetValue", new Type[] { typeof(string) });
                if (setValueMethod == null)
                {
                    Logger.Warning("无法获取SetValue方法");
                    return false;
                }

                setValueMethod.Invoke(firstValuePattern, new object[] { password });
                Thread.Sleep(150);
                Logger.Info("UI Automation已填充密码输入框");

                if (secondField != null)
                {
                    setFocusMethod.Invoke(secondField, null);
                    Thread.Sleep(100);

                    object[] secondPatternParams = new object[2];
                    secondPatternParams[0] = valuePatternProperty;
                    secondPatternParams[1] = null;
                    bool gotSecondPattern = (bool)tryGetCurrentPatternMethod.Invoke(secondField, secondPatternParams);

                    if (!gotSecondPattern || secondPatternParams[1] == null)
                    {
                        Logger.Warning("无法获取第二个输入框的ValuePattern");
                        return false;
                    }

                    object secondValuePattern = secondPatternParams[1];
                    System.Reflection.MethodInfo setValueMethod2 = secondValuePattern.GetType().GetMethod("SetValue", new Type[] { typeof(string) });
                    if (setValueMethod2 == null)
                    {
                        Logger.Warning("无法获取第二个输入框的SetValue方法");
                        return false;
                    }

                    setValueMethod2.Invoke(secondValuePattern, new object[] { password });
                    Thread.Sleep(150);
                    Logger.Info("UI Automation已填充确认密码输入框");
                }

                ClickOkButtonByUIAutomation(dialogHandle);

                Logger.Info("UI Automation密码填充成功");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"UI Automation填充密码失败: {ex.Message}");
                Logger.Error($"堆栈: {ex.StackTrace}");
                return false;
            }
        }

        private void ClickOkButtonByUIAutomation(IntPtr dialogHandle)
        {
            try
            {
                Logger.Debug("尝试通过UI Automation点击确定按钮");

                System.Reflection.Assembly uiaClient = null;
                System.Reflection.Assembly uiaTypes = null;

                try
                {
                    uiaClient = System.Reflection.Assembly.Load("UIAutomationClient, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                }
                catch
                {
                    uiaClient = System.Reflection.Assembly.Load("UIAutomationClient");
                }

                try
                {
                    uiaTypes = System.Reflection.Assembly.Load("UIAutomationTypes, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                }
                catch
                {
                    uiaTypes = System.Reflection.Assembly.Load("UIAutomationTypes");
                }

                Type automationElementType = uiaClient.GetType("System.Windows.Automation.AutomationElement");
                if (automationElementType == null)
                    return;

                object dialogElement = automationElementType.GetMethod("FromHandle").Invoke(null, new object[] { dialogHandle });
                if (dialogElement == null)
                    return;

                Type treeScopeType = uiaTypes.GetType("System.Windows.Automation.TreeScope");
                if (treeScopeType == null)
                    return;
                object treeScopeDescendants = System.Enum.Parse(treeScopeType, "Descendants");

                Type controlTypeType = uiaTypes.GetType("System.Windows.Automation.ControlType");
                if (controlTypeType == null)
                    return;

                System.Reflection.FieldInfo buttonField = controlTypeType.GetField("Button");
                if (buttonField == null)
                    return;
                object buttonControlType = buttonField.GetValue(null);

                Type propertyConditionType = uiaClient.GetType("System.Windows.Automation.PropertyCondition");
                if (propertyConditionType == null)
                    propertyConditionType = uiaTypes.GetType("System.Windows.Automation.PropertyCondition");
                if (propertyConditionType == null)
                    return;

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
                        controlTypeProperty = controlTypePropertyField.GetValue(null);
                }

                if (controlTypeProperty == null)
                    return;

                object buttonCondition = Activator.CreateInstance(propertyConditionType, new object[] { controlTypeProperty, buttonControlType });
                if (buttonCondition == null)
                    return;

                System.Reflection.MethodInfo findAllMethod = automationElementType.GetMethod("FindAll");
                if (findAllMethod == null)
                    return;

                object buttons = findAllMethod.Invoke(dialogElement, new object[] { treeScopeDescendants, buttonCondition });
                if (buttons == null)
                    return;

                System.Reflection.PropertyInfo countProperty = buttons.GetType().GetProperty("Count");
                if (countProperty == null)
                    return;

                int count = (int)countProperty.GetValue(buttons);
                Logger.Debug($"UI Automation找到 {count} 个按钮控件");

                System.Reflection.MethodInfo getItemMethod = buttons.GetType().GetMethod("get_Item");
                if (getItemMethod == null)
                    return;

                string[] okButtonTexts = { "确定", "OK", "打开" };

                for (int i = 0; i < count; i++)
                {
                    object buttonElement = getItemMethod.Invoke(buttons, new object[] { i });
                    if (buttonElement == null) continue;

                    System.Reflection.PropertyInfo currentProperty = automationElementType.GetProperty("Current");
                    if (currentProperty != null)
                    {
                        object current = currentProperty.GetValue(buttonElement);
                        if (current != null)
                        {
                            System.Reflection.PropertyInfo nameProperty = current.GetType().GetProperty("Name");
                            if (nameProperty != null)
                            {
                                string name = (string)nameProperty.GetValue(current);
                                Logger.Debug($"UI Automation按钮 #{i} 名称: {name}");

                                foreach (string okText in okButtonTexts)
                                {
                                    if (name != null && name.IndexOf(okText, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        Logger.Debug($"按钮 '{name}' 匹配成功! 准备点击...");

                                        Type invokePatternType = uiaClient.GetType("System.Windows.Automation.InvokePattern");
                                        if (invokePatternType == null)
                                            continue;

                                        object invokePatternProperty = null;
                                        System.Reflection.PropertyInfo invokePatternPropertyInfo = invokePatternType.GetProperty("Pattern");
                                        if (invokePatternPropertyInfo != null)
                                        {
                                            invokePatternProperty = invokePatternPropertyInfo.GetValue(null);
                                        }
                                        else
                                        {
                                            System.Reflection.FieldInfo invokePatternField = invokePatternType.GetField("Pattern", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                            if (invokePatternField != null)
                                                invokePatternProperty = invokePatternField.GetValue(null);
                                        }

                                        if (invokePatternProperty != null)
                                        {
                                            System.Reflection.MethodInfo tryGetCurrentPatternMethod = automationElementType.GetMethod("TryGetCurrentPattern");
                                            if (tryGetCurrentPatternMethod == null)
                                                continue;

                                            object[] patternParams = new object[2];
                                            patternParams[0] = invokePatternProperty;
                                            patternParams[1] = null;

                                            bool gotPattern = (bool)tryGetCurrentPatternMethod.Invoke(buttonElement, patternParams);

                                            if (gotPattern && patternParams[1] != null)
                                            {
                                                object invokePattern = patternParams[1];
                                                System.Reflection.MethodInfo invokeMethod = invokePattern.GetType().GetMethod("Invoke");
                                                if (invokeMethod != null)
                                                {
                                                    invokeMethod.Invoke(invokePattern, null);
                                                    Thread.Sleep(200);
                                                    Logger.Info($"UI Automation点击了 '{name}' 按钮");
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                Logger.Warning("UI Automation未找到可点击的确定按钮");
            }
            catch (Exception ex)
            {
                Logger.Error($"UI Automation点击按钮失败: {ex.Message}");
            }
        }

        public bool IsDecryptDialogPresent()
        {
            IntPtr dialog = _monitor.FindPasswordDialog();
            return dialog != IntPtr.Zero && _windowLocator.IsDecryptDialog(dialog);
        }

        public bool IsEncryptDialogPresent()
        {
            IntPtr dialog = _monitor.FindPasswordDialog();
            return dialog != IntPtr.Zero && _windowLocator.IsEncryptDialog(dialog);
        }

        public void LogDialogInfo()
        {
            IntPtr dialog = _monitor.FindPasswordDialog();
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
