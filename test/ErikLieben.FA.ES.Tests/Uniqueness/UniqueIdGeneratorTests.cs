using ErikLieben.FA.ES.Uniqueness;

namespace ErikLieben.FA.ES.Tests.Uniqueness;

public class UniqueIdGeneratorTests
{
    [Fact]
    public void FromUniqueValue_GeneratesDeterministicId()
    {
        var id1 = UniqueIdGenerator.FromUniqueValue("user", "test@example.com");
        var id2 = UniqueIdGenerator.FromUniqueValue("user", "test@example.com");

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void FromUniqueValue_NormalizesInput()
    {
        var id1 = UniqueIdGenerator.FromUniqueValue("user", "TEST@EXAMPLE.COM");
        var id2 = UniqueIdGenerator.FromUniqueValue("user", "test@example.com");
        var id3 = UniqueIdGenerator.FromUniqueValue("user", "  test@example.com  ");

        Assert.Equal(id1, id2);
        Assert.Equal(id2, id3);
    }

    [Fact]
    public void FromUniqueValue_DifferentValuesProduceDifferentIds()
    {
        var id1 = UniqueIdGenerator.FromUniqueValue("user", "user1@example.com");
        var id2 = UniqueIdGenerator.FromUniqueValue("user", "user2@example.com");

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void FromUniqueValue_DifferentPrefixesProduceDifferentIds()
    {
        var id1 = UniqueIdGenerator.FromUniqueValue("user", "test@example.com");
        var id2 = UniqueIdGenerator.FromUniqueValue("admin", "test@example.com");

        Assert.NotEqual(id1, id2);
        Assert.StartsWith("user-", id1);
        Assert.StartsWith("admin-", id2);
    }

    [Fact]
    public void FromUniqueValue_ReturnsCorrectFormat()
    {
        var id = UniqueIdGenerator.FromUniqueValue("user", "test@example.com");

        Assert.StartsWith("user-", id);
        Assert.Equal(5 + UniqueIdGenerator.DefaultHashLength, id.Length); // "user-" + hash
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void FromUniqueValue_RespectsHashLength(int hashLength)
    {
        var id = UniqueIdGenerator.FromUniqueValue("user", "test@example.com", hashLength);

        Assert.Equal(5 + hashLength, id.Length); // "user-" + hash
    }

    [Theory]
    [InlineData(7)]
    [InlineData(65)]
    [InlineData(-1)]
    public void FromUniqueValue_ThrowsForInvalidHashLength(int hashLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UniqueIdGenerator.FromUniqueValue("user", "test@example.com", hashLength));
    }

    [Fact]
    public void FromUniqueValue_ThrowsForNullPrefix()
    {
        Assert.Throws<ArgumentNullException>(() =>
            UniqueIdGenerator.FromUniqueValue(null!, "test@example.com"));
    }

    [Fact]
    public void FromUniqueValue_ThrowsForNullUniqueValue()
    {
        Assert.Throws<ArgumentNullException>(() =>
            UniqueIdGenerator.FromUniqueValue("user", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromUniqueValue_ThrowsForEmptyPrefix(string prefix)
    {
        Assert.Throws<ArgumentException>(() =>
            UniqueIdGenerator.FromUniqueValue(prefix, "test@example.com"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromUniqueValue_ThrowsForEmptyUniqueValue(string uniqueValue)
    {
        Assert.Throws<ArgumentException>(() =>
            UniqueIdGenerator.FromUniqueValue("user", uniqueValue));
    }

    [Fact]
    public void FromCompositeKey_GeneratesDeterministicId()
    {
        var id1 = UniqueIdGenerator.FromCompositeKey("user", ["tenant-1", "test@example.com"]);
        var id2 = UniqueIdGenerator.FromCompositeKey("user", ["tenant-1", "test@example.com"]);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void FromCompositeKey_DifferentOrderProducesDifferentIds()
    {
        var id1 = UniqueIdGenerator.FromCompositeKey("user", ["tenant-1", "test@example.com"]);
        var id2 = UniqueIdGenerator.FromCompositeKey("user", ["test@example.com", "tenant-1"]);

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ValidateId_ReturnsTrueForMatchingId()
    {
        var id = UniqueIdGenerator.FromUniqueValue("user", "test@example.com");

        Assert.True(UniqueIdGenerator.ValidateId("user", id, "test@example.com"));
    }

    [Fact]
    public void ValidateId_ReturnsFalseForNonMatchingId()
    {
        var id = UniqueIdGenerator.FromUniqueValue("user", "test@example.com");

        Assert.False(UniqueIdGenerator.ValidateId("user", id, "other@example.com"));
    }

    [Fact]
    public void ValidateId_IsCaseInsensitive()
    {
        var id = UniqueIdGenerator.FromUniqueValue("user", "test@example.com");

        Assert.True(UniqueIdGenerator.ValidateId("user", id.ToUpperInvariant(), "test@example.com"));
    }
}
