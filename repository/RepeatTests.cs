using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimpleTasks
{
    internal static class RepeatTests
    {
        private static TaskDocument Start(RepeatRule rule, bool dated, DateTime day, TimeZoneInfo zone)
        {
            var first = new TaskEntry { Id = Guid.NewGuid().ToString("N"), Title = "Doctor's visit", Note = "Bring up symptom x.\r\nAsk a follow-up question.", Weight = 3,
                CreatedAt = Recurrence.AtLocalTime(day.AddHours(9), zone), DueAt = dated ? (DateTimeOffset?)Recurrence.AtLocalTime(day.AddHours(17), zone) : null };
            var document = new TaskDocument(); document.Tasks.Add(first); Recurrence.Attach(document, first, rule, zone); return document;
        }

        public static void Run(Action<bool, string> check, string outputDirectory)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("US Mountain Standard Time");
            var monday = new DateTime(2026, 10, 12);
            var daily = Start(new RepeatRule { End = RepeatEnd.Count, TotalCount = 4 }, true, monday, zone);
            check(Recurrence.Generate(daily, Recurrence.AtLocalTime(monday.AddDays(10), zone), 1000) == 3 && daily.Tasks.Count == 4, "The total occurrence limit includes the original task.");
            check(daily.Tasks.Skip(1).All(t => t.DueAt.Value.Hour == 17 && t.CreatedAt.Hour == 0), "Dated repeats retain the due time and begin aging at midnight on their scheduled day.");
            check(daily.Tasks.All(t => t.Note == daily.Tasks[0].Note && t.Weight == 3), "Repeats carry forward multiline notes and weight.");
            check(daily.Tasks.Select(t => t.Id).Distinct().Count() == 4 && daily.Tasks.Select(t => t.OccurrenceNumber).SequenceEqual(new[] { 1, 2, 3, 4 }), "Every repeated task has its own identity and occurrence number.");
            daily.Tasks[1].CompletedAt = daily.Tasks[1].CreatedAt.AddHours(10);
            check(TaskTiming.ActiveTasks(daily, true).Count == 3 && Recurrence.Generate(daily, Recurrence.AtLocalTime(monday.AddDays(20), zone), 1000) == 0, "Completing an occurrence does not replace other tasks or extend the total limit.");
            daily.Tasks.RemoveAt(2);
            check(Recurrence.Generate(daily, Recurrence.AtLocalTime(monday.AddDays(30), zone), 1000) == 0, "Deleting an occurrence does not regenerate it.");

            var weekdays = Start(new RepeatRule { Pattern = RepeatPattern.Weekdays, WeekdayMask = (1 << 1) | (1 << 3) | (1 << 5), End = RepeatEnd.Count, TotalCount = 5 }, false, monday, zone);
            Recurrence.Generate(weekdays, Recurrence.AtLocalTime(monday.AddDays(30), zone), 1000);
            check(weekdays.Tasks.Select(t => TimeZoneInfo.ConvertTime(t.CreatedAt, zone).DayOfWeek).SequenceEqual(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Wednesday }), "Selected weekdays schedule the right calendar days.");
            check(weekdays.Tasks.All(t => !t.DueAt.HasValue) && TaskTiming.Age(weekdays.Tasks[1], weekdays.Tasks[1].CreatedAt.AddDays(2)) == "Age: 2d 0h 0m", "Undated repeats have age timers and no deadlines.");

            var weeks = Start(new RepeatRule { End = RepeatEnd.Weeks, Weeks = 2 }, false, monday, zone);
            Recurrence.Generate(weeks, Recurrence.AtLocalTime(monday.AddDays(30), zone), 1000);
            check(weeks.Tasks.Count == 14 && weeks.Tasks.Last().CreatedAt.Date == monday.AddDays(13), "A two-week duration covers exactly fourteen calendar days.");
            var weekly = Start(new RepeatRule { Pattern = RepeatPattern.Weekly, Interval = 2, End = RepeatEnd.Weeks, Weeks = 6 }, false, monday, zone);
            Recurrence.Generate(weekly, Recurrence.AtLocalTime(monday.AddDays(70), zone), 1000);
            check(weekly.Tasks.Count == 3 && weekly.Tasks.Last().CreatedAt.Date == monday.AddDays(28), "Intervals of multiple weeks respect the week-duration ending rule.");
            var until = Start(new RepeatRule { End = RepeatEnd.Date, UntilDate = Recurrence.DateText(monday.AddDays(2)) }, true, monday, zone);
            Recurrence.Generate(until, Recurrence.AtLocalTime(monday.AddDays(50), zone), 1000);
            check(until.Tasks.Count == 3 && until.Tasks.Last().CreatedAt.Date == monday.AddDays(2), "A repeat end date includes that day and stops afterward.");
            var once = Start(new RepeatRule { End = RepeatEnd.Count, TotalCount = 1 }, false, monday, zone);
            check(Recurrence.Generate(once, Recurrence.AtLocalTime(monday.AddYears(1), zone), 1000) == 0, "A total count of one creates only the original task.");

            var indefinite = Start(new RepeatRule(), false, monday, zone);
            check(Recurrence.Generate(indefinite, Recurrence.AtLocalTime(monday.AddDays(1).AddSeconds(-1), zone), 1000) == 0, "Future occurrences are not created early.");
            check(Recurrence.Generate(indefinite, Recurrence.AtLocalTime(monday.AddDays(1), zone), 1000) == 1, "An occurrence appears at its scheduled midnight boundary.");
            check(Recurrence.Generate(indefinite, Recurrence.AtLocalTime(monday.AddDays(100), zone), 7) == 7, "Large catch-up runs are generated in bounded batches.");
            var repository = new TaskRepository(Path.Combine(outputDirectory, "repeats.json")); repository.Save(indefinite);
            var reopened = repository.Load(); Recurrence.Generate(reopened, Recurrence.AtLocalTime(monday.AddDays(10), zone), 1000); repository.Save(reopened);
            var reopenedAgain = repository.Load();
            check(reopenedAgain.Tasks.Count == 11 && reopenedAgain.Tasks.Last().CreatedAt.Date == monday.AddDays(10), "Reopening catches up missed dates using intended creation dates.");
            check(Recurrence.Generate(reopenedAgain, Recurrence.AtLocalTime(monday.AddDays(10), zone), 1000) == 0, "Restarting again does not duplicate existing occurrences.");
            check(reopenedAgain.Tasks.Count(t => !t.CompletedAt.HasValue) == 11, "Catch-up retains all unfinished earlier occurrences.");
            var series = reopenedAgain.Series[0]; series.Enabled = false;
            check(Recurrence.Generate(reopenedAgain, Recurrence.AtLocalTime(monday.AddDays(30), zone), 1000) == 0 && reopenedAgain.Tasks.Count == 11, "Stopping a series preserves existing tasks and creates no more.");
            var updated = series.Template.Copy(); updated.Note = "Updated instructions"; updated.Title = "New title"; updated.Weight = 5;
            Recurrence.Update(series, updated, new RepeatRule { End = RepeatEnd.Count, TotalCount = 12 }, true);
            Recurrence.Generate(reopenedAgain, Recurrence.AtLocalTime(monday.AddDays(30), zone), 1000);
            check(reopenedAgain.Tasks.Last().Note == "Updated instructions" && reopenedAgain.Tasks.Last().Title == "New title" && reopenedAgain.Tasks.Last().Weight == 5 && reopenedAgain.Tasks[0].Note != "Updated instructions", "Editing a series updates future details and preserves earlier occurrences.");
            bool rejected = false;
            try { Recurrence.Update(series, updated, new RepeatRule { End = RepeatEnd.Count, TotalCount = 3 }, true); } catch (ArgumentException) { rejected = true; }
            check(rejected, "A total smaller than the number already created is rejected.");
            rejected = false; try { new RepeatRule { Pattern = RepeatPattern.Weekdays, WeekdayMask = 0 }.Validate(monday); } catch (ArgumentException) { rejected = true; }
            check(rejected, "A weekday schedule requires at least one day.");
            rejected = false; try { new RepeatRule { End = RepeatEnd.Date, UntilDate = Recurrence.DateText(monday.AddDays(-1)) }.Validate(monday); } catch (ArgumentException) { rejected = true; }
            check(rejected, "An ending date before the first occurrence is rejected.");

            var pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var dst = Start(new RepeatRule { End = RepeatEnd.Count, TotalCount = 3 }, true, new DateTime(2026, 10, 31), pacific);
            Recurrence.Generate(dst, Recurrence.AtLocalTime(new DateTime(2026, 11, 3), pacific), 1000);
            check(dst.Tasks.All(t => TimeZoneInfo.ConvertTime(t.DueAt.Value, pacific).Hour == 17) && dst.Tasks[0].DueAt.Value.Offset != dst.Tasks.Last().DueAt.Value.Offset, "Daylight-saving transitions preserve the chosen local due time.");
            check(Recurrence.AtLocalTime(new DateTime(2026, 3, 8, 2, 30, 0), pacific).Hour == 3, "A due time skipped by daylight saving moves to the next valid local time.");
            var moved = Start(new RepeatRule { End = RepeatEnd.Count, TotalCount = 2 }, true, monday, pacific);
            var originalDue = moved.Tasks[0].DueAt;
            Recurrence.Generate(moved, new DateTimeOffset(monday.AddDays(1).AddHours(18), TimeSpan.Zero), 100, TimeZoneInfo.Utc);
            check(moved.Tasks.Count == 2 && moved.Tasks[1].CreatedAt.Offset == TimeSpan.Zero && moved.Tasks[1].CreatedAt.Hour == 0
                && moved.Tasks[1].DueAt.Value.Hour == 17 && moved.Tasks[1].DueAt.Value.Offset == TimeSpan.Zero && moved.Tasks[0].DueAt == originalDue,
                "Future repeats follow the current system scheduling zone at the chosen local hour without moving existing deadlines.");
            var movedTemplate = moved.Series[0].Template.Copy(); movedTemplate.DueAt = new DateTimeOffset(monday.AddHours(19), TimeSpan.Zero);
            Recurrence.Update(moved.Series[0], movedTemplate, new RepeatRule { End = RepeatEnd.Count, TotalCount = 3 }, true, TimeZoneInfo.Utc);
            Recurrence.Generate(moved, new DateTimeOffset(monday.AddDays(2).AddHours(20), TimeSpan.Zero), 100, TimeZoneInfo.Utc);
            check(moved.Tasks.Last().DueAt.Value.Hour == 19 && moved.Tasks.Last().DueAt.Value.Offset == TimeSpan.Zero,
                "Editing a repeat after a time zone change retains the newly entered local due hour.");

            var legacyPath = Path.Combine(outputDirectory, "legacy.json");
            var legacy = new TaskDocument { Tasks = new List<TaskEntry> { new TaskEntry { Id = "legacy", Title = "Old task", CreatedAt = DateTimeOffset.Now, Weight = 1 } } };
            new TaskRepository(legacyPath).Save(legacy);
            var legacyText = File.ReadAllText(legacyPath); legacyText = legacyText.Replace("\"Series\":[],", "").Replace(",\"Series\":[]", ""); File.WriteAllText(legacyPath, legacyText);
            var loadedLegacy = new TaskRepository(legacyPath).Load();
            check(!legacyText.Contains("\"Series\"") && loadedLegacy.Series.Count == 0 && loadedLegacy.Tasks[0].Note == null, "Existing task files without notes or repeat settings remain readable.");
            var notes = Start(new RepeatRule { End = RepeatEnd.Count, TotalCount = 1 }, false, monday, zone);
            var notesRepository = new TaskRepository(Path.Combine(outputDirectory, "notes.json")); notesRepository.Save(notes);
            check(notesRepository.Load().Tasks[0].Note == notes.Tasks[0].Note, "Multiline notes survive saving and reopening.");
            notes.Tasks[0].Note = null; notesRepository.Save(notes);
            check(!notesRepository.Load().Tasks[0].HasNote, "Clearing a note persists its removal.");
        }
    }
}
