using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SimpleTasks
{
    public sealed class RepeatOptionsDialog : Form
    {
        public readonly ComboBox PatternBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
        public readonly NumericUpDown IntervalCount = new NumericUpDown { Minimum = 1, Maximum = 365, Width = 65 };
        public readonly ComboBox EndBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        public readonly NumericUpDown EndCount = new NumericUpDown { Minimum = 1, Maximum = 1000000, Width = 100, ThousandsSeparator = true };
        public readonly DateTimePicker EndDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 140 };
        public readonly CheckBox[] Days = new CheckBox[7];
        public RepeatRule Result { get; private set; }
        private readonly Label intervalUnit = TaskStyle.Label("day(s)");
        private readonly Label endUnit = TaskStyle.Label("");
        private int countValue;
        private int weeksValue;
        private int lastEnd;

        public RepeatOptionsDialog(RepeatRule rule, DateTime anchor, int generated)
        {
            Text = "Repeat settings"; Font = new Font("Tahoma", 9); ClientSize = new Size(640, 290);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 33, 36, 36, 36, 79, 42 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(new Label { Text = "First occurrence: " + anchor.ToString("MMM d, yyyy") + "    |    Already created: " + generated, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            PatternBox.Items.AddRange(new object[] { "Daily", "Weekly", "Selected weekdays" });
            var everyLabel = TaskStyle.Label("Every:");
            var schedule = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            schedule.Controls.Add(TaskStyle.Label("Schedule:")); schedule.Controls.Add(PatternBox);
            schedule.Controls.Add(everyLabel); schedule.Controls.Add(IntervalCount); schedule.Controls.Add(intervalUnit); layout.Controls.Add(schedule, 0, 1);
            var days = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            days.Controls.Add(TaskStyle.Label("On:"));
            int[] order = { 1, 2, 3, 4, 5, 6, 0 };
            foreach (int index in order)
            {
                Days[index] = new CheckBox { Text = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[index], AutoSize = true, Checked = (rule.WeekdayMask & (1 << index)) != 0, Margin = new Padding(3, 5, 8, 3) };
                days.Controls.Add(Days[index]);
            }
            layout.Controls.Add(days, 0, 2);
            EndBox.Items.AddRange(new object[] { "Indefinitely", "After total occurrences", "After number of weeks", "On a specific date" });
            var ending = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            ending.Controls.Add(TaskStyle.Label("Ends:")); ending.Controls.Add(EndBox); ending.Controls.Add(EndCount); ending.Controls.Add(EndDate); ending.Controls.Add(endUnit); layout.Controls.Add(ending, 0, 3);
            layout.Controls.Add(new Label {
                Text = "The total count includes the first task. Weeks start on the first occurrence date; the end date is inclusive.\n\nLater occurrences appear at midnight on their scheduled day. Dated repeats keep the chosen due time. Unfinished tasks stay visible.",
                Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText
            }, 0, 4);
            var save = new Button { Text = "OK", Width = 85 }; var cancel = new Button { Text = "Cancel", Width = 85, DialogResult = DialogResult.Cancel };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(cancel); buttons.Controls.Add(save); layout.Controls.Add(buttons, 0, 5); Controls.Add(layout);
            countValue = rule.TotalCount; weeksValue = rule.Weeks; lastEnd = (int)rule.End;
            EndDate.Value = Recurrence.ParseDate(rule.UntilDate); IntervalCount.Value = rule.Interval;
            PatternBox.SelectedIndex = (int)rule.Pattern; EndBox.SelectedIndex = (int)rule.End;
            Action refresh = delegate
            {
                bool selectedDays = PatternBox.SelectedIndex == 2;
                foreach (var day in Days) day.Enabled = selectedDays;
                everyLabel.Visible = IntervalCount.Visible = intervalUnit.Visible = !selectedDays;
                intervalUnit.Text = PatternBox.SelectedIndex == 1 ? "week(s)" : "day(s)";
                EndCount.Visible = EndBox.SelectedIndex == 1 || EndBox.SelectedIndex == 2;
                EndDate.Visible = EndBox.SelectedIndex == 3;
                endUnit.Text = EndBox.SelectedIndex == 1 ? "total occurrences" : EndBox.SelectedIndex == 2 ? "week(s)" : "";
            };
            PatternBox.SelectedIndexChanged += delegate { refresh(); };
            EndBox.SelectedIndexChanged += delegate
            {
                if (lastEnd == 1) countValue = (int)EndCount.Value;
                if (lastEnd == 2) weeksValue = (int)EndCount.Value;
                EndCount.Maximum = EndBox.SelectedIndex == 2 ? 5200 : 1000000;
                EndCount.Value = Math.Max(1, Math.Min((int)EndCount.Maximum, EndBox.SelectedIndex == 2 ? weeksValue : countValue));
                lastEnd = EndBox.SelectedIndex; refresh();
            };
            EndCount.Maximum = rule.End == RepeatEnd.Weeks ? 5200 : 1000000;
            EndCount.Value = Math.Max(1, Math.Min((int)EndCount.Maximum, rule.End == RepeatEnd.Weeks ? weeksValue : countValue));
            refresh();
            save.Click += delegate
            {
                try
                {
                    var result = rule.Copy(); result.Pattern = (RepeatPattern)PatternBox.SelectedIndex; result.Interval = (int)IntervalCount.Value;
                    result.WeekdayMask = Enumerable.Range(0, 7).Where(i => Days[i].Checked).Sum(i => 1 << i);
                    result.End = (RepeatEnd)EndBox.SelectedIndex;
                    result.TotalCount = result.End == RepeatEnd.Count ? (int)EndCount.Value : countValue;
                    result.Weeks = result.End == RepeatEnd.Weeks ? (int)EndCount.Value : weeksValue;
                    result.UntilDate = Recurrence.DateText(EndDate.Value); result.Validate(anchor);
                    if (result.End == RepeatEnd.Count && result.TotalCount < generated) throw new ArgumentException("The total must be at least " + generated + ", the number already created.");
                    Result = result; DialogResult = DialogResult.OK;
                }
                catch (ArgumentException error) { MessageBox.Show(this, error.Message, "Check repeat settings", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
            AcceptButton = save; CancelButton = cancel;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        }
    }
}
