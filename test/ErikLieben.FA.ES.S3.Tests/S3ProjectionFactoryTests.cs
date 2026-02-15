using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using System.Text;
using ErikLieben.FA.ES.VersionTokenParts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.S3.Tests;

/// <summary>
/// A minimal projection subclass used for testing S3ProjectionFactory.
/// Implements all abstract members of <see cref="Projection"/>.
/// </summary>
public class TestProjection : Projection
{
    public string? Name { get; set; }
    public int Value { get; set; }

    [JsonPropertyName("$checkpoint")]
    public override Checkpoint Checkpoint { get; set; } = new();

    protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; } = new();

    public override string ToJson()
    {
        return JsonSerializer.Serialize(this, TestProjectionJsonContext.Default.TestProjection);
    }

    public override Task Fold<T>(
        IEvent @event,
        VersionToken versionToken,
        T? data = null,
        IExecutionContext? context = null) where T : class
    {
        return Task.CompletedTask;
    }

    protected override Task PostWhenAll(IObjectDocument document)
    {
        return Task.CompletedTask;
    }
}

[JsonSerializable(typeof(TestProjection))]
internal partial class TestProjectionJsonContext : JsonSerializerContext
{
}

/// <summary>
/// A concrete factory subclass that makes the abstract S3ProjectionFactory testable.
/// </summary>
public class TestS3ProjectionFactory : S3ProjectionFactory<TestProjection>
{
    private readonly bool _hasExternalCheckpoint;

    public TestS3ProjectionFactory(
        IS3ClientFactory factory,
        string clientName,
        string bucket,
        bool autoCreate = true,
        bool hasExternalCheckpoint = false)
        : base(factory, clientName, bucket, autoCreate)
    {
        _hasExternalCheckpoint = hasExternalCheckpoint;
    }

    protected override bool HasExternalCheckpoint => _hasExternalCheckpoint;

    protected override TestProjection New() => new TestProjection();

    protected override TestProjection? LoadFromJson(
        string json,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory)
    {
        return JsonSerializer.Deserialize(json, TestProjectionJsonContext.Default.TestProjection);
    }
}

public class S3ProjectionFactoryTests
{
    private const string ClientName = "s3";
    private const string BucketName = "projections";

    private static (TestS3ProjectionFactory Factory, IAmazonS3 S3Client) CreateSut(
        bool autoCreate = true,
        bool hasExternalCheckpoint = false)
    {
        var clientFactory = Substitute.For<IS3ClientFactory>();
        var s3Client = Substitute.For<IAmazonS3>();
        clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

        // EnsureBucketAsync succeeds
        s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutBucketResponse());

        var factory = new TestS3ProjectionFactory(clientFactory, ClientName, BucketName, autoCreate, hasExternalCheckpoint);
        return (factory, s3Client);
    }

    private static void SetupObjectExists(IAmazonS3 s3Client)
    {
        s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectMetadataResponse());
    }

    private static void SetupObjectNotFound(IAmazonS3 s3Client)
    {
        s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });
    }

    private static void SetupGetObjectReturns(IAmazonS3 s3Client, string json)
    {
        s3Client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse
            {
                ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(json))
            });
    }

    private static void SetupGetObjectNotFound(IAmazonS3 s3Client)
    {
        s3Client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });
    }

    public class Constructor
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TestS3ProjectionFactory(null!, ClientName, BucketName));
        }

        [Fact]
        public void Should_throw_when_client_name_is_null()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();

            Assert.Throws<ArgumentNullException>(() =>
                new TestS3ProjectionFactory(clientFactory, null!, BucketName));
        }

        [Fact]
        public void Should_throw_when_bucket_is_null()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();

            Assert.Throws<ArgumentNullException>(() =>
                new TestS3ProjectionFactory(clientFactory, ClientName, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var (factory, _) = CreateSut();

            Assert.NotNull(factory);
        }
    }

    public class ProjectionTypeProperty
    {
        [Fact]
        public void Should_return_correct_type()
        {
            var (factory, _) = CreateSut();

            Assert.Equal(typeof(TestProjection), factory.ProjectionType);
        }
    }

    public class DeleteAsyncTests
    {
        [Fact]
        public async Task Should_delete_object_from_s3()
        {
            var (factory, s3Client) = CreateSut();

            s3Client.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DeleteObjectResponse());

            await factory.DeleteAsync();

            await s3Client.Received(1).DeleteObjectAsync(BucketName, "TestProjection.json", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_delete_object_with_custom_blob_name()
        {
            var (factory, s3Client) = CreateSut();

            s3Client.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DeleteObjectResponse());

            await factory.DeleteAsync("custom.json");

            await s3Client.Received(1).DeleteObjectAsync(BucketName, "custom.json", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_ignore_not_found_exception()
        {
            var (factory, s3Client) = CreateSut();

            s3Client.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

            // Should not throw
            await factory.DeleteAsync();
        }
    }

    public class ExistsAsyncTests
    {
        [Fact]
        public async Task Should_return_true_when_object_exists()
        {
            var (factory, s3Client) = CreateSut();
            SetupObjectExists(s3Client);

            var result = await factory.ExistsAsync();

            Assert.True(result);
        }

        [Fact]
        public async Task Should_return_false_when_object_not_found()
        {
            var (factory, s3Client) = CreateSut();
            SetupObjectNotFound(s3Client);

            var result = await factory.ExistsAsync();

            Assert.False(result);
        }

        [Fact]
        public async Task Should_use_default_blob_name()
        {
            var (factory, s3Client) = CreateSut();
            SetupObjectExists(s3Client);

            await factory.ExistsAsync();

            await s3Client.Received(1).GetObjectMetadataAsync(BucketName, "TestProjection.json", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_use_custom_blob_name()
        {
            var (factory, s3Client) = CreateSut();
            SetupObjectExists(s3Client);

            await factory.ExistsAsync("custom.json");

            await s3Client.Received(1).GetObjectMetadataAsync(BucketName, "custom.json", Arg.Any<CancellationToken>());
        }
    }

    public class GetLastModifiedAsyncTests
    {
        [Fact]
        public async Task Should_return_timestamp_when_object_exists()
        {
            var (factory, s3Client) = CreateSut();
            var expectedDate = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectMetadataResponse
                {
                    LastModified = expectedDate
                });

            var result = await factory.GetLastModifiedAsync();

            Assert.NotNull(result);
            Assert.Equal(expectedDate, result.Value.DateTime);
        }

        [Fact]
        public async Task Should_return_null_when_object_not_found()
        {
            var (factory, s3Client) = CreateSut();
            SetupObjectNotFound(s3Client);

            var result = await factory.GetLastModifiedAsync();

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_use_default_blob_name()
        {
            var (factory, s3Client) = CreateSut();
            SetupObjectNotFound(s3Client);

            await factory.GetLastModifiedAsync();

            await s3Client.Received(1).GetObjectMetadataAsync(BucketName, "TestProjection.json", Arg.Any<CancellationToken>());
        }
    }

    public class GetStatusAsyncTests
    {
        [Fact]
        public async Task Should_return_active_when_object_not_found()
        {
            var (factory, s3Client) = CreateSut();
            SetupGetObjectNotFound(s3Client);

            var result = await factory.GetStatusAsync();

            Assert.Equal(ProjectionStatus.Active, result);
        }

        [Fact]
        public async Task Should_return_status_from_json()
        {
            var (factory, s3Client) = CreateSut();
            var json = """{"$status":1,"Name":"test"}""";
            SetupGetObjectReturns(s3Client, json);

            var result = await factory.GetStatusAsync();

            Assert.Equal(ProjectionStatus.Rebuilding, result);
        }

        [Fact]
        public async Task Should_return_active_when_no_status_property_in_json()
        {
            var (factory, s3Client) = CreateSut();
            var json = """{"Name":"test"}""";
            SetupGetObjectReturns(s3Client, json);

            var result = await factory.GetStatusAsync();

            Assert.Equal(ProjectionStatus.Active, result);
        }

        [Fact]
        public async Task Should_return_disabled_status_from_json()
        {
            var (factory, s3Client) = CreateSut();
            var json = """{"$status":2,"Name":"test"}""";
            SetupGetObjectReturns(s3Client, json);

            var result = await factory.GetStatusAsync();

            Assert.Equal(ProjectionStatus.Disabled, result);
        }
    }

    public class SetStatusAsyncTests
    {
        [Fact]
        public async Task Should_create_status_object_when_not_exists()
        {
            var (factory, s3Client) = CreateSut();
            SetupObjectNotFound(s3Client);

            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse());

            await factory.SetStatusAsync(ProjectionStatus.Rebuilding);

            await s3Client.Received(1).PutObjectAsync(
                Arg.Is<PutObjectRequest>(r =>
                    r.BucketName == BucketName &&
                    r.Key == "TestProjection.json" &&
                    r.ContentType == "application/json"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_update_status_in_existing_json()
        {
            var (factory, s3Client) = CreateSut();
            SetupObjectExists(s3Client);

            var existingJson = """{"$status":0,"Name":"test","Value":42}""";
            SetupGetObjectReturns(s3Client, existingJson);

            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse());

            await factory.SetStatusAsync(ProjectionStatus.Rebuilding);

            await s3Client.Received(1).PutObjectAsync(
                Arg.Is<PutObjectRequest>(r =>
                    r.BucketName == BucketName &&
                    r.Key == "TestProjection.json"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_add_status_property_when_missing_from_existing_json()
        {
            var (factory, s3Client) = CreateSut();
            SetupObjectExists(s3Client);

            var existingJson = """{"Name":"test","Value":42}""";
            SetupGetObjectReturns(s3Client, existingJson);

            PutObjectRequest? capturedRequest = null;
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    capturedRequest = callInfo.Arg<PutObjectRequest>();
                    return new PutObjectResponse();
                });

            await factory.SetStatusAsync(ProjectionStatus.Disabled);

            Assert.NotNull(capturedRequest);

            // Read the body that was sent
            capturedRequest!.InputStream.Position = 0;
            using var reader = new StreamReader(capturedRequest.InputStream);
            var savedJson = await reader.ReadToEndAsync();

            using var doc = JsonDocument.Parse(savedJson);
            Assert.True(doc.RootElement.TryGetProperty("$status", out var statusEl));
            Assert.Equal((int)ProjectionStatus.Disabled, statusEl.GetInt32());
        }

        [Fact]
        public async Task Should_use_custom_blob_name()
        {
            var (factory, s3Client) = CreateSut();
            SetupObjectNotFound(s3Client);

            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse());

            await factory.SetStatusAsync(ProjectionStatus.Rebuilding, "custom.json");

            await s3Client.Received().GetObjectMetadataAsync(BucketName, "custom.json", Arg.Any<CancellationToken>());
        }
    }

    public class SaveAsyncTests
    {
        [Fact]
        public async Task Should_throw_when_projection_is_null()
        {
            var (factory, _) = CreateSut();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                factory.SaveAsync(null!));
        }

        [Fact]
        public async Task Should_save_projection_to_s3()
        {
            var (factory, s3Client) = CreateSut();

            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse());

            var projection = new TestProjection { Name = "saved", Value = 99 };

            await factory.SaveAsync(projection);

            await s3Client.Received(1).PutObjectAsync(
                Arg.Is<PutObjectRequest>(r =>
                    r.BucketName == BucketName &&
                    r.Key == "TestProjection.json" &&
                    r.ContentType == "application/json"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_save_with_custom_blob_name()
        {
            var (factory, s3Client) = CreateSut();

            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse());

            var projection = new TestProjection { Name = "saved", Value = 1 };

            await factory.SaveAsync(projection, "custom.json");

            await s3Client.Received(1).PutObjectAsync(
                Arg.Is<PutObjectRequest>(r => r.Key == "custom.json"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_serialize_projection_as_json()
        {
            var (factory, s3Client) = CreateSut();

            PutObjectRequest? capturedRequest = null;
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    capturedRequest = callInfo.Arg<PutObjectRequest>();
                    return new PutObjectResponse();
                });

            var projection = new TestProjection { Name = "serialize-test", Value = 42 };

            await factory.SaveAsync(projection);

            Assert.NotNull(capturedRequest);
            capturedRequest!.InputStream.Position = 0;
            using var reader = new StreamReader(capturedRequest.InputStream);
            var savedJson = await reader.ReadToEndAsync();

            using var doc = JsonDocument.Parse(savedJson);
            Assert.True(doc.RootElement.TryGetProperty("Name", out var nameEl));
            Assert.Equal("serialize-test", nameEl.GetString());
            Assert.True(doc.RootElement.TryGetProperty("Value", out var valueEl));
            Assert.Equal(42, valueEl.GetInt32());
        }
    }

    public class SaveProjectionAsyncTests
    {
        [Fact]
        public async Task Should_throw_when_projection_is_wrong_type()
        {
            var (factory, _) = CreateSut();

            var wrongProjection = Substitute.For<Projection>();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                factory.SaveProjectionAsync(wrongProjection));
        }

        [Fact]
        public async Task Should_save_when_projection_is_correct_type()
        {
            var (factory, s3Client) = CreateSut();

            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse());

            Projection projection = new TestProjection { Name = "typed", Value = 10 };

            await factory.SaveProjectionAsync(projection);

            await s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class GetOrCreateAsyncTests
    {
        [Fact]
        public async Task Should_return_new_projection_when_not_found()
        {
            var (factory, s3Client) = CreateSut();
            SetupGetObjectNotFound(s3Client);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            var result = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            Assert.NotNull(result);
            Assert.IsType<TestProjection>(result);
            Assert.Null(result.Name);
            Assert.Equal(0, result.Value);
        }

        [Fact]
        public async Task Should_load_projection_from_s3_when_exists()
        {
            var (factory, s3Client) = CreateSut();

            var projection = new TestProjection { Name = "loaded", Value = 77 };
            var json = JsonSerializer.Serialize(projection, TestProjectionJsonContext.Default.TestProjection);
            SetupGetObjectReturns(s3Client, json);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            var result = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            Assert.NotNull(result);
            Assert.Equal("loaded", result.Name);
            Assert.Equal(77, result.Value);
        }

        [Fact]
        public async Task Should_use_default_blob_name()
        {
            var (factory, s3Client) = CreateSut();
            SetupGetObjectNotFound(s3Client);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            await s3Client.Received(1).GetObjectAsync(BucketName, "TestProjection.json", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_use_custom_blob_name()
        {
            var (factory, s3Client) = CreateSut();
            SetupGetObjectNotFound(s3Client);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            await factory.GetOrCreateAsync(documentFactory, eventStreamFactory, "custom.json");

            await s3Client.Received(1).GetObjectAsync(BucketName, "custom.json", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_new_when_json_deserializes_to_null()
        {
            var (factory, s3Client) = CreateSut();

            // Return invalid JSON that will deserialize to null
            SetupGetObjectReturns(s3Client, "null");

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            var result = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            Assert.NotNull(result);
            Assert.Null(result.Name);
        }
    }

    public class GetOrCreateProjectionAsyncTests
    {
        [Fact]
        public async Task Should_delegate_to_typed_get_or_create()
        {
            var (factory, s3Client) = CreateSut();
            SetupGetObjectNotFound(s3Client);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            var result = await factory.GetOrCreateProjectionAsync(documentFactory, eventStreamFactory);

            Assert.NotNull(result);
            Assert.IsType<TestProjection>(result);
        }
    }

    public class GetBucketNameAsyncTests
    {
        [Fact]
        public async Task Should_ensure_bucket_when_auto_create_is_true()
        {
            var (factory, s3Client) = CreateSut(autoCreate: true);
            SetupGetObjectNotFound(s3Client);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            // Trigger GetBucketNameAsync indirectly through GetOrCreateAsync
            await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            await s3Client.Received().PutBucketAsync(
                Arg.Is<PutBucketRequest>(r => r.BucketName == BucketName),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_ensure_bucket_when_auto_create_is_false()
        {
            var (factory, s3Client) = CreateSut(autoCreate: false);
            SetupGetObjectNotFound(s3Client);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            await s3Client.DidNotReceive().PutBucketAsync(
                Arg.Any<PutBucketRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class ExternalCheckpointTests
    {
        [Fact]
        public async Task Save_should_save_checkpoint_when_fingerprint_is_set()
        {
            var (factory, s3Client) = CreateSut(hasExternalCheckpoint: true);
            SetupObjectNotFound(s3Client); // Checkpoint does not exist yet

            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse());

            var projection = new TestProjection { Name = "ext-checkpoint", Value = 1 };
            projection.CheckpointFingerprint = "abc123fingerprint";

            await factory.SaveAsync(projection);

            // Expect two PutObject calls: one for the projection, one for the checkpoint
            await s3Client.Received(2).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Save_should_not_save_checkpoint_when_fingerprint_is_null()
        {
            var (factory, s3Client) = CreateSut(hasExternalCheckpoint: true);

            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse());

            var projection = new TestProjection { Name = "no-fingerprint", Value = 1 };
            projection.CheckpointFingerprint = null;

            await factory.SaveAsync(projection);

            // Only one PutObject call for the projection itself
            await s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Save_should_not_save_checkpoint_when_already_exists()
        {
            var (factory, s3Client) = CreateSut(hasExternalCheckpoint: true);
            SetupObjectExists(s3Client); // Checkpoint already exists

            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse());

            var projection = new TestProjection { Name = "existing-cp", Value = 1 };
            projection.CheckpointFingerprint = "existing-fingerprint";

            await factory.SaveAsync(projection);

            // Only one PutObject call for the projection (checkpoint skipped because it exists)
            await s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetOrCreate_should_load_external_checkpoint_when_fingerprint_set()
        {
            var (factory, s3Client) = CreateSut(hasExternalCheckpoint: true);

            var projection = new TestProjection { Name = "with-cp", Value = 5 };
            projection.CheckpointFingerprint = "fp-abc";
            var projectionJson = JsonSerializer.Serialize(projection, TestProjectionJsonContext.Default.TestProjection);

            var objId = new ObjectIdentifier("test", "obj1");
            var verId = new VersionIdentifier("stream1", 1);
            var checkpoint = new Checkpoint();
            checkpoint[objId] = verId;
            var checkpointJson = JsonSerializer.Serialize(checkpoint);

            // First GetObjectAsync call returns the projection JSON
            // Second GetObjectAsync call returns the checkpoint JSON
            s3Client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var key = callInfo.ArgAt<string>(1);
                    if (key.Contains("checkpoints/"))
                    {
                        return new GetObjectResponse
                        {
                            ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(checkpointJson))
                        };
                    }

                    return new GetObjectResponse
                    {
                        ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(projectionJson))
                    };
                });

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            var result = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            Assert.NotNull(result);
            Assert.Equal("with-cp", result.Name);
            Assert.True(result.Checkpoint.ContainsKey(objId));
        }

        [Fact]
        public async Task GetOrCreate_should_not_load_checkpoint_when_fingerprint_is_empty()
        {
            var (factory, s3Client) = CreateSut(hasExternalCheckpoint: true);

            var projection = new TestProjection { Name = "no-fp", Value = 3 };
            projection.CheckpointFingerprint = null; // No fingerprint
            var projectionJson = JsonSerializer.Serialize(projection, TestProjectionJsonContext.Default.TestProjection);

            SetupGetObjectReturns(s3Client, projectionJson);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            var result = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            Assert.NotNull(result);
            // Should only have called GetObjectAsync once (for the projection, not for checkpoint)
            await s3Client.Received(1).GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }
}
