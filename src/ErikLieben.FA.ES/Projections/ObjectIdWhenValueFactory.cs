using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Factory that extracts the object ID for When-method parameters.
/// </summary>
public class ObjectIdWhenValueFactory :
    IProjectionWhenParameterValueFactory<string>,
    IProjectionWhenParameterValueFactoryWithVersionToken<string>
{
    /// <summary>
    /// Creates the object ID from the document.
    /// </summary>
    public string Create(IObjectDocument document, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrEmpty(document.ObjectId))
        {
            throw new InvalidOperationException(
                $"ObjectId cannot be null or empty when processing event '{@event?.EventType}'. " +
                $"Document ObjectName: '{document.ObjectName}'");
        }
        return document.ObjectId;
    }

    /// <summary>
    /// Creates the object ID from the version token.
    /// </summary>
    public string Create(VersionToken versionToken, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(versionToken);
        if (string.IsNullOrEmpty(versionToken.ObjectId))
        {
            throw new InvalidOperationException(
                $"ObjectId cannot be null or empty when processing event '{@event?.EventType}'. " +
                $"VersionToken value: '{versionToken.Value}'");
        }
        return versionToken.ObjectId;
    }
}
