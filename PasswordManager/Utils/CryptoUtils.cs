using System;
using System.Security.Cryptography;

namespace PasswordManager.Utils
{
    public static class CryptoUtils
    {
        public static string EncryptPasswordByPublicKey(string password, string publicKeyBase64)
        {
            if (string.IsNullOrEmpty(password))
            {
                Logger.Warning("加密密码为空");
                return null;
            }

            if (string.IsNullOrEmpty(publicKeyBase64))
            {
                Logger.Error("公钥为空，无法加密密码");
                return null;
            }

            try
            {
                byte[] publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
                byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

                using (var ecdh = ECDiffieHellman.Create())
                {
                    ecdh.GenerateKey(ECCurve.NamedCurves.nistP256);

                    using (var otherEcdh = ECDiffieHellman.Create())
                    {
                        otherEcdh.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
                        
                        byte[] aesKeyBytes = ecdh.DeriveKeyFromHash(otherEcdh.PublicKey, 
                            HashAlgorithmName.SHA256, null, null);

                        using (var aes = Aes.Create())
                        {
                            aes.Key = aesKeyBytes;
                            aes.GenerateIV();
                            aes.Mode = CipherMode.CBC;
                            aes.Padding = PaddingMode.PKCS7;

                            using (var encryptor = aes.CreateEncryptor())
                            {
                                byte[] encryptedPassword = encryptor.TransformFinalBlock(passwordBytes, 0, passwordBytes.Length);

                                byte[] tempPublicKeyBytes = ecdh.ExportSubjectPublicKeyInfo();
                                
                                int resultLength = 4 + tempPublicKeyBytes.Length + 16 + encryptedPassword.Length;
                                byte[] result = new byte[resultLength];

                                result[0] = (byte)((tempPublicKeyBytes.Length >> 24) & 0xFF);
                                result[1] = (byte)((tempPublicKeyBytes.Length >> 16) & 0xFF);
                                result[2] = (byte)((tempPublicKeyBytes.Length >> 8) & 0xFF);
                                result[3] = (byte)(tempPublicKeyBytes.Length & 0xFF);

                                Buffer.BlockCopy(tempPublicKeyBytes, 0, result, 4, tempPublicKeyBytes.Length);

                                Buffer.BlockCopy(aes.IV, 0, result, 4 + tempPublicKeyBytes.Length, 16);

                                Buffer.BlockCopy(encryptedPassword, 0, result, 4 + tempPublicKeyBytes.Length + 16, encryptedPassword.Length);

                                Logger.Info("密码使用公钥加密成功");
                                return Convert.ToBase64String(result);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"密码加密失败: {ex.Message}");
                return null;
            }
        }

        public static string EncryptPasswordByPublicKey(string password)
        {
            return EncryptPasswordByPublicKey(password, GlobalState.Instance.PublicKey);
        }
    }
}