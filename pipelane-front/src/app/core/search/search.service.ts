import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable, forkJoin, of, timer } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';

import { ApiService } from '../api.service';
import { Contact, ListSummary, ProspectingCampaign, ProspectingCampaignStatus } from '../models';

export type CommandType = 'prospect' | 'conversation' | 'campaign' | 'list';

export interface CommandItem {
  id: string;
  label: string;
  type: CommandType;
  subtitle?: string;
  route?: string;
  icon?: string;
}

export interface SearchFilters {
  type?: CommandType;
}

@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly api = inject(ApiService);
  private readonly recentKey = 'pl_command_palette_recent';
  private readonly recentSubject = new BehaviorSubject<string[]>(this.loadRecents());

  readonly recent$ = this.recentSubject.asObservable();

  search(term: string, filters?: SearchFilters): Observable<CommandItem[]> {
    const query = term?.trim();
    if (!query) {
      return of([]);
    }
    const type = filters?.type ?? null;
    return timer(200).pipe(switchMap(() => this.fetchResults(query, type)));
  }

  recordRecent(term: string): void {
    const normalized = term.trim();
    if (!normalized) {
      return;
    }
    const current = this.recentSubject.value.filter(
      (value) => value.localeCompare(normalized, undefined, { sensitivity: 'accent' }) !== 0,
    );
    current.unshift(normalized);
    if (current.length > 10) {
      current.length = 10;
    }
    this.recentSubject.next([...current]);
    this.persistRecents(current);
  }

  clearRecents(): void {
    this.recentSubject.next([]);
    this.persistRecents([]);
  }

  private fetchResults(query: string, type: CommandType | null): Observable<CommandItem[]> {
    const wantsProspects = !type || type === 'prospect';
    const wantsConversations = !type || type === 'conversation';
    const wantsCampaigns = !type || type === 'campaign';
    const wantsLists = !type || type === 'list';

    const tasks: Observable<CommandItem[]>[] = [];

    if (wantsProspects || wantsConversations) {
      tasks.push(
        this.api.searchContacts(query, 1, 6).pipe(
          map((response) => {
            const contacts = Array.isArray(response.items) ? response.items : [];
            const result: CommandItem[] = [];
            if (wantsProspects) {
              result.push(...contacts.map((contact) => this.toProspectItem(contact)));
            }
            if (wantsConversations) {
              result.push(...contacts.map((contact) => this.toConversationItem(contact)));
            }
            return result;
          }),
          catchError(() => of([])),
        ),
      );
    }

    if (wantsCampaigns) {
      tasks.push(
        this.api.getProspectingCampaigns().pipe(
          map((campaigns) =>
            (Array.isArray(campaigns) ? campaigns : [])
              .filter((campaign) => this.includesQuery(campaign.name, query))
              .sort((a, b) => a.name.localeCompare(b.name))
              .slice(0, 6)
              .map((campaign) => this.toCampaignItem(campaign)),
          ),
          catchError(() => of([])),
        ),
      );
    }

    if (wantsLists) {
      tasks.push(
        this.api.listSummaries().pipe(
          map((lists) =>
            (Array.isArray(lists) ? lists : [])
              .filter((list) => this.includesQuery(list.name, query))
              .sort((a, b) => this.displayListName(a).localeCompare(this.displayListName(b)))
              .slice(0, 6)
              .map((list) => this.toListItem(list)),
          ),
          catchError(() => of([])),
        ),
      );
    }

    if (!tasks.length) {
      return of([]);
    }

    return forkJoin(tasks).pipe(map((groups) => groups.flat()));
  }

  private toProspectItem(contact: Contact): CommandItem {
    const label = this.displayContactName(contact);
    const subtitle = this.buildContactSubtitle(contact);
    return {
      id: `prospect:${contact.id}`,
      label,
      subtitle,
      route: `/contacts?highlight=${encodeURIComponent(contact.id)}`,
      type: 'prospect',
      icon: 'person',
    };
  }

  private toConversationItem(contact: Contact): CommandItem {
    const label = this.displayContactName(contact);
    const subtitle =
      this.normalize(contact.email) ?? this.normalize(contact.phone) ?? 'Ouvrir la conversation';
    return {
      id: `conversation:${contact.id}`,
      label,
      subtitle,
      route: `/conversations/${contact.id}`,
      type: 'conversation',
      icon: 'forum',
    };
  }

  private toCampaignItem(campaign: ProspectingCampaign): CommandItem {
    const status = this.prettyStatus(campaign.status);
    return {
      id: `campaign:${campaign.id}`,
      label: campaign.name || 'Campagne sans nom',
      subtitle: status,
      route: `/prospecting/campaigns/${campaign.id}`,
      type: 'campaign',
      icon: 'flag',
    };
  }

  private toListItem(list: ListSummary): CommandItem {
    const label = this.displayListName(list);
    const count = typeof list.count === 'number' ? list.count : 0;
    const suffix = count === 1 ? 'prospect' : 'prospects';
    return {
      id: `list:${list.id}`,
      label,
      subtitle: `${count} ${suffix}`,
      route: `/lists/${list.id}`,
      type: 'list',
      icon: 'list_alt',
    };
  }

  private displayContactName(contact: Contact): string {
    const pieces = [this.normalize(contact.firstName), this.normalize(contact.lastName)].filter(
      Boolean,
    );
    if (pieces.length) {
      return pieces.join(' ').trim();
    }
    return this.normalize(contact.email) ?? this.normalize(contact.phone) ?? 'Contact sans nom';
  }

  private buildContactSubtitle(contact: Contact): string | undefined {
    const parts = [this.normalize(contact.email), this.normalize(contact.phone)].filter(Boolean);
    return parts.length ? parts.join(' · ') : undefined;
  }

  private displayListName(list: ListSummary): string {
    return this.normalize(list.name) ?? 'Liste sans nom';
  }

  private prettyStatus(status: ProspectingCampaignStatus): string {
    if (!status) {
      return 'Statut inconnu';
    }
    return status
      .toString()
      .replace(/_/g, ' ')
      .toLowerCase()
      .replace(/(^|\s)\w/g, (match) => match.toUpperCase());
  }

  private includesQuery(value: string | null | undefined, query: string): boolean {
    const normalized = this.normalize(value);
    if (!normalized) {
      return false;
    }
    return normalized.toLowerCase().includes(query.toLowerCase());
  }

  private normalize(value: string | null | undefined): string | undefined {
    const trimmed = value?.trim();
    return trimmed && trimmed.length ? trimmed : undefined;
  }

  private loadRecents(): string[] {
    try {
      const raw = localStorage.getItem(this.recentKey);
      if (!raw) {
        return [];
      }
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed.filter((item) => typeof item === 'string') : [];
    } catch {
      return [];
    }
  }

  private persistRecents(values: string[]): void {
    try {
      localStorage.setItem(this.recentKey, JSON.stringify(values));
    } catch {
      // ignore storage failures (private browsing, etc.)
    }
  }
}
