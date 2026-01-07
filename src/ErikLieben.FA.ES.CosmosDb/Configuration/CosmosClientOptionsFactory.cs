using ErikLieben.FA.ES.CosmosDb.Serialization;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb.Configuration;

/// <summary>
/// Factory for creating consistently configured <see cref="CosmosClientOptions"/> instances.
/// Ensures all consumers use the same AOT-compatible System.Text.Json serializer.
/// </summary>
public static class CosmosClientOptionsFactory
{
    /// <summary>
    /// Creates default <see cref="CosmosClientOptions"/> for production use.
    /// Uses Direct mode for optimal performance.
    /// </summary>
    /// <returns>Configured CosmosClientOptions with AOT-compatible serializer.</returns>
    public static CosmosClientOptions CreateDefault()
    {
        return new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Direct,
            Serializer = new CosmosDbSystemTextJsonSerializer()
        };
    }

    /// <summary>
    /// Creates <see cref="CosmosClientOptions"/> for development/emulator use.
    /// Uses Gateway mode for compatibility with emulators and local development.
    /// </summary>
    /// <returns>Configured CosmosClientOptions with AOT-compatible serializer.</returns>
    public static CosmosClientOptions CreateForDevelopment()
    {
        return new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            Serializer = new CosmosDbSystemTextJsonSerializer()
        };
    }

    /// <summary>
    /// Creates <see cref="CosmosClientOptions"/> for development with a custom HTTP client factory.
    /// Useful for integration tests with CosmosDB emulator containers.
    /// </summary>
    /// <param name="httpClientFactory">Factory function that creates the HTTP client.</param>
    /// <returns>Configured CosmosClientOptions with AOT-compatible serializer.</returns>
    public static CosmosClientOptions CreateForDevelopment(Func<HttpClient> httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        return new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            HttpClientFactory = httpClientFactory,
            Serializer = new CosmosDbSystemTextJsonSerializer()
        };
    }

    /// <summary>
    /// Applies the AOT-compatible System.Text.Json serializer to existing options.
    /// Use this when you need to configure additional options but still want the correct serializer.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The same options instance for fluent chaining.</returns>
    public static CosmosClientOptions WithAotSerializer(this CosmosClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Serializer = new CosmosDbSystemTextJsonSerializer();
        return options;
    }
}
