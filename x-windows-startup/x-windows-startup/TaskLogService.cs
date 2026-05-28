using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace x_windows_startup
{
    public class TaskLogService
    {
        private readonly object syncRoot = new object();
        private readonly string logDirectory;

        public TaskLogService(string baseDirectory)
        {
            logDirectory = Path.Combine(baseDirectory, "logs");
        }

        public void AppendInfo(StartupTask task, string message)
        {
            Append(task, "INFO", message);
        }

        public void AppendError(StartupTask task, string message)
        {
            Append(task, "ERROR", message);
        }

        public void AppendOutput(StartupTask task, string message)
        {
            Append(task, "OUT", message);
        }

        public void AppendSeparator(StartupTask task)
        {
            lock (syncRoot)
            {
                Directory.CreateDirectory(logDirectory);
                File.AppendAllText(GetLogPath(task), "======" + Environment.NewLine, new UTF8Encoding(false));
            }
        }

        public void OpenLog(StartupTask task)
        {
            var logPath = GetLogPath(task);
            Directory.CreateDirectory(logDirectory);
            if (!File.Exists(logPath))
            {
                File.WriteAllText(logPath, string.Empty, new UTF8Encoding(false));
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = "\"" + logPath + "\"",
                UseShellExecute = false
            });
        }

        public int ClearAll()
        {
            lock (syncRoot)
            {
                if (!Directory.Exists(logDirectory))
                {
                    return 0;
                }

                var deletedCount = 0;
                foreach (var filePath in Directory.GetFiles(logDirectory, "*.txt"))
                {
                    File.Delete(filePath);
                    deletedCount++;
                }

                return deletedCount;
            }
        }

        private void Append(StartupTask task, string level, string message)
        {
            lock (syncRoot)
            {
                Directory.CreateDirectory(logDirectory);
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message + Environment.NewLine;
                File.AppendAllText(GetLogPath(task), line, new UTF8Encoding(false));
            }
        }

        private string GetLogPath(StartupTask task)
        {
            if (task.Id == Guid.Empty)
            {
                task.Id = Guid.NewGuid();
            }

            return Path.Combine(logDirectory, task.Id.ToString("N") + ".txt");
        }
    }
}
