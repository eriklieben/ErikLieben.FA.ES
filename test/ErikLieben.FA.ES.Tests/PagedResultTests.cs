using System;
using System.Collections.Generic;
using Xunit;

namespace ErikLieben.FA.ES.Tests;

public class PagedResultTests
{
    public class Constructor
    {
        [Fact]
        public void Should_initialize_with_empty_items_by_default()
        {
            // Arrange & Act
            var sut = new PagedResult<string>();

            // Assert
            Assert.NotNull(sut.Items);
            Assert.Empty(sut.Items);
            Assert.Equal(0, sut.PageSize);
            Assert.Null(sut.ContinuationToken);
            Assert.False(sut.HasNextPage);
        }

        [Fact]
        public void Should_initialize_with_provided_values()
        {
            // Arrange
            var items = new List<string> { "item1", "item2", "item3" };
            var pageSize = 10;
            var continuationToken = "token123";

            // Act
            var sut = new PagedResult<string>
            {
                Items = items,
                PageSize = pageSize,
                ContinuationToken = continuationToken
            };

            // Assert
            Assert.Equal(items, sut.Items);
            Assert.Equal(pageSize, sut.PageSize);
            Assert.Equal(continuationToken, sut.ContinuationToken);
            Assert.True(sut.HasNextPage);
        }
    }

    public class HasNextPageProperty
    {
        [Fact]
        public void Should_return_true_when_continuation_token_exists()
        {
            // Arrange
            var sut = new PagedResult<int>
            {
                Items = new List<int> { 1, 2, 3 },
                ContinuationToken = "some-token"
            };

            // Act & Assert
            Assert.True(sut.HasNextPage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_return_false_when_continuation_token_is_null_or_empty(string? token)
        {
            // Arrange
            var sut = new PagedResult<int>
            {
                Items = new List<int> { 1, 2, 3 },
                ContinuationToken = token
            };

            // Act & Assert
            Assert.False(sut.HasNextPage);
        }
    }

    public class ItemsProperty
    {
        [Fact]
        public void Should_maintain_readonly_list()
        {
            // Arrange
            var items = new List<string> { "a", "b", "c" };
            var sut = new PagedResult<string>
            {
                Items = items
            };

            // Act
            var retrievedItems = sut.Items;

            // Assert
            Assert.IsAssignableFrom<IReadOnlyList<string>>(retrievedItems);
            Assert.Equal(items.Count, retrievedItems.Count);
            Assert.Equal(items[0], retrievedItems[0]);
        }

        [Fact]
        public void Should_allow_different_types()
        {
            // Arrange & Act
            var stringResult = new PagedResult<string> { Items = new List<string> { "a" } };
            var intResult = new PagedResult<int> { Items = new List<int> { 1 } };
            var guidResult = new PagedResult<Guid> { Items = new List<Guid> { Guid.NewGuid() } };

            // Assert
            Assert.NotNull(stringResult.Items);
            Assert.NotNull(intResult.Items);
            Assert.NotNull(guidResult.Items);
        }
    }
}
