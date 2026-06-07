import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { VocabularyService } from '../../core/services/vocabulary.service';
import { StudentVocabularyItem, VocabularyItemStatus } from '../../core/models/vocabulary.models';

const CATEGORY_LABELS: Record<string, string> = {
  workplace_phrase: 'Workplace phrase',
  polite_request: 'Polite request',
  grammar_pattern: 'Grammar pattern',
  connector: 'Connector',
  tone_softener: 'Tone softener',
  project_vocabulary: 'Project vocabulary',
  common_mistake: 'Common mistake',
  useful_expression: 'Useful expression',
};

@Component({
  selector: 'app-vocabulary',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <!-- Header -->
    <div class="sp-section-h" style="margin-bottom:20px">
      <div>
        <h3 style="margin-bottom:4px">Vocabulary</h3>
        <p style="font-size:13px;color:var(--sp-muted)">Review useful words and phrases from your writing practice.</p>
      </div>
    </div>

    <!-- Loading -->
    @if (loading()) {
      <div class="sp-stat-grid" style="margin-bottom:24px">
        @for (n of [1,2,3,4]; track n) {
          <div class="sp-card sp-skeleton" style="height:70px"></div>
        }
      </div>
      <div class="sp-card sp-skeleton" style="height:200px"></div>
    }

    <!-- Error -->
    @if (!loading() && error()) {
      <div class="sp-card sp-card-warning" style="padding:20px;text-align:center;margin-bottom:20px">
        <div style="font-size:28px;margin-bottom:8px">⚠️</div>
        <p style="font-size:14px;font-weight:600;color:var(--sp-ink);margin-bottom:4px">Could not load vocabulary</p>
        <p style="font-size:13px;color:var(--sp-muted);margin-bottom:12px">{{ error() }}</p>
        <button class="sp-button-secondary" (click)="load()">Try again</button>
      </div>
    }

    <!-- Empty state: authenticated but no entries -->
    @if (!loading() && !error() && allItems().length === 0) {
      <div class="sp-empty-state">
        <div style="font-size:36px;margin-bottom:12px">📖</div>
        <h3 style="font-size:16px;font-weight:800;color:var(--sp-ink);margin-bottom:8px">No vocabulary yet</h3>
        <p style="font-size:13px;color:var(--sp-muted);line-height:1.6;max-width:290px;text-align:center">
          Your vocabulary list will grow as you complete writing activities.
        </p>
        <a routerLink="/activity" class="sp-button-primary" style="margin-top:16px;display:inline-flex">
          Start practising →
        </a>
      </div>
    }

    <!-- Real data -->
    @if (!loading() && !error() && allItems().length > 0) {

      <!-- Summary cards -->
      <div class="sp-stat-grid" style="margin-bottom:24px">
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">{{ countByStatus('New') }}</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">New</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800;color:#1565c0">{{ countByStatus('Practising') }}</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">Practising</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800;color:#2e7d32">{{ countByStatus('Mastered') }}</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">Mastered</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">{{ allItems().length }}</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">Total saved</div>
        </div>
      </div>

      <!-- Filters -->
      <div style="display:flex;gap:8px;flex-wrap:wrap;margin-bottom:20px">
        @for (f of filters; track f.value) {
          <button
            [style.background]="activeFilter() === f.value ? 'var(--sp-writing,#2e7d32)' : 'var(--sp-surface)'"
            [style.color]="activeFilter() === f.value ? '#fff' : 'var(--sp-ink)'"
            style="font-size:12px;font-weight:700;padding:6px 14px;border-radius:20px;border:1px solid var(--sp-border);cursor:pointer"
            (click)="setFilter(f.value)">
            {{ f.label }}
          </button>
        }
      </div>

      <!-- Empty filtered state -->
      @if (filteredItems().length === 0) {
        <div style="text-align:center;padding:32px 16px;color:var(--sp-muted);font-size:13px">
          No entries for this filter.
        </div>
      }

      <!-- Vocabulary cards -->
      @for (item of filteredItems(); track item.id) {
        <div class="sp-card" style="padding:16px;margin-bottom:12px" [attr.data-vocab-id]="item.id">
          <!-- Top row: term + status badge -->
          <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:8px;margin-bottom:8px">
            <div style="font-size:15px;font-weight:800;color:var(--sp-ink);flex:1;min-width:0">{{ item.term }}</div>
            <span [style.background]="statusBg(item.status)"
                  [style.color]="statusFg(item.status)"
                  style="font-size:10px;font-weight:700;padding:2px 8px;border-radius:12px;white-space:nowrap;flex-shrink:0">
              {{ item.status }}
            </span>
          </div>

          <!-- Suggested phrase -->
          @if (item.suggestedPhrase) {
            <div style="font-size:13px;font-style:italic;color:var(--sp-text);margin-bottom:6px;padding:8px;background:var(--sp-surface);border-radius:6px;border-left:3px solid var(--sp-writing,#2e7d32)">
              "{{ item.suggestedPhrase }}"
            </div>
          }

          <!-- Explanation -->
          <p style="font-size:13px;color:var(--sp-ink);line-height:1.6;margin-bottom:6px">
            {{ item.meaningOrExplanation }}
          </p>

          <!-- Example sentence -->
          @if (item.exampleSentence) {
            <p style="font-size:12px;color:var(--sp-muted);line-height:1.5;margin-bottom:8px">
              <em>e.g. {{ item.exampleSentence }}</em>
            </p>
          }

          <!-- Meta row: category + seen count + source activity -->
          <div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin-bottom:10px">
            <span style="font-size:11px;font-weight:600;padding:2px 8px;background:#f3e8ff;color:#6b21a8;border-radius:10px">
              {{ categoryLabel(item.category) }}
            </span>
            @if (item.seenCount > 1) {
              <span style="font-size:11px;color:var(--sp-muted)">Seen {{ item.seenCount }}×</span>
            }
            @if (item.sourceActivityTitle) {
              <span style="font-size:11px;color:var(--sp-faint)">From: {{ item.sourceActivityTitle }}</span>
            }
          </div>

          <!-- Action buttons -->
          <div style="display:flex;gap:8px;flex-wrap:wrap">
            @if (item.status !== 'Practising') {
              <button class="sp-button-secondary"
                      style="font-size:12px;padding:5px 12px"
                      [disabled]="updatingId() === item.id"
                      (click)="updateStatus(item, 'Practising')">
                Practise
              </button>
            }
            @if (item.status !== 'Mastered') {
              <button class="sp-button-secondary"
                      style="font-size:12px;padding:5px 12px;background:#e8f5e9;color:#2e7d32;border-color:#a5d6a7"
                      [disabled]="updatingId() === item.id"
                      (click)="updateStatus(item, 'Mastered')">
                Mastered
              </button>
            }
            @if (item.status !== 'Ignored') {
              <button class="sp-button-secondary"
                      style="font-size:12px;padding:5px 12px;color:var(--sp-muted)"
                      [disabled]="updatingId() === item.id"
                      (click)="updateStatus(item, 'Ignored')">
                Ignore
              </button>
            }
          </div>

          @if (updateError() && updatingId() === item.id) {
            <p style="font-size:12px;color:#c62828;margin-top:6px">Could not update status. Please try again.</p>
          }
        </div>
      }
    }
  `,
})
export class VocabularyComponent implements OnInit {
  allItems = signal<StudentVocabularyItem[]>([]);
  loading = signal(true);
  error = signal('');
  activeFilter = signal<string>('All');
  updatingId = signal<string | null>(null);
  updateError = signal(false);

  readonly filters = [
    { label: 'All', value: 'All' },
    { label: 'New', value: 'New' },
    { label: 'Practising', value: 'Practising' },
    { label: 'Mastered', value: 'Mastered' },
    { label: 'Ignored', value: 'Ignored' },
  ];

  filteredItems = computed(() => {
    const filter = this.activeFilter();
    const items = this.allItems();
    if (filter === 'All') return items;
    return items.filter(i => i.status === filter);
  });

  constructor(private vocabularyService: VocabularyService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.vocabularyService.getVocabulary().subscribe({
      next: items => { this.allItems.set(items); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not load your vocabulary. Please try again.');
      },
    });
  }

  setFilter(filter: string): void {
    this.activeFilter.set(filter);
  }

  updateStatus(item: StudentVocabularyItem, status: VocabularyItemStatus): void {
    this.updatingId.set(item.id);
    this.updateError.set(false);
    this.vocabularyService.updateStatus(item.id, status).subscribe({
      next: () => {
        // Optimistic update
        this.allItems.update(items =>
          items.map(i => i.id === item.id ? { ...i, status } : i)
        );
        this.updatingId.set(null);
      },
      error: () => {
        this.updateError.set(true);
        this.updatingId.set(null);
      },
    });
  }

  countByStatus(status: string): number {
    return this.allItems().filter(i => i.status === status).length;
  }

  categoryLabel(cat: string): string {
    return CATEGORY_LABELS[cat] ?? cat.replace(/_/g, ' ');
  }

  statusBg(status: string): string {
    if (status === 'Mastered') return '#e8f5e9';
    if (status === 'Practising') return '#e3f2fd';
    if (status === 'Ignored') return '#f5f5f5';
    return '#fff8e1';
  }

  statusFg(status: string): string {
    if (status === 'Mastered') return '#2e7d32';
    if (status === 'Practising') return '#1565c0';
    if (status === 'Ignored') return '#757575';
    return '#f57c00';
  }
}
