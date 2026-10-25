using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SimpleTasks
{
    public sealed class CalendarItem
    {
        public string Title, Note, Key;
        public DateTimeOffset Due;
        public bool Assignment;
        public int Sequence;
        public DateTimeOffset Modified;
        public override string ToString() { return Title; }
    }

    public sealed class CalendarFile
    {
        public string Name = "Calendar";
        public List<CalendarItem> Items = new List<CalendarItem>();
        public List<string> Warnings = new List<string>();
        public int EventCount;
    }

    public sealed class CalendarPlan
    {
        public List<CalendarItem> NewItems = new List<CalendarItem>();
        public int Matching, Existing;
    }

    public static class CalendarImport
    {
        private sealed class Property
        {
            public string Name, Value;
            public Dictionary<string, string> Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        private static readonly Dictionary<string, string> Zones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "America/Phoenix", "US Mountain Standard Time" }, { "America/Denver", "Mountain Standard Time" },
            { "America/Los_Angeles", "Pacific Standard Time" }, { "America/New_York", "Eastern Standard Time" },
            { "America/Chicago", "Central Standard Time" }, { "America/Anchorage", "Alaskan Standard Time" },
            { "Pacific/Honolulu", "Hawaiian Standard Time" }, { "Europe/London", "GMT Standard Time" },
            { "Europe/Paris", "Romance Standard Time" }, { "Europe/Berlin", "W. Europe Standard Time" },
            { "UTC", "UTC" }, { "Etc/UTC", "UTC" }, { "GMT", "UTC" }, { "Etc/GMT", "UTC" }
        };

        public static CalendarFile ReadFile(string path, TimeZoneInfo displayZone)
        {
            var info = new FileInfo(path);
            if (info.Length > 10 * 1024 * 1024) throw new InvalidDataException("Choose an iCalendar file smaller than 10 MB.");
            return Parse(File.ReadAllText(path, new UTF8Encoding(false, true)), displayZone);
        }

        private static List<string> SplitOutsideQuotes(string text, char delimiter)
        {
            var parts = new List<string>(); bool quoted = false; int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '"') quoted = !quoted;
                if (text[i] == delimiter && !quoted) { parts.Add(text.Substring(start, i - start)); start = i + 1; }
            }
            parts.Add(text.Substring(start)); return parts;
        }

        private static Property ReadProperty(string line)
        {
            bool quoted = false; int colon = -1;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') quoted = !quoted;
                if (line[i] == ':' && !quoted) { colon = i; break; }
            }
            if (colon <= 0) throw new FormatException("A calendar property is missing its name or value separator.");
            var parts = SplitOutsideQuotes(line.Substring(0, colon), ';');
            var property = new Property { Name = parts[0].ToUpperInvariant(), Value = line.Substring(colon + 1) };
            foreach (var parameter in parts.Skip(1))
            {
                int equals = parameter.IndexOf('=');
                if (equals > 0) property.Parameters[parameter.Substring(0, equals)] = parameter.Substring(equals + 1).Trim('"');
            }
            return property;
        }

        public static string DecodeText(string text)
        {
            var decoded = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\' && i + 1 < text.Length)
                {
                    char escaped = text[++i];
                    if (escaped == 'n' || escaped == 'N') decoded.Append(Environment.NewLine);
                    else if (escaped == '\\' || escaped == ',' || escaped == ';') decoded.Append(escaped);
                    else { decoded.Append('\\'); decoded.Append(escaped); }
                }
                else decoded.Append(text[i]);
            }
            return decoded.ToString();
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        private static bool DateOnly(Property property)
        {
            string type;
            return (property.Parameters.TryGetValue("VALUE", out type) && type.Equals("DATE", StringComparison.OrdinalIgnoreCase)) || Regex.IsMatch(property.Value, @"^\d{8}$");
        }

        private static DateTimeOffset ReadDate(Property property, TimeZoneInfo fallback)
        {
            var value = property.Value.Trim();
            if (DateOnly(property)) return Recurrence.AtLocalTime(DateTime.ParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture), fallback);
            bool utc = value.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
            if (utc) value = value.Substring(0, value.Length - 1);
            var date = DateTime.ParseExact(value, new[] { "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm" }, CultureInfo.InvariantCulture, DateTimeStyles.None);
            if (utc) return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc));
            string zoneName, windowsName; var zone = fallback;
            if (property.Parameters.TryGetValue("TZID", out zoneName))
            {
                if (Zones.TryGetValue(zoneName, out windowsName)) zoneName = windowsName;
                zone = TimeZoneInfo.FindSystemTimeZoneById(zoneName);
            }
            return Recurrence.AtLocalTime(date, zone);
        }

        private static string PlainHtml(string html)
        {
            var options = RegexOptions.IgnoreCase | RegexOptions.Singleline;
            var timeout = TimeSpan.FromSeconds(1);
            html = Regex.Replace(html, @"<(script|style)\b[^>]*>.*?</\1\s*>", "", options, timeout);
            html = Regex.Replace(html, @"<br\s*/?>|</(?:p|div|li)\s*>", Environment.NewLine, options, timeout);
            return WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]*>", "", options, timeout)).Trim();
        }

        private static Property Get(List<Property> properties, string name) { return properties.FirstOrDefault(p => p.Name == name); }
        private static string Text(List<Property> properties, string name) { var p = Get(properties, name); return p == null ? "" : DecodeText(p.Value); }

        private static void AddEvent(CalendarFile calendar, List<Property> properties, string component, TimeZoneInfo zone, bool cancelledCalendar)
        {
            calendar.EventCount++;
            string title = Text(properties, "SUMMARY").Trim(); if (title == "") title = "Untitled calendar entry";
            if (cancelledCalendar || Text(properties, "STATUS").Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) { calendar.Warnings.Add(title + ": cancelled entry skipped."); return; }
            if ((Get(properties, "RRULE") != null || Get(properties, "RDATE") != null) && Get(properties, "RECURRENCE-ID") == null)
            { calendar.Warnings.Add(title + ": recurring calendar rules are not expanded; export individual deadlines instead."); return; }
            var due = Get(properties, "DUE") ?? Get(properties, "DTSTART");
            if (due == null) { calendar.Warnings.Add(title + ": no deadline or start date; skipped."); return; }
            try
            {
                var deadline = ReadDate(due, zone);
                if (DateOnly(due))
                {
                    // DATE-valued VEVENT DTEND is exclusive. A task deadline
                    // belongs at the end of the last included calendar day.
                    var end = component == "VEVENT" && Get(properties, "DUE") == null ? Get(properties, "DTEND") : null;
                    if (end != null && DateOnly(end)) deadline = ReadDate(end, zone).AddSeconds(-1);
                    else deadline = Recurrence.AtLocalTime(TimeZoneInfo.ConvertTime(deadline, zone).Date.AddDays(1), zone).AddSeconds(-1);
                }
                string description = Text(properties, "DESCRIPTION");
                if (String.IsNullOrWhiteSpace(description)) description = PlainHtml(Text(properties, "X-ALT-DESC"));
                string url = Text(properties, "URL").Trim();
                if (url != "") description = (String.IsNullOrWhiteSpace(description) ? "" : description.Trim() + Environment.NewLine + Environment.NewLine) + "Link: " + url;
                string uid = Text(properties, "UID"); string recurrence = Text(properties, "RECURRENCE-ID");
                Uri uri; string host = Uri.TryCreate(url, UriKind.Absolute, out uri) ? uri.Host.ToLowerInvariant() : "";
                string identity = uid == "" ? "fallback|" + calendar.Name + "|" + title + "|" + deadline.ToUniversalTime().ToString("o") + "|" + url : host + "|" + uid + "|" + recurrence;
                if (uid == "") calendar.Warnings.Add(title + ": no calendar ID; duplicate detection uses its title and deadline.");
                int sequence; Int32.TryParse(Text(properties, "SEQUENCE"), out sequence);
                DateTimeOffset modified = DateTimeOffset.MinValue; var stamp = Get(properties, "LAST-MODIFIED") ?? Get(properties, "DTSTAMP");
                if (stamp != null) { try { modified = ReadDate(stamp, zone); } catch (Exception) { /* Optional modification metadata does not define a deadline. */ } }
                calendar.Items.Add(new CalendarItem { Title = title, Note = String.IsNullOrWhiteSpace(description) ? null : description, Due = deadline,
                    Key = "ical:" + Hash(identity), Assignment = uid.StartsWith("event-assignment-", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(url, @"(?:#assignment_|/assignments/)\d+", RegexOptions.IgnoreCase), Sequence = sequence, Modified = modified });
            }
            catch (Exception error)
            {
                if (!(error is FormatException || error is ArgumentException || error is TimeZoneNotFoundException || error is InvalidTimeZoneException || error is RegexMatchTimeoutException)) throw;
                calendar.Warnings.Add(title + ": " + error.Message + " Entry skipped.");
            }
        }

        public static CalendarFile Parse(string text, TimeZoneInfo zone)
        {
            var lines = new List<string>();
            using (var reader = new StringReader(text.TrimStart('\uFEFF')))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                    { if (lines.Count == 0) throw new InvalidDataException("The calendar begins with an invalid folded line."); lines[lines.Count - 1] += line.Substring(1); }
                    else if (line.Length > 0) lines.Add(line);
                }
            }
            var calendar = new CalendarFile(); var stack = new Stack<string>(); List<Property> entry = null; string component = null;
            bool began = false, ended = false, cancelled = false;
            foreach (var line in lines)
            {
                var property = ReadProperty(line);
                if (property.Name == "BEGIN")
                {
                    var name = property.Value.ToUpperInvariant();
                    if (name == "VCALENDAR") { if (began || stack.Count != 0) throw new InvalidDataException("Choose a single complete VCALENDAR file."); began = true; }
                    else if (!began || ended || stack.Count == 0) throw new InvalidDataException("A calendar component is outside VCALENDAR.");
                    if ((name == "VEVENT" || name == "VTODO") && stack.Count == 1) { entry = new List<Property>(); component = name; }
                    stack.Push(name); continue;
                }
                if (property.Name == "END")
                {
                    if (stack.Count == 0 || stack.Pop() != property.Value.ToUpperInvariant()) throw new InvalidDataException("The calendar has an incomplete or mismatched component.");
                    if (entry != null && property.Value.Equals(component, StringComparison.OrdinalIgnoreCase) && stack.Count == 1)
                    { AddEvent(calendar, entry, component, zone, cancelled); entry = null; component = null; }
                    if (property.Value.Equals("VCALENDAR", StringComparison.OrdinalIgnoreCase)) ended = true;
                    continue;
                }
                if (stack.Count == 1 && stack.Peek() == "VCALENDAR")
                {
                    if (property.Name == "X-WR-CALNAME") calendar.Name = DecodeText(property.Value);
                    if (property.Name == "METHOD" && property.Value.Equals("CANCEL", StringComparison.OrdinalIgnoreCase)) cancelled = true;
                }
                if (entry != null && stack.Count == 2 && stack.Peek() == component) entry.Add(property);
            }
            if (!began || !ended || stack.Count != 0) throw new InvalidDataException("This is not a complete iCalendar (.ics) file.");
            int before = calendar.Items.Count;
            calendar.Items = calendar.Items.GroupBy(i => i.Key).Select(g => g.OrderByDescending(i => i.Sequence).ThenByDescending(i => i.Modified).First()).OrderBy(i => i.Due).ToList();
            if (before > calendar.Items.Count) calendar.Warnings.Add((before - calendar.Items.Count) + " duplicate calendar revision(s) skipped.");
            return calendar;
        }

        public static CalendarPlan Plan(CalendarFile file, TaskDocument document, DateTime from, DateTime through, TimeZoneInfo zone, bool assignmentsOnly)
        {
            if (from.Date > through.Date) throw new ArgumentException("The end date must be on or after the start date.");
            var existing = new HashSet<string>(document.Tasks.Where(t => t.ImportKey != null).Select(t => t.ImportKey));
            var matching = file.Items.Where(i => (!assignmentsOnly || i.Assignment) && TimeZoneInfo.ConvertTime(i.Due, zone).Date >= from.Date && TimeZoneInfo.ConvertTime(i.Due, zone).Date <= through.Date).ToList();
            return new CalendarPlan { Matching = matching.Count, Existing = matching.Count(i => existing.Contains(i.Key)), NewItems = matching.Where(i => !existing.Contains(i.Key)).ToList() };
        }

        public static int Apply(TaskDocument document, IEnumerable<CalendarItem> items, string calendarName, DateTimeOffset now, int weight)
        {
            var existing = new HashSet<string>(document.Tasks.Where(t => t.ImportKey != null).Select(t => t.ImportKey)); int added = 0;
            foreach (var item in items)
            {
                if (!existing.Add(item.Key)) continue;
                var task = new TaskEntry { Id = Guid.NewGuid().ToString("N"), Title = item.Title, Note = item.Note, CreatedAt = now,
                    DueAt = item.Due, Weight = weight, ImportKey = item.Key, ImportCalendar = calendarName };
                task.Validate(now); document.Tasks.Add(task); added++;
            }
            return added;
        }
    }
}
