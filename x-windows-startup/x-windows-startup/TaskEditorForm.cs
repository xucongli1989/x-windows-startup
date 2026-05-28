using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace x_windows_startup
{
    public class TaskEditorForm : Form
    {
        private readonly RadioButton scriptRadio;
        private readonly TextBox nameTextBox;
        private readonly TextBox programPathTextBox;
        private readonly TextBox argumentsTextBox;
        private readonly TextBox scriptTextBox;
        private readonly TableLayoutPanel rootLayout;
        private readonly TableLayoutPanel contentPanel;
        private readonly FlowLayoutPanel programPanel;
        private readonly Label argumentsLabel;
        private readonly Label scriptLabel;

        public TaskEditorForm(StartupTask task)
        {
            Task = task == null ? new StartupTask() : task.Clone();

            Text = task == null ? "Add Task" : "Edit Task";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font(Font.FontFamily, 11F);

            rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(rootLayout);

            var typePanel = CreateFieldPanel("Type");
            scriptRadio = new RadioButton
            {
                Text = "Script",
                AutoSize = true,
                Margin = new Padding(0, 8, 28, 0),
                Checked = Task.Type == StartupTaskType.Script
            };
            var programRadio = new RadioButton
            {
                Text = "Program",
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0),
                Checked = Task.Type == StartupTaskType.Program
            };
            scriptRadio.CheckedChanged += delegate { UpdateMode(); };
            typePanel.Controls.Add(scriptRadio);
            typePanel.Controls.Add(programRadio);
            rootLayout.Controls.Add(typePanel, 0, 0);

            var namePanel = CreateFieldPanel("Name");
            nameTextBox = CreateTextBox();
            nameTextBox.Text = Task.Name;
            namePanel.Controls.Add(nameTextBox);
            rootLayout.Controls.Add(namePanel, 0, 1);

            programPanel = CreateFieldPanel("Program Path");
            programPathTextBox = CreateTextBox();
            programPathTextBox.Text = Task.ProgramPath;
            programPathTextBox.Width = 480;
            var browseButton = new Button
            {
                Text = "Browse...",
                Width = 104,
                Height = 36
            };
            browseButton.Click += BrowseButton_Click;
            programPanel.Controls.Add(programPathTextBox);
            programPanel.Controls.Add(browseButton);
            rootLayout.Controls.Add(programPanel, 0, 2);

            contentPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            rootLayout.Controls.Add(contentPanel, 0, 3);

            argumentsLabel = CreateFieldLabel("Arguments");
            contentPanel.Controls.Add(argumentsLabel, 0, 0);
            argumentsTextBox = CreateTextBox();
            argumentsTextBox.Dock = DockStyle.Fill;
            argumentsTextBox.Margin = new Padding(0, 6, 0, 6);
            argumentsTextBox.Text = Task.Arguments;
            contentPanel.Controls.Add(argumentsTextBox, 1, 0);

            scriptLabel = CreateFieldLabel("Script");
            contentPanel.Controls.Add(scriptLabel, 0, 1);
            scriptTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = Task.Script,
                Margin = Padding.Empty,
                Font = Font
            };
            contentPanel.Controls.Add(scriptTextBox, 1, 1);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 2, 0, 4)
            };
            var saveButton = new Button
            {
                Text = "Save",
                Width = 104,
                Height = 36
            };
            var cancelButton = new Button
            {
                Text = "Cancel",
                Width = 96,
                Height = 36
            };
            saveButton.Click += SaveButton_Click;
            cancelButton.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);
            contentPanel.Controls.Add(buttonPanel, 1, 2);

            UpdateMode();
        }

        public StartupTask Task { get; private set; }

        private FlowLayoutPanel CreateFieldPanel(string labelText)
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            panel.Controls.Add(CreateFieldLabel(labelText));
            return panel;
        }

        private Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Width = 112,
                Height = 36,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private TextBox CreateTextBox()
        {
            return new TextBox
            {
                Width = 570,
                Height = 36,
                AutoSize = false,
                Multiline = false,
                ScrollBars = ScrollBars.None,
                Margin = Padding.Empty,
                Font = Font
            };
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select Program";
                dialog.Filter = "Executable files (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All files (*.*)|*.*";
                dialog.CheckFileExists = true;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    programPathTextBox.Text = dialog.FileName;
                }
            }
        }

        private void UpdateMode()
        {
            var scriptMode = scriptRadio.Checked;
            programPanel.Visible = !scriptMode;
            rootLayout.RowStyles[2].Height = scriptMode ? 0 : 50;
            argumentsLabel.Visible = !scriptMode;
            argumentsTextBox.Visible = !scriptMode;
            contentPanel.RowStyles[0].Height = scriptMode ? 0 : 48;
            scriptLabel.Visible = scriptMode;
            scriptTextBox.Visible = scriptMode;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            var type = scriptRadio.Checked ? StartupTaskType.Script : StartupTaskType.Program;
            var name = nameTextBox.Text.Trim();
            var programPath = programPathTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a task name.", "Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nameTextBox.Focus();
                return;
            }

            if (type == StartupTaskType.Script && string.IsNullOrWhiteSpace(scriptTextBox.Text))
            {
                MessageBox.Show("Please enter a script.", "Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                scriptTextBox.Focus();
                return;
            }

            if (type == StartupTaskType.Program)
            {
                if (string.IsNullOrWhiteSpace(programPath))
                {
                    MessageBox.Show("Please enter a program path.", "Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    programPathTextBox.Focus();
                    return;
                }

                if (!File.Exists(programPath))
                {
                    MessageBox.Show("Program file does not exist.", "Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    programPathTextBox.Focus();
                    return;
                }
            }

            Task.Type = type;
            Task.Name = name;
            Task.Script = scriptTextBox.Text;
            Task.ProgramPath = programPath;
            Task.Arguments = argumentsTextBox.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
