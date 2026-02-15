using ErikLieben.FA.ES.S3.Extensions;

namespace ErikLieben.FA.ES.S3.Tests.Extensions;

public class S3ExtensionsTests
{
    public class ComputeSha256Hash
    {
        [Fact]
        public void Should_compute_hash_for_string()
        {
            var hash = S3Extensions.ComputeSha256Hash("hello");
            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
            Assert.Equal(64, hash.Length); // SHA256 = 32 bytes = 64 hex chars
        }

        [Fact]
        public void Should_return_same_hash_for_same_input()
        {
            var hash1 = S3Extensions.ComputeSha256Hash("test data");
            var hash2 = S3Extensions.ComputeSha256Hash("test data");
            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void Should_return_different_hash_for_different_input()
        {
            var hash1 = S3Extensions.ComputeSha256Hash("data1");
            var hash2 = S3Extensions.ComputeSha256Hash("data2");
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void Should_compute_hash_for_byte_array()
        {
            var data = System.Text.Encoding.UTF8.GetBytes("hello");
            var hash = S3Extensions.ComputeSha256Hash(data, 0, data.Length);
            Assert.NotNull(hash);
            Assert.Equal(64, hash.Length);
        }

        [Fact]
        public void Should_compute_consistent_hash_between_string_and_bytes()
        {
            var text = "consistent hash test";
            var hashFromString = S3Extensions.ComputeSha256Hash(text);
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            var hashFromBytes = S3Extensions.ComputeSha256Hash(bytes, 0, bytes.Length);
            Assert.Equal(hashFromString, hashFromBytes);
        }
    }
}
