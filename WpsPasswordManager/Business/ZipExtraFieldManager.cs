using System;
using System.IO;
using System.Text;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Business
{
    public class ZipExtraFieldManager
    {
        // ZIP文件结构常量
        private const uint ZIP_EOCD_SIGNATURE = 0x06054B50; // End of Central Directory signature "PK\x05\x06"
        private const uint ZIP_COMMENT_LENGTH_MAX = 65535; // ZIP注释最大长度
        private const string METADATA_MAGIC = "WPPM"; // WPS Password Manager Magic
        private const ushort METADATA_VERSION = 1; // 元数据格式版本

        /// <summary>
        /// 构建元数据块
        /// 格式: [Magic(4)] [Version(2)] [Type(1)] [Length(4)] [Data(N)] [Checksum(4)]
        /// </summary>
        public byte[] BuildMetadataBlock(byte type, string data)
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
        public bool TryParseMetadataBlock(byte[] data, out byte type, out string content)
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

        /// <summary>
        /// 定位ZIP文件中End of Central Directory的位置
        /// </summary>
        public long FindEndOfCentralDirectory(string filePath)
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
        public bool IsValidZipFile(string filePath)
        {
            string error;
            return IsValidZipFile(filePath, out error);
        }

        /// <summary>
        /// 检查文件是否为有效的ZIP文件或Office文档
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="errorMessage">输出错误信息</param>
        /// <returns>是否为有效的ZIP文件</returns>
        public bool IsValidZipFile(string filePath, out string errorMessage)
        {
            errorMessage = string.Empty;
            
            try
            {
                string extension = Path.GetExtension(filePath).ToLower();
                
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
                    // 文件太小，不可能是有效的ZIP文件
                    if (fs.Length < 4)
                    {
                        errorMessage = "文件太小或为空文件";
                        Logger.Warning($"文件可能是空白文件，不是有效的ZIP文件: {filePath}");
                        return false;
                    }

                    byte[] buffer = new byte[8];
                    fs.Read(buffer, 0, 8);

                    // 检查ZIP文件签名: 0x04034B50 ("PK\x03\x04")
                    uint zipSignature = BitConverter.ToUInt32(buffer, 0);
                    
                    if (zipSignature == 0x04034B50)
                    {
                        // 标准ZIP格式，允许写入
                        return true;
                    }
                    
                    // 检查是否为OLE格式的加密文档 (OLE header: D0 CF 11 E0 A1 B1 1A E1)
                    if (IsOleFormat(buffer))
                    {
                        // OLE格式的加密Office文档，允许写入
                        Logger.Debug($"文件 {filePath} 是OLE格式的加密Open XML文档");
                        return true;
                    }
                    
                    // 对于Office文档，如果没有ZIP签名也不是OLE格式，说明可能是新建的临时文件
                    if (extension == ".docx" || extension == ".xlsx" || extension == ".pptx")
                    {
                        errorMessage = "文档文件格式不正确，可能是新建的临时文件尚未保存";
                        Logger.Warning($"Office文档 {filePath} 不具有有效的ZIP签名或OLE格式，可能是新建的临时文件");
                    }
                    else
                    {
                        errorMessage = "文件格式不正确";
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"验证文件有效性时出错: {ex.Message}";
                Logger.Error($"验证ZIP文件有效性时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查文件是否为OLE格式
        /// OLE格式的魔术字节: D0 CF 11 E0 A1 B1 1A E1
        /// </summary>
        private bool IsOleFormat(byte[] header)
        {
            if (header.Length < 8)
                return false;
            
            byte[] oleSignature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            for (int i = 0; i < 8; i++)
            {
                if (header[i] != oleSignature[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 检查文件是否为支持的Office文档（仅检查扩展名和文件存在性）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否为支持的Office文档</returns>
        private bool IsSupportedOfficeFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.Error($"文件不存在: {filePath}");
                    return false;
                }

                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    Logger.Warning($"文件为空: {filePath}");
                    return false;
                }

                string extension = Path.GetExtension(filePath).ToLower();
                return extension == ".docx" || extension == ".xlsx" || extension == ".pptx";
            }
            catch (Exception ex)
            {
                Logger.Error($"检查Office文件时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 查找元数据在文件中的起始位置
        /// </summary>
        public long FindMetadataStartPosition(string filePath)
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

        /// <summary>
        /// 将元数据写入ZIP文件尾部
        /// </summary>
        public bool AppendMetadataToFileEnd(string filePath, byte[] metadataBlock)
        {
            try
            {
                // 检查是否为有效的ZIP文件或Office文档
                string validationError;
                if (!IsValidZipFile(filePath, out validationError))
                {
                    Logger.Error($"文件不是有效的ZIP文件: {filePath}, 原因: {validationError}");
                    throw new Exception(validationError);
                }

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
                    fs.Flush();

                    Logger.Info($"元数据已成功写入到 {filePath} 的ZIP尾部");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"写入ZIP元数据失败: {ex.Message}");
                throw; // 重新抛出异常，让上层处理
            }
        }

        /// <summary>
        /// 从ZIP文件尾部读取元数据
        /// </summary>
        public bool ReadMetadataFromFileEnd(string filePath, out byte type, out string content)
        {
            return ReadMetadataFromFileEnd(filePath, 0, out type, out content);
        }

        /// <summary>
        /// 从ZIP文件尾部读取元数据，支持按类型过滤
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="filterType">要过滤的类型（0表示不过滤，返回第一个找到的）</param>
        /// <param name="type">输出的元数据类型</param>
        /// <param name="content">输出的元数据内容</param>
        public bool ReadMetadataFromFileEnd(string filePath, byte filterType, out byte type, out string content)
        {
            type = 0;
            content = null;

            try
            {
                string extension = Path.GetExtension(filePath).ToLower();
                
                // 对于Office文档，跳过严格的ZIP验证（加密文档可能没有标准ZIP签名）
                // 但仍需验证文件存在且非空
                if (!IsSupportedOfficeFile(filePath))
                {
                    Logger.Error($"文件不是有效的Office文档或文件不存在: {filePath}");
                    return false;
                }

                // 从ZIP文件尾部读取数据
                FileStream fs = null;
                try
                {
                    // 尝试以读写共享模式打开文件
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    Logger.Info("成功以读写共享模式打开文件");
                }
                catch (Exception ex1)
                {
                    try
                    {
                        // 如果失败，尝试以只读共享模式打开
                        fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        Logger.Info("成功以只读共享模式打开文件");
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error($"无法打开文件: {ex2.Message}");
                        return false;
                    }
                }

                using (fs)
                {
                    // 读取文件尾部的1KB数据，足够包含所有元数据
                    long fileLength = fs.Length;
                    long readSize = Math.Min(1024, fileLength);
                    long startPosition = fileLength - readSize;
                    
                    byte[] buffer = new byte[readSize];
                    fs.Seek(startPosition, SeekOrigin.Begin);
                    fs.Read(buffer, 0, (int)readSize);
                    
                    // 从后向前搜索元数据（从最后一个WPPM块开始，确保先找到密码再找UID）
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
                            System.Array.Copy(buffer, metadataBlockStart, metadataBlock, 0, metadataBlock.Length);
                            
                            if (TryParseMetadataBlock(metadataBlock, out type, out content))
                            {
                                // 如果指定了类型过滤，且当前类型不匹配，继续搜索
                                if (filterType != 0 && type != filterType)
                                {
                                    Logger.Debug($"找到元数据但类型不匹配: type={type}, filterType={filterType}，继续搜索...");
                                    continue;
                                }
                                return true;
                            }
                        }
                    }
                }
                
                Logger.Debug($"未找到元数据: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"读取ZIP元数据失败: {ex.Message}");
            }

            return false;
        }
    }
}