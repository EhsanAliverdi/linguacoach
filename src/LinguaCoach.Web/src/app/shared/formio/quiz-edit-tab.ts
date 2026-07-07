/** Form.io editForm tab definitions for the builder's per-component "Quiz" tab — replaces the
 * old hand-typed "Scoring rules (JSON)" textarea. Passed into `Formio.builder(el, schema, {
 * editForm: QUIZ_EDIT_FORM_OVERRIDES })`; Form.io merges each entry in as an additional tab next
 * to its built-in Display/Data/Validation tabs (see WebformBuilder.editComponent() ->
 * Components.components[type].editForm(overrides)). These are plain builder-UI schemas — they
 * are never part of the saved component tree; only whatever the admin fills in under
 * `component.quiz` becomes part of the authoring schema.
 *
 * The admin only ever picks Enable + a correct answer + optional points — never a "kind", since
 * that's implied by the component type. `finalizeQuizAnnotations` (quiz-scoring-rule.model.ts)
 * derives `quiz.rule.kind` from the component's own `type` right before save. */

const ENABLE_TOGGLE = {
  type: 'checkbox',
  input: true,
  key: 'quiz.enabled',
  label: 'Enable scoring for this question',
  weight: 0,
};

const ENABLED_CONDITIONAL = { json: { '===': [{ var: 'data.quiz.enabled' }, true] } };

const POINTS_FIELD = {
  type: 'number',
  input: true,
  key: 'quiz.rule.points',
  label: 'Points',
  defaultValue: 1,
  weight: 20,
  conditional: ENABLED_CONDITIONAL,
};

/** Reads the component's own already-authored option values (the "Values" datagrid on the Data
 * tab) into a select/selectboxes picker, via Form.io's dataSrc:'custom' mechanism — `data` in the
 * custom script is the whole edit-form's data model, of which "values" is a sibling property. */
const VALUES_DATA_SRC = { custom: 'values = data.values || [];' };

function quizTab(components: unknown[]) {
  return { key: 'quiz', label: 'Quiz', weight: 15, components };
}

const SINGLE_CHOICE_TAB = quizTab([
  ENABLE_TOGGLE,
  {
    type: 'select',
    input: true,
    key: 'quiz.rule.correctAnswer',
    label: 'Correct answer',
    dataSrc: 'custom',
    valueProperty: 'value',
    data: VALUES_DATA_SRC,
    weight: 10,
    conditional: ENABLED_CONDITIONAL,
  },
  POINTS_FIELD,
]);

const MULTIPLE_CHOICE_TAB = quizTab([
  ENABLE_TOGGLE,
  {
    type: 'selectboxes',
    input: true,
    key: 'quiz.rule.correctAnswers',
    label: 'Correct answer(s)',
    dataSrc: 'custom',
    valueProperty: 'value',
    data: VALUES_DATA_SRC,
    weight: 10,
    conditional: ENABLED_CONDITIONAL,
  },
  POINTS_FIELD,
]);

const CHECKBOX_TAB = quizTab([
  ENABLE_TOGGLE,
  {
    type: 'select',
    input: true,
    key: 'quiz.correctBoolAnswer',
    label: 'Correct answer',
    dataSrc: 'values',
    data: { values: [{ label: 'Checked (true)', value: 'true' }, { label: 'Unchecked (false)', value: 'false' }] },
    weight: 10,
    conditional: ENABLED_CONDITIONAL,
  },
  POINTS_FIELD,
]);

const TEXT_TAB = quizTab([
  ENABLE_TOGGLE,
  {
    type: 'textfield',
    input: true,
    key: 'quiz.rule.correctAnswer',
    label: 'Correct answer',
    weight: 10,
    conditional: ENABLED_CONDITIONAL,
  },
  {
    type: 'select',
    input: true,
    key: 'quiz.matchMode',
    label: 'Match mode',
    dataSrc: 'values',
    data: {
      values: [
        { label: 'Normalized (case/whitespace-insensitive, recommended)', value: 'text_normalized' },
        { label: 'Exact match', value: 'text_exact' },
      ],
    },
    defaultValue: 'text_normalized',
    weight: 15,
    conditional: ENABLED_CONDITIONAL,
  },
  POINTS_FIELD,
]);

/** Each value here becomes `extend` in Form.io's `Component.form.js`
 * (`ComponentClass.editForm(overrides)` -> `default_1(...extend)`), which does
 * `.concat(extend.map(items => ({ type: 'tabs', key: 'tabs', components: cloneDeep(items) })))` —
 * i.e. `items` must be the bare ARRAY of tab-definitions itself, not an object wrapping a
 * `components` property, or the merge silently produces a non-array `.components` and the tab
 * never renders. */
export const QUIZ_EDIT_FORM_OVERRIDES = {
  radio: [SINGLE_CHOICE_TAB],
  select: [SINGLE_CHOICE_TAB],
  selectboxes: [MULTIPLE_CHOICE_TAB],
  checkbox: [CHECKBOX_TAB],
  textfield: [TEXT_TAB],
  textarea: [TEXT_TAB],
};
