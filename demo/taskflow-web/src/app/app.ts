import { Component, computed, inject, signal, OnInit, OnDestroy, DestroyRef } from '@angular/core';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule, MatDrawerMode } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BreakpointObserver } from '@angular/cdk/layout';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { trigger, state, style, transition, animate } from '@angular/animations';
import { ThemeService } from './core/services/theme.service';
import { SignalRService } from './core/services/signalr.service';
import { UserContextService } from './core/services/user-context.service';
import { UserSelectorComponent } from './shared/components/user-selector.component';
import { HubConnectionState } from '@microsoft/signalr';

@Component({
  selector: 'app-root',
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule,
    MatSidenavModule,
    MatListModule,
    MatTooltipModule,
    MatDividerModule,
    UserSelectorComponent
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  animations: [
    trigger('expandCollapse', [
      state('collapsed', style({
        height: '0',
        opacity: '0',
        overflow: 'hidden'
      })),
      state('expanded', style({
        height: '*',
        opacity: '1'
      })),
      transition('collapsed <=> expanded', [
        animate('200ms ease-in-out')
      ])
    ])
  ]
})
export class App implements OnInit, OnDestroy {
  readonly router = inject(Router);
  readonly themeService = inject(ThemeService);
  readonly signalrService = inject(SignalRService);
  readonly userContext = inject(UserContextService);
  readonly snackBar = inject(MatSnackBar);
  private readonly breakpointObserver = inject(BreakpointObserver);
  private readonly destroyRef = inject(DestroyRef);

  readonly title = 'TaskFlow';
  readonly isMobile = signal(false);
  readonly sidenavMode = computed<MatDrawerMode>(() => this.isMobile() ? 'over' : 'side');
  readonly sidenavOpened = signal(true);

  // Collapsible section states (all expanded by default)
  readonly appSectionExpanded = signal(true);
  readonly testingSectionExpanded = signal(true);
  readonly esSectionExpanded = signal(true);
  readonly docsSectionExpanded = signal(true);

  // Section configuration with first routes
  private readonly sections = [
    { expanded: this.appSectionExpanded, firstRoute: '/dashboard' },
    { expanded: this.testingSectionExpanded, firstRoute: '/demo-data' },
    { expanded: this.esSectionExpanded, firstRoute: '/time-travel' },
    { expanded: this.docsSectionExpanded, firstRoute: '/docs/getting-started' }
  ];

  readonly themeIcon = computed(() =>
    this.themeService.theme() === 'dark' ? 'light_mode' : 'dark_mode'
  );

  readonly themeTooltip = computed(() =>
    `Switch to ${this.themeService.theme() === 'dark' ? 'light' : 'dark'} mode`
  );

  readonly connectionState = signal<HubConnectionState>(HubConnectionState.Disconnected);

  ngOnInit() {
    // Observe mobile breakpoint to switch sidenav mode
    this.breakpointObserver
      .observe('(max-width: 768px)')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(result => {
        this.isMobile.set(result.matches);
        if (result.matches) {
          this.sidenavOpened.set(false);
        }
      });

    // Connect to SignalR hub
    this.signalrService.connect().catch(err => {
      console.error('Failed to connect to SignalR:', err);
    });

    // Subscribe to connection state changes
    this.signalrService.connectionState.subscribe(state => {
      this.connectionState.set(state);
    });

    // Subscribe to projection updates globally
    this.signalrService.onProjectionUpdated.subscribe({
      next: (event) => {
        console.log('Global projection update received:', event);

        // Only show toast when projections are completed (idle state)
        if (event.projections.some(p => p.status === 'idle')) {
          const projectionDetails = event.projections
            .map(p => `${p.name} [${p.checkpointFingerprint.substring(0, 8)}...]`)
            .join(', ');

          this.snackBar.open(
            `⚡ DEV MODE | Projections Updated: ${projectionDetails}`,
            '✓',
            {
              duration: 5000,
              horizontalPosition: 'end',
              verticalPosition: 'bottom',
              panelClass: ['global-dev-toast']
            }
          );
        }
      },
      error: (error) => {
        console.error('Error receiving global projection update:', error);
      }
    });
  }

  ngOnDestroy() {
    this.signalrService.disconnect();
  }

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  toggleSidenav() {
    this.sidenavOpened.update(v => !v);
  }

  onSidenavClosed() {
    this.sidenavOpened.set(false);
  }

  get isConnected(): boolean {
    return this.connectionState() === HubConnectionState.Connected;
  }

  toggleSection(sectionIndex: number) {
    const section = this.sections[sectionIndex];
    const isCurrentlyExpanded = section.expanded();

    if (isCurrentlyExpanded) {
      // Closing: navigate to the section above
      section.expanded.set(false);
      this.navigateToSectionAbove(sectionIndex);
    } else {
      // Opening: navigate to first page of this section
      section.expanded.set(true);
      this.router.navigate([section.firstRoute]);
    }
  }

  private navigateToSectionAbove(closingIndex: number) {
    // Find the nearest expanded section above, or welcome page
    for (let i = closingIndex - 1; i >= 0; i--) {
      if (this.sections[i].expanded()) {
        this.router.navigate([this.sections[i].firstRoute]);
        return;
      }
    }
    // No expanded section above, go to welcome
    this.router.navigate(['/welcome']);
  }
}
