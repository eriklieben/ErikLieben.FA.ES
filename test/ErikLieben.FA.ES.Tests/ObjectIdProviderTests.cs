using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Exceptions;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests;

public class ObjectIdProviderTests
{
    private readonly IDictionary<string, IObjectIdProvider> _providers;
    private readonly EventStreamDefaultTypeSettings _settings;
    private readonly IObjectIdProvider _blobProvider;

    public ObjectIdProviderTests()
    {
        _blobProvider = Substitute.For<IObjectIdProvider>();
        _providers = new Dictionary<string, IObjectIdProvider>
        {
            ["blob"] = _blobProvider
        };
        _settings = new EventStreamDefaultTypeSettings { DocumentType = "blob" };
    }

    public class ConstructorTests : ObjectIdProviderTests
    {
        [Fact]
        public void Should_throw_when_providers_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ObjectIdProvider(null!, _settings));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ObjectIdProvider(_providers, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Act
            var sut = new ObjectIdProvider(_providers, _settings);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class GetObjectIdsAsyncTests : ObjectIdProviderTests
    {
        [Fact]
        public async Task Should_delegate_to_correct_provider()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);
            var expectedResult = new PagedResult<string> { Items = ["id1", "id2"], ContinuationToken = "token" };
            _blobProvider.GetObjectIdsAsync("project", null, 10, Arg.Any<CancellationToken>())
                .Returns(expectedResult);

            // Act
            var result = await sut.GetObjectIdsAsync("project", null, 10);

            // Assert
            Assert.Equal(expectedResult, result);
            await _blobProvider.Received(1).GetObjectIdsAsync("project", null, 10, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetObjectIdsAsync(null!, null, 10));
        }

        [Fact]
        public async Task Should_throw_when_object_name_is_empty()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);

            // Act & Assert (empty string throws ArgumentException, not ArgumentNullException)
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.GetObjectIdsAsync("", null, 10));
        }

        [Fact]
        public async Task Should_throw_when_provider_not_found()
        {
            // Arrange
            var settings = new EventStreamDefaultTypeSettings { DocumentType = "unknown" };
            var sut = new ObjectIdProvider(_providers, settings);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnableToFindDocumentFactoryException>(() =>
                sut.GetObjectIdsAsync("project", null, 10));
            Assert.Contains("unknown", ex.Message);
        }

        [Fact]
        public async Task Should_pass_continuation_token()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);
            var expectedResult = new PagedResult<string> { Items = ["id3"], ContinuationToken = null };
            _blobProvider.GetObjectIdsAsync("project", "continue-token", 5, Arg.Any<CancellationToken>())
                .Returns(expectedResult);

            // Act
            var result = await sut.GetObjectIdsAsync("project", "continue-token", 5);

            // Assert
            Assert.Equal(expectedResult, result);
        }
    }

    public class ExistsAsyncTests : ObjectIdProviderTests
    {
        [Fact]
        public async Task Should_delegate_to_correct_provider()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);
            _blobProvider.ExistsAsync("project", "proj-123", Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await sut.ExistsAsync("project", "proj-123");

            // Assert
            Assert.True(result);
            await _blobProvider.Received(1).ExistsAsync("project", "proj-123", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_false_when_not_exists()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);
            _blobProvider.ExistsAsync("project", "unknown", Arg.Any<CancellationToken>())
                .Returns(false);

            // Act
            var result = await sut.ExistsAsync("project", "unknown");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ExistsAsync(null!, "id"));
        }

        [Fact]
        public async Task Should_throw_when_object_id_is_null()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ExistsAsync("project", null!));
        }

        [Fact]
        public async Task Should_throw_when_provider_not_found()
        {
            // Arrange
            var settings = new EventStreamDefaultTypeSettings { DocumentType = "cosmos" };
            var sut = new ObjectIdProvider(_providers, settings);

            // Act & Assert
            await Assert.ThrowsAsync<UnableToFindDocumentFactoryException>(() =>
                sut.ExistsAsync("project", "id"));
        }
    }

    public class CountAsyncTests : ObjectIdProviderTests
    {
        [Fact]
        public async Task Should_delegate_to_correct_provider()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);
            _blobProvider.CountAsync("project", Arg.Any<CancellationToken>())
                .Returns(42L);

            // Act
            var result = await sut.CountAsync("project");

            // Assert
            Assert.Equal(42L, result);
            await _blobProvider.Received(1).CountAsync("project", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.CountAsync(null!));
        }

        [Fact]
        public async Task Should_throw_when_object_name_is_whitespace()
        {
            // Arrange
            var sut = new ObjectIdProvider(_providers, _settings);

            // Act & Assert (whitespace throws ArgumentException, not ArgumentNullException)
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.CountAsync("   "));
        }

        [Fact]
        public async Task Should_throw_when_provider_not_found()
        {
            // Arrange
            var settings = new EventStreamDefaultTypeSettings { DocumentType = "table" };
            var sut = new ObjectIdProvider(_providers, settings);

            // Act & Assert
            await Assert.ThrowsAsync<UnableToFindDocumentFactoryException>(() =>
                sut.CountAsync("project"));
        }
    }

    public class ProviderResolutionTests : ObjectIdProviderTests
    {
        [Fact]
        public async Task Should_resolve_provider_case_insensitively()
        {
            // Arrange
            var settings = new EventStreamDefaultTypeSettings { DocumentType = "BLOB" };
            var sut = new ObjectIdProvider(_providers, settings);
            _blobProvider.CountAsync("test", Arg.Any<CancellationToken>()).Returns(5L);

            // Act
            var result = await sut.CountAsync("test");

            // Assert
            Assert.Equal(5L, result);
        }
    }
}
