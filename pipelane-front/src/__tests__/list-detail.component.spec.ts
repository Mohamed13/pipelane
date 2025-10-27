import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { convertToParamMap, ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

import { ApiService } from '../app/core/api.service';
import { ProspectListResponse } from '../app/core/models';
import { ListDetailComponent } from '../app/features/lists/list-detail.component';

const snackbarStub = { open: jest.fn() };

describe('ListDetailComponent', () => {
  let component: ListDetailComponent;
  let api: jest.Mocked<ApiService>;

  const list: ProspectListResponse = {
    id: 'list-123',
    name: 'VIP',
    createdAtUtc: new Date().toISOString(),
    updatedAtUtc: new Date().toISOString(),
    items: [
      {
        prospectId: 'prospect-1',
        prospect: {
          company: 'Acme',
          email: 'contact@acme.com',
          phone: '+33123456789',
          city: 'Paris',
          country: 'France',
          website: 'https://acme.com',
          firstName: 'Alex',
          lastName: 'Martin',
          whatsAppMsisdn: null,
          social: null,
        },
        score: 85,
        features: {
          rating: 4.2,
          reviews: 120,
          hasSite: true,
          booking: false,
          socialActive: true,
          cms: 'WordPress',
          mobileOk: true,
          pixelPresent: true,
          lcpSlow: false,
        },
        why: ['Réputation solide'],
        addedAtUtc: new Date().toISOString(),
      },
    ],
  };

  beforeEach(async () => {
    api = {
      getList: jest.fn().mockReturnValue(of(list)),
      createCadenceFromList: jest.fn().mockReturnValue(of(void 0)),
    } as unknown as jest.Mocked<ApiService>;

    await TestBed.configureTestingModule({
      imports: [ListDetailComponent],
      providers: [
        { provide: ApiService, useValue: api },
        { provide: MatSnackBar, useValue: snackbarStub },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ id: 'list-123' })),
          },
        },
      ],
    })
      .overrideComponent(ListDetailComponent, {
        set: { template: '' },
      })
      .compileComponents();

    const fixture = TestBed.createComponent(ListDetailComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  it('loads the list on init', () => {
    expect(api.getList).toHaveBeenCalledWith('list-123');
    expect(component.list()).toEqual(list);
    expect(component.loading()).toBe(false);
  });

  it('creates cadence from list when requested', () => {
    component['list'].set(list);

    component.createCadence();

    expect(api.createCadenceFromList).toHaveBeenCalledWith({ listId: 'list-123' });
  });
});
