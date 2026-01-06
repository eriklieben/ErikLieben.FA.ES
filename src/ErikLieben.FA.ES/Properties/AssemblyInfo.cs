using System.Runtime.CompilerServices;

// Allow internal types to be visible to implementation assemblies
[assembly: InternalsVisibleTo("ErikLieben.FA.ES.AzureStorage")]
[assembly: InternalsVisibleTo("ErikLieben.FA.ES.CosmosDb")]
[assembly: InternalsVisibleTo("ErikLieben.FA.ES.EventStreamManagement")]
[assembly: InternalsVisibleTo("ErikLieben.FA.ES.Testing")]

// Allow internal types to be visible to test assemblies
[assembly: InternalsVisibleTo("ErikLieben.FA.ES.Tests")]
[assembly: InternalsVisibleTo("ErikLieben.FA.ES.AzureStorage.Tests")]
[assembly: InternalsVisibleTo("ErikLieben.FA.ES.CosmosDb.Tests")]
[assembly: InternalsVisibleTo("ErikLieben.FA.ES.EventStreamManagement.Tests")]
[assembly: InternalsVisibleTo("ErikLieben.FA.ES.Testing.Tests")]

// Allow NSubstitute (Castle.DynamicProxy) to create proxies for internal interfaces
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
