using System.Text;
using System.Text.RegularExpressions;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

/// <summary>
/// Generates aggregate factory and System.Text.Json source generation context code for a target solution.
/// </summary>
public partial class GenerateExtensionCode
{
    private readonly SolutionDefinition solution;
    private readonly Config config;
    private readonly string solutionPath;

    /// <summary>
/// Initializes a new instance of the <see cref="GenerateExtensionCode"/> class.
/// </summary>
/// <param name="solution">The parsed solution definition describing projects, aggregates, and events.</param>
/// <param name="config">The CLI configuration influencing generation behavior.</param>
/// <param name="solutionPath">The absolute path to the solution root where files are written.</param>
public GenerateExtensionCode(SolutionDefinition solution, Config config, string solutionPath)
    {
        this.solution = solution;
        this.config = config;
        this.solutionPath = solutionPath;
    }

    /// <summary>
/// Generates extension registration classes and JSON serializer contexts for all eligible projects in the solution.
/// </summary>
/// <returns>A task that represents the asynchronous generation operation.</returns>
public async Task Generate()
    {

        foreach (var project in solution.Projects.Where(p => !p.Name.StartsWith("ErikLieben.FA.ES")))
        {
            AnsiConsole.MarkupLine($"Generating extension registration for: [violet]{project.Name}[/]");

            var currentFile = project.FileLocation;
            if (currentFile.Contains(".generated", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rel = currentFile.Replace('\\', '/');
            var relGen = rel.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                ? string.Concat(rel.AsSpan(0, rel.Length - ".csproj".Length), "Extensions.Generated.cs")
                : rel + "Extensions.Generated.cs";
            var normalized = relGen.Replace('/', System.IO.Path.DirectorySeparatorChar)
                .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            var path = System.IO.Path.Combine(solutionPath, normalized);
            AnsiConsole.MarkupLine($"Path: [blue]{path}[/]");

            try
            {
                await GenerateExtension(project, path);
            }
            catch (Exception)
            {
                // Swallowing exceptions here is intentional to continue generation for other projects; consider logging if needed.
            }

        }
    }

    private static async Task GenerateExtension(ProjectDefinition project, string path)
    {
        var projectName = ProjectNameRegex().Replace(project.Name, string.Empty);

        var registerCode = new StringBuilder();
        var mappingCode = new StringBuilder();

        var (jsonSerializerCode, jsonNamespaces) = GenerateJsonSerializers(project);
        var aggregateStorageRegistryCode = GenerateAggregateStorageRegistryCode(project);
        var projectionRegistrationCode = GenerateProjectionRegistrationCode(project);

        // Collect all aggregate-related namespaces
        var aggregateNamespaces = new HashSet<string>();

        foreach (var declaration in project.Aggregates)
        {
            if (!declaration.IsPartialClass)
            {
                continue;
            }

            // Collect aggregate namespace
            if (!string.IsNullOrWhiteSpace(declaration.Namespace))
            {
                aggregateNamespaces.Add(declaration.Namespace);
            }

            // Collect identifier namespace
            if (!string.IsNullOrWhiteSpace(declaration.IdentifierTypeNamespace))
            {
                aggregateNamespaces.Add(declaration.IdentifierTypeNamespace);
            }

            registerCode.AppendLine(
                $"serviceCollection.AddSingleton<IAggregateFactory<{declaration.IdentifierName}, {declaration.IdentifierType}>, {declaration.IdentifierName}Factory>();");
            registerCode.AppendLine(
                $"serviceCollection.AddSingleton<I{declaration.IdentifierName}Factory, {declaration.IdentifierName}Factory>();");
            registerCode.AppendLine(
                $"serviceCollection.AddScoped<I{declaration.IdentifierName}Repository, {declaration.IdentifierName}Repository>();");
            mappingCode.AppendLine(
                $"Type agg when agg == typeof({declaration.IdentifierName}) => typeof(IAggregateFactory<{declaration.IdentifierName}, {declaration.IdentifierType}>),");
        }


        foreach (var declaration in project.InheritedAggregates)
        {
            // Collect aggregate namespace
            if (!string.IsNullOrWhiteSpace(declaration.Namespace))
            {
                aggregateNamespaces.Add(declaration.Namespace);
            }

            // Collect identifier namespace
            if (!string.IsNullOrWhiteSpace(declaration.IdentifierTypeNamespace))
            {
                aggregateNamespaces.Add(declaration.IdentifierTypeNamespace);
            }

            registerCode.AppendLine(
                $"serviceCollection.AddSingleton<IAggregateFactory<{declaration.IdentifierName}, {declaration.IdentifierType}>, {declaration.IdentifierName}Factory>();");
            registerCode.AppendLine(
                $"serviceCollection.AddSingleton<I{declaration.IdentifierName}Factory, {declaration.IdentifierName}Factory>();");
            registerCode.AppendLine(
                $"serviceCollection.AddScoped<I{declaration.IdentifierName}Repository, {declaration.IdentifierName}Repository>();");
            mappingCode.AppendLine(
                $"Type agg when agg == typeof({declaration.IdentifierName}) => typeof(IAggregateFactory<{declaration.IdentifierName}, {declaration.IdentifierType}>),");
        }

        // Collect projection namespaces
        foreach (var projection in project.Projections.Where(p => p.BlobProjection != null))
        {
            if (!string.IsNullOrWhiteSpace(projection.Namespace))
            {
                aggregateNamespaces.Add(projection.Namespace);
            }
        }

        // Merge aggregate namespaces with JSON serializer namespaces
        var allNamespaces = jsonNamespaces.Union(aggregateNamespaces).Distinct().Order();

        var ss = new StringBuilder();
        foreach (var @namespace in allNamespaces.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            ss.AppendLine($"using {@namespace};");
        }

        var code = $$"""
                     using ErikLieben.FA.ES;
                     using ErikLieben.FA.ES.Aggregates;
                     using Microsoft.Extensions.DependencyInjection;
                     using System.Text.Json.Serialization;
                     using System.CodeDom.Compiler;
                     using System.Diagnostics.CodeAnalysis;
                     {{ss}}

                     namespace {{project.Namespace}};

                     // <auto-generated />
                     /// <summary>
                     /// Factory for creating aggregate instances in the {{project.Name}} project.
                     /// </summary>
                     [GeneratedCode("ErikLieben.FA.ES", "1.0.0.0")]
                     [ExcludeFromCodeCoverage]
                     public partial class {{projectName}}Factory : AggregateFactory, IAggregateFactory
                     {
                         /// <summary>
                         /// Initializes a new instance of the {{projectName}}Factory class.
                         /// </summary>
                         /// <param name="serviceProvider">Service provider for dependency injection.</param>
                         [GeneratedCode("ErikLieben.FA.ES", "1.0.0.0")]
                         [ExcludeFromCodeCoverage]
                         public {{projectName}}Factory(IServiceProvider serviceProvider) : base(serviceProvider)
                         {

                         }

                         /// <summary>
                         /// Registers all aggregate factories and repositories with the service collection.
                         /// </summary>
                         /// <param name="serviceCollection">The service collection to register services with.</param>
                         [GeneratedCode("ErikLieben.FA.ES", "1.0.0.0")]
                         [ExcludeFromCodeCoverage]
                         public static void Register(IServiceCollection serviceCollection)
                         {
                             {{registerCode}}
                             serviceCollection.AddSingleton<IAggregateFactory, {{projectName}}Factory>();
                         }

                         /// <summary>
                         /// Gets the aggregate factory type for a given aggregate type.
                         /// </summary>
                         /// <param name="type">The aggregate type to get the factory for.</param>
                         /// <returns>The factory type for the specified aggregate type.</returns>
                         [GeneratedCode("ErikLieben.FA.ES", "1.0.0.0")]
                         [ExcludeFromCodeCoverage]
                         public static Type Get(Type type)
                         {
                             return type switch
                             {
                                 {{mappingCode}}
                                 _ => null!
                             };
                         }

                         /// <summary>
                         /// Gets the aggregate factory type for a given aggregate type (internal implementation).
                         /// </summary>
                         /// <param name="type">The aggregate type to get the factory for.</param>
                         /// <returns>The factory type for the specified aggregate type.</returns>
                         [GeneratedCode("ErikLieben.FA.ES", "1.0.0.0")]
                         [ExcludeFromCodeCoverage]
                         protected override Type InternalGet(Type type)
                         {
                             return Get(type);
                         }
                     }

                     // <auto-generated />
                     /// <summary>
                     /// Extension methods for configuring {{project.Name}} aggregate factories.
                     /// </summary>
                     [GeneratedCode("ErikLieben.FA.ES", "1.0.0.0")]
                     [ExcludeFromCodeCoverage]
                     public static class {{projectName}}Extensions
                     {
                         /// <summary>
                         /// Configures the {{projectName}} aggregate factory and all related services.
                         /// </summary>
                         /// <param name="services">The service collection to configure.</param>
                         /// <returns>The service collection for chaining.</returns>
                         [GeneratedCode("ErikLieben.FA.ES", "1.0.0.0")]
                         [ExcludeFromCodeCoverage]
                         public static IServiceCollection Configure{{projectName}}Factory(this IServiceCollection services)
                         {
                             {{projectName}}Factory.Register(services);

                             // Register aggregate storage registry for cross-storage projections
                             {{aggregateStorageRegistryCode}}

                             // Register projection factories and projections
                             {{projectionRegistrationCode}}

                             return services;
                         }
                     }

                     {{jsonSerializerCode}}
                     """;

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path!)!);
        var projectDir = CodeFormattingHelper.FindProjectDirectory(path!);
        await File.WriteAllTextAsync(path!, CodeFormattingHelper.FormatCode(code.ToString(), projectDir));
    }

    private static (StringBuilder, List<string>) GenerateJsonSerializers(ProjectDefinition project)
    {
        var events = CollectDistinctEvents(project);
        var nameSpaceUsings = new List<string>();
        var jsonSerializerCode = new StringBuilder();

        foreach (var eventDefinition in events)
        {
            ProcessEventDefinition(eventDefinition, nameSpaceUsings, jsonSerializerCode);
        }

        return (jsonSerializerCode, nameSpaceUsings);
    }

    private static List<EventDefinition> CollectDistinctEvents(ProjectDefinition project)
    {
        // Use TypeName for distinction since multiple event types can share the same EventName
        // but have different schema versions (e.g., MemberJoinedProjectV1 and MemberJoinedProject
        // both using EventName "Project.MemberJoined")
        return project.Aggregates
            .SelectMany(agg => agg.Events)
            .Concat(project.Projections.SelectMany(proj => proj.Events))
            .DistinctBy(e => e.TypeName)
            .ToList();
    }

    private static void ProcessEventDefinition(
        EventDefinition eventDefinition,
        List<string> nameSpaceUsings,
        StringBuilder jsonSerializerCode)
    {
        var jsonSerializerCodeList = new List<string>();

        nameSpaceUsings.Add(eventDefinition.Namespace);
        jsonSerializerCodeList.Add($"[JsonSerializable(typeof({eventDefinition.TypeName}))]");

        ProcessEventProperties(eventDefinition, nameSpaceUsings, jsonSerializerCodeList);
        ProcessEventParameters(eventDefinition, nameSpaceUsings, jsonSerializerCodeList);

        AppendSerializerContext(eventDefinition, jsonSerializerCodeList, jsonSerializerCode);
    }

    private static void ProcessEventProperties(
        EventDefinition eventDefinition,
        List<string> nameSpaceUsings,
        List<string> jsonSerializerCodeList)
    {
        foreach (var prop in eventDefinition.Properties)
        {
            nameSpaceUsings.Add(prop.Namespace);

            if (prop.IsGeneric)
            {
                ProcessGenericProperty(prop, nameSpaceUsings, jsonSerializerCodeList);
            }
            else
            {
                jsonSerializerCodeList.Add($"[JsonSerializable(typeof({prop.Type}))]");
            }

            ProcessPropertySubTypes(prop.SubTypes, nameSpaceUsings, jsonSerializerCodeList);
        }
    }

    private static void ProcessGenericProperty(
        PropertyDefinition prop,
        List<string> nameSpaceUsings,
        List<string> jsonSerializerCodeList)
    {
        var genericSignature = BuildGenericTypeSignature(prop.GenericTypes, nameSpaceUsings);
        jsonSerializerCodeList.Add($"[JsonSerializable(typeof({prop.Type}<{genericSignature}>))]");

        // Process nested subtypes within generic types
        foreach (var generic in prop.GenericTypes)
        {
            foreach (var subType2 in generic.SubTypes)
            {
                nameSpaceUsings.Add(subType2.Namespace);
                AddSerializerForType(subType2, jsonSerializerCodeList, nameSpaceUsings);
            }
        }
    }

    private static void ProcessPropertySubTypes(
        List<PropertyGenericTypeDefinition> subTypes,
        List<string> nameSpaceUsings,
        List<string> jsonSerializerCodeList)
    {
        foreach (var subType in subTypes)
        {
            nameSpaceUsings.Add(subType.Namespace);
            AddSerializerForType(subType, jsonSerializerCodeList, nameSpaceUsings);
        }
    }

    private static void AddSerializerForType(
        PropertyGenericTypeDefinition type,
        List<string> jsonSerializerCodeList,
        List<string> nameSpaceUsings)
    {
        if (type.GenericTypes.Count != 0)
        {
            var genericSignature = BuildGenericTypeSignature(type.GenericTypes, nameSpaceUsings);
            jsonSerializerCodeList.Add($"[JsonSerializable(typeof({type.Name}<{genericSignature}>))]");
        }
        else
        {
            jsonSerializerCodeList.Add($"[JsonSerializable(typeof({type.Name}))]");
        }
    }

    private static string BuildGenericTypeSignature(
        List<PropertyGenericTypeDefinition> genericTypes,
        List<string> nameSpaceUsings)
    {
        var builder = new StringBuilder();

        for (int i = 0; i < genericTypes.Count; i++)
        {
            var generic = genericTypes[i];
            nameSpaceUsings.Add(generic.Namespace);
            builder.Append(generic.Name);

            if (i < genericTypes.Count - 1)
            {
                builder.Append(", ");
            }
        }

        return builder.ToString();
    }

    private static void ProcessEventParameters(
        EventDefinition eventDefinition,
        List<string> nameSpaceUsings,
        List<string> jsonSerializerCodeList)
    {
        foreach (var parameter in eventDefinition.Parameters)
        {
            foreach (var subType in parameter.SubTypes)
            {
                if (!nameSpaceUsings.Contains(subType.Namespace))
                {
                    nameSpaceUsings.Add(subType.Namespace);
                }

                if (parameter.Type != subType.Name)
                {
                    jsonSerializerCodeList.Add($"[JsonSerializable(typeof({subType.Name}))]");
                }
            }
        }
    }

    private static void AppendSerializerContext(
        EventDefinition eventDefinition,
        List<string> jsonSerializerCodeList,
        StringBuilder jsonSerializerCode)
    {
        var filteredSerializers = jsonSerializerCodeList
            .Distinct()
            .Where(i =>
                !i.StartsWith("[JsonSerializable(typeof(IList<") &&
                !i.StartsWith("[JsonSerializable(typeof(List<") &&
                !i.StartsWith("[JsonSerializable(typeof(Collection<"))
            .Order();

        jsonSerializerCode.Append(string.Join(Environment.NewLine, filteredSerializers));
        jsonSerializerCode.AppendLine("");
        jsonSerializerCode.AppendLine("// <auto-generated />");
        jsonSerializerCode.AppendLine($"/// <summary>");
        jsonSerializerCode.AppendLine($"/// JSON serializer context for {eventDefinition.TypeName} event type.");
        jsonSerializerCode.AppendLine($"/// </summary>");
        jsonSerializerCode.AppendLine(
            $"internal partial class {eventDefinition.TypeName}JsonSerializerContext : JsonSerializerContext {{ }}");
        jsonSerializerCode.AppendLine("");
    }


    [GeneratedRegex(@"[^a-zA-Z0-9_]")]
    private static partial Regex ProjectNameRegex();

    /// <summary>
    /// Generates code for registering the aggregate storage registry based on EventStreamBlobSettings attributes.
    /// </summary>
    /// <param name="project">The project definition containing aggregates with storage settings.</param>
    /// <returns>Generated code as a StringBuilder for registry initialization.</returns>
    private static StringBuilder GenerateAggregateStorageRegistryCode(ProjectDefinition project)
    {
        var code = new StringBuilder();
        var storageMap = new Dictionary<string, string>();

        // Collect storage settings from all aggregates
        foreach (var aggregate in project.Aggregates.Where(a => a.IsPartialClass))
        {
            if (aggregate.EventStreamBlobSettingsAttribute?.DataStore != null)
            {
                var aggregateName = aggregate.IdentifierName.ToLowerInvariant();
                storageMap[aggregateName] = aggregate.EventStreamBlobSettingsAttribute.DataStore;
            }
        }

        // Collect from inherited aggregates as well
        foreach (var aggregate in project.InheritedAggregates)
        {
            if (aggregate.EventStreamBlobSettingsAttribute?.DataStore != null)
            {
                var aggregateName = aggregate.IdentifierName.ToLowerInvariant();
                storageMap[aggregateName] = aggregate.EventStreamBlobSettingsAttribute.DataStore;
            }
        }

        // Only register if there are storage mappings in this project
        if (storageMap.Count > 0)
        {
            code.AppendLine("// Get or create aggregate storage registry");
            code.AppendLine("var existingRegistry = services.FirstOrDefault(d => d.ServiceType == typeof(ErikLieben.FA.ES.Configuration.IAggregateStorageRegistry));");
            code.AppendLine("if (existingRegistry != null)");
            code.AppendLine("{");
            code.AppendLine("    // Registry already exists - merge our mappings into the existing one");
            code.AppendLine("    var descriptor = existingRegistry;");
            code.AppendLine("    services.Remove(descriptor);");
            code.AppendLine("    var existingMap = descriptor.ImplementationInstance is ErikLieben.FA.ES.Configuration.AggregateStorageRegistry reg");
            code.AppendLine("        ? typeof(ErikLieben.FA.ES.Configuration.AggregateStorageRegistry)");
            code.AppendLine("            .GetField(\"_storageMap\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)");
            code.AppendLine("            ?.GetValue(reg) as IReadOnlyDictionary<string, string> ?? new Dictionary<string, string>()");
            code.AppendLine("        : new Dictionary<string, string>();");
            code.AppendLine("    var mergedMap = new Dictionary<string, string>(existingMap);");

            foreach (var (aggregateName, storage) in storageMap.OrderBy(kvp => kvp.Key))
            {
                code.AppendLine($"    mergedMap.TryAdd(\"{aggregateName}\", \"{storage}\");");
            }

            code.AppendLine("    services.AddSingleton<ErikLieben.FA.ES.Configuration.IAggregateStorageRegistry>(");
            code.AppendLine("        new ErikLieben.FA.ES.Configuration.AggregateStorageRegistry(mergedMap));");
            code.AppendLine("}");
            code.AppendLine("else");
            code.AppendLine("{");
            code.AppendLine("    // No existing registry - create a new one");
            code.AppendLine("    var storageMap = new Dictionary<string, string>");
            code.AppendLine("    {");
            foreach (var (aggregateName, storage) in storageMap.OrderBy(kvp => kvp.Key))
            {
                code.AppendLine($"        [\"{aggregateName}\"] = \"{storage}\",");
            }
            code.AppendLine("    };");
            code.AppendLine("    services.AddSingleton<ErikLieben.FA.ES.Configuration.IAggregateStorageRegistry>(");
            code.AppendLine("        new ErikLieben.FA.ES.Configuration.AggregateStorageRegistry(storageMap));");
            code.AppendLine("}");
        }

        return code;
    }

    /// <summary>
    /// Generates code for registering projection factories and projection singletons.
    /// </summary>
    /// <param name="project">The project definition containing projections.</param>
    /// <returns>Generated code as a StringBuilder for projection registration.</returns>
    private static StringBuilder GenerateProjectionRegistrationCode(ProjectDefinition project)
    {
        var code = new StringBuilder();
        var projectionsWithBlob = project.Projections.Where(p => p.BlobProjection != null).ToList();

        if (projectionsWithBlob.Count == 0)
        {
            return code;
        }

        code.AppendLine("// Register projection factories");
        foreach (var projection in projectionsWithBlob)
        {
            code.AppendLine($"services.AddSingleton<{projection.Namespace}.{projection.Name}Factory>();");
        }

        code.AppendLine();
        code.AppendLine("// Register projection singletons (loaded from blob storage or created new)");
        foreach (var projection in projectionsWithBlob)
        {
            code.AppendLine($"services.AddSingleton<{projection.Namespace}.{projection.Name}>(sp =>");
            code.AppendLine("{");
            code.AppendLine($"    var factory = sp.GetRequiredService<{projection.Namespace}.{projection.Name}Factory>();");
            code.AppendLine("    var docFactory = sp.GetRequiredService<IObjectDocumentFactory>();");
            code.AppendLine("    var streamFactory = sp.GetRequiredService<IEventStreamFactory>();");
            code.AppendLine();
            code.AppendLine("    try");
            code.AppendLine("    {");
            code.AppendLine("        return factory.GetOrCreateAsync(docFactory, streamFactory).GetAwaiter().GetResult();");
            code.AppendLine("    }");
            code.AppendLine("    catch");
            code.AppendLine("    {");
            code.AppendLine($"        return new {projection.Namespace}.{projection.Name}(docFactory, streamFactory);");
            code.AppendLine("    }");
            code.AppendLine("});");
        }

        return code;
    }
}
