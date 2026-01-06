using System;
using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.UserProfile;

[EventName("UserProfile.Updated")]
public record UserProfileUpdated(
    string DisplayName,
    string Email,
    string JobRole,
    DateTime UpdatedAt);
