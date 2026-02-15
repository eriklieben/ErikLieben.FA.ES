import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, forkJoin, map, of } from 'rxjs';
import {
  BenchmarkReport,
  BenchmarkSummary,
  BenchmarkFileInfo,
  BenchmarkResult
} from '../contracts/benchmark.contracts';

@Injectable({
  providedIn: 'root'
})
export class BenchmarkApiService {
  private readonly http = inject(HttpClient);

  /**
   * Get list of available benchmark result files
   */
  listBenchmarkFiles(): Observable<BenchmarkFileInfo[]> {
    return this.http.get<BenchmarkFileInfo[]>('/api/admin/benchmarks');
  }

  /**
   * Load a specific benchmark report by filename
   */
  loadBenchmarkReport(filename: string): Observable<BenchmarkReport> {
    return this.http.get<BenchmarkReport>(`/api/admin/benchmarks/${encodeURIComponent(filename)}`);
  }

  /**
   * Load ALL benchmark reports and combine them
   */
  loadAllBenchmarkReports(files: BenchmarkFileInfo[]): Observable<BenchmarkReport[]> {
    if (files.length === 0) {
      return of([]);
    }

    const requests = files.map(f => this.loadBenchmarkReport(f.name));
    return forkJoin(requests);
  }

  /**
   * Combine multiple reports into a single list of summaries
   */
  combineReportsToSummaries(reports: BenchmarkReport[]): BenchmarkSummary[] {
    const allBenchmarks: BenchmarkResult[] = [];

    for (const report of reports) {
      allBenchmarks.push(...report.Benchmarks);
    }

    // Create a combined report structure for transformation
    const combinedReport: BenchmarkReport = {
      Title: 'Combined Benchmarks',
      HostEnvironmentInfo: reports[0]?.HostEnvironmentInfo || {
        BenchmarkDotNetVersion: '',
        OsVersion: '',
        ProcessorName: '',
        RuntimeVersion: '',
        Architecture: '',
        Configuration: '',
        DotNetCliVersion: ''
      },
      Benchmarks: allBenchmarks
    };

    return this.transformToSummaries(combinedReport);
  }

  /**
   * Transform raw benchmark results into UI-friendly summaries
   * Baselines are calculated per TYPE + PARAMETERS + OPERATION group
   */
  transformToSummaries(report: BenchmarkReport): BenchmarkSummary[] {
    // Group by type, parameters, AND operation to find baselines correctly
    // For registry benchmarks: FrozenRegistry_X vs MutableRegistry_X should compare
    // For JSON benchmarks: SourceGen vs Reflection should compare per PayloadSize
    const byComparisonGroup = new Map<string, BenchmarkResult[]>();

    for (const b of report.Benchmarks) {
      const operationGroup = this.getOperationGroup(b.Method);
      const key = `${b.Type}|${b.Parameters || ''}|${operationGroup}`;
      const existing = byComparisonGroup.get(key) || [];
      existing.push(b);
      byComparisonGroup.set(key, existing);
    }

    // Find baseline for each comparison group
    const baselineMeans = new Map<string, number>();
    for (const [key, benchmarks] of byComparisonGroup) {
      // Look for baseline: DisplayInfo includes 'Baseline', or method starts with 'Frozen', 'SourceGen'
      const baseline = benchmarks.find(b => b.DisplayInfo?.includes('Baseline'))
        || benchmarks.find(b => b.Method.startsWith('Frozen') || b.Method === 'SourceGen' || b.Method === 'RawDeserialize')
        || benchmarks[0];
      baselineMeans.set(key, baseline.Statistics.Mean);
    }

    return report.Benchmarks.map(b => {
      const meanNs = b.Statistics.Mean;
      const operationGroup = this.getOperationGroup(b.Method);
      const key = `${b.Type}|${b.Parameters || ''}|${operationGroup}`;
      const baselineMean = baselineMeans.get(key) || meanNs;
      const ratio = meanNs / baselineMean;
      const isBaseline = Math.abs(ratio - 1) < 0.001;

      return {
        name: b.FullName,
        type: b.Type,
        method: b.MethodTitle || b.Method,
        parameters: b.Parameters,
        meanNs,
        meanFormatted: this.formatTime(meanNs),
        medianNs: b.Statistics.Median,
        allocatedBytes: b.Memory?.BytesAllocatedPerOperation || 0,
        allocatedFormatted: this.formatBytes(b.Memory?.BytesAllocatedPerOperation || 0),
        gen0: b.Memory?.Gen0Collections || 0,
        gen1: b.Memory?.Gen1Collections || 0,
        gen2: b.Memory?.Gen2Collections || 0,
        stdDev: b.Statistics.StandardDeviation,
        stdDevFormatted: this.formatTime(b.Statistics.StandardDeviation),
        ratio: isBaseline ? undefined : ratio,
        isBaseline
      };
    });
  }

  /**
   * Extract the operation group from a method name for proper baseline comparison.
   *
   * Examples:
   * - "FrozenRegistry_SingleLookup" -> "SingleLookup" (compare with MutableRegistry_SingleLookup)
   * - "MutableRegistry_TryGetByType" -> "TryGetByType" (compare with FrozenRegistry_TryGetByType)
   * - "SourceGen" -> "Serialization" (compare with Reflection)
   * - "Reflection" -> "Serialization" (compare with SourceGen)
   * - "RawDeserialize" -> "Processing" (compare with ToEventWithMetadata)
   * - "ToEventWithMetadata" -> "Processing"
   */
  private getOperationGroup(methodName: string): string {
    // Registry benchmarks: extract operation after prefix
    if (methodName.includes('Registry_')) {
      const parts = methodName.split('_');
      return parts.length > 1 ? parts.slice(1).join('_') : methodName;
    }

    // JSON serialization: SourceGen vs Reflection are in the same group
    if (methodName === 'SourceGen' || methodName === 'Reflection') {
      return 'JsonComparison';
    }

    // Event processing: RawDeserialize vs ToEventWithMetadata
    if (methodName === 'RawDeserialize' || methodName === 'ToEventWithMetadata') {
      return 'EventProcessing';
    }

    // Default: use the method name itself
    return methodName;
  }

  /**
   * Format nanoseconds to a human-readable string
   */
  formatTime(ns: number): string {
    if (ns < 1000) {
      return `${ns.toFixed(2)} ns`;
    } else if (ns < 1_000_000) {
      return `${(ns / 1000).toFixed(2)} us`;
    } else if (ns < 1_000_000_000) {
      return `${(ns / 1_000_000).toFixed(2)} ms`;
    } else {
      return `${(ns / 1_000_000_000).toFixed(2)} s`;
    }
  }

  /**
   * Format bytes to a human-readable string
   */
  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    if (bytes < 1024) {
      return `${bytes} B`;
    } else if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(2)} KB`;
    } else {
      return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
    }
  }

  /**
   * Group benchmarks by their type/class
   */
  groupByType(summaries: BenchmarkSummary[]): Map<string, BenchmarkSummary[]> {
    const groups = new Map<string, BenchmarkSummary[]>();
    for (const summary of summaries) {
      const existing = groups.get(summary.type) || [];
      existing.push(summary);
      groups.set(summary.type, existing);
    }
    return groups;
  }

  /**
   * Group benchmarks by parameters for comparison
   */
  groupByParameters(summaries: BenchmarkSummary[]): Map<string, BenchmarkSummary[]> {
    const groups = new Map<string, BenchmarkSummary[]>();
    for (const summary of summaries) {
      const params = summary.parameters || 'Default';
      const existing = groups.get(params) || [];
      existing.push(summary);
      groups.set(params, existing);
    }
    return groups;
  }
}
