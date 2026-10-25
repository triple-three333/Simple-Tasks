using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SimpleTasks
{
    public sealed class DateTimeEntry : UserControl
    {
        public readonly ComboBox Month = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems, AccessibleName = "Month" };
        public readonly NumericUpDown Day = Number(1, 31, "Day");
        public readonly NumericUpDown Year = Number(1753, 9998, "Year");
        public readonly NumericUpDown Hour = Number(1, 12, "Hour");
        public readonly NumericUpDown Minute = new MinuteNumber { Minimum = 0, Maximum = 59, AccessibleName = "Minute" };
        public readonly ComboBox Period = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "AM or PM" };
        private DateTime current = DateTime.Now;
        private bool updating;
        private readonly ToolTip hints = new ToolTip();
        public event EventHandler ValueChanged;

        private static NumericUpDown Number(int minimum, int maximum, string name)
        {
            return new NumericUpDown { Minimum = minimum, Maximum = maximum, AccessibleName = name };
        }

        private sealed class MinuteNumber : NumericUpDown
        {
            protected override void UpdateEditText()
            {
                base.UpdateEditText();
                if (Text.Length == 1 && Char.IsDigit(Text[0])) { ChangingText = true; Text = "0" + Text; }
            }
        }

        public Control[] Fields { get { return new Control[] { Month, Day, Year, Hour, Minute, Period }; } }

        public DateTimeEntry()
        {
            Width = 360; Height = 26; Margin = new Padding(3); TabStop = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 1, Margin = Padding.Empty };
            int[] widths = { 66, 10, 42, 10, 58, 42, 12, 42, 52, 26 };
            foreach (int width in widths) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var calendar = new Button { Text = "...", TabStop = false, AccessibleName = "Choose calendar date" };
            var controls = new Control[] { Month, Separator("/"), Day, Separator("/"), Year, Hour, Separator(":"), Minute, Period, calendar };
            for (int i = 0; i < controls.Length; i++)
            {
                controls[i].Dock = DockStyle.Fill; controls[i].Margin = new Padding(1, 1, 1, 1); layout.Controls.Add(controls[i], i, 0);
            }
            Controls.Add(layout);
            Month.Items.AddRange(Enumerable.Range(1, 12).Select(i => (object)CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(i)).ToArray());
            Period.Items.AddRange(new object[] { "AM", "PM" });
            for (int i = 0; i < Fields.Length; i++) Fields[i].TabIndex = i;
            foreach (var number in new[] { Day, Year, Hour, Minute })
            {
                number.ValueChanged += delegate { Changed(); };
                number.Enter += delegate(object sender, EventArgs e) { var field = (NumericUpDown)sender; field.Select(0, field.Text.Length); };
                hints.SetToolTip(number, number.AccessibleName);
            }
            Month.Enter += delegate { Month.SelectAll(); };
            Month.TextChanged += delegate { int month; Month.BackColor = TryMonth(Month.Text, out month) ? SystemColors.Window : Color.MistyRose; Changed(); };
            Month.Validating += delegate(object sender, CancelEventArgs e) { int month; e.Cancel = !TryMonth(Month.Text, out month); };
            Month.Validated += delegate { int month; if (TryMonth(Month.Text, out month)) Month.Text = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month); };
            Period.SelectedIndexChanged += delegate { Changed(); };
            hints.SetToolTip(Month, "Month name or number, for example Sep, September, or 9.");
            hints.SetToolTip(Period, "AM or PM. Press A or P to choose.");
            calendar.Click += delegate
            {
                var picker = new MonthCalendar { MaxSelectionCount = 1, MinDate = new DateTime(1753, 1, 1), MaxDate = new DateTime(9998, 12, 31) };
                picker.SetDate(current.Date);
                var popup = new ContextMenuStrip(); popup.Items.Add(new ToolStripControlHost(picker));
                picker.DateSelected += delegate(object sender, DateRangeEventArgs e) { Value = e.Start.Date.Add(current.TimeOfDay); popup.Close(); };
                popup.Closed += delegate { popup.Dispose(); }; popup.Show(calendar, new Point(0, calendar.Height));
            };
            Value = current;
        }

        private static Label Separator(string text) { return new Label { Text = text, TextAlign = ContentAlignment.MiddleCenter, TabStop = false }; }

        public static bool TryMonth(string text, out int month)
        {
            text = (text ?? "").Trim().TrimEnd('.');
            if (Int32.TryParse(text, out month)) return month >= 1 && month <= 12;
            var matches = Enumerable.Range(1, 12).Where(i => text.Length > 0 && CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(i).StartsWith(text, StringComparison.OrdinalIgnoreCase)).ToArray();
            month = matches.Length == 1 ? matches[0] : 0; return month != 0;
        }

        private void Changed()
        {
            if (updating) return;
            int month; if (!TryMonth(Month.Text, out month) || Period.SelectedIndex < 0) return;
            updating = true;
            Day.Maximum = DateTime.DaysInMonth((int)Year.Value, month);
            updating = false;
            var next = Read();
            if (next != current) { current = next; if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
        }

        private DateTime Read()
        {
            int month;
            if (!TryMonth(Month.Text, out month)) throw new ArgumentException("Enter a month such as Sep, September, or 9.");
            if (Period.SelectedIndex < 0) throw new ArgumentException("Choose AM or PM.");
            int day = Math.Min((int)Day.Value, DateTime.DaysInMonth((int)Year.Value, month));
            return new DateTime((int)Year.Value, month, day, (int)Hour.Value % 12 + (Period.SelectedIndex == 1 ? 12 : 0), (int)Minute.Value, current.Second, current.Kind).AddTicks(current.Ticks % TimeSpan.TicksPerSecond);
        }

        public DateTime Value
        {
            get { return Read(); }
            set
            {
                if (value.Year < 1753 || value.Year > 9998) throw new ArgumentOutOfRangeException("value", "Choose a year between 1753 and 9998.");
                bool changed = value != current; updating = true; current = value;
                Year.Value = value.Year; Month.Text = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(value.Month);
                Day.Maximum = DateTime.DaysInMonth(value.Year, value.Month); Day.Value = value.Day;
                Hour.Value = value.Hour % 12 == 0 ? 12 : value.Hour % 12; Minute.Value = value.Minute; Period.SelectedIndex = value.Hour >= 12 ? 1 : 0;
                updating = false; if (changed && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        protected override void Dispose(bool disposing) { if (disposing) hints.Dispose(); base.Dispose(disposing); }
    }
}
