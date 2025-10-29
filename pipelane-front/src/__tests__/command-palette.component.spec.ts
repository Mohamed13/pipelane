import { BehaviorSubject, of } from 'rxjs';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { CommandItem, SearchService } from '../app/core/search/search.service';
import { LanguageService, LangCode } from '../app/core/i18n/language.service';
import { CommandPaletteComponent } from '../app/core/search/command-palette.component';
import { MatDialogRef } from '@angular/material/dialog';
import { Router } from '@angular/router';

class SearchStub {
  recent$ = new BehaviorSubject<string[]>(['Apollo', 'Sequences']);
  search = jest.fn().mockReturnValue(
    of<CommandItem[]>([
      {
        id: 'campaign:cmp-1',
        label: 'Campagne Apollo',
        subtitle: 'Running',
        route: '/prospecting/campaigns/cmp-1',
        type: 'campaign',
        icon: 'flag',
      },
      {
        id: 'list:list-1',
        label: 'Sequences prioritaires',
        subtitle: '15 prospects',
        route: '/lists/list-1',
        type: 'list',
        icon: 'list_alt',
      },
    ]),
  );
  recordRecent = jest.fn();
  clearRecents = jest.fn();
}

class LanguageStub {
  private readonly dict: Record<string, string> = {
    'search.placeholder': 'Rechercher (prospects, conversations, campagnes…)',
    'search.inputLabel': 'Rechercher',
    'search.recent': 'Recherches récentes',
    'search.clearHistory': "Effacer l'historique",
    'search.noRecent': 'Tapez une requête pour l’ajouter à votre historique.',
    'search.noResults': 'Aucun résultat pour',
    'search.filters.ariaLabel': 'Filtrer les résultats de recherche',
    'search.filters.all': 'Tous',
    'search.filters.prospects': 'Prospects',
    'search.filters.conversations': 'Conversations',
    'search.filters.campaigns': 'Campagnes',
    'search.filters.lists': 'Listes',
    'search.groups.prospects': 'Prospects',
    'search.groups.conversations': 'Conversations',
    'search.groups.campaigns': 'Campagnes',
    'search.groups.lists': 'Listes',
    'search.groups.default': 'Résultats',
    'common.close': 'Fermer',
  };

  current = signal<LangCode>('fr');
  dictionary$ = of(this.dict);
  set = jest.fn((lang: LangCode) => this.current.set(lang));
  translate = jest.fn((key: string) => this.dict[key] ?? key);
}

describe('CommandPaletteComponent', () => {
  let searchStub: SearchStub;
  let navigateMock: jest.Mock;
  let dialogCloseMock: jest.Mock;
  let languageStub: LanguageStub;

  beforeEach(async () => {
    searchStub = new SearchStub();
    navigateMock = jest.fn().mockResolvedValue(true);
    dialogCloseMock = jest.fn();
    languageStub = new LanguageStub();

    await TestBed.configureTestingModule({
      imports: [CommandPaletteComponent],
      providers: [
        { provide: SearchService, useValue: searchStub },
        { provide: Router, useValue: { navigateByUrl: navigateMock } },
        { provide: MatDialogRef, useValue: { close: dialogCloseMock } },
        { provide: LanguageService, useValue: languageStub },
      ],
    }).compileComponents();
  });

  it('navigates through results with keyboard and selects the active item', () => {
    const fixture = TestBed.createComponent(CommandPaletteComponent);
    const component = fixture.componentInstance;

    fixture.detectChanges();

    component.queryControl.setValue('apollo');
    fixture.detectChanges();

    expect(component['activeIndex']()).toBe(0);

    component.handleKeydown(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    expect(component['activeIndex']()).toBe(1);

    component.handleKeydown(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(searchStub.recordRecent).toHaveBeenCalledWith('apollo');
    expect(navigateMock).toHaveBeenCalledWith('/lists/list-1');
    expect(dialogCloseMock).toHaveBeenCalledWith(expect.objectContaining({ id: 'list:list-1' }));
  });

  it('lets users reuse a recent search from the history list', () => {
    const fixture = TestBed.createComponent(CommandPaletteComponent);
    fixture.detectChanges();

    const host: HTMLElement = fixture.nativeElement;
    const recentButton = host.querySelector<HTMLButtonElement>('.recent-item');
    expect(recentButton).toBeTruthy();

    recentButton?.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.queryControl.getRawValue()).toBe('Apollo');
    fixture.componentInstance.queryControl.setValue('');
    fixture.detectChanges();

    const clearButton = host.querySelector<HTMLButtonElement>('.recent-clear');
    clearButton?.click();
    expect(searchStub.clearRecents).toHaveBeenCalled();
  });
});
