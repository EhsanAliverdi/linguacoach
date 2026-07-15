import { Formio } from '@formio/js';
import { escapeHtml } from '../escape-html';

/**
 * Custom Form.io input component (registered via Formio.Components.addComponent in
 * register-custom-components.ts) — renders a fixed list of word "tokens" (schema-authored, never
 * student-editable) as clickable spans; the student toggles which ones they believe differ from
 * what they heard. Its value is a plain string array of selected token ids, scored via
 * ComponentAnswerScorer.ScoreMultipleChoice (which already accepts a bare JSON array — see
 * ExtractValueSet) against ScoringRulesJson's backend-only CorrectAnswers, so this component
 * itself never carries or leaks which tokens are actually correct — see highlight_incorrect_words
 * (Phase K21).
 */
const FormioComponentBase = (Formio as any).Components.components.base;

interface HighlightWordsToken {
  id: string;
  text: string;
}

export class HighlightWordsComponent extends FormioComponentBase {
  static schema(...extend: any[]) {
    return FormioComponentBase.schema(
      {
        type: 'highlightWords',
        label: 'Highlight Words',
        key: 'answer',
        input: true,
        tokens: [] as HighlightWordsToken[],
      },
      ...extend,
    );
  }

  static get builderInfo() {
    return {
      title: 'Highlight Words',
      group: 'basic',
      icon: 'i-cursor',
      weight: 92,
      schema: HighlightWordsComponent.schema(),
    };
  }

  static savedValueTypes() {
    return [];
  }

  get defaultSchema() {
    return HighlightWordsComponent.schema();
  }

  get emptyValue() {
    return [];
  }

  get defaultValue() {
    return [];
  }

  private get fio(): any {
    return this;
  }

  private get tokens(): HighlightWordsToken[] {
    const raw = this.fio.component?.tokens;
    return Array.isArray(raw) ? raw : [];
  }

  private get selected(): Set<string> {
    const value = this.fio.dataValue;
    return new Set(Array.isArray(value) ? value : []);
  }

  isEmpty(value?: string[] | null): boolean {
    const v = value === undefined ? (this.fio.dataValue as string[] | null) : value;
    return !Array.isArray(v) || v.length === 0;
  }

  render(): string {
    const label = escapeHtml(this.fio.component?.label ?? '');
    const question = this.fio.component?.question ? escapeHtml(this.fio.component.question) : '';
    const spans = this.tokens
      .map((t) => `<span ref="token" data-token-id="${escapeHtml(t.id)}" class="sf-highlight-token">${escapeHtml(t.text)}</span>`)
      .join(' ');
    return super.render(`
      <div class="sf-highlight-words">
        ${label ? `<div class="sf-highlight-label">${label}</div>` : ''}
        ${question ? `<div class="sf-highlight-question">${question}</div>` : ''}
        <div ref="tokenContainer" class="sf-highlight-tokens">${spans}</div>
      </div>
    `);
  }

  attach(element: HTMLElement) {
    this.fio.loadRefs(element, { token: 'multiple', tokenContainer: 'single' });
    const tokens: HTMLElement[] = this.fio.refs?.token ?? [];
    for (const el of tokens) {
      this.fio.addEventListener(el, 'click', () => this.toggleToken(el));
    }
    this.renderSelection();
    return super.attach(element);
  }

  private toggleToken(el: HTMLElement): void {
    const id = el.getAttribute('data-token-id');
    if (!id) return;
    const current = new Set(this.selected);
    if (current.has(id)) current.delete(id);
    else current.add(id);
    this.fio.setValue([...current]);
    this.renderSelection();
  }

  private renderSelection(): void {
    const tokens: HTMLElement[] = this.fio.refs?.token ?? [];
    const selected = this.selected;
    for (const el of tokens) {
      const id = el.getAttribute('data-token-id');
      el.classList.toggle('sf-highlight-token-selected', !!id && selected.has(id));
    }
  }
}
