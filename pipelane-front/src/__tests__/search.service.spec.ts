import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiService } from '../app/core/api.service';
import { SearchService } from '../app/core/search/search.service';

describe('SearchService', () => {
  let service: SearchService;
  let apiMock: {
    searchContacts: jest.Mock;
    getProspectingCampaigns: jest.Mock;
    listSummaries: jest.Mock;
  };

  beforeEach(() => {
    localStorage.clear();
    apiMock = {
      searchContacts: jest.fn().mockReturnValue(
        of({
          total: 1,
          items: [
            {
              id: 'ct-1',
              firstName: 'Ada',
              lastName: 'Lovelace',
              email: 'ada@example.com',
              phone: '+33123456789',
              lang: 'fr',
              createdAt: '',
              updatedAt: '',
            },
          ],
        }),
      ),
      getProspectingCampaigns: jest.fn().mockReturnValue(
        of([
          {
            id: 'cmp-1',
            name: 'Campagne Apollo',
            sequenceId: 'seq-1',
            status: 'running',
            segmentJson: '{}',
            createdAtUtc: '',
            updatedAtUtc: '',
          },
        ]),
      ),
      listSummaries: jest.fn().mockReturnValue(
        of([
          {
            id: 'list-1',
            name: 'Salesforce Leads',
            count: 24,
            createdAtUtc: '',
            updatedAtUtc: '',
          },
        ]),
      ),
    };

    TestBed.configureTestingModule({
      providers: [{ provide: ApiService, useValue: apiMock }, SearchService],
    });

    service = TestBed.inject(SearchService);
  });

  it('debounces searches and cancels unsubscribed calls', fakeAsync(() => {
    const firstSub = service.search('alpha').subscribe();
    tick(100);
    expect(apiMock.searchContacts).not.toHaveBeenCalled();
    firstSub.unsubscribe();

    let results: string[] = [];
    service.search('beta').subscribe((items) => (results = items.map((item) => item.id)));

    tick(199);
    expect(apiMock.searchContacts).not.toHaveBeenCalled();

    tick(1);

    expect(apiMock.searchContacts).toHaveBeenCalledTimes(1);
    expect(apiMock.searchContacts).toHaveBeenCalledWith('beta', 1, 6);
    expect(results.length).toBeGreaterThan(0);
    expect(results.some((id) => id.startsWith('prospect:'))).toBe(true);
  }));

  it('filters results when a specific type is requested', fakeAsync(() => {
    apiMock.searchContacts.mockClear();

    let types: string[] = [];
    service
      .search('apollo', { type: 'campaign' })
      .subscribe((items) => (types = items.map((item) => item.type)));

    tick(200);

    expect(apiMock.searchContacts).not.toHaveBeenCalled();
    expect(apiMock.getProspectingCampaigns).toHaveBeenCalledTimes(1);
    expect(types).toEqual(['campaign']);
  }));

  it('records recent searches with deduplication and persistence', () => {
    const seen: string[][] = [];
    const sub = service.recent$.subscribe((value) => seen.push(value));

    service.recordRecent('Playbooks');
    service.recordRecent('playbooks ');
    service.recordRecent('Sequences');

    expect(seen.at(-1)).toEqual(['Sequences', 'playbooks']);
    expect(JSON.parse(localStorage.getItem('pl_command_palette_recent') ?? '[]')).toEqual([
      'Sequences',
      'playbooks',
    ]);

    sub.unsubscribe();
  });
});
