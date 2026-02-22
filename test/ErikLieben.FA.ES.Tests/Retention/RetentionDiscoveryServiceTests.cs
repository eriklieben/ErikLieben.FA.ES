using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Retention;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Retention;

public class RetentionDiscoveryServiceTests
{
    private readonly IObjectIdProvider _objectIdProvider;
    private readonly IRetentionPolicyProvider _policyProvider;
    private readonly IStreamMetadataProvider _metadataProvider;
    private readonly RetentionOptions _options;

    public RetentionDiscoveryServiceTests()
    {
        _objectIdProvider = Substitute.For<IObjectIdProvider>();
        _policyProvider = Substitute.For<IRetentionPolicyProvider>();
        _metadataProvider = Substitute.For<IStreamMetadataProvider>();
        _options = new RetentionOptions();
    }

    private RetentionDiscoveryService CreateSut(RetentionOptions? options = null)
    {
        var opts = Options.Create(options ?? _options);
        return new RetentionDiscoveryService(_objectIdProvider, _policyProvider, _metadataProvider, opts);
    }

    public class ConstructorTests : RetentionDiscoveryServiceTests
    {
        [Fact]
        public void Should_throw_when_objectIdProvider_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new RetentionDiscoveryService(null!, _policyProvider, _metadataProvider));
        }

        [Fact]
        public void Should_throw_when_policyProvider_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new RetentionDiscoveryService(_objectIdProvider, null!, _metadataProvider));
        }

        [Fact]
        public void Should_throw_when_metadataProvider_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new RetentionDiscoveryService(_objectIdProvider, _policyProvider, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var sut = CreateSut();
            Assert.NotNull(sut);
        }
    }

    public class DiscoverViolationsAsyncTests : RetentionDiscoveryServiceTests
    {
        [Fact]
        public async Task Should_return_empty_when_no_registered_types()
        {
            // Arrange
            _policyProvider.GetRegisteredTypes().Returns(Array.Empty<string>());
            var sut = CreateSut();

            // Act
            var violations = new List<RetentionViolation>();
            await foreach (var v in sut.DiscoverViolationsAsync())
            {
                violations.Add(v);
            }

            // Assert
            Assert.Empty(violations);
        }

        [Fact]
        public async Task Should_return_empty_when_policy_not_found()
        {
            // Arrange
            _policyProvider.GetRegisteredTypes().Returns(["Order"]);
            // No policy override in options, no default
            var sut = CreateSut();

            // Act
            var violations = new List<RetentionViolation>();
            await foreach (var v in sut.DiscoverViolationsAsync())
            {
                violations.Add(v);
            }

            // Assert
            Assert.Empty(violations);
        }

        [Fact]
        public async Task Should_return_empty_when_policy_disabled()
        {
            // Arrange
            _policyProvider.GetRegisteredTypes().Returns(["Order"]);
            var options = new RetentionOptions();
            options.PolicyOverrides["Order"] = new RetentionPolicy { Enabled = false, MaxEvents = 10 };
            var sut = CreateSut(options);

            // Act
            var violations = new List<RetentionViolation>();
            await foreach (var v in sut.DiscoverViolationsAsync())
            {
                violations.Add(v);
            }

            // Assert
            Assert.Empty(violations);
        }

        [Fact]
        public async Task Should_discover_violations_when_event_count_exceeds_policy()
        {
            // Arrange
            var options = new RetentionOptions();
            options.PolicyOverrides["Order"] = new RetentionPolicy
            {
                Enabled = true,
                MaxEvents = 100,
                Action = RetentionAction.FlagForReview
            };

            _policyProvider.GetRegisteredTypes().Returns(["Order"]);
            _objectIdProvider.GetObjectIdsAsync("Order", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["order-1", "order-2"],
                    PageSize = 100
                });

            _metadataProvider.GetStreamMetadataAsync("Order", "order-1", Arg.Any<CancellationToken>())
                .Returns(new StreamMetadata("Order", "order-1", 200, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            _metadataProvider.GetStreamMetadataAsync("Order", "order-2", Arg.Any<CancellationToken>())
                .Returns(new StreamMetadata("Order", "order-2", 50, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

            var sut = CreateSut(options);

            // Act
            var violations = new List<RetentionViolation>();
            await foreach (var v in sut.DiscoverViolationsAsync())
            {
                violations.Add(v);
            }

            // Assert
            Assert.Single(violations);
            Assert.Equal("order-1", violations[0].StreamId);
            Assert.Equal("Order", violations[0].ObjectName);
            Assert.Equal(RetentionViolationType.ExceedsMaxEvents, violations[0].ViolationType);
        }

        [Fact]
        public async Task Should_respect_max_results()
        {
            // Arrange
            var options = new RetentionOptions();
            options.PolicyOverrides["Order"] = new RetentionPolicy
            {
                Enabled = true,
                MaxEvents = 10,
                Action = RetentionAction.FlagForReview
            };

            _policyProvider.GetRegisteredTypes().Returns(["Order"]);
            _objectIdProvider.GetObjectIdsAsync("Order", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["order-1", "order-2", "order-3"],
                    PageSize = 100
                });

            _metadataProvider.GetStreamMetadataAsync("Order", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new StreamMetadata("Order", "any", 100, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

            var sut = CreateSut(options);
            var discoveryOptions = new RetentionDiscoveryOptions { MaxResults = 2 };

            // Act
            var violations = new List<RetentionViolation>();
            await foreach (var v in sut.DiscoverViolationsAsync(discoveryOptions))
            {
                violations.Add(v);
            }

            // Assert
            Assert.Equal(2, violations.Count);
        }

        [Fact]
        public async Task Should_skip_streams_with_no_metadata()
        {
            // Arrange
            var options = new RetentionOptions();
            options.PolicyOverrides["Order"] = new RetentionPolicy
            {
                Enabled = true,
                MaxEvents = 10,
                Action = RetentionAction.FlagForReview
            };

            _policyProvider.GetRegisteredTypes().Returns(["Order"]);
            _objectIdProvider.GetObjectIdsAsync("Order", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["order-1"],
                    PageSize = 100
                });

            _metadataProvider.GetStreamMetadataAsync("Order", "order-1", Arg.Any<CancellationToken>())
                .Returns((StreamMetadata?)null);

            var sut = CreateSut(options);

            // Act
            var violations = new List<RetentionViolation>();
            await foreach (var v in sut.DiscoverViolationsAsync())
            {
                violations.Add(v);
            }

            // Assert
            Assert.Empty(violations);
        }

        [Fact]
        public async Task Should_filter_by_aggregate_types_option()
        {
            // Arrange
            var options = new RetentionOptions();
            options.PolicyOverrides["Order"] = new RetentionPolicy { Enabled = true, MaxEvents = 10 };
            options.PolicyOverrides["Customer"] = new RetentionPolicy { Enabled = true, MaxEvents = 10 };

            _policyProvider.GetRegisteredTypes().Returns(["Order", "Customer"]);
            _objectIdProvider.GetObjectIdsAsync("Order", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["order-1"],
                    PageSize = 100
                });

            _metadataProvider.GetStreamMetadataAsync("Order", "order-1", Arg.Any<CancellationToken>())
                .Returns(new StreamMetadata("Order", "order-1", 100, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

            var sut = CreateSut(options);
            var discoveryOptions = new RetentionDiscoveryOptions { AggregateTypes = ["Order"] };

            // Act
            var violations = new List<RetentionViolation>();
            await foreach (var v in sut.DiscoverViolationsAsync(discoveryOptions))
            {
                violations.Add(v);
            }

            // Assert
            Assert.Single(violations);
            Assert.Equal("Order", violations[0].ObjectName);
            await _objectIdProvider.DidNotReceive().GetObjectIdsAsync("Customer", Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_use_default_policy_when_no_override()
        {
            // Arrange
            var options = new RetentionOptions
            {
                DefaultPolicy = new RetentionPolicy { Enabled = true, MaxEvents = 50 }
            };

            _policyProvider.GetRegisteredTypes().Returns(["Order"]);
            _objectIdProvider.GetObjectIdsAsync("Order", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["order-1"],
                    PageSize = 100
                });

            _metadataProvider.GetStreamMetadataAsync("Order", "order-1", Arg.Any<CancellationToken>())
                .Returns(new StreamMetadata("Order", "order-1", 100, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

            var sut = CreateSut(options);

            // Act
            var violations = new List<RetentionViolation>();
            await foreach (var v in sut.DiscoverViolationsAsync())
            {
                violations.Add(v);
            }

            // Assert
            Assert.Single(violations);
        }

        [Fact]
        public async Task Should_respect_cancellation_token()
        {
            // Arrange
            _policyProvider.GetRegisteredTypes().Returns(["Order"]);
            var options = new RetentionOptions();
            options.PolicyOverrides["Order"] = new RetentionPolicy { Enabled = true, MaxEvents = 10 };
            var sut = CreateSut(options);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in sut.DiscoverViolationsAsync(cancellationToken: cts.Token))
                {
                }
            });
        }
    }

    public class ProcessViolationAsyncTests : RetentionDiscoveryServiceTests
    {
        [Fact]
        public async Task Should_throw_when_violation_is_null()
        {
            var sut = CreateSut();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ProcessViolationAsync(null!));
        }

        [Fact]
        public async Task Should_return_success_for_flag_for_review()
        {
            // Arrange
            var policy = new RetentionPolicy { Action = RetentionAction.FlagForReview };
            var violation = new RetentionViolation(
                "order-1", "Order", policy, 200,
                DateTimeOffset.UtcNow.AddDays(-400),
                RetentionViolationType.ExceedsMaxEvents);
            var sut = CreateSut();

            // Act
            var result = await sut.ProcessViolationAsync(violation);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(RetentionAction.FlagForReview, result.ActionTaken);
            Assert.Equal("order-1", result.StreamId);
        }

        [Fact]
        public async Task Should_return_success_for_archive()
        {
            // Arrange
            var policy = new RetentionPolicy { Action = RetentionAction.Archive };
            var violation = new RetentionViolation(
                "order-1", "Order", policy, 200,
                DateTimeOffset.UtcNow.AddDays(-400),
                RetentionViolationType.ExceedsMaxAge);
            var sut = CreateSut();

            // Act
            var result = await sut.ProcessViolationAsync(violation);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(RetentionAction.Archive, result.ActionTaken);
        }

        [Fact]
        public async Task Should_return_success_for_delete()
        {
            // Arrange
            var policy = new RetentionPolicy { Action = RetentionAction.Delete };
            var violation = new RetentionViolation(
                "order-1", "Order", policy, 200,
                DateTimeOffset.UtcNow.AddDays(-400),
                RetentionViolationType.Both);
            var sut = CreateSut();

            // Act
            var result = await sut.ProcessViolationAsync(violation);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(RetentionAction.Delete, result.ActionTaken);
        }

        [Fact]
        public async Task Should_return_success_for_migrate()
        {
            // Arrange
            var policy = new RetentionPolicy { Action = RetentionAction.Migrate };
            var violation = new RetentionViolation(
                "order-1", "Order", policy, 200,
                DateTimeOffset.UtcNow.AddDays(-400),
                RetentionViolationType.ExceedsMaxEvents);
            var sut = CreateSut();

            // Act
            var result = await sut.ProcessViolationAsync(violation);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(RetentionAction.Migrate, result.ActionTaken);
        }
    }

    public class ProcessViolationsAsyncTests : RetentionDiscoveryServiceTests
    {
        [Fact]
        public async Task Should_throw_when_violations_is_null()
        {
            var sut = CreateSut();
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in sut.ProcessViolationsAsync(null!))
                {
                }
            });
        }

        [Fact]
        public async Task Should_process_all_violations()
        {
            // Arrange
            var policy = new RetentionPolicy { Action = RetentionAction.FlagForReview };
            var violations = new[]
            {
                new RetentionViolation("order-1", "Order", policy, 200, DateTimeOffset.UtcNow, RetentionViolationType.ExceedsMaxEvents),
                new RetentionViolation("order-2", "Order", policy, 300, DateTimeOffset.UtcNow, RetentionViolationType.ExceedsMaxEvents),
            };
            var sut = CreateSut();

            // Act
            var results = new List<RetentionProcessingResult>();
            await foreach (var r in sut.ProcessViolationsAsync(violations))
            {
                results.Add(r);
            }

            // Assert
            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.True(r.Success));
        }

        [Fact]
        public async Task Should_respect_cancellation_token()
        {
            // Arrange
            var policy = new RetentionPolicy { Action = RetentionAction.FlagForReview };
            var violations = new[]
            {
                new RetentionViolation("order-1", "Order", policy, 200, DateTimeOffset.UtcNow, RetentionViolationType.ExceedsMaxEvents),
            };
            var sut = CreateSut();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in sut.ProcessViolationsAsync(violations, cts.Token))
                {
                }
            });
        }
    }
}
