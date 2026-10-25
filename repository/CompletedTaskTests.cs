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
    internal static class CompletedTaskTests
    {
        private static TaskDocument Example()
        {
            var now = DateTimeOffset.Now; var document = new TaskDocument();
            for (int i = 0; i < 4; i++)
                document.Tasks.Add(new TaskEntry { Id = "completed-" + i, Title = (i % 2 == 0 ? "Dated" : "Undated") + " completed task " + (i + 1),
                    CreatedAt = now.AddDays(-5), DueAt = i % 2 == 0 ? (DateTimeOffset?)now.AddDays(-2) : null,
                    CompletedAt = now.AddHours(-i - 1), Weight = 1, Note = "A saved task note." });
            document.Tasks.Add(new TaskEntry { Id = "active", Title = "Keep this active task", CreatedAt = now.AddDays(-1), Weight = 1 });
            var daily = new TaskEntry { Id = "daily", Title = "Daily task", CreatedAt = now.AddDays(-1), Weight = 1, IsDaily = true };
            DailyTasks.Toggle(daily, now); document.Tasks.Add(daily); return document;
        }

        public static void Run(Action<bool, string> check, string outputDirectory)
        {
            var original = Example(); var repository = new TaskRepository(Path.Combine(outputDirectory, "completed-tallies.json"));
            repository.Save(original); var legacy = repository.Load();
            check(!File.ReadAllText(repository.FilePath).Contains("DeletedCompleted") && CompletionTotals.From(legacy).Total == 4,
                "Older saves without deleted-completion tallies keep their existing totals.");
            var deleted = TaskOperations.Delete(legacy, new[] { "completed-0", "completed-1", "completed-1", "active", "daily", "unknown" });
            var totals = CompletionTotals.From(deleted);
            check(deleted.Tasks.Count == 2 && totals.Dated == 2 && totals.Undated == 2 && totals.Total == 4,
                "Mixed deletion keeps dated and undated lifetime totals and excludes active and daily tasks.");
            check(legacy.Tasks.Count == 6 && legacy.DeletedCompletedDated == 0 && legacy.DeletedCompletedUndated == 0,
                "Deletion prepares an independent save without changing the current tasks or tallies.");
            check(deleted.DeletedCompletedDated == 1 && deleted.DeletedCompletedUndated == 1,
                "Repeated or missing selected IDs do not double-count deleted completions.");
            repository.Save(deleted); var reopened = repository.Load();
            check(reopened.Copy().DeletedCompletedDated == 1 && reopened.Copy().DeletedCompletedUndated == 1 && CompletionTotals.From(reopened).Total == 4,
                "Deleted completion tallies survive copying, saving, and reopening.");
            check(TaskOperations.Delete(reopened, new[] { "completed-0", "unknown" }).DeletedCompletedDated == 1,
                "Deleting an already removed task does not increment lifetime totals again.");
            var allDeleted = TaskOperations.Delete(reopened, reopened.Tasks.Select(t => t.Id)); repository.Save(allDeleted);
            check(repository.Load().Tasks.Count == 0 && CompletionTotals.From(repository.Load()).Total == 4,
                "Deleting the entire completed archive leaves lifetime counts saved without task details.");
            reopened.Tasks[0].CompletedAt = null;
            check(CompletionTotals.From(reopened).Total == 3 && CompletionTotals.From(reopened).Dated == 1,
                "Restoring an undeleted task still undoes only its completion while retaining deleted tallies.");
            reopened.Tasks[0].CompletedAt = DateTimeOffset.Now;
            check(CompletionTotals.From(reopened).Total == 4, "Completing a restored task counts it again without duplicating deleted totals.");
            reopened.Tasks[1].DueAt = DateTimeOffset.Now;
            check(CompletionTotals.From(reopened).Dated == 3 && CompletionTotals.From(reopened).Undated == 1,
                "Editing a saved completion's type retains the deleted category totals.");
            reopened.DeletedCompletedDated = -1; repository.Save(reopened); bool rejected = false;
            try { repository.Load(); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Invalid negative completion tallies are rejected without rewriting the save.");
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
        }
        private static void Require(bool success, string message) { if (!success) throw new Exception(message); }
        private static Button Button(Form form, string text) { return Descendants(form).OfType<Button>().Single(b => b.Text == text); }
        private static void Capture(Form form, string path)
        {
            Application.DoEvents();
            using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path, ImageFormat.Png); }
        }
        private static void Key(TaskList list, Keys key)
        {
            typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(list, new object[] { new KeyEventArgs(key) });
        }
        private static void Delete(Form archive, bool confirm, int count, bool keyboard, string outputDirectory)
        {
            Exception failure = null;
            using (var timer = new Timer { Interval = 100 })
            {
                timer.Tick += delegate
                {
                    timer.Stop(); var confirmation = Application.OpenForms.OfType<DeleteTasksDialog>().FirstOrDefault();
                    try
                    {
                        Require(confirmation != null && Descendants(confirmation).OfType<ListBox>().Single().Items.Count == count, "Confirmation must list exactly the selected completed tasks.");
                        Require(Descendants(confirmation).OfType<Label>().Any(l => l.Text.Contains("Lifetime completion totals will be kept")), "Confirmation must explain that lifetime counts are kept.");
                        Require(confirmation.AcceptButton != confirmation.DeleteButton, "Confirmation must default to Cancel.");
                        confirmation.Opacity = 0; Capture(confirmation, Path.Combine(outputDirectory, "completed-delete-confirmation.png"));
                        if (confirm) confirmation.DeleteButton.PerformClick(); else confirmation.DialogResult = DialogResult.Cancel;
                    }
                    catch (Exception error) { failure = error; if (confirmation != null) confirmation.Close(); else archive.Close(); }
                };
                timer.Start();
                if (keyboard) Key(Descendants(archive).OfType<TaskList>().Single(), Keys.Delete);
                else Descendants(archive).OfType<Button>().Single(b => b.Text.StartsWith("Delete")).PerformClick();
            }
            if (failure != null) throw failure;
        }
        private static void Archive(MainForm form, Action<Form, TaskList> action)
        {
            Exception failure = null;
            using (var timer = new Timer { Interval = 100 })
            {
                timer.Tick += delegate
                {
                    timer.Stop(); var archive = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "Completed tasks");
                    try { Require(archive != null, "Completed archive did not open."); archive.Opacity = 0; action(archive, Descendants(archive).OfType<TaskList>().Single()); }
                    catch (Exception error) { failure = error; }
                    finally { if (archive != null) archive.Close(); else form.Close(); }
                };
                timer.Start(); ((ToolStripMenuItem)((ToolStripMenuItem)form.MainMenuStrip.Items[1]).DropDownItems[0]).PerformClick();
            }
            if (failure != null) throw failure;
        }
        private static string Counter(MainForm form)
        {
            return Descendants(form).OfType<StatusStrip>().Single().Items.OfType<ToolStripStatusLabel>().Single(l => l.AccessibleName == "Completed task counters").Text;
        }
        public static int Preview(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            try
            {
                var document = Example(); var repository = new TaskRepository(Path.Combine(outputDirectory, "completed-ui.json")); repository.Save(document);
                using (var form = new MainForm(repository, document))
                {
                    form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); form.SetPreviewTime(DateTimeOffset.Now);
                    Archive(form, delegate(Form archive, TaskList list)
                    {
                        Require(!Button(archive, "Delete...").Enabled, "Delete must be disabled with no selection.");
                        list.SetSelected(0, true); list.SetSelected(1, true);
                        Require(!Button(archive, "Edit...").Enabled && !Button(archive, "Restore task").Enabled && Button(archive, "Delete (2)...").Enabled,
                            "Multiple selection must enable deletion and disable single-task actions.");
                        Capture(archive, Path.Combine(outputDirectory, "completed-multiselect-preview.png"));
                        string saved = File.ReadAllText(repository.FilePath); Delete(archive, false, 2, false, outputDirectory);
                        Require(File.ReadAllText(repository.FilePath) == saved && list.SelectedItems.Count == 2, "Cancel must preserve the save and selection.");
                        Delete(archive, true, 2, false, outputDirectory);
                        Require(list.Items.Count == 2 && repository.Load().Tasks.Count == 4 && Counter(form) == "Completed: 4 total  |  2 dated  |  2 undated",
                            "Deleting two completions must remove only their details and retain all displayed totals.");
                        list.SelectedIndex = 0; Button(archive, "Restore task").PerformClick();
                        Require(list.Items.Count == 1 && Counter(form) == "Completed: 3 total  |  1 dated  |  2 undated", "Restore must undo only the remaining saved completion.");
                        Key(list, Keys.Control | Keys.A); Require(list.SelectedItems.Count == 1, "Ctrl+A must select every remaining completion.");
                        Delete(archive, true, 1, true, outputDirectory);
                        Require(list.Items.Count == 0 && !Button(archive, "Delete...").Enabled && !Button(archive, "Restore task").Enabled,
                            "Deleting the final saved completion must leave an empty archive with disabled actions.");
                        Capture(archive, Path.Combine(outputDirectory, "completed-empty-preview.png"));
                    });
                    var dated = Descendants(form).OfType<TaskList>().Single(l => l.AccessibleName == "Dated tasks"); dated.SelectedIndex = 0;
                    Button(form, "&Complete").PerformClick();
                    Require(Counter(form) == "Completed: 4 total  |  2 dated  |  2 undated" && repository.Load().DeletedCompletedUndated == 2,
                        "Completing another task must retain tallies through the normal save path.");
                    form.Close();
                }
                using (var form = new MainForm(repository, repository.Load()))
                {
                    form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); form.SetPreviewTime(DateTimeOffset.Now);
                    Require(Counter(form) == "Completed: 4 total  |  2 dated  |  2 undated", "Reopening must preserve lifetime totals.");
                    Archive(form, delegate(Form archive, TaskList list)
                    {
                        Key(list, Keys.Control | Keys.A); Require(list.SelectedItems.Count == 1, "The completed task must reopen in the archive.");
                        Delete(archive, true, 1, true, outputDirectory);
                    });
                    Require(repository.Load().Tasks.Select(t => t.Id).OrderBy(id => id).SequenceEqual(new[] { "active", "daily" }), "Archive deletion must preserve active and daily tasks.");
                    var daily = Descendants(form).OfType<TaskList>().Single(l => l.AccessibleName == "Daily tasks"); daily.SelectedIndex = 0;
                    Button(form, "&Undo").PerformClick();
                    Require(Counter(form) == "Completed: 4 total  |  2 dated  |  2 undated" && CompletionTotals.From(repository.Load()).Total == 4,
                        "Daily toggling after archive deletion must neither lose nor increase lifetime totals.");
                    Capture(form, Path.Combine(outputDirectory, "lifetime-counters-preview.png")); form.Close();
                }
                File.WriteAllText(Path.Combine(outputDirectory, "completed-results.txt"), "PASS: Completed multi-selection, selection-dependent actions, Cancel preserving save and selection, one confirmation, single and multiple deletion, Delete key, Ctrl+A, empty state, lifetime counters across deletion/reopening/completion/daily toggling, Restore, and active/daily task preservation."); return 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(outputDirectory, "completed-results.txt"), "FAIL: " + error); return 1; }
        }
    }
}
