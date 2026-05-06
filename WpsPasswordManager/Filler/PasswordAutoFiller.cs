using System;
using System.Threading;
using WpsPasswordManager.Locator;
using WpsPasswordManager.Simulator;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Filler
{
    public class PasswordAutoFiller
    {
        private readonly QtWindowLocator _windowLocator;
        private readonly InputSimulator _inputSimulator;

        private const int MaxRetries = 3;
        private const int RetryDelayMs = 200;

        public PasswordAutoFiller()
        {
            _windowLocator = new QtWindowLocator();
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

                if (isEncrypt)
                {
                    _inputSimulator.SimulateTabKey();
                    Thread.Sleep(100);
                    _inputSimulator.SimulateTextInput(password);
                    Thread.Sleep(200);
                }

                _inputSimulator.SimulateEnterKey();
                Thread.Sleep(300);

                Logger.Info("键盘导航填充成功");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"键盘导航填充失败: {ex.Message}");
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