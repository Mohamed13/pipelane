import { Injectable } from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';

@Injectable({ providedIn: 'root' })
export class IconService {
  constructor(private readonly iconRegistry: MatIconRegistry) {
    this.iconRegistry.setDefaultFontSetClass('material-symbols-rounded');
    this.iconRegistry.registerFontClassAlias('material-symbols-rounded', 'msr');
  }
}
