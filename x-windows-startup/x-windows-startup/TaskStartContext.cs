using System.Diagnostics;

namespace x_windows_startup
{
    public class TaskStartContext
    {
        public TaskStartContext(ProcessStartInfo startInfo, string commandLine, string scriptFileContent)
        {
            StartInfo = startInfo;
            CommandLine = commandLine;
            ScriptFileContent = scriptFileContent;
        }

        public ProcessStartInfo StartInfo { get; private set; }

        public string CommandLine { get; private set; }

        public string ScriptFileContent { get; private set; }
    }
}
