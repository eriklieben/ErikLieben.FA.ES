using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace ErikLieben.FA.ES.Configuration;

public record EventStreamDefaultTypeSettings(
    string StreamType,
    string DocumentType,
    string DocumentTagType,
    string EventStreamTagType,
    string DocumentRefType)
{
    public EventStreamDefaultTypeSettings(string all) : this(all, all, all, all, all)
    {

    }

    public EventStreamDefaultTypeSettings() : this(string.Empty)
    {
        
    }
}
