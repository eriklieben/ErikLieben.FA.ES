import { Component, inject, signal, OnInit, OnDestroy, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { CodeHighlighterService } from '../../../core/services/code-highlighter.service';
import { ThemeService } from '../../../core/services/theme.service';

interface NavItem {
  id: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-backup-restore-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './backup-restore.component.html',
  styleUrl: './backup-restore.component.css'
})
export class BackupRestoreComponent implements OnInit, OnDestroy {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);
  private intersectionObserver: IntersectionObserver | null = null;
  private visibleSections = new Set<string>();

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'setup', label: 'Setup', icon: 'settings' },
    { id: 'single-backup', label: 'Single Backup', icon: 'backup' },
    { id: 'restore', label: 'Restore', icon: 'restore' },
    { id: 'bulk-operations', label: 'Bulk Operations', icon: 'batch_prediction' },
    { id: 'management', label: 'Management', icon: 'folder_managed' },
    { id: 'options', label: 'Options', icon: 'tune' },
    { id: 'progress', label: 'Progress Reporting', icon: 'trending_up' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' }
  ];

  activeSection = signal<string>('overview');

  setupCodeHtml = signal<SafeHtml>('');
  singleBackupCodeHtml = signal<SafeHtml>('');
  backupWithOptionsCodeHtml = signal<SafeHtml>('');
  restoreCodeHtml = signal<SafeHtml>('');
  cloneCodeHtml = signal<SafeHtml>('');
  bulkBackupCodeHtml = signal<SafeHtml>('');
  bulkRestoreCodeHtml = signal<SafeHtml>('');
  listBackupsCodeHtml = signal<SafeHtml>('');
  managementCodeHtml = signal<SafeHtml>('');
  progressCodeHtml = signal<SafeHtml>('');
  handleInterfaceCodeHtml = signal<SafeHtml>('');

  private readonly setupCode = `// Register services
services.AddSingleton<IBackupProvider, AzureBlobBackupProvider>();
services.AddSingleton<IBackupRestoreService, BackupRestoreService>();

// Optionally register a backup registry for listing/querying backups
services.AddSingleton<IBackupRegistry, YourBackupRegistry>();`;

  private readonly singleBackupCode = `public class BackupService
{
    private readonly IBackupRestoreService _backupService;

    public BackupService(IBackupRestoreService backupService)
    {
        _backupService = backupService;
    }

    public async Task<Guid> BackupOrder(string orderId)
    {
        // Create backup with default options
        var handle = await _backupService.BackupStreamAsync(
            "order",       // Object name (aggregate type)
            orderId,       // Object ID
            BackupOptions.Default);

        return handle.BackupId;
    }
}`;

  private readonly backupWithOptionsCode = `public async Task<Guid> BackupWithOptions(string orderId)
{
    var options = new BackupOptions
    {
        IncludeSnapshots = true,
        IncludeObjectDocument = true,
        EnableCompression = true,
        Retention = TimeSpan.FromDays(30),
        Description = "Manual backup before migration",
        Tags = new Dictionary<string, string>
        {
            ["reason"] = "pre-migration",
            ["operator"] = "admin"
        }
    };

    var handle = await _backupService.BackupStreamAsync("order", orderId, options);
    return handle.BackupId;
}`;

  private readonly restoreCode = `public async Task RestoreOrder(Guid backupId)
{
    // Get the backup handle
    var handle = await _backupService.GetBackupAsync(backupId);
    if (handle == null)
    {
        throw new InvalidOperationException("Backup not found");
    }

    // Restore to original location
    await _backupService.RestoreStreamAsync(handle, RestoreOptions.Default);
}

public async Task RestoreWithOverwrite(Guid backupId)
{
    var handle = await _backupService.GetBackupAsync(backupId);

    // Restore with overwrite option
    await _backupService.RestoreStreamAsync(handle, RestoreOptions.WithOverwrite);
}`;

  private readonly cloneCode = `public async Task CloneOrder(Guid backupId, string newOrderId)
{
    var handle = await _backupService.GetBackupAsync(backupId);

    // Restore to a new location (clone)
    await _backupService.RestoreToNewStreamAsync(
        handle,
        newOrderId,
        RestoreOptions.Default);
}`;

  private readonly bulkBackupCode = `public async Task<BulkBackupResult> BackupAllOrders(IEnumerable<string> orderIds)
{
    var options = new BulkBackupOptions
    {
        EnableCompression = true,
        MaxConcurrency = 8,        // Process 8 at a time
        ContinueOnError = true,    // Continue if one fails
        OnProgress = progress =>
        {
            Console.WriteLine(
                $"Progress: {progress.ProcessedStreams}/{progress.TotalStreams}");
        }
    };

    var result = await _backupService.BackupManyAsync(
        orderIds,
        "order",
        options);

    Console.WriteLine(
        $"Completed: {result.SuccessCount} succeeded, {result.FailureCount} failed");

    // Handle failures
    foreach (var failure in result.FailedBackups)
    {
        Console.WriteLine($"Failed: {failure.ObjectId} - {failure.ErrorMessage}");
    }

    return result;
}`;

  private readonly bulkRestoreCode = `public async Task<BulkRestoreResult> RestoreAllBackups(
    IEnumerable<IBackupHandle> handles)
{
    var options = new BulkRestoreOptions
    {
        Overwrite = true,
        MaxConcurrency = 4,
        ContinueOnError = true,
        OnProgress = progress =>
        {
            Console.WriteLine(
                $"Restoring: {progress.ProcessedBackups}/{progress.TotalBackups}");
        }
    };

    return await _backupService.RestoreManyAsync(handles, options);
}`;

  private readonly listBackupsCode = `public async Task ListBackups()
{
    // List all backups
    var allBackups = await _backupService.ListBackupsAsync();

    // Query with filters
    var query = new BackupQuery
    {
        ObjectName = "order",
        CreatedAfter = DateTimeOffset.UtcNow.AddDays(-7),
        MaxResults = 100,
        Tags = new Dictionary<string, string>
        {
            ["environment"] = "production"
        }
    };

    var filteredBackups = await _backupService.ListBackupsAsync(query);

    foreach (var backup in filteredBackups)
    {
        Console.WriteLine(
            $"{backup.BackupId}: {backup.ObjectName}/{backup.ObjectId} " +
            $"({backup.EventCount} events)");
    }
}`;

  private readonly managementCode = `// Validate backup integrity
public async Task<bool> ValidateBackup(Guid backupId)
{
    var handle = await _backupService.GetBackupAsync(backupId);
    if (handle == null) return false;

    return await _backupService.ValidateBackupAsync(handle);
}

// Delete a specific backup
public async Task DeleteBackup(Guid backupId)
{
    var handle = await _backupService.GetBackupAsync(backupId);
    if (handle != null)
    {
        await _backupService.DeleteBackupAsync(handle);
    }
}

// Cleanup expired backups based on retention policy
public async Task CleanupOldBackups()
{
    var deletedCount = await _backupService.CleanupExpiredBackupsAsync();
    Console.WriteLine($"Deleted {deletedCount} expired backups");
}`;

  private readonly progressCode = `// Single operation progress
public async Task BackupWithProgress(string objectId)
{
    var progress = new Progress<BackupProgress>(p =>
    {
        Console.WriteLine(
            $"Backup: {p.EventsBackedUp}/{p.TotalEvents} events " +
            $"({p.PercentageComplete:F1}%)");
    });

    await _backupService.BackupStreamAsync(
        "order",
        objectId,
        BackupOptions.Default,
        progress);
}

public async Task RestoreWithProgress(IBackupHandle handle)
{
    var progress = new Progress<RestoreProgress>(p =>
    {
        Console.WriteLine(
            $"Restore: {p.EventsRestored}/{p.TotalEvents} events " +
            $"({p.PercentageComplete:F1}%)");
    });

    await _backupService.RestoreStreamAsync(
        handle,
        RestoreOptions.Default,
        progress);
}`;

  private readonly handleInterfaceCode = `public interface IBackupHandle
{
    /// <summary>Unique identifier for this backup.</summary>
    Guid BackupId { get; }

    /// <summary>The object name (aggregate type).</summary>
    string ObjectName { get; }

    /// <summary>The object identifier.</summary>
    string ObjectId { get; }

    /// <summary>Number of events in the backup.</summary>
    int EventCount { get; }

    /// <summary>When the backup was created.</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>Location/path of the backup.</summary>
    string Location { get; }
}`;

  constructor() {
    effect(() => {
      this.themeService.theme();
      this.highlightCodeSamples();
    });
  }

  ngOnInit(): void {
    this.highlightCodeSamples();
    this.setupIntersectionObserver();
  }

  ngOnDestroy(): void {
    if (this.intersectionObserver) {
      this.intersectionObserver.disconnect();
    }
  }

  private setupIntersectionObserver(): void {
    this.intersectionObserver = new IntersectionObserver(
      (entries) => {
        entries.forEach(entry => {
          const sectionId = entry.target.id;
          if (entry.isIntersecting) {
            this.visibleSections.add(sectionId);
          } else {
            this.visibleSections.delete(sectionId);
          }
        });
        this.updateActiveSection();
      },
      { threshold: 0, rootMargin: '-80px 0px -50% 0px' }
    );

    this.navItems.forEach(item => {
      const element = document.getElementById(item.id);
      if (element) {
        this.intersectionObserver!.observe(element);
      }
    });
  }

  private updateActiveSection(): void {
    for (const item of this.navItems) {
      if (this.visibleSections.has(item.id)) {
        this.activeSection.set(item.id);
        return;
      }
    }
    if (this.visibleSections.size === 0) {
      this.activeSection.set('overview');
    }
  }

  scrollToSection(event: Event, sectionId: string): void {
    event.preventDefault();
    const element = document.getElementById(sectionId);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
      this.activeSection.set(sectionId);
    }
  }

  private async highlightCodeSamples(): Promise<void> {
    const [
      setup, singleBackup, backupWithOptions, restore, clone,
      bulkBackup, bulkRestore, listBackups, management, progress, handleInterface
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.setupCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.singleBackupCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.backupWithOptionsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.restoreCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.cloneCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.bulkBackupCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.bulkRestoreCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.listBackupsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.managementCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.progressCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.handleInterfaceCode, { language: 'csharp' })
    ]);

    this.setupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(setup));
    this.singleBackupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(singleBackup));
    this.backupWithOptionsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(backupWithOptions));
    this.restoreCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(restore));
    this.cloneCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(clone));
    this.bulkBackupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(bulkBackup));
    this.bulkRestoreCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(bulkRestore));
    this.listBackupsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(listBackups));
    this.managementCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(management));
    this.progressCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(progress));
    this.handleInterfaceCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(handleInterface));
  }
}
