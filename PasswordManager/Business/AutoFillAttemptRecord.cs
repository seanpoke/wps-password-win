using System;

namespace PasswordManager.Business
{
    public class AutoFillAttemptRecord
    {
        public string FilePath { get; }
        public bool HasAttempted { get; set; }

        public AutoFillAttemptRecord(string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            HasAttempted = true;
        }
    }
}