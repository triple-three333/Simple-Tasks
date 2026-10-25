using System.Collections.Generic;
using System.Linq;

namespace SimpleTasks
{
    public sealed class CompletionTotals
    {
        public long Dated, Undated;
        public long Total { get { return Dated + Undated; } }
        public static CompletionTotals From(TaskDocument document)
        {
            var completed = document.Tasks.Where(t => !t.IsDaily && t.CompletedAt.HasValue).ToList();
            return new CompletionTotals { Dated = document.DeletedCompletedDated + completed.Count(t => t.DueAt.HasValue),
                Undated = document.DeletedCompletedUndated + completed.Count(t => !t.DueAt.HasValue) };
        }
        public override string ToString() { return "Completed: " + Total + " total  |  " + Dated + " dated  |  " + Undated + " undated"; }
    }

    public static class TaskOperations
    {
        public static TaskDocument Delete(TaskDocument document, IEnumerable<string> ids)
        {
            var selected = new HashSet<string>(ids); var next = document.Copy();
            // Keep only aggregate counts when completed task details are removed.
            foreach (var task in next.Tasks.Where(t => selected.Contains(t.Id) && !t.IsDaily && t.CompletedAt.HasValue))
                if (task.DueAt.HasValue) next.DeletedCompletedDated++;
                else next.DeletedCompletedUndated++;
            next.Tasks.RemoveAll(t => selected.Contains(t.Id)); return next;
        }
    }
}
