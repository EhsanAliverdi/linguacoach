import { Component, computed, effect, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StudentChipComponent } from '../../../../design-system/student/student-chip.component';

type WizardFieldType = 'textfield' | 'select' | 'selectboxes' | 'radio';

interface WizardOption {
  label: string;
  value: string;
}

interface WizardCondition {
  when: string;
  eq: string;
  show: boolean;
}

interface WizardField {
  key: string;
  type: WizardFieldType;
  label: string;
  required: boolean;
  options: WizardOption[];
  defaultValue?: unknown;
  conditional?: WizardCondition;
}

interface WizardPage {
  key: string;
  title: string;
  fields: WizardField[];
}

const SUPPORTED_FIELD_TYPES = new Set<string>(['textfield', 'select', 'selectboxes', 'radio']);

/** Matches the CSS animation duration of `.wizard-title-splash-text` in onboarding-wizard.component.css. */
const TITLE_SPLASH_MS = 900;

function parseWizardSchema(schema: unknown): WizardPage[] {
  const root = schema as { components?: unknown[] } | null | undefined;
  const pages = Array.isArray(root?.components) ? root!.components : [];

  return pages
    .filter((p: any) => p?.type === 'panel')
    .map((panel: any): WizardPage => ({
      key: panel.key,
      title: panel.title ?? panel.label ?? panel.breadcrumb ?? '',
      fields: (Array.isArray(panel.components) ? panel.components : [])
        .filter((f: any) => SUPPORTED_FIELD_TYPES.has(f?.type))
        .map((f: any): WizardField => ({
          key: f.key,
          type: f.type,
          label: f.label ?? '',
          required: !!f.validate?.required,
          options: f.type === 'select' ? (f.data?.values ?? []) : (f.values ?? []),
          defaultValue: f.defaultValue,
          conditional: f.conditional?.when !== undefined && f.conditional?.eq !== undefined
            ? { when: f.conditional.when, eq: String(f.conditional.eq), show: f.conditional.show !== false }
            : undefined,
        })),
    }));
}

/**
 * Purpose-built onboarding renderer (FormRendererKind.Custom) — parses the same admin-authored
 * Form.io wizard schema (so admin edits to labels/options/pages still take effect) but owns its
 * own presentation entirely, rather than delegating to @formio/js's generic DOM. Renders inside
 * OnboardingLayoutComponent's chromeless, 480px-wide narrow shell.
 */
@Component({
  selector: 'app-onboarding-wizard',
  standalone: true,
  imports: [CommonModule, StudentChipComponent],
  templateUrl: './onboarding-wizard.component.html',
  styleUrl: './onboarding-wizard.component.css',
})
export class OnboardingWizardComponent {
  schema = input<any>(null);
  submissionData = input<any>(null);
  disabled = input(false);

  change = output<any>();
  submit = output<any>();

  pages = computed<WizardPage[]>(() => parseWizardSchema(this.schema()));
  currentPageIndex = signal(0);
  formData = signal<Record<string, any>>({});

  currentPage = computed<WizardPage | null>(() => this.pages()[this.currentPageIndex()] ?? null);
  isFirstPage = computed(() => this.currentPageIndex() === 0);
  isLastPage = computed(() => this.currentPageIndex() === this.pages().length - 1);

  progressPercent = computed(() => {
    const total = this.pages().length;
    return total === 0 ? 0 : Math.round(((this.currentPageIndex() + 1) / total) * 100);
  });

  /** Between pages (including the very first one), a big animated title for the destination page
   * is shown on its own before that page's fields appear — see goToPage(). */
  phase = signal<'content' | 'title'>('title');
  private pendingPageIndex = signal<number | null>(0);
  splashTitle = computed(() => {
    const idx = this.pendingPageIndex();
    return idx === null ? '' : (this.pages()[idx]?.title ?? '');
  });

  canGoNext = computed(() => {
    const page = this.currentPage();
    if (!page) return false;
    const data = this.formData();
    return page.fields.every(f => !this.isVisible(f, data) || !f.required || this.isAnswered(f, data));
  });

  /** Declarative Form.io `conditional: { show, when, eq }` only — no custom/eval conditions are
   * ever authored (FormIoSchemaValidationService rejects those at save time). When the trigger
   * field is a selectboxes (array value), `eq` is checked as "array contains" rather than
   * strict equality, so e.g. checking "Other" among several selected goals still reveals the
   * conditional field. */
  isVisible(field: WizardField, data: Record<string, any>): boolean {
    const c = field.conditional;
    if (!c) return true;
    const triggerValue = data[c.when];
    const matches = Array.isArray(triggerValue)
      ? triggerValue.includes(c.eq)
      : String(triggerValue ?? '') === c.eq;
    return c.show ? matches : !matches;
  }

  private isAnswered(field: WizardField, data: Record<string, any>): boolean {
    const v = data[field.key];
    if (field.type === 'selectboxes') return Array.isArray(v) && v.length > 0;
    return v !== undefined && v !== null && String(v).trim().length > 0;
  }

  private seeded = false;
  private introStarted = false;

  constructor() {
    effect(() => {
      const schema = this.schema();
      const submission = this.submissionData();
      if (!schema || this.seeded) return;
      this.seeded = true;

      const seed: Record<string, any> = {};
      for (const page of parseWizardSchema(schema)) {
        for (const field of page.fields) {
          if (field.defaultValue !== undefined) seed[field.key] = field.defaultValue;
        }
      }
      this.formData.set({ ...seed, ...(submission ?? {}) });
    }, { allowSignalWrites: true });

    // Same title-splash-then-content handoff as goToPage(), for the very first page.
    effect(() => {
      if (this.pages().length === 0 || this.introStarted) return;
      this.introStarted = true;

      setTimeout(() => {
        this.currentPageIndex.set(0);
        this.phase.set('content');
        this.pendingPageIndex.set(null);
      }, TITLE_SPLASH_MS);
    }, { allowSignalWrites: true });
  }

  isSelected(field: WizardField, value: string): boolean {
    const v = this.formData()[field.key];
    return field.type === 'selectboxes' ? Array.isArray(v) && v.includes(value) : v === value;
  }

  onTextInput(key: string, event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.updateField(key, value);
  }

  selectSingle(field: WizardField, value: string): void {
    if (this.disabled()) return;
    this.updateField(field.key, value);
  }

  toggleMulti(field: WizardField, value: string): void {
    if (this.disabled()) return;
    const current: string[] = Array.isArray(this.formData()[field.key]) ? [...this.formData()[field.key]] : [];
    const idx = current.indexOf(value);
    if (idx >= 0) current.splice(idx, 1); else current.push(value);
    this.updateField(field.key, current);
  }

  private updateField(key: string, value: unknown): void {
    this.formData.update(d => {
      const next = { ...d, [key]: value };
      // If this change hid a conditional field, drop its stale value so a hidden answer (e.g.
      // a previously-picked support language after switching back to "No") never gets submitted.
      for (const page of this.pages()) {
        for (const field of page.fields) {
          if (field.conditional && !this.isVisible(field, next)) delete next[field.key];
        }
      }
      return next;
    });
    this.change.emit(this.formData());
  }

  next(): void {
    if (this.disabled() || !this.canGoNext()) return;
    if (this.isLastPage()) {
      this.submit.emit(this.formData());
    } else {
      this.goToPage(this.currentPageIndex() + 1);
    }
  }

  back(): void {
    if (this.disabled()) return;
    this.goToPage(Math.max(0, this.currentPageIndex() - 1));
  }

  /** Shows the destination page's title on its own first (big, animated), then swaps in the
   * page's fields once the splash animation finishes. */
  private goToPage(index: number): void {
    if (index < 0 || index >= this.pages().length || this.phase() === 'title') return;
    this.pendingPageIndex.set(index);
    this.phase.set('title');
    setTimeout(() => {
      this.currentPageIndex.set(index);
      this.phase.set('content');
      this.pendingPageIndex.set(null);
    }, TITLE_SPLASH_MS);
  }
}
