namespace SQLWMS
{
    internal static class DatabaseConnectionSettings
    {
        public const string Server = "Bartek";
        public const string Database = "Projekty";
        public const string Authentication = "NT";

        public static string ConnectionString { get; } =
            "Server=Bartek;Database=Projekty;Trusted_Connection=True;TrustServerCertificate=True;";

        public static string Summary =>
            $"Serwer: {Server} | Baza: {Database} | Logowanie: {Authentication}";
    }
}