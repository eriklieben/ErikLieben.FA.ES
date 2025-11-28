using System;
using System.Collections.Generic;
using ErikLieben.FA.ES.Configuration;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Configuration;

public class AggregateStorageRegistryTests
{
    public class Ctor
    {
        [Fact]
        public void Should_initialize_correctly_with_valid_parameters()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>
            {
                ["userprofile"] = "UserDataStore",
                ["project"] = "Store"
            };

            // Act
            var sut = new AggregateStorageRegistry(storageMap);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_throw_when_storageMap_is_null()
        {
            // Arrange
            IReadOnlyDictionary<string, string>? storageMap = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AggregateStorageRegistry(storageMap!));
        }
    }

    public class GetStorageForAggregate
    {
        [Fact]
        public void Should_return_storage_name_when_mapping_exists()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>
            {
                ["userprofile"] = "UserDataStore",
                ["project"] = "Store"
            };
            var sut = new AggregateStorageRegistry(storageMap);

            // Act
            var result = sut.GetStorageForAggregate("userprofile");

            // Assert
            Assert.Equal("UserDataStore", result);
        }

        [Fact]
        public void Should_return_null_when_mapping_does_not_exist()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>
            {
                ["userprofile"] = "UserDataStore"
            };
            var sut = new AggregateStorageRegistry(storageMap);

            // Act
            var result = sut.GetStorageForAggregate("project");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Should_be_case_insensitive()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>
            {
                ["userprofile"] = "UserDataStore"
            };
            var sut = new AggregateStorageRegistry(storageMap);

            // Act
            var result1 = sut.GetStorageForAggregate("UserProfile");
            var result2 = sut.GetStorageForAggregate("USERPROFILE");
            var result3 = sut.GetStorageForAggregate("userprofile");

            // Assert
            Assert.Equal("UserDataStore", result1);
            Assert.Equal("UserDataStore", result2);
            Assert.Equal("UserDataStore", result3);
        }

        [Fact]
        public void Should_throw_when_aggregateName_is_null()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>();
            var sut = new AggregateStorageRegistry(storageMap);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.GetStorageForAggregate(null!));
        }

        [Fact]
        public void Should_throw_when_aggregateName_is_empty()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>();
            var sut = new AggregateStorageRegistry(storageMap);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => sut.GetStorageForAggregate(""));
        }

        [Fact]
        public void Should_throw_when_aggregateName_is_whitespace()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>();
            var sut = new AggregateStorageRegistry(storageMap);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => sut.GetStorageForAggregate("   "));
        }
    }

    public class HasMapping
    {
        [Fact]
        public void Should_return_true_when_mapping_exists()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>
            {
                ["userprofile"] = "UserDataStore"
            };
            var sut = new AggregateStorageRegistry(storageMap);

            // Act
            var result = sut.HasMapping("userprofile");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_when_mapping_does_not_exist()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>
            {
                ["userprofile"] = "UserDataStore"
            };
            var sut = new AggregateStorageRegistry(storageMap);

            // Act
            var result = sut.HasMapping("project");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Should_be_case_insensitive()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>
            {
                ["userprofile"] = "UserDataStore"
            };
            var sut = new AggregateStorageRegistry(storageMap);

            // Act
            var result1 = sut.HasMapping("UserProfile");
            var result2 = sut.HasMapping("USERPROFILE");
            var result3 = sut.HasMapping("userprofile");

            // Assert
            Assert.True(result1);
            Assert.True(result2);
            Assert.True(result3);
        }

        [Fact]
        public void Should_throw_when_aggregateName_is_null()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>();
            var sut = new AggregateStorageRegistry(storageMap);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.HasMapping(null!));
        }

        [Fact]
        public void Should_throw_when_aggregateName_is_empty()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>();
            var sut = new AggregateStorageRegistry(storageMap);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => sut.HasMapping(""));
        }

        [Fact]
        public void Should_throw_when_aggregateName_is_whitespace()
        {
            // Arrange
            var storageMap = new Dictionary<string, string>();
            var sut = new AggregateStorageRegistry(storageMap);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => sut.HasMapping("   "));
        }
    }
}
