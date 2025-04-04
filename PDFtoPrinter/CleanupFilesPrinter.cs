using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;

namespace PDFtoPrinter
{
    /// <summary>
    /// Deletes files after printing. Doesn't print files by itself but uses an inner printer.
    /// </summary>
    public class CleanupFilesPrinter : IPrinter
    {
        public delegate void OnCleanupFailedHandler(QueuedFile file, Exception exception);
        public static event OnCleanupFailedHandler OnCleanupFailed;

        private static readonly IDictionary<string, ConcurrentQueue<QueuedFile>> printingQueues =
            new ConcurrentDictionary<string, ConcurrentQueue<QueuedFile>>();

        private static readonly object locker = new object();
        private static readonly Timer cleanupTimer = new Timer(1000)
        {
            AutoReset = true,
            Enabled = true
        };

        private static bool deletingInProgress = false;

        private readonly IPrinter inner;
        private readonly bool waitFileDeletion;

        static CleanupFilesPrinter()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                cleanupTimer.Elapsed += CleanupTimerElapsed;
            }
        }

        public CleanupFilesPrinter(IPrinter inner, bool waitFileDeletion = false)
        {
            this.inner = inner;
            this.waitFileDeletion = waitFileDeletion;
        }

        public async Task Print(PrintingOptions options, TimeSpan? timeout = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("CleanupFilesPrinter is only supported on Windows.");
            }

            await this.inner.Print(options, timeout);
            var task = EnqueuePrintingFile(options.PrinterName, options.FilePath);
            if (waitFileDeletion)
            {
                await task;
            }
        }

        private static Task EnqueuePrintingFile(string printerName, string filePath)
        {
            var queue = GetQueue(printerName);
            var file = new QueuedFile(filePath);
            queue.Enqueue(file);
            return file.TaskCompletionSource.Task;
        }

        private static ConcurrentQueue<QueuedFile> GetQueue(string printerName)
        {
            if (!printingQueues.ContainsKey(printerName))
            {
                lock (locker)
                {
                    if (!printingQueues.ContainsKey(printerName))
                    {
                        printingQueues[printerName] = new ConcurrentQueue<QueuedFile>();
                    }
                }
            }
            return printingQueues[printerName];
        }

        private static void CleanupTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || deletingInProgress)
            {
                return;
            }

            deletingInProgress = true;
            try
            {
                CleanupPrintedFiles(printingQueues);
            }
            catch (Exception)
            {
                // Optional: log or handle
            }
            finally
            {
                deletingInProgress = false;
            }
        }

        private static void CleanupPrintedFiles(IDictionary<string, ConcurrentQueue<QueuedFile>> queues)
        {
            // Dynamically load types inside method to avoid type loading on non-Windows
            var printServerType = Type.GetType("System.Printing.PrintServer, ReachFramework");
            var printQueueType = Type.GetType("System.Printing.PrintQueue, ReachFramework");

            if (printServerType == null || printQueueType == null)
                return;

            var printServer = (IDisposable)Activator.CreateInstance(printServerType);
            foreach (var kv in queues)
            {
                var method = printServerType.GetMethod("GetPrintQueue", new[] { typeof(string) });
                var printQueue = method?.Invoke(printServer, new object[] { kv.Key });
                if (printQueue == null) continue;

                DeletePrintedFiles(kv.Value, printQueue, printQueueType);
                (printQueue as IDisposable)?.Dispose();
            }
        }

        private static void DeletePrintedFiles(
            ConcurrentQueue<QueuedFile> files,
            object printQueue,
            Type printQueueType)
        {
            var getJobsMethod = printQueueType.GetMethod("GetPrintJobInfoCollection");
            var jobs = getJobsMethod?.Invoke(printQueue, null) as System.Collections.IEnumerable;

            var jobNames = new HashSet<string>();
            foreach (var job in jobs ?? Array.Empty<object>())
            {
                var nameProp = job.GetType().GetProperty("Name");
                if (nameProp != null)
                {
                    var name = nameProp.GetValue(job)?.ToString()?.ToUpper();
                    if (!string.IsNullOrEmpty(name))
                    {
                        jobNames.Add(name);
                    }
                }
            }

            while (!files.IsEmpty)
            {
                files.TryPeek(out QueuedFile currentFile);
                if (jobNames.Contains(Path.GetFileName(currentFile.Path).ToUpper()))
                {
                    break;
                }

                files.TryDequeue(out QueuedFile dequeuedFile);
                try
                {
                    File.Delete(dequeuedFile.Path);
                    dequeuedFile.TaskCompletionSource.SetResult(dequeuedFile.Path);
                }
                catch (Exception ex)
                {
                    OnCleanupFailed?.Invoke(dequeuedFile, ex);
                }
            }
        }
    }
}
