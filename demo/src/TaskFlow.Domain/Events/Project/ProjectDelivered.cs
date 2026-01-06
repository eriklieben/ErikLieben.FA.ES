using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Project was delivered to production/client
/// </summary>
[EventName("Project.Delivered")]
public record ProjectDelivered(
    string DeliveryNotes,
    string DeliveredBy,
    DateTime DeliveredAt);
