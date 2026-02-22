using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.Testing.Assertions;

/// <summary>
/// Provides fluent assertion methods for verifying projection state and checkpoints.
/// </summary>
/// <typeparam name="TProjection">The projection type that inherits from <see cref="Projection"/>.</typeparam>
public class ProjectionAssertion<TProjection> where TProjection : Projection
{
    private readonly TProjection _projection;

    internal ProjectionAssertion(TProjection projection)
    {
        _projection = projection;
    }

    /// <summary>
    /// Gets the projection instance being tested.
    /// </summary>
    public TProjection Projection => _projection;

    /// <summary>
    /// Asserts that the projection state satisfies the given assertion.
    /// </summary>
    /// <param name="assertion">The assertion to execute on the projection.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the assertion fails.</exception>
    public ProjectionAssertion<TProjection> ShouldHaveState(Action<TProjection> assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);

        try
        {
            assertion(_projection);
        }
        catch (Exception ex)
        {
            throw new TestAssertionException(
                $"Projection state assertion failed: {ex.Message}",
                ex);
        }

        return this;
    }

    /// <summary>
    /// Asserts that a specific property of the projection has the expected value.
    /// </summary>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="propertySelector">The property selector.</param>
    /// <param name="expectedValue">The expected value.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the property value doesn't match.</exception>
    public ProjectionAssertion<TProjection> ShouldHaveProperty<TValue>(
        Func<TProjection, TValue> propertySelector,
        TValue expectedValue)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        var actualValue = propertySelector(_projection);

        if (!EqualityComparer<TValue>.Default.Equals(actualValue, expectedValue))
        {
            throw new TestAssertionException(
                $"Expected projection property to be '{expectedValue}', but found '{actualValue}'.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the projection has a checkpoint for the specified object at the expected version.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="expectedVersion">The expected version number.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the checkpoint doesn't match.</exception>
    public ProjectionAssertion<TProjection> ShouldHaveCheckpoint(
        string objectName,
        string objectId,
        int expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(objectName);
        ArgumentNullException.ThrowIfNull(objectId);

        var objectIdentifier = new ObjectIdentifier(objectName, objectId);

        if (!_projection.Checkpoint.TryGetValue(objectIdentifier, out var versionIdentifier))
        {
            throw new TestAssertionException(
                $"Projection checkpoint does not contain entry for '{objectName}/{objectId}'.");
        }

        var actualVersion = new VersionToken(objectIdentifier, versionIdentifier).Version;

        if (actualVersion != expectedVersion)
        {
            throw new TestAssertionException(
                $"Expected checkpoint version {expectedVersion} for '{objectName}/{objectId}', " +
                $"but found {actualVersion}.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the projection has checkpoints for all specified objects.
    /// </summary>
    /// <param name="expectedCheckpoints">Dictionary of (objectName, objectId) to expected version.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when any checkpoint doesn't match.</exception>
    public ProjectionAssertion<TProjection> ShouldHaveCheckpoints(
        Dictionary<(string objectName, string objectId), int> expectedCheckpoints)
    {
        ArgumentNullException.ThrowIfNull(expectedCheckpoints);

        foreach (var kvp in expectedCheckpoints)
        {
            ShouldHaveCheckpoint(kvp.Key.objectName, kvp.Key.objectId, kvp.Value);
        }

        return this;
    }

    /// <summary>
    /// Asserts that the projection checkpoint has the expected fingerprint.
    /// </summary>
    /// <param name="expectedFingerprint">The expected checkpoint fingerprint.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the fingerprint doesn't match.</exception>
    public ProjectionAssertion<TProjection> ShouldHaveCheckpointFingerprint(
        string expectedFingerprint)
    {
        ArgumentNullException.ThrowIfNull(expectedFingerprint);

        if (_projection.CheckpointFingerprint != expectedFingerprint)
        {
            throw new TestAssertionException(
                $"Expected checkpoint fingerprint '{expectedFingerprint}', " +
                $"but found '{_projection.CheckpointFingerprint}'.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the projection checkpoint is not null or empty.
    /// </summary>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the checkpoint is null or empty.</exception>
    public ProjectionAssertion<TProjection> ShouldHaveNonEmptyCheckpoint()
    {
        if (_projection.Checkpoint == null || _projection.Checkpoint.Count == 0)
        {
            throw new TestAssertionException("Expected projection to have a non-empty checkpoint.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the projection checkpoint has the expected number of entries.
    /// </summary>
    /// <param name="expectedCount">The expected number of checkpoint entries.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the count doesn't match.</exception>
    public ProjectionAssertion<TProjection> ShouldHaveCheckpointCount(int expectedCount)
    {
        var actualCount = _projection.Checkpoint?.Count ?? 0;

        if (actualCount != expectedCount)
        {
            throw new TestAssertionException(
                $"Expected {expectedCount} checkpoint entries, but found {actualCount}.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the projection matches the stored snapshot.
    /// </summary>
    /// <param name="snapshotName">The name of the snapshot.</param>
    /// <param name="options">Optional snapshot options.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    public ProjectionAssertion<TProjection> ShouldMatchSnapshot(
        string snapshotName,
        SnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshotName);

        SnapshotAssertion.MatchesSnapshot(_projection, snapshotName, options);
        return this;
    }

    /// <summary>
    /// Asserts that a selected portion of the projection state matches the stored snapshot.
    /// </summary>
    /// <param name="snapshotName">The name of the snapshot.</param>
    /// <param name="stateSelector">A function to select the portion of state to snapshot.</param>
    /// <param name="options">Optional snapshot options.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    public ProjectionAssertion<TProjection> ShouldMatchSnapshot(
        string snapshotName,
        Func<TProjection, object> stateSelector,
        SnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshotName);
        ArgumentNullException.ThrowIfNull(stateSelector);

        var selectedState = stateSelector(_projection);
        SnapshotAssertion.MatchesSnapshot(selectedState, snapshotName, options);
        return this;
    }

    /// <summary>
    /// Asynchronously asserts that the projection matches the stored snapshot.
    /// </summary>
    /// <param name="snapshotName">The name of the snapshot.</param>
    /// <param name="options">Optional snapshot options.</param>
    /// <returns>A task that returns the assertion instance for method chaining.</returns>
    public async Task<ProjectionAssertion<TProjection>> ShouldMatchSnapshotAsync(
        string snapshotName,
        SnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshotName);

        await SnapshotAssertion.MatchesSnapshotAsync(_projection, snapshotName, options);
        return this;
    }

    /// <summary>
    /// Asynchronously asserts that a selected portion of the projection state matches the stored snapshot.
    /// </summary>
    /// <param name="snapshotName">The name of the snapshot.</param>
    /// <param name="stateSelector">A function to select the portion of state to snapshot.</param>
    /// <param name="options">Optional snapshot options.</param>
    /// <returns>A task that returns the assertion instance for method chaining.</returns>
    public async Task<ProjectionAssertion<TProjection>> ShouldMatchSnapshotAsync(
        string snapshotName,
        Func<TProjection, object> stateSelector,
        SnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshotName);
        ArgumentNullException.ThrowIfNull(stateSelector);

        var selectedState = stateSelector(_projection);
        await SnapshotAssertion.MatchesSnapshotAsync(selectedState, snapshotName, options);
        return this;
    }
}
