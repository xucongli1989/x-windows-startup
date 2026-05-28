using System;

namespace x_windows_startup
{
    public class StartupTask
    {
        public StartupTask()
        {
            Id = Guid.NewGuid();
            Enabled = true;
            Type = StartupTaskType.Script;
            Name = string.Empty;
            Script = string.Empty;
            ProgramPath = string.Empty;
            Arguments = string.Empty;
        }

        public Guid Id { get; set; }

        public bool Enabled { get; set; }

        public StartupTaskType Type { get; set; }

        public string Name { get; set; }

        public string Script { get; set; }

        public string ProgramPath { get; set; }

        public string Arguments { get; set; }

        public DateTime? LastRunAt { get; set; }

        public StartupTask Clone()
        {
            return new StartupTask
            {
                Enabled = Enabled,
                Id = Id,
                Type = Type,
                Name = Name,
                Script = Script,
                ProgramPath = ProgramPath,
                Arguments = Arguments,
                LastRunAt = LastRunAt
            };
        }

        public string GetSummary()
        {
            if (Type == StartupTaskType.Program)
            {
                var programPath = ProgramPath ?? string.Empty;
                return string.IsNullOrWhiteSpace(Arguments) ? programPath : programPath + " " + Arguments;
            }

            var summary = (Script ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return summary.Length > 100 ? summary.Substring(0, 100) + "..." : summary;
        }

        public bool Normalize()
        {
            var changed = false;
            if (Id == Guid.Empty)
            {
                Id = Guid.NewGuid();
                changed = true;
            }

            if (Name == null)
            {
                Name = string.Empty;
                changed = true;
            }

            if (Script == null)
            {
                Script = string.Empty;
                changed = true;
            }

            if (ProgramPath == null)
            {
                ProgramPath = string.Empty;
                changed = true;
            }

            if (Arguments == null)
            {
                Arguments = string.Empty;
                changed = true;
            }

            return changed;
        }
    }
}
