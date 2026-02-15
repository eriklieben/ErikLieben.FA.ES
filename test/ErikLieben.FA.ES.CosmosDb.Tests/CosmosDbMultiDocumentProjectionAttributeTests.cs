namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbMultiDocumentProjectionAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void Should_set_container_from_constructor()
        {
            var sut = new CosmosDbMultiDocumentProjectionAttribute("audit-log");

            Assert.Equal("audit-log", sut.ContainerName);
        }

        [Fact]
        public void Should_have_null_connection_by_default()
        {
            var sut = new CosmosDbMultiDocumentProjectionAttribute("audit-log");

            Assert.Null(sut.Connection);
        }

        [Fact]
        public void Should_have_default_partition_key_path()
        {
            var sut = new CosmosDbMultiDocumentProjectionAttribute("audit-log");

            Assert.Equal("/partitionKey", sut.PartitionKeyPath);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_allow_setting_connection()
        {
            var sut = new CosmosDbMultiDocumentProjectionAttribute("audit-log")
            {
                Connection = "my-connection"
            };

            Assert.Equal("my-connection", sut.Connection);
        }

        [Fact]
        public void Should_allow_setting_partition_key_path()
        {
            var sut = new CosmosDbMultiDocumentProjectionAttribute("audit-log")
            {
                PartitionKeyPath = "/customPartition"
            };

            Assert.Equal("/customPartition", sut.PartitionKeyPath);
        }

        [Fact]
        public void Should_allow_setting_auto_create_container()
        {
            var sut = new CosmosDbMultiDocumentProjectionAttribute("audit-log")
            {
                AutoCreateContainer = false
            };

            Assert.False(sut.AutoCreateContainer);
        }

        [Fact]
        public void Should_have_auto_create_container_default_to_true()
        {
            var sut = new CosmosDbMultiDocumentProjectionAttribute("audit-log");

            Assert.True(sut.AutoCreateContainer);
        }
    }

    public class AttributeUsage
    {
        [Fact]
        public void Should_be_applicable_to_classes_only()
        {
            var attributeUsage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(CosmosDbMultiDocumentProjectionAttribute),
                typeof(AttributeUsageAttribute));

            Assert.NotNull(attributeUsage);
            Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
        }

        [Fact]
        public void Should_not_allow_multiple()
        {
            var attributeUsage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(CosmosDbMultiDocumentProjectionAttribute),
                typeof(AttributeUsageAttribute));

            Assert.NotNull(attributeUsage);
            Assert.False(attributeUsage.AllowMultiple);
        }

        [Fact]
        public void Should_not_be_inherited()
        {
            var attributeUsage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(CosmosDbMultiDocumentProjectionAttribute),
                typeof(AttributeUsageAttribute));

            Assert.NotNull(attributeUsage);
            Assert.False(attributeUsage.Inherited);
        }
    }
}
