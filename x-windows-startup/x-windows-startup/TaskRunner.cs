using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace x_windows_startup
{
    public static class TaskRunner
    {
        public static Process Run(
            StartupTask task,
            Action<string> outputReceived,
            Action<string> errorReceived,
            Action<int> exited,
            Action<TaskStartContext> startInfoReady)
        {
            Process process;
            if (task.Type == StartupTaskType.Script)
            {
                process = RunScript(task.Script, outputReceived, errorReceived, exited, startInfoReady);
            }
            else
            {
                process = RunProgram(task.ProgramPath, task.Arguments, outputReceived, errorReceived, exited, startInfoReady);
            }

            task.LastRunAt = DateTime.Now;
            return process;
        }

        public static string FormatCommandLine(ProcessStartInfo startInfo)
        {
            if (startInfo == null)
            {
                return string.Empty;
            }

            var command = QuoteCommandPart(startInfo.FileName);
            if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
            {
                command += " " + startInfo.Arguments;
            }

            return command;
        }

        private static Process RunScript(string script, Action<string> outputReceived, Action<string> errorReceived, Action<int> exited, Action<TaskStartContext> startInfoReady)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                throw new InvalidOperationException("Script cannot be empty.");
            }

            var directory = Path.Combine(Path.GetTempPath(), "x-windows-startup");
            Directory.CreateDirectory(directory);
            var scriptPath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(scriptPath, script, new UTF8Encoding(true));

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    outputReceived(e.Data);
                }
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    errorReceived(e.Data);
                }
            };
            process.Exited += delegate
            {
                process.WaitForExit();
                exited(process.ExitCode);
                TryDeleteFile(scriptPath);
                process.Dispose();
            };

            try
            {
                if (startInfoReady != null)
                {
                    startInfoReady(new TaskStartContext(process.StartInfo, FormatCommandLine(process.StartInfo), script));
                }

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return process;
            }
            catch
            {
                process.Dispose();
                TryDeleteFile(scriptPath);
                throw;
            }
        }

        private static Process RunProgram(string programPath, string arguments, Action<string> outputReceived, Action<string> errorReceived, Action<int> exited, Action<TaskStartContext> startInfoReady)
        {
            if (string.IsNullOrWhiteSpace(programPath))
            {
                throw new InvalidOperationException("Program path cannot be empty.");
            }

            if (!File.Exists(programPath))
            {
                throw new FileNotFoundException("Program file does not exist.", programPath);
            }

            var startInfo = CreateProgramStartInfo(programPath, arguments);
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    outputReceived(e.Data);
                }
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    errorReceived(e.Data);
                }
            };
            process.Exited += delegate
            {
                process.WaitForExit();
                exited(process.ExitCode);
                process.Dispose();
            };

            try
            {
                if (startInfoReady != null)
                {
                    startInfoReady(new TaskStartContext(process.StartInfo, FormatCommandLine(process.StartInfo), null));
                }

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        private static ProcessStartInfo CreateProgramStartInfo(string programPath, string arguments)
        {
            var extension = Path.GetExtension(programPath);
            if (string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase))
            {
                return CreateRedirectedStartInfo(
                    "cmd.exe",
                    "/c \"" + programPath + "\" " + (arguments ?? string.Empty),
                    Path.GetDirectoryName(programPath) ?? string.Empty);
            }

            return CreateRedirectedStartInfo(programPath, arguments ?? string.Empty, Path.GetDirectoryName(programPath) ?? string.Empty);
        }

        private static ProcessStartInfo CreateRedirectedStartInfo(string fileName, string arguments, string workingDirectory)
        {
            return new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        private static string QuoteCommandPart(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
            }
        }
    }
}
