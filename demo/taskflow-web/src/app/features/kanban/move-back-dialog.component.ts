import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';

export interface MoveBackDialogResult {
  reason: string;
  isAccidental: boolean;
  confirmed: boolean;
}

@Component({
  selector: 'app-move-back-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule,
  ],
  templateUrl: './move-back-dialog.component.html',
  styleUrl: './move-back-dialog.component.css'
})
export class MoveBackDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<MoveBackDialogComponent>);

  reason = '';
  isAccidental = false;

  onCancel(): void {
    this.dialogRef.close({ confirmed: false, reason: '', isAccidental: false });
  }

  onConfirm(): void {
    this.dialogRef.close({
      confirmed: true,
      reason: this.reason.trim(),
      isAccidental: this.isAccidental
    });
  }
}
