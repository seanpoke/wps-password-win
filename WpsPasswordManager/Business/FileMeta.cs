using System.Collections.Generic;

namespace WpsPasswordManager.Business
{
    public class FileMeta
    {
        public string FilePath { get; set; }            // 文件绝对路径 (作为唯一标识)
        public string Uid { get; set; }                // 文件权限标识
        public string CurrentPassword { get; set; }    // 旧密码：当前已确认生效的密码
        public SortedSet<string> PendingPasswordList { get; set; } // 待定密码：无障碍服务捕获到的新密码集合
        public string OwnerAccount { get; set; }       // 文档所属账号
        public string OwnerName { get; set; }          // 文档所属名称
        public bool ReadAuth { get; set; }             // 读权限
        public bool WriteAuth { get; set; }            // 写权限
        public bool IsModify { get; set; }            // 元数据是否已被修改

        public FileMeta(string filePath)
        {
            FilePath = filePath;
            PendingPasswordList = new SortedSet<string>();
        }

        public FileMeta(string filePath, string uid, string currentPassword, 
                       SortedSet<string> pendingPasswordList, string ownerAccount, 
                       string ownerName, bool readAuth, bool writeAuth)
        {
            FilePath = filePath;
            Uid = uid;
            CurrentPassword = currentPassword;
            PendingPasswordList = pendingPasswordList ?? new SortedSet<string>();
            OwnerAccount = ownerAccount;
            OwnerName = ownerName;
            ReadAuth = readAuth;
            WriteAuth = writeAuth;
        }

        public void AddPendingPassword(string password)
        {
            if (!string.IsNullOrEmpty(password))
            {
                PendingPasswordList.Add(password);
            }
        }

        public void ClearPendingPasswords()
        {
            PendingPasswordList.Clear();
        }

        public bool HasPendingPasswords()
        {
            return PendingPasswordList.Count > 0;
        }
    }
}