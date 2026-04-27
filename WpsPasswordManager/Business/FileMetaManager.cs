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

        /// <summary>
        /// 从文件中读取密码
        /// </summary>
        public string ReadPasswordFromFile(string filePath)
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return null;
            }

            // 检查文件扩展名
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
                        if (type == 1) // Type 1 = Password
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

        /// <summary>
        /// 从文件中读取UID
        /// </summary>
        public string ReadUidFromFile(string filePath)
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return null;
            }

            // 检查文件扩展名
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
                        if (type == 2) // Type 2 = UID
                        {
                            Logger.Info($"从 {filePath} 的ZIP尾部读取到UID: {content}");
                            // 更新FileMeta中的UID
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

        /// <summary>
        /// 写入密码到文件
        /// </summary>
        public bool WritePasswordToFile(string filePath, string password)
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return false;
            }

            // 检查文件扩展名
            string extension = Path.GetExtension(filePath).ToLower();
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
                    // 构建密码元数据块
                    byte[] metadataBlock = zipExtraFieldManager.BuildMetadataBlock(1, password); // Type 1 = Password
                    
                    // 写入到ZIP文件尾部
                    if (zipExtraFieldManager.AppendMetadataToFileEnd(filePath, metadataBlock))
                    {
                        // 更新FileMeta中的当前密码
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

        /// <summary>
        /// 写入UID到文件
        /// </summary>
        public bool WriteUidToFile(string filePath, string uid)
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                Logger.Error($"文件不存在: {filePath}");
                return false;
            }

            // 检查文件扩展名
            string extension = Path.GetExtension(filePath).ToLower();
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
                    // 构建UID元数据块
                    byte[] metadataBlock = zipExtraFieldManager.BuildMetadataBlock(2, uid); // Type 2 = UID
                    
                    // 写入到ZIP文件尾部
                    if (zipExtraFieldManager.AppendMetadataToFileEnd(filePath, metadataBlock))
                    {
                        // 更新FileMeta中的UID
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

        /// <summary>
        /// 写入元数据到文件
        /// </summary>
        public bool WriteMetaDataToFile(string filePath)
        {
            // 获取FileMeta实例
            var fileMeta = fileMetaFactory.GetFileMeta(filePath);
            if (fileMeta == null)
            {
                Logger.Error($"未找到文件 {filePath} 的元数据");
                return false;
            }

            // 获取写入密码
            string password = fileMetaFactory.GetWritePassword(filePath);
            if (!string.IsNullOrEmpty(password))
            {
                // 写入密码
                if (!WritePasswordToFile(filePath, password))
                {
                    return false;
                }
            }

            // 写入UID
            if (!string.IsNullOrEmpty(fileMeta.Uid))
            {
                if (!WriteUidToFile(filePath, fileMeta.Uid))
                {
                    return false;
                }
            }
            else
            {
                // 生成并写入新的UID
                string newUid = GenerateUid();
                if (!WriteUidToFile(filePath, newUid))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 检查文件是否有密码元数据
        /// </summary>
        public bool HasPasswordMetadata(string filePath)
        {
            return !string.IsNullOrEmpty(ReadPasswordFromFile(filePath));
        }

        /// <summary>
        /// 检查是否支持的文件格式
        /// </summary>
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

        /// <summary>
        /// 生成UID
        /// </summary>
        private string GenerateUid()
        {
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            string uuid = Guid.NewGuid().ToString();
            return $"{timestamp}_{uuid}";
        }

        /// <summary>
        /// 获取文档的UID
        /// </summary>
        public string GetDocumentUid(string filePath)
        {
            Logger.Info($"开始获取文档UID: {filePath}");
            // 首先尝试从元数据中读取UID
            string uid = ReadUidFromFile(filePath);
            if (!string.IsNullOrEmpty(uid))
            {
                Logger.Info($"从元数据中读取到UID: {uid}");
                return uid;
            }

            // 如果元数据中没有UID，生成新的UID
            string newUid = GenerateUid();
            Logger.Info($"生成新的UID: {newUid}");
            return newUid;
        }

        /// <summary>
        /// 保存文档的UID到元数据
        /// </summary>
        public bool SaveDocumentUid(string filePath)
        {
            // 获取文档的UID
            string uid = GetDocumentUid(filePath);
            if (!string.IsNullOrEmpty(uid))
            {
                // 写入到元数据
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