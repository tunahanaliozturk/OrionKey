; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ORIONKEY001 | OrionKey | Error | OrionId target must be a readonly partial struct
ORIONKEY002 | OrionKey | Error | Unsupported OrionId value or strategy type
ORIONKEY003 | OrionKey | Error | string OrionId requires an explicit strategy
ORIONKEY004 | OrionKey | Error | Incompatible OrionId strategy
ORIONKEY005 | OrionKey | Warning | OrionId struct declares a generated member
