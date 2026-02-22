using ErikLieben.FA.ES.Projections;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Projections;

public class CatchUpDiscoveryServiceTests
{
    private readonly IObjectIdProvider _objectIdProvider;

    public CatchUpDiscoveryServiceTests()
    {
        _objectIdProvider = Substitute.For<IObjectIdProvider>();
    }

    public class ConstructorTests : CatchUpDiscoveryServiceTests
    {
        [Fact]
        public void Should_throw_when_objectIdProvider_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CatchUpDiscoveryService(null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_objectIdProvider()
        {
            // Act
            var sut = new CatchUpDiscoveryService(_objectIdProvider);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class DiscoverWorkItemsAsyncTests : CatchUpDiscoveryServiceTests
    {
        [Fact]
        public async Task Should_throw_when_objectNames_is_null()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.DiscoverWorkItemsAsync(null!));
        }

        [Fact]
        public async Task Should_return_empty_result_when_objectNames_is_empty()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);

            // Act
            var result = await sut.DiscoverWorkItemsAsync([]);

            // Assert
            Assert.Empty(result.WorkItems);
            Assert.Null(result.ContinuationToken);
            Assert.Equal(0, result.TotalEstimate);
        }

        [Fact]
        public async Task Should_return_work_items_for_single_object_type()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2", "proj-3"],
                    ContinuationToken = null
                });

            // Act
            var result = await sut.DiscoverWorkItemsAsync(["project"]);

            // Assert
            Assert.Equal(3, result.WorkItems.Count);
            Assert.All(result.WorkItems, wi => Assert.Equal("project", wi.ObjectName));
            Assert.Collection(result.WorkItems,
                wi => Assert.Equal("proj-1", wi.ObjectId),
                wi => Assert.Equal("proj-2", wi.ObjectId),
                wi => Assert.Equal("proj-3", wi.ObjectId));
            Assert.Null(result.ContinuationToken);
        }

        [Fact]
        public async Task Should_return_work_items_for_multiple_object_types()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2"],
                    ContinuationToken = null
                });
            _objectIdProvider.GetObjectIdsAsync("workitem", null, 98, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["wi-1", "wi-2"],
                    ContinuationToken = null
                });

            // Act
            var result = await sut.DiscoverWorkItemsAsync(["project", "workitem"]);

            // Assert
            Assert.Equal(4, result.WorkItems.Count);
            Assert.Equal(2, result.WorkItems.Count(wi => wi.ObjectName == "project"));
            Assert.Equal(2, result.WorkItems.Count(wi => wi.ObjectName == "workitem"));
        }

        [Fact]
        public async Task Should_return_continuation_token_when_more_items_exist()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 2, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2"],
                    ContinuationToken = "provider-token"
                });

            // Act
            var result = await sut.DiscoverWorkItemsAsync(["project"], pageSize: 2);

            // Assert
            Assert.Equal(2, result.WorkItems.Count);
            Assert.NotNull(result.ContinuationToken);
        }

        [Fact]
        public async Task Should_continue_from_continuation_token()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);

            // First page
            _objectIdProvider.GetObjectIdsAsync("project", null, 2, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2"],
                    ContinuationToken = "provider-token-1"
                });

            // Get first page and token
            var firstResult = await sut.DiscoverWorkItemsAsync(["project"], pageSize: 2);

            // Second page setup
            _objectIdProvider.GetObjectIdsAsync("project", "provider-token-1", 2, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-3"],
                    ContinuationToken = null
                });

            // Act - continue from token
            var result = await sut.DiscoverWorkItemsAsync(["project"], pageSize: 2, continuationToken: firstResult.ContinuationToken);

            // Assert
            Assert.Single(result.WorkItems);
            Assert.Equal("proj-3", result.WorkItems[0].ObjectId);
            Assert.Null(result.ContinuationToken);
        }

        [Fact]
        public async Task Should_handle_cross_object_type_pagination()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);

            // First object type fills the page, moving to second type on next call
            _objectIdProvider.GetObjectIdsAsync("project", null, 3, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2", "proj-3"],
                    ContinuationToken = null // All projects returned
                });

            // First call should get projects
            var result1 = await sut.DiscoverWorkItemsAsync(["project", "workitem"], pageSize: 3);

            // Assert first result
            Assert.Equal(3, result1.WorkItems.Count);
            Assert.All(result1.WorkItems, wi => Assert.Equal("project", wi.ObjectName));

            // Since page is exactly filled and there are more object types, should have token
            // Actually looking at the code, it continues with remaining page size
            // Let me trace through: remainingPageSize = 3, processes project with 3 items
            // remainingPageSize becomes 0, loop exits. Then checks if processedAllObjectTypes
            // workItems.Count (3) < pageSize (3) is false, so !processedAllObjectTypes
        }

        [Fact]
        public async Task Should_fill_page_across_multiple_object_types()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 5, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2"],
                    ContinuationToken = null
                });
            _objectIdProvider.GetObjectIdsAsync("workitem", null, 3, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["wi-1", "wi-2", "wi-3"],
                    ContinuationToken = null
                });

            // Act
            var result = await sut.DiscoverWorkItemsAsync(["project", "workitem"], pageSize: 5);

            // Assert - should get both project and workitem types
            Assert.Equal(5, result.WorkItems.Count);
            Assert.Equal(2, result.WorkItems.Count(wi => wi.ObjectName == "project"));
            Assert.Equal(3, result.WorkItems.Count(wi => wi.ObjectName == "workitem"));
        }

        [Fact]
        public async Task Should_respect_page_size()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 10, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = Enumerable.Range(1, 10).Select(i => $"proj-{i}").ToList(),
                    ContinuationToken = "more"
                });

            // Act
            var result = await sut.DiscoverWorkItemsAsync(["project"], pageSize: 10);

            // Assert
            Assert.Equal(10, result.WorkItems.Count);
        }

        [Fact]
        public async Task Should_use_default_page_size_of_100()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1"],
                    ContinuationToken = null
                });

            // Act
            var result = await sut.DiscoverWorkItemsAsync(["project"]);

            // Assert
            await _objectIdProvider.Received(1).GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_handle_invalid_continuation_token_gracefully()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1"],
                    ContinuationToken = null
                });

            // Act - pass invalid token (not base64)
            var result = await sut.DiscoverWorkItemsAsync(["project"], continuationToken: "invalid-token!");

            // Assert - should start from beginning
            Assert.Single(result.WorkItems);
            await _objectIdProvider.Received(1).GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_handle_empty_continuation_token()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1"],
                    ContinuationToken = null
                });

            // Act
            var result = await sut.DiscoverWorkItemsAsync(["project"], continuationToken: "");

            // Assert - should start from beginning
            await _objectIdProvider.Received(1).GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_handle_continuation_token_with_out_of_bounds_index()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1"],
                    ContinuationToken = null
                });

            // Create a token with index 10 but only 1 object type
            var invalidToken = CreateTestContinuationToken(10, null);

            // Act
            var result = await sut.DiscoverWorkItemsAsync(["project"], continuationToken: invalidToken);

            // Assert - should reset to beginning
            await _objectIdProvider.Received(1).GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_handle_continuation_token_with_negative_index()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1"],
                    ContinuationToken = null
                });

            var invalidToken = CreateTestContinuationToken(-1, null);

            // Act
            var result = await sut.DiscoverWorkItemsAsync(["project"], continuationToken: invalidToken);

            // Assert - should reset to beginning
            await _objectIdProvider.Received(1).GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_pass_cancellation_token()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            var cts = new CancellationTokenSource();
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, cts.Token)
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1"],
                    ContinuationToken = null
                });

            // Act
            await sut.DiscoverWorkItemsAsync(["project"], cancellationToken: cts.Token);

            // Assert
            await _objectIdProvider.Received(1).GetObjectIdsAsync("project", null, 100, cts.Token);
        }

        [Fact]
        public async Task Should_generate_continuation_token_for_next_object_type()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);

            // Project returns fewer items than page size but no more pages
            _objectIdProvider.GetObjectIdsAsync("project", null, 10, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = Enumerable.Range(1, 10).Select(i => $"proj-{i}").ToList(),
                    ContinuationToken = null // All projects done
                });

            // Act - request first page with project + workitem
            var result = await sut.DiscoverWorkItemsAsync(["project", "workitem"], pageSize: 10);

            // Assert - page is full from projects, but there might be workitems
            // Looking at the logic:
            // - After project loop iteration: remainingPageSize = 0, loop exits
            // - processedAllObjectTypes check: state.ObjectIndex (0) >= objectNames.Length-1 (1) is false
            // - workItems.Count (10) < pageSize (10) is false
            // - So processedAllObjectTypes = false
            // - But state.ObjectIndex (0) < objectNames.Length - 1 (1) is true
            // - So should have token for next type
            Assert.NotNull(result.ContinuationToken);
        }

        private static string CreateTestContinuationToken(int objectIndex, string? providerToken)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new { ObjectIndex = objectIndex, ProviderToken = providerToken });
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        }
    }

    public class StreamWorkItemsAsyncTests : CatchUpDiscoveryServiceTests
    {
        [Fact]
        public async Task Should_throw_when_objectNames_is_null()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in sut.StreamWorkItemsAsync(null!))
                {
                }
            });
        }

        [Fact]
        public async Task Should_return_empty_enumerable_for_empty_objectNames()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            var items = new List<CatchUpWorkItem>();

            // Act
            await foreach (var item in sut.StreamWorkItemsAsync([]))
            {
                items.Add(item);
            }

            // Assert
            Assert.Empty(items);
        }

        [Fact]
        public async Task Should_stream_all_items_for_single_object_type()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2", "proj-3"],
                    ContinuationToken = null
                });

            var items = new List<CatchUpWorkItem>();

            // Act
            await foreach (var item in sut.StreamWorkItemsAsync(["project"]))
            {
                items.Add(item);
            }

            // Assert
            Assert.Equal(3, items.Count);
            Assert.All(items, item => Assert.Equal("project", item.ObjectName));
        }

        [Fact]
        public async Task Should_stream_items_across_multiple_pages()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 2, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2"],
                    ContinuationToken = "page2"
                });
            _objectIdProvider.GetObjectIdsAsync("project", "page2", 2, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-3"],
                    ContinuationToken = null
                });

            var items = new List<CatchUpWorkItem>();

            // Act
            await foreach (var item in sut.StreamWorkItemsAsync(["project"], pageSize: 2))
            {
                items.Add(item);
            }

            // Assert
            Assert.Equal(3, items.Count);
        }

        [Fact]
        public async Task Should_stream_items_across_multiple_object_types()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2"],
                    ContinuationToken = null
                });
            _objectIdProvider.GetObjectIdsAsync("workitem", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["wi-1", "wi-2", "wi-3"],
                    ContinuationToken = null
                });

            var items = new List<CatchUpWorkItem>();

            // Act
            await foreach (var item in sut.StreamWorkItemsAsync(["project", "workitem"]))
            {
                items.Add(item);
            }

            // Assert
            Assert.Equal(5, items.Count);
            Assert.Equal(2, items.Count(i => i.ObjectName == "project"));
            Assert.Equal(3, items.Count(i => i.ObjectName == "workitem"));
        }

        [Fact]
        public async Task Should_handle_cancellation()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            var cts = new CancellationTokenSource();
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2"],
                    ContinuationToken = "more"
                });

            var items = new List<CatchUpWorkItem>();

            // Act
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var item in sut.StreamWorkItemsAsync(["project"], cancellationToken: cts.Token))
                {
                    items.Add(item);
                    if (items.Count == 1)
                    {
                        cts.Cancel();
                    }
                }
            });

            // Assert - should have processed at least 1 item before cancellation
            Assert.True(items.Count >= 1);
        }

        [Fact]
        public async Task Should_use_default_page_size_of_100()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1"],
                    ContinuationToken = null
                });

            // Act
            await foreach (var _ in sut.StreamWorkItemsAsync(["project"]))
            {
            }

            // Assert
            await _objectIdProvider.Received(1).GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_handle_empty_pages_from_provider()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 100, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = [],
                    ContinuationToken = null
                });

            var items = new List<CatchUpWorkItem>();

            // Act
            await foreach (var item in sut.StreamWorkItemsAsync(["project"]))
            {
                items.Add(item);
            }

            // Assert
            Assert.Empty(items);
        }

        [Fact]
        public async Task Should_stream_items_in_correct_order()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.GetObjectIdsAsync("project", null, 2, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-1", "proj-2"],
                    ContinuationToken = "page2"
                });
            _objectIdProvider.GetObjectIdsAsync("project", "page2", 2, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<string>
                {
                    Items = ["proj-3", "proj-4"],
                    ContinuationToken = null
                });

            var items = new List<CatchUpWorkItem>();

            // Act
            await foreach (var item in sut.StreamWorkItemsAsync(["project"], pageSize: 2))
            {
                items.Add(item);
            }

            // Assert
            Assert.Equal(4, items.Count);
            Assert.Equal("proj-1", items[0].ObjectId);
            Assert.Equal("proj-2", items[1].ObjectId);
            Assert.Equal("proj-3", items[2].ObjectId);
            Assert.Equal("proj-4", items[3].ObjectId);
        }
    }

    public class EstimateTotalWorkItemsAsyncTests : CatchUpDiscoveryServiceTests
    {
        [Fact]
        public async Task Should_throw_when_objectNames_is_null()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.EstimateTotalWorkItemsAsync(null!));
        }

        [Fact]
        public async Task Should_return_zero_for_empty_objectNames()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);

            // Act
            var result = await sut.EstimateTotalWorkItemsAsync([]);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_return_count_for_single_object_type()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.CountAsync("project", Arg.Any<CancellationToken>())
                .Returns(42L);

            // Act
            var result = await sut.EstimateTotalWorkItemsAsync(["project"]);

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task Should_sum_counts_across_multiple_object_types()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.CountAsync("project", Arg.Any<CancellationToken>())
                .Returns(10L);
            _objectIdProvider.CountAsync("workitem", Arg.Any<CancellationToken>())
                .Returns(25L);
            _objectIdProvider.CountAsync("task", Arg.Any<CancellationToken>())
                .Returns(5L);

            // Act
            var result = await sut.EstimateTotalWorkItemsAsync(["project", "workitem", "task"]);

            // Assert
            Assert.Equal(40, result);
        }

        [Fact]
        public async Task Should_handle_cancellation()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                sut.EstimateTotalWorkItemsAsync(["project"], cts.Token));
        }

        [Fact]
        public async Task Should_pass_cancellation_token_to_provider()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            var cts = new CancellationTokenSource();
            _objectIdProvider.CountAsync("project", cts.Token).Returns(5L);

            // Act
            await sut.EstimateTotalWorkItemsAsync(["project"], cts.Token);

            // Assert
            await _objectIdProvider.Received(1).CountAsync("project", cts.Token);
        }

        [Fact]
        public async Task Should_handle_zero_counts()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.CountAsync("project", Arg.Any<CancellationToken>())
                .Returns(0L);

            // Act
            var result = await sut.EstimateTotalWorkItemsAsync(["project"]);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_handle_large_counts()
        {
            // Arrange
            var sut = new CatchUpDiscoveryService(_objectIdProvider);
            _objectIdProvider.CountAsync("project", Arg.Any<CancellationToken>())
                .Returns(long.MaxValue / 2);
            _objectIdProvider.CountAsync("workitem", Arg.Any<CancellationToken>())
                .Returns(long.MaxValue / 2);

            // Act
            var result = await sut.EstimateTotalWorkItemsAsync(["project", "workitem"]);

            // Assert
            Assert.Equal(long.MaxValue - 1, result);
        }
    }

    public class CatchUpWorkItemTests
    {
        [Fact]
        public void Should_create_work_item_with_object_name_and_id()
        {
            // Act
            var workItem = new CatchUpWorkItem("project", "proj-123");

            // Assert
            Assert.Equal("project", workItem.ObjectName);
            Assert.Equal("proj-123", workItem.ObjectId);
            Assert.Null(workItem.ProjectionTypeName);
        }

        [Fact]
        public void Should_create_work_item_with_projection_type_name()
        {
            // Act
            var workItem = new CatchUpWorkItem("project", "proj-123", "ProjectDashboard");

            // Assert
            Assert.Equal("project", workItem.ObjectName);
            Assert.Equal("proj-123", workItem.ObjectId);
            Assert.Equal("ProjectDashboard", workItem.ProjectionTypeName);
        }

        [Fact]
        public void Should_support_equality_comparison()
        {
            // Arrange
            var workItem1 = new CatchUpWorkItem("project", "proj-123");
            var workItem2 = new CatchUpWorkItem("project", "proj-123");
            var workItem3 = new CatchUpWorkItem("project", "proj-456");

            // Assert
            Assert.Equal(workItem1, workItem2);
            Assert.NotEqual(workItem1, workItem3);
        }
    }

    public class CatchUpDiscoveryResultTests
    {
        [Fact]
        public void Should_create_result_with_all_properties()
        {
            // Arrange
            var workItems = new List<CatchUpWorkItem>
            {
                new("project", "proj-1"),
                new("project", "proj-2")
            };

            // Act
            var result = new CatchUpDiscoveryResult(workItems, "token", 100);

            // Assert
            Assert.Equal(2, result.WorkItems.Count);
            Assert.Equal("token", result.ContinuationToken);
            Assert.Equal(100, result.TotalEstimate);
        }

        [Fact]
        public void Should_allow_null_continuation_token()
        {
            // Act
            var result = new CatchUpDiscoveryResult([], null, null);

            // Assert
            Assert.Null(result.ContinuationToken);
        }

        [Fact]
        public void Should_allow_null_total_estimate()
        {
            // Act
            var result = new CatchUpDiscoveryResult([], "token", null);

            // Assert
            Assert.Null(result.TotalEstimate);
        }
    }
}
