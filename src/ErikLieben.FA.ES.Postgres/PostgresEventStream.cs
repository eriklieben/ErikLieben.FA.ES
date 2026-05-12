using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Postgres-backed event stream. Thin subclass of <see cref="BaseEventStream"/>; all behavior comes from the base.
/// </summary>
public sealed class PostgresEventStream(
    IObjectDocumentWithMethods document,
    IStreamDependencies streamDependencies)
    : BaseEventStream(document, streamDependencies)
{
}
