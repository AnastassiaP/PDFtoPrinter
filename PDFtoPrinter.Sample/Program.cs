using System;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace PDFtoPrinter.Sample
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            try
            {
                // First get available printers
                var printers = await PrintersQuery.RunAsync();
                if (!printers.Any())
                {
                    Console.WriteLine("No printers found!");
                    return;
                }

                // Print printer names
                Console.WriteLine("Available printers:");
                foreach (var printer in printers)
                {
                    Console.WriteLine($"- {printer.Name}");
                }

                // Select first printer
                string printerName = printers[0].Name;
                Console.WriteLine($"\nUsing printer: {printerName}");

                // Create printer instance
                var wrapper = new PDFtoPrinterPrinter(5);

                // Print test file
                string testFile = "somefile.pdf"; // Make sure this file exists
                Console.WriteLine($"Printing file: {testFile}");

                await wrapper.Print(new PrintingOptions(
                    printerName,
                    testFile
                ));

                Console.WriteLine("Print job completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"OS: {GetOSDescription()}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static string GetOSDescription()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macOS";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";
            return "Unknown";
        }
    }
}
