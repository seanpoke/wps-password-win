using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Business
{
    public static class OfficeEncryptUtils
    {
        private const int PASSWORD_VERIFY_BYTES = 4;

        public static bool IsFileEncrypted(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return false;
                }

                string extension = Path.GetExtension(filePath).ToLower();

                switch (extension)
                {
                    case ".docx":
                    case ".xlsx":
                    case ".pptx":
                        return IsOfficeOpenXmlEncrypted(filePath);
                    case ".doc":
                    case ".xls":
                    case ".ppt":
                        return IsOfficeOleEncrypted(filePath);
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

        private static bool HasZipSignature(string filePath)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length < 4)
                    {
                        return false;
                    }
                    byte[] buffer = new byte[4];
                    fs.Read(buffer, 0, 4);
                    uint signature = BitConverter.ToUInt32(buffer, 0);
                    return signature == 0x04034B50; // ZIP local file header signature
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"检查ZIP签名失败: {ex.Message}");
                return false;
            }
        }

        private static bool IsOfficeOpenXmlEncrypted(string filePath)
        {
            try
            {
                if (!HasZipSignature(filePath))
                {
                    Logger.Debug($"文件 {filePath} 不具有ZIP签名，可能不是标准Open XML格式");
                    return false;
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.Equals("EncryptedPackage", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.Equals("EncryptedPackage.core", StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.Debug($"文件 {filePath} 是加密的Office Open XML文档");
                            return true;
                        }
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"检查Open XML加密失败，文件可能损坏或加密格式不标准: {ex.Message}");
                return false;
            }
        }

        private static bool IsOfficeOleEncrypted(string filePath)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] header = new byte[8];
                    if (fs.Read(header, 0, 8) < 8)
                    {
                        return false;
                    }

                    if (header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0)
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        byte[] sectorTable = new byte[512];
                        fs.Read(sectorTable, 0, 512);

                        int dirSectorCount = BitConverter.ToInt32(sectorTable, 0x2C);
                        if (dirSectorCount < 0 || dirSectorCount > 100)
                        {
                            return false;
                        }

                        fs.Seek(0x44, SeekOrigin.Begin);
                        byte[] propertyBytes = new byte[4];
                        fs.Read(propertyBytes, 0, 4);
                        int propertyStartSector = BitConverter.ToInt32(propertyBytes, 0);

                        long propertyOffset = 512 + propertyStartSector * 512;
                        if (propertyOffset >= fs.Length)
                        {
                            return false;
                        }

                        fs.Seek(propertyOffset, SeekOrigin.Begin);
                        byte[] propertyEntry = new byte[128];
                        fs.Read(propertyEntry, 0, 128);

                        if (propertyEntry[0x42] == 0xFF && propertyEntry[0x43] == 0xFF)
                        {
                            Logger.Debug($"文件 {filePath} 是加密的Office OLE文档");
                            return true;
                        }
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"检查OLE加密失败: {ex.Message}");
                return false;
            }
        }

        public static bool VerifyPassword(string filePath, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            try
            {
                Logger.Debug($"验证密码对于文件: {filePath}");

                string extension = Path.GetExtension(filePath).ToLower();
                
                bool isEncrypted = IsFileEncrypted(filePath);
                
                if (!isEncrypted)
                {
                    Logger.Debug($"文件 {filePath} 加密检测结果为未加密，但仍尝试验证密码");
                }
                else
                {
                    Logger.Debug($"文件 {filePath} 已加密，开始验证密码");
                }

                switch (extension)
                {
                    case ".docx":
                    case ".xlsx":
                    case ".pptx":
                        return VerifyOpenXmlPassword(filePath, password);
                    case ".doc":
                    case ".xls":
                    case ".ppt":
                        return VerifyOlePassword(filePath, password);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"验证密码失败: {ex.Message}");
                return false;
            }
        }

        private static bool VerifyOpenXmlPassword(string filePath, string password)
        {
            try
            {
                if (!HasZipSignature(filePath))
                {
                    Logger.Debug($"文件 {filePath} 不具有ZIP签名，无法验证Open XML密码");
                    return false;
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry encryptionInfo = null;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.Equals("EncryptionInfo", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.Equals("EncryptionInfo.xml", StringComparison.OrdinalIgnoreCase))
                        {
                            encryptionInfo = entry;
                            break;
                        }
                    }

                    if (encryptionInfo == null)
                    {
                        Logger.Debug($"未找到加密信息文件: {filePath}，文件可能未加密或加密格式不同");
                        return false;
                    }

                    using (Stream infoStream = encryptionInfo.Open())
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        infoStream.CopyTo(memStream);
                        byte[] infoBytes = memStream.ToArray();

                        if (infoBytes.Length < 20)
                        {
                            Logger.Debug($"加密信息文件过短: {filePath}");
                            return false;
                        }

                        int flags = infoBytes[0] | (infoBytes[1] << 8);
                        bool isExternal = (flags & 0x04) != 0;
                        bool cryptoApi = (flags & 0x10) != 0;

                        if (!isExternal && cryptoApi)
                        {
                            return VerifyOpenXmlCryptoApiPassword(filePath, password, infoBytes);
                        }
                        else if (isExternal && !cryptoApi)
                        {
                            return VerifyOpenXmlExternalPassword(password, infoBytes);
                        }
                        else
                        {
                            Logger.Debug($"不支持的加密类型: flags={flags}");
                            return VerifyOpenXmlExternalPassword(password, infoBytes);
                        }
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                Logger.Error($"验证Open XML密码失败 - ZIP结构无效: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"验证Open XML密码失败: {ex.Message}");
                return false;
            }
        }

        private static bool VerifyOpenXmlCryptoApiPassword(string filePath, string password, byte[] infoBytes)
        {
            try
            {
                byte[] saltValue = new byte[16];
                byte[] encryptedVerifier = new byte[16];
                byte[] encryptedVerifierHash = new byte[32];

                if (infoBytes.Length < 84)
                {
                    Logger.Debug($"加密信息太短: {infoBytes.Length}");
                    return false;
                }

                Array.Copy(infoBytes, 8, saltValue, 0, 16);
                Array.Copy(infoBytes, 24, encryptedVerifier, 0, 16);
                Array.Copy(infoBytes, 40, encryptedVerifierHash, 0, 32);

                byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
                byte[] verifierWithSalt = new byte[saltValue.Length + PASSWORD_VERIFY_BYTES];
                Array.Copy(saltValue, 0, verifierWithSalt, 0, saltValue.Length);
                for (int i = 0; i < PASSWORD_VERIFY_BYTES && i < passwordBytes.Length; i++)
                {
                    verifierWithSalt[saltValue.Length + i] = passwordBytes[i];
                }

                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] firstHash = sha256.ComputeHash(verifierWithSalt);
                    byte[] hashValue = new byte[saltValue.Length + 32];
                    Array.Copy(saltValue, 0, hashValue, 0, saltValue.Length);
                    Array.Copy(firstHash, 0, hashValue, saltValue.Length, 32);

                    for (int i = 0; i < 1000; i++)
                    {
                        byte[] iterationBytes = BitConverter.GetBytes(i);
                        byte[] toHash = new byte[hashValue.Length + 4];
                        Array.Copy(hashValue, 0, toHash, 0, hashValue.Length);
                        Array.Copy(iterationBytes, 0, toHash, hashValue.Length, 4);
                        hashValue = sha256.ComputeHash(toHash);
                    }

                    byte[] finalHash = new byte[32];
                    for (int i = 0; i < 10000; i++)
                    {
                        byte[] toHash = new byte[hashValue.Length + 4];
                        Array.Copy(hashValue, 0, toHash, 0, hashValue.Length);
                        int blockIndex = i;
                        toHash[hashValue.Length] = (byte)(blockIndex & 0xFF);
                        toHash[hashValue.Length + 1] = (byte)((blockIndex >> 8) & 0xFF);
                        toHash[hashValue.Length + 2] = (byte)((blockIndex >> 16) & 0xFF);
                        toHash[hashValue.Length + 3] = (byte)((blockIndex >> 24) & 0xFF);
                        hashValue = sha256.ComputeHash(toHash);
                    }

                    if (ByteArrayEquals(hashValue, encryptedVerifierHash))
                    {
                        Logger.Info($"密码验证成功(CryptoAPI模式): {filePath}");
                        return true;
                    }
                }

                Logger.Debug($"密码验证失败(CryptoAPI模式): {filePath}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"CryptoAPI密码验证异常: {ex.Message}");
                return false;
            }
        }

        private static bool VerifyOpenXmlExternalPassword(string password, byte[] infoBytes)
        {
            try
            {
                byte[] hashValue = new byte[16];
                byte[] saltValue = new byte[16];

                if (infoBytes.Length < 32)
                {
                    return false;
                }

                Array.Copy(infoBytes, 8, saltValue, 0, 16);

                byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
                byte[] verifier = new byte[saltValue.Length + passwordBytes.Length];
                Array.Copy(saltValue, 0, verifier, 0, saltValue.Length);
                Array.Copy(passwordBytes, 0, verifier, saltValue.Length, passwordBytes.Length);

                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    byte[] firstHash = sha1.ComputeHash(verifier);
                    Array.Copy(saltValue, 0, hashValue, 0, 8);
                    Array.Copy(firstHash, 0, hashValue, 8, 8);

                    for (int i = 0; i < 50000; i++)
                    {
                        byte[] iterationBytes = BitConverter.GetBytes(i);
                        byte[] toHash = new byte[hashValue.Length + 4];
                        Array.Copy(hashValue, 0, toHash, 0, hashValue.Length);
                        Array.Copy(iterationBytes, 0, toHash, hashValue.Length, 4);
                        byte[] result = sha1.ComputeHash(toHash);
                        Array.Copy(result, 0, hashValue, 0, 16);
                    }
                }

                byte[] storedVerifier = new byte[16];
                Array.Copy(infoBytes, 24, storedVerifier, 0, 16);

                if (ByteArrayEquals(hashValue, storedVerifier))
                {
                    Logger.Info($"密码验证成功(External模式)");
                    return true;
                }

                Logger.Debug($"密码验证失败(External模式)");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"External密码验证异常: {ex.Message}");
                return false;
            }
        }

        private static bool VerifyOlePassword(string filePath, string password)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] header = new byte[512];
                    if (fs.Read(header, 0, 512) < 512)
                    {
                        return false;
                    }

                    if (header[0] != 0xD0 || header[1] != 0xCF || header[2] != 0x11 || header[3] != 0xE0)
                    {
                        return false;
                    }

                    int minorVersion = BitConverter.ToInt16(header, 0x18);
                    int majorVersion = BitConverter.ToInt16(header, 0x1A);

                    byte[] encryptedVerifier = new byte[PASSWORD_VERIFY_BYTES];
                    byte[] encryptedVerifierHash = new byte[16];

                    fs.Seek(0xD0, SeekOrigin.Begin);
                    fs.Read(encryptedVerifier, 0, PASSWORD_VERIFY_BYTES);
                    fs.Read(encryptedVerifierHash, 0, 16);

                    byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
                    byte[] verifierBytes = new byte[encryptedVerifier.Length + passwordBytes.Length];
                    Array.Copy(passwordBytes, 0, verifierBytes, 0, passwordBytes.Length);
                    Array.Copy(encryptedVerifier, 0, verifierBytes, passwordBytes.Length, encryptedVerifier.Length);

                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        byte[] hash = md5.ComputeHash(verifierBytes);

                        if (ByteArrayEquals(hash, encryptedVerifierHash))
                        {
                            Logger.Info($"OLE文档密码验证成功");
                            return true;
                        }
                    }

                    Logger.Debug($"OLE文档密码验证失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"验证OLE密码失败: {ex.Message}");
                return false;
            }
        }

        private static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}