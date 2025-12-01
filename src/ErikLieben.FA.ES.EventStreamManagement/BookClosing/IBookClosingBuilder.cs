namespace ErikLieben.FA.ES.EventStreamManagement.BookClosing;

/// <summary>
/// Builder for configuring book closing options.
/// </summary>
public interface IBookClosingBuilder
{
    /// <summary>
    /// Sets the reason for closing the book.
    /// </summary>
    /// <param name="reason">The reason description.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IBookClosingBuilder Reason(string reason);

    /// <summary>
    /// Creates a snapshot of the aggregate before closing the book.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IBookClosingBuilder CreateSnapshot();

    /// <summary>
    /// Archives the old stream to cold storage.
    /// </summary>
    /// <param name="storageLocation">The archive storage location.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IBookClosingBuilder ArchiveToStorage(string storageLocation);

    /// <summary>
    /// Marks the old stream as deleted after archiving.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IBookClosingBuilder MarkAsDeleted();

    /// <summary>
    /// Sets custom metadata for the terminated stream entry.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IBookClosingBuilder WithMetadata(string key, object value);
}
