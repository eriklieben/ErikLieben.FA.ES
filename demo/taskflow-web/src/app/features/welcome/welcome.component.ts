import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';

interface QuickStartStep {
  number: number;
  title: string;
  description: string;
  icon: string;
  link: string;
  linkText: string;
}

interface FeatureCard {
  title: string;
  description: string;
  icon: string;
  link: string;
  color: string;
}

@Component({
  selector: 'app-welcome',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './welcome.component.html',
  styleUrl: './welcome.component.css'
})
export class WelcomeComponent {
  readonly quickStartSteps: QuickStartStep[] = [
    {
      number: 1,
      title: 'Generate Demo Data',
      description: 'Populate the application with sample projects, work items, and users. This creates realistic data that demonstrates all the event sourcing features.',
      icon: 'data_object',
      link: '/demo-data',
      linkText: 'Generate Data'
    },
    {
      number: 2,
      title: 'Explore the Application',
      description: 'Browse projects, view work items on the Kanban board, and interact with the app. Every action you take generates events that are stored in the event stream.',
      icon: 'apps',
      link: '/dashboard',
      linkText: 'Go to Dashboard'
    },
    {
      number: 3,
      title: 'See Event Sourcing in Action',
      description: 'Use Time Travel to replay events and see how state changes over time. Explore Projections to understand how read models are built from events.',
      icon: 'bolt',
      link: '/time-travel',
      linkText: 'Try Time Travel'
    },
    {
      number: 4,
      title: 'Learn the Framework',
      description: 'Read the documentation to understand how to build your own event-sourced applications using the ErikLieben.FA.ES framework.',
      icon: 'menu_book',
      link: '/docs/getting-started',
      linkText: 'Read Docs'
    }
  ];

  readonly featureCards: FeatureCard[] = [
    {
      title: 'Events',
      description: 'Immutable records of state changes',
      icon: 'bolt',
      link: '/docs/events',
      color: 'var(--ctp-yellow)'
    },
    {
      title: 'Aggregates',
      description: 'Domain objects with business logic',
      icon: 'account_tree',
      link: '/docs/aggregates',
      color: 'var(--ctp-blue)'
    },
    {
      title: 'Projections',
      description: 'Read models built from events',
      icon: 'view_quilt',
      link: '/projections',
      color: 'var(--ctp-green)'
    },
    {
      title: 'Time Travel',
      description: 'Replay to any point in time',
      icon: 'history',
      link: '/time-travel',
      color: 'var(--ctp-mauve)'
    },
    {
      title: 'Upcasting',
      description: 'Transform old events on read',
      icon: 'upgrade',
      link: '/event-upcasting',
      color: 'var(--ctp-peach)'
    },
    {
      title: 'Versioning',
      description: 'Handle schema changes safely',
      icon: 'schema',
      link: '/event-versioning',
      color: 'var(--ctp-teal)'
    }
  ];
}
