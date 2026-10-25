using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SimpleTasks
{
    public static class TaskStyle
    {
        public static readonly Color[] Weights = { Color.Black, Color.Black, Color.FromArgb(86, 153, 62), Color.FromArgb(142, 157, 20), Color.FromArgb(208, 111, 13), Color.FromArgb(199, 42, 42) };
        public static Label Label(string text) { return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 6, 3) }; }
    }

    public sealed class TaskInput : UserControl
    {
        public readonly TextBox TitleBox = new TextBox { Dock = DockStyle.Fill, MaxLength = 500 };
        public readonly TextBox NoteBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, MaxLength = 0, AccessibleName = "Note (optional)" };
        public readonly DateTimeEntry CreatedPicker = new DateTimeEntry { AccessibleName = "Creation date and time" };
        public readonly DateTimeEntry Due = new DateTimeEntry { AccessibleName = "Due date and time" };
        public readonly CheckBox HasDue = new CheckBox { Text = "Due date:", AutoSize = true, Margin = new Padding(18, 6, 3, 3) };
        public readonly ComboBox Weight = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, Width = 60, Dock = DockStyle.Fill };
        public readonly Button Add = new Button { Text = "&Add task", Dock = DockStyle.Fill, Height = 26 };
        public readonly CheckBox HasRepeat = new CheckBox { Text = "Repeat", AutoSize = true, Margin = new Padding(3, 6, 8, 3) };
        public readonly CheckBox IsDaily = new CheckBox { Text = "Daily task", AutoSize = true, Margin = new Padding(3, 6, 14, 3) };
        public readonly Button RepeatSettings = new Button { Text = "Settings...", Width = 85, Enabled = false };
        private readonly Label repeatSummary = new Label { AutoEllipsis = true, Width = 480, Height = 24, TextAlign = ContentAlignment.MiddleLeft, ForeColor = SystemColors.GrayText, Margin = new Padding(10, 2, 0, 0) };
        public RepeatRule Rule { get; private set; }
        private TaskSeries editingSeries;
        public event EventHandler Submit;
        private bool creationDateEdited;
        private bool refreshingCreationDate;
        private bool dynamicColors;
        private readonly List<Label> weightLegend = new List<Label>();
        private readonly Label weightHint = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 3, 0, 0) };

        public TaskInput(bool editing)
        {
            Dock = DockStyle.Fill;
            Rule = new RepeatRule();
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 5, Padding = new Padding(6, 4, 6, 2) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.Controls.Add(TaskStyle.Label("Task:"), 0, 0);
            table.Controls.Add(TitleBox, 1, 0);
            table.Controls.Add(TaskStyle.Label("Weight:"), 2, 0);
            table.Controls.Add(Weight, 3, 0);
            table.Controls.Add(Add, 4, 0);
            Add.Text = editing ? "&Save" : "&Add task";
            var dates = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
            var createdRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            createdRow.Controls.Add(TaskStyle.Label("Created:")); createdRow.Controls.Add(CreatedPicker);
            var dueRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            HasDue.Margin = new Padding(3, 6, 3, 3);
            dueRow.Controls.Add(HasDue); dueRow.Controls.Add(Due);
            dates.Controls.Add(createdRow); dates.Controls.Add(dueRow);
            var dailyHint = new Label { Text = "Complete by the end of each day. Resets at midnight in your system's local time.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = SystemColors.GrayText, Visible = false };
            var datesRow = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            datesRow.Controls.Add(dates); datesRow.Controls.Add(dailyHint);
            table.Controls.Add(datesRow, 0, 1);
            table.SetColumnSpan(datesRow, 5);
            table.Controls.Add(TaskStyle.Label("Note:"), 0, 2);
            table.Controls.Add(NoteBox, 1, 2);
            table.SetColumnSpan(NoteBox, 4);
            var repeats = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            repeats.Controls.Add(IsDaily); repeats.Controls.Add(HasRepeat); repeats.Controls.Add(RepeatSettings); repeats.Controls.Add(repeatSummary);
            table.Controls.Add(repeats, 0, 3); table.SetColumnSpan(repeats, 5);
            var legend = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            legend.Controls.Add(new Label { Text = "Weight:", AutoSize = true, Margin = new Padding(3, 3, 6, 0), ForeColor = SystemColors.GrayText });
            for (int i = 0; i < 4; i++)
            {
                var label = new Label { Text = TaskPriority.Names[i], ForeColor = TaskPriority.StaticColor(TaskPriority.Value(i)), AutoSize = true, Margin = new Padding(3, 3, 10, 0) };
                weightLegend.Add(label); legend.Controls.Add(label);
            }
            legend.Controls.Add(weightHint); SetColorSettings(null);
            table.Controls.Add(legend, 0, 4);
            table.SetColumnSpan(legend, 5);
            TitleBox.TabIndex = 0; Weight.TabIndex = 1; datesRow.TabIndex = 2; NoteBox.TabIndex = 3; repeats.TabIndex = 4; Add.TabIndex = 5;
            createdRow.TabIndex = 0; dueRow.TabIndex = 1; CreatedPicker.TabIndex = 0; HasDue.TabIndex = 0; Due.TabIndex = 1;
            IsDaily.TabIndex = 0; HasRepeat.TabIndex = 1; RepeatSettings.TabIndex = 2;
            Controls.Add(table);
            Weight.Items.AddRange(TaskPriority.Names);
            Weight.SelectedIndex = 1;
            Weight.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                e.DrawBackground();
                if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, TaskPriority.Names[e.Index], e.Font, e.Bounds, dynamicColors && !IsDaily.Checked ? Color.Black : TaskPriority.StaticColor(TaskPriority.Value(e.Index)), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                e.DrawFocusRectangle();
            };
            Due.Value = DateTime.Today.AddDays(1).AddHours(17);
            Due.Enabled = false;
            HasDue.CheckedChanged += delegate { Due.Enabled = HasDue.Checked; };
            CreatedPicker.ValueChanged += delegate { if (!refreshingCreationDate) creationDateEdited = true; };
            HasRepeat.CheckedChanged += delegate { RepeatSettings.Enabled = HasRepeat.Checked; UpdateRepeatSummary(); };
            IsDaily.CheckedChanged += delegate
            {
                dates.Visible = !IsDaily.Checked; dailyHint.Visible = IsDaily.Checked;
                CreatedPicker.Enabled = HasDue.Enabled = HasRepeat.Enabled = !IsDaily.Checked;
                if (IsDaily.Checked) { HasDue.Checked = false; HasRepeat.Checked = false; }
                Due.Enabled = !IsDaily.Checked && HasDue.Checked;
                RepeatSettings.Enabled = !IsDaily.Checked && HasRepeat.Checked;
                UpdateWeightColors();
            };
            RepeatSettings.Click += delegate
            {
                var anchor = editingSeries == null ? (HasDue.Checked ? Due.Value : CreatedPicker.Value).Date : Recurrence.ParseDate(editingSeries.AnchorDate);
                using (var dialog = new RepeatOptionsDialog(Rule, anchor, editingSeries == null ? 1 : editingSeries.GeneratedCount))
                    if (dialog.ShowDialog(FindForm()) == DialogResult.OK) SetRule(dialog.Result);
            };
            Add.Click += delegate { if (Submit != null) Submit(this, EventArgs.Empty); };
            TitleBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Add.PerformClick(); }
            };
        }

        public TaskEntry Read(TaskEntry original)
        {
            var task = original == null ? new TaskEntry() : original.Copy();
            task.Title = TitleBox.Text;
            task.Note = String.IsNullOrWhiteSpace(NoteBox.Text) ? null : NoteBox.Text;
            task.IsDaily = IsDaily.Checked;
            task.Weight = original != null && TaskPriority.Group(original.Weight) == Weight.SelectedIndex ? original.Weight : TaskPriority.Value(Weight.SelectedIndex);
            task.CreatedAt = creationDateEdited ? new DateTimeOffset(DateTime.SpecifyKind(CreatedPicker.Value, DateTimeKind.Local)) : DateTimeOffset.Now;
            task.DueAt = !task.IsDaily && HasDue.Checked ? (DateTimeOffset?)new DateTimeOffset(DateTime.SpecifyKind(Due.Value, DateTimeKind.Local)) : null;
            if (task.IsDaily) task.CompletedAt = null;
            else task.DailyCompletedOn = null;
            task.Validate(DateTimeOffset.Now);
            return task;
        }

        public void LoadTask(TaskEntry task)
        {
            TitleBox.Text = task.Title;
            NoteBox.Text = task.Note ?? "";
            Weight.SelectedIndex = TaskPriority.Group(task.Weight);
            CreatedPicker.Value = task.CreatedAt.LocalDateTime;
            creationDateEdited = true;
            HasDue.Checked = task.DueAt.HasValue;
            if (task.DueAt.HasValue) Due.Value = task.DueAt.Value.LocalDateTime;
            IsDaily.Checked = task.IsDaily;
        }

        public void Reset()
        {
            TitleBox.Clear(); NoteBox.Clear(); IsDaily.Checked = false; CreatedPicker.Value = DateTime.Now; creationDateEdited = false; HasDue.Checked = false; HasRepeat.Checked = false; SetRule(new RepeatRule()); Weight.SelectedIndex = 1; TitleBox.Focus();
        }

        public void SetColorSettings(TaskColorSettings settings)
        {
            dynamicColors = settings != null && settings.Enabled;
            UpdateWeightColors();
        }
        private void UpdateWeightColors()
        {
            bool timed = dynamicColors && !IsDaily.Checked;
            for (int i = 0; i < weightLegend.Count; i++) weightLegend[i].ForeColor = timed ? Color.Black : TaskPriority.StaticColor(TaskPriority.Value(i));
            weightHint.Text = timed ? "Age/due colors: Settings." : "Creation dates can be backdated.";
            Weight.Invalidate();
        }

        public void SetRule(RepeatRule rule) { Rule = rule.Copy(); UpdateRepeatSummary(); }
        private void UpdateRepeatSummary() { repeatSummary.Text = HasRepeat.Checked ? Rule.Summary() : editingSeries != null ? "Future repeats will stop; existing tasks are kept." : ""; }
        public void LoadSeries(TaskSeries series)
        {
            editingSeries = series;
            if (series != null) { SetRule(series.Rule); HasRepeat.Checked = series.Enabled && !IsDaily.Checked; }
            UpdateRepeatSummary();
        }

        public void RefreshCreationDate()
        {
            if (creationDateEdited || CreatedPicker.ContainsFocus) return;
            refreshingCreationDate = true; CreatedPicker.Value = DateTime.Now; refreshingCreationDate = false;
        }

        public Control[] InputTabOrder()
        {
            var entries = new List<Control> { TitleBox, Weight };
            entries.AddRange(CreatedPicker.Fields); entries.Add(HasDue); entries.AddRange(Due.Fields);
            entries.AddRange(new Control[] { NoteBox, IsDaily, HasRepeat, RepeatSettings, Add });
            return entries.Where(c => c.Enabled && c.Visible && c.TabStop).ToArray();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Tab && (keyData & (Keys.Control | Keys.Alt)) == Keys.None)
            {
                var entries = InputTabOrder(); int current = Array.FindIndex(entries, c => c.ContainsFocus);
                if (current >= 0 && entries.Length > 0)
                {
                    int direction = (keyData & Keys.Shift) != 0 ? -1 : 1;
                    int next = current + direction;
                    if (next >= 0 && next < entries.Length) { entries[next].Focus(); return true; }
                }
            }
            return base.ProcessDialogKey(keyData);
        }
    }

    public sealed class TaskList : ListBox
    {
        private readonly ToolTip tooltip = new ToolTip();
        private readonly Dictionary<string, string> paintedTimers = new Dictionary<string, string>();
        private readonly Dictionary<string, int> paintedColors = new Dictionary<string, int>();
        private TaskColorSettings colorSettings;
        public TaskColorSettings ColorSettings { get { return colorSettings; } set { colorSettings = value; Invalidate(); } }
        private int hovered = -1;
        private readonly Label emptyLabel = new Label { Dock = DockStyle.Top, Height = 70, Padding = new Padding(12, 18, 12, 0), ForeColor = SystemColors.GrayText, BackColor = Color.White };
        public string EmptyText { get { return emptyLabel.Text; } set { emptyLabel.Text = value; } }
        public DateTimeOffset? PreviewNow { get; set; }

        public TaskList()
        {
            Dock = DockStyle.Fill;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 59;
            IntegralHeight = false;
            BorderStyle = BorderStyle.Fixed3D;
            BackColor = Color.White;
            // A native ListBox ignores managed double buffering unless it also
            // owns painting. Draw complete frames without a separate erase pass.
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AccessibleName = "Task list";
            Controls.Add(emptyLabel);
            MouseMove += delegate(object sender, MouseEventArgs e)
            {
                int index = IndexFromPoint(e.Location);
                if (index == hovered) return;
                hovered = index;
                if (index >= 0)
                {
                    var task = (TaskEntry)Items[index];
                    tooltip.SetToolTip(this, task.Title + "\nWeight: " + TaskPriority.Name(task.Weight) +
                        (task.IsDaily ? "\n" + (DailyTasks.IsDone(task, PreviewNow ?? DateTimeOffset.Now) ? "Done today" : "To do today") + "; resets at midnight in your system's local time." : "\nCreated: " + task.CreatedAt.LocalDateTime.ToString("f")) +
                        (!task.IsDaily && task.DueAt.HasValue ? "\nDue: " + task.DueAt.Value.LocalDateTime.ToString("f") : "") +
                        (task.SeriesId != null ? "\nRepeat occurrence #" + task.OccurrenceNumber + " (edit to change future repeats)" : "") +
                        (task.HasNote ? "\n\nNote: " + task.Note : ""));
                }
                else tooltip.SetToolTip(this, "");
            };
        }

        protected override void Dispose(bool disposing) { if (disposing) tooltip.Dispose(); base.Dispose(disposing); }

        private int ScalePixel(int value) { return (int)Math.Round(value * Font.Height / 14.0); }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ItemHeight = Font.Height * 2 + ScalePixel(22);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            for (int index = Math.Max(0, TopIndex); index < Items.Count; index++)
            {
                var bounds = GetItemRectangle(index);
                if (bounds.Top >= ClientSize.Height) break;
                if (!e.ClipRectangle.IntersectsWith(bounds)) continue;
                var state = GetSelected(index) ? DrawItemState.Selected : DrawItemState.None;
                if (index == SelectedIndex && Focused && ShowFocusCues) state |= DrawItemState.Focus;
                OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, bounds, index, state, ForeColor, BackColor));
            }
            base.OnPaint(e);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            // Native scrolling still supplies keyboard, wheel, and scrollbar
            // behavior; repaint its new visible rows through our buffered path.
            if (message.Msg == 0x0115 || message.Msg == 0x020A) Invalidate();
        }

        public void RefreshTimers()
        {
            var now = PreviewNow ?? DateTimeOffset.Now;
            for (int index = Math.Max(0, TopIndex); index < Items.Count; index++)
            {
                var bounds = GetItemRectangle(index);
                if (bounds.Top >= ClientSize.Height) break;
                var task = (TaskEntry)Items[index];
                if (task.IsDaily) continue;
                int color = TaskColors.For(task, ColorSettings, now).ToArgb(), previousColor;
                if (!paintedColors.TryGetValue(task.Id, out previousColor) || previousColor != color) { Invalidate(bounds); continue; }
                string text = TaskTiming.Age(task, now) + "\n" + TaskTiming.Deadline(task, now), previous;
                if (paintedTimers.TryGetValue(task.Id, out previous) && text == previous) continue;
                int pad = ScalePixel(9), width = Math.Min(ScalePixel(174), bounds.Width / 2);
                Invalidate(new Rectangle(bounds.Right - width - pad, bounds.Top, width + pad, bounds.Height));
            }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            var task = (TaskEntry)Items[e.Index];
            var now = PreviewNow ?? DateTimeOffset.Now;
            bool doneToday = DailyTasks.IsDone(task, now);
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var background = new SolidBrush(selected ? Color.FromArgb(224, 236, 248) : doneToday ? Color.FromArgb(244, 244, 244) : Color.White)) e.Graphics.FillRectangle(background, e.Bounds);
            int pad = ScalePixel(9);
            int timerWidth = task.IsDaily ? 0 : Math.Min(ScalePixel(174), e.Bounds.Width / 2);
            int titleWidth = Math.Max(20, e.Bounds.Width - timerWidth - pad * 3);
            int lineHeight = Font.Height + ScalePixel(4);
            var titleBounds = new Rectangle(e.Bounds.X + pad, e.Bounds.Y + ScalePixel(7), titleWidth, lineHeight);
            var detailBounds = new Rectangle(e.Bounds.X + pad, titleBounds.Bottom + ScalePixel(2), titleWidth, lineHeight);
            var timerBounds = new Rectangle(e.Bounds.Right - timerWidth - pad, titleBounds.Y, timerWidth, lineHeight);
            var deadlineBounds = new Rectangle(timerBounds.X, detailBounds.Y, timerWidth, lineHeight);
            if (task.IsDaily)
            {
                titleBounds.Y = e.Bounds.Y + (e.Bounds.Height - lineHeight) / 2;
                ControlPaint.DrawCheckBox(e.Graphics, DailyCheckboxBounds(e.Bounds), doneToday ? ButtonState.Checked | ButtonState.Inactive : ButtonState.Normal);
                titleBounds.X += ScalePixel(24); titleBounds.Width = Math.Max(1, titleBounds.Width - ScalePixel(24));
            }
            if (!task.IsDaily && task.SeriesId != null)
            {
                int x = titleBounds.X + ScalePixel(1), y = titleBounds.Y + (lineHeight - ScalePixel(12)) / 2;
                using (var pen = new Pen(Color.FromArgb(110, 110, 110), Math.Max(1, ScalePixel(1))))
                {
                    e.Graphics.DrawArc(pen, x, y, ScalePixel(11), ScalePixel(11), 40, 300);
                    e.Graphics.DrawLine(pen, x + ScalePixel(10), y, x + ScalePixel(11), y + ScalePixel(5));
                    e.Graphics.DrawLine(pen, x + ScalePixel(11), y + ScalePixel(5), x + ScalePixel(7), y + ScalePixel(4));
                }
                titleBounds.X += ScalePixel(18); titleBounds.Width = Math.Max(1, titleBounds.Width - ScalePixel(18));
            }
            if (task.HasNote)
            {
                int iconX = titleBounds.X + ScalePixel(1);
                int iconY = titleBounds.Y + (lineHeight - ScalePixel(12)) / 2;
                using (var pen = new Pen(Color.FromArgb(110, 110, 110), Math.Max(1, ScalePixel(1))))
                {
                    e.Graphics.DrawRectangle(pen, iconX, iconY, ScalePixel(10), ScalePixel(12));
                    for (int line = 0; line < 3; line++)
                        e.Graphics.DrawLine(pen, iconX + ScalePixel(2), iconY + ScalePixel(3 + line * 3), iconX + ScalePixel(8), iconY + ScalePixel(3 + line * 3));
                }
                int iconSpace = ScalePixel(18);
                titleBounds.X += iconSpace; titleBounds.Width = Math.Max(1, titleBounds.Width - iconSpace);
            }
            var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
            Color titleColor = TaskColors.For(task, ColorSettings, now); paintedColors[task.Id] = titleColor.ToArgb();
            using (var titleFont = new Font(Font, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, task.Title, titleFont, titleBounds, titleColor, flags);
            if (!task.IsDaily)
            {
                string ageText = TaskTiming.Age(task, now), deadlineText = TaskTiming.Deadline(task, now);
                paintedTimers[task.Id] = ageText + "\n" + deadlineText;
                TextRenderer.DrawText(e.Graphics, ageText, Font, timerBounds, Color.FromArgb(75, 75, 75), flags | TextFormatFlags.Right);
                string detail = task.DueAt.HasValue ? "Due " + task.DueAt.Value.LocalDateTime.ToString("MMM d, yyyy  h:mm tt") : "Created " + task.CreatedAt.LocalDateTime.ToString("MMM d, yyyy  h:mm tt");
                TextRenderer.DrawText(e.Graphics, detail, Font, detailBounds, Color.FromArgb(105, 105, 105), flags);
                bool overdue = task.Weight > 0 && !task.CompletedAt.HasValue && task.DueAt.HasValue && task.DueAt.Value <= now;
                TextRenderer.DrawText(e.Graphics, deadlineText, Font, deadlineBounds, overdue ? TaskStyle.Weights[5] : Color.FromArgb(75, 75, 75), flags | TextFormatFlags.Right);
            }
            using (var pen = new Pen(Color.FromArgb(235, 235, 235))) e.Graphics.DrawLine(pen, e.Bounds.Left + pad, e.Bounds.Bottom - 1, e.Bounds.Right - pad, e.Bounds.Bottom - 1);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }

        private Rectangle DailyCheckboxBounds(Rectangle row)
        {
            int size = ScalePixel(15); return new Rectangle(row.Left + ScalePixel(9), row.Top + (row.Height - size) / 2, size, size);
        }

        public bool IsDailyCheckboxHit(Point point)
        {
            int index = IndexFromPoint(point);
            return index >= 0 && ((TaskEntry)Items[index]).IsDaily && DailyCheckboxBounds(GetItemRectangle(index)).Contains(point);
        }

        public void Populate(IEnumerable<TaskEntry> tasks, string selectedId)
        {
            int top = Items.Count > 0 ? TopIndex : 0;
            BeginUpdate(); Items.Clear(); paintedTimers.Clear(); paintedColors.Clear();
            foreach (var task in tasks) Items.Add(task);
            if (selectedId != null)
                for (int i = 0; i < Items.Count; i++) if (((TaskEntry)Items[i]).Id == selectedId) SelectedIndex = i;
            if (Items.Count > 0) TopIndex = Math.Min(top, Items.Count - 1);
            emptyLabel.Visible = Items.Count == 0;
            EndUpdate(); Invalidate(); hovered = -1; tooltip.SetToolTip(this, "");
        }
    }

    public sealed class EditTaskDialog : Form
    {
        public TaskEntry Result { get; private set; }
        public bool Repeats { get; private set; }
        public RepeatRule Rule { get; private set; }
        public EditTaskDialog(TaskEntry task, TaskSeries series = null, bool futureOnly = false, TaskColorSettings colors = null)
        {
            Text = "Edit task"; Font = new Font("Tahoma", 9); ClientSize = new Size(770, series == null ? 275 : 303);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
            var input = new TaskInput(true);
            input.SetColorSettings(colors);
            input.LoadTask(task);
            input.LoadSeries(series);
            if (futureOnly) input.IsDaily.Visible = false;
            input.Submit += delegate
            {
                try
                {
                    Result = input.Read(task); Repeats = input.HasRepeat.Checked; Rule = input.Rule.Copy();
                    if (Repeats)
                    {
                        Rule.Validate(series == null ? (Result.DueAt ?? Result.CreatedAt).LocalDateTime.Date : Recurrence.ParseDate(series.AnchorDate));
                        if (series != null && Rule.End == RepeatEnd.Count && Rule.TotalCount < series.GeneratedCount) throw new ArgumentException("The repeat total cannot be less than the number already created.");
                    }
                    DialogResult = DialogResult.OK;
                }
                catch (ArgumentException error) { MessageBox.Show(this, error.Message, "Check task", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 85 };
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 37, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
            bottom.Controls.Add(cancel); Controls.Add(input); Controls.Add(bottom); CancelButton = cancel;
            if (series != null)
            {
                var hint = new Label { Text = "Changes also apply to future repeats. Earlier occurrences keep their details.", Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(8, 3, 0, 0), ForeColor = SystemColors.GrayText };
                input.IsDaily.CheckedChanged += delegate { hint.Text = input.IsDaily.Checked ? "Switching to Daily stops future repeats. Other existing tasks are kept." : "Changes also apply to future repeats. Earlier occurrences keep their details."; };
                Controls.Add(hint); Controls.SetChildIndex(hint, 1);
            }
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        }
    }

    public sealed class MainForm : Form
    {
        private TaskDocument document;
        private readonly TaskRepository repository;
        private readonly TaskInput input = new TaskInput(false);
        private readonly TaskList dated = new TaskList { EmptyText = "No dated tasks. Add a task with a due date above.", AccessibleName = "Dated tasks", SelectionMode = SelectionMode.MultiExtended };
        private readonly TaskList undated = new TaskList { EmptyText = "No undated tasks. Add a task above to get started.", AccessibleName = "Undated tasks", SelectionMode = SelectionMode.MultiExtended };
        private readonly TaskList daily = new TaskList { EmptyText = "No daily tasks. Check Daily task when adding a task.", AccessibleName = "Daily tasks", SelectionMode = SelectionMode.MultiExtended };
        private readonly Label datedHeading = new Label();
        private readonly Label undatedHeading = new Label();
        private readonly Label dailyHeading = new Label();
        private readonly Button edit = new Button { Text = "&Edit", Width = 85 };
        private readonly Button complete = new Button { Text = "&Complete", Width = 95 };
        private readonly Button delete = new Button { Text = "&Delete", Width = 85 };
        private readonly ToolStripStatusLabel status = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ToolStripStatusLabel completedCounter = new ToolStripStatusLabel { AccessibleName = "Completed task counters" };
        private readonly Label selectionHint = new Label { Text = "Ctrl/Shift-click to select multiple tasks.", AutoSize = true, Margin = new Padding(14, 7, 0, 0), ForeColor = SystemColors.GrayText };
        private readonly Timer clock = new Timer { Interval = 1000 };
        private bool selecting;
        private DateTimeOffset nextRepeatCheck = DateTimeOffset.MinValue;
        private DateTimeOffset? previewTime;
        private string dailyDisplayedDate;
        private DateTimeOffset DisplayNow { get { return previewTime ?? DateTimeOffset.Now; } }

        public MainForm(TaskRepository storage, TaskDocument tasks)
        {
            repository = storage; document = tasks;
            Text = "Simple Tasks"; Font = new Font("Tahoma", 9);
            ClientSize = new Size(1240, 718); MinimumSize = new Size(1080, 508);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = SystemColors.Control; KeyPreview = true;
            var menu = new MenuStrip();
            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add("&Import calendar (.ics)...", null, delegate { ImportCalendar(); });
            file.DropDownItems.Add("&Export backup...", null, delegate { Export(); });
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("E&xit", null, delegate { Close(); });
            var view = new ToolStripMenuItem("&View");
            view.DropDownItems.Add("&Completed tasks...", null, delegate { ShowCompleted(); });
            view.DropDownItems.Add("&Repeating tasks...", null, delegate { ShowRepeating(); });
            var help = new ToolStripMenuItem("&Help");
            var settings = new ToolStripMenuItem("&Settings");
            settings.DropDownItems.Add("Task &colors...", null, delegate { ShowColorSettings(); });
            help.DropDownItems.Add(new ToolStripMenuItem("&User Guide...", null, delegate { using (var guide = new UserGuideDialog(repository.FilePath)) guide.ShowDialog(this); }) { ShortcutKeys = Keys.F1 });
            help.DropDownItems.Add(new ToolStripSeparator());
            help.DropDownItems.Add("&About Simple Tasks...", null, delegate { using (var about = new AboutTasksDialog(repository.FilePath)) about.ShowDialog(this); });
            menu.Items.AddRange(new ToolStripItem[] { file, view, settings, help }); MainMenuStrip = menu;
            var statusBar = new StatusStrip { SizingGrip = true };
            statusBar.Items.Add(status);
            statusBar.Items.Add(completedCounter);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 5, 10, 4), RowCount = 3, ColumnCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 236));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var inputGroup = new GroupBox { Text = "New task", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 9) };
            inputGroup.Controls.Add(input); layout.Controls.Add(inputGroup, 0, 0);
            var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            columns.Controls.Add(MakeColumn(datedHeading, dated, new Padding(0, 0, 6, 0)), 0, 0);
            columns.Controls.Add(MakeColumn(undatedHeading, undated, new Padding(6, 0, 6, 0)), 1, 0);
            columns.Controls.Add(MakeColumn(dailyHeading, daily, new Padding(6, 0, 0, 0)), 2, 0);
            layout.Controls.Add(columns, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
            actions.Controls.AddRange(new Control[] { edit, complete, delete, selectionHint });
            layout.Controls.Add(actions, 0, 2);
            Controls.Add(layout); Controls.Add(statusBar); Controls.Add(menu);
            input.Submit += delegate { AddTask(); };
            edit.Click += delegate { EditSelected(); }; complete.Click += delegate { CompleteSelected(); }; delete.Click += delegate { DeleteSelected(); };
            dated.SelectedIndexChanged += delegate { SelectList(dated); };
            undated.SelectedIndexChanged += delegate { SelectList(undated); };
            daily.SelectedIndexChanged += delegate { SelectList(daily); };
            dated.MouseDoubleClick += delegate(object sender, MouseEventArgs e) { if (dated.IndexFromPoint(e.Location) >= 0) EditSelected(); };
            undated.MouseDoubleClick += delegate(object sender, MouseEventArgs e) { if (undated.IndexFromPoint(e.Location) >= 0) EditSelected(); };
            daily.MouseDoubleClick += delegate(object sender, MouseEventArgs e) { if (daily.IndexFromPoint(e.Location) >= 0 && !daily.IsDailyCheckboxHit(e.Location)) EditSelected(); };
            daily.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && daily.IsDailyCheckboxHit(e.Location))
                {
                    int index = daily.IndexFromPoint(e.Location); ClearOtherSelections(null); daily.SetSelected(index, true); CompleteSelected();
                }
            };
            dated.KeyDown += ListKeyDown; undated.KeyDown += ListKeyDown; daily.KeyDown += ListKeyDown;
            var context = new ContextMenuStrip();
            context.Items.Add("Edit...", null, delegate { EditSelected(); }); context.Items.Add("Complete", null, delegate { CompleteSelected(); }); context.Items.Add("Delete...", null, delegate { DeleteSelected(); });
            context.Opening += delegate
            {
                context.Items[0].Enabled = context.Items[1].Enabled = SelectedTasks.Count == 1;
                context.Items[2].Enabled = SelectedTasks.Count > 0;
                context.Items[1].Text = Selected != null && DailyTasks.IsDone(Selected, DisplayNow) ? "Undo completion" : "Complete";
            };
            dated.ContextMenuStrip = context; undated.ContextMenuStrip = context; daily.ContextMenuStrip = context;
            foreach (var list in new[] { dated, undated, daily }) list.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                var target = (TaskList)sender;
                if (e.Button == MouseButtons.Left && (ModifierKeys & Keys.Control) == 0) ClearOtherSelections(target);
                if (e.Button == MouseButtons.Right)
                {
                    int index = target.IndexFromPoint(e.Location);
                    if (index < 0 || !target.GetSelected(index)) { ClearOtherSelections(null); if (index >= 0) target.SetSelected(index, true); }
                }
            };
            clock.Tick += delegate { input.RefreshCreationDate(); dated.RefreshTimers(); undated.RefreshTimers(); RefreshDailyDate(); CheckRepeats(); }; clock.Start();
            Shown += delegate { input.TitleBox.Focus(); CheckRepeats(); };
            RefreshTasks(null); status.Text = "Tasks save automatically on this computer.";
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        }

        protected override void Dispose(bool disposing) { if (disposing) clock.Dispose(); base.Dispose(disposing); }

        private Control MakeColumn(Label heading, TaskList list, Padding margin)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = margin, ColumnCount = 1, RowCount = 2 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 29)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            heading.Dock = DockStyle.Fill; heading.Font = new Font(Font, FontStyle.Bold); heading.TextAlign = ContentAlignment.MiddleLeft;
            list.Margin = Padding.Empty; panel.Controls.Add(heading, 0, 0); panel.Controls.Add(list, 0, 1); return panel;
        }

        private List<TaskEntry> SelectedTasks { get { return new[] { dated, undated, daily }.SelectMany(l => l.SelectedItems.Cast<TaskEntry>()).ToList(); } }
        private TaskEntry Selected { get { var selected = SelectedTasks; return selected.Count == 1 ? selected[0] : null; } }
        private void SelectList(TaskList chosen)
        {
            if (selecting) return;
            UpdateActions();
        }

        private void ClearOtherSelections(TaskList keep)
        {
            selecting = true;
            foreach (var list in new[] { dated, undated, daily }) if (list != keep) list.ClearSelected();
            selecting = false; UpdateActions();
        }

        private void UpdateActions()
        {
            int count = SelectedTasks.Count;
            edit.Enabled = complete.Enabled = count == 1; delete.Enabled = count > 0;
            delete.Text = count > 1 ? "&Delete (" + count + ")" : "&Delete";
            complete.Text = Selected != null && DailyTasks.IsDone(Selected, DisplayNow) ? "&Undo" : "&Complete";
            selectionHint.Text = count > 1 ? count + " tasks selected. Ctrl/Shift-click to adjust." : "Ctrl/Shift-click to select multiple tasks.";
        }

        private void RefreshTasks(string selectedId, string[] preserveIds = null)
        {
            dated.ColorSettings = undated.ColorSettings = daily.ColorSettings = document.ColorSettings;
            input.SetColorSettings(document.ColorSettings);
            selecting = true;
            var datedTasks = TaskTiming.ActiveTasks(document, true); var undatedTasks = TaskTiming.ActiveTasks(document, false);
            var dailyTasks = DailyTasks.List(document);
            dated.Populate(datedTasks, selectedId); undated.Populate(undatedTasks, selectedId); daily.Populate(dailyTasks, selectedId);
            if (preserveIds != null)
            {
                var keep = new HashSet<string>(preserveIds);
                foreach (var list in new[] { dated, undated, daily })
                    for (int index = 0; index < list.Items.Count; index++) if (keep.Contains(((TaskEntry)list.Items[index]).Id)) list.SetSelected(index, true);
            }
            selecting = false;
            datedHeading.Text = "Dated tasks (" + datedTasks.Count + ")"; undatedHeading.Text = "Undated tasks (" + undatedTasks.Count + ")";
            dailyHeading.Text = "Daily tasks (" + dailyTasks.Count(t => DailyTasks.IsDone(t, DisplayNow)) + "/" + dailyTasks.Count + " done)";
            dailyDisplayedDate = DailyTasks.Today(DisplayNow);
            completedCounter.Text = CompletionTotals.From(document).ToString();
            UpdateActions();
        }

        private void RefreshDailyDate()
        {
            if (dailyDisplayedDate != DailyTasks.Today(DisplayNow)) RefreshTasks(null, SelectedTasks.Select(t => t.Id).ToArray());
        }

        private bool Commit(List<TaskEntry> next, string selectedId, string message)
        {
            var candidate = document.Copy(); candidate.Tasks = next;
            return Commit(candidate, selectedId, message, true);
        }

        private bool Commit(TaskDocument candidate, string selectedId, string message, bool showError = true, string[] preserveIds = null)
        {
            try { repository.Save(candidate); }
            catch (Exception error)
            {
                status.Text = "Unable to save: " + error.Message;
                if (showError) MessageBox.Show(this, "The change could not be saved. Your previous tasks are unchanged.\n\n" + error.Message, "Unable to save", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            document = candidate; RefreshTasks(selectedId, preserveIds); status.Text = message + "   Saved " + DateTime.Now.ToString("h:mm tt") + "."; return true;
        }

        private void CheckRepeats()
        {
            var now = DateTimeOffset.Now; if (now < nextRepeatCheck) return;
            nextRepeatCheck = now.AddMinutes(1);
            try
            {
                var candidate = document.Copy(); int added = Recurrence.Generate(candidate, now, 1000, TimeZoneInfo.Local);
                if (added > 0 && Commit(candidate, null, added + " repeating task(s) added.", false, SelectedTasks.Select(t => t.Id).ToArray()) && added == 1000)
                    nextRepeatCheck = now.AddSeconds(1);
            }
            catch (Exception error) { status.Text = "Could not create repeating tasks: " + error.Message; }
        }

        private void AddTask()
        {
            try
            {
                var task = input.Read(null); var next = document.Copy(); next.Tasks.Add(task);
                if (input.HasRepeat.Checked) Recurrence.Attach(next, task, input.Rule, TimeZoneInfo.Local);
                Recurrence.Generate(next, DateTimeOffset.Now, 1000, TimeZoneInfo.Local);
                if (Commit(next, task.Id, "Task added.")) { input.Reset(); nextRepeatCheck = DateTimeOffset.MinValue; }
            }
            catch (ArgumentException error) { MessageBox.Show(this, error.Message, "Check task", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }

        private void EditSelected()
        {
            var selected = Selected; if (selected == null) return;
            using (var dialog = new EditTaskDialog(selected, document.Series.FirstOrDefault(s => s.Id == selected.SeriesId), colors: document.ColorSettings))
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    SaveEdit(selected, dialog);
        }

        private bool SaveEdit(TaskEntry selected, EditTaskDialog dialog)
        {
            try
            {
                var next = document.Copy();
                var series = next.Series.FirstOrDefault(s => s.Id == selected.SeriesId);
                next.Tasks = next.Tasks.Select(t => t.Id == selected.Id ? dialog.Result : t).ToList();
                if (series != null && dialog.Result.IsDaily)
                {
                    series.Enabled = false; dialog.Result.SeriesId = null; dialog.Result.OccurrenceNumber = 0;
                }
                else if (series != null) Recurrence.Update(series, dialog.Result, dialog.Rule, dialog.Repeats, TimeZoneInfo.Local);
                else if (dialog.Repeats) Recurrence.Attach(next, dialog.Result, dialog.Rule, TimeZoneInfo.Local);
                Recurrence.Generate(next, DateTimeOffset.Now, 1000, TimeZoneInfo.Local);
                bool saved = Commit(next, selected.Id, "Task updated.");
                if (saved) nextRepeatCheck = DateTimeOffset.MinValue;
                return saved;
            }
            catch (ArgumentException error) { MessageBox.Show(this, error.Message, "Check repeat settings", MessageBoxButtons.OK, MessageBoxIcon.Information); return false; }
        }

        private void CompleteSelected()
        {
            var selected = Selected; if (selected == null) return;
            if (selected.IsDaily)
            {
                var dailyTask = selected.Copy(); DailyTasks.Toggle(dailyTask, DisplayNow);
                Commit(document.Tasks.Select(t => t.Id == selected.Id ? dailyTask : t).ToList(), selected.Id,
                    DailyTasks.IsDone(dailyTask, DisplayNow) ? "Daily task done for today." : "Daily task marked incomplete.");
                return;
            }
            var completed = selected.Copy(); completed.CompletedAt = DateTimeOffset.Now;
            Commit(document.Tasks.Select(t => t.Id == selected.Id ? completed : t).ToList(), null, "Task completed. Find it under View > Completed tasks.");
        }

        private void DeleteSelected()
        {
            var selected = SelectedTasks; if (selected.Count == 0) return;
            using (var dialog = new DeleteTasksDialog(selected))
                if (dialog.ShowDialog(this) == DialogResult.Yes)
                    Commit(TaskOperations.Delete(document, selected.Select(t => t.Id)), null, selected.Count == 1 ? "Task deleted." : selected.Count + " tasks deleted.");
        }

        private void ListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                var list = (TaskList)sender; selecting = true; list.BeginUpdate();
                for (int i = 0; i < list.Items.Count; i++) list.SetSelected(i, true);
                list.EndUpdate(); selecting = false; UpdateActions(); e.SuppressKeyPress = true;
            }
            if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.Space) { CompleteSelected(); e.SuppressKeyPress = true; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.N)) { input.TitleBox.Focus(); return true; }
            if (keyData == (Keys.Control | Keys.E)) { EditSelected(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Export()
        {
            using (var dialog = new SaveFileDialog { Filter = "JSON backup (*.json)|*.json", FileName = "Simple Tasks backup.json", Title = "Export task backup", OverwritePrompt = true })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        if (String.Equals(System.IO.Path.GetFullPath(dialog.FileName), System.IO.Path.GetFullPath(repository.FilePath), StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Choose a different location from the active task file.");
                        new TaskRepository(dialog.FileName).Save(document); status.Text = "Backup exported.";
                    }
                    catch (Exception error) { MessageBox.Show(this, error.Message, "Unable to export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ImportCalendar()
        {
            using (var choose = new OpenFileDialog { Title = "Choose calendar file", Filter = "iCalendar files (*.ics)|*.ics|All files (*.*)|*.*", CheckFileExists = true })
            {
                if (choose.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var calendar = CalendarImport.ReadFile(choose.FileName, TimeZoneInfo.Local);
                    using (var dialog = new CalendarImportDialog(calendar, document))
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            var next = document.Copy(); int added = CalendarImport.Apply(next, dialog.SelectedItems, calendar.Name, DateTimeOffset.Now, dialog.SelectedWeight);
                            if (added > 0) Commit(next, null, added + " calendar task(s) imported.");
                        }
                }
                catch (Exception error) { MessageBox.Show(this, "The calendar could not be imported.\n\n" + error.Message, "Import calendar", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void ShowColorSettings()
        {
            using (var dialog = new ColorSettingsDialog(document.ColorSettings))
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var next = document.Copy(); next.ColorSettings = dialog.Result;
                    Commit(next, null, "Color settings saved.", true, SelectedTasks.Select(t => t.Id).ToArray());
                }
        }

        private void ShowCompleted()
        {
            using (var dialog = new Form { Text = "Completed tasks", Font = Font, ClientSize = new Size(760, 420), MinimumSize = new Size(620, 320), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, ShowInTaskbar = false })
            {
                var list = new TaskList { EmptyText = "No completed tasks saved.", SelectionMode = SelectionMode.MultiExtended, AccessibleName = "Completed tasks", ColorSettings = document.ColorSettings };
                list.Populate(document.Tasks.Where(t => !t.IsDaily && t.CompletedAt.HasValue).OrderByDescending(t => t.CompletedAt), null);
                var restore = new Button { Text = "Restore task", Width = 110, Enabled = false };
                var editCompleted = new Button { Text = "Edit...", Width = 85, Enabled = false };
                var deleteCompleted = new Button { Text = "Delete...", Width = 115, Enabled = false };
                var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Width = 85 };
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
                buttons.Controls.Add(restore); buttons.Controls.Add(editCompleted); buttons.Controls.Add(deleteCompleted); buttons.Controls.Add(close);
                var hint = new Label { Text = "Ctrl/Shift-click to select multiple tasks. Deleting keeps lifetime completion totals.", Dock = DockStyle.Top, Height = 38, Padding = new Padding(8, 10, 0, 0) };
                Action updateCompletedActions = delegate
                {
                    int count = list.SelectedItems.Count;
                    restore.Enabled = editCompleted.Enabled = count == 1; deleteCompleted.Enabled = count > 0;
                    deleteCompleted.Text = count > 1 ? "Delete (" + count + ")..." : "Delete...";
                };
                list.SelectedIndexChanged += delegate { updateCompletedActions(); };
                Action<string> refreshCompleted = delegate(string selectedId)
                {
                    list.Populate(document.Tasks.Where(t => !t.IsDaily && t.CompletedAt.HasValue).OrderByDescending(t => t.CompletedAt), selectedId);
                    updateCompletedActions();
                };
                Action editCompletedTask = delegate
                {
                    var task = list.SelectedItem as TaskEntry; if (task == null || list.SelectedItems.Count != 1) return;
                    using (var editor = new EditTaskDialog(task, document.Series.FirstOrDefault(s => s.Id == task.SeriesId), colors: document.ColorSettings))
                        if (editor.ShowDialog(dialog) == DialogResult.OK &&
                            SaveEdit(task, editor))
                            refreshCompleted(task.Id);
                };
                editCompleted.Click += delegate { editCompletedTask(); };
                list.MouseDoubleClick += delegate(object sender, MouseEventArgs e) { if (list.IndexFromPoint(e.Location) >= 0) editCompletedTask(); };
                restore.Click += delegate
                {
                    var task = list.SelectedItem as TaskEntry; if (task == null || list.SelectedItems.Count != 1) return;
                    var restored = task.Copy(); restored.CompletedAt = null;
                    if (Commit(document.Tasks.Select(t => t.Id == task.Id ? restored : t).ToList(), task.Id, "Task restored."))
                        refreshCompleted(null);
                };
                Action deleteCompletedTasks = delegate
                {
                    var selected = list.SelectedItems.Cast<TaskEntry>().ToList(); if (selected.Count == 0) return;
                    using (var confirmation = new DeleteTasksDialog(selected))
                        if (confirmation.ShowDialog(dialog) == DialogResult.Yes &&
                            Commit(TaskOperations.Delete(document, selected.Select(t => t.Id)), null,
                                selected.Count == 1 ? "Completed task deleted. Lifetime totals kept." : selected.Count + " completed tasks deleted. Lifetime totals kept."))
                            refreshCompleted(null);
                };
                deleteCompleted.Click += delegate { deleteCompletedTasks(); };
                list.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.Control && e.KeyCode == Keys.A)
                    {
                        list.BeginUpdate();
                        for (int i = 0; i < list.Items.Count; i++) list.SetSelected(i, true);
                        list.EndUpdate(); e.SuppressKeyPress = true;
                    }
                    if (e.KeyCode == Keys.Delete) { deleteCompletedTasks(); e.SuppressKeyPress = true; }
                };
                dialog.Controls.Add(list); dialog.Controls.Add(buttons); dialog.Controls.Add(hint); dialog.CancelButton = close;
                dialog.AutoScaleDimensions = new SizeF(96, 96); dialog.AutoScaleMode = AutoScaleMode.Dpi;
                dialog.ShowDialog(this);
            }
        }

        public void SetPreviewTime(DateTimeOffset now)
        {
            previewTime = now; dated.PreviewNow = undated.PreviewNow = daily.PreviewNow = now; clock.Stop();
            RefreshDailyDate(); dated.Invalidate(); undated.Invalidate(); daily.Invalidate();
        }

        private void ShowRepeating()
        {
            using (var dialog = new Form { Text = "Repeating tasks", Font = Font, ClientSize = new Size(900, 390), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, ShowInTaskbar = false })
            {
                var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
                var editFuture = new Button { Text = "Edit future...", Width = 110, Enabled = false };
                var stop = new Button { Text = "Stop repeating", Width = 120, Enabled = false };
                var close = new Button { Text = "Close", Width = 85, DialogResult = DialogResult.Cancel };
                Action<string> refresh = delegate(string selected)
                {
                    list.Items.Clear();
                    foreach (var series in document.Series.OrderBy(s => s.Template.Title))
                    {
                        int index = list.Items.Add(series); if (series.Id == selected) list.SelectedIndex = index;
                    }
                    editFuture.Enabled = list.SelectedItem != null;
                    stop.Enabled = list.SelectedItem != null && ((TaskSeries)list.SelectedItem).Enabled;
                };
                list.SelectedIndexChanged += delegate
                {
                    editFuture.Enabled = list.SelectedItem != null;
                    stop.Enabled = list.SelectedItem != null && ((TaskSeries)list.SelectedItem).Enabled;
                };
                Action editSeries = delegate
                {
                    var series = list.SelectedItem as TaskSeries; if (series == null) return;
                    using (var editor = new EditTaskDialog(series.Template, series, true, document.ColorSettings))
                        if (editor.ShowDialog(dialog) == DialogResult.OK)
                        {
                            try
                            {
                                var next = document.Copy(); var updated = next.Series.First(s => s.Id == series.Id);
                                Recurrence.Update(updated, editor.Result, editor.Rule, editor.Repeats, TimeZoneInfo.Local); Recurrence.Generate(next, DateTimeOffset.Now, 1000, TimeZoneInfo.Local);
                                if (Commit(next, Selected == null ? null : Selected.Id, "Future repeats updated.")) { refresh(series.Id); nextRepeatCheck = DateTimeOffset.MinValue; }
                            }
                            catch (ArgumentException error) { MessageBox.Show(dialog, error.Message, "Check repeat settings", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                        }
                };
                editFuture.Click += delegate { editSeries(); };
                list.MouseDoubleClick += delegate(object sender, MouseEventArgs e) { if (list.IndexFromPoint(e.Location) >= 0) editSeries(); };
                stop.Click += delegate
                {
                    var series = list.SelectedItem as TaskSeries; if (series == null) return;
                    var next = document.Copy(); next.Series.First(s => s.Id == series.Id).Enabled = false;
                    if (Commit(next, Selected == null ? null : Selected.Id, "Repeating stopped. Existing tasks are kept.")) refresh(series.Id);
                };
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
                buttons.Controls.AddRange(new Control[] { editFuture, stop, close });
                var hint = new Label { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8, 7, 0, 0), Text = document.Series.Count == 0 ? "No repeating tasks yet. Check Repeat when adding or editing a task." : "Edit future repeats here. Changes do not alter tasks already created; stopping keeps all existing tasks." };
                dialog.Controls.Add(list); dialog.Controls.Add(buttons); dialog.Controls.Add(hint); dialog.CancelButton = close;
                refresh(null); dialog.AutoScaleDimensions = new SizeF(96, 96); dialog.AutoScaleMode = AutoScaleMode.Dpi; dialog.ShowDialog(this);
            }
        }
    }
}

