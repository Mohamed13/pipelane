import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AsyncPipe, NgFor } from '@angular/common';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';

@Component({
  standalone: true,
  selector: 'pl-contacts-list',
  imports: [FormsModule, NgFor, AsyncPipe],
  template: `
  <h2>Contacts</h2>
  <form (ngSubmit)="search()">
    <input placeholder="Search" [(ngModel)]="q" name="q" />
    <button type="submit">Search</button>
  </form>
  <ul>
    <li *ngFor="let c of (results | async)?.items" (click)="open(c)" style="cursor:pointer; padding:.25rem 0;">
      {{c.firstName}} {{c.lastName}} — {{c.phone || c.email}}
    </li>
  </ul>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ContactsListComponent {
  private api = inject(ApiService);
  private router = inject(Router);
  q = '';
  results = this.api.searchContacts('');
  search(){ this.results = this.api.searchContacts(this.q); }
  open(c:any){ this.router.navigate(['/conversations', c.id]); }
}

