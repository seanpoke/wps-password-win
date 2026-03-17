using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.VariantTypes;
using System;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Business
{
    public class MetadataManager
    {
        private const string PasswordPropertyName = "WpsPasswordManager";

        // 写入密码到文档元数据
        public bool WritePasswordToMetadata(string filePath, string password)
        {
            // 检查文件扩展名
            string extension = System.IO.Path.GetExtension(filePath).ToLower();
            if (!IsSupportedFormat(extension))
            {
                Logger.Warning($"不支持的文件格式: {extension}");
                return false;
            }

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    // 根据文件类型打开文档
                    using (var doc = OpenDocument(filePath, true))
                    {
                        if (doc != null)
                        {
                            // 获取自定义属性部分
                            CustomFilePropertiesPart customProps = GetCustomPropertiesPart(doc);
                            if (customProps == null)
                            {
                                customProps = AddCustomPropertiesPart(doc);
                                customProps.Properties = new Properties();
                            }

                            // 创建或更新密码属性
                            var passwordProp = customProps.Properties.Elements<CustomDocumentProperty>()
                                .FirstOrDefault(p => p.Name.Value == PasswordPropertyName);

                            if (passwordProp == null)
                            {
                                passwordProp = new CustomDocumentProperty();
                                passwordProp.Name = PasswordPropertyName;
                                passwordProp.VTLPWSTR = new VTLPWSTR() { Text = password };
                                customProps.Properties.Append(passwordProp);
                            }
                            else
                            {
                                passwordProp.VTLPWSTR.Text = password;
                            }

                            customProps.Properties.Save();
                            Logger.Info($"密码已成功写入到 {filePath} 的元数据中");
                            return true;
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

        // 从文档元数据读取密码
        public string ReadPasswordFromMetadata(string filePath)
        {
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
                    // 根据文件类型打开文档
                    using (var doc = OpenDocument(filePath, false))
                    {
                        if (doc != null)
                        {
                            CustomFilePropertiesPart customProps = GetCustomPropertiesPart(doc);
                            if (customProps != null)
                            {
                                var passwordProp = customProps.Properties.Elements<CustomDocumentProperty>()
                                    .FirstOrDefault(p => p.Name.Value == PasswordPropertyName);

                                if (passwordProp != null && passwordProp.VTLPWSTR != null)
                                {
                                    string password = passwordProp.VTLPWSTR.Text;
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

        // 根据文件类型打开文档
        private OpenXmlPackage OpenDocument(string filePath, bool isEditable)
        {
            string extension = System.IO.Path.GetExtension(filePath).ToLower();
            
            try
            {
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
                        return WordprocessingDocument.Open(filePath, isEditable);
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