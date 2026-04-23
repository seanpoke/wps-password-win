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
                if (!IsValidZipFile(filePath))
                {
                    Logger.Error($"文件不是有效的ZIP文件: {filePath}");
                    return false;
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
                return false;
            }
        }

        /// <summary>
        /// 从ZIP文件尾部读取元数据
        /// </summary>
        public bool ReadMetadataFromFileEnd(string filePath, out byte type, out string content)
        {
            type = 0;
            content = null;

            try
            {
                // 检查是否为有效的ZIP文件
                if (!IsValidZipFile(filePath))
                {
                    Logger.Error($"文件不是有效的ZIP文件: {filePath}");
                    return false;
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
                            System.Array.Copy(buffer, metadataBlockStart, metadataBlock, 0, metadataBlock.Length);
                            
                            if (TryParseMetadataBlock(metadataBlock, out type, out content))
                            {
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