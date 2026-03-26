using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.VariantTypes;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.IO.Packaging;
using System.Xml.Linq;
using System.Text;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Business
{
    public class MetadataManager
    {
        private const string PasswordPropertyName = "WpsPasswordManager";
        private const string UidPropertyName = "WpsPasswordManagerUid";
        private const string ExternalStorageFile = "passwords.json";
        private const string UidCacheFile = "uid_cache.json";

        // ZIP文件结构常量
        private const uint ZIP_EOCD_SIGNATURE = 0x06054B50; // End of Central Directory signature "PK\x05\x06"
        private const uint ZIP_COMMENT_LENGTH_MAX = 65535; // ZIP注释最大长度
        private const string METADATA_MAGIC = "WPPM"; // WPS Password Manager Magic
        private const ushort METADATA_VERSION = 1; // 元数据格式版本

        // UID缓存，用于存储文档的UID
        private Dictionary<string, string> uidCache;

        // 外部存储，用于存储非ZIP格式文档的密码
        private Dictionary<string, string> externalPasswordStore;

        public MetadataManager()
        {
            LoadExternalPasswordStore();
            LoadUidCache();
        }

        // 加载UID缓存
        private void LoadUidCache()
        {
            try
            {
                if (File.Exists(UidCacheFile))
                {
                    string json = File.ReadAllText(UidCacheFile);
                    uidCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
                else
                {
                    uidCache = new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"加载UID缓存失败: {ex.Message}");
                uidCache = new Dictionary<string, string>();
            }
        }

        // 保存UID缓存
        private void SaveUidCache()
        {
            try
            {
                string json = JsonSerializer.Serialize(uidCache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(UidCacheFile, json);
            }
            catch (Exception ex)
            {
                Logger.Error($"保存UID缓存失败: {ex.Message}");
            }
        }

        // 加载外部密码存储
        private void LoadExternalPasswordStore()
        {
            try
            {
                if (File.Exists(ExternalStorageFile))
                {
                    string json = File.ReadAllText(ExternalStorageFile);
                    externalPasswordStore = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
                else
                {
                    externalPasswordStore = new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"加载外部密码存储失败: {ex.Message}");
                externalPasswordStore = new Dictionary<string, string>();
            }
        }

        // 保存外部密码存储
        private void SaveExternalPasswordStore()
        {
            try
            {
                string json = JsonSerializer.Serialize(externalPasswordStore, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ExternalStorageFile, json);
            }
            catch (Exception ex)
            {
                Logger.Error($"保存外部密码存储失败: {ex.Message}");
            }
        }

        // 从外部存储中读取密码
        private string ReadPasswordFromExternalStorage(string filePath)
        {
            if (externalPasswordStore.TryGetValue(filePath, out string password))
            {
                return password;
            }
            return null;
        }

        // 将密码保存到外部存储
        private bool WritePasswordToExternalStorage(string filePath, string password)
        {
            try
            {
                externalPasswordStore[filePath] = password;
                SaveExternalPasswordStore();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"写入外部密码存储失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 定位ZIP文件中End of Central Directory的位置
        /// </summary>
        /// <param name="filePath">ZIP文件路径</param>
        /// <returns>EOCD的起始位置，如果未找到返回-1</returns>
        private long FindEndOfCentralDirectory(string filePath)
        {
            try
            {
                // 尝试以不同的FileShare模式打开文件
                FileStream fs = null;
                try
                {
                    // 尝试以共享读取模式打开文件
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch
                {
                    // 如果失败，尝试以共享读取模式打开
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }

                using (fs)
                {
                    long fileLength = fs.Length;
                    if (fileLength < 22) // EOCD最小长度
                    {
                        return -1;
                    }

                    // 从文件末尾开始搜索EOCD签名
                    // EOCD签名: 0x06054B50 ("PK\x05\x06")
                    // 注释长度字段在EOCD的第20-21字节（0-based index）
                    // 所以搜索范围是文件末尾的64KB + 22字节

                    long searchStart = Math.Max(0, fileLength - ZIP_COMMENT_LENGTH_MAX - 22);
                    int searchLength = (int)(fileLength - searchStart);

                    byte[] buffer = new byte[searchLength];
                    fs.Seek(searchStart, SeekOrigin.Begin);
                    fs.Read(buffer, 0, searchLength);

                    // 从后向前搜索签名
                    for (int i = searchLength - 22; i >= 0; i--)
                    {
                        uint signature = BitConverter.ToUInt32(buffer, i);
                        if (signature == ZIP_EOCD_SIGNATURE)
                        {
                            long eocdPosition = searchStart + i;
                            Logger.Debug($"找到EOCD位置: {eocdPosition}");
                            return eocdPosition;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"查找EOCD时出错: {ex.Message}");
            }

            return -1;
        }

        /// <summary>
        /// 检查文件是否为有效的ZIP文件或Office文档
        /// </summary>
        private bool IsValidZipFile(string filePath)
        {
            try
            {
                // 对于Office文档，即使加密也应该认为是有效的
                string extension = Path.GetExtension(filePath).ToLower();
                if (extension == ".docx" || extension == ".xlsx" || extension == ".pptx")
                {
                    // 对于Office文档，我们假设它是有效的ZIP文件结构
                    return true;
                }

                // 尝试以不同的FileShare模式打开文件
                FileStream fs = null;
                try
                {
                    // 尝试以共享读取模式打开文件
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch
                {
                    // 如果失败，尝试以共享读取模式打开
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }

                using (fs)
                {
                    if (fs.Length < 4)
                    {
                        return false;
                    }

                    byte[] buffer = new byte[4];
                    fs.Read(buffer, 0, 4);

                    // 检查ZIP文件签名: 0x04034B50 ("PK\x03\x04")
                    uint signature = BitConverter.ToUInt32(buffer, 0);
                    return signature == 0x04034B50;
                }
            }
            catch
            {
                // 对于Office文档，即使无法打开也认为是有效的
                string extension = Path.GetExtension(filePath).ToLower();
                if (extension == ".docx" || extension == ".xlsx" || extension == ".pptx")
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 构建元数据块
        /// 格式: [Magic(4)] [Version(2)] [Type(1)] [Length(4)] [Data(N)] [Checksum(4)]
        /// </summary>
        private byte[] BuildMetadataBlock(byte type, string data)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            int totalLength = 4 + 2 + 1 + 4 + dataBytes.Length + 4; // Magic + Version + Type + Length + Data + Checksum

            byte[] block = new byte[totalLength];
            int offset = 0;

            // Magic (4 bytes)
            byte[] magicBytes = Encoding.ASCII.GetBytes(METADATA_MAGIC);
            Buffer.BlockCopy(magicBytes, 0, block, offset, 4);
            offset += 4;

            // Version (2 bytes)
            BitConverter.GetBytes(METADATA_VERSION).CopyTo(block, offset);
            offset += 2;

            // Type (1 byte): 1=Password, 2=UID
            block[offset] = type;
            offset += 1;

            // Data Length (4 bytes)
            BitConverter.GetBytes(dataBytes.Length).CopyTo(block, offset);
            offset += 4;

            // Data (N bytes)
            Buffer.BlockCopy(dataBytes, 0, block, offset, dataBytes.Length);
            offset += dataBytes.Length;

            // Checksum (4 bytes) - 使用CRC32校验
            uint checksum = CalculateCrc32(block, 0, offset);
            BitConverter.GetBytes(checksum).CopyTo(block, offset);

            return block;
        }

        /// <summary>
        /// 从文件末尾解析元数据块
        /// </summary>
        private bool TryParseMetadataBlock(byte[] data, out byte type, out string content)
        {
            type = 0;
            content = null;

            try
            {
                if (data.Length < 15) // 最小长度: Magic(4) + Version(2) + Type(1) + Length(4) + Checksum(4)
                {
                    return false;
                }

                int offset = 0;

                // 检查Magic
                string magic = Encoding.ASCII.GetString(data, offset, 4);
                if (magic != METADATA_MAGIC)
                {
                    return false;
                }
                offset += 4;

                // 检查Version
                ushort version = BitConverter.ToUInt16(data, offset);
                if (version != METADATA_VERSION)
                {
                    Logger.Warning($"不支持的元数据版本: {version}");
                    return false;
                }
                offset += 2;

                // 读取Type
                type = data[offset];
                offset += 1;

                // 读取Data Length
                int dataLength = BitConverter.ToInt32(data, offset);
                offset += 4;

                if (dataLength < 0 || dataLength > 65535 || offset + dataLength + 4 > data.Length)
                {
                    Logger.Warning($"无效的数据长度: {dataLength}");
                    return false;
                }

                // 读取Data
                content = Encoding.UTF8.GetString(data, offset, dataLength);
                offset += dataLength;

                // 验证Checksum
                uint storedChecksum = BitConverter.ToUInt32(data, offset);
                uint calculatedChecksum = CalculateCrc32(data, 0, offset);

                if (storedChecksum != calculatedChecksum)
                {
                    Logger.Warning("元数据校验和验证失败");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"解析元数据块时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 简单的CRC32计算
        /// </summary>
        private uint CalculateCrc32(byte[] data, int offset, int length)
        {
            uint crc = 0xFFFFFFFF;
            uint[] crcTable = new uint[256];

            // 初始化CRC表
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                {
                    c = (c & 1) != 0 ? (0xEDB88320 ^ (c >> 1)) : (c >> 1);
                }
                crcTable[i] = c;
            }

            // 计算CRC
            for (int i = offset; i < offset + length; i++)
            {
                crc = crcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFF;
        }

        // 写入密码到文档元数据（基于ZIP尾部附加数据）
        public bool WritePasswordToMetadata(string filePath, string password)
        {
            // 检查文件是否存在
            if (!System.IO.File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return false;
            }

            // 检查文件扩展名
            string extension = System.IO.Path.GetExtension(filePath).ToLower();
            if (!IsSupportedFormat(extension))
            {
                Logger.Warning($"不支持的文件格式: {extension}");
                return false;
            }

            // 打印密码值
            Logger.Info($"准备写入密码到 {filePath}，密码: {password}");

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    // 使用ZIP尾部附加数据方式写入密码
                    if (WritePasswordToZipMetadata(filePath, password))
                    {
                        return true;
                    }
                    else
                    {
                        throw new Exception("无法写入ZIP元数据");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"写入元数据失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        Logger.Debug($"重试写入元数据，剩余次数: {retryCount}");
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            Logger.Error($"多次尝试后仍无法写入元数据到 {filePath}");
            return false;
        }

        /// <summary>
        /// 将密码写入ZIP文件尾部（End of Central Directory之后）
        /// </summary>
        private bool WritePasswordToZipMetadata(string filePath, string password)
        {
            try
            {
                // 检查是否为有效的ZIP文件或Office文档
                if (!IsValidZipFile(filePath))
                {
                    Logger.Error($"文件不是有效的ZIP文件: {filePath}");
                    return false;
                }

                // 构建密码元数据块
                byte[] metadataBlock = BuildMetadataBlock(1, password); // Type 1 = Password

                // 尝试以不同的FileShare模式打开文件
                FileStream fs = null;
                try
                {
                    // 尝试以共享读取模式打开文件
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                }
                catch
                {
                    // 如果失败，尝试以独占模式打开
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }

                using (fs)
                {
                    // 尝试查找EOCD位置，但即使失败也继续执行
                    long eocdPosition = -1;
                    try
                    {
                        eocdPosition = FindEndOfCentralDirectory(filePath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"查找EOCD时出错: {ex.Message}，将直接附加到文件末尾");
                    }

                    if (eocdPosition >= 0)
                    {
                        try
                        {
                            // 找到EOCD，读取EOCD的长度
                            fs.Seek(eocdPosition, SeekOrigin.Begin);
                            byte[] eocdBuffer = new byte[22];
                            fs.Read(eocdBuffer, 0, 22);
                            
                            // 读取注释长度（EOCD的第20-21字节）
                            ushort commentLength = BitConverter.ToUInt16(eocdBuffer, 20);
                            long eocdEndPosition = eocdPosition + 22 + commentLength;
                            
                            // 尝试查找旧的元数据
                            long existingMetadataStart = -1;
                            try
                            {
                                existingMetadataStart = FindMetadataStartPosition(filePath);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warning($"查找元数据位置时出错: {ex.Message}");
                            }

                            if (existingMetadataStart > 0 && existingMetadataStart >= eocdEndPosition)
                            {
                                // 截断到EOCD结束位置
                                fs.SetLength(eocdEndPosition);
                                Logger.Debug($"移除旧的元数据，截断到EOCD结束位置: {eocdEndPosition}");
                            }
                            else
                            {
                                // 移动到文件末尾
                                fs.Seek(0, SeekOrigin.End);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"处理EOCD时出错: {ex.Message}，将直接附加到文件末尾");
                            // 移动到文件末尾
                            fs.Seek(0, SeekOrigin.End);
                        }
                    }
                    else
                    {
                        // 找不到EOCD，直接附加到文件末尾
                        fs.Seek(0, SeekOrigin.End);
                    }

                    // 写入元数据块
                    fs.Write(metadataBlock, 0, metadataBlock.Length);

                    Logger.Info($"密码已成功写入到 {filePath} 的ZIP尾部");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"写入ZIP元数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 查找元数据在文件中的起始位置
        /// </summary>
        private long FindMetadataStartPosition(string filePath)
        {
            try
            {
                // 尝试以不同的FileShare模式打开文件
                FileStream fs = null;
                try
                {
                    // 尝试以共享读取模式打开文件
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch
                {
                    // 如果失败，尝试以共享读取模式打开
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }

                using (fs)
                {
                    long fileLength = fs.Length;
                    if (fileLength < 15) // 最小元数据块长度
                    {
                        return -1;
                    }

                    // 从文件末尾开始搜索Magic
                    long searchStart = Math.Max(0, fileLength - 1024); // 最多搜索1KB
                    int searchLength = (int)(fileLength - searchStart);

                    byte[] buffer = new byte[searchLength];
                    fs.Seek(searchStart, SeekOrigin.Begin);
                    fs.Read(buffer, 0, searchLength);

                    byte[] magicBytes = Encoding.ASCII.GetBytes(METADATA_MAGIC);

                    // 从后向前搜索Magic
                    for (int i = searchLength - magicBytes.Length; i >= 0; i--)
                    {
                        bool found = true;
                        for (int j = 0; j < magicBytes.Length; j++)
                        {
                            if (buffer[i + j] != magicBytes[j])
                            {
                                found = false;
                                break;
                            }
                        }

                        if (found)
                        {
                            long metadataPosition = searchStart + i;
                            Logger.Debug($"找到元数据位置: {metadataPosition}");
                            return metadataPosition;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"查找元数据位置时出错: {ex.Message}");
            }

            return -1;
        }

        // 从文档元数据读取密码（基于ZIP尾部附加数据）
        public string ReadPasswordFromMetadata(string filePath)
        {
            // 检查文件是否存在
            if (!System.IO.File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return null;
            }

            // 检查文件扩展名
            string extension = System.IO.Path.GetExtension(filePath).ToLower();
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
                    // 使用ZIP尾部附加数据方式读取密码
                    string password = ReadPasswordFromZipMetadata(filePath);
                    if (!string.IsNullOrEmpty(password))
                    {
                        Logger.Info($"从 {filePath} 的ZIP尾部读取到密码");
                        return password;
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取元数据失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            Logger.Error($"多次尝试后仍无法读取 {filePath} 的元数据");
            return null;
        }

        /// <summary>
        /// 从ZIP文件尾部读取密码和UID
        /// </summary>
        private string ReadPasswordFromZipMetadata(string filePath)
        {
            try
            {
                // 检查是否为有效的ZIP文件
                if (!IsValidZipFile(filePath))
                {
                    Logger.Error($"文件不是有效的ZIP文件: {filePath}");
                    return null;
                }

                // 从ZIP文件尾部读取数据
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // 读取文件尾部的1KB数据，足够包含所有元数据
                    long fileLength = fs.Length;
                    long readSize = Math.Min(1024, fileLength);
                    long startPosition = fileLength - readSize;
                    
                    byte[] buffer = new byte[readSize];
                    fs.Seek(startPosition, SeekOrigin.Begin);
                    fs.Read(buffer, 0, (int)readSize);
                    
                    Logger.Info($"从文件尾部读取 {readSize} 字节数据");


                    string password = null;
                    string uid = null;

                    // 从后向前搜索元数据
                    byte[] magicBytes = Encoding.ASCII.GetBytes(METADATA_MAGIC);
                    for (int i = buffer.Length - magicBytes.Length; i >= 0; i--)
                    {
                        bool found = true;
                        for (int j = 0; j < magicBytes.Length; j++)
                        {
                            if (buffer[i + j] != magicBytes[j])
                            {
                                found = false;
                                break;
                            }
                        }

                        if (found)
                        {
                            // 提取元数据块
                            int metadataBlockStart = i;
                            // 尝试解析元数据块
                            byte[] metadataBlock = new byte[buffer.Length - metadataBlockStart];
                            Array.Copy(buffer, metadataBlockStart, metadataBlock, 0, metadataBlock.Length);
                            
                            if (TryParseMetadataBlock(metadataBlock, out byte type, out string content))
                            {
                                if (type == 1) // Type 1 = Password
                                {
                                    password = content;
                                    Logger.Info($"从ZIP尾部成功读取密码：{password}");
                                    // 找到密码后停止搜索，只返回最后一个密码（最新的）
                                    break;
                                }
                                else if (type == 2) // Type 2 = UID
                                {
                                    uid = content;
                                    Logger.Info($"从ZIP尾部成功读取UID: {uid}");
                                    // 将UID放入缓存
                                    if (!string.IsNullOrEmpty(uid))
                                    {
                                        uidCache[filePath] = uid;
                                        SaveUidCache();
                                    }
                                }
                            }
                        }
                    }

                    // 返回密码，用于自动填充
                    return password;
                }
                
                Logger.Debug($"未找到元数据: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"读取ZIP元数据失败: {ex.Message}");
            }

            return null;
        }

        // 检查文档是否有密码元数据
        public bool HasPasswordMetadata(string filePath)
        {
            return !string.IsNullOrEmpty(ReadPasswordFromMetadata(filePath));
        }

        // 检查是否支持的文件格式
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

        // 生成UID
        private string GenerateUid()
        {
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            string uuid = Guid.NewGuid().ToString();
            return $"{timestamp}_{uuid}";
        }

        // 从文档元数据读取UID（基于ZIP尾部附加数据）
        public string ReadUidFromMetadata(string filePath)
        {
            // 检查文件是否存在
            if (!System.IO.File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return null;
            }

            // 检查文件扩展名
            string extension = System.IO.Path.GetExtension(filePath).ToLower();
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
                    // 使用ZIP尾部附加数据方式读取UID
                    string uid = ReadUidFromZipMetadata(filePath);
                    if (!string.IsNullOrEmpty(uid))
                    {
                        Logger.Info($"从 {filePath} 的ZIP尾部读取到UID");
                        return uid;
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取UID元数据失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            Logger.Error($"多次尝试后仍无法读取 {filePath} 的UID元数据");
            return null;
        }

        /// <summary>
        /// 从ZIP文件尾部读取UID
        /// </summary>
        private string ReadUidFromZipMetadata(string filePath)
        {
            try
            {
                // 检查是否为有效的ZIP文件
                if (!IsValidZipFile(filePath))
                {
                    Logger.Error($"文件不是有效的ZIP文件: {filePath}");
                    return null;
                }

                // 从ZIP文件尾部读取数据
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // 读取文件尾部的1KB数据，足够包含所有元数据
                    long fileLength = fs.Length;
                    long readSize = Math.Min(1024, fileLength);
                    long startPosition = fileLength - readSize;
                    
                    byte[] buffer = new byte[readSize];
                    fs.Seek(startPosition, SeekOrigin.Begin);
                    fs.Read(buffer, 0, (int)readSize);
                    
                    // 从后向前搜索元数据
                    byte[] magicBytes = Encoding.ASCII.GetBytes(METADATA_MAGIC);
                    for (int i = buffer.Length - magicBytes.Length; i >= 0; i--)
                    {
                        bool found = true;
                        for (int j = 0; j < magicBytes.Length; j++)
                        {
                            if (buffer[i + j] != magicBytes[j])
                            {
                                found = false;
                                break;
                            }
                        }

                        if (found)
                        {
                            // 提取元数据块
                            int metadataBlockStart = i;
                            // 尝试解析元数据块
                            byte[] metadataBlock = new byte[buffer.Length - metadataBlockStart];
                            Array.Copy(buffer, metadataBlockStart, metadataBlock, 0, metadataBlock.Length);
                            
                            if (TryParseMetadataBlock(metadataBlock, out byte type, out string content))
                            {
                                if (type == 2) // Type 2 = UID
                                {
                                    Logger.Info($"从ZIP尾部成功读取UID: {content}");
                                    // 找到UID后停止搜索
                                    return content;
                                }
                            }
                        }
                    }
                }
                
                Logger.Debug($"未找到UID元数据: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"读取ZIP UID元数据失败: {ex.Message}");
            }

            return null;
        }

        // 写入UID到文档元数据（基于ZIP尾部附加数据）
        public bool WriteUidToMetadata(string filePath, string uid)
        {
            // 检查文件是否存在
            if (!System.IO.File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return false;
            }

            // 检查文件扩展名
            string extension = System.IO.Path.GetExtension(filePath).ToLower();
            if (!IsSupportedFormat(extension))
            {
                Logger.Warning($"不支持的文件格式: {extension}");
                return false;
            }

            // 打印UID值
            Logger.Info($"准备写入UID到 {filePath}，UID: {uid}");

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    Logger.Info($"开始写入UID，尝试次数: {4 - retryCount}/3");
                    // 使用ZIP尾部附加数据方式写入UID
                    if (WriteUidToZipMetadata(filePath, uid))
                    {
                        Logger.Info($"UID写入成功");
                        return true;
                    }
                    else
                    {
                        Logger.Error("WriteUidToZipMetadata返回false");
                        throw new Exception("无法写入ZIP UID元数据");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"写入UID元数据失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        Logger.Debug($"重试写入UID元数据，剩余次数: {retryCount}");
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            Logger.Error($"多次尝试后仍无法写入UID元数据到 {filePath}");
            return false;
        }

        /// <summary>
        /// 将UID写入ZIP文件尾部（End of Central Directory之后）
        /// </summary>
        private bool WriteUidToZipMetadata(string filePath, string uid)
        {
            try
            {
                Logger.Info($"开始执行WriteUidToZipMetadata，文件: {filePath}");
                
                // 检查是否为有效的ZIP文件或Office文档
                if (!IsValidZipFile(filePath))
                {
                    Logger.Error($"文件不是有效的ZIP文件: {filePath}");
                    return false;
                }
                Logger.Info($"文件验证通过");

                // 构建UID元数据块
                byte[] metadataBlock = BuildMetadataBlock(2, uid); // Type 2 = UID
                Logger.Info($"元数据块构建完成，长度: {metadataBlock.Length}");

                // 尝试以不同的FileShare模式打开文件
                FileStream fs = null;
                try
                {
                    // 尝试以共享读取模式打开文件
                    Logger.Info("尝试以共享读取模式打开文件");
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                    Logger.Info("成功以共享读取模式打开文件");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"共享读取模式打开失败: {ex.Message}，尝试独占模式");
                    // 如果失败，尝试以独占模式打开
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    Logger.Info("成功以独占模式打开文件");
                }

                using (fs)
                {
                    Logger.Info($"文件打开成功，当前文件长度: {fs.Length}");
                    
                    // 尝试查找EOCD位置，但即使失败也继续执行
                    long eocdPosition = -1;
                    try
                    {
                        Logger.Info("开始查找EOCD位置");
                        eocdPosition = FindEndOfCentralDirectory(filePath);
                        Logger.Info($"EOCD位置: {eocdPosition}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"查找EOCD时出错: {ex.Message}，将直接附加到文件末尾");
                    }

                    if (eocdPosition >= 0)
                    {
                        try
                        {
                            // 找到EOCD，读取EOCD的长度
                            fs.Seek(eocdPosition, SeekOrigin.Begin);
                            byte[] eocdBuffer = new byte[22];
                            fs.Read(eocdBuffer, 0, 22);
                            
                            // 读取注释长度（EOCD的第20-21字节）
                            ushort commentLength = BitConverter.ToUInt16(eocdBuffer, 20);
                            long eocdEndPosition = eocdPosition + 22 + commentLength;
                            Logger.Info($"EOCD结束位置: {eocdEndPosition}");
                            
                            // 尝试查找旧的元数据
                            long existingMetadataStart = -1;
                            try
                            {
                                Logger.Info("开始查找旧的元数据位置");
                                existingMetadataStart = FindMetadataStartPosition(filePath);
                                Logger.Info($"旧元数据位置: {existingMetadataStart}");
                            }
                            catch (Exception ex)
                            {
                                Logger.Warning($"查找元数据位置时出错: {ex.Message}");
                            }

                            if (existingMetadataStart > 0 && existingMetadataStart >= eocdEndPosition)
                            {
                                // 截断到EOCD结束位置
                                fs.SetLength(eocdEndPosition);
                                Logger.Info($"移除旧的UID元数据，截断到EOCD结束位置: {eocdEndPosition}");
                            }
                            else
                            {
                                // 移动到文件末尾
                                fs.Seek(0, SeekOrigin.End);
                                Logger.Info($"移动到文件末尾，当前位置: {fs.Position}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"处理EOCD时出错: {ex.Message}，将直接附加到文件末尾");
                            // 移动到文件末尾
                            fs.Seek(0, SeekOrigin.End);
                            Logger.Info($"移动到文件末尾，当前位置: {fs.Position}");
                        }
                    }
                    else
                    {
                        // 找不到EOCD，直接附加到文件末尾
                        fs.Seek(0, SeekOrigin.End);
                        Logger.Info($"找不到EOCD，移动到文件末尾，当前位置: {fs.Position}");
                    }

                    // 写入元数据块
                    Logger.Info($"开始写入元数据块，长度: {metadataBlock.Length}");
                    fs.Write(metadataBlock, 0, metadataBlock.Length);
                    fs.Flush();
                    Logger.Info($"元数据块写入完成，新文件长度: {fs.Length}");

                    Logger.Info($"UID已成功写入到 {filePath} 的ZIP尾部");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"写入ZIP UID元数据失败: {ex.Message}");
                Logger.Error($"异常堆栈: {ex.StackTrace}");
                return false;
            }
        }

        // 获取文档的UID
        public string GetDocumentUid(string filePath)
        {
            Logger.Info($"开始获取文档UID: {filePath}");
            // 首先尝试从元数据中读取UID
            string uid = ReadUidFromMetadata(filePath);
            if (!string.IsNullOrEmpty(uid))
            {
                Logger.Info($"从元数据中读取到UID: {uid}");
                // 如果读取到UID，将其放入缓存
                uidCache[filePath] = uid;
                SaveUidCache();
                return uid;
            }

            // 如果元数据中没有UID，尝试从缓存中读取
            if (uidCache.TryGetValue(filePath, out string cachedUid))
            {
                Logger.Info($"从缓存中读取到UID: {cachedUid}");
                return cachedUid;
            }

            // 如果缓存中也没有，生成新的UID并放入缓存
            string newUid = GenerateUid();
            Logger.Info($"生成新的UID: {newUid}");
            uidCache[filePath] = newUid;
            SaveUidCache();
            return newUid;
        }

        // 保存文档的UID到元数据（在文档关闭时调用）
        public bool SaveDocumentUid(string filePath)
        {
            // 从缓存中获取UID
            if (uidCache.TryGetValue(filePath, out string uid))
            {
                // 写入到元数据
                bool success = WriteUidToMetadata(filePath, uid);
                if (success)
                {
                    Logger.Info($"UID已成功保存到 {filePath} 的元数据中");
                }
                return success;
            }
            return false;
        }

        // 根据文件类型打开文档
        private OpenXmlPackage OpenDocument(string filePath, bool isEditable)
        {
            string extension = System.IO.Path.GetExtension(filePath).ToLower();

            try
            {
                // 检查文件是否为有效的ZIP文件
                if (extension == ".docx" || extension == ".xlsx" || extension == ".pptx")
                {
                    if (!IsValidZipFile(filePath))
                    {
                        Logger.Error($"文件不是有效的ZIP文件: {filePath}");
                        return null;
                    }
                }

                // 对于不同的文件类型，使用不同的打开方式
                switch (extension)
                {
                    case ".docx":
                        return WordprocessingDocument.Open(filePath, isEditable);
                    case ".xlsx":
                        return SpreadsheetDocument.Open(filePath, isEditable);
                    case ".pptx":
                        return PresentationDocument.Open(filePath, isEditable);
                    default:
                        // 对于.doc, .xls, .ppt等旧格式，尝试使用WordprocessingDocument打开
                        try
                        {
                            return WordprocessingDocument.Open(filePath, isEditable);
                        }
                        catch
                        {
                            Logger.Error($"无法打开旧格式文档: {filePath}");
                            return null;
                        }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"打开文档失败: {ex.Message}");
                return null;
            }
        }

        // 获取自定义属性部分
        private CustomFilePropertiesPart GetCustomPropertiesPart(OpenXmlPackage doc)
        {
            if (doc is WordprocessingDocument wordDoc)
            {
                return wordDoc.CustomFilePropertiesPart;
            }
            else if (doc is SpreadsheetDocument excelDoc)
            {
                return excelDoc.CustomFilePropertiesPart;
            }
            else if (doc is PresentationDocument pptDoc)
            {
                return pptDoc.CustomFilePropertiesPart;
            }
            return null;
        }

        // 添加自定义属性部分
        private CustomFilePropertiesPart AddCustomPropertiesPart(OpenXmlPackage doc)
        {
            if (doc is WordprocessingDocument wordDoc)
            {
                return wordDoc.AddCustomFilePropertiesPart();
            }
            else if (doc is SpreadsheetDocument excelDoc)
            {
                return excelDoc.AddCustomFilePropertiesPart();
            }
            else if (doc is PresentationDocument pptDoc)
            {
                return pptDoc.AddCustomFilePropertiesPart();
            }
            return null;
        }
    }
}
