using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests.Integration;

[Collection("MinIO")]
[Trait("Category", "Integration")]
public class S3DocumentStoreIntegrationTests : IAsyncLifetime
{
    private readonly MinioContainerFixture _fixture;
    private readonly EventStreamS3Settings _settings;
    private S3DocumentStore _sut = null!;

    public S3DocumentStoreIntegrationTests(MinioContainerFixture fixture)
    {
        _fixture = fixture;
        _settings = fixture.CreateSettings(bucketName: $"docstore-{Guid.NewGuid():N}");
    }

    public Task InitializeAsync()
    {
        S3DataStore.ClearVerifiedBucketsCache();
        var clientFactory = new S3ClientFactory(_settings);
        var tagFactory = Substitute.For<IDocumentTagDocumentFactory>();
        var typeSettings = new EventStreamDefaultTypeSettings("s3");
        _sut = new S3DocumentStore(clientFactory, tagFactory, _settings, typeSettings);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        S3DataStore.ClearVerifiedBucketsCache();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Should_create_and_get_document()
    {
        var document = await _sut.CreateAsync("testobject", "doc-001");

        Assert.NotNull(document);
        Assert.Equal("testobject", document.ObjectName);
        Assert.Equal("doc-001", document.ObjectId);
    }

    [Fact]
    public async Task Should_return_existing_document_on_duplicate_create()
    {
        var first = await _sut.CreateAsync("testobject", "doc-002");
        var second = await _sut.CreateAsync("testobject", "doc-002");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.ObjectId, second!.ObjectId);
    }

    [Fact]
    public async Task Should_get_document_by_name_and_id()
    {
        await _sut.CreateAsync("testobject", "doc-003");

        var document = await _sut.GetAsync("testobject", "doc-003");

        Assert.NotNull(document);
        Assert.Equal("doc-003", document!.ObjectId);
    }

    [Fact]
    public async Task Should_throw_for_nonexistent_document()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GetAsync("testobject", "nonexistent"));
    }

    [Fact]
    public async Task Should_set_and_persist_document()
    {
        var document = await _sut.CreateAsync("testobject", "doc-004");
        Assert.NotNull(document);

        await _sut.SetAsync(document);

        var retrieved = await _sut.GetAsync("testobject", "doc-004");
        Assert.NotNull(retrieved);
        Assert.Equal("doc-004", retrieved!.ObjectId);
    }
}
