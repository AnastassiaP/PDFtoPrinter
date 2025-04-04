using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PDFtoPrinter
{
    public partial class SystemProcessFactory : IProcessFactory
    {
        /// <inheritdoc/>
        public IProcess Create(string executablePath, PrintingOptions options)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new Process
                {
                    StartInfo =
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = executablePath,
                        Arguments = $"{options} /s",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
            }
            else
            {
                // 🛠️ Build args safely
                string args = "";

                if (!string.IsNullOrWhiteSpace(options.PrinterName))
                {
                    args += $" -d \"{options.PrinterName}\"";
                }

                if (!string.IsNullOrWhiteSpace(options.Pages))
                {
                    args += $" -P \"{options.Pages}\"";
                }

                if (options.Copies.HasValue)
                {
                    args += $" -n {options.Copies}";
                }

                if (!string.IsNullOrWhiteSpace(options.Focus))
                {
                    args += $" -t \"{options.Focus}\"";
                }

                // ⚠️ Make sure file exists and quote the path
                if (string.IsNullOrWhiteSpace(options.FilePath) || !System.IO.File.Exists(options.FilePath))
                {
                    throw new ArgumentException($"File does not exist: {options.FilePath}");
                }

                args += $" \"{options.FilePath}\"";
                args = args.Trim();

                Console.WriteLine($"[DEBUG] Running: {executablePath} {args}"); // Optional debug log

                return new Process
                {
                    StartInfo =
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = executablePath,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
            }
        }
    }
}
