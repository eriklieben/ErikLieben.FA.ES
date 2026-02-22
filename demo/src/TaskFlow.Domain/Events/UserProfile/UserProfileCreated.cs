using System;
using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.UserProfile;

[EventName("UserProfile.Created")]
public record UserProfileCreated(
    string UserId,
    string DisplayName,
    string Email,
    string JobRole,
    DateTime CreatedAt);
