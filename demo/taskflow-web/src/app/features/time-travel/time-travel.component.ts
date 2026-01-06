import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-time-travel',
  imports: [CommonModule, MatCardModule, MatIconModule],
  templateUrl: './time-travel.component.html',
  styleUrl: './time-travel.component.css'
})
export class TimeTravelComponent {}
