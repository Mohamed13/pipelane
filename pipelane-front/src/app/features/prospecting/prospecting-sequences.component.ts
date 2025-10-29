import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ApiService } from '../../core/api.service';
import {
  ProspectingSequence,
  ProspectingSequencePayload,
  SequenceStepType,
} from '../../core/models';

@Component({
  selector: 'pl-prospecting-sequences',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatTooltipModule,
    MatSnackBarModule,
  ],
  templateUrl: './prospecting-sequences.component.html',
  styleUrls: ['./prospecting-sequences.component.scss'],
})
export class ProspectingSequencesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly snackbar = inject(MatSnackBar);

  readonly sequences = signal<ProspectingSequence[]>([]);
  readonly loading = signal(false);

  readonly form: FormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    description: [''],
    isActive: [true],
    targetPersona: [''],
    entryCriteriaJson: [''],
    steps: this.fb.array([]),
  });

  ngOnInit(): void {
    this.addStep();
    this.loadSequences();
  }

  get steps(): FormArray {
    return this.form.get('steps') as FormArray;
  }

  addStep(): void {
    this.steps.push(
      this.fb.group({
        stepType: ['email' as SequenceStepType, Validators.required],
        channel: ['email', Validators.required],
        offsetDays: [
          this.steps.length === 0 ? 0 : this.steps.length * 3,
          [Validators.required, Validators.min(0)],
        ],
        promptTemplate: [''],
        subjectTemplate: [''],
        guardrailInstructions: [''],
        requiresApproval: [false],
      }),
    );
  }

  removeStep(index: number): void {
    if (this.steps.length > 1) {
      this.steps.removeAt(index);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = this.form.value as ProspectingSequencePayload;
    this.loading.set(true);
    this.api
      .createProspectingSequence(payload)
      .subscribe({
        next: (sequence) => {
          this.snackbar?.open('Sequence created', 'Dismiss', { duration: 4000 });
          this.form.reset({ isActive: true, steps: [] });
          this.steps.clear();
          this.addStep();
          this.sequences.update((current) => [sequence, ...current]);
        },
        error: () => {
          this.snackbar?.open('Failed to create sequence', 'Dismiss', { duration: 5000 });
        },
      })
      .add(() => this.loading.set(false));
  }

  loadSequences(): void {
    this.loading.set(true);
    this.api
      .getProspectingSequences()
      .subscribe({
        next: (sequences) => this.sequences.set(sequences),
        error: () => this.snackbar?.open('Unable to load sequences', 'Dismiss', { duration: 5000 }),
      })
      .add(() => this.loading.set(false));
  }
}
