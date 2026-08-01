namespace IPAbuyer.Core.Configuration
{
    public static class IpatoolSettings
    {
        public const string FlavorMain = ConfigurationStore.IpatoolFlavorMain;
        public const string FlavorCustom = ConfigurationStore.IpatoolFlavorCustom;

        public static string GetFlavor() => ConfigurationStore.GetIpatoolFlavor();

        public static void SaveFlavor(string flavor) => ConfigurationStore.SaveIpatoolFlavor(flavor);

        public static string GetCustomPath() => ConfigurationStore.GetCustomIpatoolPath();

        public static bool HasUsableCustomPath() => ConfigurationStore.HasUsableCustomIpatoolPath();

        public static void SaveCustomPath(string path) => ConfigurationStore.SaveCustomIpatoolPath(path);

        public static void DeleteCustomPath() => ConfigurationStore.DeleteCustomIpatoolPath();
    }
}
