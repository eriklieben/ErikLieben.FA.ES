namespace ErikLieben.FA.ES.EventStreamManagement.BookClosing;

using ErikLieben.FA.ES.EventStreamManagement.Core;

/// <summary>
/// Builder for configuring book closing operations.
/// </summary>
public class BookClosingBuilder : IBookClosingBuilder
{
    private readonly BookClosingConfiguration config = new();

    /// <inheritdoc/>
    public IBookClosingBuilder Reason(string reason)
    {
        config.Reason = reason;
        return this;
    }

    /// <inheritdoc/>
    public IBookClosingBuilder CreateSnapshot()
    {
        config.CreateSnapshot = true;
        return this;
    }

    /// <inheritdoc/>
    public IBookClosingBuilder ArchiveToStorage(string storageLocation)
    {
        config.ArchiveLocation = storageLocation;
        return this;
    }

    /// <inheritdoc/>
    public IBookClosingBuilder MarkAsDeleted()
    {
        config.MarkAsDeleted = true;
        return this;
    }

    /// <inheritdoc/>
    public IBookClosingBuilder WithMetadata(string key, object value)
    {
        config.Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the book closing configuration.
    /// </summary>
    internal BookClosingConfiguration Build() => config;
}
