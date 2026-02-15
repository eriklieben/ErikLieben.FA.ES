/**
 * TypeScript interfaces for BenchmarkDotNet JSON output format.
 * These match the JSON structure exported by BenchmarkDotNet with --exporters json
 */

export interface BenchmarkReport {
  Title: string;
  HostEnvironmentInfo: HostEnvironmentInfo;
  Benchmarks: BenchmarkResult[];
}

export interface HostEnvironmentInfo {
  BenchmarkDotNetCaption: string;
  BenchmarkDotNetVersion: string;
  OsVersion: string;
  ProcessorName: string;
  PhysicalProcessorCount: number | null;
  PhysicalCoreCount: number | null;
  LogicalCoreCount: number | null;
  RuntimeVersion: string;
  Architecture: string;
  HasAttachedDebugger: boolean;
  HasRyuJit: boolean;
  Configuration: string;
  DotNetCliVersion: string;
  ChronometerFrequency: { Hertz: number };
  HardwareTimerKind: string;
}

export interface BenchmarkResult {
  DisplayInfo: string;
  Namespace: string;
  Type: string;
  Method: string;
  MethodTitle: string;
  Parameters: string;
  FullName: string;
  HardwareIntrinsics: string;
  Statistics: BenchmarkStatistics;
  Memory: BenchmarkMemory;
  Measurements: BenchmarkMeasurement[];
  Metrics: BenchmarkMetric[];
}

export interface BenchmarkStatistics {
  OriginalValues: number[];
  N: number;
  Min: number;
  LowerFence: number;
  Q1: number;
  Median: number;
  Mean: number;
  Q3: number;
  UpperFence: number;
  Max: number;
  InterquartileRange: number;
  LowerOutliers: number[];
  UpperOutliers: number[];
  AllOutliers: number[];
  StandardError: number;
  Variance: number;
  StandardDeviation: number;
  Skewness: string | number;
  Kurtosis: string | number;
  ConfidenceInterval: {
    N: number;
    Mean: number;
    StandardError: number;
    Level: number;
    Margin: string | number;
    Lower: string | number;
    Upper: string | number;
  };
  Percentiles: {
    P0: number;
    P25: number;
    P50: number;
    P67: number;
    P80: number;
    P85: number;
    P90: number;
    P95: number;
    P100: number;
  };
}

export interface BenchmarkMemory {
  Gen0Collections: number;
  Gen1Collections: number;
  Gen2Collections: number;
  TotalOperations: number;
  BytesAllocatedPerOperation: number;
}

export interface BenchmarkMeasurement {
  IterationMode: string;
  IterationStage: string;
  LaunchIndex: number;
  IterationIndex: number;
  Operations: number;
  Nanoseconds: number;
}

export interface BenchmarkMetric {
  Value: number;
  Descriptor: {
    Id: string;
    DisplayName: string;
    Legend: string;
    NumberFormat: string;
    UnitType: number;
    Unit: string;
    TheGreaterTheBetter: boolean;
    PriorityInCategory: number;
  };
}

// UI-friendly transformed types
export interface BenchmarkSummary {
  name: string;
  type: string;
  method: string;
  parameters: string;
  meanNs: number;
  meanFormatted: string;
  medianNs: number;
  allocatedBytes: number;
  allocatedFormatted: string;
  gen0: number;
  gen1: number;
  gen2: number;
  stdDev: number;
  stdDevFormatted: string;
  ratio?: number;
  isBaseline?: boolean;
}

export interface BenchmarkFileInfo {
  name: string;
  path: string;
  date: string;
  framework: string;
  benchmarkCount: number;
}

export interface BenchmarkComparison {
  method: string;
  baseline: BenchmarkSummary;
  comparison: BenchmarkSummary;
  speedupFactor: number;
  memoryReduction: number;
}
