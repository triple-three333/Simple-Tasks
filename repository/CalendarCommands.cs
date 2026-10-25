using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SimpleTasks
{
    internal static class CalendarCommands
    {
        public static int Import(string[] args, TaskRepository repository)
        {
            string reportPath = Path.GetFullPath(args[4]);
            try
            {
                var from = DateTime.ParseExact(args[2], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var through = DateTime.ParseExact(args[3], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var calendar = CalendarImport.ReadFile(args[1], TimeZoneInfo.Local); var document = repository.Load();
                var plan = CalendarImport.Plan(calendar, document, from, through, TimeZoneInfo.Local, true);
                var next = document.Copy(); int added = CalendarImport.Apply(next, plan.NewItems, calendar.Name, DateTimeOffset.Now, 1);
                var report = new StringBuilder("# Canvas calendar import\n\n");
                report.AppendLine("Due date range: **" + from.ToString("MMMM d, yyyy") + " through " + through.ToString("MMMM d, yyyy") + "**, inclusive.");
                report.AppendLine("Time zone: " + TimeZoneInfo.Local.StandardName + ".\n");
                report.AppendLine("Calendar events: " + calendar.EventCount + ". Matching assignments: " + plan.Matching + ". New tasks: " + added + ". Already imported: " + plan.Existing + ".\n");
                report.AppendLine("Descriptions and calendar links are saved as notes. New tasks use weight 1 and start aging at import time. Existing tasks are preserved.\n");
                report.AppendLine("| Assignment | Due (local time) |\n| --- | --- |");
                foreach (var item in plan.NewItems) report.AppendLine("| " + item.Title.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ") + " | " + TimeZoneInfo.ConvertTime(item.Due, TimeZoneInfo.Local).ToString("MMM d, yyyy h:mm tt") + " |");
                if (calendar.Warnings.Count > 0) { report.AppendLine("\nSkipped entries / warnings:\n"); foreach (var warning in calendar.Warnings) report.AppendLine("- " + warning.Replace("\n", " ")); }
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                if (added > 0)
                {
                    // Keep a dated, untouched pre-import copy as well as the normal one-save backup.
                    if (File.Exists(repository.FilePath)) File.Copy(repository.FilePath, repository.FilePath + ".before-calendar-import-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".bak");
                    repository.Save(next);
                }
                File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
                return 0;
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, "Calendar import failed: " + error.Message); return 1;
            }
        }
    }
}
