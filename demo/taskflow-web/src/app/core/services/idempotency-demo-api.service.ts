import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * API service for idempotency demo endpoints.
 *
 * Demonstrates the Decision Checkpoint Pattern:
 * 1. Fetch data with checkpoint fingerprint
 * 2. Store the checkpoint
 * 3. Include checkpoint in subsequent state-changing requests
 * 4. Handle stale decision errors gracefully
 */
@Injectable({
  providedIn: 'root'
})
export class IdempotencyDemoApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/demo/idempotency`;

  // Store checkpoints per entity
  private checkpointStore = new Map<string, string>();

  /**
   * Get work item with checkpoint fingerprint
   */
  getWorkItemWithCheckpoint(id: string): Observable<WorkItemWithCheckpoint> {
    return this.http.get<WorkItemWithCheckpoint>(`${this.baseUrl}/workitems/${id}/with-checkpoint`);
  }

  /**
   * Store a checkpoint for later use
   */
  storeCheckpoint(entityId: string, fingerprint: string): void {
    this.checkpointStore.set(entityId, fingerprint);
  }

  /**
   * Get stored checkpoint for an entity
   */
  getStoredCheckpoint(entityId: string): string | undefined {
    return this.checkpointStore.get(entityId);
  }

  /**
   * Complete work with checkpoint validation
   */
  completeWorkWithValidation(
    id: string,
    outcome: string,
    useCheckpoint: boolean = true
  ): Observable<CompleteWorkValidatedResult> {
    let headers = new HttpHeaders();

    if (useCheckpoint) {
      const checkpoint = this.checkpointStore.get(id);
      if (checkpoint) {
        headers = headers.set('X-Decision-Checkpoint', checkpoint);
      }
    }

    return this.http.post<CompleteWorkValidatedResult>(
      `${this.baseUrl}/workitems/${id}/complete-validated`,
      { outcome },
      { headers }
    );
  }

  /**
   * Simulate a stale decision for testing
   */
  simulateStaleDecision(id: string, staleVersion?: number): Observable<StaleDecisionSimulation> {
    return this.http.post<StaleDecisionSimulation>(
      `${this.baseUrl}/workitems/${id}/simulate-stale-decision`,
      { staleVersion }
    );
  }

  /**
   * Complete work with webhook (may fail)
   */
  completeWorkWithWebhook(id: string, outcome: string): Observable<WebhookResult> {
    return this.http.post<WebhookResult>(
      `${this.baseUrl}/workitems/${id}/complete-with-webhook`,
      { outcome }
    );
  }

  /**
   * Configure webhook behavior
   */
  configureWebhook(config: WebhookConfig): Observable<WebhookConfigResult> {
    return this.http.post<WebhookConfigResult>(
      `${this.baseUrl}/webhook/configure`,
      config
    );
  }

  /**
   * Get current webhook configuration
   */
  getWebhookStatus(): Observable<WebhookStatus> {
    return this.http.get<WebhookStatus>(`${this.baseUrl}/webhook/status`);
  }
}

// Types for the idempotency demo

export interface WorkItemWithCheckpoint {
  id: string;
  title: string;
  description: string;
  status: string;
  priority: number;
  assignedTo?: string;
  checkpoint: {
    fingerprint: string;
    streamId: string;
    version: number;
    message: string;
  };
}

export interface CompleteWorkValidatedResult {
  success: boolean;
  message?: string;
  error?: string;
  details?: {
    streamId: string;
    expectedVersion: number;
    actualVersion: number;
  };
  suggestion?: string;
  newCheckpoint?: {
    fingerprint: string;
    streamId: string;
    version: number;
  };
}

export interface StaleDecisionSimulation {
  scenario: string;
  steps: string[];
  validationResult: {
    isValid: boolean;
    message?: string;
    expectedVersion?: number;
    actualVersion?: number;
  };
  recommendation: string;
}

export interface WebhookResult {
  success: boolean;
  message?: string;
  warning?: string;
  details?: {
    errorCode: string;
    streamId: string;
    committedEventCount: number;
    committedVersionRange: { item1: number; item2: number };
    failedActions: string[];
    succeededActions: string[];
    firstError?: string;
  };
  recommendation?: string;
  webhookConfig?: {
    simulateFailure: boolean;
    failFirstNAttempts: number;
  };
}

export interface WebhookConfig {
  simulateFailure?: boolean;
  failFirstNAttempts?: number;
  simulatedLatencyMs?: number;
}

export interface WebhookConfigResult {
  message: string;
  config: {
    simulateFailure: boolean;
    failFirstNAttempts: number;
    simulatedLatencyMs: number;
  };
}

export interface WebhookStatus {
  config: {
    simulateFailure: boolean;
    failFirstNAttempts: number;
    simulatedLatencyMs: number;
  };
  description: {
    simulateFailure: string;
    failFirstNAttempts: string;
    simulatedLatencyMs: string;
  };
}
