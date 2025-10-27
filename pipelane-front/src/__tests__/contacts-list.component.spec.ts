import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';

import { ApiService } from '../app/core/api.service';
import { ContactsListComponent } from '../app/features/contacts/contacts-list.component';

describe('ContactsListComponent lifecycle', () => {
  beforeEach(async () => {
    const apiStub = {
      searchContacts: jest.fn().mockReturnValue(of({ total: 0, items: [] })),
    };
    const routerStub = { navigate: jest.fn() };

    await TestBed.configureTestingModule({
      imports: [ContactsListComponent],
      providers: [
        { provide: ApiService, useValue: apiStub },
        { provide: Router, useValue: routerStub },
      ],
    })
      .overrideComponent(ContactsListComponent, {
        set: { template: '' },
      })
      .compileComponents();
  });

  it('guards paginator and sort assignments in ngAfterViewInit', () => {
    const fixture = TestBed.createComponent(ContactsListComponent);
    const component = fixture.componentInstance;

    component['paginator'] = undefined as never;
    component['sort'] = undefined as never;

    expect(() => component.ngAfterViewInit()).not.toThrow();
  });
});
