using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PDFtoPrinter
{
    public sealed partial class PrintersQuery
    {
        public static async Task<PrinterResponse[]> RunAsync()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await Task.Run(QueryWindowsPrinters); // wrap sync in async
            }
            else
            {
                return await QueryUnixPrintersAsync();
            }
        }

        // Windows implementation (System.Management)
        private static PrinterResponse[] QueryWindowsPrinters()
        {
            var printers = new List<PrinterResponse>();

            try
            {
                var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_Printer");
                foreach (System.Management.ManagementBaseObject printer in searcher.Get())
                {
                    var name = printer["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        printers.Add(new PrinterResponse(name));
                    }
                }
            }
            catch (Exception ex)
            {
                throw new PlatformNotSupportedException("Failed to query printers using WMI on Windows.", ex);
            }

            return printers.ToArray();
        }

        // macOS/Linux implementation (`lpstat -a`)
        private static async Task<PrinterResponse[]> QueryUnixPrintersAsync()
        {
            var printers = new List<PrinterResponse>();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "lpstat",
                    Arguments = "-a",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string line;
            while ((line = await process.StandardOutput.ReadLineAsync()) != null)
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    printers.Add(new PrinterResponse(parts[0]));
                }
            }

            return printers.ToArray();
        }
    }
}
