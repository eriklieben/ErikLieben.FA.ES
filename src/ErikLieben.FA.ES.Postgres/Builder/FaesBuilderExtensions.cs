using ErikLieben.FA.ES.Builder;
using ErikLieben.FA.ES.Postgres.Configuration;
using ErikLieben.FA.ES.Postgres.HealthChecks;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ErikLieben.FA.ES.Postgres.Builder;

/// <summary>
/// Fluent <see cref="IFaesBuilder"/> extensions for the Postgres provider.
/// </summary>
public static class FaesBuilderExtensions
{
    /// <summary>Registers the Postgres provider with explicit settings.</summary>
    public static IFaesBuilder UsePostgres(this IFaesBuilder builder, EventStreamPostgresSettings settings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);
        builder.Services.ConfigureNpgsqlEventStore(settings);
        return builder;
    }

    /// <summary>Registers the Postgres provider with an inline settings configurator.</summary>
    public static IFaesBuilder UsePostgres(this IFaesBuilder builder, Action<EventStreamPostgresSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var settings = new EventStreamPostgresSettings();
        configure(settings);
        return builder.UsePostgres(settings);
    }

    /// <summary>Adds a Postgres health check tagged for FAES storage.</summary>
    public static IFaesBuilder WithPostgresHealthCheck(this IFaesBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddHealthChecks()
            .AddPostgresHealthCheck(tags: ["faes", "storage", "postgres"]);
        return builder;
    }

    /// <summary>Registers the Postgres-backed <see cref="IStreamMetadataProvider"/> used by retention services.</summary>
    public static IFaesBuilder WithPostgresStreamMetadataProvider(this IFaesBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IStreamMetadataProvider, PostgresStreamMetadataProvider>();
        return builder;
    }

    /// <summary>Registers the Postgres-backed <see cref="IProjectionStatusCoordinator"/>.</summary>
    public static IFaesBuilder WithPostgresProjectionStatusCoordinator(this IFaesBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IProjectionStatusCoordinator>(sp =>
            new PostgresProjectionStatusCoordinator(
                sp.GetRequiredService<NpgsqlDataSource>(),
                sp.GetService<ILogger<PostgresProjectionStatusCoordinator>>()));
        return builder;
    }
}
