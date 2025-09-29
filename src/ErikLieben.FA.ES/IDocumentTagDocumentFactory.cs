﻿using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

/// <summary>
/// Provides factory methods to create document and stream tag stores based on document context or explicit type.
/// </summary>
public interface IDocumentTagDocumentFactory
{
    /// <summary>
    /// Creates a document tag store using the tag configuration of the specified document.
    /// </summary>
    /// <param name="document">The object document whose tag configuration determines which store to create.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> instance appropriate for the document's configured tag type.</returns>
    IDocumentTagStore CreateDocumentTagStore(IObjectDocument document);

    /// <summary>
    /// Creates a document tag store for the specified tag provider type (for example, "blob").
    /// </summary>
    /// <param name="type">The tag provider type key used to resolve the underlying store implementation.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> instance for the requested provider type.</returns>
    IDocumentTagStore CreateDocumentTagStore(string type);
}
