import { ChangeDetectorRef, DestroyRef, Pipe, PipeTransform } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { Subscription } from 'rxjs';

import { LanguageService } from './language.service';

@Pipe({
  name: 'translate',
  standalone: true,
  pure: false,
})
export class TranslatePipe implements PipeTransform {
  private latest = '';
  private lastKey = '';
  private readonly _dictionarySubscription: Subscription;

  constructor(
    private readonly language: LanguageService,
    private readonly cdr: ChangeDetectorRef,
    destroyRef: DestroyRef,
  ) {
    // keep subscription reference to satisfy rxjs lint rule
    this._dictionarySubscription = this.language.dictionary$
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(() => {
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
