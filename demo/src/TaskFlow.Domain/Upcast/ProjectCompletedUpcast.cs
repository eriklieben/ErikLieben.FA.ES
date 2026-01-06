using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Upcasting;
using TaskFlow.Domain.Events.Project;

namespace TaskFlow.Domain.Upcast;

/// <summary>
/// Upcasts legacy ProjectCompleted events to specific outcome events based on the outcome text.
/// This demonstrates event schema evolution and backwards compatibility.
/// </summary>
public class ProjectCompletedUpcast : IUpcastEvent
{
    public bool CanUpcast(IEvent @event)
    {
        return @event.EventType == "Project.Completed";
    }

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var legacyEvent = JsonEvent.ToEvent(@event,
            ProjectCompletedJsonSerializerContext.Default.ProjectCompleted);
        var eventData = legacyEvent.Data();
        var outcome = eventData.Outcome?.ToLowerInvariant() ?? string.Empty;
        var newEvent = outcome switch
        {
            _ when outcome.Contains("success") || outcome.Contains("complete") =>
                CreateEvent("Project.CompletedSuccessfully",
                    new ProjectCompletedSuccessfully(eventData.Outcome ?? string.Empty, eventData.CompletedBy, eventData.CompletedAt),
                    @event),

            _ when outcome.Contains("cancel") =>
                CreateEvent("Project.Cancelled",
                    new ProjectCancelled(eventData.Outcome ?? string.Empty, eventData.CompletedBy, eventData.CompletedAt),
                    @event),

            _ when outcome.Contains("fail") =>
                CreateEvent("Project.Failed",
                    new ProjectFailed(eventData.Outcome ?? string.Empty, eventData.CompletedBy, eventData.CompletedAt),
                    @event),

            _ when outcome.Contains("deliver") || outcome.Contains("ship") || outcome.Contains("deploy") =>
                CreateEvent("Project.Delivered",
                    new ProjectDelivered(eventData.Outcome ?? string.Empty, eventData.CompletedBy, eventData.CompletedAt),
                    @event),

            _ when outcome.Contains("suspend") || outcome.Contains("hold") || outcome.Contains("pause") =>
                CreateEvent("Project.Suspended",
                    new ProjectSuspended(eventData.Outcome ?? string.Empty, eventData.CompletedBy, eventData.CompletedAt),
                    @event),

            _ when outcome.Contains("merge") =>
                CreateEvent("Project.Merged",
                    new ProjectMerged(ExtractTargetProjectId(eventData.Outcome ?? string.Empty), eventData.Outcome ?? string.Empty, eventData.CompletedBy, eventData.CompletedAt),
                    @event),

            // Default to successful completion if we can't determine the outcome
            _ => CreateEvent("Project.CompletedSuccessfully",
                    new ProjectCompletedSuccessfully(eventData.Outcome ?? string.Empty, eventData.CompletedBy, eventData.CompletedAt),
                    @event)
        };

        yield return newEvent;
    }

    private static IEvent CreateEvent<T>(string eventType, T data, IEvent originalEvent) where T : class
    {
        return new Event<T>
        {
            EventType = eventType,
            EventVersion = originalEvent.EventVersion,
            ExternalSequencer = originalEvent.ExternalSequencer,
            Data = data,
            Payload = originalEvent.Payload,
            ActionMetadata = originalEvent.ActionMetadata ?? new ActionMetadata(),
            Metadata = originalEvent.Metadata
        };
    }

    private static string ExtractTargetProjectId(string outcome)
    {
        // Try to extract a GUID from the outcome text
        // Format expected: "Merged into project {guid}" or similar
        var parts = outcome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (Guid.TryParse(part, out var _))
            {
                return part;
            }
        }

        // Return a placeholder if no GUID found
        return "00000000-0000-0000-0000-000000000000";
    }
}
