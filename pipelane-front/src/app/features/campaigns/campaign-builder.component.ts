import { CommonModule, JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal, computed } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { MatStepperModule } from '@angular/material/stepper';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDividerModule } from '@angular/material/divider';

import { ApiService } from '../../core/api.service';
import { CampaignCreatePayload, Channel, ChannelLabels, TemplateSummary } from '../../core/models';

@Component({
  standalone: true,
  selector: 'pl-campaign-builder',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    JsonPipe,
    MatCardModule,
    MatStepperModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatChipsModule,
    MatSnackBarModule,
    MatDividerModule,
  ],
  template: `
    <mat-card class="surface-card">
      <h2>Campaign Builder</h2>
      <p class="body-text-muted">Create a multi-channel campaign with fallbacks and schedule.</p>

      <mat-horizontal-stepper [linear]="true" [formGroup]="form">
        <mat-step [stepControl]="detailsGroup">
          <ng-template matStepLabel>Campaign details</ng-template>
          <div [formGroup]="detailsGroup" class="step-content">
            <mat-form-field appearance="outline">
              <mat-label>Name</mat-label>
              <input matInput formControlName="name" placeholder="e.g. Spring Promo" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Primary channel</mat-label>
              <mat-select formControlName="primaryChannel">
                <mat-option *ngFor="let channel of channels" [value]="channel">
                  {{ ChannelLabels[channel] }}
                </mat-option>
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Fallback channels</mat-label>
              <mat-select formControlName="fallbackOrder" multiple>
                <mat-option *ngFor="let channel of fallbackOptions()" [value]="channel">
                  {{ ChannelLabels[channel] }}
                </mat-option>
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Batch size</mat-label>
              <input matInput type="number" formControlName="batchSize" min="1" placeholder="Optional" />
            </mat-form-field>

            <div class="chip-row" *ngIf="detailsGroup.value.fallbackOrder?.length">
              <mat-chip *ngFor="let channel of detailsGroup.value.fallbackOrder" color="accent" selected>
                {{ ChannelLabels[channel] }}
              </mat-chip>
            </div>

            <div class="actions">
              <button mat-raised-button color="primary" matStepperNext [disabled]="detailsGroup.invalid">
                Next
                <mat-icon>arrow_forward</mat-icon>
              </button>
            </div>
          </div>
        </mat-step>

        <mat-step [stepControl]="templateGroup">
          <ng-template matStepLabel>Template & schedule</ng-template>
          <div [formGroup]="templateGroup" class="step-content">
            <mat-form-field appearance="outline">
              <mat-label>Template</mat-label>
              <mat-select formControlName="templateId">
                <mat-option *ngFor="let template of templates()" [value]="template.id">
                  {{ template.name }} ({{ template.lang.toUpperCase() }} · {{ template.channel | titlecase }})
                </mat-option>
              </mat-select>
            </mat-form-field>

            <div class="schedule-grid">
              <mat-form-field appearance="outline">
                <mat-label>Schedule date (UTC)</mat-label>
                <input matInput [matDatepicker]="picker" formControlName="scheduledDate" />
                <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
                <mat-datepicker #picker></mat-datepicker>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Schedule time (UTC)</mat-label>
                <input matInput type="time" formControlName="scheduledTime" />
              </mat-form-field>
            </div>

            <mat-form-field appearance="outline" class="segment">
              <mat-label>Audience segment (JSON)</mat-label>
              <textarea matInput formControlName="segmentJson" rows="4"></textarea>
            </mat-form-field>
            <div class="preview" *ngIf="previewCount() !== null || previewLoading()">
              <mat-icon>{{ previewLoading() ? 'hourglass_top' : 'groups' }}</mat-icon>
              <span *ngIf="!previewLoading()">Potential recipients: {{ previewCount() }}</span>
              <span *ngIf="previewLoading()">Calculating…</span>
            </div>

            <pre *ngIf="summary() as payload">{{ payload | json }}</pre>

            <div class="actions">
              <button mat-button matStepperPrevious>
                <mat-icon>arrow_back</mat-icon>
                Back
              </button>
              <button mat-raised-button color="primary" (click)="createCampaign()" [disabled]="templateGroup.invalid || creating()">
                <mat-icon>check</mat-icon>
                Launch campaign
              </button>
            </div>
          </div>
        </mat-step>
      </mat-horizontal-stepper>
    </mat-card>
  `,
  styles: [
    `
      .step-content { display:flex; flex-direction:column; gap:var(--space-3); }
      .chip-row { display:flex; gap:var(--space-2); flex-wrap:wrap; }
      .actions { display:flex; justify-content:flex-end; gap:var(--space-3); margin-top: var(--space-4); }
      .segment textarea { font-family: 'JetBrains Mono', monospace; }
      .schedule-grid { display:grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: var(--space-3); }
      .preview { display:flex; align-items:center; gap:var(--space-2); color: var(--color-text-muted); }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignBuilderComponent {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly snackbar = inject(MatSnackBar);

  readonly ChannelLabels = ChannelLabels;
  readonly channels: Channel[] = ['whatsapp', 'email', 'sms'];

  templates = signal<TemplateSummary[]>([]);
  creating = signal(false);
  previewLoading = signal(false);
  previewCount = signal<number | null>(null);

  readonly form: FormGroup = this.fb.group({
    details: this.fb.group({
      name: ['Untitled campaign', [Validators.required, Validators.minLength(3)]],
      primaryChannel: ['whatsapp' as Channel, Validators.required],
      fallbackOrder: this.fb.control<Channel[]>([]),
      batchSize: this.fb.control<number | null>(null, Validators.min(1)),
    }),
    template: this.fb.group({
      templateId: ['', Validators.required],
      scheduledDate: [null as Date | null],
      scheduledTime: [''],
      segmentJson: ['{}', Validators.required],
    }),
  });

  get detailsGroup() {
    return this.form.get('details') as FormGroup;
  }

  get templateGroup() {
    return this.form.get('template') as FormGroup;
  }

  constructor() {
    this.api.getTemplates().subscribe({
      next: (templates) => this.templates.set(templates ?? []),
    });

    this.templateGroup
      .get('segmentJson')!
      .valueChanges.pipe(debounceTime(600), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((segment) => this.updatePreview(segment ?? '{}'));

    this.updatePreview(this.templateGroup.get('segmentJson')!.value as string); // initial preview
  }

  fallbackOptions(): Channel[] {
    const primary = this.detailsGroup.value.primaryChannel as Channel;
    return this.channels.filter((ch) => ch !== primary);
  }

  summary = computed<CampaignCreatePayload | null>(() => {
    if (this.form.invalid) return null;
    return this.buildPayload();
  });

  createCampaign(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const payload = this.buildPayload();
    if (!payload) return;
    this.creating.set(true);
    this.api.createCampaign(payload).subscribe({
      next: (res) => {
        this.creating.set(false);
        this.snackbar.open(`Campaign created (ID: ${res.id})`, 'Close', { duration: 4000 });
        this.form.reset({
          details: { name: 'Untitled campaign', primaryChannel: 'whatsapp', fallbackOrder: [], batchSize: null },
          template: { templateId: '', scheduledDate: null, scheduledTime: '', segmentJson: '{}' },
        });
        this.previewCount.set(null);
      },
      error: () => {
        this.creating.set(false);
        this.snackbar.open('Failed to create campaign', 'Dismiss', { duration: 4000 });
      },
    });
  }

  private buildPayload(): CampaignCreatePayload | null {
    if (this.form.invalid) return null;
    const details = this.detailsGroup.value;
    const template = this.templateGroup.value;
    const scheduledAtUtc = combineDateTime(template.scheduledDate, template.scheduledTime);

    return {
      name: details.name!,
      primaryChannel: details.primaryChannel as Channel,
      fallbackOrderJson: (details.fallbackOrder ?? []).length ? JSON.stringify(details.fallbackOrder) : null,
      templateId: template.templateId!,
      segmentJson: template.segmentJson ?? '{}',
      scheduledAtUtc,
      batchSize: details.batchSize ? Number(details.batchSize) : null,
    };
  }

  private updatePreview(segmentJson: string) {
    this.previewLoading.set(true);
    this.api.previewFollowups(segmentJson).subscribe({
      next: (res) => {
        this.previewLoading.set(false);
        this.previewCount.set(res?.count ?? 0);
      },
      error: () => {
        this.previewLoading.set(false);
        this.previewCount.set(null);
      },
    });
  }
}

function combineDateTime(date: Date | null, time: string | null): string | null {
  if (!date) return null;
  const base = new Date(date);
  const [hours, minutes] = time?.split(':') ?? [];
  const h = hours !== undefined ? Number(hours) : 0;
  const m = minutes !== undefined ? Number(minutes) : 0;
  const iso = new Date(Date.UTC(base.getFullYear(), base.getMonth(), base.getDate(), h, m, 0, 0));
  return iso.toISOString();
}
