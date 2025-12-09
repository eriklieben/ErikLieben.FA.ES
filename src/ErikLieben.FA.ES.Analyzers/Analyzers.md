# Analyzers

This package includes Roslyn analyzers and code fixes to help you write correct event-sourced code with the ErikLieben.FA.ES framework.

## Usage Analyzers

Rule ID | Severity | Description
--------|----------|------------
FAES0001 | Warning | **Prefer Fold over When** - Use `Fold(...)` instead of calling `When(...)` directly. The Fold method properly tracks event processing.
FAES0002 | Warning | **Event appended but not applied** - An event was appended to the stream but not applied to the active state. Wrap with `Fold(context.Append(...))` to ensure state consistency.
FAES0003 | Warning | **Aggregate should be partial** - Classes inheriting from `Aggregate` should be declared `partial` to allow code generation.
FAES0004 | Info | **Unused When event parameter** - The event parameter in a `When` method is not used. Consider using `[When<TEvent>]` attribute instead.

## Code Generation Analyzers

Rule ID | Severity | Description
--------|----------|------------
FAES0005 | Warning | **Generated file missing** - The `.Generated.cs` file for this aggregate/projection is missing. Run `dotnet faes` to generate supporting code.
FAES0006 | Warning | **Generated code out of date** - The generated code is out of date with the source. Run `dotnet faes` to update.
FAES0007 | Warning | **Property not in generated interface** - A property exists in the aggregate that is not included in the generated interface. Run `dotnet faes` to update.
FAES0012 | Warning | **Aggregate not registered** - The aggregate is not registered in the Extensions file. Run `dotnet faes` to update.
FAES0014 | Warning | **Extensions file missing** - The Extensions registration file is missing. Run `dotnet faes` to generate.
FAES0015 | Warning | **VersionToken file missing** - The VersionToken generated file is missing. Run `dotnet faes` to generate.

## Code Fixes

- **FAES0004**: Automatically converts `private void When(EventType @event)` to `[When<EventType>] private void When()` when the event parameter is unused.

## Refactoring Providers

The package also includes code refactoring providers accessible via the lightbulb menu:

- **StreamAction Refactoring**: Convert between `stream.RegisterAction(new ActionType())` and `[StreamAction<ActionType>]` attribute.
- **ObjectName Refactoring**: Add explicit `[ObjectName("...")]` attribute or remove redundant ones that match the convention.
- **EventName Refactoring**: Add explicit `[EventName("...")]` attribute or remove redundant ones that match the convention.
