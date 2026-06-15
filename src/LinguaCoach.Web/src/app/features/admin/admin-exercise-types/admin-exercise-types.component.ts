import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnInit, signal } from '@angular/core';
import { AdminService } from '../../../core/services/admin.service';
import { ExerciseTypeDefinition } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-exercise-types',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="sp-admin-page">
      <div class="sp-admin-page-header">
        <p class="sp-eyebrow">Learning engine</p>
        <h1>Exercise types</h1>
        <p>
          Control which exercise types can be used for future SpeakPath lessons and Practice Gym generation.
          Planned types stay unavailable until their renderer and evaluator are ready.
        </p>
      </div>

      @if (error()) {
        <div class="sp-card" style="border-color:#fecaca;color:#991b1b">{{ error() }}</div>
      }

      <div class="sp-card" style="overflow:auto">
        <table style="width:100%;border-collapse:collapse;font-size:14px">
          <thead>
            <tr style="text-align:left;color:#64748b;border-bottom:1px solid #e2e8f0">
              <th style="padding:12px">Exercise</th>
              <th style="padding:12px">Skill</th>
              <th style="padding:12px">Category</th>
              <th style="padding:12px">Status</th>
              <th style="padding:12px">Surfaces</th>
              <th style="padding:12px">Needs</th>
              <th style="padding:12px">Generation</th>
              <th style="padding:12px">Item counts (min/def/max)</th>
              <th style="padding:12px">Option counts (min/def/max)</th>
              <th style="padding:12px">Action</th>
            </tr>
          </thead>
          <tbody>
            @for (type of exerciseTypes(); track type.key) {
              <tr style="border-bottom:1px solid #f1f5f9">
                <td style="padding:12px;min-width:240px">
                  <strong>{{ type.displayName }}</strong>
                  <div style="color:#64748b;font-size:12px">{{ type.key }}</div>
                  <div style="color:#64748b;font-size:12px">{{ type.description }}</div>
                </td>
                <td style="padding:12px">
                  <span style="text-transform:capitalize">{{ type.primarySkill }}</span>
                  @if (type.secondarySkills.length) {
                    <div style="color:#64748b;font-size:12px">+ {{ type.secondarySkills.join(', ') }}</div>
                  }
                </td>
                <td style="padding:12px">{{ type.category }}</td>
                <td style="padding:12px">
                  <span [style.color]="type.implementationStatus === 'ready' ? '#047857' : '#92400e'">
                    {{ type.implementationStatus === 'ready' ? 'Ready' : 'Not implemented' }}
                  </span>
                  <div style="font-size:12px;color:#64748b">{{ type.isEnabled ? 'Enabled' : 'Disabled' }}</div>
                </td>
                <td style="padding:12px;font-size:12px;color:#475569">
                  <div>Practice: {{ type.supportsPracticeGym ? 'yes' : 'no' }}</div>
                  <div>Today: {{ type.supportsTodayLesson ? 'yes' : 'no' }}</div>
                </td>
                <td style="padding:12px;font-size:12px;color:#475569">
                  <div>Audio: {{ type.requiresAudio ? 'yes' : 'no' }}</div>
                  <div>Image: {{ type.requiresImage ? 'yes' : 'no' }}</div>
                </td>
                <td style="padding:12px">
                  <span [style.color]="type.isAvailableForGeneration ? '#047857' : '#991b1b'">
                    {{ type.isAvailableForGeneration ? 'Available' : 'Blocked' }}
                  </span>
                </td>
                <td style="padding:12px;white-space:nowrap">
                  <input type="number" min="0" style="width:48px" [(ngModel)]="type.minItemsPerPractice" aria-label="min items" />
                  <input type="number" min="0" style="width:48px" [(ngModel)]="type.defaultItemsPerPractice" aria-label="default items" />
                  <input type="number" min="0" style="width:48px" [(ngModel)]="type.maxItemsPerPractice" aria-label="max items" />
                </td>
                <td style="padding:12px;white-space:nowrap">
                  <input type="number" min="0" style="width:48px" [(ngModel)]="type.minOptionsPerItem" aria-label="min options" />
                  <input type="number" min="0" style="width:48px" [(ngModel)]="type.defaultOptionsPerItem" aria-label="default options" />
                  <input type="number" min="0" style="width:48px" [(ngModel)]="type.maxOptionsPerItem" aria-label="max options" />
                  @if (countError(type)) {
                    <div style="color:#991b1b;font-size:11px">{{ countError(type) }}</div>
                  }
                </td>
                <td style="padding:12px;white-space:nowrap">
                  <button class="sp-btn sp-btn-secondary" type="button" (click)="toggle(type)" [disabled]="savingKey() === type.key">
                    {{ type.isEnabled ? 'Disable' : 'Enable' }}
                  </button>
                  <button class="sp-btn sp-btn-secondary" type="button" (click)="saveCounts(type)" [disabled]="savingKey() === type.key || !!countError(type)">
                    Save counts
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  `,
})
export class AdminExerciseTypesComponent implements OnInit {
  exerciseTypes = signal<ExerciseTypeDefinition[]>([]);
  savingKey = signal<string | null>(null);
  error = signal<string | null>(null);

  constructor(private admin: AdminService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.admin.listExerciseTypes().subscribe({
      next: items => this.exerciseTypes.set(items),
      error: () => this.error.set('Could not load exercise types.'),
    });
  }

  countError(type: ExerciseTypeDefinition): string | null {
    const vals = [
      type.minItemsPerPractice, type.defaultItemsPerPractice, type.maxItemsPerPractice,
      type.minOptionsPerItem, type.defaultOptionsPerItem, type.maxOptionsPerItem,
    ];
    if (vals.some(v => v == null || v < 0)) {
      return 'No negative values.';
    }
    if (!(type.minItemsPerPractice <= type.defaultItemsPerPractice && type.defaultItemsPerPractice <= type.maxItemsPerPractice)) {
      return 'Items: min <= default <= max.';
    }
    if (!(type.minOptionsPerItem <= type.defaultOptionsPerItem && type.defaultOptionsPerItem <= type.maxOptionsPerItem)) {
      return 'Options: min <= default <= max.';
    }
    return null;
  }

  saveCounts(type: ExerciseTypeDefinition): void {
    if (this.countError(type)) {
      return;
    }
    this.savingKey.set(type.key);
    this.admin.updateExerciseType(type.key, {
      minItemsPerPractice: type.minItemsPerPractice,
      defaultItemsPerPractice: type.defaultItemsPerPractice,
      maxItemsPerPractice: type.maxItemsPerPractice,
      minOptionsPerItem: type.minOptionsPerItem,
      defaultOptionsPerItem: type.defaultOptionsPerItem,
      maxOptionsPerItem: type.maxOptionsPerItem,
    }).subscribe({
      next: updated => {
        this.exerciseTypes.update(items => items.map(item => item.key === updated.key ? updated : item));
        this.savingKey.set(null);
      },
      error: () => {
        this.error.set('Could not update exercise type counts.');
        this.savingKey.set(null);
      },
    });
  }

  toggle(type: ExerciseTypeDefinition): void {
    this.savingKey.set(type.key);
    this.admin.updateExerciseType(type.key, { isEnabled: !type.isEnabled }).subscribe({
      next: updated => {
        this.exerciseTypes.update(items => items.map(item => item.key === updated.key ? updated : item));
        this.savingKey.set(null);
      },
      error: () => {
        this.error.set('Could not update exercise type.');
        this.savingKey.set(null);
      },
    });
  }
}
