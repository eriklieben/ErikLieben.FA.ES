; Unshipped analyzer release
; https://aka.ms/roslyn-analyzer-release-tracking

### New Rules

Rule ID | Category | Severity | Notes
----- | ----- | ----- | -----
FAES0001 | Usage | Warning | WhenUsageAnalyzer: Use Fold(...) instead of When(...)
FAES0002 | Usage | Warning | AppendWithoutApplyAnalyzer: Appended event is not applied to active state; wrap with Fold(context.Append(...))
FAES0003 | Usage | Warning | NonPartialAggregateAnalyzer: Aggregates inheriting from Aggregate should be declared partial
