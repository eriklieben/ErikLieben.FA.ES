using ErikLieben.FA.ES.S3.Model;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.S3.Tests.Model;

public class S3VersionIndexDocumentTests
{
    [Fact]
    public void Should_have_default_schema_version()
    {
        var doc = new S3VersionIndexDocument();
        Assert.Equal("1.0.0", doc.SchemaVersion);
    }

    [Fact]
    public void Should_have_empty_version_index_by_default()
    {
        var doc = new S3VersionIndexDocument();
        Assert.NotNull(doc.VersionIndex);
        Assert.Empty(doc.VersionIndex);
    }

    [Fact]
    public void Should_roundtrip_via_json()
    {
        var versionIndex = new Dictionary<ObjectIdentifier, VersionIdentifier>
        {
            [new ObjectIdentifier("project__obj1")] = new VersionIdentifier("v__1"),
            [new ObjectIdentifier("project__obj2")] = new VersionIdentifier("v__2"),
        };

        var json = S3VersionIndexDocument.ToJson(versionIndex);
        Assert.NotNull(json);
        Assert.Contains("obj1", json);

        var deserialized = S3VersionIndexDocument.FromJson(json);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.VersionIndex.Count);
    }

    [Fact]
    public void FromJson_should_throw_when_json_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => S3VersionIndexDocument.FromJson(null!));
    }

    [Fact]
    public void FromJson_should_throw_when_json_is_empty()
    {
        Assert.Throws<ArgumentException>(() => S3VersionIndexDocument.FromJson(string.Empty));
    }

    [Fact]
    public void ToJson_should_throw_when_version_index_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => S3VersionIndexDocument.ToJson(null!));
    }

    [Fact]
    public void ToJson_should_serialize_empty_index()
    {
        var json = S3VersionIndexDocument.ToJson(new Dictionary<ObjectIdentifier, VersionIdentifier>());
        Assert.NotNull(json);
        Assert.Contains("versionIndex", json);
    }
}
