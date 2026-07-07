/** Shared with the backend's ComponentScoringRule (LinguaCoach.Application.Placement) — kept in
 * sync manually since Form.io schemas cross the TS/C# boundary as plain JSON. Written onto the
 * *authoring* schema only (never the student-facing one) as `component.quiz = { enabled, rule }`
 * by the Form.io builder's Quiz tab; the server-side IFormIoQuizSchemaSplitter is the sole
 * authority that extracts/strips it before anything is served to students. */
export type QuizScoringKind = 'single_choice' | 'multiple_choice' | 'text_exact' | 'text_normalized';

export interface QuizScoringRule {
  kind: QuizScoringKind;
  correctAnswer?: string;
  correctAnswers?: string[];
  points?: number;
}

export interface QuizAnnotation {
  enabled: boolean;
  rule?: QuizScoringRule;
  /** Transient, builder-UI-only field for a single boolean checkbox's Yes/No picker — finalized
   * into `rule.correctAnswers` by `finalizeQuizAnnotations` before save, never sent as-is. */
  correctBoolAnswer?: 'true' | 'false';
  /** Transient, builder-UI-only field controlling whether a text component's rule.kind becomes
   * text_exact or text_normalized — finalized the same way. */
  matchMode?: 'text_exact' | 'text_normalized';
}

const CHOICE_SINGLE_TYPES = new Set(['radio', 'select']);
const CHOICE_MULTI_TYPES = new Set(['selectboxes']);
const TEXT_TYPES = new Set(['textfield', 'textarea']);
const CONTAINER_ARRAY_PROPS = ['components', 'columns', 'rows'] as const;

/** Derives each enabled component's final `quiz.rule.kind` (and, for the checkbox/text-mode
 * builder-only helper fields, the actual rule shape) from its Form.io component `type`, since the
 * admin never picks a "kind" directly — mirrors FormIoQuizSchemaSplitter's container-walk shape so
 * both sides agree on what "every component" means. Call this on the builder's live schema
 * immediately before serializing it for save. */
export function finalizeQuizAnnotations(schema: any): any {
  const clone = structuredClone(schema ?? {});
  walkArray(clone.components);
  return clone;
}

function walkArray(nodes: any): void {
  if (!Array.isArray(nodes)) return;
  for (const node of nodes) {
    if (node && typeof node === 'object') walkComponent(node);
  }
}

function walkComponent(component: any): void {
  if (component.quiz?.enabled === true) {
    const quiz: QuizAnnotation = component.quiz;
    const points = quiz.rule?.points ?? 1;

    if (CHOICE_SINGLE_TYPES.has(component.type)) {
      component.quiz.rule = { kind: 'single_choice', correctAnswer: quiz.rule?.correctAnswer, points };
    } else if (CHOICE_MULTI_TYPES.has(component.type)) {
      component.quiz.rule = { kind: 'multiple_choice', correctAnswers: quiz.rule?.correctAnswers ?? [], points };
    } else if (component.type === 'checkbox') {
      component.quiz.rule = { kind: 'multiple_choice', correctAnswers: [quiz.correctBoolAnswer ?? 'false'], points };
    } else if (TEXT_TYPES.has(component.type)) {
      component.quiz.rule = { kind: quiz.matchMode === 'text_exact' ? 'text_exact' : 'text_normalized', correctAnswer: quiz.rule?.correctAnswer, points };
    } else {
      // Not a scorable type — never leave a stray quiz annotation Form.io might have carried over.
      delete component.quiz;
    }
  }

  for (const prop of CONTAINER_ARRAY_PROPS) {
    const arr = component[prop];
    if (!Array.isArray(arr)) continue;
    for (const child of arr) {
      if (!child || typeof child !== 'object') continue;
      if (Array.isArray(child.components)) walkArray(child.components);
      else if (child.type) walkComponent(child);
    }
  }
}

/** True if any component in the schema has quiz scoring enabled — for the editor's
 * "X of Y questions scored" summary line. */
export function countScoredComponents(schema: any): { scored: number; total: number } {
  let scored = 0;
  let total = 0;
  walkCount(schema?.components);
  return { scored, total };

  function walkCount(nodes: any): void {
    if (!Array.isArray(nodes)) return;
    for (const node of nodes) {
      if (!node || typeof node !== 'object') continue;
      const isScorable = CHOICE_SINGLE_TYPES.has(node.type) || CHOICE_MULTI_TYPES.has(node.type)
        || node.type === 'checkbox' || TEXT_TYPES.has(node.type);
      if (isScorable) {
        total++;
        if (node.quiz?.enabled === true) scored++;
      }
      for (const prop of CONTAINER_ARRAY_PROPS) {
        const arr = node[prop];
        if (!Array.isArray(arr)) continue;
        for (const child of arr) {
          if (!child || typeof child !== 'object') continue;
          if (Array.isArray(child.components)) walkCount(child.components);
          else if (child.type) walkCount([child]);
        }
      }
    }
  }
}
