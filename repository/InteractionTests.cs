using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace SimpleTasks
{
    internal static class InteractionTests
    {
        public static void Run(Action<bool, string> check, string outputDirectory)
        {
            var now = DateTimeOffset.Now;
            var dated = new TaskEntry { Id = "counter-dated", Title = "Completed dated task", CreatedAt = now.AddDays(-3), DueAt = now.AddDays(-1), CompletedAt = now.AddHours(-2), Weight = 1 };
            var undated = new TaskEntry { Id = "counter-undated", Title = "Completed undated task", CreatedAt = now.AddDays(-3), CompletedAt = now.AddHours(-1), Weight = 1 };
            var daily = new TaskEntry { Id = "counter-daily", Title = "Daily task", CreatedAt = now, IsDaily = true, Weight = 1 };
            DailyTasks.Toggle(daily, now);
            var document = new TaskDocument { Tasks = new List<TaskEntry> { dated, undated, daily, new TaskEntry { Id = "active", Title = "Active", CreatedAt = now, Weight = 1 } } };
            var totals = CompletionTotals.From(document);
            check(totals.Total == 2 && totals.Dated == 1 && totals.Undated == 1, "Completed totals separate dated and undated tasks and exclude active and daily tasks.");
            var repository = new TaskRepository(Path.Combine(outputDirectory, "counter-roundtrip.json")); repository.Save(document);
            check(CompletionTotals.From(repository.Load()).Total == 2, "Completion totals persist through the existing completed-task save data.");
            var restored = document.Copy(); restored.Tasks[0].CompletedAt = null;
            check(CompletionTotals.From(restored).Total == 1 && CompletionTotals.From(restored).Dated == 0, "Restoring a completed task updates its category and total counters.");
            var recategorized = document.Copy(); recategorized.Tasks[1].DueAt = now;
            check(CompletionTotals.From(recategorized).Dated == 2 && CompletionTotals.From(recategorized).Undated == 0, "Editing the due date on a completed task updates its counter category.");
            var deleted = TaskOperations.Delete(document, new[] { dated.Id, daily.Id, dated.Id, "unknown" });
            check(deleted.Tasks.Count == 2 && document.Tasks.Count == 4 && deleted.Tasks.Any(t => t.Id == undated.Id), "Batch deletion removes only selected IDs without mutating the original document.");
            var repeated = new TaskEntry { Id = "repeat-delete", Title = "Repeating task", CreatedAt = now, DueAt = now.AddDays(1), Weight = 1 };
            document.Tasks.Add(repeated); Recurrence.Attach(document, repeated, new RepeatRule(), TimeZoneInfo.Local);
            string next = document.Series[0].NextDate;
            var noOccurrence = TaskOperations.Delete(document, new[] { repeated.Id, "active" });
            check(noOccurrence.Series.Count == 1 && noOccurrence.Series[0].NextDate == next && noOccurrence.Series[0].GeneratedCount == 1, "Deleting multiple occurrences leaves future repeat schedules and occurrence counts intact.");
            int month; string[] names = { "jan", "February", "MAR", "Apr", "May", "June", "Jul", "aug", "Sep", "oct", "November", "DEC" };
            check(names.Select((name, index) => DateTimeEntry.TryMonth(name, out month) && month == index + 1).All(result => result), "All twelve month names and abbreviations are accepted regardless of case.");
            check(DateTimeEntry.TryMonth("9", out month) && month == 9 && !DateTimeEntry.TryMonth("M", out month) && !DateTimeEntry.TryMonth("unknown", out month), "Month numbers are accepted and ambiguous or invalid month text is rejected.");
            using (var editor = new DateTimeEntry())
            {
                editor.Value = new DateTime(2024, 5, 31, 0, 5, 17, DateTimeKind.Local); editor.Month.Text = "Sep";
                check(editor.Value.Month == 9 && editor.Value.Day == 30 && editor.Value.Second == 17, "Typing Sep changes the month and safely adjusts a thirty-first day without losing seconds.");
                editor.Hour.Value = 12; editor.Period.SelectedIndex = 1;
                check(editor.Value.Hour == 12, "12 PM is interpreted as noon.");
                editor.Period.SelectedIndex = 0;
                check(editor.Value.Hour == 0, "12 AM is interpreted as midnight.");
                editor.Hour.Value = 1; editor.Minute.Value = 59; editor.Period.SelectedIndex = 1;
                check(editor.Value.Hour == 13 && editor.Value.Minute == 59, "Hour, minute, and AM/PM fields combine into the intended deadline.");
                editor.Value = new DateTime(2024, 2, 29, 10, 30, 0); editor.Year.Value = 2025;
                check(editor.Value.Day == 28 && editor.Value.Month == 2, "Editing a leap-year date to a non-leap year keeps the date valid.");
            }
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
        }
        private static void Tab(Control from, bool backwards)
        {
            typeof(Control).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(from, new object[] { backwards ? Keys.Shift | Keys.Tab : Keys.Tab });
        }
        private static void Capture(Form form, string path)
        {
            Application.DoEvents();
            using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path, ImageFormat.Png); }
        }
        private static string Counter(MainForm form)
        {
            return Descendants(form).OfType<StatusStrip>().Single().Items.OfType<ToolStripStatusLabel>().Single(l => l.AccessibleName == "Completed task counters").Text;
        }

        private static void Delete(MainForm form, bool confirm, int count, string outputDirectory)
        {
            Exception failure = null;
            using (var timer = new Timer { Interval = 100 })
            {
                timer.Tick += delegate
                {
                    timer.Stop(); var dialog = Application.OpenForms.OfType<DeleteTasksDialog>().FirstOrDefault();
                    try
                    {
                        if (dialog == null || Descendants(dialog).OfType<ListBox>().Single().Items.Count != count) throw new Exception("The deletion confirmation does not list exactly the selected tasks.");
                        dialog.Opacity = 0; Capture(dialog, Path.Combine(outputDirectory, "batch-delete-preview.png"));
                        if (confirm) dialog.DeleteButton.PerformClick(); else dialog.DialogResult = DialogResult.Cancel;
                    }
                    catch (Exception error) { failure = error; if (dialog != null) dialog.Close(); else form.Close(); }
                };
                timer.Start(); Descendants(form).OfType<Button>().Single(b => b.Text.StartsWith("&Delete")).PerformClick();
            }
            if (failure != null) throw failure;
        }

        public static int Preview(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            try
            {
                using (var host = new Form { ClientSize = new Size(790, 260), Font = new Font("Tahoma", 9), Opacity = 0, ShowInTaskbar = false })
                using (var input = new TaskInput(false))
                {
                    host.Controls.Add(input); host.AutoScaleDimensions = new SizeF(96, 96); host.AutoScaleMode = AutoScaleMode.Dpi;
                    host.Show(); Application.DoEvents(); input.HasDue.Checked = true; input.HasRepeat.Checked = true;
                    var expected = new List<Control> { input.TitleBox, input.Weight, input.CreatedPicker.Month, input.CreatedPicker.Day, input.CreatedPicker.Year, input.CreatedPicker.Hour, input.CreatedPicker.Minute, input.CreatedPicker.Period, input.HasDue,
                        input.Due.Month, input.Due.Day, input.Due.Year, input.Due.Hour, input.Due.Minute, input.Due.Period, input.NoteBox, input.IsDaily, input.HasRepeat, input.RepeatSettings, input.Add };
                    input.TitleBox.Focus(); Application.DoEvents();
                    for (int i = 0; i < expected.Count - 1; i++) { Tab(expected[i], false); if (!expected[i + 1].ContainsFocus) throw new Exception("Forward Tab skipped entry " + (i + 1) + ": " + expected[i + 1].AccessibleName); }
                    for (int i = expected.Count - 1; i > 0; i--) { Tab(expected[i], true); if (!expected[i - 1].ContainsFocus) throw new Exception("Shift+Tab skipped entry " + (i - 1)); }
                    input.HasDue.Checked = false; input.HasDue.Focus(); Tab(input.HasDue, false);
                    if (!input.NoteBox.ContainsFocus) throw new Exception("Tab did not skip disabled due-date fields.");
                    input.IsDaily.Checked = true; input.Weight.Focus(); Tab(input.Weight, false);
                    if (!input.NoteBox.ContainsFocus) throw new Exception("Tab did not skip hidden daily date fields.");
                    input.IsDaily.Checked = false; input.HasDue.Checked = true;
                    input.CreatedPicker.Value = new DateTime(2026, 9, 20, 9, 30, 0); input.Due.Value = new DateTime(2026, 12, 15, 17, 0, 0);
                    input.CreatedPicker.Month.Text = "Sep"; input.CreatedPicker.Month.Focus(); Tab(input.CreatedPicker.Month, false);
                    if (!input.CreatedPicker.Day.ContainsFocus || input.CreatedPicker.Value.Month != 9) throw new Exception("Typed month names did not work during Tab navigation.");
                    input.TitleBox.Text = "Doctor's visit"; input.NoteBox.Text = "Bring up symptom x.";
                    Capture(host, Path.Combine(outputDirectory, "keyboard-input-preview.png")); host.Close();
                }

                var now = DateTimeOffset.Now; var document = new TaskDocument();
                for (int i = 0; i < 3; i++) document.Tasks.Add(new TaskEntry { Id = "active-dated-" + i, Title = "Assignment " + (i + 1), CreatedAt = now.AddDays(-3), DueAt = now.AddDays(i - 1), Weight = i + 1 });
                for (int i = 0; i < 2; i++) document.Tasks.Add(new TaskEntry { Id = "active-undated-" + i, Title = "Undated task " + (i + 1), CreatedAt = now.AddDays(-2), Weight = 1 });
                document.Tasks.Add(new TaskEntry { Id = "done-dated", Title = "Completed assignment", CreatedAt = now.AddDays(-3), DueAt = now.AddDays(-1), CompletedAt = now.AddHours(-2), Weight = 1 });
                document.Tasks.Add(new TaskEntry { Id = "done-undated", Title = "Completed undated task", CreatedAt = now.AddDays(-3), CompletedAt = now.AddHours(-1), Weight = 1 });
                var daily = new TaskEntry { Id = "daily", Title = "Read ten pages", IsDaily = true, CreatedAt = now, Weight = 1 }; DailyTasks.Toggle(daily, now); document.Tasks.Add(daily);
                Recurrence.Attach(document, document.Tasks[2], new RepeatRule(), TimeZoneInfo.Local);
                var repository = new TaskRepository(Path.Combine(outputDirectory, "interaction-ui.json")); repository.Save(document);
                using (var form = new MainForm(repository, document))
                {
                    form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); form.SetPreviewTime(now);
                    var dated = Descendants(form).OfType<TaskList>().Single(l => l.AccessibleName == "Dated tasks"); var undated = Descendants(form).OfType<TaskList>().Single(l => l.AccessibleName == "Undated tasks");
                    if (Counter(form) != "Completed: 2 total  |  1 dated  |  1 undated") throw new Exception("The completed task counters are incorrect on opening.");
                    dated.SetSelected(0, true); dated.SetSelected(1, true); undated.SetSelected(0, true);
                    if (!Descendants(form).OfType<Button>().Any(b => b.Text == "&Delete (3)" && b.Enabled) || Descendants(form).OfType<Button>().Single(b => b.Text == "&Edit").Enabled) throw new Exception("Multiple selection did not enable batch deletion or disable single-task editing.");
                    typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dated, new object[] { new KeyEventArgs(Keys.Control | Keys.A) });
                    if (dated.SelectedItems.Count != 3) throw new Exception("Ctrl+A did not select every task in the focused column.");
                    dated.ClearSelected(); dated.SetSelected(0, true); dated.SetSelected(1, true);
                    typeof(Control).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(undated, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 30, 30, 0) });
                    if (dated.SelectedItems.Count != 0 || undated.SelectedItems.Count != 1) throw new Exception("A plain click did not clear selections in other columns.");
                    dated.SetSelected(0, true); dated.SetSelected(1, true);
                    Capture(form, Path.Combine(outputDirectory, "multi-selection-preview.png"));
                    Delete(form, false, 3, outputDirectory);
                    if (repository.Load().Tasks.Count != 8) throw new Exception("Cancelling a batch deletion changed saved tasks.");
                    Delete(form, true, 3, outputDirectory);
                    var after = repository.Load();
                    if (after.Tasks.Count != 5 || after.Tasks.Any(t => t.Id == "active-dated-0" || t.Id == "active-dated-1" || t.Id == "active-undated-0") || after.Series.Count != 1 || Counter(form) != CompletionTotals.From(after).ToString()) throw new Exception("Batch deletion removed the wrong tasks or changed completion totals.");
                    undated.SetSelected(0, true); Descendants(form).OfType<Button>().Single(b => b.Text == "&Complete").PerformClick();
                    if (Counter(form) != "Completed: 3 total  |  1 dated  |  2 undated") throw new Exception("Completing a task did not update total and category counters.");
                    Capture(form, Path.Combine(outputDirectory, "completed-counters-preview.png"));
                    form.Size = new Size(form.MinimumSize.Width, form.MinimumSize.Height + 180); Capture(form, Path.Combine(outputDirectory, "interaction-compact-preview.png")); form.Close();
                }
                using (var reopened = new MainForm(repository, repository.Load()))
                {
                    if (Counter(reopened) != "Completed: 3 total  |  1 dated  |  2 undated") throw new Exception("Reopening lost the completed counters.");
                }
                File.WriteAllText(Path.Combine(outputDirectory, "interaction-results.txt"), "PASS: Forward and reverse Tab across all 20 input entries, skipping disabled and hidden date fields, typing Sep, separate completion counters with daily excluded, counters after completing and reopening, multiple selections across columns, one deletion confirmation, cancel without changes, and deleting only the selected tasks.");
                return 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(outputDirectory, "interaction-results.txt"), "FAIL: " + error); return 1; }
        }
    }
}
