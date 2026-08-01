namespace IPAbuyer.Core.Configuration
{
    public static class ConfigurationBootstrapper
    {
        public static void Initialize()
        {
            ConfigurationStore.InitializeDatabase();
        }
    }

    public static class DevelopmentAccountRules
    {
        public static bool IsMockAccount(string? username, string? password)
        {
            return ConfigurationStore.IsMockAccount(username, password);
        }
    }
}
