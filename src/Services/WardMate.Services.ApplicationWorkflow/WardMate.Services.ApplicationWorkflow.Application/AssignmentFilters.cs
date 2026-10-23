namespace WardMate.Services.ApplicationWorkflow.Application;

public static class AssignmentFilters
{
    public static bool Valid(string? categories, string? scope, string? assignee, string? sort, bool manager) =>
        (categories is null || categories.Length <= 1000 && categories.Split(',').Length <= 100 && categories.Split(',').All(x => int.TryParse(x, out var id) && id > 0))
        && scope is null or "my-categories"
        && (assignee is null or "me" or "unassigned" || manager && Guid.TryParse(assignee, out var id) && id != Guid.Empty)
        && sort is null or "submittedAt" or "-submittedAt" or "dueAt";
}
