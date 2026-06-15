using System;

namespace PasswordManager.Utils
{
    public class UrlParser
    {
        public static Uri ParseUserAddress(string userInput)
        {
            string address = userInput.Trim();

            if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                address = "https://" + address;
            }

            if (Uri.TryCreate(address, UriKind.Absolute, out Uri validatedUri))
            {
                return validatedUri;
            }

            throw new ArgumentException("服务器地址格式输入有误，请重新输入。");
        }

        public static string ExtractProtocol(string userInput)
        {
            string address = userInput.Trim();

            if (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "https";
            }

            if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return "http";
            }

            return "https";
        }

        public static string ExtractHost(string userInput)
        {
            try
            {
                Uri uri = ParseUserAddress(userInput);
                return uri.Host;
            }
            catch
            {
                string address = userInput.Trim();
                
                address = address.Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                                .Replace("https://", "", StringComparison.OrdinalIgnoreCase);
                
                int colonIndex = address.IndexOf(':');
                if (colonIndex > 0)
                {
                    return address.Substring(0, colonIndex);
                }
                
                int slashIndex = address.IndexOf('/');
                if (slashIndex > 0)
                {
                    return address.Substring(0, slashIndex);
                }
                
                return address;
            }
        }

        public static int ExtractPort(string userInput)
        {
            try
            {
                Uri uri = ParseUserAddress(userInput);
                return uri.Port;
            }
            catch
            {
                string address = userInput.Trim();
                
                address = address.Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                                .Replace("https://", "", StringComparison.OrdinalIgnoreCase);
                
                int colonIndex = address.IndexOf(':');
                if (colonIndex > 0 && colonIndex < address.Length - 1)
                {
                    string portPart = address.Substring(colonIndex + 1);
                    int slashIndex = portPart.IndexOf('/');
                    if (slashIndex > 0)
                    {
                        portPart = portPart.Substring(0, slashIndex);
                    }
                    if (int.TryParse(portPart, out int port))
                    {
                        return port;
                    }
                }
                
                return 8443;
            }
        }
    }
}