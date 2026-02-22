import { Component, inject, Inject, signal, OnInit, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { AdminApiService } from '../../core/services/admin-api.service';
import { ThemeService } from '../../core/services/theme.service';

export interface ProjectionViewerData {
  projectionName: string;
}

@Component({
  selector: 'app-projection-viewer-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MonacoEditorModule
  ],
  templateUrl: './projection-viewer-dialog.component.html',
  styleUrl: './projection-viewer-dialog.component.css'
})
export class ProjectionViewerDialogComponent implements OnInit {
  private readonly adminApi = inject(AdminApiService);
  private readonly dialogRef = inject(MatDialogRef<ProjectionViewerDialogComponent>);
  private readonly themeService = inject(ThemeService);

  readonly data = inject<ProjectionViewerData>(MAT_DIALOG_DATA);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  jsonContent = '';

  // Compute Monaco theme based on current app theme
  readonly monacoTheme = computed(() =>
    this.themeService.theme() === 'dark' ? 'catppuccin-macchiato' : 'catppuccin-latte'
  );

  editorOptions = {
    theme: this.monacoTheme(),
    language: 'json',
    readOnly: true,
    minimap: { enabled: true },
    scrollBeyondLastLine: false,
    wordWrap: 'on' as const,
    automaticLayout: true,
    // Enable code folding
    folding: true,
    showFoldingControls: 'always' as const,
    foldingStrategy: 'indentation' as const,
    foldingHighlight: true
  };

  constructor() {
    // Update editor theme when app theme changes
    effect(() => {
      this.editorOptions = {
        ...this.editorOptions,
        theme: this.monacoTheme()
      };
    });
  }

  ngOnInit() {
    this.loadProjectionJson();
  }

  private loadProjectionJson() {
    this.loading.set(true);
    this.error.set(null);

    console.log('Loading projection JSON for:', this.data.projectionName);

    this.adminApi.getProjectionJson(this.data.projectionName).subscribe({
      next: (json) => {
        console.log('Received JSON response:', json);
        console.log('JSON length:', json?.length);
        console.log('JSON type:', typeof json);

        if (!json || json.trim().length === 0) {
          console.error('Received empty JSON response');
          this.error.set('Projection returned empty data');
          this.loading.set(false);
          return;
        }

        try {
          // Pretty-print the JSON
          const parsed = JSON.parse(json);
          this.jsonContent = JSON.stringify(parsed, null, 2);
          console.log('Parsed and formatted JSON successfully');
          console.log('Final content length:', this.jsonContent.length);
          this.loading.set(false);
        } catch (e) {
          console.error('Failed to parse JSON:', e);
          console.error('Raw JSON that failed to parse:', json);
          this.error.set('Failed to parse projection JSON');
          this.loading.set(false);
        }
      },
      error: (err) => {
        console.error('Failed to load projection:', err);
        this.error.set(`Failed to load projection: ${err.message || err.statusText || 'Unknown error'}`);
        this.loading.set(false);
      }
    });
  }

  copyToClipboard() {
    navigator.clipboard.writeText(this.jsonContent).then(() => {
      // Could add a snackbar notification here
      console.log('Copied to clipboard!');
    });
  }

  close() {
    this.dialogRef.close();
  }
}
