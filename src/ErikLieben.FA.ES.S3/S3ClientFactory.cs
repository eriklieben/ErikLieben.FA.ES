using System.Collections.Concurrent;
using Amazon;
using Amazon.S3;
using ErikLieben.FA.ES.S3.Configuration;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Thread-safe factory that creates and caches named <see cref="IAmazonS3"/> clients from <see cref="EventStreamS3Settings"/>.
/// </summary>
public class S3ClientFactory : IS3ClientFactory
{
    private readonly EventStreamS3Settings _settings;
    private readonly ConcurrentDictionary<string, IAmazonS3> _clients = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ClientFactory"/> class.
    /// </summary>
    /// <param name="settings">The S3 settings used to configure client instances.</param>
    public S3ClientFactory(EventStreamS3Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    /// <inheritdoc />
    public IAmazonS3 CreateClient(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _clients.GetOrAdd(name, _ => CreateClientInternal());
    }

    private AmazonS3Client CreateClientInternal()
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = _settings.ForcePathStyle,
            RegionEndpoint = RegionEndpoint.GetBySystemName(_settings.Region)
        };

        if (!string.IsNullOrEmpty(_settings.ServiceUrl))
        {
            config.ServiceURL = _settings.ServiceUrl;
        }

        if (_settings.MaxConnectionsPerServer.HasValue)
        {
            config.MaxConnectionsPerServer = _settings.MaxConnectionsPerServer.Value;
        }

        if (!string.IsNullOrEmpty(_settings.AccessKey) && !string.IsNullOrEmpty(_settings.SecretKey))
        {
            return new AmazonS3Client(_settings.AccessKey, _settings.SecretKey, config);
        }

        // Use default credential chain (environment variables, IAM role, etc.)
        return new AmazonS3Client(config);
    }
}
