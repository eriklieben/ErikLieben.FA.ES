using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace ErikLieben.FA.ES.Configuration;

/// <summary>
/// Configuration settings for default type names used in the event sourcing infrastructure.
/// </summary>
/// <param name="StreamType">The default type name for event streams.</param>
/// <param name="DocumentType">The default type name for documents.</param>
/// <param name="DocumentTagType">The default type name for document tags.</param>
/// <param name="EventStreamTagType">The default type name for event stream tags.</param>
/// <param name="DocumentRefType">The default type name for document references.</param>
public record EventStreamDefaultTypeSettings(
    string StreamType,
    string DocumentType,
    string DocumentTagType,
    string EventStreamTagType,
    string DocumentRefType)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamDefaultTypeSettings"/> class
    /// with the same type name for all type categories.
    /// </summary>
    /// <param name="all">The type name to use for all categories.</param>
    public EventStreamDefaultTypeSettings(string all) : this(all, all, all, all, all)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamDefaultTypeSettings"/> class
    /// with empty type names for all categories.
    /// </summary>
    public EventStreamDefaultTypeSettings() : this(string.Empty)
    {

    }
}
