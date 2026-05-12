using System;
using System.IO;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Business
{
    public class FileMetaManager
    {
        private ZipExtraFieldManager zipExtraFieldManager;
        private FileMetaFactory fileMetaFactory;

        public FileMetaManager()
        {
            zipExtraFieldManager = new ZipExtraFieldManager();
            fileMetaFactory = FileMetaFactory.Instance;
        }

        public string ReadPasswordFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return null;
            }

            string extension = Path.GetExtension(filePath).ToLower();
            if (!IsSupportedFormat(extension))
            {
                Logger.Warning($"不支持的文件格式: {extension}");
                return null;
            }

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    if (zipExtraFieldManager.ReadMetadataFromFileEnd(filePath, 1, out byte type, out string content))
                    {
                        if (type == 1)
                        {
                            Logger.Info($"从 {filePath} 的ZIP尾部读取到密码: {content}");
                            return content;
                        }
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取密码失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            Logger.Error($"多次尝试后仍无法读取 {filePath} 的密码");
            return null;
        }

        public string ReadKeyVersionFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return null;
            }

            string extension = Path.GetExtension(filePath).ToLower();
            if (!IsSupportedFormat(extension))
            {
                Logger.Warning($"不支持的文件格式: {extension}");
                return null;
            }

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    if (zipExtraFieldManager.ReadMetadataFromFileEnd(filePath, 3, out byte type, out string content))
                    {
                        if (type == 3)
                        {
                            Logger.Info($"从 {filePath} 的ZIP尾部读取到keyVersion: {content}");
                            return content;
                        }
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取keyVersion失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            Logger.Error($"多次尝试后仍无法读取 {filePath} 的keyVersion");
            return null;
        }

        public bool WriteKeyVersionToFile(string filePath, string keyVersion)
        {
            if (!File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return false;
            }

            string extension = Path.GetExtension(filePath).ToLower();
            if (!IsSupportedFormat(extension))
            {
                Logger.Warning($"不支持的文件格式: {extension}");
                return false;
            }

            Logger.Info($"准备写入keyVersion到 {filePath}，keyVersion: {keyVersion}");

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    byte[] metadataBlock = zipExtraFieldManager.BuildMetadataBlock(3, keyVersion);
                    
                    if (zipExtraFieldManager.AppendMetadataToFileEnd(filePath, metadataBlock))
                    {
                        var fileMeta = fileMetaFactory.GetFileMeta(filePath);
                        if (fileMeta != null)
                        {
                            fileMeta.CurrentKeyVersion = keyVersion;
                        }
                        return true;
                    }
                    else
                    {
                        throw new Exception("无法写入ZIP keyVersion元数据");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"写入keyVersion失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        Logger.Debug($"重试写入keyVersion，剩余次数: {retryCount}");
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            Logger.Error($"多次尝试后仍无法写入keyVersion到 {filePath}");
            return false;
        }

        public string ReadUidFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return null;
            }

            string extension = Path.GetExtension(filePath).ToLower();
            if (!IsSupportedFormat(extension))
            {
                Logger.Warning($"不支持的文件格式: {extension}");
                return null;
            }

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    if (zipExtraFieldManager.ReadMetadataFromFileEnd(filePath, 2, out byte type, out string content))
                    {
                        if (type == 2)
                        {
                            Logger.Info($"从 {filePath} 的ZIP尾部读取到UID: {content}");
                            var fileMeta = fileMetaFactory.GetFileMeta(filePath);
                            if (fileMeta != null)
                            {
                                fileMeta.Uid = content;
                            }
                            return content;
                        }
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取UID失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            Logger.Error($"多次尝试后仍无法读取 {filePath} 的UID");
            return null;
        }

        public bool WritePasswordToFile(string filePath, string password)
        {
            if (!File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return false;
            }

            string extension = Path.GetExtension(filePath).ToLower();
            if (!IsSupportedFormat(extension))
            {
                Logger.Warning($"不支持的文件格式: {extension}");
                return false;
            }

            Logger.Info($"准备写入密码到 {filePath}，密码: {password}");

            string encryptedPassword = EncryptPasswordByPublicKey(password);
            if (string.IsNullOrEmpty(encryptedPassword))
            {
                Logger.Error($"密码加密失败，无法写入文件");
                return false;
            }

            Logger.Info($"密码加密成功，加密后的密码: {encryptedPassword}");

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    byte[] metadataBlock = zipExtraFieldManager.BuildMetadataBlock(1, encryptedPassword);
                    
                    if (zipExtraFieldManager.AppendMetadataToFileEnd(filePath, metadataBlock))
                    {
                        var fileMeta = fileMetaFactory.GetFileMeta(filePath);
                        fileMeta.CurrentPassword = password;
                        fileMeta.ClearPendingPasswords();
                        return true;
                    }
                    else
                    {
                        throw new Exception("无法写入ZIP元数据");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"写入密码失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        Logger.Debug($"重试写入密码，剩余次数: {retryCount}");
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            Logger.Error($"多次尝试后仍无法写入密码到 {filePath}");
            return false;
        }

        private string EncryptPasswordByPublicKey(string password)
        {
            try
            {
                string publicKeyBase64 = GlobalState.Instance.PublicKey;
                if (string.IsNullOrEmpty(publicKeyBase64))
                {
                    Logger.Error($"公钥为空，无法加密密码");
                    return null;
                }

                byte[] publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
                byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

                using (var ecdh = System.Security.Cryptography.ECDiffieHellman.Create())
                {
                    ecdh.GenerateKey(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);

                    using (var otherEcdh = System.Security.Cryptography.ECDiffieHellman.Create())
                    {
                        otherEcdh.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
                        
                        byte[] aesKeyBytes = ecdh.DeriveKeyFromHash(otherEcdh.PublicKey, 
                            System.Security.Cryptography.HashAlgorithmName.SHA256, null, null);

                        using (var aes = System.Security.Cryptography.Aes.Create())
                        {
                            aes.Key = aesKeyBytes;
                            aes.GenerateIV();
                            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

                            using (var encryptor = aes.CreateEncryptor())
                            {
                                byte[] encryptedPassword = encryptor.TransformFinalBlock(passwordBytes, 0, passwordBytes.Length);

                                byte[] tempPublicKeyBytes = ecdh.ExportSubjectPublicKeyInfo();
                                
                                int resultLength = 4 + tempPublicKeyBytes.Length + 16 + encryptedPassword.Length;
                                byte[] result = new byte[resultLength];

                                result[0] = (byte)((tempPublicKeyBytes.Length >> 24) & 0xFF);
                                result[1] = (byte)((tempPublicKeyBytes.Length >> 16) & 0xFF);
                                result[2] = (byte)((tempPublicKeyBytes.Length >> 8) & 0xFF);
                                result[3] = (byte)(tempPublicKeyBytes.Length & 0xFF);

                                Buffer.BlockCopy(tempPublicKeyBytes, 0, result, 4, tempPublicKeyBytes.Length);

                                Buffer.BlockCopy(aes.IV, 0, result, 4 + tempPublicKeyBytes.Length, 16);

                                Buffer.BlockCopy(encryptedPassword, 0, result, 4 + tempPublicKeyBytes.Length + 16, encryptedPassword.Length);

                                Logger.Info($"密码使用本地公钥加密成功");
                                return Convert.ToBase64String(result);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"密码加密失败: {ex.Message}");
                return null;
            }
        }

        public bool WriteUidToFile(string filePath, string uid)
        {
            if (!File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return false;
            }

            string extension = Path.GetExtension(filePath).ToLower();
            if (!IsSupportedFormat(extension))
            {
                Logger.Warning($"不支持的文件格式: {extension}");
                return false;
            }

            Logger.Info($"准备写入UID到 {filePath}，UID: {uid}");

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    byte[] metadataBlock = zipExtraFieldManager.BuildMetadataBlock(2, uid);
                    
                    if (zipExtraFieldManager.AppendMetadataToFileEnd(filePath, metadataBlock))
                    {
                        var fileMeta = fileMetaFactory.GetFileMeta(filePath);
                        fileMeta.Uid = uid;
                        return true;
                    }
                    else
                    {
                        throw new Exception("无法写入ZIP UID元数据");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"写入UID失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        Logger.Debug($"重试写入UID，剩余次数: {retryCount}");
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            Logger.Error($"多次尝试后仍无法写入UID到 {filePath}");
            return false;
        }

        public bool WriteMetaDataToFile(string filePath)
        {
            var fileMeta = fileMetaFactory.GetFileMeta(filePath);
            if (fileMeta == null)
            {
                Logger.Error($"未找到文件 {filePath} 的元数据");
                return false;
            }

            fileMetaFactory.SetPluginOperation(true);

            string password = fileMetaFactory.GetWritePassword(filePath);
            bool hasPassword = !string.IsNullOrEmpty(password);
            
            if (hasPassword)
            {
                if (!WritePasswordToFile(filePath, password))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(fileMeta.Uid))
            {
                if (!WriteUidToFile(filePath, fileMeta.Uid))
                {
                    return false;
                }
            }
            else
            {
                string newUid = GenerateUid();
                if (!WriteUidToFile(filePath, newUid))
                {
                    return false;
                }
            }

            // 只有在有密码的情况下才写入keyVersion
            if (hasPassword)
            {
                string keyVersion = GlobalState.Instance.KeyVersion;
                if (!string.IsNullOrEmpty(keyVersion))
                {
                    if (!WriteKeyVersionToFile(filePath, keyVersion))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool HasPasswordMetadata(string filePath)
        {
            return !string.IsNullOrEmpty(ReadPasswordFromFile(filePath));
        }

        private bool IsSupportedFormat(string extension)
        {
            string[] supportedFormats = { ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt" };
            foreach (string format in supportedFormats)
            {
                if (extension == format)
                {
                    return true;
                }
            }
            return false;
        }

        private string GenerateUid()
        {
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            string uuid = Guid.NewGuid().ToString();
            return $"{timestamp}_{uuid}";
        }

        public string GetDocumentUid(string filePath)
        {
            Logger.Info($"开始获取文档UID: {filePath}");
            string uid = ReadUidFromFile(filePath);
            if (!string.IsNullOrEmpty(uid))
            {
                Logger.Info($"从元数据中读取到UID: {uid}");
                return uid;
            }

            string newUid = GenerateUid();
            Logger.Info($"生成新的UID: {newUid}");
            return newUid;
        }

        public bool SaveDocumentUid(string filePath)
        {
            string uid = GetDocumentUid(filePath);
            if (!string.IsNullOrEmpty(uid))
            {
                bool success = WriteUidToFile(filePath, uid);
                if (success)
                {
                    Logger.Info($"UID已成功保存到 {filePath} 的元数据中");
                }
                return success;
            }
            return false;
        }
    }
}