using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES;

/// <summary>
/// Represents a mapping of object identifiers to their last processed version identifiers.
/// </summary>
/// <remarks>
/// A projection uses a checkpoint to track progress across multiple streams. Keys are object identifiers,
/// and values are the latest processed version identifiers for those objects.
/// </remarks>
public class Checkpoint : Dictionary<ObjectIdentifier, VersionIdentifier>
{
}
