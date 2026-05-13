using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WpsPasswordManager.Business;
using WpsPasswordManager.Services.Request;
using WpsPasswordManager.Services.Routing;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Services.Report
{
    public class PasswordReportService
    {
        private static PasswordReportService _instance;
        private static readonly object _lock = new object();

        private PasswordReportService() { }

        public static PasswordReportService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new PasswordReportService();
                        }
                    }
                }
                return _instance;
            }
        }

        public async Task<bool> ReportSaveLog(FileMeta fileMeta)
        {
            if (fileMeta == null)
            {
                Logger.Error("FileMeta为空，无法上报");
                return false;
            }

            if (!GlobalState.Instance.IsLoggedIn)
            {
                Logger.Info("用户未登录，跳过密码上报");
                return false;
            }

            try
            {
                Logger.Info($"开始上报文档密码保存记录: {fileMeta.FilePath}");

                string publicKey = GlobalState.Instance.PublicKey;
                string keyVersion = GlobalState.Instance.KeyVersion;

                string encryptedBeforePassword = null;
                string encryptedAfterPassword = null;
                List<string> encryptedPossiblePasswords = new List<string>();

                if (!string.IsNullOrEmpty(fileMeta.CurrentPassword))
                {
                    Logger.Info($"准备加密旧密码 (CurrentPassword)，密码长度: {fileMeta.CurrentPassword.Length}");
                    encryptedBeforePassword = CryptoUtils.EncryptPasswordByPublicKey(fileMeta.CurrentPassword, publicKey);
                    if (encryptedBeforePassword == null)
                    {
                        Logger.Warning("旧密码加密失败，继续处理其他密码");
                    }
                    else
                    {
                        Logger.Info($"旧密码加密成功，加密后长度: {encryptedBeforePassword.Length}");
                    }
                }
                else
                {
                    Logger.Info("旧密码 (CurrentPassword) 为空，跳过");
                }

                if (fileMeta.HasPendingPasswords())
                {
                    Logger.Info($"准备加密待定密码 (PendingPasswordList)，数量: {fileMeta.PendingPasswordList.Count}");
                    int index = 0;
                    foreach (string password in fileMeta.PendingPasswordList)
                    {
                        Logger.Info($"加密待定密码 [{index}]，密码长度: {password.Length}");
                        string encryptedPassword = CryptoUtils.EncryptPasswordByPublicKey(password, publicKey);
                        if (encryptedPassword != null)
                        {
                            encryptedPossiblePasswords.Add(encryptedPassword);
                            Logger.Info($"待定密码 [{index}] 加密成功，加密后长度: {encryptedPassword.Length}");
                        }
                        else
                        {
                            Logger.Warning($"待定密码 [{index}] 加密失败");
                        }
                        index++;
                    }

                    if (encryptedPossiblePasswords.Count > 0)
                    {
                        encryptedAfterPassword = encryptedPossiblePasswords[0];
                        Logger.Info($"待定密码集合加密完成，有效密码数量: {encryptedPossiblePasswords.Count}");
                    }
                }
                else
                {
                    Logger.Info("待定密码 (PendingPasswordList) 为空，跳过");
                }

                if (string.IsNullOrEmpty(fileMeta.Uid))
                {
                    Logger.Error("文档UID为空，无法上报");
                    return false;
                }

                Logger.Info($"构造上报请求数据: docId={fileMeta.Uid}, path={fileMeta.FilePath}, keyVersion={fileMeta.CurrentKeyVersion}");
                Logger.Info($"  - beforePassword: {(encryptedBeforePassword != null ? "已加密" : "空")}");
                Logger.Info($"  - afterPassword: {(encryptedAfterPassword != null ? "已加密" : "空")}");
                Logger.Info($"  - pendingPassword: {(encryptedPossiblePasswords.Count > 0 ? encryptedPossiblePasswords.Count + "个已加密" : "空")}");

                var requestData = new
                {
                    docId = fileMeta.Uid,
                    path = fileMeta.FilePath,
                    keyVersion = fileMeta.CurrentKeyVersion,
                    beforePassword = encryptedBeforePassword,
                    afterPassword = encryptedAfterPassword,
                    pendingPassword = encryptedPossiblePasswords.Count > 0 ? encryptedPossiblePasswords.ToArray() : null,
                    platform = "win"
                };

                string jsonRequest = JsonSerializer.Serialize(requestData, new JsonSerializerOptions 
                { 
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                Logger.Info($"上报请求JSON: {jsonRequest}");

                var httpRequestService = RequestFactory.GetHttpRequestService();
                var response = await httpRequestService.PostAsync<object>(ApiRoutes.DocSaveLog, requestData, GlobalState.Instance.Token);

                if (response != null && response.status == 200)
                {
                    Logger.Info("密码保存记录上报成功");
                    return true;
                }
                else
                {
                    Logger.Error($"密码保存记录上报失败: {response?.message ?? "未知错误"}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"密码保存记录上报异常: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReportSaveLog(string filePath)
        {
            var fileMeta = FileMetaFactory.Instance.GetFileMeta(filePath);
            return await ReportSaveLog(fileMeta);
        }

        public async Task<bool> ReportSaveLogWithPasswords(FileMeta fileMeta)
        {
            if (string.IsNullOrEmpty(fileMeta.Uid))
            {
                Logger.Error("文档UID为空，无法上报");
                return false;
            }

            if (!GlobalState.Instance.IsLoggedIn)
            {
                Logger.Info("用户未登录，跳过密码上报");
                return false;
            }

            try
            {
                Logger.Info($"开始上报文档密码保存记录: {fileMeta.FilePath}");

                string publicKey = GlobalState.Instance.PublicKey;

                string encryptedBeforePassword = null;
                string encryptedAfterPassword = null;
                List<string> encryptedPossiblePasswords = new List<string>();

                if (!string.IsNullOrEmpty(fileMeta.CurrentPassword))
                {
                    Logger.Info($"准备加密旧密码 (beforePassword)，密码长度: {fileMeta.CurrentPassword.Length}");
                    encryptedBeforePassword = CryptoUtils.EncryptPasswordByPublicKey(fileMeta.CurrentPassword, publicKey);
                    if (encryptedBeforePassword == null)
                    {
                        Logger.Warning("旧密码加密失败，继续处理其他密码");
                    }
                    else
                    {
                        Logger.Info($"旧密码加密成功，加密后长度: {encryptedBeforePassword.Length}");
                    }
                }
                else
                {
                    Logger.Info("旧密码 (beforePassword) 为空，跳过");
                }

                if (fileMeta.PendingPasswordList != null && fileMeta.PendingPasswordList.Count > 0)
                {
                    Logger.Info($"准备加密待定密码 (pendingPasswords)，数量: {fileMeta.PendingPasswordList.Count}");
                    int index = 0;
                    foreach (string password in fileMeta.PendingPasswordList)
                    {
                        Logger.Info($"加密待定密码 [{index}]，密码长度: {password?.Length ?? 0}");
                        if (!string.IsNullOrEmpty(password))
                        {
                            string encryptedPassword = CryptoUtils.EncryptPasswordByPublicKey(password, publicKey);
                            if (encryptedPassword != null)
                            {
                                encryptedPossiblePasswords.Add(encryptedPassword);
                                Logger.Info($"待定密码 [{index}] 加密成功，加密后长度: {encryptedPassword.Length}");
                            }
                            else
                            {
                                Logger.Warning($"待定密码 [{index}] 加密失败");
                            }
                        }
                        index++;
                    }

                    if (encryptedPossiblePasswords.Count > 0)
                    {
                        encryptedAfterPassword = encryptedPossiblePasswords[0];
                        Logger.Info($"待定密码集合加密完成，有效密码数量: {encryptedPossiblePasswords.Count}");
                    }
                }
                else
                {
                    Logger.Info($"待定密码 (pendingPasswords) 为空，跳过");
                }
                Logger.Info($"构造上报请求数据: docId={fileMeta.Uid}, path={fileMeta.FilePath}, keyVersion={fileMeta.CurrentKeyVersion}"); 
                Logger.Info($"  - beforePassword: {(encryptedBeforePassword != null ? "已加密" : "空")}");
                Logger.Info($"  - afterPassword: {(encryptedAfterPassword != null ? "已加密" : "空")}");
                Logger.Info($"  - pendingPassword: {(encryptedPossiblePasswords.Count > 0 ? encryptedPossiblePasswords.Count + "个已加密" : "空")}");

                var requestData = new
                {
                    docId = fileMeta.Uid,
                    path = fileMeta.FilePath,
                    keyVersion = fileMeta.CurrentKeyVersion,
                    beforePassword = encryptedBeforePassword,
                    afterPassword = encryptedAfterPassword,
                    possiblePassword = encryptedPossiblePasswords.Count > 0 ? encryptedPossiblePasswords.ToArray() : null,
                    platform = "win"
                };

                string jsonRequest = JsonSerializer.Serialize(requestData, new JsonSerializerOptions 
                { 
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                Logger.Info($"上报请求JSON: {jsonRequest}");

                var httpRequestService = RequestFactory.GetHttpRequestService();
                var response = await httpRequestService.PostAsync<object>(ApiRoutes.DocSaveLog, requestData, GlobalState.Instance.Token);

                if (response != null && response.status == 200)
                {
                    Logger.Info("密码保存记录上报成功");
                    return true;
                }
                else
                {
                    Logger.Error($"密码保存记录上报失败: {response?.message ?? "未知错误"}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"密码保存记录上报异常: {ex.Message}");
                return false;
            }
        }
    }
}