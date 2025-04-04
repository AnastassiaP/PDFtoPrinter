using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PDFtoPrinter
{
    public partial class PDFtoPrinterPrinter
    {
        private static string GetUtilPath(string utilName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows-specific logic
                string utilLocation = Path.Combine(AppContext.BaseDirectory, utilName);

                return File.Exists(utilLocation)
                    ? utilLocation
                    : Path.Combine(
                        Path.GetDirectoryName((Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).Location),
                        utilName);
            }
            else
            {
                // Non-Windows logic
                if (Path.IsPathRooted(utilName))
                {
                    return utilName;
                }

                string baseUtilPath = Environment.GetEnvironmentVariable("UTIL_PATH");
                if (!string.IsNullOrEmpty(baseUtilPath))
                {
                    return Path.Combine(baseUtilPath, utilName);
                }

                string defaultPath = Path.Combine("/usr/bin", utilName);
                if (File.Exists(defaultPath))
                {
                    return defaultPath;
                }

                return utilName;
            }
        }

        private static string UtilName =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "PDFtoPrinter_m.exe" : "lp";
    }
}
