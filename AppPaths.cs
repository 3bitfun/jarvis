namespace JARVIS;

internal static class AppPaths
{
    public static string DataDirectory
    {
        get
        {
            var custom = Environment.GetEnvironmentVariable("JARVIS_DATA_DIR");
            return !string.IsNullOrWhiteSpace(custom) && Path.IsPathFullyQualified(custom)
                ? Path.GetFullPath(custom) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JARVIS");
        }
    }
}
