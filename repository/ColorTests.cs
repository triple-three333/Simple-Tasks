using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SimpleTasks
{
    internal static class ColorTests
    {
        private static DateTimeOffset After(DateTimeOffset instant, decimal days) { return instant.AddTicks((long)(days * TimeSpan.TicksPerDay)); }
        private static bool Same(Color first, Color second) { return first.ToArgb() == second.ToArgb(); }
        public static void Run(Action<bool, string> check, string outputDirectory)
        {
            var now = DateTimeOffset.Now; var settings = new TaskColorSettings(); settings.Validate();
            var task = new TaskEntry { Id = "colors", Title = "Color test", CreatedAt = now, Weight = 5 };
            check(!settings.Enabled && Same(TaskColors.For(task, null, now), TaskStyle.Weights[5]) && Same(TaskColors.For(task, settings, now), TaskStyle.Weights[5]),
                "Age coloring defaults off and preserves original weight colors for existing files.");
            settings.Enabled = true;
            bool undated = true, dated = true, startsBlack = true;
            for (int weight = 1; weight <= 5; weight++)
            {
                task.Weight = weight; task.DueAt = null;
                startsBlack &= Same(TaskColors.For(task, settings, now), Color.Black);
                for (int c = 0; c < 4; c++)
                {
                    var boundary = After(now, settings.UndatedDays[TaskPriority.Group(weight) - 1][c]);
                    undated &= Same(TaskColors.For(task, settings, boundary.AddTicks(-1)), TaskStyle.Weights[c + 1]) && Same(TaskColors.For(task, settings, boundary), TaskStyle.Weights[c + 2]);
                }
                task.DueAt = now.AddDays(100);
                for (int c = 0; c < 4; c++)
                {
                    var boundary = After(task.DueAt.Value, -settings.DatedDays[TaskPriority.Group(weight) - 1][c]);
                    dated &= Same(TaskColors.For(task, settings, boundary.AddTicks(-1)), TaskStyle.Weights[c + 1]) && Same(TaskColors.For(task, settings, boundary), TaskStyle.Weights[c + 2]);
                }
            }
            check(startsBlack, "All undated weights 1–5 start black with age coloring enabled.");
            check(undated, "All twenty undated color transitions occur inclusively at the exact configured elapsed time.");
            check(dated, "All twenty dated color transitions occur inclusively at the configured time remaining.");
            task.Weight = 1; task.DueAt = null;
            check(Same(TaskColors.For(task, settings, now.AddDays(3)), TaskStyle.Weights[2]) && Same(TaskColors.For(task, settings, now.AddDays(6)), TaskStyle.Weights[3]), "Weight 1 turns light green at 72 hours and yellow-green at 144 hours.");
            task.Weight = 5;
            check(Same(TaskColors.For(task, settings, now.AddDays(3)), TaskStyle.Weights[5]), "Undated weight 5 reaches red after exactly three days.");
            task.DueAt = now.AddDays(7);
            check(Same(TaskColors.For(task, settings, now), TaskStyle.Weights[5]), "Dated High reaches red with seven days remaining.");
            bool overdue = true;
            for (int weight = 1; weight <= 5; weight++) { task.Weight = weight; task.DueAt = now; overdue &= Same(TaskColors.For(task, settings, now), TaskStyle.Weights[5]) && Same(TaskColors.For(task, settings, now.AddTicks(1)), TaskStyle.Weights[5]); }
            check(overdue, "All dated weights 1–5 are red at and after the due instant.");
            task.Weight = 0; task.CreatedAt = now.AddDays(-500);
            check(Same(TaskColors.For(task, settings, now), Color.Black) && Same(TaskColors.For(task, null, now), Color.Black), "Weight 0 stays black when overdue in either coloring mode.");
            task.DueAt = null;
            check(Same(TaskColors.For(task, settings, now), Color.Black), "Old undated weight 0 tasks are exempt from age coloring.");
            task.Weight = 4; task.IsDaily = true;
            check(Same(TaskColors.For(task, settings, now), TaskPriority.StaticColor(4)), "Incomplete daily tasks retain their static priority colors.");
            DailyTasks.Toggle(task, now); task.Weight = 0;
            check(Same(TaskColors.For(task, settings, now), SystemColors.GrayText), "Completed daily tasks turn gray, including exempt weight 0 tasks.");
            task.IsDaily = false; task.Weight = 1; task.CreatedAt = now; task.CompletedAt = now.AddDays(4);
            check(Same(TaskColors.For(task, settings, now.AddDays(100)), TaskStyle.Weights[2]), "Completed undated tasks freeze their age color at completion.");
            task.DueAt = now.AddDays(10); task.CompletedAt = now;
            check(Same(TaskColors.For(task, settings, now.AddDays(100)), Color.Black), "Completed dated tasks do not turn overdue after completion.");
            task.CompletedAt = null; task.DueAt = null;
            check(Same(TaskColors.For(task, settings, now.AddDays(3).ToOffset(TimeSpan.FromHours(9))), TaskStyle.Weights[2]), "Color timing compares elapsed instants rather than calendar days or UTC offsets.");
            var custom = settings.Copy(); custom.Advanced = true; custom.UndatedDays[0] = new[] { .01m, .02m, .03m, .04m }; custom.DatedDays[0] = new[] { 8m, 7m, 6m, 5m }; custom.Validate();
            check(Same(TaskColors.For(task, custom, After(now, .01m)), TaskStyle.Weights[2]) && settings.UndatedDays[0][0] == 3m, "Fractional-day settings work and copies do not mutate the original settings.");
            custom.UndatedDays[0][1] = .01m; bool rejected = false;
            try { custom.Validate(); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Undated thresholds reject equal or decreasing transition times.");
            custom = settings.Copy(); custom.DatedDays[2][2] = 99; rejected = false;
            try { custom.Validate(); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Dated thresholds reject out-of-order time remaining.");
            var repository = new TaskRepository(Path.Combine(outputDirectory, "color-settings.json"));
            var document = new TaskDocument { ColorSettings = settings.Copy(), DeletedCompletedDated = 7, DeletedCompletedUndated = 3 };
            task.Weight = 0; task.Validate(now); document.Tasks.Add(task.Copy());
            Recurrence.Attach(document, document.Tasks[0], new RepeatRule(), TimeZoneInfo.Local); Recurrence.Generate(document, now.AddDays(1), 100);
            repository.Save(document); var loaded = repository.Load();
            check(loaded.Tasks.All(t => t.Weight == 0) && loaded.Series[0].Template.Weight == 0 && loaded.ColorSettings.Enabled && loaded.Copy().ColorSettings.DatedDays[2][3] == 7m && CompletionTotals.From(loaded).Total == 10,
                "Weight 0 tasks/repeats, color settings, and lifetime counters survive saving and reopening together.");
            var legacy = new TaskDocument { Tasks = new List<TaskEntry> { task.Copy() } }; repository.Save(legacy);
            check(repository.Load().ColorSettings == null && Same(TaskColors.For(task, repository.Load().ColorSettings, now), Color.Black), "Older files without color settings load with static colors and no migration required.");
            using (var input = new TaskInput(false))
            {
                check(input.Weight.SelectedIndex == 1 && input.Weight.Items.Cast<string>().SequenceEqual(TaskPriority.Names), "Task input shows None, Low, Medium, and High, defaulting to Low.");
                input.TitleBox.Text = "Exempt task"; input.Weight.SelectedIndex = 0; var entry = input.Read(null); input.LoadTask(entry);
                check(entry.Weight == 0 && input.Weight.SelectedIndex == 0 && input.Read(entry).Weight == 0, "Creating and editing weight 0 preserves its value.");
            }
            var imported = new TaskDocument(); CalendarImport.Apply(imported, new[] { new CalendarItem { Key = "zero", Title = "Exempt assignment", Due = now } }, "Test", now, 0);
            check(imported.Tasks.Single().Weight == 0, "Calendar imports accept weight 0.");
            check(Enumerable.Range(0, 6).Select(TaskPriority.Name).SequenceEqual(new[] { "None", "Low", "Low", "Medium", "High", "High" }),
                "All six existing numeric values map to the agreed four priority labels.");
            check(Same(TaskPriority.StaticColor(1), Color.Black) && Same(TaskPriority.StaticColor(2), Color.Black)
                && Same(TaskPriority.StaticColor(3), Color.FromArgb(202, 150, 0)) && Same(TaskPriority.StaticColor(4), TaskStyle.Weights[5]),
                "Static priority colors are black for None/Low, yellow-orange for Medium, and red for High.");
            check(settings.DatedDays[0].SequenceEqual(new[] { 4m, 3m, 2m, 1m })
                && settings.DatedDays[1].SequenceEqual(new[] { 12m, 9m, 6m, 3m }) && settings.DatedDays[2].SequenceEqual(new[] { 28m, 21m, 14m, 7m }),
                "Dated Low, Medium, and High use exactly the agreed evenly spaced default transitions.");
            check(settings.UndatedDays.Select(r => r[3]).SequenceEqual(new[] { 12m, 6m, 3m }) && !settings.Advanced,
                "Undated red thresholds default to 12/6/3 days and settings default to red-only controls.");
            using (var input = new TaskInput(true))
            {
                bool preserved = true;
                for (int weight = 0; weight <= 5; weight++)
                {
                    var original = new TaskEntry { Id = "old-" + weight, Title = "Existing task", CreatedAt = now.AddDays(-1), Weight = weight };
                    input.LoadTask(original); input.NoteBox.Text = "Edited note";
                    preserved &= input.Weight.SelectedIndex == TaskPriority.Group(weight) && input.Read(original).Weight == weight;
                }
                check(preserved, "Editing a note preserves original numeric weights while showing grouped labels.");
                input.Weight.SelectedIndex = 2;
                check(input.Read(null).Weight == 3, "New Medium tasks use the existing numeric weight 3.");
            }
            var oldSettings = settings.Copy(); oldSettings.SchemaVersion = 0; oldSettings.Advanced = true;
            oldSettings.DatedDays = Enumerable.Range(0, 5).Select(i => new[] { 99m, 88m, 77m, 66m }).ToArray();
            oldSettings.UndatedDays = Enumerable.Range(0, 5).Select(i => new[] { 11m, 22m, 33m, 44m }).ToArray();
            var oldDocument = new TaskDocument { ColorSettings = oldSettings, DeletedCompletedDated = 7, Tasks = new List<TaskEntry> { new TaskEntry { Id = "legacy-two", Title = "Legacy Low task", Weight = 2, CreatedAt = now } } };
            repository.Save(oldDocument); string beforeUpgrade = File.ReadAllText(repository.FilePath); var upgraded = repository.Load();
            check(upgraded.ColorSettings.SchemaVersion == 2 && upgraded.ColorSettings.Enabled && !upgraded.ColorSettings.Advanced
                && upgraded.ColorSettings.DatedDays.Select(r => r[3]).SequenceEqual(new[] { 1m, 3m, 7m })
                && upgraded.ColorSettings.UndatedDays.Select(r => r[3]).SequenceEqual(new[] { 12m, 6m, 3m })
                && upgraded.Tasks.Single().Weight == 2 && upgraded.DeletedCompletedDated == 7 && beforeUpgrade == File.ReadAllText(repository.FilePath),
                "Old custom tables reset to the new defaults, retaining the toggle, task values, and lifetime totals without rewriting during load.");
            upgraded.ColorSettings.Advanced = true; upgraded.ColorSettings.DatedDays[1][0] = 14; repository.Save(upgraded);
            check(repository.Load().ColorSettings.Advanced && repository.Load().ColorSettings.DatedDays[1][0] == 14,
                "New advanced settings survive subsequent loads without being reset again.");
            using (var dialog = new ColorSettingsDialog(settings))
            {
                dialog.Undated[0, 3].Value = .01m; dialog.Dated[0, 3].Value = .01m; var fractional = dialog.ReadSettings();
                check(fractional.UndatedDays[0].SequenceEqual(new[] { .0025m, .005m, .0075m, .01m }) && fractional.DatedDays[0].SequenceEqual(new[] { .04m, .03m, .02m, .01m }),
                    "Red-only fractional inputs interpolate exactly without rounding away intermediate thresholds.");
            }
            using (var importDialog = new CalendarImportDialog(new CalendarFile(), new TaskDocument()))
            {
                importDialog.Weight.SelectedIndex = 3;
                check(importDialog.Weight.Items.Cast<string>().SequenceEqual(TaskPriority.Names) && importDialog.SelectedWeight == 5,
                    "Calendar imports show the same four priority names and map High to the existing backend value.");
            }
            using (var dialog = new ColorSettingsDialog(settings))
            {
                dialog.Advanced.Checked = true; dialog.Undated[0, 1].Value = 1; rejected = false;
                try { dialog.ReadSettings(); } catch (ArgumentException) { rejected = true; }
                check(rejected, "The settings window validates transition ordering before saving.");
            }
            document.ColorSettings.UndatedDays = null; repository.Save(document); rejected = false;
            try { repository.Load(); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Malformed saved color settings are rejected without replacing task data.");
            task.Weight = -1; rejected = false;
            try { task.Validate(now); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Negative task weights are rejected.");
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
        }
        private static void Require(bool success, string message) { if (!success) throw new Exception(message); }
        private static void Capture(Form form, string path)
        {
            Application.DoEvents(); using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path, ImageFormat.Png); }
        }
        private static void Settings(MainForm form, Action<ColorSettingsDialog> action)
        {
            Exception failure = null;
            using (var timer = new Timer { Interval = 100 })
            {
                timer.Tick += delegate
                {
                    timer.Stop(); var dialog = Application.OpenForms.OfType<ColorSettingsDialog>().FirstOrDefault();
                    try { Require(dialog != null, "Settings window did not open."); dialog.Opacity = 0; action(dialog); }
                    catch (Exception error) { failure = error; }
                    finally { if (dialog != null && !dialog.IsDisposed) dialog.Close(); else if (dialog == null) form.Close(); }
                };
                timer.Start(); ((ToolStripMenuItem)((ToolStripMenuItem)form.MainMenuStrip.Items[2]).DropDownItems[0]).PerformClick();
            }
            if (failure != null) throw failure;
        }
        private static int Pixels(TaskList list, Color color)
        {
            int count = 0;
            using (var image = new Bitmap(list.Width, list.Height))
            {
                list.DrawToBitmap(image, new Rectangle(Point.Empty, list.Size));
                for (int y = 5; y < list.ItemHeight / 2; y++) for (int x = 5; x < list.Width / 2; x++) if (image.GetPixel(x, y).ToArgb() == color.ToArgb()) count++;
            }
            return count;
        }
        public static int Preview(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            try
            {
                var now = DateTimeOffset.Now; var document = new TaskDocument { DeletedCompletedDated = 2 };
                for (int weight = 0; weight <= 5; weight++)
                {
                    document.Tasks.Add(new TaskEntry { Id = "u" + weight, Title = "Undated weight " + weight, CreatedAt = now.AddDays(-3), Weight = weight });
                    document.Tasks.Add(new TaskEntry { Id = "d" + weight, Title = "Dated weight " + weight, CreatedAt = now.AddDays(-5), DueAt = now.AddDays(3), Weight = weight });
                }
                var daily = new TaskEntry { Id = "daily", Title = "Daily task", CreatedAt = now, Weight = 4, IsDaily = true }; document.Tasks.Add(daily);
                var repository = new TaskRepository(Path.Combine(outputDirectory, "color-ui.json")); repository.Save(document);
                using (var form = new MainForm(repository, document))
                {
                    form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); form.SetPreviewTime(now);
                    var undated = Descendants(form).OfType<TaskList>().Single(l => l.AccessibleName == "Undated tasks"); undated.SelectedIndex = 2;
                    string original = File.ReadAllText(repository.FilePath);
                    Settings(form, delegate(ColorSettingsDialog dialog)
                    {
                        Require(!dialog.EnableColors.Checked && !Descendants(dialog).OfType<TabControl>().Single().Enabled, "Age coloring must start off with disabled threshold fields.");
                        dialog.EnableColors.Checked = true; dialog.Undated[0, 3].Value = 8;
                        dialog.DialogResult = DialogResult.Cancel;
                    });
                    Require(File.ReadAllText(repository.FilePath) == original && undated.SelectedIndex == 2, "Cancel must preserve saved settings and selection.");
                    Settings(form, delegate(ColorSettingsDialog dialog)
                    {
                        dialog.EnableColors.Checked = true;
                        Require(!dialog.Advanced.Checked && !dialog.Undated[0, 0].Visible && dialog.Undated[0, 3].Visible, "Normal settings must show only red thresholds.");
                        Capture(dialog, Path.Combine(outputDirectory, "color-settings-undated.png"));
                        Descendants(dialog).OfType<TabControl>().Single().SelectedIndex = 1;
                        Capture(dialog, Path.Combine(outputDirectory, "color-settings-dated.png"));
                        dialog.Advanced.Checked = true; Capture(dialog, Path.Combine(outputDirectory, "color-settings-advanced.png"));
                        dialog.Advanced.Checked = false; dialog.Undated[0, 3].Value = 10m; dialog.SaveButton.PerformClick();
                    });
                    Require(repository.Load().ColorSettings.Enabled && repository.Load().ColorSettings.UndatedDays[0][0] == 2.5m && undated.ColorSettings.Enabled && undated.SelectedIndex == 2,
                        "Save must persist and immediately apply color settings while preserving selection.");
                    Require(CompletionTotals.From(repository.Load()).Total == 2 && repository.Load().Tasks.Count == 13, "Saving settings must preserve tasks and lifetime counters.");
                    Capture(form, Path.Combine(outputDirectory, "age-colors-preview.png"));
                    Settings(form, delegate(ColorSettingsDialog dialog) { dialog.EnableColors.Checked = false; dialog.SaveButton.PerformClick(); });
                    Require(!repository.Load().ColorSettings.Enabled && !undated.ColorSettings.Enabled && repository.Load().ColorSettings.UndatedDays[0][0] == 2.5m, "Turning off age coloring must retain edited thresholds for later use.");
                    form.Close();
                }
                using (var reopened = new MainForm(repository, repository.Load()))
                {
                    reopened.Opacity = 0; reopened.ShowInTaskbar = false; reopened.Show(); reopened.SetPreviewTime(now);
                    Settings(reopened, delegate(ColorSettingsDialog dialog)
                    {
                        Require(!dialog.EnableColors.Checked && dialog.Undated[0, 0].Value == 2.5m, "Settings must survive reopening.");
                        Descendants(dialog).OfType<Button>().Single(b => b.Text == "Reset defaults").PerformClick();
                        Require(!dialog.EnableColors.Checked && !dialog.Advanced.Checked && dialog.Undated[0, 3].Value == 12m && dialog.Dated[2, 3].Value == 7m, "Reset defaults must restore the new red thresholds, simple settings, and static coloring.");
                        dialog.DialogResult = DialogResult.Cancel;
                    });
                    Settings(reopened, delegate(ColorSettingsDialog dialog)
                    {
                        dialog.EnableColors.Checked = true; dialog.Advanced.Checked = true; dialog.Dated[1, 0].Value = 14m; dialog.SaveButton.PerformClick();
                    });
                    Require(repository.Load().ColorSettings.Advanced && repository.Load().ColorSettings.DatedDays[1][0] == 14m, "Saving Advanced must retain individually edited transitions.");
                    reopened.Close();
                }
                using (var reopened = new MainForm(repository, repository.Load()))
                {
                    reopened.Opacity = 0; reopened.ShowInTaskbar = false; reopened.Show(); reopened.SetPreviewTime(now);
                    Settings(reopened, delegate(ColorSettingsDialog dialog)
                    {
                        Require(dialog.Advanced.Checked && dialog.Dated[1, 0].Value == 14m, "Advanced mode and custom thresholds must reopen intact.");
                        dialog.Advanced.Checked = false;
                        Require(dialog.ReadSettings().DatedDays[1].SequenceEqual(new[] { 12m, 9m, 6m, 3m }), "Returning to simple settings must restore even spacing from red.");
                        dialog.SaveButton.PerformClick();
                    }); reopened.Close();
                }
                using (var host = new Form { ClientSize = new Size(1000, 220), Font = new Font("Tahoma", 9), Opacity = 0, ShowInTaskbar = false })
                using (var list = new TaskList { ColorSettings = new TaskColorSettings { Enabled = true } })
                {
                    var task = new TaskEntry { Id = "live-color", Title = "Live color transition", CreatedAt = now.AddDays(-3).AddTicks(1), Weight = 1 };
                    list.PreviewNow = now; host.Controls.Add(list); list.Populate(new[] { task }, task.Id); host.Show(); Application.DoEvents(); list.Refresh();
                    Require(Pixels(list, TaskStyle.Weights[2]) == 0, "A task must not change color before the exact threshold.");
                    bool titleInvalidated = false; list.Invalidated += delegate(object sender, InvalidateEventArgs e) { if (e.InvalidRect.Left == 0) titleInvalidated = true; };
                    list.PreviewNow = now.AddTicks(1); list.RefreshTimers(); list.Update();
                    Require(titleInvalidated && Pixels(list, TaskStyle.Weights[2]) > 10 && list.SelectedIndex == 0 && list.TopIndex == 0, "A live threshold crossing must repaint the title without changing selection or scroll.");
                    titleInvalidated = false; list.PreviewNow = now.AddSeconds(1); list.RefreshTimers(); list.Update();
                    Require(!titleInvalidated, "Timer ticks without color changes must keep the title's repaint area untouched.");
                    Capture(host, Path.Combine(outputDirectory, "live-color-transition.png")); host.Close();
                }
                File.WriteAllText(Path.Combine(outputDirectory, "color-results.txt"), "PASS: Four priority labels, red-only settings by default, linear transitions, Advanced display/save/reopen and return to simple settings, Cancel, fractional thresholds, immediate recoloring, selection/tasks/lifetime totals preserved, disabling keeps thresholds, reset defaults, both tabs, and live color updates without repeated title redraw or selection/scroll changes."); return 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(outputDirectory, "color-results.txt"), "FAIL: " + error); return 1; }
        }
    }
}
