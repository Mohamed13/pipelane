import { ChangeDetectorRef, DestroyRef, Pipe, PipeTransform } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { LanguageService } from './language.service';

@Pipe({
  name: 'translate',
  standalone: true,
  pure: false,
})
export class TranslatePipe implements PipeTransform {
  private latest = '';
  private lastKey = '';

  constructor(
    private readonly language: LanguageService,
    private readonly cdr: ChangeDetectorRef,
    destroyRef: DestroyRef,
  ) {
    this.language.dictionary$.pipe(takeUntilDestroyed(destroyRef)).subscribe(() => {
      if (!this.lastKey) {
        return;
      }
      this.latest = this.language.translate(this.lastKey);
      this.cdr.markForCheck();
    });
  }

  transform(key: string | null | undefined): string {
    if (!key) {
      this.lastKey = '';
      this.latest = '';
      return '';
    }
    this.lastKey = key;
    this.latest = this.language.translate(key);
    return this.latest;
  }
}
