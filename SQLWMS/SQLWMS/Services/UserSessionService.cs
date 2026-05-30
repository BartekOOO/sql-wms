using System.IO;

namespace SQLWMS.Services
{
    internal sealed class UserSessionService
    {
        private static readonly string SessionDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SQLWMS");

        private static readonly string SessionFilePath = Path.Combine(SessionDirectoryPath, "current-user.txt");

        public string CurrentUser { get; private set; }

        public UserSessionService()
        {
            CurrentUser = LoadCurrentUser();
        }

        public bool HasUser => !string.IsNullOrWhiteSpace(CurrentUser);

        public void SaveCurrentUser(string userName)
        {
            string normalizedUser = userName.Trim();
            Directory.CreateDirectory(SessionDirectoryPath);
            File.WriteAllText(SessionFilePath, normalizedUser);
            CurrentUser = normalizedUser;
        }

        private static string LoadCurrentUser()
        {
            if (!File.Exists(SessionFilePath))
            {
                return string.Empty;
            }

            return File.ReadAllText(SessionFilePath).Trim();
        }
    }
}