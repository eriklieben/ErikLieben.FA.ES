using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Model;

namespace ErikLieben.FA.ES.CLI.Analysis;

/// <summary>
/// Detects changes between solution definitions for detailed activity reporting.
/// </summary>
public class ChangeDetector : IChangeDetector
{
    public IReadOnlyList<DetectedChange> DetectChanges(
        SolutionDefinition? previous,
        SolutionDefinition current)
    {
        var changes = new List<DetectedChange>();

        if (previous == null)
        {
            // Initial analysis - report summary of what was found
            foreach (var project in current.Projects)
            {
                foreach (var aggregate in project.Aggregates)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Added,
                        ChangeCategory.Aggregate,
                        "Aggregate",
                        aggregate.IdentifierName,
                        $"Found aggregate {aggregate.IdentifierName}",
                        $"{aggregate.Events.Count} events, {aggregate.Properties.Count} properties"));
                }

                foreach (var projection in project.Projections)
                {
                    var projType = projection is RoutedProjectionDefinition rp && rp.IsRoutedProjection
                        ? "routed projection"
                        : "projection";
                    changes.Add(new DetectedChange(
                        ChangeType.Added,
                        ChangeCategory.Projection,
                        "Projection",
                        projection.Name,
                        $"Found {projType} {projection.Name}",
                        $"{projection.Events.Count} events"));
                }

                foreach (var inherited in project.InheritedAggregates)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Added,
                        ChangeCategory.InheritedAggregate,
                        "InheritedAggregate",
                        inherited.IdentifierName,
                        $"Found inherited aggregate {inherited.IdentifierName}"));
                }

                foreach (var versionToken in project.VersionTokens)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Added,
                        ChangeCategory.VersionToken,
                        "VersionToken",
                        versionToken.Name,
                        $"Found version token {versionToken.Name}",
                        $"for type {versionToken.GenericType}"));
                }
            }

            return changes;
        }

        // Compare aggregates
        var prevAggregates = previous.Projects
            .SelectMany(p => p.Aggregates)
            .ToDictionary(a => a.IdentifierName);
        var currAggregates = current.Projects
            .SelectMany(p => p.Aggregates)
            .ToDictionary(a => a.IdentifierName);

        DetectAggregateChanges(prevAggregates, currAggregates, changes);

        // Compare projections
        var prevProjections = previous.Projects
            .SelectMany(p => p.Projections)
            .ToDictionary(p => p.Name);
        var currProjections = current.Projects
            .SelectMany(p => p.Projections)
            .ToDictionary(p => p.Name);

        DetectProjectionChanges(prevProjections, currProjections, changes);

        // Compare inherited aggregates
        var prevInherited = previous.Projects
            .SelectMany(p => p.InheritedAggregates)
            .ToDictionary(i => i.IdentifierName);
        var currInherited = current.Projects
            .SelectMany(p => p.InheritedAggregates)
            .ToDictionary(i => i.IdentifierName);

        DetectInheritedAggregateChanges(prevInherited, currInherited, changes);

        // Compare version tokens
        var prevVersionTokens = previous.Projects
            .SelectMany(p => p.VersionTokens)
            .ToDictionary(v => v.Name);
        var currVersionTokens = current.Projects
            .SelectMany(p => p.VersionTokens)
            .ToDictionary(v => v.Name);

        DetectVersionTokenChanges(prevVersionTokens, currVersionTokens, changes);

        return changes;
    }

    private static void DetectAggregateChanges(
        Dictionary<string, AggregateDefinition> previous,
        Dictionary<string, AggregateDefinition> current,
        List<DetectedChange> changes)
    {
        // Find added aggregates
        foreach (var (name, aggregate) in current)
        {
            if (!previous.ContainsKey(name))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Added,
                    ChangeCategory.Aggregate,
                    "Aggregate",
                    name,
                    $"Added aggregate {name}",
                    $"{aggregate.Events.Count} events, {aggregate.Properties.Count} properties"));
            }
        }

        // Find removed aggregates
        foreach (var (name, _) in previous)
        {
            if (!current.ContainsKey(name))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Removed,
                    ChangeCategory.Aggregate,
                    "Aggregate",
                    name,
                    $"Removed aggregate {name}"));
            }
        }

        // Find modified aggregates
        foreach (var (name, currAggregate) in current)
        {
            if (previous.TryGetValue(name, out var prevAggregate))
            {
                DetectAggregateDetailChanges(prevAggregate, currAggregate, changes);
            }
        }
    }

    private static void DetectAggregateDetailChanges(
        AggregateDefinition previous,
        AggregateDefinition current,
        List<DetectedChange> changes)
    {
        var aggregateName = current.IdentifierName;

        // Detect event changes
        DetectEventChanges(previous.Events, current.Events, "Aggregate", aggregateName, changes);

        // Detect property changes
        DetectPropertyChanges(previous.Properties, current.Properties, "Aggregate", aggregateName, changes);

        // Detect command changes
        DetectCommandChanges(previous.Commands, current.Commands, aggregateName, changes);

        // Detect constructor changes
        DetectConstructorChanges(previous.Constructors, current.Constructors, "Aggregate", aggregateName, changes);

        // Detect PostWhen changes
        DetectPostWhenChanges(previous.PostWhen, current.PostWhen, "Aggregate", aggregateName, changes);

        // Detect StreamAction changes
        DetectStreamActionChanges(previous.StreamActions, current.StreamActions, aggregateName, changes);

        // Detect attribute changes
        DetectEventStreamTypeChanges(previous.EventStreamTypeAttribute, current.EventStreamTypeAttribute, aggregateName, changes);
        DetectBlobSettingsChanges(previous.EventStreamBlobSettingsAttribute, current.EventStreamBlobSettingsAttribute, aggregateName, changes);

        // Detect partial class status changes
        if (previous.IsPartialClass != current.IsPartialClass)
        {
            changes.Add(new DetectedChange(
                ChangeType.Modified,
                ChangeCategory.Aggregate,
                "Aggregate",
                aggregateName,
                current.IsPartialClass
                    ? $"{aggregateName} is now partial"
                    : $"{aggregateName} is no longer partial"));
        }

        // Detect factory/repository partial changes
        if (previous.HasUserDefinedFactoryPartial != current.HasUserDefinedFactoryPartial)
        {
            changes.Add(new DetectedChange(
                ChangeType.Modified,
                ChangeCategory.Aggregate,
                "Aggregate",
                aggregateName,
                current.HasUserDefinedFactoryPartial
                    ? $"Custom factory partial added to {aggregateName}"
                    : $"Custom factory partial removed from {aggregateName}"));
        }

        if (previous.HasUserDefinedRepositoryPartial != current.HasUserDefinedRepositoryPartial)
        {
            changes.Add(new DetectedChange(
                ChangeType.Modified,
                ChangeCategory.Aggregate,
                "Aggregate",
                aggregateName,
                current.HasUserDefinedRepositoryPartial
                    ? $"Custom repository partial added to {aggregateName}"
                    : $"Custom repository partial removed from {aggregateName}"));
        }
    }

    private static void DetectEventChanges(
        List<EventDefinition> previous,
        List<EventDefinition> current,
        string entityType,
        string entityName,
        List<DetectedChange> changes)
    {
        var prevEvents = previous.ToDictionary(e => e.EventName);
        var currEvents = current.ToDictionary(e => e.EventName);

        foreach (var (eventName, eventDef) in currEvents)
        {
            if (!prevEvents.ContainsKey(eventName))
            {
                var activationType = eventDef.ActivationType == "Command" ? "command" : "When handler";
                changes.Add(new DetectedChange(
                    ChangeType.Added,
                    ChangeCategory.Event,
                    entityType,
                    entityName,
                    $"Added {eventDef.TypeName} event to {entityName}",
                    $"via {activationType}"));
            }
        }

        foreach (var (eventName, eventDef) in prevEvents)
        {
            if (!currEvents.ContainsKey(eventName))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Removed,
                    ChangeCategory.Event,
                    entityType,
                    entityName,
                    $"Removed {eventDef.TypeName} event from {entityName}"));
            }
        }

        // Detect When method changes (by comparing activation types and parameters)
        foreach (var (eventName, currEvent) in currEvents)
        {
            if (prevEvents.TryGetValue(eventName, out var prevEvent))
            {
                if (prevEvent.ActivationType != currEvent.ActivationType)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.WhenMethod,
                        entityType,
                        entityName,
                        $"Changed {currEvent.TypeName} activation in {entityName}",
                        $"{prevEvent.ActivationType} → {currEvent.ActivationType}"));
                }

                if (prevEvent.Parameters.Count != currEvent.Parameters.Count)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.WhenMethod,
                        entityType,
                        entityName,
                        $"Changed {currEvent.ActivationType} parameters for {currEvent.TypeName}",
                        $"{prevEvent.Parameters.Count} → {currEvent.Parameters.Count} parameters"));
                }

                if (prevEvent.ActivationAwaitRequired != currEvent.ActivationAwaitRequired)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.WhenMethod,
                        entityType,
                        entityName,
                        currEvent.ActivationAwaitRequired
                            ? $"{currEvent.ActivationType} for {currEvent.TypeName} now async"
                            : $"{currEvent.ActivationType} for {currEvent.TypeName} no longer async"));
                }
            }
        }
    }

    private static void DetectPropertyChanges(
        List<PropertyDefinition> previous,
        List<PropertyDefinition> current,
        string entityType,
        string entityName,
        List<DetectedChange> changes)
    {
        var prevProps = previous.ToDictionary(p => p.Name);
        var currProps = current.ToDictionary(p => p.Name);

        foreach (var (propName, propDef) in currProps)
        {
            if (!prevProps.ContainsKey(propName))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Added,
                    ChangeCategory.Property,
                    entityType,
                    entityName,
                    $"Added property {propName} to {entityName}",
                    $"type: {propDef.Type}{(propDef.IsNullable ? "?" : "")}"));
            }
            else if (prevProps.TryGetValue(propName, out var prevProp))
            {
                if (prevProp.Type != propDef.Type || prevProp.IsNullable != propDef.IsNullable)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.Property,
                        entityType,
                        entityName,
                        $"Modified property {propName} in {entityName}",
                        $"{prevProp.Type}{(prevProp.IsNullable ? "?" : "")} → {propDef.Type}{(propDef.IsNullable ? "?" : "")}"));
                }

                if (prevProp.IsGeneric != propDef.IsGeneric ||
                    !prevProp.GenericTypes.Select(g => g.Name).SequenceEqual(propDef.GenericTypes.Select(g => g.Name)))
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.Property,
                        entityType,
                        entityName,
                        $"Changed generic types for {propName} in {entityName}"));
                }
            }
        }

        foreach (var (propName, _) in prevProps)
        {
            if (!currProps.ContainsKey(propName))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Removed,
                    ChangeCategory.Property,
                    entityType,
                    entityName,
                    $"Removed property {propName} from {entityName}"));
            }
        }
    }

    private static void DetectCommandChanges(
        List<CommandDefinition> previous,
        List<CommandDefinition> current,
        string aggregateName,
        List<DetectedChange> changes)
    {
        var prevCommands = previous.ToDictionary(c => c.CommandName);
        var currCommands = current.ToDictionary(c => c.CommandName);

        foreach (var (cmdName, cmdDef) in currCommands)
        {
            if (!prevCommands.ContainsKey(cmdName))
            {
                var eventList = cmdDef.ProducesEvents.Count > 0
                    ? string.Join(", ", cmdDef.ProducesEvents.Select(e => e.TypeName))
                    : "no events";
                changes.Add(new DetectedChange(
                    ChangeType.Added,
                    ChangeCategory.Command,
                    "Aggregate",
                    aggregateName,
                    $"Added command {cmdName} to {aggregateName}",
                    $"produces: {eventList}"));
            }
            else if (prevCommands.TryGetValue(cmdName, out var prevCmd))
            {
                // Check parameter changes
                if (prevCmd.Parameters.Count != cmdDef.Parameters.Count)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.Command,
                        "Aggregate",
                        aggregateName,
                        $"Changed parameters for command {cmdName}",
                        $"{prevCmd.Parameters.Count} → {cmdDef.Parameters.Count} parameters"));
                }

                // Check return type changes
                if (prevCmd.ReturnType.Type != cmdDef.ReturnType.Type)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.Command,
                        "Aggregate",
                        aggregateName,
                        $"Changed return type for command {cmdName}",
                        $"{prevCmd.ReturnType.Type} → {cmdDef.ReturnType.Type}"));
                }

                // Check async changes
                if (prevCmd.RequiresAwait != cmdDef.RequiresAwait)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.Command,
                        "Aggregate",
                        aggregateName,
                        cmdDef.RequiresAwait
                            ? $"Command {cmdName} is now async"
                            : $"Command {cmdName} is no longer async"));
                }

                // Check produced events changes
                var prevEventNames = prevCmd.ProducesEvents.Select(e => e.TypeName).OrderBy(x => x).ToList();
                var currEventNames = cmdDef.ProducesEvents.Select(e => e.TypeName).OrderBy(x => x).ToList();
                if (!prevEventNames.SequenceEqual(currEventNames))
                {
                    var added = currEventNames.Except(prevEventNames).ToList();
                    var removed = prevEventNames.Except(currEventNames).ToList();

                    foreach (var evt in added)
                    {
                        changes.Add(new DetectedChange(
                            ChangeType.Added,
                            ChangeCategory.Command,
                            "Aggregate",
                            aggregateName,
                            $"Command {cmdName} now produces {evt}"));
                    }

                    foreach (var evt in removed)
                    {
                        changes.Add(new DetectedChange(
                            ChangeType.Removed,
                            ChangeCategory.Command,
                            "Aggregate",
                            aggregateName,
                            $"Command {cmdName} no longer produces {evt}"));
                    }
                }
            }
        }

        foreach (var (cmdName, _) in prevCommands)
        {
            if (!currCommands.ContainsKey(cmdName))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Removed,
                    ChangeCategory.Command,
                    "Aggregate",
                    aggregateName,
                    $"Removed command {cmdName} from {aggregateName}"));
            }
        }
    }

    private static void DetectConstructorChanges(
        List<ConstructorDefinition> previous,
        List<ConstructorDefinition> current,
        string entityType,
        string entityName,
        List<DetectedChange> changes)
    {
        // Compare by parameter count and types
        var prevCtorSignatures = previous.Select(c => string.Join(",", c.Parameters.Select(p => p.Type))).ToHashSet();
        var currCtorSignatures = current.Select(c => string.Join(",", c.Parameters.Select(p => p.Type))).ToHashSet();

        var added = currCtorSignatures.Except(prevCtorSignatures).ToList();
        var removed = prevCtorSignatures.Except(currCtorSignatures).ToList();

        foreach (var sig in added)
        {
            var paramCount = string.IsNullOrEmpty(sig) ? 0 : sig.Split(',').Length;
            changes.Add(new DetectedChange(
                ChangeType.Added,
                ChangeCategory.Constructor,
                entityType,
                entityName,
                $"Added constructor to {entityName}",
                $"{paramCount} parameters"));
        }

        foreach (var sig in removed)
        {
            var paramCount = string.IsNullOrEmpty(sig) ? 0 : sig.Split(',').Length;
            changes.Add(new DetectedChange(
                ChangeType.Removed,
                ChangeCategory.Constructor,
                entityType,
                entityName,
                $"Removed constructor from {entityName}",
                $"{paramCount} parameters"));
        }
    }

    private static void DetectPostWhenChanges(
        PostWhenDeclaration? previous,
        PostWhenDeclaration? current,
        string entityType,
        string entityName,
        List<DetectedChange> changes)
    {
        if (previous == null && current != null)
        {
            changes.Add(new DetectedChange(
                ChangeType.Added,
                ChangeCategory.PostWhen,
                entityType,
                entityName,
                $"Added PostWhen handler to {entityName}",
                $"{current.Parameters.Count} parameters"));
        }
        else if (previous != null && current == null)
        {
            changes.Add(new DetectedChange(
                ChangeType.Removed,
                ChangeCategory.PostWhen,
                entityType,
                entityName,
                $"Removed PostWhen handler from {entityName}"));
        }
        else if (previous != null) // current is also not null at this point
        {
            if (previous.Parameters.Count != current!.Parameters.Count)
            {
                changes.Add(new DetectedChange(
                    ChangeType.Modified,
                    ChangeCategory.PostWhen,
                    entityType,
                    entityName,
                    $"Changed PostWhen parameters in {entityName}",
                    $"{previous.Parameters.Count} → {current.Parameters.Count} parameters"));
            }
        }
    }

    private static void DetectStreamActionChanges(
        List<StreamActionDefinition> previous,
        List<StreamActionDefinition> current,
        string aggregateName,
        List<DetectedChange> changes)
    {
        var prevActions = previous.ToDictionary(a => a.Type);
        var currActions = current.ToDictionary(a => a.Type);

        foreach (var (actionType, actionDef) in currActions)
        {
            if (!prevActions.ContainsKey(actionType))
            {
                var interfaces = string.Join(", ", actionDef.StreamActionInterfaces);
                var regType = actionDef.RegistrationType == "Manual" ? " (manual)" : "";
                changes.Add(new DetectedChange(
                    ChangeType.Added,
                    ChangeCategory.StreamAction,
                    "Aggregate",
                    aggregateName,
                    $"Added stream action {actionType}{regType}",
                    $"implements: {interfaces}"));
            }
            else if (prevActions.TryGetValue(actionType, out var prevAction))
            {
                var prevInterfaces = prevAction.StreamActionInterfaces.OrderBy(x => x).ToList();
                var currInterfaces = actionDef.StreamActionInterfaces.OrderBy(x => x).ToList();

                if (!prevInterfaces.SequenceEqual(currInterfaces))
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.StreamAction,
                        "Aggregate",
                        aggregateName,
                        $"Changed interfaces for stream action {actionType}"));
                }

                // Detect registration type changes
                if (prevAction.RegistrationType != actionDef.RegistrationType)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.StreamAction,
                        "Aggregate",
                        aggregateName,
                        $"Changed registration type for {actionType}",
                        $"{prevAction.RegistrationType} → {actionDef.RegistrationType}"));
                }
            }
        }

        foreach (var (actionType, actionDef) in prevActions)
        {
            if (!currActions.ContainsKey(actionType))
            {
                var regType = actionDef.RegistrationType == "Manual" ? " (manual)" : "";
                changes.Add(new DetectedChange(
                    ChangeType.Removed,
                    ChangeCategory.StreamAction,
                    "Aggregate",
                    aggregateName,
                    $"Removed stream action {actionType}{regType}"));
            }
        }
    }

    private static void DetectEventStreamTypeChanges(
        EventStreamTypeAttributeData? previous,
        EventStreamTypeAttributeData? current,
        string aggregateName,
        List<DetectedChange> changes)
    {
        if (previous == null && current != null)
        {
            changes.Add(new DetectedChange(
                ChangeType.Added,
                ChangeCategory.EventStreamType,
                "Aggregate",
                aggregateName,
                $"Added [EventStreamType] to {aggregateName}"));
        }
        else if (previous != null && current == null)
        {
            changes.Add(new DetectedChange(
                ChangeType.Removed,
                ChangeCategory.EventStreamType,
                "Aggregate",
                aggregateName,
                $"Removed [EventStreamType] from {aggregateName}"));
        }
        else if (previous != null && current != null)
        {
            if (previous.StreamType != current.StreamType)
            {
                changes.Add(new DetectedChange(
                    ChangeType.Modified,
                    ChangeCategory.EventStreamType,
                    "Aggregate",
                    aggregateName,
                    $"Changed StreamType in {aggregateName}",
                    $"{previous.StreamType ?? "default"} → {current.StreamType ?? "default"}"));
            }
            if (previous.DocumentType != current.DocumentType)
            {
                changes.Add(new DetectedChange(
                    ChangeType.Modified,
                    ChangeCategory.EventStreamType,
                    "Aggregate",
                    aggregateName,
                    $"Changed DocumentType in {aggregateName}",
                    $"{previous.DocumentType ?? "default"} → {current.DocumentType ?? "default"}"));
            }
        }
    }

    private static void DetectBlobSettingsChanges(
        EventStreamBlobSettingsAttributeData? previous,
        EventStreamBlobSettingsAttributeData? current,
        string aggregateName,
        List<DetectedChange> changes)
    {
        if (previous == null && current != null)
        {
            var settings = new List<string>();
            if (current.DataStore != null) settings.Add($"DataStore={current.DataStore}");
            if (current.DocumentStore != null) settings.Add($"DocumentStore={current.DocumentStore}");
            if (current.SnapShotStore != null) settings.Add($"SnapShotStore={current.SnapShotStore}");

            changes.Add(new DetectedChange(
                ChangeType.Added,
                ChangeCategory.BlobSettings,
                "Aggregate",
                aggregateName,
                $"Added [EventStreamBlobSettings] to {aggregateName}",
                settings.Count > 0 ? string.Join(", ", settings) : null));
        }
        else if (previous != null && current == null)
        {
            changes.Add(new DetectedChange(
                ChangeType.Removed,
                ChangeCategory.BlobSettings,
                "Aggregate",
                aggregateName,
                $"Removed [EventStreamBlobSettings] from {aggregateName}"));
        }
        else if (previous != null && current != null)
        {
            if (previous.DataStore != current.DataStore)
            {
                changes.Add(new DetectedChange(
                    ChangeType.Modified,
                    ChangeCategory.BlobSettings,
                    "Aggregate",
                    aggregateName,
                    $"Changed DataStore for {aggregateName}",
                    $"{previous.DataStore ?? "default"} → {current.DataStore ?? "default"}"));
            }
            if (previous.DocumentStore != current.DocumentStore)
            {
                changes.Add(new DetectedChange(
                    ChangeType.Modified,
                    ChangeCategory.BlobSettings,
                    "Aggregate",
                    aggregateName,
                    $"Changed DocumentStore for {aggregateName}",
                    $"{previous.DocumentStore ?? "default"} → {current.DocumentStore ?? "default"}"));
            }
            if (previous.SnapShotStore != current.SnapShotStore)
            {
                changes.Add(new DetectedChange(
                    ChangeType.Modified,
                    ChangeCategory.BlobSettings,
                    "Aggregate",
                    aggregateName,
                    $"Changed SnapShotStore for {aggregateName}",
                    $"{previous.SnapShotStore ?? "default"} → {current.SnapShotStore ?? "default"}"));
            }
        }
    }

    private static void DetectProjectionChanges(
        Dictionary<string, ProjectionDefinition> previous,
        Dictionary<string, ProjectionDefinition> current,
        List<DetectedChange> changes)
    {
        // Find added projections
        foreach (var (name, projection) in current)
        {
            if (!previous.ContainsKey(name))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Added,
                    ChangeCategory.Projection,
                    "Projection",
                    name,
                    $"Added projection {name}",
                    $"{projection.Events.Count} events"));
            }
        }

        // Find removed projections
        foreach (var (name, _) in previous)
        {
            if (!current.ContainsKey(name))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Removed,
                    ChangeCategory.Projection,
                    "Projection",
                    name,
                    $"Removed projection {name}"));
            }
        }

        // Find modified projections
        foreach (var (name, currProjection) in current)
        {
            if (previous.TryGetValue(name, out var prevProjection))
            {
                DetectProjectionDetailChanges(prevProjection, currProjection, changes);
            }
        }
    }

    private static void DetectProjectionDetailChanges(
        ProjectionDefinition previous,
        ProjectionDefinition current,
        List<DetectedChange> changes)
    {
        var projectionName = current.Name;

        // Detect event changes (projection events are different type)
        var prevEvents = previous.Events.ToDictionary(e => e.EventName);
        var currEvents = current.Events.ToDictionary(e => e.EventName);

        foreach (var (eventName, eventDef) in currEvents)
        {
            if (!prevEvents.ContainsKey(eventName))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Added,
                    ChangeCategory.Event,
                    "Projection",
                    projectionName,
                    $"Added {eventDef.TypeName} handler to {projectionName}"));
            }
        }

        foreach (var (eventName, eventDef) in prevEvents)
        {
            if (!currEvents.ContainsKey(eventName))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Removed,
                    ChangeCategory.Event,
                    "Projection",
                    projectionName,
                    $"Removed {eventDef.TypeName} handler from {projectionName}"));
            }
        }

        // Detect property changes
        DetectPropertyChanges(previous.Properties, current.Properties, "Projection", projectionName, changes);

        // Detect constructor changes
        DetectConstructorChanges(previous.Constructors, current.Constructors, "Projection", projectionName, changes);

        // Detect PostWhen changes
        DetectPostWhenChanges(previous.PostWhen, current.PostWhen, "Projection", projectionName, changes);

        // Detect ExternalCheckpoint changes
        if (previous.ExternalCheckpoint != current.ExternalCheckpoint)
        {
            changes.Add(new DetectedChange(
                ChangeType.Modified,
                ChangeCategory.Projection,
                "Projection",
                projectionName,
                current.ExternalCheckpoint
                    ? $"Enabled external checkpoint for {projectionName}"
                    : $"Disabled external checkpoint for {projectionName}"));
        }

        // Detect HasPostWhenAllMethod changes
        if (previous.HasPostWhenAllMethod != current.HasPostWhenAllMethod)
        {
            changes.Add(new DetectedChange(
                current.HasPostWhenAllMethod ? ChangeType.Added : ChangeType.Removed,
                ChangeCategory.PostWhen,
                "Projection",
                projectionName,
                current.HasPostWhenAllMethod
                    ? $"Added PostWhenAll handler to {projectionName}"
                    : $"Removed PostWhenAll handler from {projectionName}"));
        }

        // Detect BlobProjection changes
        if (previous.BlobProjection == null && current.BlobProjection != null)
        {
            changes.Add(new DetectedChange(
                ChangeType.Added,
                ChangeCategory.Projection,
                "Projection",
                projectionName,
                $"Added blob projection to {projectionName}",
                $"container: {current.BlobProjection.Container}"));
        }
        else if (previous.BlobProjection != null && current.BlobProjection == null)
        {
            changes.Add(new DetectedChange(
                ChangeType.Removed,
                ChangeCategory.Projection,
                "Projection",
                projectionName,
                $"Removed blob projection from {projectionName}"));
        }
        else if (previous.BlobProjection != null && current.BlobProjection != null)
        {
            if (previous.BlobProjection.Container != current.BlobProjection.Container)
            {
                changes.Add(new DetectedChange(
                    ChangeType.Modified,
                    ChangeCategory.Projection,
                    "Projection",
                    projectionName,
                    $"Changed blob container for {projectionName}",
                    $"{previous.BlobProjection.Container} → {current.BlobProjection.Container}"));
            }
            if (previous.BlobProjection.Connection != current.BlobProjection.Connection)
            {
                changes.Add(new DetectedChange(
                    ChangeType.Modified,
                    ChangeCategory.Projection,
                    "Projection",
                    projectionName,
                    $"Changed blob connection for {projectionName}",
                    $"{previous.BlobProjection.Connection} → {current.BlobProjection.Connection}"));
            }
        }

        // Detect routed projection specific changes
        if (current is RoutedProjectionDefinition currRouted && previous is RoutedProjectionDefinition prevRouted)
        {
            if (prevRouted.RouterType != currRouted.RouterType)
            {
                changes.Add(new DetectedChange(
                    ChangeType.Modified,
                    ChangeCategory.RoutedProjection,
                    "Projection",
                    projectionName,
                    $"Changed router type for {projectionName}",
                    $"{prevRouted.RouterType ?? "none"} → {currRouted.RouterType ?? "none"}"));
            }

            if (prevRouted.DestinationType != currRouted.DestinationType)
            {
                changes.Add(new DetectedChange(
                    ChangeType.Modified,
                    ChangeCategory.RoutedProjection,
                    "Projection",
                    projectionName,
                    $"Changed destination type for {projectionName}",
                    $"{prevRouted.DestinationType ?? "none"} → {currRouted.DestinationType ?? "none"}"));
            }
        }
    }

    private static void DetectVersionTokenChanges(
        Dictionary<string, VersionTokenDefinition> previous,
        Dictionary<string, VersionTokenDefinition> current,
        List<DetectedChange> changes)
    {
        foreach (var (name, token) in current)
        {
            if (!previous.ContainsKey(name))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Added,
                    ChangeCategory.VersionToken,
                    "VersionToken",
                    name,
                    $"Added version token {name}",
                    $"for type {token.GenericType}"));
            }
            else if (previous.TryGetValue(name, out var prevToken))
            {
                if (prevToken.GenericType != token.GenericType)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.VersionToken,
                        "VersionToken",
                        name,
                        $"Changed generic type for {name}",
                        $"{prevToken.GenericType} → {token.GenericType}"));
                }

                if (prevToken.IsPartialClass != token.IsPartialClass)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.VersionToken,
                        "VersionToken",
                        name,
                        token.IsPartialClass
                            ? $"{name} is now partial"
                            : $"{name} is no longer partial"));
                }
            }
        }

        foreach (var (name, _) in previous)
        {
            if (!current.ContainsKey(name))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Removed,
                    ChangeCategory.VersionToken,
                    "VersionToken",
                    name,
                    $"Removed version token {name}"));
            }
        }
    }

    private static void DetectInheritedAggregateChanges(
        Dictionary<string, InheritedAggregateDefinition> previous,
        Dictionary<string, InheritedAggregateDefinition> current,
        List<DetectedChange> changes)
    {
        // Find added inherited aggregates
        foreach (var (name, inherited) in current)
        {
            if (!previous.ContainsKey(name))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Added,
                    ChangeCategory.InheritedAggregate,
                    "InheritedAggregate",
                    name,
                    $"Added inherited aggregate {name}",
                    $"inherits from {inherited.InheritedIdentifierName}"));
            }
        }

        // Find removed inherited aggregates
        foreach (var (name, _) in previous)
        {
            if (!current.ContainsKey(name))
            {
                changes.Add(new DetectedChange(
                    ChangeType.Removed,
                    ChangeCategory.InheritedAggregate,
                    "InheritedAggregate",
                    name,
                    $"Removed inherited aggregate {name}"));
            }
        }

        // Find modified inherited aggregates
        foreach (var (name, currInherited) in current)
        {
            if (previous.TryGetValue(name, out var prevInherited))
            {
                if (prevInherited.InheritedIdentifierName != currInherited.InheritedIdentifierName)
                {
                    changes.Add(new DetectedChange(
                        ChangeType.Modified,
                        ChangeCategory.InheritedAggregate,
                        "InheritedAggregate",
                        name,
                        $"Changed base type of {name}",
                        $"{prevInherited.InheritedIdentifierName} → {currInherited.InheritedIdentifierName}"));
                }
            }
        }
    }
}
