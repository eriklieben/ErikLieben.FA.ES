namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbJsonProjectionAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void Should_set_container_from_constructor()
        {
            var sut = new CosmosDbJsonProjectionAttribute("my-container");

            Assert.Equal("my-container", sut.Container);
        }

        [Fact]
        public void Should_have_default_partition_key_path()
        {
            var sut = new CosmosDbJsonProjectionAttribute("my-container");

            Assert.Equal("/projectionName", sut.PartitionKeyPath);
        }

        [Fact]
        public void Should_have_null_connection_by_default()
        {
            var sut = new CosmosDbJsonProjectionAttribute("my-container");

            Assert.Null(sut.Connection);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_allow_setting_partition_key_path()
        {
            var sut = new CosmosDbJsonProjectionAttribute("my-container")
            {
                PartitionKeyPath = "/customPath"
            };

            Assert.Equal("/customPath", sut.PartitionKeyPath);
        }

        [Fact]
        public void Should_allow_setting_connection()
        {
            var sut = new CosmosDbJsonProjectionAttribute("my-container")
            {
                Connection = "my-connection"
            };

            Assert.Equal("my-connection", sut.Connection);
        }
    }

    public class AttributeUsage
    {
        [Fact]
        public void Should_be_applicable_to_classes_only()
        {
            var attributeUsage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(CosmosDbJsonProjectionAttribute),
                typeof(AttributeUsageAttribute));

            Assert.NotNull(attributeUsage);
            Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
        }

        [Fact]
        public void Should_not_allow_multiple()
        {
            var attributeUsage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(CosmosDbJsonProjectionAttribute),
                typeof(AttributeUsageAttribute));

            Assert.NotNull(attributeUsage);
            Assert.False(attributeUsage.AllowMultiple);
        }

        [Fact]
        public void Should_not_be_inherited()
        {
            var attributeUsage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(CosmosDbJsonProjectionAttribute),
                typeof(AttributeUsageAttribute));

            Assert.NotNull(attributeUsage);
            Assert.False(attributeUsage.Inherited);
        }
    }
}
