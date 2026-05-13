using System;

namespace WpsPasswordManager.Business
{
    public class AutoFillAttemptRecord
    {
        public string FilePath { get; }
        public string FileUid { get; set; }
        public DateTime AttemptTime { get; private set; }
        public bool Success { get; set; }
        public string PasswordHint { get; set; }
        public int AttemptCount { get; set; }

        public AutoFillAttemptRecord(string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            AttemptTime = DateTime.Now;
            Success = false;
            AttemptCount = 1;
        }

        public AutoFillAttemptRecord(string filePath, string fileUid, bool success, string passwordHint)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            FileUid = fileUid;
            AttemptTime = DateTime.Now;
            Success = success;
            PasswordHint = passwordHint;
            AttemptCount = 1;
        }

        public void MarkSuccess(string passwordHint = null)
        {
            Success = true;
            PasswordHint = passwordHint;
        }

        public void IncrementAttemptCount()
        {
            AttemptCount++;
            AttemptTime = DateTime.Now;
        }
    }
}