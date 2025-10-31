import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MAT_SNACK_BAR_DATA, MatSnackBarModule, MatSnackBarRef } from '@angular/material/snack-bar';

import { ErrorDialogComponent, ErrorDialogData } from '../error-dialog/error-dialog.component';

interface ErrorToastData {
  context: string;
  detail: string;
}

@Component({
  standalone: true,
  selector: 'app-error-toast',
  templateUrl: './error-toast.component.html',
  styleUrls: ['./error-toast.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, MatSnackBarModule, MatButtonModule, MatIconModule],
})
export class ErrorToastComponent {
  readonly data = inject<ErrorToastData>(MAT_SNACK_BAR_DATA);
  private readonly snackRef = inject(MatSnackBarRef<ErrorToastComponent>);
  private readonly dialog = inject(MatDialog);

  retry(): void {
    if (typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent('pipelane:retry-action', { detail: this.data.context }));
    }
    this.snackRef.dismiss();
  }

  openDetails(): void {
    this.dialog.open<ErrorDialogComponent, ErrorDialogData>(ErrorDialogComponent, {
      data: {
        context: this.data.context,
        detail: this.data.detail,
      },
      panelClass: 'help-center-dialog',
    });
  }
}
