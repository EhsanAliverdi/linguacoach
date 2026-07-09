import { Component, OnInit, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ActivityService } from '../../../core/services/activity.service';
import {
  PracticeGymSuggestionsService,
  PracticeGymSuggestionItem,
  PracticeGymSuggestionsResponse,
  PracticeGymModuleSuggestion,
  routingReasonLabel,
} from '../../../core/services/practice-gym-suggestions.service';
import { ExerciseTypeDefinition } from '../../../core/models/admin.models';

export type LoadState = 'loading' | 'ready' | 'error';
export type SuggestionsLoadState = 'loading' | 'ready' | 'error' | 'empty';

export interface FormatCard {
  key: string;
  displayName: string;
  description: string;
  primarySkill: string;
  secondarySkills: string[];
  defaultItemCount: number;
  estimatedMinutes: number;
  runnable: boolean;
  icon: string;
}

const SKILL_ORDER = ['listening', 'reading', 'writing', 'speaking', 'vocabulary', 'grammar'];

const SKILL_LABELS: Record<string, string> = {
  listening: 'Listening',
  reading: 'Reading',
  writing: 'Writing',
  speaking: 'Speaking',
  vocabulary: 'Vocabulary',
  grammar: 'Grammar',
};

const SKILL_ICONS: Record<string, string> = {
  listening: 'L',
  reading: 'R',
  writing: 'W',
  speaking: 'S',
  vocabulary: 'V',
  grammar: 'G',
};

function itemLabel(type: ExerciseTypeDefinition): string {
  const n = type.defaultItemsPerPractice || 0;
  if (n <= 0) return '';
  const key = type.key;
  if (key.includes('essay') || key.includes('summarize_written') || key.includes('summarize_spoken') || key.includes('email') || key.includes('chat')) {
    return `${n} ${n === 1 ? 'task' : 'tasks'}`;
  }
  if (key.includes('gap') || key.includes('fill_in_blanks') || key.includes('dictation')) {
    return `${n} ${n === 1 ? 'gap' : 'gaps'}`;
  }
  if (key.includes('match')) {
    return `${n} ${n === 1 ? 'pair' : 'pairs'}`;
  }
  if (key.includes('question') || key.includes('answer')) {
    return `${n} ${n === 1 ? 'question' : 'questions'}`;
  }
  if (key.includes('speaking') || key.includes('spoken') || key.includes('speak')) {
    return `${n} ${n === 1 ? 'prompt' : 'prompts'}`;
  }
  if (key.includes('reorder') || key.includes('paragraph')) {
    return `${n} ${n === 1 ? 'paragraph' : 'paragraphs'}`;
  }
  if (key.includes('word')) {
    return `${n} ${n === 1 ? 'word' : 'words'}`;
  }
  return `${n} ${n === 1 ? 'item' : 'items'}`;
}

function toCard(type: ExerciseTypeDefinition): FormatCard {
  return {
    key: type.key,
    displayName: type.displayName,
    description: type.description,
    primarySkill: type.primarySkill,
    secondarySkills: type.secondarySkills ?? [],
    defaultItemCount: type.defaultItemsPerPractice ?? 0,
    estimatedMinutes: type.estimatedDurationMinutes ?? 0,
    runnable: type.isEnabled && type.isAvailableForGeneration && type.implementationStatus === 'ready' && type.supportsPracticeGym,
    icon: SKILL_ICONS[type.primarySkill] ?? type.primarySkill.charAt(0).toUpperCase(),
  };
}

@Component({
  selector: 'app-practice-gym',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './practice-gym.component.html',
  styleUrl: './practice-gym.component.css',
})
export class PracticeGymComponent implements OnInit {
  private readonly _types = signal<ExerciseTypeDefinition[]>([]);

  loadState = signal<LoadState>('loading');
  activeSkill = signal<string | null>(null);
  selectionMessage = signal<string | null>(null);

  // Suggestions state
  suggestionsLoadState = signal<SuggestionsLoadState>('loading');
  suggestions = signal<PracticeGymSuggestionsResponse | null>(null);
  startingItemId = signal<string | null>(null);
  suggestionMessage = signal<string | null>(null);

  /** Phase H10 — tracks which module suggestion is currently starting, so its button shows a
   * busy state without disabling every other card. */
  startingModuleId = signal<string | null>(null);

  readonly skillGroups = computed(() => {
    const all = this._types();
    const map = new Map<string, FormatCard[]>();
    for (const t of all) {
      const card = toCard(t);
      if (!map.has(card.primarySkill)) map.set(card.primarySkill, []);
      map.get(card.primarySkill)!.push(card);
    }
    return SKILL_ORDER
      .filter(s => map.has(s))
      .map(s => ({ skill: s, label: SKILL_LABELS[s] ?? s, cards: map.get(s)! }));
  });

  readonly runnableCount = computed(() =>
    this._types().filter(t => t.isEnabled && t.isAvailableForGeneration && t.implementationStatus === 'ready' && t.supportsPracticeGym).length
  );

  readonly suggestedItems = computed(() => this.suggestions()?.suggestedItems ?? []);
  readonly continueItems = computed(() => this.suggestions()?.continueItems ?? []);
  readonly reviewItems = computed(() => this.suggestions()?.reviewItems ?? []);

  /** Phase H7 — additive, read-only. Empty whenever no compatible approved Module exists; the
   * existing suggestion sections above remain the full Practice Gym experience either way. */
  readonly moduleSuggestions = computed(() => {
    const section = this.suggestions()?.moduleSuggestions;
    return section && !section.fallbackRequired ? section.suggestions : [];
  });

  constructor(
    private activityService: ActivityService,
    private practiceGymSuggestionsService: PracticeGymSuggestionsService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.activityService.getExerciseTypes().subscribe({
      next: items => {
        this._types.set(items);
        this.loadState.set('ready');
      },
      error: () => this.loadState.set('error'),
    });

    this.loadSuggestions();
  }

  loadSuggestions(): void {
    this.suggestionsLoadState.set('loading');
    this.practiceGymSuggestionsService.getSuggestions().subscribe({
      next: resp => {
        this.suggestions.set(resp);
        const hasAny = resp.suggestedItems.length > 0
          || resp.continueItems.length > 0
          || resp.reviewItems.length > 0;
        this.suggestionsLoadState.set(hasAny ? 'ready' : 'empty');
      },
      error: () => {
        this.suggestionsLoadState.set('error');
      },
    });
  }

  startSuggestion(item: PracticeGymSuggestionItem): void {
    if (this.startingItemId() !== null) return;
    this.suggestionMessage.set(null);
    this.startingItemId.set(item.readinessItemId);

    this.practiceGymSuggestionsService.startSuggestion(item.readinessItemId).subscribe({
      next: result => {
        this.startingItemId.set(null);
        if (!result.success) {
          this.suggestionMessage.set('This item is no longer available. Refreshing suggestions.');
          this.loadSuggestions();
          return;
        }
        if (result.learningActivityId) {
          this.router.navigate(['/activity'], {
            queryParams: { activityId: result.learningActivityId, returnTo: '/practice' },
          });
        } else if (result.learningSessionId) {
          this.router.navigate(['/lesson'], {
            queryParams: { sessionId: result.learningSessionId, returnTo: '/practice' },
          });
        } else {
          this.suggestionMessage.set('Could not start this practice. Please try again.');
        }
      },
      error: () => {
        this.startingItemId.set(null);
        this.suggestionMessage.set('Could not start this practice. Please try again.');
      },
    });
  }

  /** Phase H10 — starts a launch-eligible module suggestion. Mirrors startSuggestion() above:
   * on success, navigates to /activity with the real, materialized LearningActivity id. */
  startModuleSuggestion(module: PracticeGymModuleSuggestion): void {
    if (!module.canLaunch || this.startingModuleId() !== null) return;
    this.suggestionMessage.set(null);
    this.startingModuleId.set(module.moduleDefinitionId);

    this.practiceGymSuggestionsService.startModuleSuggestion(module.moduleDefinitionId).subscribe({
      next: result => {
        this.startingModuleId.set(null);
        if (!result.success || !result.learningActivityId) {
          this.suggestionMessage.set(result.unsupportedReason ?? 'Could not start this practice. Please try again.');
          return;
        }
        this.router.navigate(['/activity'], {
          queryParams: { activityId: result.learningActivityId, returnTo: '/practice' },
        });
      },
      error: () => {
        this.startingModuleId.set(null);
        this.suggestionMessage.set('Could not start this practice. Please try again.');
      },
    });
  }

  isStartingModule(moduleDefinitionId: string): boolean {
    return this.startingModuleId() === moduleDefinitionId;
  }

  isStartingSuggestion(id: string): boolean {
    return this.startingItemId() === id;
  }

  routingLabel(item: PracticeGymSuggestionItem): string {
    return routingReasonLabel(item.routingReason);
  }

  startFormat(card: FormatCard): void {
    if (!card.runnable || this.activeSkill()) return;
    this.selectionMessage.set(null);
    this.activeSkill.set(card.key);

    this.activityService.getPracticeGymNext({ skill: card.primarySkill, exerciseType: card.key }).subscribe({
      next: result => {
        this.activeSkill.set(null);
        if (!result.hasActivity || !result.activityId) {
          this.selectionMessage.set(result.reason ?? 'This format is not ready yet. Try again shortly.');
          return;
        }
        this.router.navigate(['/activity'], {
          queryParams: { activityId: result.activityId, returnTo: '/practice' },
        });
      },
      error: () => {
        this.activeSkill.set(null);
        this.selectionMessage.set('Practice is temporarily unavailable. Please try again shortly.');
      },
    });
  }

  isStarting(key: string): boolean {
    return this.activeSkill() === key;
  }

  itemLabel(type: ExerciseTypeDefinition): string {
    return itemLabel(type);
  }

  cardItemLabel(card: FormatCard): string {
    const fake = { key: card.key, defaultItemsPerPractice: card.defaultItemCount } as ExerciseTypeDefinition;
    return itemLabel(fake);
  }

  // Kept for tests that call the old API
  selectSkill(skill: string): void {
    if (this.activeSkill()) return;
    this.selectionMessage.set(null);
    this.activeSkill.set(skill);

    this.activityService.getPracticeGymNext({ skill }).subscribe({
      next: result => {
        this.activeSkill.set(null);
        if (!result.hasActivity || !result.activityId) {
          this.selectionMessage.set(result.reason ?? 'This skill is not ready in Practice Gym yet.');
          return;
        }
        this.router.navigate(['/activity'], {
          queryParams: { activityId: result.activityId, returnTo: '/practice' },
        });
      },
      error: () => {
        this.activeSkill.set(null);
        this.selectionMessage.set('Practice is temporarily unavailable. Please try again shortly.');
      },
    });
  }

  // Kept for backward-compat with existing tests
  hasSkillAvailable(skill: string): boolean {
    return this._types().some(t =>
      t.primarySkill === skill &&
      t.isEnabled &&
      t.isAvailableForGeneration &&
      t.implementationStatus === 'ready' &&
      t.supportsPracticeGym);
  }

  skillStatusText(skill: string): string {
    return this.hasSkillAvailable(skill) ? 'Available' : 'Coming soon';
  }

  isAvailable(key: string): boolean {
    const item = this._types().find(t => t.key === key);
    return !!item && item.isEnabled && item.isAvailableForGeneration && item.implementationStatus === 'ready' && item.supportsPracticeGym;
  }

  statusText(key: string): string {
    const item = this._types().find(t => t.key === key);
    if (!item) return 'Coming soon';
    if (!item.isEnabled) return 'Disabled';
    if (item.implementationStatus !== 'ready') return 'Coming soon';
    if (!item.supportsPracticeGym) return 'Not in Practice Gym';
    return 'Available';
  }
}


