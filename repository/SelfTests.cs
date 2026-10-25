using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SimpleTasks
{
    internal static class SelfTests
    {
        private static readonly List<string> report = new List<string>();
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            report.Add("PASS: " + message);
        }

        public static int Run(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            try
            {
                var now = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(-7));
                var undated = new TaskEntry { Id = "undated", Title = "  Old task  ", CreatedAt = now.AddDays(-30), Weight = 1 };
                undated.Validate(now);
                Check(undated.Title == "Old task", "Task names are trimmed.");
                Check(TaskTiming.Age(undated, now) == "Age: 30d 0h 0m", "Backdated tasks show the correct age.");
                Check(TaskTiming.Deadline(undated, now) == "", "Undated tasks have no deadline timer.");
                var dated = new TaskEntry { Id = "dated", Title = "Essay", CreatedAt = now.AddDays(-2), DueAt = now.AddHours(3), Weight = 5 };
                Check(TaskTiming.Deadline(dated, now) == "Due in: 3h 0m", "Dated tasks count down to their deadline.");
                Check(TaskTiming.Age(dated, now) == "Age: 2d 0h 0m", "Dated tasks also show their age.");
                Check(TaskTiming.Deadline(dated, now.AddHours(4)) == "Overdue: 1h 0m", "Deadlines become overdue timers after passing.");
                Check(TaskTiming.Deadline(dated, now.AddHours(3)) == "Overdue: 0s", "The exact deadline boundary is handled.");
                Check(TaskTiming.Duration(TimeSpan.FromSeconds(65)) == "1m 5s", "Short timers include seconds.");
                Check(TaskTiming.Duration(TimeSpan.FromDays(400)) == "400d 0h 0m", "Long task ages do not wrap after a year.");
                var offsetEquivalent = dated.Copy(); offsetEquivalent.DueAt = dated.DueAt.Value.ToOffset(TimeSpan.FromHours(2));
                Check(TaskTiming.Deadline(offsetEquivalent, now) == TaskTiming.Deadline(dated, now), "Timers compare instants correctly across time zones.");
                bool rejected = false;
                try { new TaskEntry { Title = " ", CreatedAt = now, Weight = 1 }.Validate(now); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Blank task names are rejected.");
                rejected = false;
                try { new TaskEntry { Title = "Future", CreatedAt = now.AddDays(1), Weight = 1 }.Validate(now); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Future creation dates are rejected.");
                rejected = false;
                try { new TaskEntry { Title = "Bad weight", CreatedAt = now, Weight = 6 }.Validate(now); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Weights above 5 are rejected.");
                var document = new TaskDocument { Tasks = new List<TaskEntry> { dated, undated } };
                var repository = new TaskRepository(Path.Combine(outputDirectory, "roundtrip.json"));
                repository.Save(document);
                var loaded = repository.Load();
                Check(loaded.Tasks.Count == 2 && loaded.Tasks[0].DueAt == dated.DueAt && loaded.Tasks[1].CreatedAt == undated.CreatedAt, "Saving and reopening preserves dates and both task types.");
                loaded.Tasks[0].Title = "Updated essay"; repository.Save(loaded);
                Check(new TaskRepository(repository.FilePath + ".bak").Load().Tasks[0].Title == "Essay", "Saving keeps a recoverable previous copy.");
                Check(repository.Load().Tasks[0].Title == "Updated essay" && !File.Exists(repository.FilePath + ".tmp"), "The current save is replaced completely without a leftover temporary file.");
                var completed = dated.Copy(); completed.CompletedAt = now;
                document.Tasks.Add(completed);
                Check(TaskTiming.ActiveTasks(document, true).Count == 1, "Completed tasks leave the active lists.");
                Check(TaskTiming.Age(completed, now.AddDays(1)) == TaskTiming.Age(completed, now), "Completed task ages stop at completion.");
                completed.CompletedAt = null; completed.Id = "restored";
                Check(TaskTiming.ActiveTasks(document, true).Count == 2, "Restoring a task returns it to the active list.");
                var oldest = undated.Copy(); oldest.Id = "older"; oldest.CreatedAt = now.AddDays(-60); document.Tasks.Add(oldest);
                Check(TaskTiming.ActiveTasks(document, false)[0].Id == "older", "Undated tasks sort oldest first.");
                var sooner = dated.Copy(); sooner.Id = "sooner"; sooner.DueAt = now.AddHours(-1); document.Tasks.Add(sooner);
                Check(TaskTiming.ActiveTasks(document, true)[0].Id == "sooner", "Dated tasks sort by the nearest deadline, including overdue tasks.");
                var corruptPath = Path.Combine(outputDirectory, "invalid.json"); File.WriteAllText(corruptPath, "invalid");
                rejected = false; try { new TaskRepository(corruptPath).Load(); } catch { rejected = true; }
                Check(rejected && File.ReadAllText(corruptPath) == "invalid", "Invalid saved files are rejected and preserved.");
                RepeatTests.Run(Check, outputDirectory);
                CalendarTests.Run(Check, outputDirectory);
                DailyTests.Run(Check, outputDirectory);
                InteractionTests.Run(Check, outputDirectory);
                CompletedTaskTests.Run(Check, outputDirectory);
                ColorTests.Run(Check, outputDirectory);
                File.WriteAllLines(Path.Combine(outputDirectory, "test-results.txt"), report);
                return 0;
            }
            catch (Exception error)
            {
                report.Add("FAIL: " + error); File.WriteAllLines(Path.Combine(outputDirectory, "test-results.txt"), report); return 1;
            }
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control control in root.Controls)
            {
                yield return control;
                foreach (var nested in Descendants(control)) yield return nested;
            }
        }

        private static void Capture(Form form, string path)
        {
            form.PerformLayout(); Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path, ImageFormat.Png);
            }
        }

        public static int CheckRefresh(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Simple Tasks", "tasks.json");
                var document = new TaskRepository(path).Load();
                var now = DateTimeOffset.Now;
                var tasks = TaskTiming.ActiveTasks(document, true);
                tasks.Insert(0, new TaskEntry { Id = "refresh-check", Title = "Live counter check", CreatedAt = now.AddSeconds(-12), DueAt = now.AddSeconds(45), Weight = 1 });
                using (var form = new Form { Text = "Task refresh check", ClientSize = new Size(900, 440), Font = new Font("Tahoma", 9), ShowInTaskbar = false, Opacity = 0 })
                using (var list = new TaskList())
                using (var timer = new Timer { Interval = 1000 })
                {
                    form.Controls.Add(list); list.Populate(tasks, null); form.AutoScaleDimensions = new SizeF(96, 96); form.AutoScaleMode = AutoScaleMode.Dpi;
                    form.Show(); Application.DoEvents(); list.SelectedIndex = Math.Min(2, list.Items.Count - 1); list.TopIndex = 0; list.Focus(); Application.DoEvents(); list.Refresh();
                    bool refreshing = false, invalidatedTitle = false; int updates = 0;
                    list.Invalidated += delegate(object sender, InvalidateEventArgs e) { if (refreshing && e.InvalidRect.Left < list.ClientSize.Width / 2) invalidatedTitle = true; };
                    using (var before = new Bitmap(list.Width, list.Height))
                    {
                        list.DrawToBitmap(before, new Rectangle(Point.Empty, list.Size));
                        int selected = list.SelectedIndex, top = list.TopIndex; Exception failure = null;
                        timer.Tick += delegate
                        {
                            try
                            {
                                refreshing = true; list.RefreshTimers(); refreshing = false; list.Update(); updates++;
                                if (updates < 2) return;
                                timer.Stop();
                                using (var after = new Bitmap(list.Width, list.Height))
                                {
                                    list.DrawToBitmap(after, new Rectangle(Point.Empty, list.Size));
                                    before.Save(Path.Combine(outputDirectory, "refresh-before.png"), ImageFormat.Png);
                                    after.Save(Path.Combine(outputDirectory, "refresh-after.png"), ImageFormat.Png);
                                    int height = Math.Min(list.ItemHeight * 4, list.ClientSize.Height), split = list.ClientSize.Width / 2;
                                    for (int y = 0; y < height; y++)
                                    {
                                        // Native keyboard-focus cues may appear when the message
                                        // loop activates; compare text, not the row's focus border.
                                        if (y % list.ItemHeight < 4 || y % list.ItemHeight >= list.ItemHeight - 4) continue;
                                        for (int x = 2; x < split; x++)
                                            if (before.GetPixel(x, y) != after.GetPixel(x, y)) throw new Exception("A task title changed while only its timer should update at " + x + ", " + y + ".");
                                    }
                                    bool changed = false;
                                    for (int y = 2; y < list.ItemHeight && !changed; y++)
                                        for (int x = split; x < list.ClientSize.Width; x++)
                                            if (before.GetPixel(x, y) != after.GetPixel(x, y)) { changed = true; break; }
                                    if (!changed || invalidatedTitle) throw new Exception("Counters did not advance independently of the title area.");
                                    after.Save(Path.Combine(outputDirectory, "live-refresh-preview.png"), ImageFormat.Png);
                                }
                                if (list.SelectedIndex != selected || list.TopIndex != top || list.Items.Count != tasks.Count) throw new Exception("A timer update changed selection, scroll position, or task count.");
                                list.TopIndex = Math.Min(12, list.Items.Count - 1); form.Width -= 180; list.Refresh();
                                var bounds = list.GetItemRectangle(list.TopIndex);
                                if (list.IndexFromPoint(new Point(20, bounds.Top + list.ItemHeight / 2)) != list.TopIndex) throw new Exception("Scrolling or resizing broke row hit testing.");
                                File.WriteAllText(Path.Combine(outputDirectory, "refresh-results.txt"), "PASS: Two live timer updates across " + tasks.Count + " dated tasks (including a sample counter); counter pixels advance, title pixels remain unchanged, title areas are not invalidated, selection and scroll position remain stable, and resized/scrolled rows retain correct hit testing. Saved tasks were read only.");
                                form.Close();
                            }
                            catch (Exception error) { failure = error; timer.Stop(); form.Close(); }
                            finally { refreshing = false; }
                        };
                        timer.Start(); Application.Run(form);
                        if (failure != null) throw failure;
                    }
                }
                return 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(outputDirectory, "refresh-results.txt"), "FAIL: " + error); return 1; }
        }

        public static int Preview(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            try
            {
                var now = DateTimeOffset.Now;
                var document = new TaskDocument();
                string[] titles = { "Doctor's visit", "Submit history assignment", "Finish lab report", "Pay registration fee", "Submit final essay" };
                for (int i = 1; i <= 5; i++)
                    document.Tasks.Add(new TaskEntry { Id = "dated" + i, Title = titles[i - 1], CreatedAt = now.AddDays(-i * 2), DueAt = now.AddHours(40 - i * 10), Weight = i });
                string[] undatedTitles = { "Clean out desk drawer", "Organize lecture notes", "Email academic advisor", "Schedule study group", "Review scholarship requirements" };
                for (int i = 1; i <= 5; i++)
                    document.Tasks.Add(new TaskEntry { Id = "undated" + i, Title = undatedTitles[i - 1], CreatedAt = now.AddDays(-i * 3), Weight = i });
                document.Tasks[0].Note = "Bring up symptom x.\r\nAsk about follow-up care.";
                var repository = new TaskRepository(Path.Combine(outputDirectory, "ui-tasks.json"));
                using (var form = new MainForm(repository, document))
                {
                    form.ShowInTaskbar = false; form.Opacity = 0; form.Show(); form.SetPreviewTime(now);
                    Capture(form, Path.Combine(outputDirectory, "app-preview.png"));
                    var input = Descendants(form).OfType<TaskInput>().Single();
                    input.TitleBox.Text = "UI test: backdated assignment"; input.CreatedPicker.Value = DateTime.Now.AddDays(-14); input.HasDue.Checked = true; input.Due.Value = DateTime.Now.AddDays(-1); input.Weight.SelectedIndex = 3;
                    input.NoteBox.Text = "Bring up symptom x.\r\nSecond line of the note.";
                    string addedNote = input.NoteBox.Text;
                    input.Add.PerformClick(); Application.DoEvents();
                    var saved = repository.Load();
                    if (saved.Tasks.Count != 11 || saved.Tasks.Last().Weight != 5 || !saved.Tasks.Last().DueAt.HasValue || saved.Tasks.Last().CreatedAt > now.AddDays(-13)) throw new Exception("Adding a backdated overdue task through the input failed.");
                    if (saved.Tasks.Last().Note != addedNote || input.NoteBox.Text != "") throw new Exception("Task entry did not save the note or reset the note field.");
                    Descendants(form).OfType<Button>().Single(b => b.Text == "&Complete").PerformClick(); Application.DoEvents();
                    if (!repository.Load().Tasks.Last().CompletedAt.HasValue || repository.Load().Tasks.Last().Note != addedNote) throw new Exception("Completing the selected task did not preserve its note.");
                    if (!Descendants(form).OfType<StatusStrip>().Single().Items.OfType<ToolStripStatusLabel>().Any(l => l.Text == "Completed: 1 total  |  1 dated  |  0 undated")) throw new Exception("Completing a dated task did not update the visible counters.");
                    Exception completedError = null;
                    using (var completedTimer = new Timer { Interval = 100 })
                    {
                        completedTimer.Tick += delegate
                        {
                            completedTimer.Stop();
                            Form completedForm = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "Completed tasks");
                            try
                            {
                                if (completedForm == null) throw new Exception("Completed tasks window did not open.");
                                completedForm.Opacity = 0;
                                Capture(completedForm, Path.Combine(outputDirectory, "completed-preview.png"));
                                var completedList = Descendants(completedForm).OfType<TaskList>().Single();
                                completedList.SelectedIndex = 0;
                                Descendants(completedForm).OfType<Button>().Single(b => b.Text == "Restore task").PerformClick();
                                if (repository.Load().Tasks.Last().CompletedAt.HasValue || repository.Load().Tasks.Last().Note != addedNote) throw new Exception("Restoring a task did not preserve its note.");
                                if (!Descendants(form).OfType<StatusStrip>().Single().Items.OfType<ToolStripStatusLabel>().Any(l => l.Text == "Completed: 0 total  |  0 dated  |  0 undated")) throw new Exception("Restoring a task did not update the visible counters.");
                            }
                            catch (Exception error) { completedError = error; }
                            finally { if (completedForm != null) completedForm.Close(); }
                        };
                        completedTimer.Start();
                        ((ToolStripMenuItem)((ToolStripMenuItem)form.MainMenuStrip.Items[1]).DropDownItems[0]).PerformClick();
                    }
                    if (completedError != null) throw completedError;
                    using (var edit = new EditTaskDialog(document.Tasks[0]))
                    {
                        edit.ShowInTaskbar = false; edit.Opacity = 0; edit.Show();
                        Capture(edit, Path.Combine(outputDirectory, "edit-preview.png"));
                        var editor = Descendants(edit).OfType<TaskInput>().Single();
                        if (editor.NoteBox.Text != document.Tasks[0].Note) throw new Exception("Editing did not load the saved note.");
                        editor.NoteBox.Text = "Edited note\r\nStill multiline";
                        editor.HasDue.Checked = false; editor.TitleBox.Text = "Changed to undated"; editor.Add.PerformClick();
                        if (edit.Result == null || edit.Result.DueAt.HasValue || edit.Result.Title != "Changed to undated" || edit.Result.Id != document.Tasks[0].Id) throw new Exception("Editing a task did not preserve identity or change its type.");
                        if (edit.Result.Note != "Edited note\r\nStill multiline") throw new Exception("Editing did not update the multiline note.");
                        edit.Close();
                    }
                    input.TitleBox.Text = "Weekly check-in"; input.CreatedPicker.Value = DateTime.Now.AddDays(-7); input.NoteBox.Text = "Review progress.";
                    input.SetRule(new RepeatRule { Pattern = RepeatPattern.Weekly, End = RepeatEnd.Count, TotalCount = 3 }); input.HasRepeat.Checked = true;
                    input.Add.PerformClick(); Application.DoEvents();
                    var repeats = repository.Load();
                    if (repeats.Series.Count != 1 || repeats.Series[0].GeneratedCount != 2 || repeats.Tasks.Count(t => t.SeriesId != null) != 2) throw new Exception("Creating a repeating task through the input failed.");
                    Exception repeatEditError = null;
                    using (var editTimer = new Timer { Interval = 100 })
                    {
                        editTimer.Tick += delegate
                        {
                            editTimer.Stop(); var editorForm = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "Edit task");
                            try
                            {
                                if (editorForm == null) throw new Exception("The repeat occurrence editor did not open."); editorForm.Opacity = 0;
                                var editor = Descendants(editorForm).OfType<TaskInput>().Single();
                                if (!editor.HasRepeat.Checked || editor.NoteBox.Text != "Review progress.") throw new Exception("The editor did not load the recurrence or note.");
                                editor.NoteBox.Text = "Changed task and future note.";
                                Capture(editorForm, Path.Combine(outputDirectory, "repeat-edit-preview.png")); editor.Add.PerformClick();
                            }
                            catch (Exception error) { repeatEditError = error; if (editorForm != null) editorForm.Close(); }
                        };
                        editTimer.Start(); Descendants(form).OfType<Button>().Single(b => b.Text == "&Edit").PerformClick();
                    }
                    if (repeatEditError != null) throw repeatEditError;
                    var afterEdit = repository.Load();
                    if (afterEdit.Series[0].Template.Note != "Changed task and future note." || afterEdit.Tasks.Count(t => t.SeriesId != null && t.Note == "Changed task and future note.") != 1 || afterEdit.Series[0].GeneratedCount != 2)
                        throw new Exception("Editing an occurrence did not persist its note and future template independently of earlier occurrences.");
                    using (var options = new RepeatOptionsDialog(new RepeatRule { Pattern = RepeatPattern.Weekdays, End = RepeatEnd.Count, TotalCount = 8 }, DateTime.Today, 1))
                    {
                        options.ShowInTaskbar = false; options.Opacity = 0; options.Show(); Capture(options, Path.Combine(outputDirectory, "repeat-settings-preview.png"));
                        options.EndBox.SelectedIndex = (int)RepeatEnd.Weeks; options.EndCount.Value = 3;
                        ((Button)options.AcceptButton).PerformClick();
                        if (options.Result == null || options.Result.End != RepeatEnd.Weeks || options.Result.Weeks != 3) throw new Exception("Repeat options did not return the chosen ending rule."); options.Close();
                    }
                    Exception managerError = null;
                    using (var managerTimer = new Timer { Interval = 100 })
                    {
                        managerTimer.Tick += delegate
                        {
                            managerTimer.Stop(); var manager = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "Repeating tasks");
                            try
                            {
                                if (manager == null) throw new Exception("Repeating tasks window did not open."); manager.Opacity = 0;
                                Capture(manager, Path.Combine(outputDirectory, "repeating-preview.png"));
                                var list = Descendants(manager).OfType<ListBox>().Single(); list.SelectedIndex = 0;
                                Descendants(manager).OfType<Button>().Single(b => b.Text == "Stop repeating").PerformClick();
                                var stopped = repository.Load(); if (stopped.Series[0].Enabled || stopped.Tasks.Count != repeats.Tasks.Count) throw new Exception("Stopping repeating through the interface did not preserve existing tasks.");
                            }
                            catch (Exception error) { managerError = error; }
                            finally { if (manager != null) manager.Close(); }
                        };
                        managerTimer.Start(); ((ToolStripMenuItem)((ToolStripMenuItem)form.MainMenuStrip.Items[1]).DropDownItems[1]).PerformClick();
                    }
                    if (managerError != null) throw managerError;
                    input.TitleBox.Text = "Read ten pages"; input.NoteBox.Text = "Choose a book."; input.Weight.SelectedIndex = 2;
                    input.HasDue.Checked = true; input.HasRepeat.Checked = true; input.IsDaily.Checked = true;
                    if (input.HasDue.Checked || input.HasRepeat.Checked || input.CreatedPicker.Enabled || input.HasDue.Enabled || input.HasRepeat.Enabled) throw new Exception("Daily input still permits dates or ordinary repeating schedules.");
                    input.Add.PerformClick(); Application.DoEvents();
                    var dailyList = Descendants(form).OfType<TaskList>().Single(l => l.AccessibleName == "Daily tasks");
                    var savedDaily = repository.Load().Tasks.Last();
                    if (!savedDaily.IsDaily || savedDaily.DueAt.HasValue || dailyList.Items.Count != 1 || dailyList.SelectedItem == null) throw new Exception("Adding a daily task did not place it in the Daily column.");
                    Descendants(form).OfType<Button>().Single(b => b.Text == "&Complete").PerformClick();
                    if (!DailyTasks.IsDone(repository.Load().Tasks.Last(), now) || dailyList.Items.Count != 1 || dailyList.SelectedItem == null) throw new Exception("Completing a daily task removed it or failed to persist.");
                    Descendants(form).OfType<Button>().Single(b => b.Text == "&Undo").PerformClick();
                    if (DailyTasks.IsDone(repository.Load().Tasks.Last(), now)) throw new Exception("Undo did not mark the daily task incomplete.");
                    Descendants(form).OfType<Button>().Single(b => b.Text == "&Complete").PerformClick();
                    input.TitleBox.Text = "Drink water"; input.IsDaily.Checked = true; input.Add.PerformClick();
                    input.TitleBox.Text = "Stretch"; input.IsDaily.Checked = true; input.Add.PerformClick();
                    dailyList.ClearSelected(); Application.DoEvents();
                    Capture(form, Path.Combine(outputDirectory, "daily-tasks-preview.png"));
                    using (var dailyEditor = new EditTaskDialog(savedDaily))
                    {
                        dailyEditor.Opacity = 0; dailyEditor.Show();
                        var dailyInput = Descendants(dailyEditor).OfType<TaskInput>().Single();
                        if (!dailyInput.IsDaily.Checked || dailyInput.NoteBox.Text != "Choose a book." || dailyInput.CreatedPicker.Visible || dailyInput.Due.Visible) throw new Exception("The daily editor did not hide dates or retain the note.");
                        Capture(dailyEditor, Path.Combine(outputDirectory, "daily-edit-preview.png")); dailyEditor.Close();
                    }
                    form.SetPreviewTime(now.AddDays(1)); Application.DoEvents();
                    if (!Descendants(form).OfType<Label>().Any(l => l.Text == "Daily tasks (0/3 done)") || dailyList.Items.Count != 3) throw new Exception("The Daily column did not reset at the next local day.");
                    form.SetPreviewTime(now);
                    form.Size = new Size(form.MinimumSize.Width, Math.Max(form.MinimumSize.Height, (int)(600 * form.CurrentAutoScaleDimensions.Height / 96))); Capture(form, Path.Combine(outputDirectory, "compact-preview.png"));
                    File.WriteAllLines(Path.Combine(outputDirectory, "layout-metrics.txt"), new[] { "Form bounds: " + form.Bounds, "Client: " + form.ClientRectangle, "Scale: " + form.AutoScaleDimensions, "Current scale: " + form.CurrentAutoScaleDimensions }.Concat(Descendants(form).Select(c => c.GetType().Name + " " + c.Bounds + " client " + c.ClientRectangle + " font " + c.Font.Size)));
                    form.Close();
                }
                using (var empty = new MainForm(new TaskRepository(Path.Combine(outputDirectory, "empty.json")), new TaskDocument()))
                {
                    empty.ShowInTaskbar = false; empty.Opacity = 0; empty.Show(); Capture(empty, Path.Combine(outputDirectory, "empty-preview.png")); empty.Close();
                }
                File.WriteAllText(Path.Combine(outputDirectory, "ui-results.txt"), "PASS: Three task columns, existing task and note operations, repeat schedules, daily task creation, completion without removal, undo, next-day reset, daily editor without dates, compact layout, and empty-state rendering.");
                return 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(outputDirectory, "ui-results.txt"), "FAIL: " + error); return 1; }
        }
    }
}

