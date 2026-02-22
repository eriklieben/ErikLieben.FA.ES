#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3ProjectionStatusCoordinatorTests
{
    private readonly IS3ClientFactory _clientFactory;
    private readonly IAmazonS3 _s3Client;
    private readonly EventStreamS3Settings _settings;

    private static EventStreamS3Settings CreateSettings(bool supportsConditionalWrites = false) =>
        new("s3", serviceUrl: "http://localhost:9000", accessKey: "key", secretKey: "secret",
            supportsConditionalWrites: supportsConditionalWrites);

    public S3ProjectionStatusCoordinatorTests()
    {
        _clientFactory = Substitute.For<IS3ClientFactory>();
        _s3Client = Substitute.For<IAmazonS3>();
        _settings = CreateSettings();

        _clientFactory.CreateClient(Arg.Any<string>()).Returns(_s3Client);
    }

    private S3ProjectionStatusCoordinator CreateSut(string prefix = "projection-status") =>
        new(_clientFactory, _settings, prefix);

    private S3ProjectionStatusCoordinator CreateSut(EventStreamS3Settings settings, string prefix = "projection-status") =>
        new(_clientFactory, settings, prefix);

    private static GetObjectResponse CreateGetObjectResponse(S3ProjectionStatusCoordinator.StatusDocument document, string etag = "\"etag-1\"")
    {
        var json = JsonSerializer.Serialize(document, S3StatusDocumentJsonContext.Default.StatusDocument);
        var bytes = Encoding.UTF8.GetBytes(json);
        var stream = new MemoryStream(bytes);

        return new GetObjectResponse
        {
            ResponseStream = stream,
            ETag = etag,
            HttpStatusCode = HttpStatusCode.OK
        };
    }

    private void SetupGetObjectReturns(S3ProjectionStatusCoordinator.StatusDocument document, string etag = "\"etag-1\"")
    {
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateGetObjectResponse(document, etag));
    }

    private void SetupGetObjectNotFound()
    {
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });
    }

    private void SetupPutObjectSuccess()
    {
        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"new-etag\"", HttpStatusCode = HttpStatusCode.OK });
    }

    private void SetupListObjectsReturns(params string[] keys)
    {
        var s3Objects = keys.Select(k => new S3Object { Key = k }).ToList();
        _s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
            .Returns(new ListObjectsV2Response
            {
                S3Objects = s3Objects,
                IsTruncated = false
            });
    }

    public class Constructor : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3ProjectionStatusCoordinator(null!, CreateSettings()));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3ProjectionStatusCoordinator(Substitute.For<IS3ClientFactory>(), null!));
        }

        [Fact]
        public void Should_throw_when_prefix_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3ProjectionStatusCoordinator(Substitute.For<IS3ClientFactory>(), CreateSettings(), null!));
        }

        [Fact]
        public void Should_create_instance_with_default_prefix()
        {
            var sut = new S3ProjectionStatusCoordinator(
                Substitute.For<IS3ClientFactory>(), CreateSettings());

            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_custom_prefix()
        {
            var sut = new S3ProjectionStatusCoordinator(
                Substitute.For<IS3ClientFactory>(), CreateSettings(), "custom-prefix");

            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_null_logger()
        {
            var sut = new S3ProjectionStatusCoordinator(
                Substitute.For<IS3ClientFactory>(), CreateSettings(), "projection-status", null);

            Assert.NotNull(sut);
        }
    }

    public class StartRebuildAsync : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_projection_name_is_null()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartRebuildAsync(null!, "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30)));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_id_is_null()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartRebuildAsync("MyProjection", null!, RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30)));
        }

        [Fact]
        public async Task Should_return_rebuild_token_on_success()
        {
            SetupPutObjectSuccess();
            var sut = CreateSut();

            var token = await sut.StartRebuildAsync(
                "MyProjection", "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30));

            Assert.NotNull(token);
            Assert.Equal("MyProjection", token.ProjectionName);
            Assert.Equal("object-1", token.ObjectId);
            Assert.Equal(RebuildStrategy.BlockingWithCatchUp, token.Strategy);
            Assert.False(token.IsExpired);
        }

        [Fact]
        public async Task Should_upload_status_document_to_s3()
        {
            SetupPutObjectSuccess();
            var sut = CreateSut();

            await sut.StartRebuildAsync(
                "MyProjection", "object-1", RebuildStrategy.BlueGreen, TimeSpan.FromMinutes(30));

            await _s3Client.Received(1).PutObjectAsync(
                Arg.Is<PutObjectRequest>(r =>
                    r.Key == "projection-status/MyProjection_object-1.json" &&
                    r.ContentType == "application/json"),
                Arg.Any<CancellationToken>());
        }
    }

    public class GetStatusAsync : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_return_null_when_object_not_found()
        {
            SetupGetObjectNotFound();
            var sut = CreateSut();

            var result = await sut.GetStatusAsync("MyProjection", "object-1");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_status_when_document_exists()
        {
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Active,
                DateTimeOffset.UtcNow, 0);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, null);
            SetupGetObjectReturns(document);
            var sut = CreateSut();

            var result = await sut.GetStatusAsync("MyProjection", "object-1");

            Assert.NotNull(result);
            Assert.Equal("MyProjection", result.ProjectionName);
            Assert.Equal("object-1", result.ObjectId);
            Assert.Equal(ProjectionStatus.Active, result.Status);
        }

        [Fact]
        public async Task Should_propagate_non_404_exception()
        {
            _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new AmazonS3Exception("Internal Server Error") { StatusCode = HttpStatusCode.InternalServerError });
            var sut = CreateSut();

            await Assert.ThrowsAsync<AmazonS3Exception>(() =>
                sut.GetStatusAsync("MyProjection", "object-1"));
        }
    }

    public class StartCatchUpAsync : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartCatchUpAsync(null!));
        }

        [Fact]
        public async Task Should_throw_when_token_does_not_match()
        {
            var token = RebuildToken.Create("MyProjection", "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30));
            var differentToken = RebuildToken.Create("MyProjection", "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30));
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Rebuilding,
                DateTimeOffset.UtcNow, 0);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, differentToken);
            SetupGetObjectReturns(document);
            var sut = CreateSut();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.StartCatchUpAsync(token));
        }

        [Fact]
        public async Task Should_update_status_to_catching_up()
        {
            var token = RebuildToken.Create("MyProjection", "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30));
            var rebuildInfo = RebuildInfo.Start(RebuildStrategy.BlockingWithCatchUp);
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Rebuilding,
                DateTimeOffset.UtcNow, 0, rebuildInfo);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, token);
            SetupGetObjectReturns(document);
            SetupPutObjectSuccess();
            var sut = CreateSut();

            await sut.StartCatchUpAsync(token);

            await _s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class MarkReadyAsync : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.MarkReadyAsync(null!));
        }
    }

    public class CompleteRebuildAsync : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.CompleteRebuildAsync(null!));
        }

        [Fact]
        public async Task Should_set_status_to_active_and_clear_token()
        {
            var token = RebuildToken.Create("MyProjection", "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30));
            var rebuildInfo = RebuildInfo.Start(RebuildStrategy.BlockingWithCatchUp);
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.CatchingUp,
                DateTimeOffset.UtcNow, 0, rebuildInfo);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, token);
            SetupGetObjectReturns(document);
            SetupPutObjectSuccess();
            var sut = CreateSut();

            await sut.CompleteRebuildAsync(token);

            await _s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class CancelRebuildAsync : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.CancelRebuildAsync(null!));
        }

        [Fact]
        public async Task Should_set_status_to_failed_when_error_provided()
        {
            var token = RebuildToken.Create("MyProjection", "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30));
            var rebuildInfo = RebuildInfo.Start(RebuildStrategy.BlockingWithCatchUp);
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Rebuilding,
                DateTimeOffset.UtcNow, 0, rebuildInfo);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, token);
            SetupGetObjectReturns(document);
            SetupPutObjectSuccess();
            var sut = CreateSut();

            await sut.CancelRebuildAsync(token, "Something went wrong");

            await _s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_set_status_to_active_when_no_error()
        {
            var token = RebuildToken.Create("MyProjection", "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30));
            var rebuildInfo = RebuildInfo.Start(RebuildStrategy.BlockingWithCatchUp);
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Rebuilding,
                DateTimeOffset.UtcNow, 0, rebuildInfo);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, token);
            SetupGetObjectReturns(document);
            SetupPutObjectSuccess();
            var sut = CreateSut();

            await sut.CancelRebuildAsync(token);

            await _s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class GetByStatusAsync : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_return_empty_when_no_objects()
        {
            SetupListObjectsReturns();
            var sut = CreateSut();

            var result = await sut.GetByStatusAsync(ProjectionStatus.Active);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_filter_by_status()
        {
            var activeStatus = new ProjectionStatusInfo(
                "Proj1", "obj-1", ProjectionStatus.Active,
                DateTimeOffset.UtcNow, 0);
            var rebuildingStatus = new ProjectionStatusInfo(
                "Proj2", "obj-2", ProjectionStatus.Rebuilding,
                DateTimeOffset.UtcNow, 0);

            SetupListObjectsReturns(
                "projection-status/Proj1_obj-1.json",
                "projection-status/Proj2_obj-2.json");

            var callCount = 0;
            _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    callCount++;
                    return callCount == 1
                        ? CreateGetObjectResponse(new S3ProjectionStatusCoordinator.StatusDocument(activeStatus, null))
                        : CreateGetObjectResponse(new S3ProjectionStatusCoordinator.StatusDocument(rebuildingStatus, null));
                });

            var sut = CreateSut();

            var result = (await sut.GetByStatusAsync(ProjectionStatus.Active)).ToList();

            Assert.Single(result);
            Assert.Equal("Proj1", result[0].ProjectionName);
        }

        [Fact]
        public async Task Should_skip_non_json_objects()
        {
            SetupListObjectsReturns("projection-status/readme.txt");
            var sut = CreateSut();

            var result = await sut.GetByStatusAsync(ProjectionStatus.Active);

            Assert.Empty(result);
            await _s3Client.DidNotReceive().GetObjectAsync(
                Arg.Any<GetObjectRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class RecoverStuckRebuildsAsync : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_return_zero_when_no_objects()
        {
            SetupListObjectsReturns();
            var sut = CreateSut();

            var result = await sut.RecoverStuckRebuildsAsync();

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_recover_expired_rebuilding_entries()
        {
            var expiredToken = new RebuildToken(
                "MyProjection", "object-1", "token-1",
                RebuildStrategy.BlockingWithCatchUp,
                DateTimeOffset.UtcNow.AddHours(-2),
                DateTimeOffset.UtcNow.AddHours(-1)); // expired
            var rebuildInfo = RebuildInfo.Start(RebuildStrategy.BlockingWithCatchUp);
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Rebuilding,
                DateTimeOffset.UtcNow.AddHours(-2), 0, rebuildInfo);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, expiredToken);

            SetupListObjectsReturns("projection-status/MyProjection_object-1.json");
            _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(CreateGetObjectResponse(document));
            SetupPutObjectSuccess();
            var sut = CreateSut();

            var result = await sut.RecoverStuckRebuildsAsync();

            Assert.Equal(1, result);
            await _s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_recover_non_expired_entries()
        {
            var validToken = RebuildToken.Create(
                "MyProjection", "object-1",
                RebuildStrategy.BlockingWithCatchUp,
                TimeSpan.FromHours(1));
            var rebuildInfo = RebuildInfo.Start(RebuildStrategy.BlockingWithCatchUp);
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Rebuilding,
                DateTimeOffset.UtcNow, 0, rebuildInfo);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, validToken);

            SetupListObjectsReturns("projection-status/MyProjection_object-1.json");
            _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(CreateGetObjectResponse(document));
            var sut = CreateSut();

            var result = await sut.RecoverStuckRebuildsAsync();

            Assert.Equal(0, result);
            await _s3Client.DidNotReceive().PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_recover_active_entries()
        {
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Active,
                DateTimeOffset.UtcNow, 0);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, null);

            SetupListObjectsReturns("projection-status/MyProjection_object-1.json");
            _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(CreateGetObjectResponse(document));
            var sut = CreateSut();

            var result = await sut.RecoverStuckRebuildsAsync();

            Assert.Equal(0, result);
        }
    }

    public class DisableAsync : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_set_status_to_disabled_for_existing_document()
        {
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Active,
                DateTimeOffset.UtcNow, 0);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, null);
            SetupGetObjectReturns(document);
            SetupPutObjectSuccess();
            var sut = CreateSut();

            await sut.DisableAsync("MyProjection", "object-1");

            await _s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_create_disabled_document_when_not_found()
        {
            SetupGetObjectNotFound();
            SetupPutObjectSuccess();
            var sut = CreateSut();

            await sut.DisableAsync("MyProjection", "object-1");

            await _s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class EnableAsync : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_set_status_to_active_for_existing_document()
        {
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Disabled,
                DateTimeOffset.UtcNow, 0);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, null);
            SetupGetObjectReturns(document);
            SetupPutObjectSuccess();
            var sut = CreateSut();

            await sut.EnableAsync("MyProjection", "object-1");

            await _s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_do_nothing_when_document_not_found()
        {
            SetupGetObjectNotFound();
            var sut = CreateSut();

            await sut.EnableAsync("MyProjection", "object-1");

            await _s3Client.DidNotReceive().PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class ConditionalWriteSupport : S3ProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_include_if_match_header_when_conditional_writes_supported()
        {
            var settings = CreateSettings(supportsConditionalWrites: true);
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Active,
                DateTimeOffset.UtcNow, 0);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, null);
            SetupGetObjectReturns(document, "\"etag-123\"");
            SetupPutObjectSuccess();
            var sut = CreateSut(settings);

            await sut.DisableAsync("MyProjection", "object-1");

            await _s3Client.Received(1).PutObjectAsync(
                Arg.Is<PutObjectRequest>(r => r.Headers["If-Match"] == "\"etag-123\""),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_include_if_match_header_when_conditional_writes_not_supported()
        {
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Active,
                DateTimeOffset.UtcNow, 0);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, null);
            SetupGetObjectReturns(document, "\"etag-123\"");
            SetupPutObjectSuccess();
            var sut = CreateSut(); // default settings, no conditional writes

            await sut.DisableAsync("MyProjection", "object-1");

            await _s3Client.Received(1).PutObjectAsync(
                Arg.Is<PutObjectRequest>(r => string.IsNullOrEmpty(r.Headers["If-Match"])),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_on_precondition_failed()
        {
            var settings = CreateSettings(supportsConditionalWrites: true);
            var statusInfo = new ProjectionStatusInfo(
                "MyProjection", "object-1", ProjectionStatus.Active,
                DateTimeOffset.UtcNow, 0);
            var document = new S3ProjectionStatusCoordinator.StatusDocument(statusInfo, null);
            SetupGetObjectReturns(document, "\"etag-123\"");
            _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new AmazonS3Exception("Precondition Failed") { StatusCode = HttpStatusCode.PreconditionFailed });
            var sut = CreateSut(settings);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.DisableAsync("MyProjection", "object-1"));
        }
    }
}
