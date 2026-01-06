import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-workitem-list',
  imports: [CommonModule, MatCardModule, MatIconModule],
  templateUrl: './workitem-list.component.html',
  styleUrl: './workitem-list.component.css'
})
export class WorkItemListComponent {}
