using System;
using System.Drawing;
using System.Windows.Forms;

namespace SimpleTasks
{
    public sealed class ColorSettingsDialog : Form
    {
        public readonly CheckBox EnableColors = new CheckBox { Text = "Color tasks by age / time until due", AutoSize = true, Dock = DockStyle.Fill };
        public readonly CheckBox Advanced = new CheckBox { Text = "Advanced: manually set every color transition", AutoSize = true, Dock = DockStyle.Fill };
        public readonly NumericUpDown[,] Undated = new NumericUpDown[3, 4];
        public readonly NumericUpDown[,] Dated = new NumericUpDown[3, 4];
        public readonly Button SaveButton = new Button { Text = "Save", Width = 90 };
        public TaskColorSettings Result { get; private set; }
        private readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };
        private readonly TableLayoutPanel[] grids = new TableLayoutPanel[2];
        private readonly Label[,] headers = new Label[2, 4];
        private readonly Label[] explanations = new Label[2];
        private bool loading;

        public ColorSettingsDialog(TaskColorSettings settings)
        {
            Text = "Task color settings"; Font = new Font("Tahoma", 9); ClientSize = new Size(680, 438);
            MinimumSize = new Size(680, 470); StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 30, 44, 30 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 44, 40 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(EnableColors, 0, 0);
            layout.Controls.Add(new Label { Text = "Off: use weight colors. On: tasks progress from black to red.\nNone stays black. Daily tasks keep weight colors and turn gray when done.", Dock = DockStyle.Fill }, 0, 1);
            layout.Controls.Add(Advanced, 0, 2);
            AddTable("Undated tasks", Undated, 0); AddTable("Dated tasks", Dated, 1);
            layout.Controls.Add(tabs, 0, 3);
            layout.Controls.Add(new Label { Text = "1 day = 24 hours; 0.5 = 12 hours. Save recolors existing tasks.\nTurning Advanced off recalculates the other colors from the red threshold.", Dock = DockStyle.Fill, Padding = new Padding(0, 7, 0, 0) }, 0, 4);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var cancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
            var defaults = new Button { Text = "Reset defaults", Width = 120 };
            buttons.Controls.Add(cancel); buttons.Controls.Add(SaveButton); buttons.Controls.Add(defaults); layout.Controls.Add(buttons, 0, 5);
            Controls.Add(layout); CancelButton = cancel; AcceptButton = SaveButton;
            defaults.Click += delegate { LoadSettings(new TaskColorSettings()); };
            EnableColors.CheckedChanged += delegate { tabs.Enabled = Advanced.Enabled = EnableColors.Checked; };
            Advanced.CheckedChanged += delegate { if (!loading) ApplyView(); };
            SaveButton.Click += delegate
            {
                try { Result = ReadSettings(); DialogResult = DialogResult.OK; }
                catch (ArgumentException error) { MessageBox.Show(this, error.Message, "Check color thresholds", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
            LoadSettings(settings ?? new TaskColorSettings());
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        }

        private void AddTable(string title, NumericUpDown[,] fields, int tab)
        {
            bool dated = tab == 1;
            var page = new TabPage(title); var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 5, Padding = new Padding(6) };
            grids[tab] = grid; grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            for (int i = 0; i < 3; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            var explanation = new Label { Dock = DockStyle.Fill }; explanations[tab] = explanation;
            grid.Controls.Add(explanation, 0, 0); grid.SetColumnSpan(explanation, 5);
            grid.Controls.Add(TaskStyle.Label("Weight"), 0, 1);
            string[] names = { "Light green", "Yellow-green", "Orange", "Red" };
            for (int c = 0; c < 4; c++)
            {
                var header = new Label { Text = names[c], ForeColor = TaskStyle.Weights[c + 2], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
                headers[tab, c] = header; grid.Controls.Add(header, c + 1, 1);
            }
            for (int r = 0; r < 3; r++)
            {
                grid.Controls.Add(TaskStyle.Label(TaskPriority.Names[r + 1]), 0, r + 2);
                for (int c = 0; c < 4; c++)
                {
                    var field = new NumericUpDown { Minimum = dated ? 0 : .0001m, Maximum = 36500, DecimalPlaces = 4, Increment = .25m, Dock = DockStyle.Fill,
                        AccessibleName = title + ", " + TaskPriority.Names[r + 1] + ", " + names[c] + ", days", TabIndex = r * 4 + c };
                    fields[r, c] = field; grid.Controls.Add(field, c + 1, r + 2);
                    if (c == 3)
                    {
                        int row = r;
                        field.ValueChanged += delegate { if (!loading && !Advanced.Checked) LinearizeRow(fields, row, dated); };
                    }
                }
            }
            page.Controls.Add(grid); tabs.TabPages.Add(page);
        }
        private void LinearizeRow(NumericUpDown[,] fields, int row, bool dated)
        {
            var values = TaskColorSettings.Linear(fields[row, 3].Value, dated);
            for (int c = 0; c < 3; c++) fields[row, c].Value = values[c];
        }
        private void ApplyView()
        {
            loading = true;
            for (int tab = 0; tab < 2; tab++)
            {
                bool dated = tab == 1; var fields = dated ? Dated : Undated;
                explanations[tab].Text = Advanced.Checked
                    ? (dated ? "Days remaining until due. Enter decreasing days from light green to red." : "Days since creation. Enter increasing days from light green to red.")
                    : (dated ? "Days remaining when tasks turn red. Other colors begin at 4x, 3x, and 2x these days." : "Days old when tasks turn red. Other colors begin at 25%, 50%, and 75% of these days.");
                for (int c = 0; c < 4; c++)
                {
                    bool visible = c == 3 || Advanced.Checked; headers[tab, c].Visible = visible;
                    grids[tab].ColumnStyles[c + 1].SizeType = visible ? SizeType.Percent : SizeType.Absolute;
                    grids[tab].ColumnStyles[c + 1].Width = visible ? 25 : 0;
                    headers[tab, c].Text = c == 3 && !Advanced.Checked ? "Turns red (days)" : new[] { "Light green", "Yellow-green", "Orange", "Red" }[c];
                    for (int r = 0; r < 3; r++)
                    {
                        var field = fields[r, c]; field.Visible = visible;
                        field.Dock = Advanced.Checked ? DockStyle.Fill : DockStyle.None; field.Anchor = AnchorStyles.Left; field.Width = 140;
                        field.Minimum = !Advanced.Checked && c == 3 ? .01m : (dated ? 0 : .0001m);
                        field.Maximum = !Advanced.Checked && dated && c == 3 ? 9125m : 36500m;
                        field.DecimalPlaces = !Advanced.Checked && c == 3 ? 2 : 4;
                        if (!Advanced.Checked && c == 3) field.Value = Math.Max(.01m, Decimal.Round(field.Value, 2));
                    }
                }
            }
            loading = false;
            if (!Advanced.Checked) for (int r = 0; r < 3; r++) { LinearizeRow(Undated, r, false); LinearizeRow(Dated, r, true); }
        }
        private void LoadSettings(TaskColorSettings settings)
        {
            loading = true; EnableColors.Checked = settings.Enabled; tabs.Enabled = Advanced.Enabled = settings.Enabled; Advanced.Checked = settings.Advanced;
            for (int r = 0; r < 3; r++) for (int c = 0; c < 4; c++)
            {
                Undated[r, c].Minimum = .0001m; Dated[r, c].Minimum = 0; Dated[r, c].Maximum = 36500;
                Undated[r, c].Value = settings.UndatedDays[r][c]; Dated[r, c].Value = settings.DatedDays[r][c];
            }
            loading = false; ApplyView();
        }
        public TaskColorSettings ReadSettings()
        {
            var settings = new TaskColorSettings { Enabled = EnableColors.Checked, Advanced = Advanced.Checked };
            for (int r = 0; r < 3; r++) for (int c = 0; c < 4; c++) { settings.UndatedDays[r][c] = Undated[r, c].Value; settings.DatedDays[r][c] = Dated[r, c].Value; }
            settings.Validate(); return settings;
        }
    }
}
