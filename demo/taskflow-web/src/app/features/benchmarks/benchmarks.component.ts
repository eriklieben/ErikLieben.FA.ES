import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { BenchmarkApiService } from '../../core/services/benchmark-api.service';
import {
  BenchmarkReport,
  BenchmarkSummary,
  BenchmarkFileInfo
} from '../../core/contracts/benchmark.contracts';
import {
  getBenchmarkMetadata,
  getMethodMetadata,
  BenchmarkTypeMetadata,
  BenchmarkMethodMetadata
} from '../../core/contracts/benchmark-metadata';
import { switchMap } from 'rxjs';

@Component({
  selector: 'app-benchmarks',
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatExpansionModule,
    MatFormFieldModule
  ],
  templateUrl: './benchmarks.component.html',
  styleUrl: './benchmarks.component.css'
})
export class BenchmarksComponent implements OnInit {
  readonly benchmarkApi = inject(BenchmarkApiService);

  // State
  readonly isLoading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly files = signal<BenchmarkFileInfo[]>([]);
  readonly reports = signal<BenchmarkReport[]>([]);
  readonly summaries = signal<BenchmarkSummary[]>([]);

  // Computed
  readonly hostInfo = computed(() => {
    const allReports = this.reports();
    return allReports.length > 0 ? allReports[0].HostEnvironmentInfo : null;
  });

  readonly availableTypes = computed(() => {
    const types = new Set(this.summaries().map(s => s.type));
    return Array.from(types).sort((a, b) => a.localeCompare(b));
  });

  readonly groupedByType = computed(() => {
    return this.benchmarkApi.groupByType(this.summaries());
  });

  readonly totalBenchmarkCount = computed(() => {
    return this.summaries().length;
  });

  readonly totalFileCount = computed(() => {
    return this.files().length;
  });

  ngOnInit() {
    this.loadAllBenchmarks();
  }

  loadAllBenchmarks() {
    this.isLoading.set(true);
    this.loadError.set(null);

    this.benchmarkApi.listBenchmarkFiles().pipe(
      switchMap(files => {
        this.files.set(files);
        return this.benchmarkApi.loadAllBenchmarkReports(files);
      })
    ).subscribe({
      next: (reports) => {
        this.reports.set(reports);
        const summaries = this.benchmarkApi.combineReportsToSummaries(reports);
        this.summaries.set(summaries);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading benchmark files:', err);
        this.loadError.set(err.message || 'Failed to load benchmark files');
        this.isLoading.set(false);
      }
    });
  }

  refresh() {
    this.loadAllBenchmarks();
  }

  formatRatio(ratio: number | undefined): string {
    if (ratio === undefined) return 'baseline';
    if (ratio < 1) {
      return (1 / ratio).toFixed(2) + 'x faster';
    }
    return ratio.toFixed(2) + 'x slower';
  }

  getRatioClass(ratio: number | undefined): string {
    if (ratio === undefined) return 'baseline';
    if (ratio < 0.95) return 'faster';
    if (ratio > 1.05) return 'slower';
    return 'similar';
  }

  /** Get metadata for a benchmark type */
  getTypeMetadata(typeName: string): BenchmarkTypeMetadata {
    return getBenchmarkMetadata(typeName);
  }

  /** Get metadata for a specific method */
  getMethodMetadata(typeName: string, methodName: string): BenchmarkMethodMetadata | undefined {
    return getMethodMetadata(typeName, methodName);
  }

  /** Get short type name from full qualified name */
  getShortTypeName(fullName: string): string {
    return fullName.split('.').pop() || fullName;
  }

  /** Get minimum mean time from a list of benchmarks */
  getMinMean(benchmarks: BenchmarkSummary[]): number {
    if (benchmarks.length === 0) return 0;
    return Math.min(...benchmarks.map(b => b.meanNs));
  }

  /** Get maximum mean time from a list of benchmarks */
  getMaxMean(benchmarks: BenchmarkSummary[]): number {
    if (benchmarks.length === 0) return 0;
    return Math.max(...benchmarks.map(b => b.meanNs));
  }

  /** Get the fastest benchmark by mean time */
  getFastestBenchmark(): BenchmarkSummary {
    const all = this.summaries();
    if (all.length === 0) {
      return this.createEmptyBenchmark();
    }
    return all.reduce((min, b) => b.meanNs < min.meanNs ? b : min, all[0]);
  }

  /** Get the slowest benchmark by mean time */
  getSlowestBenchmark(): BenchmarkSummary {
    const all = this.summaries();
    if (all.length === 0) {
      return this.createEmptyBenchmark();
    }
    return all.reduce((max, b) => b.meanNs > max.meanNs ? b : max, all[0]);
  }

  /** Get count of zero-allocation benchmarks */
  getZeroAllocCount(): number {
    return this.summaries().filter(b => b.allocatedBytes === 0).length;
  }

  /** Get percentage of zero-allocation benchmarks */
  getZeroAllocPercentage(): string {
    const total = this.summaries().length;
    if (total === 0) return '0';
    return ((this.getZeroAllocCount() / total) * 100).toFixed(0);
  }

  /** Get the speed range description */
  getSpeedRange(): string {
    const fastest = this.getFastestBenchmark();
    const slowest = this.getSlowestBenchmark();
    if (fastest.meanNs === 0) return 'N/A';
    const ratio = slowest.meanNs / fastest.meanNs;
    return ratio.toFixed(0) + 'x';
  }

  /** Get the ratio between slowest and fastest */
  getSpeedRatio(): string {
    const fastest = this.getFastestBenchmark();
    const slowest = this.getSlowestBenchmark();
    if (fastest.meanNs === 0) return '0';
    return (slowest.meanNs / fastest.meanNs).toFixed(0);
  }

  /** Check if a row is the fastest in its category */
  isRowFastest(row: BenchmarkSummary, benchmarks: BenchmarkSummary[]): boolean {
    if (benchmarks.length === 0) return false;
    const minMean = Math.min(...benchmarks.map(b => b.meanNs));
    return row.meanNs === minMean;
  }

  /** Get count of zero-allocation benchmarks in a category */
  getZeroAllocInCategory(benchmarks: BenchmarkSummary[]): number {
    return benchmarks.filter(b => b.allocatedBytes === 0).length;
  }

  /** Check if there are source-gen benchmarks */
  hasSourceGenBenchmarks(): boolean {
    return this.summaries().some(b =>
      b.method.toLowerCase().includes('sourcegen') ||
      b.type.toLowerCase().includes('serializ')
    );
  }

  /** Check if there are frozen registry benchmarks */
  hasFrozenRegistryBenchmarks(): boolean {
    return this.summaries().some(b =>
      b.method.toLowerCase().includes('frozen') ||
      b.type.toLowerCase().includes('registry')
    );
  }

  /** Get icon for a benchmark category */
  getCategoryIcon(typeName: string): string {
    const shortName = this.getShortTypeName(typeName).toLowerCase();

    if (shortName.includes('registry')) return 'app_registration';
    if (shortName.includes('token') || shortName.includes('version')) return 'token';
    if (shortName.includes('stream')) return 'stream';
    if (shortName.includes('memory') || shortName.includes('inmemory')) return 'memory';
    if (shortName.includes('upcast')) return 'upgrade';
    if (shortName.includes('fold') || shortName.includes('aggregate')) return 'layers';
    if (shortName.includes('session')) return 'verified_user';
    if (shortName.includes('json') || shortName.includes('serial') || shortName.includes('deserial')) return 'data_object';
    if (shortName.includes('snapshot')) return 'camera_alt';

    return 'timer';
  }

  /** Scroll to a benchmark section */
  scrollToSection(typeName: string): void {
    const sectionId = 'section-' + this.getShortTypeName(typeName);
    const element = document.getElementById(sectionId);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  /** Create empty benchmark summary */
  private createEmptyBenchmark(): BenchmarkSummary {
    return {
      name: 'N/A',
      method: 'N/A',
      type: '',
      parameters: '',
      meanNs: 0,
      medianNs: 0,
      stdDev: 0,
      allocatedBytes: 0,
      gen0: 0,
      gen1: 0,
      gen2: 0,
      meanFormatted: 'N/A',
      stdDevFormatted: 'N/A',
      allocatedFormatted: 'N/A',
      isBaseline: false
    };
  }
}
