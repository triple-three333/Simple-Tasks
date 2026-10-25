using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimpleTasks
{
    internal static class DailyTests
    {
        public static void Run(Action<bool, string> check, string outputDirectory)
        {
            var now = Recurrence.AtLocalTime(new DateTime(2026, 10, 14, 23, 59, 59), TimeZoneInfo.Local);
            var task = new TaskEntry { Id = "daily", Title = "Read ten pages", Note = "Choose a book", IsDaily = true, CreatedAt = now.AddDays(-10), Weight = 3 };
            task.Validate(now);
            var document = new TaskDocument { Tasks = new List<TaskEntry> { task, new TaskEntry { Id = "ordinary", Title = "Ordinary task", CreatedAt = now, Weight = 1 } } };
            check(!DailyTasks.IsDone(task, now) && TaskTiming.Age(task, now) == "" && TaskTiming.Deadline(task, now) == "", "Daily tasks have neither age nor deadline timers.");
            DailyTasks.Toggle(task, now);
            check(DailyTasks.IsDone(task, now) && task.CompletedAt == null && DailyTasks.List(document).Count == 1, "Completing a daily task keeps it in its own column rather than archiving it.");
            check(DailyTasks.IsDone(task, now.ToUniversalTime()) && DailyTasks.Today(now) == "2026-10-14", "Daily completion follows the system's local date regardless of the input UTC offset.");
            check(!DailyTasks.IsDone(task, now.AddSeconds(1)), "Daily completion resets exactly at the system's local midnight.");
            check(!DailyTasks.IsDone(task, now.AddDays(7)) && document.Tasks.Count == 2, "Reopening after missed days resets daily status without creating duplicate tasks.");
            check(TaskTiming.ActiveTasks(document, false).Count == 1 && TaskTiming.ActiveTasks(document, true).Count == 0, "Daily tasks are excluded from the dated and undated columns.");
            check(task.Note == "Choose a book" && task.Weight == 3 && task.CreatedAt == now.AddDays(-10), "Daily completion preserves notes, weight, and stored creation data.");
            var repository = new TaskRepository(Path.Combine(outputDirectory, "daily-roundtrip.json")); repository.Save(document); var loaded = repository.Load();
            check(loaded.Tasks[0].IsDaily && DailyTasks.IsDone(loaded.Tasks[0], now) && !DailyTasks.IsDone(loaded.Tasks[0], now.AddSeconds(1)), "Saved daily tasks reopen with completion retained for the right calendar day.");
            DailyTasks.Toggle(task, now);
            check(!DailyTasks.IsDone(task, now) && task.DailyCompletedOn == null, "Daily completion can be undone.");
            DailyTasks.Toggle(task, now.AddDays(1));
            check(DailyTasks.IsDone(task, now.AddDays(1)) && task.DailyCompletedOn == "2026-10-15", "A daily task can be completed again on a subsequent day.");
            var other = task.Copy(); other.Id = "daily-newer"; other.CreatedAt = now;
            document.Tasks.Add(other);
            check(DailyTasks.List(document).Select(t => t.Id).SequenceEqual(new[] { "daily", "daily-newer" }), "Daily tasks keep their positions regardless of completion state.");
            check(!loaded.Tasks[1].IsDaily && TaskTiming.ActiveTasks(loaded, false).Count == 1, "Existing ordinary tasks keep their type after loading the extended save format.");
            bool rejected = false; var invalid = task.Copy(); invalid.DueAt = now;
            try { invalid.Validate(now); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Daily tasks cannot be given a due date or time.");
            rejected = false;
            try { Recurrence.Attach(document, task, new RepeatRule(), TimeZoneInfo.Local); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Daily tasks cannot also create separate repeating occurrences.");
            bool resets = true;
            foreach (string zoneId in new[] { "UTC", "Eastern Standard Time", "Pacific Standard Time", "Tokyo Standard Time" })
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
                var beforeMidnight = Recurrence.AtLocalTime(new DateTime(2026, 10, 14, 23, 59, 59), zone);
                var sample = task.Copy(); sample.DailyCompletedOn = null; DailyTasks.Toggle(sample, beforeMidnight, zone);
                resets &= DailyTasks.IsDone(sample, beforeMidnight.ToUniversalTime(), zone) && !DailyTasks.IsDone(sample, beforeMidnight.AddSeconds(1), zone);
            }
            check(resets, "Daily completion resets at midnight in UTC, Eastern, Pacific, and Tokyo time zones.");
            var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var fallBack = new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-4));
            var dstTask = task.Copy(); dstTask.DailyCompletedOn = null; DailyTasks.Toggle(dstTask, fallBack, eastern);
            check(DailyTasks.IsDone(dstTask, fallBack.AddHours(1), eastern), "The repeated hour at the end of daylight saving does not reset daily completion.");
            var springForward = new DateTimeOffset(2026, 3, 8, 1, 59, 59, TimeSpan.FromHours(-5));
            dstTask.DailyCompletedOn = null; DailyTasks.Toggle(dstTask, springForward, eastern);
            check(DailyTasks.IsDone(dstTask, springForward.AddSeconds(1), eastern), "The skipped hour at the start of daylight saving does not reset daily completion.");
        }
    }
}
