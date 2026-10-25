using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SimpleTasks
{
    internal static class CalendarTests
    {
        private static string Event(string uid, string date, string extra)
        {
            return "BEGIN:VEVENT\r\nUID:" + uid + "\r\nSUMMARY:Assignment\r\nDTSTART" + date + "\r\n" + extra + "\r\nEND:VEVENT\r\n";
        }
        private static string Calendar(string entries) { return "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nX-WR-CALNAME:Test calendar\r\n" + entries + "END:VCALENDAR\r\n"; }

        public static void Run(Action<bool, string> check, string outputDirectory)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("US Mountain Standard Time");
            var from = new DateTime(2026, 8, 20); var through = new DateTime(2026, 12, 15);
            var boundary = CalendarImport.Parse(Calendar(
                Event("event-assignment-before", ":20260820T065959Z", "") + Event("event-assignment-start", ":20260820T070000Z", "") +
                Event("event-assignment-end", ":20261216T065959Z", "") + Event("event-assignment-after", ":20261216T070000Z", "")), zone);
            var plan = CalendarImport.Plan(boundary, new TaskDocument(), from, through, zone, true);
            check(plan.NewItems.Count == 2 && TimeZoneInfo.ConvertTime(plan.NewItems[0].Due, zone).Date == from && TimeZoneInfo.ConvertTime(plan.NewItems[1].Due, zone).Date == through, "Calendar date filtering includes both boundary days in the selected time zone.");
            bool rejected = false; try { CalendarImport.Plan(boundary, new TaskDocument(), through, from, zone, true); } catch (ArgumentException) { rejected = true; }
            check(rejected, "An inverted calendar import range is rejected.");
            var folded = CalendarImport.Parse(Calendar("BEGIN:VEVENT\r\nUID:event-assignment-folded\r\nSUMMARY:Doctor's visit\\, with\r\n  follow-up\r\nDTSTART:20261015T065900Z\r\nDESCRIPTION:Bring up symptom x.\\nSecond line\\; path C:\\\\notes\r\nURL;VALUE=URI:https://example.edu/calendar?course=1&\r\n month=10#assignment_5\r\nBEGIN:VALARM\r\nSUMMARY:Ignore nested alarm title\r\nDESCRIPTION:Ignore nested alarm note\r\nEND:VALARM\r\nEND:VEVENT\r\n"), zone);
            check(folded.Items[0].Title == "Doctor's visit, with follow-up" && folded.Items[0].Note.Contains("Second line; path C:\\notes"), "Folded lines and escaped calendar text preserve titles and multiline notes.");
            check(folded.Items[0].Note.Contains("course=1&month=10#assignment_5") && !folded.Items[0].Note.Contains("Ignore nested"), "Folded URLs are restored and nested alarm properties do not override assignments.");
            var html = CalendarImport.Parse(Calendar(Event("event-assignment-html", ":20261015T170000Z", "X-ALT-DESC;FMTTYPE=text/html:<p>Bring &amp; discuss</p><script>untrusted instructions</script>")), zone);
            check(html.Items[0].Note == "Bring & discuss" && !html.Items[0].Note.Contains("script"), "HTML-only descriptions become plain text without scripts.");
            var dates = CalendarImport.Parse(Calendar(
                Event("event-assignment-date", ";VALUE=DATE:20261215", "DTEND;VALUE=DATE:20261216") +
                Event("event-assignment-single-day", ";VALUE=DATE:20261001", "") +
                Event("event-assignment-zoned", ";TZID=America/Phoenix:20261020T235900", "") +
                Event("event-assignment-floating", ":20261021T235900", "") +
                "BEGIN:VTODO\r\nUID:todo-1\r\nSUMMARY:To-do deadline\r\nDUE:20261022T200000Z\r\nEND:VTODO\r\n"), zone);
            check(dates.Items.First(i => i.Due.Month == 12).Due == new DateTimeOffset(2026, 12, 15, 23, 59, 59, TimeSpan.FromHours(-7)), "All-day calendar end dates are interpreted as exclusive.");
            check(dates.Items.Any(i => i.Due.Day == 1 && i.Due.Hour == 23 && i.Due.Minute == 59), "Single-day events receive an end-of-day deadline.");
            check(dates.Items.Any(i => i.Due.Day == 20 && i.Due.Hour == 23 && i.Due.Offset == TimeSpan.FromHours(-7)) && dates.Items.Any(i => i.Due.Day == 21 && i.Due.Hour == 23), "Named and floating local time values produce the intended due times.");
            check(dates.Items.Any(i => i.Title == "To-do deadline" && i.Due.UtcDateTime.Hour == 20), "VTODO components use their DUE property.");
            var skipped = CalendarImport.Parse(Calendar(
                Event("event-assignment-cancelled", ":20261015T170000Z", "STATUS:CANCELLED") +
                Event("event-assignment-recurring", ":20261015T170000Z", "RRULE:FREQ=DAILY") +
                Event("event-assignment-bad-zone", ";TZID=Unknown/Zone:20261015T170000", "") +
                Event("event-assignment-bad-date", ":not-a-date", "")), zone);
            check(skipped.Items.Count == 0 && skipped.Warnings.Count == 4, "Cancelled, unsupported recurring, invalid-date, and unknown-zone entries are reported instead of guessed.");
            var revisions = CalendarImport.Parse(Calendar(
                Event("event-assignment-revised", ":20261101T170000Z", "SEQUENCE:1") +
                Event("event-assignment-revised", ":20261102T170000Z", "SEQUENCE:2")), zone);
            check(revisions.Items.Count == 1 && revisions.Items[0].Due.Day == 2, "Duplicate UID revisions select the latest sequence.");
            var mixed = CalendarImport.Parse(Calendar(Event("event-assignment-one", ":20261015T170000Z", "") + Event("meeting-one", ":20261016T170000Z", "")), zone);
            check(CalendarImport.Plan(mixed, new TaskDocument(), from, through, zone, true).NewItems.Count == 1 && CalendarImport.Plan(mixed, new TaskDocument(), from, through, zone, false).NewItems.Count == 2, "The Canvas assignment filter can include or exclude other calendar events.");
            var now = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(-7));
            var document = new TaskDocument();
            int added = CalendarImport.Apply(document, folded.Items, folded.Name, now, 4);
            check(added == 1 && document.Tasks[0].CreatedAt == now && document.Tasks[0].Weight == 4 && document.Tasks[0].HasNote, "Imported tasks use the chosen weight, preserve notes, and begin aging at import time.");
            var repository = new TaskRepository(Path.Combine(outputDirectory, "calendar-roundtrip.json")); repository.Save(document); var loaded = repository.Load();
            loaded.Tasks[0].CompletedAt = now; loaded.Tasks[0].Note = "My edited note"; loaded.Tasks[0].Weight = 5;
            check(CalendarImport.Plan(folded, loaded, from, through, zone, true).Existing == 1 && CalendarImport.Apply(loaded, folded.Items, folded.Name, now, 1) == 0 && loaded.Tasks[0].Note == "My edited note" && loaded.Tasks[0].Weight == 5 && loaded.Tasks[0].CompletedAt == now, "Re-import preserves completed tasks and manual edits while avoiding duplicates across saves.");
            check(loaded.Tasks[0].DueAt == folded.Items[0].Due && loaded.Tasks[0].ImportKey == folded.Items[0].Key, "Calendar deadlines and source identities survive saving and reopening.");
            rejected = false; try { CalendarImport.Parse("BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\n", zone); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Truncated calendar files are rejected without importing partial data.");
            rejected = false; try { CalendarImport.Parse("not a calendar", zone); } catch (FormatException) { rejected = true; }
            check(rejected, "Non-calendar files are rejected.");
            var noId = CalendarImport.Parse(Calendar(Event("", ":20261015T170000Z", "")), zone);
            check(noId.Items.Count == 1 && noId.Warnings.Count == 1 && CalendarImport.Apply(document, noId.Items.Concat(noId.Items), noId.Name, now, 1) == 1, "Events without a UID have deterministic fallback duplicate detection and a warning.");
        }

        public static int CheckFile(string[] args)
        {
            string output = Path.GetFullPath(args[4]); Directory.CreateDirectory(output);
            try
            {
                var from = Recurrence.ParseDate(args[2]); var through = Recurrence.ParseDate(args[3]);
                var file = CalendarImport.ReadFile(args[1], TimeZoneInfo.Local); var original = new TaskDocument();
                var plan = CalendarImport.Plan(file, original, from, through, TimeZoneInfo.Local, true);
                if (plan.NewItems.Count == 0) throw new Exception("No assignments match the requested date range.");
                using (var dialog = new CalendarImportDialog(file, original, from, through))
                {
                    dialog.ShowInTaskbar = false; dialog.Opacity = 0; dialog.Show(); Application.DoEvents();
                    if (dialog.Entries.CheckedItems.Count != plan.NewItems.Count) throw new Exception("The import preview does not match the range-filtered assignments.");
                    using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size)); bitmap.Save(Path.Combine(output, "calendar-import-preview.png"), ImageFormat.Png); }
                    dialog.ThroughDate.Value = from.AddDays(-1);
                    if (dialog.Import.Enabled) throw new Exception("An invalid range still enables import.");
                    dialog.ThroughDate.Value = through; dialog.Import.PerformClick();
                    if (dialog.SelectedItems == null || dialog.SelectedItems.Count != plan.NewItems.Count) throw new Exception("The importer did not return the checked entries.");
                    dialog.Close();
                }
                var testDirectory = Path.Combine(output, "calendar-check-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(testDirectory);
                var repository = new TaskRepository(Path.Combine(testDirectory, "tasks.json"));
                var importArgs = new[] { "--import-ics", args[1], args[2], args[3], Path.Combine(testDirectory, "report.md") };
                if (CalendarCommands.Import(importArgs, repository) != 0) throw new Exception(File.ReadAllText(importArgs[4]));
                var imported = repository.Load();
                if (imported.Tasks.Count != plan.NewItems.Count || imported.Tasks.Any(t => !t.DueAt.HasValue)) throw new Exception("Saving the requested import did not preserve all selected tasks and due dates.");
                if (CalendarCommands.Import(importArgs, repository) != 0 || repository.Load().Tasks.Count != imported.Tasks.Count) throw new Exception("Re-importing the same file created duplicates.");
                File.WriteAllText(Path.Combine(output, "actual-calendar-results.txt"), "PASS: " + file.EventCount + " calendar events; " + plan.NewItems.Count + " assignments in range; " + file.Warnings.Count + " warnings. Preview, inclusive range validation, import, saved deadlines and notes, and duplicate-free re-import verified using test data only.\nEarliest due: " + TimeZoneInfo.ConvertTime(plan.NewItems.Min(i => i.Due), TimeZoneInfo.Local).ToString("f") + "\nLatest due: " + TimeZoneInfo.ConvertTime(plan.NewItems.Max(i => i.Due), TimeZoneInfo.Local).ToString("f"));
                return 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(output, "actual-calendar-results.txt"), "FAIL: " + error); return 1; }
        }
    }
}
