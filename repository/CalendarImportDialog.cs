using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SimpleTasks
{
    public sealed class CalendarImportDialog : Form
    {
        private sealed class Choice
        {
            public CalendarItem Item;
            public override string ToString() { return TimeZoneInfo.ConvertTime(Item.Due, TimeZoneInfo.Local).ToString("MMM d, yyyy  h:mm tt") + "    |    " + Item.Title; }
        }
        public readonly DateTimePicker FromDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 125 };
        public readonly DateTimePicker ThroughDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 125 };
        public readonly CheckBox AssignmentsOnly = new CheckBox { Text = "Canvas assignments only", AutoSize = true, Margin = new Padding(14, 5, 6, 3) };
        public readonly ComboBox Weight = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
        public int SelectedWeight { get { return TaskPriority.Value(Weight.SelectedIndex); } }
        public readonly CheckedListBox Entries = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, HorizontalScrollbar = true };
        public readonly Button Import = new Button { Text = "Import selected", Width = 130 };
        public List<CalendarItem> SelectedItems { get; private set; }
        private readonly Label summary = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        private readonly TextBox note = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Window };
        private readonly CalendarFile calendar;
        private readonly TaskDocument document;

        public CalendarImportDialog(CalendarFile file, TaskDocument tasks, DateTime? from = null, DateTime? through = null)
        {
            calendar = file; document = tasks;
            Weight.Items.AddRange(TaskPriority.Names); Weight.SelectedIndex = 1;
            Text = "Import calendar"; Font = new Font("Tahoma", 9); ClientSize = new Size(940, 590); MinimumSize = new Size(860, 480);
            StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; ShowInTaskbar = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 27, 38, 32 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 24, 83, 42 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(new Label { Text = file.Name, Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
            var dates = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            dates.Controls.Add(TaskStyle.Label("From:")); dates.Controls.Add(FromDate); dates.Controls.Add(TaskStyle.Label("Through:")); dates.Controls.Add(ThroughDate);
            dates.Controls.Add(AssignmentsOnly); dates.Controls.Add(TaskStyle.Label("Weight:")); dates.Controls.Add(Weight); layout.Controls.Add(dates, 0, 1);
            layout.Controls.Add(summary, 0, 2); layout.Controls.Add(Entries, 0, 3);
            layout.Controls.Add(new Label { Text = "Selected entry's note / link:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 4);
            layout.Controls.Add(note, 0, 5);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
            var all = new Button { Text = "Select all", Width = 90 }; var none = new Button { Text = "Select none", Width = 95 };
            var details = new Button { Text = "Skipped entries...", Width = 130, Enabled = file.Warnings.Count > 0 };
            var cancel = new Button { Text = "Cancel", Width = 85, DialogResult = DialogResult.Cancel };
            buttons.Controls.AddRange(new Control[] { Import, cancel, all, none, details }); layout.Controls.Add(buttons, 0, 6); Controls.Add(layout); CancelButton = cancel;
            FromDate.Value = from ?? DateTime.Today.AddMonths(-1); ThroughDate.Value = through ?? DateTime.Today.AddMonths(3);
            AssignmentsOnly.Checked = file.Items.Any(i => i.Assignment);
            FromDate.ValueChanged += delegate { Rebuild(); }; ThroughDate.ValueChanged += delegate { Rebuild(); }; AssignmentsOnly.CheckedChanged += delegate { Rebuild(); };
            Entries.SelectedIndexChanged += delegate { var choice = Entries.SelectedItem as Choice; note.Text = choice == null ? "" : choice.Item.Note ?? "No note provided."; };
            all.Click += delegate { for (int i = 0; i < Entries.Items.Count; i++) Entries.SetItemChecked(i, true); };
            none.Click += delegate { for (int i = 0; i < Entries.Items.Count; i++) Entries.SetItemChecked(i, false); };
            details.Click += delegate { MessageBox.Show(this, String.Join(Environment.NewLine + Environment.NewLine, file.Warnings.Take(50)) + (file.Warnings.Count > 50 ? "\n\nAdditional skipped entries are not listed here." : ""), "Skipped calendar entries", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            Import.Click += delegate
            {
                SelectedItems = Entries.CheckedItems.Cast<Choice>().Select(c => c.Item).ToList();
                if (SelectedItems.Count == 0) { MessageBox.Show(this, "Select at least one entry to import.", "Import calendar", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                DialogResult = DialogResult.OK;
            };
            Rebuild(); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        }

        private void Rebuild()
        {
            Entries.Items.Clear(); note.Clear(); Import.Enabled = false;
            try
            {
                var plan = CalendarImport.Plan(calendar, document, FromDate.Value, ThroughDate.Value, TimeZoneInfo.Local, AssignmentsOnly.Checked);
                foreach (var item in plan.NewItems) Entries.Items.Add(new Choice { Item = item }, true);
                summary.Text = plan.NewItems.Count + " new  |  " + plan.Existing + " already imported  |  " + calendar.Warnings.Count + " warning(s). Dates inclusive, " + TimeZoneInfo.Local.StandardName + ".";
                Import.Enabled = plan.NewItems.Count > 0;
                if (Entries.Items.Count > 0) Entries.SelectedIndex = 0;
            }
            catch (ArgumentException error) { summary.Text = error.Message; }
        }
    }
}
