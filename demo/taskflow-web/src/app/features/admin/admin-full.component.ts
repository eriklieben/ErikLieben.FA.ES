import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AdminApiService } from '../../core/services/admin-api.service';
import { FunctionsApiService } from '../../core/services/functions-api.service';
import type { StorageConnection } from '../../core/contracts/admin.contracts';

interface ServiceHealth {
  name: string;
  status: 'checking' | 'healthy' | 'unhealthy';
  details?: string;
  lastChecked?: Date;
}

@Component({
  selector: 'app-admin-full',
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule
  ],
  templateUrl: './admin-full.component.html',
  styleUrl: './admin-full.component.css'
})
export class AdminFullComponent implements OnInit, OnDestroy {
  private readonly adminApi = inject(AdminApiService);
  private readonly functionsApi = inject(FunctionsApiService);
  private readonly snackBar = inject(MatSnackBar);

  readonly storageConnection = signal<StorageConnection | null>(null);
  readonly serviceHealth = signal<ServiceHealth[]>([
    { name: 'TaskFlow API', status: 'checking' },
    { name: 'Azure Functions', status: 'checking' }
  ]);
  readonly isCheckingHealth = signal(false);

  ngOnInit() {
    this.loadStorageConnection();
    this.refreshHealthChecks();
  }

  ngOnDestroy() {
    // Cleanup if needed
  }

  refreshHealthChecks() {
    this.isCheckingHealth.set(true);

    // Reset to checking state
    this.serviceHealth.set([
      { name: 'TaskFlow API', status: 'checking' },
      { name: 'Azure Functions', status: 'checking' }
    ]);

    // Check TaskFlow API health (via storage connection endpoint as a proxy)
    this.adminApi.getStorageConnection().subscribe({
      next: () => {
        this.updateServiceHealth('TaskFlow API', 'healthy', 'API responding normally');
      },
      error: (err) => {
        this.updateServiceHealth('TaskFlow API', 'unhealthy', `Error: ${err.status || 'Connection failed'}`);
      }
    });

    // Check Azure Functions health
    this.functionsApi.getHealthStatus().subscribe({
      next: (response) => {
        console.log('Azure Functions health response:', response);
        if (response && response.status === 'healthy') {
          this.updateServiceHealth('Azure Functions', 'healthy', response.service);
        } else if (response) {
          this.updateServiceHealth('Azure Functions', 'unhealthy', `Unexpected: ${JSON.stringify(response)}`);
        } else {
          this.updateServiceHealth('Azure Functions', 'unhealthy', 'Response was null/undefined');
        }
        this.isCheckingHealth.set(false);
      },
      error: (err) => {
        console.error('Azure Functions health error:', err);
        const errorMsg = err.error?.message || err.message || `HTTP ${err.status}`;
        this.updateServiceHealth('Azure Functions', 'unhealthy', `Error: ${errorMsg}`);
        this.isCheckingHealth.set(false);
      }
    });
  }

  private updateServiceHealth(name: string, status: 'healthy' | 'unhealthy', details?: string) {
    this.serviceHealth.update(services =>
      services.map(s =>
        s.name === name
          ? { ...s, status, details, lastChecked: new Date() }
          : s
      )
    );
  }

  private loadStorageConnection() {
    this.adminApi.getStorageConnection().subscribe({
      next: (connection) => {
        this.storageConnection.set(connection);
      },
      error: (error) => {
        console.error('Error loading storage connection:', error);
        this.snackBar.open('Failed to load storage connection', 'Close', {
          duration: 3000,
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  copyConnectionString(connectionString: string) {
    navigator.clipboard.writeText(connectionString).then(() => {
      this.snackBar.open('Connection string copied to clipboard!', 'Close', {
        duration: 3000,
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }).catch(err => {
      console.error('Failed to copy:', err);
      this.snackBar.open('Failed to copy connection string', 'Close', {
        duration: 3000,
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    });
  }
}
