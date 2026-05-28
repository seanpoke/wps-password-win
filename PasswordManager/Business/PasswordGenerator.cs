using System;
using System.Collections.Generic;
using System.Linq;

namespace PasswordManager.Business
{
    public class PasswordGenerator
    {
        private const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string LowercaseLetters = "abcdefghijklmnopqrstuvwxyz";
        private const string Numbers = "0123456789";
        private const string SpecialCharacters = "!@#$%^&*";
        private const int PasswordLength = 10;
        private const int MinCharsPerCategory = 2;

        private readonly Random _random;

        public PasswordGenerator()
        {
            _random = new Random();
        }

        public string GeneratePassword()
        {
            List<char> passwordChars = new List<char>();

            // 确保每个类别至少有2个字符
            AddCharsFromCategory(passwordChars, UppercaseLetters, MinCharsPerCategory);
            AddCharsFromCategory(passwordChars, LowercaseLetters, MinCharsPerCategory);
            AddCharsFromCategory(passwordChars, Numbers, MinCharsPerCategory);
            AddCharsFromCategory(passwordChars, SpecialCharacters, MinCharsPerCategory);

            // 填充剩余字符
            string allChars = UppercaseLetters + LowercaseLetters + Numbers + SpecialCharacters;
            while (passwordChars.Count < PasswordLength)
            {
                char c = allChars[_random.Next(allChars.Length)];
                if (!HasConsecutiveCharacters(passwordChars, c) && !HasSimilarCharacters(passwordChars, c))
                {
                    passwordChars.Add(c);
                }
            }

            // 打乱顺序
            return new string(passwordChars.OrderBy(x => _random.Next()).ToArray());
        }

        private void AddCharsFromCategory(List<char> passwordChars, string category, int count)
        {
            for (int i = 0; i < count; i++)
            {
                char c;
                do
                {
                    c = category[_random.Next(category.Length)];
                } while (HasConsecutiveCharacters(passwordChars, c) || HasSimilarCharacters(passwordChars, c));
                
                passwordChars.Add(c);
            }
        }

        private bool HasConsecutiveCharacters(List<char> passwordChars, char newChar)
        {
            if (passwordChars.Count == 0)
                return false;
            
            return passwordChars.Last() == newChar;
        }

        private bool HasSimilarCharacters(List<char> passwordChars, char newChar)
        {
            // 相似字符对
            HashSet<char> similarCharsSet = new HashSet<char> { '1', 'l', '0', 'O', 'I' };
            Dictionary<char, char> similarChars = new Dictionary<char, char>
            {
                {'1', 'l'},
                {'0', 'O'},
                {'I', 'l'}
            };

            if (similarChars.ContainsKey(newChar))
            {
                return passwordChars.Contains(similarChars[newChar]);
            }
            else if (similarChars.ContainsValue(newChar))
            {
                // 检查反向映射
                foreach (var pair in similarChars)
                {
                    if (pair.Value == newChar)
                    {
                        return passwordChars.Contains(pair.Key);
                    }
                }
            }
            
            return false;
        }
    }
}