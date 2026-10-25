using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SimpleTasks
{
    public sealed class GuideTopic
    {
        public readonly string Title, Body;
        public GuideTopic(string title, string body) { Title = title; Body = body; }
        public override string ToString() { return Title; }
    }

    public static class TaskHelp
    {
        public static GuideTopic[] Topics(string dataPath)
        {
            return new[] {
                new GuideTopic("Quick start",
                    "Simple Tasks keeps three kinds of tasks on this computer. Changes save automatically.\n\n" +
                    "1. In New task, type a name such as Doctor's visit.\n" +
                    "2. Choose None, Low, Medium, or High for Weight. Low is the default.\n" +
                    "3. For a deadline, check Due date and enter its date, time, and AM/PM. Leave it unchecked for an undated task. For an everyday habit, check Daily task instead.\n" +
                    "4. Optionally add a Note, such as Bring up symptom x.\n" +
                    "5. Click Add task. Your task appears in the appropriate column.\n\n" +
                    "Select a task and click Edit to change it, or double-click it. Click Complete when it is done. Completed dated and undated tasks move to View > Completed tasks; completed daily tasks stay in their column and turn gray.\n\n" +
                    "Use the topics on the left for detailed instructions. You can open this guide from Help > User Guide or by pressing F1 in the main window. All help is available offline."),
                new GuideTopic("The three columns",
                    "Dated tasks\nTasks with a deadline belong here. They are sorted by due date, earliest first, including overdue tasks. Each row shows its age since creation and time until due. After the deadline, the countdown becomes an overdue timer.\n\n" +
                    "Undated tasks\nUse these for work without a deadline. Each row shows its age since creation. The oldest tasks appear first. You can backdate Created to reflect when you first became aware of a task.\n\n" +
                    "Daily tasks\nUse these for habits or chores to finish by the end of each day. They have no deadline or age timer. Click the checkbox, select the task and click Complete, or press Space in the list. The task stays in place and turns gray. Click again, click Undo, or press Space to undo completion.\n\n" +
                    "Daily tasks become incomplete at midnight in your system's local time zone, including after reopening the app. Missed days do not create a backlog or duplicate tasks. The column heading shows how many are done today. Daily completions are separate from the lifetime completed-task counters."),
                new GuideTopic("Dates, notes, and editing",
                    "Adding or changing dates\nCreated starts at the current local date and time. Backdate it if you knew about the task earlier; future creation dates are not allowed. Check Due date to enter a deadline. A deadline may already be past. Uncheck it to make the task undated.\n\n" +
                    "Each date and time part is a separate field: month, day, year, hour, minute, and AM/PM. Tab moves forward and Shift+Tab moves back. Type Sep, September, or 9 for September. Use the ... button for a calendar picker. Check AM/PM carefully: 12 AM is midnight; 12 PM is noon.\n\n" +
                    "Notes\nNotes can contain several lines. For example, Doctor's visit can have the note Bring up symptom x. A page icon marks a task with a note. Hover over a row to preview its details and note, or double-click to edit it. Clear the Note field to remove a note.\n\n" +
                    "Editing\nSelect exactly one task and click Edit, double-click it, or use Ctrl+E in the main window. Change the details and click Save. Notes are kept when tasks are completed or restored. To edit a completed task, open View > Completed tasks and select one task.\n\n" +
                    "Dates display in your system's local time. Age and deadline timers measure actual elapsed time. For long intervals they show days, hours, and minutes; short intervals include seconds."),
                new GuideTopic("Weights and colors",
                    "Choose a weight when adding or editing a task:\n\n" +
                    "None: exempt from age/deadline color changes; stays black.\n" +
                    "Low: lower priority.\n" +
                    "Medium: middle priority.\n" +
                    "High: highest priority.\n\n" +
                    "With age/deadline coloring off, None and Low are black, Medium is yellow-orange, and High is red. This is the default mode.\n\n" +
                    "With age/deadline coloring on, undated tasks begin black and become more urgent in color as they age. Dated tasks become more urgent as their deadlines approach. Colors progress through light green, yellow-green, orange, and red. All overdue Low, Medium, and High dated tasks are red.\n\n" +
                    "Daily tasks keep their static weight colors and turn gray when completed, including tasks whose weight is None. Completed dated and undated tasks use their completion time for color calculations, so their colors do not continue changing just because time passes.\n\n" +
                    "The Color settings topic explains how to turn the optional mode on and change its timing."),
                new GuideTopic("Color settings",
                    "1. Open Settings > Task colors.\n" +
                    "2. Check Color tasks by age / time until due to enable the optional mode.\n" +
                    "3. Use the Undated tasks or Dated tasks tab. Enter when each weight should turn red.\n" +
                    "4. Click Save to apply the settings to existing tasks. Cancel discards changes.\n\n" +
                    "Default red thresholds\nUndated: Low after 12 days, Medium after 6 days, High after 3 days.\nDated: Low with 1 day remaining, Medium with 3 days remaining, High with 7 days remaining.\n\n" +
                    "The other colors are spaced automatically. For undated tasks, light green, yellow-green, and orange start at 25%, 50%, and 75% of the red age. For dated tasks, those colors start at 4, 3, and 2 times the red time remaining. For example, dated High becomes light green at 28 days, yellow-green at 21, orange at 14, and red at 7 days remaining. It is black above 28 days.\n\n" +
                    "One day means 24 elapsed hours. Fractions are allowed: 0.5 day is 12 hours. A transition begins at its exact threshold, not at midnight.\n\n" +
                    "Advanced\nCheck Advanced to edit every transition independently. Undated values must increase from light green through red; dated values must decrease. Turning Advanced off recalculates the other colors from red and rounds red to two decimal places, with a minimum of 0.01 day. Save applies the change.\n\n" +
                    "Turning coloring off retains your thresholds. Reset defaults restores the default thresholds, simple controls, and static coloring when you save."),
                new GuideTopic("Repeating tasks",
                    "Use Repeat for separate dated or undated occurrences, such as a weekly assignment. Use Daily task for a single daily checklist item that resets.\n\n" +
                    "1. Enter a normal task and its first creation date or deadline.\n" +
                    "2. Check Repeat and click its Settings button.\n" +
                    "3. Choose Daily with an interval, Weekly with an interval, or Selected weekdays.\n" +
                    "4. Choose an ending: Indefinitely, After total occurrences, After number of weeks, or On a specific date. Click OK, then Add task or Save.\n\n" +
                    "The total occurrence count includes the first task. Five means the first task plus four later occurrences. A two-week duration covers fourteen calendar days from the first occurrence; a specific end date is inclusive. The first task is kept even if its weekday is not one of the selected repeat days.\n\n" +
                    "Later occurrences appear at local midnight on their scheduled days. Dated repeats keep the chosen local due time. Their ages begin on the intended creation day. The app catches up missed occurrences when reopened. Each occurrence is independent, and unfinished ones remain visible.\n\n" +
                    "View > Repeating tasks\nUse Edit future to change the future schedule and template. Use Stop repeating to prevent later occurrences while keeping existing tasks. Editing an individual occurrence changes that task and future details; other occurrences already created keep their details. Deleting or completing an occurrence does not stop its schedule.\n\n" +
                    "Changing a repeating occurrence into a Daily task stops that series' future repeats and keeps its other existing occurrences."),
                new GuideTopic("Completion and counters",
                    "Completing tasks\nSelect one dated or undated task and click Complete, or press Space while its list has focus. It moves to View > Completed tasks. Its age stops at completion.\n\n" +
                    "Viewing and restoring\nOpen View > Completed tasks. The newest completions appear first. Select one task and click Edit to read or change its details and note. Click Restore task to return it to the active list. An overdue restored task is still overdue unless you change its deadline.\n\n" +
                    "Lifetime totals\nThe bottom bar shows completed total, completed dated, and completed undated. Daily completions are excluded. Deleting saved completed tasks preserves these totals, even after reopening the app.\n\n" +
                    "Restore undoes a task's completion and reduces the relevant total. Completing it again counts it again. Changing a saved completed task from dated to undated, or the reverse, moves its count between those categories.\n\n" +
                    "Editing and restoring work with one selected task. You can select several completed tasks for deletion; see Selecting and deleting."),
                new GuideTopic("Selecting and deleting",
                    "Select multiple tasks\nCtrl-click adds or removes individual selections. In the main window this can include different columns. Shift-click selects a range within a column. Ctrl+A selects all tasks in the focused list. A plain click in another column clears the other columns' selections.\n\n" +
                    "Delete tasks\nSelect one or more tasks, then click Delete or press Delete while the list has focus. The confirmation lists the selected tasks. Review it and choose Delete to remove them, or Cancel to keep them. Deletion removes task details permanently from the current save. A backup may still contain an earlier copy.\n\n" +
                    "Completed tasks\nOpen View > Completed tasks, select tasks with Ctrl/Shift-click or Ctrl+A, and click Delete or press Delete. Lifetime completed total, dated, and undated counts are preserved when you delete completed entries.\n\n" +
                    "Repeating tasks\nDeleting an occurrence removes only that occurrence. Future repeats continue. To stop the schedule, use View > Repeating tasks > Stop repeating.\n\n" +
                    "Edit and Complete work with a single selection, so those actions are unavailable while several tasks are selected."),
                new GuideTopic("Importing from Canvas",
                    "Canvas assignments are brought in manually from a downloaded .ics calendar file.\n\n" +
                    "1. Download your Canvas calendar as an .ics file. If the download is a ZIP archive, extract the .ics file first.\n" +
                    "2. In Simple Tasks, choose File > Import calendar (.ics).\n" +
                    "3. Choose the file, then set From and Through. Both dates are included, using due dates in your system's local time zone.\n" +
                    "4. Review the checked entries. Use Select all, Select none, or individual checkboxes. Choose a weight.\n" +
                    "5. Read the selected entry's note/link preview, then click Import selected.\n\n" +
                    "Canvas assignments only filters out other calendar events. Skipped entries lists warnings about entries that could not be imported.\n\n" +
                    "Imported assignments become dated tasks with their titles, deadlines, notes, and links. Their Created date is the import time; you can backdate it afterward. Already imported entries that are still saved, including completed tasks, are skipped on re-import. Deleting an imported task removes its duplicate record, so a later import may add it again.\n\n" +
                    "Re-importing does not update changed Canvas deadlines or overwrite your edits. Edit a deadline manually if it changes. The importer is the app's Canvas workflow; it does not connect to or automatically synchronize with Canvas. Recurring calendar rules are skipped rather than expanded into tasks."),
                new GuideTopic("Saving and backups",
                    "Automatic saving\nEvery successful change saves on this computer. Closing or reopening the app preserves your tasks, settings, repeat schedules, and completion totals. No account or internet connection is required.\n\n" +
                    "Your active data file:\n" + dataPath + "\n\n" +
                    "Export a backup\nChoose File > Export backup and save the JSON backup somewhere separate from the active data file. Keep an extra copy before moving computers or making large changes. Copying only the app's EXE does not copy your task data.\n\n" +
                    "Restore a backup manually\n1. Close Simple Tasks.\n2. Preserve a copy of the current tasks.json file.\n3. Open the data folder shown below and replace tasks.json with a copy of your exported backup, using the name tasks.json.\n4. Reopen Simple Tasks.\n\nData folder:\n" + Path.GetDirectoryName(dataPath) + "\n\n" +
                    "The file tasks.json.bak in that folder contains the previous saved version and may help recover a recent change. It is replaced by later saves, so exported backups are useful for longer-term recovery.\n\n" +
                    "If a change cannot be saved, the app reports the problem and keeps the previous saved tasks. If the data file cannot be opened, the app preserves it instead of silently replacing it."),
                new GuideTopic("Keyboard and local time",
                    "Keyboard shortcuts\nF1: open the User Guide from the main window.\nCtrl+N: focus the new task name.\nCtrl+E: edit the one selected active task.\nEnter in the task name: add the task, or save in the editor.\nTab / Shift+Tab: move forward / backward through available input fields.\nSpace in a main task list: complete the selected task, or toggle a daily task.\nDelete in a task list: review and confirm deletion.\nCtrl+A in a task list: select all tasks in that list.\nEscape in a dialog: cancel or close it.\n\n" +
                    "Use Ctrl-click for separate selections and Shift-click for a range. These selection shortcuts apply to task lists; Ctrl+A in a text field selects its text.\n\n" +
                    "Local time\nThe app uses your system's local time zone for date entry, date display, calendar import date filtering, and daily resets. New repeat occurrences are scheduled using local midnight and their chosen local due time. Existing saved deadlines keep their actual instant and display in local time. Calendar files may specify their own time zone; those dates are converted for local display.\n\n" +
                    "Age/deadline color thresholds measure elapsed hours: one day is 24 hours. Daily checklists reset on the local calendar day, which may contain 23 or 25 hours when daylight saving changes. This keeps daily resets at midnight instead of shifting by an hour.")
            };
        }
    }

    public sealed class UserGuideDialog : Form
    {
        public readonly ListBox Topics = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, AccessibleName = "Help topics" };
        public readonly TextBox Body = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = SystemColors.Window, Font = new Font("Tahoma", 10), AccessibleName = "Guide instructions" };
        public readonly Button CloseButton = new Button { Text = "Close", Width = 90, DialogResult = DialogResult.Cancel };
        public UserGuideDialog(string dataPath)
        {
            Text = "Simple Tasks User Guide"; Font = new Font("Tahoma", 9); ClientSize = new Size(880, 580); MinimumSize = new Size(760, 430);
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MinimizeBox = false;
            var columns = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 2, RowCount = 1 };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var reading = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 8, 12, 10), BackColor = SystemColors.Window };
            var heading = new Label { Dock = DockStyle.Top, Height = 38, Font = new Font("Tahoma", 13, FontStyle.Bold) };
            reading.Controls.Add(Body); reading.Controls.Add(heading); columns.Controls.Add(Topics, 0, 0); columns.Controls.Add(reading, 1, 0);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(8, 3, 8, 3), FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(CloseButton); Controls.Add(columns); Controls.Add(buttons); CancelButton = CloseButton;
            Topics.TabIndex = 0; Body.TabIndex = 1; buttons.TabIndex = 2;
            Topics.Items.AddRange(TaskHelp.Topics(dataPath));
            Topics.SelectedIndexChanged += delegate
            {
                var topic = Topics.SelectedItem as GuideTopic; if (topic == null) return;
                heading.Text = topic.Title; Body.Text = topic.Body.Replace("\n", Environment.NewLine); Body.SelectionStart = 0; Body.SelectionLength = 0; Body.ScrollToCaret();
            };
            Topics.SelectedIndex = 0;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        }
    }

    public sealed class AboutTasksDialog : Form
    {
        public readonly Button GuideButton = new Button { Text = "User Guide", Width = 110 };
        public readonly Button CloseButton = new Button { Text = "Close", Width = 90, DialogResult = DialogResult.Cancel };
        public AboutTasksDialog(string dataPath)
        {
            Text = "About Simple Tasks"; Font = new Font("Tahoma", 9); ClientSize = new Size(620, 430); MinimumSize = new Size(620, 360);
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 22, 28, 42 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(new Label { Text = "Simple Tasks", Dock = DockStyle.Fill, Font = new Font("Tahoma", 16, FontStyle.Bold) }, 0, 0);
            var description = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = SystemColors.Control,
                Text = "A local Windows to-do list with dated, undated, and daily tasks.\n\nKeep notes, set repeating schedules, choose None/Low/Medium/High weights, and optionally color tasks by age or time until due. Completed tasks can be restored or deleted while retaining lifetime completion totals.\n\nCanvas assignments are brought in through File > Import calendar (.ics), with a selectable date range and duplicate detection. This manual importer is the Canvas workflow; automatic Canvas synchronization is not included.\n\nTasks save automatically on this computer. Dates and daily resets use the system's local time. Use File > Export backup to keep an extra copy of your data." };
            description.Text = description.Text.Replace("\n", Environment.NewLine);
            layout.Controls.Add(description, 0, 1);
            layout.Controls.Add(new Label { Text = "Saved task data (select and copy the path):", Dock = DockStyle.Fill }, 0, 2);
            layout.Controls.Add(new TextBox { Text = dataPath, ReadOnly = true, Dock = DockStyle.Fill, AccessibleName = "Saved task file path" }, 0, 3);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 7, 0, 0) };
            buttons.Controls.Add(CloseButton); buttons.Controls.Add(GuideButton); layout.Controls.Add(buttons, 0, 4); Controls.Add(layout); CancelButton = CloseButton;
            GuideButton.Click += delegate { using (var guide = new UserGuideDialog(dataPath)) guide.ShowDialog(this); };
            Shown += delegate { GuideButton.Focus(); description.SelectionStart = 0; description.SelectionLength = 0; };
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        }
    }
}
