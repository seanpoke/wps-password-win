using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.VariantTypes;
using System;

namespace WpsPasswordManager.Business
{
    public class MetadataManager
    {
        private const string PasswordPropertyName = "WpsPasswordManager";

        // 写入密码到文档元数据
        public bool WritePasswordToMetadata(string filePath, string password)
        {
            if (!filePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                return false;

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, true))
                    {
                        // 获取自定义属性部分
                        CustomFilePropertiesPart customProps = doc.CustomFilePropertiesPart;
                        if (customProps == null)
                        {
                            customProps = doc.AddCustomFilePropertiesPart();
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
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"写入元数据失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            return false;
        }

        // 从文档元数据读取密码
        public string ReadPasswordFromMetadata(string filePath)
        {
            if (!filePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                return null;

            int retryCount = 3;
            int delayMs = 500;

            while (retryCount > 0)
            {
                try
                {
                    using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
                    {
                        CustomFilePropertiesPart customProps = doc.CustomFilePropertiesPart;
                        if (customProps != null)
                        {
                            var passwordProp = customProps.Properties.Elements<CustomDocumentProperty>()
                                .FirstOrDefault(p => p.Name.Value == PasswordPropertyName);

                            if (passwordProp != null && passwordProp.VTLPWSTR != null)
                            {
                                return passwordProp.VTLPWSTR.Text;
                            }
                        }
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"读取元数据失败: {ex.Message}");
                    retryCount--;
                    if (retryCount > 0)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            return null;
        }

        // 检查文档是否有密码元数据
        public bool HasPasswordMetadata(string filePath)
        {
            return !string.IsNullOrEmpty(ReadPasswordFromMetadata(filePath));
        }
    }
}