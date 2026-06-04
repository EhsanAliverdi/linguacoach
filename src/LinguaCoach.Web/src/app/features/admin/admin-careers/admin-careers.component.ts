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
    <div class="flex gap-3 mb-5">
      <select [(ngModel)]="selectedCareerId" (change)="loadWords()" class="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500">
        <option value="">Select career profile…</option>
        @for (c of careers(); track c.id) {
          <option [value]="c.id">{{ c.name }}</option>
        }
      </select>
    </div>

    @if (selectedCareerId && words().length >= 0) {
      <!-- Add word form -->
      @if (showAddForm()) {
        <div class="bg-white rounded-xl border border-slate-200 p-5 mb-4 shadow-sm">
          <h3 class="font-medium text-slate-800 mb-3 text-sm">Add word</h3>
          <div class="grid grid-cols-2 gap-3">
            <input [(ngModel)]="newWord" placeholder="Word or phrase" class="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
            <input [(ngModel)]="newDefinition" placeholder="Definition" class="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
            <input [(ngModel)]="newExample" placeholder="Example sentence" class="col-span-2 rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
            <input [(ngModel)]="newPriority" type="number" placeholder="Priority" class="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
            <input [(ngModel)]="newTags" placeholder="Tags (comma-separated)" class="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
          </div>
          <div class="flex gap-2 mt-3">
            <button (click)="addWord()" class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white hover:bg-green-700 transition-colors">Add</button>
            <button (click)="showAddForm.set(false)" class="rounded-lg border border-slate-300 px-4 py-2 text-sm text-slate-600 hover:bg-slate-50 transition-colors">Cancel</button>
          </div>
        </div>
      } @else {
        <button (click)="showAddForm.set(true)" class="mb-4 rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-indigo-700 transition-colors">+ Add word</button>
      }

      <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-slate-50 border-b border-slate-200">
            <tr>
              <th class="text-left px-4 py-3 font-medium text-slate-600">Word</th>
              <th class="text-left px-4 py-3 font-medium text-slate-600">Definition</th>
              <th class="text-left px-4 py-3 font-medium text-slate-600">Priority</th>
              <th class="px-4 py-3"></th>
            </tr>
          </thead>
          <tbody>
            @for (w of words(); track w.id) {
              <tr class="border-b border-slate-100 last:border-0">
                @if (editingId() === w.id) {
                  <td class="px-4 py-2" colspan="4">
                    <div class="grid grid-cols-2 gap-2">
                      <input [(ngModel)]="editDef" placeholder="Definition" class="rounded border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-indigo-500" />
                      <input [(ngModel)]="editExample" placeholder="Example" class="rounded border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-indigo-500" />
                      <input [(ngModel)]="editPriority" type="number" placeholder="Priority" class="rounded border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-indigo-500" />
                      <input [(ngModel)]="editTags" placeholder="Tags" class="rounded border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-indigo-500" />
                    </div>
                    <div class="flex gap-2 mt-2">
                      <button (click)="saveEdit(w.id)" class="text-xs text-green-600 font-medium hover:underline">Save</button>
                      <button (click)="editingId.set(null)" class="text-xs text-slate-400 hover:underline">Cancel</button>
                    </div>
                  </td>
                } @else {
                  <td class="px-4 py-3 font-medium text-slate-800">{{ w.word }}</td>
                  <td class="px-4 py-3 text-slate-600 text-xs">{{ w.definition }}</td>
                  <td class="px-4 py-3 text-slate-500">{{ w.priority }}</td>
                  <td class="px-4 py-3">
                    <button (click)="startEdit(w)" class="text-xs text-indigo-600 hover:underline">Edit</button>
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
        @if (words().length === 0) {
          <p class="px-4 py-6 text-sm text-slate-400 text-center">No words yet.</p>
        }
      </div>
    }
  `,
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
