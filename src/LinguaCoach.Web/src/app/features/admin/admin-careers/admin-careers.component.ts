import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { CareerProfileItem, CurriculumWordItem } from '../../../core/models/admin.models';
import { ReferenceService } from '../../../core/services/reference.service';

@Component({
  selector: 'app-admin-careers',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="sp-admin-page-header">
      <h1 class="sp-admin-page-title">Curriculum</h1>
      <p class="sp-admin-page-sub">Career profiles and vocabulary word lists</p>
    </div>

    <!-- Career picker -->
    <div class="sp-admin-mb">
      <select [(ngModel)]="selectedCareerId" (change)="loadWords()" class="sp-input sp-admin-select">
        <option value="">Select career profile…</option>
        @for (c of careers(); track c.id) {
          <option [value]="c.id">{{ c.name }}</option>
        }
      </select>
    </div>

    @if (selectedCareerId && words().length >= 0) {
      <!-- Add word form -->
      @if (showAddForm()) {
        <div class="sp-admin-form-card sp-admin-mb">
          <h3 class="sp-admin-section-title">Add word</h3>
          <div class="sp-admin-field-grid">
            <label class="sp-admin-field">
              <span class="sp-admin-field-label">Word or phrase</span>
              <input [(ngModel)]="newWord" class="sp-input" />
            </label>
            <label class="sp-admin-field">
              <span class="sp-admin-field-label">Definition</span>
              <input [(ngModel)]="newDefinition" class="sp-input" />
            </label>
            <label class="sp-admin-field sp-admin-wide">
              <span class="sp-admin-field-label">Example sentence</span>
              <input [(ngModel)]="newExample" class="sp-input" />
            </label>
            <label class="sp-admin-field">
              <span class="sp-admin-field-label">Priority</span>
              <input [(ngModel)]="newPriority" type="number" class="sp-input" />
            </label>
            <label class="sp-admin-field">
              <span class="sp-admin-field-label">Tags (comma-separated)</span>
              <input [(ngModel)]="newTags" class="sp-input" />
            </label>
          </div>
          <div class="sp-admin-action-row">
            <button (click)="addWord()" class="sp-admin-btn-primary">Add</button>
            <button (click)="showAddForm.set(false)" class="sp-button-ghost">Cancel</button>
          </div>
        </div>
      } @else {
        <button (click)="showAddForm.set(true)" class="sp-admin-btn-primary sp-admin-btn-sm sp-admin-mb">+ Add word</button>
      }

      <div class="sp-admin-table-card">
        <table class="sp-admin-table">
          <thead>
            <tr>
              <th>Word</th>
              <th>Definition</th>
              <th>Priority</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (w of words(); track w.id) {
              <tr>
                @if (editingId() === w.id) {
                  <td colspan="4">
                    <div class="sp-admin-field-grid">
                      <label class="sp-admin-field">
                        <span class="sp-admin-field-label">Definition</span>
                        <input [(ngModel)]="editDef" class="sp-input" />
                      </label>
                      <label class="sp-admin-field">
                        <span class="sp-admin-field-label">Example</span>
                        <input [(ngModel)]="editExample" class="sp-input" />
                      </label>
                      <label class="sp-admin-field">
                        <span class="sp-admin-field-label">Priority</span>
                        <input [(ngModel)]="editPriority" type="number" class="sp-input" />
                      </label>
                      <label class="sp-admin-field">
                        <span class="sp-admin-field-label">Tags</span>
                        <input [(ngModel)]="editTags" class="sp-input" />
                      </label>
                    </div>
                    <div class="sp-admin-action-row">
                      <button (click)="saveEdit(w.id)" class="sp-admin-link-button sp-admin-text-success-link">Save</button>
                      <button (click)="editingId.set(null)" class="sp-admin-link-button sp-admin-text-muted-link">Cancel</button>
                    </div>
                  </td>
                } @else {
                  <td>{{ w.word }}</td>
                  <td class="sp-admin-table-muted">{{ w.definition }}</td>
                  <td>{{ w.priority }}</td>
                  <td>
                    <button (click)="startEdit(w)" class="sp-admin-link-button">Edit</button>
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
        @if (words().length === 0) {
          <p class="sp-admin-empty-row">No words yet.</p>
        }
      </div>
    }
  `,
  styles: [`
    .sp-admin-wide{grid-column:1/-1;}
    .sp-admin-mb{margin-bottom:20px;}
    .sp-admin-select{max-width:320px;}
    .sp-admin-link-button{border:none;background:none;padding:0;font:inherit;font-size:12.5px;font-weight:800;cursor:pointer;color:#4338CA;}
    .sp-admin-text-success-link{color:#16A34A;}
    .sp-admin-text-muted-link{color:#94A3B8;}
  `],
})
export class AdminCareersComponent implements OnInit {
  careers = signal<CareerProfileItem[]>([]);
  words = signal<CurriculumWordItem[]>([]);
  selectedCareerId = '';
  showAddForm = signal(false);
  editingId = signal<string | null>(null);

  newWord = ''; newDefinition = ''; newExample = ''; newPriority = 100; newTags = '';
  editDef = ''; editExample = ''; editPriority = 0; editTags = '';

  private languagePairId = '';

  constructor(private adminApi: AdminApiService, private referenceService: ReferenceService) {}

  ngOnInit(): void {
    this.adminApi.listCareers().subscribe({ next: c => this.careers.set(c) });
    this.referenceService.getLanguagePairs().subscribe({
      next: pairs => { if (pairs.length > 0) this.languagePairId = pairs[0].id; }
    });
  }

  loadWords(): void {
    if (!this.selectedCareerId || !this.languagePairId) return;
    this.adminApi.listWords(this.selectedCareerId, this.languagePairId).subscribe({ next: w => this.words.set(w) });
  }

  addWord(): void {
    if (!this.newWord.trim()) return;
    this.adminApi.addWord(this.selectedCareerId, {
      languagePairId: this.languagePairId,
      word: this.newWord, definition: this.newDefinition,
      exampleSentence: this.newExample, priority: this.newPriority, tags: this.newTags
    }).subscribe({ next: () => { this.showAddForm.set(false); this.newWord = ''; this.loadWords(); } });
  }

  startEdit(w: CurriculumWordItem): void {
    this.editingId.set(w.id);
    this.editDef = w.definition; this.editExample = w.exampleSentence;
    this.editPriority = w.priority; this.editTags = w.tags;
  }

  saveEdit(id: string): void {
    this.adminApi.updateWord(id, {
      definition: this.editDef, exampleSentence: this.editExample,
      priority: this.editPriority, tags: this.editTags
    }).subscribe({ next: () => { this.editingId.set(null); this.loadWords(); } });
  }
}
