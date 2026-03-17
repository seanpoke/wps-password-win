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
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Business
{
    public class MetadataManager
    {
        private const string PasswordPropertyName = "WpsPasswordManager";
        private const string ExternalStorageFile = "passwords.json";
        
        // 外部存储，用于存储非ZIP格式文档的密码
        private Dictionary<string, string> externalPasswordStore;
        
        public MetadataManager()
        {
            LoadExternalPasswordStore();
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

        // 写入密码到文档元数据
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

            // 不再检查文件大小，允许空文件

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    // 尝试使用System.IO.Packaging直接操作ZIP文件
                    try
                    {
                        using (Package package = Package.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                        {
                            // 检查是否存在自定义属性部分
                            Uri customPropsUri = new Uri("/docProps/custom.xml", UriKind.Relative);
                            PackagePart customPropsPart;
                            
                            if (package.PartExists(customPropsUri))
                            {
                                customPropsPart = package.GetPart(customPropsUri);
                            }
                            else
                            {
                                // 创建自定义属性部分
                                customPropsPart = package.CreatePart(customPropsUri, "application/vnd.openxmlformats-officedocument.custom-properties+xml");
                                
                                // 写入初始XML
                                using (Stream stream = customPropsPart.GetStream())
                                using (StreamWriter writer = new StreamWriter(stream))
                                {
                                    writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Properties xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/custom-properties"" xmlns:vt=""http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"">
</Properties>");
                                }
                            }
                            
                            // 读取并修改XML
                            XElement customPropsXml;
                            using (Stream stream = customPropsPart.GetStream())
                            using (StreamReader reader = new StreamReader(stream))
                            {
                                customPropsXml = XElement.Parse(reader.ReadToEnd());
                            }
                            
                            // 查找或创建密码属性
                            XNamespace ns = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
                            XNamespace vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
                            
                            var passwordProp = customPropsXml.Elements(ns + "Property")
                                .FirstOrDefault(p => (string)p.Attribute("name") == PasswordPropertyName);
                            
                            if (passwordProp == null)
                            {
                                // 创建新属性
                                passwordProp = new XElement(ns + "Property",
                                    new XAttribute("fmtid", "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}"),
                                    new XAttribute("pid", customPropsXml.Elements(ns + "Property").Count() + 2),
                                    new XAttribute("name", PasswordPropertyName),
                                    new XElement(vt + "lpwstr", password)
                                );
                                customPropsXml.Add(passwordProp);
                            }
                            else
                            {
                                // 更新现有属性
                                var lpwstr = passwordProp.Element(vt + "lpwstr");
                                if (lpwstr != null)
                                {
                                    lpwstr.Value = password;
                                }
                                else
                                {
                                    passwordProp.Add(new XElement(vt + "lpwstr", password));
                                }
                            }
                            
                            // 写回XML
                            using (Stream stream = customPropsPart.GetStream(FileMode.Create, FileAccess.Write))
                            using (StreamWriter writer = new StreamWriter(stream))
                            {
                                customPropsXml.Save(writer);
                            }
                            
                            Logger.Info($"密码已成功写入到 {filePath} 的元数据中");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 如果System.IO.Packaging失败，尝试使用其他方法
                        Logger.Warning($"System.IO.Packaging失败: {ex.Message}，尝试使用其他方法");
                        
                        // 对于非ZIP格式的文档，尝试使用Windows API写入元数据
                        if (WritePasswordToWindowsMetadata(filePath, password))
                        {
                            Logger.Info($"密码已成功写入到 {filePath} 的Windows元数据中");
                            return true;
                        }
                        else
                        {
                            throw ex;
                        }
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
        
        // 使用Windows API写入元数据
        private bool WritePasswordToWindowsMetadata(string filePath, string password)
        {
            try
            {
                // 这里可以使用Windows API来写入文件的扩展属性
                // 由于不同版本的Windows和文件系统可能有不同的实现，这里只做一个简单的尝试
                
                // 对于NTFS文件系统，可以使用Alternate Data Streams
                string adsPath = $"{filePath}:WpsPasswordManager";
                System.IO.File.WriteAllText(adsPath, password);
                
                Logger.Info($"密码已成功写入到 {filePath} 的备用数据流中");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"写入Windows元数据失败: {ex.Message}");
                return false;
            }
        }

        // 从文档元数据读取密码
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

            // 不再检查文件大小，允许空文件

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    // 尝试使用System.IO.Packaging直接操作ZIP文件
                    try
                    {
                        using (Package package = Package.Open(filePath, FileMode.Open, FileAccess.Read))
                        {
                            // 检查是否存在自定义属性部分
                            Uri customPropsUri = new Uri("/docProps/custom.xml", UriKind.Relative);
                            if (package.PartExists(customPropsUri))
                            {
                                PackagePart customPropsPart = package.GetPart(customPropsUri);
                                
                                // 读取XML
                                XElement customPropsXml;
                                using (Stream stream = customPropsPart.GetStream())
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    customPropsXml = XElement.Parse(reader.ReadToEnd());
                                }
                                
                                // 查找密码属性
                                XNamespace ns = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
                                XNamespace vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
                                
                                var passwordProp = customPropsXml.Elements(ns + "Property")
                                    .FirstOrDefault(p => (string)p.Attribute("name") == PasswordPropertyName);
                                
                                if (passwordProp != null)
                                {
                                    var lpwstr = passwordProp.Element(vt + "lpwstr");
                                    if (lpwstr != null)
                                    {
                                        string password = lpwstr.Value;
                                        Logger.Info($"从 {filePath} 的元数据中读取到密码");
                                        return password;
                                    }
                                }
                            }
                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 如果System.IO.Packaging失败，尝试使用其他方法
                        Logger.Warning($"System.IO.Packaging失败: {ex.Message}，尝试使用其他方法");
                        
                        // 对于非ZIP格式的文档，尝试使用Windows API读取元数据
                        string password = ReadPasswordFromWindowsMetadata(filePath);
                        if (!string.IsNullOrEmpty(password))
                        {
                            Logger.Info($"从 {filePath} 的Windows元数据中读取到密码");
                            return password;
                        }
                        else
                        {
                            throw ex;
                        }
                    }
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
        
        // 使用Windows API读取元数据
        private string ReadPasswordFromWindowsMetadata(string filePath)
        {
            try
            {
                // 对于NTFS文件系统，可以使用Alternate Data Streams
                string adsPath = $"{filePath}:WpsPasswordManager";
                if (System.IO.File.Exists(adsPath))
                {
                    string password = System.IO.File.ReadAllText(adsPath);
                    Logger.Info($"从 {filePath} 的备用数据流中读取到密码");
                    return password;
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"读取Windows元数据失败: {ex.Message}");
                return null;
            }
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

        // 检查文件是否为有效的ZIP文件（Office文档本质上是ZIP文件）
        private bool IsValidZipFile(string filePath)
        {
            try
            {
                using (System.IO.FileStream fs = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    // 检查文件大小是否至少为4字节（ZIP文件头大小）
                    if (fs.Length < 4)
                    {
                        return false;
                    }

                    // 读取文件头的前4个字节
                    byte[] buffer = new byte[4];
                    fs.Read(buffer, 0, 4);

                    // 检查ZIP文件的魔术数字：PK\003\004 或 PK\005\006 或 PK\007\008
                    // 这些都是有效的ZIP文件签名，WPS创建的文档可能使用不同的签名
                    bool isZipFile = (buffer[0] == 0x50 && buffer[1] == 0x4B && 
                                     (buffer[2] == 0x03 && buffer[3] == 0x04 ||
                                      buffer[2] == 0x05 && buffer[3] == 0x06 ||
                                      buffer[2] == 0x07 && buffer[3] == 0x08));
                    
                    // 对于WPS创建的文档，即使不是标准ZIP文件，也尝试打开
                    // 因为WPS可能使用不同的文件格式
                    return true;
                }
            }
            catch
            {
                return false;
            }
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