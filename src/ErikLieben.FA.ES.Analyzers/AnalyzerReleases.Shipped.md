; Shipped analyzer releases
; https://aka.ms/roslyn-analyzer-release-tracking

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
FAES0001 | Usage | Warning | WhenUsageAnalyzer: Use Fold(...) instead of When(...)
FAES0002 | Usage | Warning | AppendWithoutApplyAnalyzer: Appended event is not applied to active state
FAES0003 | Usage | Warning | NonPartialAggregateAnalyzer: Aggregates inheriting from Aggregate should be declared partial
FAES0004 | Usage | Info | UnusedWhenEventParameterAnalyzer: When method has unused event parameter
FAES0005 | CodeGeneration | Warning | CodeGenerationRequiredAnalyzer: Generated file missing
FAES0006 | CodeGeneration | Warning | CodeGenerationRequiredAnalyzer: Generated code is out of date
FAES0007 | CodeGeneration | Warning | CodeGenerationRequiredAnalyzer: Property not in generated interface
FAES0012 | CodeGeneration | Warning | ExtensionsRegistrationAnalyzer: Aggregate not registered in Extensions
FAES0014 | CodeGeneration | Warning | ExtensionsRegistrationAnalyzer: Extensions file missing
FAES0015 | CodeGeneration | Warning | VersionTokenGenerationAnalyzer: VersionToken generated file missing
