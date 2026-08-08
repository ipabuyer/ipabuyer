namespace IPAbuyer.Core.Configuration
{
    public static class PassphraseStore
    {
        public static void Save(string passphrase) => ConfigurationStore.SavePassphrase(passphrase);

        public static string Get() => ConfigurationStore.GetPassphrase(null) ?? ConfigurationStore.GetDefaultPassphrase();

        public static string Rotate() => ConfigurationStore.RotateDefaultPassphrase();
    }
}
