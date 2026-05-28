using System;
using System.IO;
using NPOI.POIFS.FileSystem;
using NPOI.POIFS.Crypt;
using PasswordManager.Utils;

namespace PasswordManager.Business
{
    public static class OfficeEncryptUtils
    {
        public static bool IsFileEncrypted(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                string extension = Path.GetExtension(filePath).ToLower();

                switch (extension)
                {
                    case ".docx":
                    case ".xlsx":
                    case ".pptx":
                        return IsOfficeOpenXmlEncrypted(filePath);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"检查文件加密状态失败: {ex.Message}");
                return false;
            }
        }

        private static bool HasOleSignature(string filePath)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length < 8)
                    {
                        return false;
                    }
                    byte[] buffer = new byte[8];
                    fs.Read(buffer, 0, 8);
                    return buffer[0] == 0xD0 && buffer[1] == 0xCF &&
                           buffer[2] == 0x11 && buffer[3] == 0xE0;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"检查OLE签名失败: {ex.Message}");
                return false;
            }
        }

        private static bool IsOfficeOpenXmlEncrypted(string filePath)
        {
            try
            {
                if (HasOleSignature(filePath))
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        try
                        {
                            NPOIFSFileSystem npoiFs = new NPOIFSFileSystem(fs);
                            try
                            {
                                new EncryptionInfo(npoiFs);
                                Logger.Debug($"文件 {filePath} 是OLE格式的加密Open XML文档");
                                return true;
                            }
                            catch
                            {
                                return false;
                            }
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < 4)
                    {
                        return false;
                    }
                    byte[] buffer = new byte[4];
                    fs.Read(buffer, 0, 4);
                    uint signature = BitConverter.ToUInt32(buffer, 0);
                    if (signature != 0x04034B50)
                    {
                        Logger.Debug($"文件 {filePath} 不具有ZIP签名");
                        return false;
                    }
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            if (entry.FullName.Equals("EncryptedPackage", StringComparison.OrdinalIgnoreCase) ||
                                entry.FullName.Equals("EncryptedPackage.core", StringComparison.OrdinalIgnoreCase))
                            {
                                Logger.Debug($"文件 {filePath} 是加密的Office Open XML文档");
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Debug($"检查Open XML加密失败: {ex.Message}");
                return false;
            }
        }

        public static bool VerifyPassword(string filePath, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                Logger.Debug($"验证密码对于文件: {filePath}");
                return VerifyPasswordWithNpoi(filePath, password);
            }
            catch (IOException ex)
            {
                Logger.Error($"文件IO异常: {ex.Message}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error($"文件权限不足: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"验证密码失败: {ex.Message}");
                return false;
            }
        }

        private static bool VerifyPasswordWithNpoi(string filePath, string password)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[8];
                    fs.Read(header, 0, 8);
                    
                    bool isOleFormat = header[0] == 0xD0 && header[1] == 0xCF && 
                                       header[2] == 0x11 && header[3] == 0xE0;
                    
                    fs.Seek(0, SeekOrigin.Begin);
                    
                    if (isOleFormat)
                    {
                        NPOIFSFileSystem npoiFs = new NPOIFSFileSystem(fs);
                        EncryptionInfo encryptionInfo = new EncryptionInfo(npoiFs);
                        Decryptor decryptor = Decryptor.GetInstance(encryptionInfo);
                        
                        bool isValid = decryptor.VerifyPassword(password);
                        
                        if (isValid)
                        {
                            Logger.Info($"NPOI密码验证成功: {filePath}");
                        }
                        else
                        {
                            Logger.Debug($"NPOI密码验证失败: {filePath}");
                        }
                        
                        return isValid;
                    }
                    else
                    {
                        using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read))
                        {
                            bool hasEncryptedPackage = false;
                            foreach (var entry in zip.Entries)
                            {
                                if (entry.FullName.Equals("EncryptedPackage", StringComparison.OrdinalIgnoreCase) ||
                                    entry.FullName.Equals("EncryptedPackage.core", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasEncryptedPackage = true;
                                    break;
                                }
                            }
                            
                            if (!hasEncryptedPackage)
                            {
                                Logger.Debug($"文件 {filePath} 未加密");
                                return false;
                            }
                        }
                        
                        return false;
                    }
                }
            }
            catch (NPOI.EncryptedDocumentException)
            {
                Logger.Debug($"NPOI密码验证失败 - 密码错误: {filePath}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"NPOI验证密码失败: {ex.Message}");
                return false;
            }
        }
    }
}