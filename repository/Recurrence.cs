using System;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace SimpleTasks
{
    [DataContract]
    public enum RepeatPattern { [EnumMember] Daily, [EnumMember] Weekly, [EnumMember] Weekdays }
    [DataContract]
    public enum RepeatEnd { [EnumMember] Never, [EnumMember] Count, [EnumMember] Weeks, [EnumMember] Date }

    [DataContract]
    public sealed class RepeatRule
    {
        [DataMember] public RepeatPattern Pattern { get; set; }
        [DataMember] public int Interval { get; set; }
        [DataMember] public int WeekdayMask { get; set; }
        [DataMember] public RepeatEnd End { get; set; }
        [DataMember] public int TotalCount { get; set; }
        [DataMember] public int Weeks { get; set; }
        [DataMember] public string UntilDate { get; set; }
        public RepeatRule() { Interval = 1; TotalCount = 10; Weeks = 4; WeekdayMask = 62; UntilDate = Recurrence.DateText(DateTime.Today.AddMonths(1)); }
        public RepeatRule Copy() { return (RepeatRule)MemberwiseClone(); }

        public void Validate(DateTime anchor)
        {
            if (!Enum.IsDefined(typeof(RepeatPattern), Pattern) || !Enum.IsDefined(typeof(RepeatEnd), End)) throw new ArgumentException("Choose a supported repeat schedule.");
            if (Interval < 1 || Interval > 365) throw new ArgumentException("The repeat interval must be between 1 and 365.");
            if (Pattern == RepeatPattern.Weekdays && (WeekdayMask < 1 || WeekdayMask > 127)) throw new ArgumentException("Select at least one day of the week.");
            if (End == RepeatEnd.Count && (TotalCount < 1 || TotalCount > 1000000)) throw new ArgumentException("Enter a total occurrence count between 1 and 1,000,000.");
            if (End == RepeatEnd.Weeks && (Weeks < 1 || Weeks > 5200)) throw new ArgumentException("Enter a duration between 1 and 5,200 weeks.");
            if (End == RepeatEnd.Date && Recurrence.ParseDate(UntilDate) < anchor.Date) throw new ArgumentException("The end date cannot be before the first occurrence.");
        }

        public string Summary()
        {
            string schedule;
            if (Pattern == RepeatPattern.Weekdays)
                schedule = String.Join(", ", Enumerable.Range(0, 7).Where(i => (WeekdayMask & (1 << i)) != 0).Select(i => CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[i]));
            else schedule = "Every " + Interval + (Pattern == RepeatPattern.Daily ? " day(s)" : " week(s)");
            if (End == RepeatEnd.Count) return schedule + " / " + TotalCount + " total occurrences";
            if (End == RepeatEnd.Weeks) return schedule + " / " + Weeks + " week(s)";
            if (End == RepeatEnd.Date) return schedule + " / through " + Recurrence.ParseDate(UntilDate).ToString("MMM d, yyyy");
            return schedule + " / indefinitely";
        }
    }

    [DataContract]
    public sealed class TaskSeries
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public TaskEntry Template { get; set; }
        [DataMember] public RepeatRule Rule { get; set; }
        [DataMember] public string AnchorDate { get; set; }
        [DataMember] public string LastDate { get; set; }
        [DataMember] public string NextDate { get; set; }
        [DataMember] public int GeneratedCount { get; set; }
        [DataMember] public string TimeZoneId { get; set; }
        [DataMember] public bool Enabled { get; set; }
        public TaskSeries Copy()
        {
            var copy = (TaskSeries)MemberwiseClone(); copy.Template = Template.Copy(); copy.Rule = Rule.Copy(); return copy;
        }
        public override string ToString()
        {
            string state = !Enabled ? "Stopped" : Recurrence.CanGenerate(this) ? "Next: " + Recurrence.ParseDate(NextDate).ToString("MMM d, yyyy") : "Finished";
            return Template.Title + " — " + Rule.Summary() + " — " + state + " (" + GeneratedCount + " created)";
        }
    }

    public static class Recurrence
    {
        public static string DateText(DateTime date) { return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static DateTime ParseDate(string text) { return DateTime.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None); }

        public static DateTime NextDay(RepeatRule rule, DateTime after)
        {
            if (rule.Pattern == RepeatPattern.Daily) return after.AddDays(rule.Interval);
            if (rule.Pattern == RepeatPattern.Weekly) return after.AddDays(rule.Interval * 7);
            for (int days = 1; days <= 7; days++)
            {
                var next = after.AddDays(days);
                if ((rule.WeekdayMask & (1 << (int)next.DayOfWeek)) != 0) return next;
            }
            throw new ArgumentException("Select at least one day of the week.");
        }

        public static TimeZoneInfo Zone(TaskSeries series) { return TimeZoneInfo.FindSystemTimeZoneById(series.TimeZoneId); }

        // Calendar days use the series' time zone, so a daylight-saving change
        // does not shift a chosen due time or day of the week.
        public static DateTimeOffset AtLocalTime(DateTime date, TimeZoneInfo zone)
        {
            date = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
            while (zone.IsInvalidTime(date)) date = date.AddMinutes(1);
            var offset = zone.IsAmbiguousTime(date) ? zone.GetAmbiguousTimeOffsets(date).Max() : zone.GetUtcOffset(date);
            return new DateTimeOffset(date, offset);
        }

        public static bool CanGenerate(TaskSeries series)
        {
            if (!series.Enabled) return false;
            var next = ParseDate(series.NextDate);
            if (series.Rule.End == RepeatEnd.Count) return series.GeneratedCount < series.Rule.TotalCount;
            if (series.Rule.End == RepeatEnd.Weeks) return (next - ParseDate(series.AnchorDate)).TotalDays < series.Rule.Weeks * 7;
            if (series.Rule.End == RepeatEnd.Date) return next <= ParseDate(series.Rule.UntilDate);
            return true;
        }

        public static TaskSeries Attach(TaskDocument document, TaskEntry first, RepeatRule rule, TimeZoneInfo zone)
        {
            if (first.IsDaily) throw new ArgumentException("Daily tasks already reset each day and do not need a repeat schedule.");
            var anchor = TimeZoneInfo.ConvertTime(first.DueAt ?? first.CreatedAt, zone).Date;
            rule.Validate(anchor);
            var series = new TaskSeries {
                Id = Guid.NewGuid().ToString("N"), Template = first.Copy(), Rule = rule.Copy(), AnchorDate = DateText(anchor),
                LastDate = DateText(anchor), NextDate = DateText(NextDay(rule, anchor)), GeneratedCount = 1, TimeZoneId = zone.Id, Enabled = true
            };
            first.SeriesId = series.Id; first.OccurrenceNumber = 1;
            document.Series.Add(series); return series;
        }

        public static void Update(TaskSeries series, TaskEntry template, RepeatRule rule, bool enabled, TimeZoneInfo localZone = null)
        {
            if (enabled && template.IsDaily) throw new ArgumentException("Daily tasks already reset each day and do not need a repeat schedule.");
            rule.Validate(ParseDate(series.AnchorDate));
            if (enabled && rule.End == RepeatEnd.Count && rule.TotalCount < series.GeneratedCount)
                throw new ArgumentException("This series already has " + series.GeneratedCount + " occurrences. Choose that many or more, or turn Repeat off.");
            series.Template = template.Copy(); series.Rule = rule.Copy(); series.Enabled = enabled;
            if (localZone != null) series.TimeZoneId = localZone.Id;
            series.NextDate = DateText(NextDay(rule, ParseDate(series.LastDate)));
        }

        public static int Generate(TaskDocument document, DateTimeOffset now, int maximum, TimeZoneInfo localZone = null)
        {
            int added = 0;
            foreach (var series in document.Series)
            {
                if (!CanGenerate(series)) continue;
                var originalZone = Zone(series);
                var zone = localZone ?? originalZone;
                // Keep the chosen wall-clock hour when scheduling in the
                // current system zone, without moving already-created tasks.
                var dueTime = series.Template.DueAt.HasValue ? TimeZoneInfo.ConvertTime(series.Template.DueAt.Value, originalZone).TimeOfDay : TimeSpan.Zero;
                while (CanGenerate(series) && added < maximum)
                {
                    var date = ParseDate(series.NextDate);
                    var created = AtLocalTime(date, zone);
                    if (created > now) break;
                    var task = series.Template.Copy();
                    task.Id = series.Id + "-" + (series.GeneratedCount + 1).ToString(CultureInfo.InvariantCulture);
                    task.SeriesId = series.Id; task.OccurrenceNumber = series.GeneratedCount + 1;
                    task.CreatedAt = created; task.CompletedAt = null;
                    task.DueAt = series.Template.DueAt.HasValue ? (DateTimeOffset?)AtLocalTime(date.Add(dueTime), zone) : null;
                    document.Tasks.Add(task); series.GeneratedCount++; series.LastDate = series.NextDate;
                    series.NextDate = DateText(NextDay(series.Rule, date)); added++;
                }
                if (added >= maximum) break;
            }
            return added;
        }
    }
}
