using System;
using Microsoft.Win32;

namespace x_windows_startup
{
    public class AutoStartManager
    {
        private const string RegistryName = "x-windows-startup";
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private readonly string command;

        public AutoStartManager(string executablePath, string argument)
        {
            command = "\"" + executablePath + "\" " + argument;
        }

        public bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                return key != null && string.Equals(key.GetValue(RegistryName) as string, command, StringComparison.OrdinalIgnoreCase);
            }
        }

        public void Enable()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Windows Run registry key was not found.");
                }

                key.SetValue(RegistryName, command, RegistryValueKind.String);
            }
        }

        public void Disable()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(RegistryName, false);
                }
            }
        }
    }
}
