using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace x_windows_startup
{
    public class TaskStore
    {
        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

        private readonly string dataFilePath;

        public TaskStore(string baseDirectory)
        {
            dataFilePath = Path.Combine(baseDirectory, "tasks.json");
        }

        public List<StartupTask> Load()
        {
            bool ignored;
            return Load(out ignored);
        }

        public List<StartupTask> Load(out bool changed)
        {
            var tasks = new List<StartupTask>();
            changed = false;
            if (!File.Exists(dataFilePath))
            {
                return tasks;
            }

            var json = File.ReadAllText(dataFilePath, Encoding.UTF8);
            var loadedTasks = JsonSerializer.Deserialize<List<StartupTask>>(json, JsonOptions);
            if (loadedTasks == null)
            {
                return tasks;
            }

            foreach (var task in loadedTasks)
            {
                if (task == null)
                {
                    continue;
                }

                changed = task.Normalize() || changed;
                tasks.Add(task);
            }

            return tasks;
        }

        public void Save(IEnumerable<StartupTask> tasks)
        {
            var directory = Path.GetDirectoryName(dataFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempFile = dataFilePath + ".tmp";
            var backupFile = dataFilePath + ".bak";
            var json = JsonSerializer.Serialize(tasks, JsonOptions);
            File.WriteAllText(tempFile, json, new UTF8Encoding(false));

            try
            {
                if (File.Exists(dataFilePath))
                {
                    File.Replace(tempFile, dataFilePath, backupFile, true);
                }
                else
                {
                    File.Move(tempFile, dataFilePath);
                }
            }
            catch
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                throw;
            }
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
