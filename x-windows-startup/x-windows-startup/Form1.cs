using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace x_windows_startup
{
    public partial class Form1 : Form
    {
        private readonly List<StartupTask> tasks = new List<StartupTask>();
        private readonly Dictionary<Guid, string> taskStatuses = new Dictionary<Guid, string>();
        private readonly TaskStore taskStore = new TaskStore(Application.StartupPath);
        private readonly TaskLogService taskLogService = new TaskLogService(Application.StartupPath);
        private readonly AutoStartManager autoStartManager = new AutoStartManager(Application.ExecutablePath, Program.RunAllArgument);
        private readonly bool runAllOnLoad;
        private readonly object autoRunSyncRoot = new object();

        private DataGridView taskGrid;
        private Button autoStartButton;
        private LinkLabel projectHomeLink;
        private Label countLabel;
        private bool autoRunExitRequested;
        private bool autoRunLaunching;
        private int pendingAutoRunTasks;
        private int dragRowIndex = -1;
        private int hoverRowIndex = -1;

        private const string ColumnIndex = "Index";
        private const string ColumnType = "Type";
        private const string ColumnName = "Name";
        private const string ColumnContent = "Content";
        private const string ColumnEnabled = "Enabled";
        private const string ColumnStatus = "Status";
        private const string ColumnLastRun = "LastRun";
        private const string ColumnTest = "Test";
        private const string ColumnLog = "Log";
        private const string ColumnEdit = "Edit";
        private const string ColumnDelete = "Delete";

        public Form1()
            : this(false)
        {
        }

        public Form1(bool runAllOnLoad)
        {
            this.runAllOnLoad = runAllOnLoad;
            InitializeComponent();
            BuildUi();
            LoadTasks();
            RefreshGrid();
        }

        private void BuildUi()
        {
            Text = GetWindowTitle();
            Icon = LoadApplicationIcon();
            Size = new Size(1460, 720);
            MinimumSize = new Size(1320, 620);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font(Font.FontFamily, 11F);
            Shown += Form1_Shown;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 5, 0, 0)
            };
            root.Controls.Add(toolbar, 0, 0);

            var addButton = CreateToolbarButton("Add Task", 112);
            addButton.Click += AddButton_Click;
            toolbar.Controls.Add(addButton);

            var runAllButton = CreateToolbarButton("Test All Tasks", 142);
            runAllButton.Click += RunAllButton_Click;
            toolbar.Controls.Add(runAllButton);

            autoStartButton = CreateToolbarButton(string.Empty, 330);
            autoStartButton.Click += AutoStartButton_Click;
            toolbar.Controls.Add(autoStartButton);
            RefreshAutoStartButton();

            var clearLogsButton = CreateToolbarButton("Clear All Logs", 142);
            clearLogsButton.Click += ClearLogsButton_Click;
            toolbar.Controls.Add(clearLogsButton);

            taskGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                StandardTab = true
            };
            taskGrid.ColumnHeadersHeight = 36;
            taskGrid.RowTemplate.Height = 34;

            AddTextColumn(ColumnIndex, "No.", 70);
            AddTextColumn(ColumnType, "Type", 100);
            AddTextColumn(ColumnName, "Name", 190);
            AddFillTextColumn(ColumnContent, "Content / Path");
            taskGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = ColumnEnabled,
                HeaderText = "Enabled",
                Width = 90,
                ThreeState = false,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            AddTextColumn(ColumnStatus, "Status", 110);
            AddTextColumn(ColumnLastRun, "Last Run", 210);
            AddButtonColumn(ColumnTest, "Actions", "Test", 70);
            AddButtonColumn(ColumnLog, string.Empty, "Log", 70);
            AddButtonColumn(ColumnEdit, string.Empty, "Edit", 70);
            AddButtonColumn(ColumnDelete, string.Empty, "Delete", 76);

            taskGrid.CellClick += TaskGrid_CellClick;
            taskGrid.CellDoubleClick += TaskGrid_CellDoubleClick;
            taskGrid.CellMouseEnter += TaskGrid_CellMouseEnter;
            taskGrid.CellMouseLeave += TaskGrid_CellMouseLeave;
            taskGrid.MouseDown += TaskGrid_MouseDown;
            taskGrid.MouseMove += TaskGrid_MouseMove;
            taskGrid.MouseLeave += TaskGrid_MouseLeave;
            taskGrid.DragOver += TaskGrid_DragOver;
            taskGrid.DragDrop += TaskGrid_DragDrop;
            taskGrid.AllowDrop = true;

            root.Controls.Add(taskGrid, 0, 1);

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.Controls.Add(footer, 0, 2);

            projectHomeLink = new LinkLabel
            {
                Text = "Project Home",
                AutoSize = true,
                Margin = new Padding(0, 4, 16, 0),
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            projectHomeLink.LinkClicked += ProjectHomeLink_LinkClicked;
            footer.Controls.Add(projectHomeLink);

            countLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            footer.Controls.Add(countLabel);
        }

        private static string GetWindowTitle()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null
                ? "x-windows-startup"
                : "x-windows-startup v" + version.ToString(3);
        }

        private static Icon LoadApplicationIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (!runAllOnLoad)
            {
                return;
            }

            ShowInTaskbar = false;
            Hide();
            BeginInvoke(new MethodInvoker(RunAutoStartTasksAndExit));
        }

        private Button CreateToolbarButton(string text, int width)
        {
            return new Button
            {
                Text = text,
                Width = width,
                Height = 36,
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0, 0, 8, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = true
            };
        }

        private void AddTextColumn(string name, string headerText, int width)
        {
            taskGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = headerText,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private void AddFillTextColumn(string name, string headerText)
        {
            taskGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = headerText,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private void AddButtonColumn(string name, string headerText, string text, int width)
        {
            taskGrid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = name,
                HeaderText = headerText,
                Text = text,
                UseColumnTextForButtonValue = true,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            using (var editor = new TaskEditorForm(null))
            {
                if (editor.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                tasks.Add(editor.Task);
                SaveTasks();
                RefreshGrid();
                SelectRow(tasks.Count - 1);
            }
        }

        private void RunAllButton_Click(object sender, EventArgs e)
        {
            RunAllTasks(false, false);
        }

        private void AutoStartButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (autoStartManager.IsEnabled())
                {
                    autoStartManager.Disable();
                }
                else
                {
                    autoStartManager.Enable();
                }

                RefreshAutoStartButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Auto Start Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ProjectHomeLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/xucongli1989/x-windows-startup",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Open Project Home Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearLogsButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Delete all task log files?",
                "Clear All Logs",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.OK)
            {
                return;
            }

            try
            {
                var deletedCount = taskLogService.ClearAll();
                MessageBox.Show("Deleted " + deletedCount + " log file(s).", "Clear All Logs", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Clear Logs Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TaskGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= tasks.Count)
            {
                return;
            }

            if (e.ColumnIndex < 0)
            {
                return;
            }

            var columnName = taskGrid.Columns[e.ColumnIndex].Name;
            if (columnName == ColumnEnabled)
            {
                tasks[e.RowIndex].Enabled = !tasks[e.RowIndex].Enabled;
                SaveTasks();
                RefreshGrid();
                SelectRow(e.RowIndex);
            }
            else if (columnName == ColumnTest)
            {
                if (IsTaskRunning(tasks[e.RowIndex]))
                {
                    return;
                }

                RunTask(e.RowIndex);
            }
            else if (columnName == ColumnLog)
            {
                ViewLog(e.RowIndex);
            }
            else if (columnName == ColumnEdit)
            {
                EditTask(e.RowIndex);
            }
            else if (columnName == ColumnDelete)
            {
                DeleteTask(e.RowIndex);
            }
        }

        private void TaskGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= tasks.Count || e.ColumnIndex < 0)
            {
                return;
            }

            var columnName = taskGrid.Columns[e.ColumnIndex].Name;
            if (columnName == ColumnName || columnName == ColumnContent)
            {
                EditTask(e.RowIndex);
            }
        }

        private void EditTask(int rowIndex)
        {
            using (var editor = new TaskEditorForm(tasks[rowIndex]))
            {
                if (editor.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                tasks[rowIndex] = editor.Task;
                SaveTasks();
                RefreshGrid();
                SelectRow(rowIndex);
            }
        }

        private void ViewLog(int rowIndex)
        {
            try
            {
                taskLogService.OpenLog(tasks[rowIndex]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Open Log Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteTask(int rowIndex)
        {
            var task = tasks[rowIndex];
            var result = MessageBox.Show(
                "Delete task \"" + task.Name + "\"?",
                "Delete Task",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.OK)
            {
                return;
            }

            tasks.RemoveAt(rowIndex);
            SaveTasks();
            RefreshGrid();
            SelectRow(Math.Min(rowIndex, tasks.Count - 1));
        }

        private void RunTask(int rowIndex)
        {
            try
            {
                RunTask(tasks[rowIndex], "Test");
                SaveTasks();
                RefreshGrid();
                SelectRow(rowIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Test Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunAutoStartTasksAndExit()
        {
            lock (autoRunSyncRoot)
            {
                autoRunExitRequested = true;
                autoRunLaunching = true;
                pendingAutoRunTasks = 0;
            }

            RunAllTasks(true, true);

            lock (autoRunSyncRoot)
            {
                autoRunLaunching = false;
            }

            TryExitAfterAutoRun();
        }

        private void RunAllTasks(bool enabledOnly, bool exitWhenFinished)
        {
            var failedCount = 0;
            var runCount = 0;
            var skippedCount = 0;

            for (var i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                if (enabledOnly && !task.Enabled)
                {
                    continue;
                }

                if (IsTaskRunning(task))
                {
                    skippedCount++;
                    taskLogService.AppendInfo(task, "Skipped because the task is already running.");
                    continue;
                }

                try
                {
                    RunTask(task, enabledOnly ? "Auto start run" : "Test", exitWhenFinished);
                    runCount++;
                }
                catch (Exception ex)
                {
                    failedCount++;
                    if (enabledOnly)
                    {
                        taskLogService.AppendError(task, "Auto start failed: " + ex.Message);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to run task \"" + task.Name + "\": " + ex.Message,
                            "Test Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }

            if (runCount > 0)
            {
                SaveTasks();
                RefreshGrid();
            }

            if (!enabledOnly && tasks.Count == 0)
            {
                MessageBox.Show("There are no tasks to test.", "Test All Tasks", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (!enabledOnly && failedCount == 0)
            {
                var message = "Test command sent for " + runCount + " task(s).";
                if (skippedCount > 0)
                {
                    message += " Skipped " + skippedCount + " running task(s).";
                }

                MessageBox.Show(message, "Test All Tasks", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RunTask(StartupTask task, string actionName)
        {
            RunTask(task, actionName, false);
        }

        private void RunTask(StartupTask task, string actionName, bool exitWhenFinished)
        {
            taskLogService.AppendSeparator(task);
            taskLogService.AppendInfo(task, actionName + " requested for task \"" + task.Name + "\".");
            SetTaskStatus(task, "Running");
            if (exitWhenFinished)
            {
                lock (autoRunSyncRoot)
                {
                    pendingAutoRunTasks++;
                }
            }

            try
            {
                TaskRunner.Run(
                    task,
                    line => taskLogService.AppendOutput(task, line),
                    line => taskLogService.AppendError(task, line),
                    exitCode => OnTaskExited(task, exitCode),
                    context =>
                    {
                        taskLogService.AppendInfo(task, "Command: " + context.CommandLine);
                        if (context.StartInfo != null && !string.IsNullOrWhiteSpace(context.StartInfo.WorkingDirectory))
                        {
                            taskLogService.AppendInfo(task, "Working directory: " + context.StartInfo.WorkingDirectory);
                        }

                        LogScriptFileContent(task, context.ScriptFileContent);
                    });
                taskLogService.AppendInfo(task, "Process start requested successfully.");
            }
            catch (Exception ex)
            {
                SetTaskStatus(task, "Failed");
                taskLogService.AppendError(task, ex.Message);
                if (exitWhenFinished)
                {
                    MarkAutoRunTaskFinished();
                }

                throw;
            }
        }

        private void LogScriptFileContent(StartupTask task, string scriptFileContent)
        {
            if (string.IsNullOrEmpty(scriptFileContent))
            {
                return;
            }

            taskLogService.AppendInfo(task, "Script file content:");
            var lines = scriptFileContent.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                taskLogService.AppendInfo(task, "  " + (i + 1) + ": " + lines[i]);
            }
        }

        private void OnTaskExited(StartupTask task, int exitCode)
        {
            taskLogService.AppendInfo(task, "Process exited with code " + exitCode + ".");
            SetTaskStatus(task, exitCode == 0 ? "Finished" : "Failed");
            if (autoRunExitRequested)
            {
                MarkAutoRunTaskFinished();
            }
        }

        private void MarkAutoRunTaskFinished()
        {
            lock (autoRunSyncRoot)
            {
                if (pendingAutoRunTasks > 0)
                {
                    pendingAutoRunTasks--;
                }
            }

            TryExitAfterAutoRun();
        }

        private void TryExitAfterAutoRun()
        {
            bool shouldExit;
            lock (autoRunSyncRoot)
            {
                shouldExit = autoRunExitRequested && !autoRunLaunching && pendingAutoRunTasks == 0;
            }

            if (!shouldExit || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(Close));
                return;
            }

            Close();
        }

        private void SetTaskStatus(StartupTask task, string status)
        {
            if (task.Id == Guid.Empty)
            {
                task.Id = Guid.NewGuid();
            }

            taskStatuses[task.Id] = status;
            RefreshGridOnUiThread();
        }

        private string GetTaskStatus(StartupTask task)
        {
            string status;
            return taskStatuses.TryGetValue(task.Id, out status) ? status : "Idle";
        }

        private bool IsTaskRunning(StartupTask task)
        {
            return string.Equals(GetTaskStatus(task), "Running", StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshGridOnUiThread()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(RefreshGrid));
                return;
            }

            RefreshGrid();
        }

        private void RefreshGrid()
        {
            taskGrid.Rows.Clear();

            for (var i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                taskGrid.Rows.Add(
                    (i + 1).ToString(),
                    task.Type == StartupTaskType.Script ? "Script" : "Program",
                    task.Name,
                    task.GetSummary(),
                    task.Enabled,
                    GetTaskStatus(task),
                    task.LastRunAt.HasValue ? task.LastRunAt.Value.ToString("yyyy-MM-dd HH:mm") : "Never");

                ApplyRowStateStyle(taskGrid.Rows[i], task.Enabled);
                ApplyRowHoverStyle(taskGrid.Rows[i], i == hoverRowIndex);
                ApplyRunButtonState(taskGrid.Rows[i], IsTaskRunning(task));
            }

            countLabel.Text = tasks.Count + " tasks, " + tasks.Count(task => task.Enabled) + " enabled. Drag rows to change run order.";
        }

        private void ApplyRowStateStyle(DataGridViewRow row, bool enabled)
        {
            if (enabled)
            {
                row.DefaultCellStyle.BackColor = taskGrid.DefaultCellStyle.BackColor;
                row.DefaultCellStyle.ForeColor = taskGrid.DefaultCellStyle.ForeColor;
                row.DefaultCellStyle.SelectionBackColor = taskGrid.DefaultCellStyle.SelectionBackColor;
                row.DefaultCellStyle.SelectionForeColor = taskGrid.DefaultCellStyle.SelectionForeColor;
                return;
            }

            row.DefaultCellStyle.BackColor = SystemColors.Control;
            row.DefaultCellStyle.ForeColor = SystemColors.GrayText;
            row.DefaultCellStyle.SelectionBackColor = Color.Gainsboro;
            row.DefaultCellStyle.SelectionForeColor = SystemColors.GrayText;
        }

        private void ApplyRowHoverStyle(DataGridViewRow row, bool hovered)
        {
            if (!hovered || row.Selected)
            {
                return;
            }

            var task = tasks[row.Index];
            row.DefaultCellStyle.BackColor = task.Enabled ? Color.FromArgb(232, 242, 255) : Color.FromArgb(224, 224, 224);
        }

        private void RefreshRowStyle(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= taskGrid.Rows.Count || rowIndex >= tasks.Count)
            {
                return;
            }

            ApplyRowStateStyle(taskGrid.Rows[rowIndex], tasks[rowIndex].Enabled);
            ApplyRowHoverStyle(taskGrid.Rows[rowIndex], rowIndex == hoverRowIndex);
            ApplyRunButtonState(taskGrid.Rows[rowIndex], IsTaskRunning(tasks[rowIndex]));
        }

        private void ApplyRunButtonState(DataGridViewRow row, bool running)
        {
            var cell = row.Cells[ColumnTest];
            cell.Value = running ? "Running" : "Test";
            cell.Style.ForeColor = running ? SystemColors.GrayText : taskGrid.DefaultCellStyle.ForeColor;
            cell.Style.SelectionForeColor = running ? SystemColors.GrayText : taskGrid.DefaultCellStyle.SelectionForeColor;
        }

        private void TaskGrid_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= tasks.Count || e.RowIndex == hoverRowIndex)
            {
                return;
            }

            var oldHoverRowIndex = hoverRowIndex;
            hoverRowIndex = e.RowIndex;
            RefreshRowStyle(oldHoverRowIndex);
            RefreshRowStyle(hoverRowIndex);
        }

        private void TaskGrid_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex != hoverRowIndex)
            {
                return;
            }

            var oldHoverRowIndex = hoverRowIndex;
            hoverRowIndex = -1;
            RefreshRowStyle(oldHoverRowIndex);
        }

        private void TaskGrid_MouseLeave(object sender, EventArgs e)
        {
            var oldHoverRowIndex = hoverRowIndex;
            hoverRowIndex = -1;
            RefreshRowStyle(oldHoverRowIndex);
        }

        private void SelectRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= taskGrid.Rows.Count)
            {
                return;
            }

            taskGrid.ClearSelection();
            taskGrid.Rows[rowIndex].Selected = true;
            taskGrid.CurrentCell = taskGrid.Rows[rowIndex].Cells[0];
        }

        private void TaskGrid_MouseDown(object sender, MouseEventArgs e)
        {
            var hit = taskGrid.HitTest(e.X, e.Y);
            dragRowIndex = hit.RowIndex >= 0 && IsDragSourceColumn(hit.ColumnIndex)
                ? hit.RowIndex
                : -1;
        }

        private bool IsDragSourceColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= taskGrid.Columns.Count)
            {
                return false;
            }

            var columnName = taskGrid.Columns[columnIndex].Name;
            return columnName != ColumnEnabled
                && columnName != ColumnTest
                && columnName != ColumnLog
                && columnName != ColumnEdit
                && columnName != ColumnDelete;
        }

        private void TaskGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || dragRowIndex < 0 || dragRowIndex >= tasks.Count)
            {
                return;
            }

            taskGrid.DoDragDrop(dragRowIndex, DragDropEffects.Move);
        }

        private void TaskGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void TaskGrid_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(int)))
            {
                return;
            }

            var clientPoint = taskGrid.PointToClient(new Point(e.X, e.Y));
            var hit = taskGrid.HitTest(clientPoint.X, clientPoint.Y);
            var targetIndex = hit.RowIndex;
            var sourceIndex = (int)e.Data.GetData(typeof(int));

            if (sourceIndex < 0 || sourceIndex >= tasks.Count || targetIndex < 0 || targetIndex >= tasks.Count || sourceIndex == targetIndex)
            {
                return;
            }

            var task = tasks[sourceIndex];
            tasks.RemoveAt(sourceIndex);
            tasks.Insert(targetIndex, task);
            SaveTasks();
            RefreshGrid();
            SelectRow(targetIndex);
        }

        private void LoadTasks()
        {
            try
            {
                bool changed;
                tasks.Clear();
                tasks.AddRange(taskStore.Load(out changed));
                if (changed)
                {
                    SaveTasks();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to read task configuration: " + ex.Message, "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveTasks()
        {
            taskStore.Save(tasks);
        }

        private void RefreshAutoStartButton()
        {
            if (autoStartButton == null)
            {
                return;
            }

            autoStartButton.Text = autoStartManager.IsEnabled()
                ? "Disable Auto Start and Run All Tasks"
                : "Enable Auto Start and Run All Tasks";
        }
    }
}
