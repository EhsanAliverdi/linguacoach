// Shared, polymorphic question schema — mirrors the backend's QuestionContent hierarchy
// (LinguaCoach.Domain.Questions) discriminated by "type". Used by both onboarding and
// placement, on both the student-facing renderer and the admin editor.

export interface ChoiceOption {
  key: string;
  label: string;
}

export interface SingleChoiceQuestion {
  type: 'single_choice';
  id: string;
  questionText: string;
  choices: ChoiceOption[];
  correctAnswerKey?: string | null;
}

export interface MultipleChoiceQuestion {
  type: 'multiple_choice';
  id: string;
  questionText: string;
  choices: ChoiceOption[];
  correctAnswerKeys?: string[] | null;
}

export interface GapFillQuestion {
  type: 'gap_fill';
  id: string;
  questionText: string;
  correctAnswer?: string | null;
}

export interface FreeTextQuestion {
  type: 'free_text';
  id: string;
  questionText: string;
  placeholder?: string | null;
  maxLength?: number | null;
}

export type LeafQuestionContent =
  | SingleChoiceQuestion
  | MultipleChoiceQuestion
  | GapFillQuestion
  | FreeTextQuestion;

export interface ListeningGroupQuestion {
  type: 'listening_group';
  id: string;
  instructions?: string | null;
  audioScript: string;
  audioStorageKey?: string | null;
  audioContentType?: string | null;
  questions: LeafQuestionContent[];
}

export interface ReadingGroupQuestion {
  type: 'reading_group';
  id: string;
  instructions?: string | null;
  passage: string;
  questions: LeafQuestionContent[];
}

export type QuestionContent =
  | LeafQuestionContent
  | ListeningGroupQuestion
  | ReadingGroupQuestion;

export function isGroupQuestion(
  content: QuestionContent,
): content is ListeningGroupQuestion | ReadingGroupQuestion {
  return content.type === 'listening_group' || content.type === 'reading_group';
}

/** A submitted answer to one sub-question, addressed by QuestionContent.id. */
export interface QuestionAnswerItem {
  questionId: string;
  values: string[];
}

export interface QuestionAnswer {
  answers: QuestionAnswerItem[];
}

/** Flattens a QuestionContent tree into its leaf questions (a standalone leaf is its own list of one). */
export function flattenLeafQuestions(content: QuestionContent): LeafQuestionContent[] {
  return isGroupQuestion(content) ? content.questions : [content];
}
