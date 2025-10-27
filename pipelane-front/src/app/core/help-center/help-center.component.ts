import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';

interface ShortcutItem {
  combo: string;
  description: string;
}

interface HelpLink {
  label: string;
  url: string;
  icon: string;
}

@Component({
  standalone: true,
  selector: 'app-help-center-dialog',
  templateUrl: './help-center.component.html',
  styleUrls: ['./help-center.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, MatDialogModule, MatListModule, MatIconModule, MatButtonModule],
})
export class HelpCenterComponent {
  constructor(private readonly dialogRef: MatDialogRef<HelpCenterComponent>) {}

  readonly shortcuts: ShortcutItem[] = [
    { combo: 'Ctrl/⌘ + K', description: 'Ouvrir la recherche globale' },
    { combo: 'G, puis H', description: 'Aller sur Hunter' },
    { combo: 'G, puis A', description: 'Ouvrir Analytics' },
    { combo: 'N, puis C', description: 'Nouvelle cadence' },
    { combo: 'Shift + ?', description: 'Afficher ce centre d’aide' },
  ];

  readonly links: HelpLink[] = [
    { label: 'Documentation Lead Hunter AI', url: 'https://www.pipelane.app/prospection-ia', icon: 'rocket_launch' },
    { label: 'Relance intelligente', url: 'https://www.pipelane.app/relance-intelligente', icon: 'timelapse' },
    { label: 'Contacter le support', url: 'mailto:support@pipelane.app', icon: 'support_agent' },
  ];

  triggerReplay(): void {
    if (typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent('pipelane:replay-tour'));
    }
    this.dialogRef.close();
  }
}
