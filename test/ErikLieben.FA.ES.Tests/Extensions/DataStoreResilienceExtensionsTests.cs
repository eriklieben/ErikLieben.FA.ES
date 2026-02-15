using System.Runtime.CompilerServices;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace ErikLieben.FA.ES.Tests.Extensions;

/// <summary>
/// Tests for the DataStoreResilienceExtensions methods.
/// </summary>
public class DataStoreResilienceExtensionsTests
{
    #region Test Helpers

    /// <summary>
    /// A concrete IDataStore for testing registration.
    /// </summary>
    private sealed class FakeDataStore : IDataStore, IDataStoreRecovery
    {
        public int AppendCallCount { get; private set; }
        public int ReadCallCount { get; private set; }

        public Task AppendAsync(IObjectDocument document, CancellationToken cancellationToken, params IEvent[] events)
        {
            AppendCallCount++;
            return Task.CompletedTask;
        }

        public Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events)
        {
            AppendCallCount++;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null, CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            return Task.FromResult<IEnumerable<IEvent>?>(new List<IEvent>());
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators
        public async IAsyncEnumerable<IEvent> ReadAsStreamAsync(
            IObjectDocument document,
            int startVersion = 0,
            int? untilVersion = null,
            int? chunk = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore CS1998
        {
            yield break;
        }

        public Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
        {
            return Task.FromResult(0);
        }
    }

    /// <summary>
    /// Another concrete IDataStore for testing different type registration.
    /// </summary>
    private sealed class AnotherFakeDataStore : IDataStore, IDataStoreRecovery
    {
        public Task AppendAsync(IObjectDocument document, CancellationToken cancellationToken, params IEvent[] events) => Task.CompletedTask;
        public Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events) => Task.CompletedTask;
        public Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<IEvent>?>(new List<IEvent>());
#pragma warning disable CS1998
        public async IAsyncEnumerable<IEvent> ReadAsStreamAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore CS1998
        { yield break; }
        public Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
            => Task.FromResult(0);
    }

    #endregion

    #region AddDataStoreResilience Tests

    [Fact]
    public void AddDataStoreResilience_should_return_services_when_no_datastore_registered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddDataStoreResilience();

        // Assert - should not throw and should return services for chaining
        Assert.Same(services, result);
    }

    [Fact]
    public void AddDataStoreResilience_should_wrap_type_based_registration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IDataStore, FakeDataStore>();

        // Act
        services.AddDataStoreResilience();

        // Assert
        var provider = services.BuildServiceProvider();
        var dataStore = provider.GetRequiredService<IDataStore>();
        Assert.IsType<ResilientDataStore>(dataStore);
    }

    [Fact]
    public void AddDataStoreResilience_should_wrap_factory_based_registration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IDataStore>(sp => new FakeDataStore());

        // Act
        services.AddDataStoreResilience();

        // Assert
        var provider = services.BuildServiceProvider();
        var dataStore = provider.GetRequiredService<IDataStore>();
        Assert.IsType<ResilientDataStore>(dataStore);
    }

    [Fact]
    public void AddDataStoreResilience_should_wrap_instance_based_registration()
    {
        // Arrange
        var services = new ServiceCollection();
        var instance = new FakeDataStore();
        services.AddSingleton<IDataStore>(instance);

        // Act
        services.AddDataStoreResilience();

        // Assert
        var provider = services.BuildServiceProvider();
        var dataStore = provider.GetRequiredService<IDataStore>();
        Assert.IsType<ResilientDataStore>(dataStore);
    }

    [Fact]
    public void AddDataStoreResilience_should_apply_configure_action()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IDataStore, FakeDataStore>();
        var configured = false;

        // Act
        services.AddDataStoreResilience(options =>
        {
            options.MaxRetryAttempts = 5;
            options.InitialDelay = TimeSpan.FromMilliseconds(500);
            configured = true;
        });

        // Assert
        Assert.True(configured);
        var provider = services.BuildServiceProvider();
        var dataStore = provider.GetRequiredService<IDataStore>();
        Assert.IsType<ResilientDataStore>(dataStore);
    }

    [Fact]
    public void AddDataStoreResilience_should_work_without_configure_action()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IDataStore, FakeDataStore>();

        // Act
        services.AddDataStoreResilience(configure: null);

        // Assert
        var provider = services.BuildServiceProvider();
        var dataStore = provider.GetRequiredService<IDataStore>();
        Assert.IsType<ResilientDataStore>(dataStore);
    }

    [Fact]
    public void AddDataStoreResilience_should_preserve_scoped_lifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IDataStore, FakeDataStore>();

        // Act
        services.AddDataStoreResilience();

        // Assert - verify the service descriptor for IDataStore has Scoped lifetime
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDataStore));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
    }

    [Fact]
    public void AddDataStoreResilience_should_preserve_transient_lifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IDataStore, FakeDataStore>();

        // Act
        services.AddDataStoreResilience();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDataStore));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddDataStoreResilience_should_return_services_for_chaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IDataStore, FakeDataStore>();

        // Act
        var result = services.AddDataStoreResilience();

        // Assert
        Assert.Same(services, result);
    }

    #endregion

    #region AddResilientDataStore<T> Tests

    [Fact]
    public void AddResilientDataStore_should_register_concrete_type_and_wrapper()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilientDataStore<FakeDataStore>();

        // Assert
        var provider = services.BuildServiceProvider();
        var dataStore = provider.GetRequiredService<IDataStore>();
        Assert.IsType<ResilientDataStore>(dataStore);

        // Also verify the concrete type is registered
        var concrete = provider.GetRequiredService<FakeDataStore>();
        Assert.NotNull(concrete);
    }

    [Fact]
    public void AddResilientDataStore_should_apply_configure_action()
    {
        // Arrange
        var services = new ServiceCollection();
        var configured = false;

        // Act
        services.AddResilientDataStore<FakeDataStore>(options =>
        {
            options.MaxRetryAttempts = 10;
            configured = true;
        });

        // Assert
        Assert.True(configured);
        var provider = services.BuildServiceProvider();
        var dataStore = provider.GetRequiredService<IDataStore>();
        Assert.IsType<ResilientDataStore>(dataStore);
    }

    [Fact]
    public void AddResilientDataStore_should_work_without_configure_action()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilientDataStore<FakeDataStore>(configure: null);

        // Assert
        var provider = services.BuildServiceProvider();
        var dataStore = provider.GetRequiredService<IDataStore>();
        Assert.IsType<ResilientDataStore>(dataStore);
    }

    [Fact]
    public void AddResilientDataStore_should_not_register_concrete_type_twice()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<FakeDataStore>();

        // Act
        services.AddResilientDataStore<FakeDataStore>();

        // Assert - TryAddSingleton should not add a duplicate
        var concreteDescriptors = services.Where(d => d.ServiceType == typeof(FakeDataStore)).ToList();
        Assert.Single(concreteDescriptors);
    }

    [Fact]
    public void AddResilientDataStore_should_return_services_for_chaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddResilientDataStore<FakeDataStore>();

        // Assert
        Assert.Same(services, result);
    }

    #endregion

    #region AddResilientDataStore<T> with custom pipeline Tests

    [Fact]
    public void AddResilientDataStore_with_pipeline_should_register_wrapper()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilientDataStore<FakeDataStore>(sp => ResiliencePipeline.Empty);

        // Assert
        var provider = services.BuildServiceProvider();
        var dataStore = provider.GetRequiredService<IDataStore>();
        Assert.IsType<ResilientDataStore>(dataStore);
    }

    [Fact]
    public void AddResilientDataStore_with_pipeline_should_throw_on_null_factory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddResilientDataStore<FakeDataStore>((Func<IServiceProvider, ResiliencePipeline>)null!));
    }

    [Fact]
    public void AddResilientDataStore_with_pipeline_should_use_custom_pipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        var pipelineUsed = false;

        // Act
        services.AddResilientDataStore<FakeDataStore>(sp =>
        {
            pipelineUsed = true;
            return ResiliencePipeline.Empty;
        });

        // Build to trigger factory
        var provider = services.BuildServiceProvider();
        var dataStore = provider.GetRequiredService<IDataStore>();

        // Assert
        Assert.True(pipelineUsed);
        Assert.IsType<ResilientDataStore>(dataStore);
    }

    [Fact]
    public void AddResilientDataStore_with_pipeline_should_return_services_for_chaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddResilientDataStore<FakeDataStore>(sp => ResiliencePipeline.Empty);

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddResilientDataStore_with_pipeline_should_register_concrete_type()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilientDataStore<FakeDataStore>(sp => ResiliencePipeline.Empty);

        // Assert
        var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<FakeDataStore>();
        Assert.NotNull(concrete);
    }

    #endregion

    #region DataStoreResilienceOptions Tests

    [Fact]
    public void DataStoreResilienceOptions_should_have_correct_defaults()
    {
        // Act
        var options = new DataStoreResilienceOptions();

        // Assert
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(10), options.MaxDelay);
        Assert.True(options.UseJitter);
        Assert.True(options.SkipIfClientHasRetry);
    }

    [Fact]
    public void DataStoreResilienceOptions_should_be_configurable()
    {
        // Act
        var options = new DataStoreResilienceOptions
        {
            MaxRetryAttempts = 5,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(30),
            UseJitter = false,
            SkipIfClientHasRetry = false
        };

        // Assert
        Assert.Equal(5, options.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(1), options.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), options.MaxDelay);
        Assert.False(options.UseJitter);
        Assert.False(options.SkipIfClientHasRetry);
    }

    #endregion
}
