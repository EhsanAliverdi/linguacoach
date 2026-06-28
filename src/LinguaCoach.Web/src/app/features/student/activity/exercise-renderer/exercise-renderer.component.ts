import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivityDto, InteractionMode } from '../../../../core/models/activity.models';
import { ReadOnlyContent, ReadOnlyStepComponent } from '../renderers/read-only-step/read-only-step.component';
import { FreeTextEntryComponent, FreeTextEntryContent } from '../renderers/free-text-entry/free-text-entry.component';
import { MatchingPairsComponent, MatchingPairsContent } from '../renderers/matching-pairs/matching-pairs.component';
import { GapFillComponent, GapFillContent, GapFillItem } from '../renderers/gap-fill/gap-fill.component';
import { AudioAndFreeTextComponent, AudioAndFreeTextContent } from '../renderers/audio-and-free-text/audio-and-free-text.component';
import { AudioAndGapFillComponent, AudioAndGapFillContent, AudioGapItem } from '../renderers/audio-and-gap-fill/audio-and-gap-fill.component';
import { ChatReplyComponent, ChatReplyContent } from '../renderers/chat-reply/chat-reply.component';
import { EmailReplyComponent, EmailReplyContent } from '../renderers/email-reply/email-reply.component';
import { ReadingMultipleChoiceComponent, ReadingMultipleChoiceContent } from '../renderers/reading-multiple-choice/reading-multiple-choice.component';
import { ReadingMultipleChoiceMultiComponent, ReadingMultipleChoiceMultiContent } from '../renderers/reading-multiple-choice-multi/reading-multiple-choice-multi.component';
import { ReadingFillInBlanksComponent, ReadingFillInBlanksContent } from '../renderers/reading-fill-in-blanks/reading-fill-in-blanks.component';
import { ReorderParagraphsComponent, ReorderParagraphsContent } from '../renderers/reorder-paragraphs/reorder-paragraphs.component';
import { ReadingWritingFillInBlanksComponent, ReadingWritingFillInBlanksContent } from '../renderers/reading-writing-fill-in-blanks/reading-writing-fill-in-blanks.component';
import { ListeningFillInBlanksComponent, ListeningFillInBlanksContent } from '../renderers/listening-fill-in-blanks/listening-fill-in-blanks.component';
import { HighlightCorrectSummaryComponent, HighlightCorrectSummaryContent } from '../renderers/highlight-correct-summary/highlight-correct-summary.component';
import { HighlightIncorrectWordsComponent, HighlightIncorrectWordsContent } from '../renderers/highlight-incorrect-words/highlight-incorrect-words.component';
import { WriteFromDictationComponent, WriteFromDictationContent } from '../renderers/write-from-dictation/write-from-dictation.component';
import { SummarizeSpokenTextComponent, SummarizeSpokenTextContent } from '../renderers/summarize-spoken-text/summarize-spoken-text.component';
import { AnswerShortQuestionComponent, AnswerShortQuestionContent } from '../renderers/answer-short-question/answer-short-question.component';
import { ReadAloudComponent, ReadAloudContent } from '../renderers/read-aloud/read-aloud.component';
import { RepeatSentenceComponent, RepeatSentenceContent } from '../renderers/repeat-sentence/repeat-sentence.component';
import { RespondToSituationComponent, RespondToSituationContent } from '../renderers/respond-to-situation/respond-to-situation.component';
import { DescribeImageComponent, DescribeImageContent } from '../renderers/describe-image/describe-image.component';
import { RetellLectureComponent, RetellLectureContent } from '../renderers/retell-lecture/retell-lecture.component';
import { SummarizeGroupDiscussionComponent, SummarizeGroupDiscussionContent } from '../renderers/summarize-group-discussion/summarize-group-discussion.component';
import { AudioResponseComponent, AudioResponseContent } from '../renderers/audio-response/audio-response.component';

export type ExerciseAnswerPayload =
  | { kind: 'freeText'; text: string }
  | { kind: 'matchingPairs'; submittedPairs: { leftId: string; rightId: string }[] }
  | { kind: 'gapFill'; answers: { gapId: string; value: string }[] }
  | { kind: 'audioFreeText'; answers: { questionId: string; answer: string }[]; responseText: string }
  | { kind: 'audioGapFill'; answers: { questionId: string; answer: string }[] }
  | { kind: 'chatReply'; replyText: string }
  | { kind: 'emailReply'; subject: string; body: string }
  | { kind: 'multipleChoiceSingle'; selectedOptionId: string }
  | { kind: 'multipleChoiceMulti'; selectedOptionIds: string[] }
  | { kind: 'readingFillInBlanks'; answers: Record<string, string> }
  | { kind: 'reorderParagraphs'; orderedIds: string[] }
  | { kind: 'readingWritingFillInBlanks'; answers: Record<string, string> }
  | { kind: 'listeningFillInBlanks'; answers: Record<string, string> }
  | { kind: 'highlightCorrectSummary'; selectedOptionId: string }
  | { kind: 'highlightIncorrectWords'; selectedTokenIds: string[] }
  | { kind: 'writeFromDictation'; items: { itemId: string; submittedText: string }[] }
  | { kind: 'summarizeSpokenText'; summaryText: string }
  | { kind: 'answerShortQuestion'; items: { itemId: string; answerText: string }[] }
  | { kind: 'readAloud'; items: { itemId: string; answerText: string }[] }
  | { kind: 'repeatSentence'; items: { itemId: string; answerText: string }[] }
  | { kind: 'respondToSituation'; items: { itemId: string; answerText: string }[] }
  | { kind: 'describeImage'; items: { itemId: string; answerText: string }[] }
  | { kind: 'retellLecture'; items: { itemId: string; answerText: string }[] }
  | { kind: 'summarizeGroupDiscussion'; items: { itemId: string; answerText: string }[] }
  | { kind: 'audioResponse'; blob: Blob; mimeType: string; durationSeconds: number };

@Component({
  selector: 'app-exercise-renderer',
  standalone: true,
  imports: [
    CommonModule,
    ReadOnlyStepComponent,
    FreeTextEntryComponent,
    MatchingPairsComponent,
    GapFillComponent,
    AudioAndFreeTextComponent,
    AudioAndGapFillComponent,
    ChatReplyComponent,
    EmailReplyComponent,
    ReadingMultipleChoiceComponent,
    ReadingMultipleChoiceMultiComponent,
    ReadingFillInBlanksComponent,
    ReorderParagraphsComponent,
    ReadingWritingFillInBlanksComponent,
    ListeningFillInBlanksComponent,
    HighlightCorrectSummaryComponent,
    HighlightIncorrectWordsComponent,
    WriteFromDictationComponent,
    SummarizeSpokenTextComponent,
    AnswerShortQuestionComponent,
    ReadAloudComponent,
    RepeatSentenceComponent,
    RespondToSituationComponent,
    DescribeImageComponent,
    RetellLectureComponent,
    SummarizeGroupDiscussionComponent,
    AudioResponseComponent,
  ],
  templateUrl: './exercise-renderer.component.html',
})
export class ExerciseRendererComponent {
  @Input({ required: true }) activity!: ActivityDto;
  @Input() disabled = false;
  @Input() attemptCount = 0;
  @Output() answerSubmitted = new EventEmitter<ExerciseAnswerPayload>();
  @Output() readOnlyDone = new EventEmitter<void>();

  get mode(): InteractionMode {
    return this.activity.interactionMode ?? 'freeTextEntry';
  }

  get raw(): Record<string, unknown> {
    const content: unknown = this.activity.contentJson;
    if (!content) return {};
    if (typeof content !== 'string') return content as Record<string, unknown>;

    try {
      const parsed = JSON.parse(content);
      return parsed && typeof parsed === 'object' ? parsed as Record<string, unknown> : {};
    } catch {
      return {};
    }
  }

  get readOnlyContent(): ReadOnlyContent {
    const raw = this.raw;
    return {
      title: this.stringValue(raw['title']) ?? this.activity.title,
      coachNote: this.stringValue(raw['coachNote']) ?? this.stringValue(raw['instructions']),
      body: this.stringValue(raw['lessonSummary']) ?? this.stringValue(raw['body']),
      phrasesToRemember: this.stringArray(raw['phrasesToRemember'])
        ?? this.stringValue(raw['keyPhrase'])?.split('|').map(p => p.trim()).filter(Boolean),
      reflectionPrompts: this.stringArray(raw['reflectionPrompts']),
    };
  }

  // Unwraps practiceContent.exerciseData for module_stage_v1 activities.
  private get stagedExerciseData(): Record<string, unknown> {
    const raw = this.raw;
    if (this.stringValue(raw['schemaVersion']) === 'module_stage_v1') {
      const pc = this.objectValue(raw['practiceContent']);
      if (pc) {
        const ed = this.objectValue(pc['exerciseData']);
        if (ed) return ed;
      }
    }
    return {};
  }

  get freeTextContent(): FreeTextEntryContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const incomingEmail = this.objectValue(raw['incomingEmail']);
    const emailBody = this.stringValue(incomingEmail?.['body']);
    const prompt = this.stringValue(ed['prompt'])
      ?? this.stringValue(raw['taskDescription'])
      ?? this.stringValue(raw['prompt'])
      ?? this.activity.speakingPrompt
      ?? this.activity.learningGoal;

    const requirements = this.objectValue(ed['requirements']);

    return {
      situation: this.stringValue(ed['sourceText'])
        ?? this.stringValue(ed['topic'])
        ?? this.stringValue(raw['situation'])
        ?? this.activity.situation
        ?? this.activity.speakingScenario
        ?? emailBody,
      prompt,
      wordCountTarget: this.stringValue(requirements?.['targetWordCount'])
        ?? null,
      targetPhrases: this.stringArray(raw['targetPhrases'])
        ?? this.activity.targetPhrases
        ?? this.activity.suggestedPhrases
        ?? [],
      exampleText: this.stringValue(raw['exampleText'])
        ?? this.activity.exampleText,
      coachNote: this.stringValue(raw['learningGoal'])
        ?? this.activity.learningGoal
        ?? this.stringValue(raw['speakingGoal'])
        ?? this.activity.speakingGoal,
    };
  }

  get matchingPairsContent(): MatchingPairsContent {
    const raw = this.raw;
    const pairs = this.arrayValue(raw['pairs']).map((pair, index) => {
      const obj = this.objectValue(pair) ?? {};
      return {
        id: `phrase_${index}`,
        meaningId: `meaning_${index}`,
        phrase: this.stringValue(obj['phrase']) ?? this.stringValue(obj['left']) ?? '',
        meaning: this.stringValue(obj['meaning']) ?? this.stringValue(obj['right']) ?? '',
        context: this.stringValue(obj['context']),
      };
    }).filter(pair => pair.phrase || pair.meaning);

    return {
      learningGoal: this.stringValue(raw['learningGoal'])
        ?? this.stringValue(raw['teachingNote'])
        ?? this.activity.learningGoal,
      teachingNote: this.stringValue(raw['teachingNote']),
      instructions: this.stringValue(raw['instructions']) ?? this.activity.instructions,
      pairs,
    };
  }

  get gapFillContent(): GapFillContent {
    const raw = this.raw;
    const wordBank = this.stringArray(raw['wordBank']) ?? this.collectWordBankFromItems(raw['items']);
    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(raw['instructions']) ?? this.activity.instructions,
      items: this.mapGapItems(raw['items'], raw['passage']),
      wordBank,
    };
  }

  get audioFreeTextContent(): AudioAndFreeTextContent {
    const raw = this.raw;
    const questions = this.arrayValue(raw['questions']).map((q, index) => {
      const obj = this.objectValue(q) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? `q${index + 1}`,
        question: this.stringValue(obj['question']) ?? '',
      };
    }).filter(q => q.question);

    return {
      audioUrl: this.activity.audioUrl,
      audioDurationSeconds: this.activity.audioDurationSeconds,
      audioUnavailableMessage: this.activity.audioUnavailableMessage,
      scenario: this.stringValue(raw['scenario']) ?? this.activity.scenario,
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(raw['instructions']) ?? this.activity.instructions,
      questions: questions.length ? questions : (this.activity.listeningQuestions ?? []),
      responseTask: this.stringValue(this.objectValue(raw['responseTask'])?.['prompt'])
        ?? this.activity.responseTask?.prompt
        ?? null,
    };
  }

  get audioGapFillContent(): AudioAndGapFillContent {
    const raw = this.raw;
    return {
      audioUrl: this.activity.audioUrl,
      audioUnavailableMessage: this.activity.audioUnavailableMessage,
      scenario: this.stringValue(raw['scenario']) ?? this.activity.scenario,
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(raw['instructions']) ?? this.activity.instructions,
      gaps: this.mapAudioGaps(raw['gaps'], raw['gappedTranscript']),
      wordBank: this.stringArray(raw['wordBank']),
    };
  }

  get chatReplyContent(): ChatReplyContent {
    const raw = this.raw;
    const thread = this.arrayValue(raw['chatThread']).map((m, index) => {
      const obj = this.objectValue(m) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? String(index + 1),
        sender: this.stringValue(obj['sender']) ?? this.stringValue(raw['colleagueName']) ?? 'Colleague',
        text: this.stringValue(obj['message']) ?? this.stringValue(obj['text']) ?? '',
      };
    }).filter(m => m.text);

    const fallbackText = this.stringValue(raw['scenario']) ?? this.activity.situation;
    return {
      scenario: this.stringValue(raw['scenario']) ?? this.activity.situation,
      learningGoal: this.stringValue(raw['learningGoal']),
      instructions: this.stringValue(raw['toneGuidance']),
      messages: thread.length ? thread : [{
        id: '1',
        sender: this.stringValue(raw['colleagueRole']) ?? 'Colleague',
        text: fallbackText ?? 'Please reply professionally to this workplace message.',
      }],
      replyPrompt: 'Write your reply',
      targetPhrases: this.stringArray(raw['targetPhrases']) ?? this.activity.targetPhrases,
      wordCountTarget: this.numberValue(raw['wordLimit']),
    };
  }

  get emailReplyContent(): EmailReplyContent {
    const raw = this.raw;
    return {
      situation: this.stringValue(raw['situation']) ?? this.activity.situation,
      audience: this.stringValue(raw['audience']),
      suggestedSubject: this.stringValue(raw['suggestedSubject']),
      targetPhrases: this.stringArray(raw['targetPhrases']) ?? this.activity.targetPhrases ?? [],
      exampleText: this.stringValue(raw['exampleText']) ?? this.activity.exampleText,
      coachNote: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      wordCountTarget: this.numberValue(raw['wordLimit']),
    };
  }

  get readingMultipleChoiceContent(): ReadingMultipleChoiceContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const options = this.arrayValue(ed['options'] ?? raw['options']).map((opt, index) => {
      const obj = this.objectValue(opt) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? String.fromCharCode(65 + index),
        text: this.stringValue(obj['text']) ?? '',
      };
    });

    const distractorExplanationsObj = this.objectValue(ed['distractorExplanations'] ?? raw['distractorExplanations']);
    const distractorExplanations = distractorExplanationsObj
      ? Object.fromEntries(
          Object.entries(distractorExplanationsObj)
            .map(([key, value]) => [key, this.stringValue(value) ?? ''])
            .filter(([, value]) => value),
        )
      : null;

    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      passage: this.stringValue(ed['passage'] ?? raw['passage']),
      incompleteText: this.stringValue(ed['incompleteText'] ?? raw['incompleteText']),
      question: this.stringValue(ed['question'] ?? raw['question']) ?? '',
      options,
      correctOptionId: this.stringValue(ed['correctOptionId'] ?? raw['correctOptionId']),
      explanation: this.stringValue(ed['explanation'] ?? raw['explanation']),
      distractorExplanations,
      audioScript: this.stringValue(ed['audioScript']),
      audioUrl: this.stringValue(ed['audioUrl']) ?? this.activity.audioUrl,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
    };
  }

  get readingMultipleChoiceMultiContent(): ReadingMultipleChoiceMultiContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const options = this.arrayValue(ed['options'] ?? raw['options']).map((opt, index) => {
      const obj = this.objectValue(opt) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? String.fromCharCode(65 + index),
        text: this.stringValue(obj['text']) ?? '',
      };
    });

    const optionExplanationsObj = this.objectValue(ed['optionExplanations'] ?? raw['optionExplanations']);
    const optionExplanations = optionExplanationsObj
      ? Object.fromEntries(
          Object.entries(optionExplanationsObj)
            .map(([key, value]) => [key, this.stringValue(value) ?? ''])
            .filter(([, value]) => value),
        )
      : null;

    const correctOptionIds = this.stringArray(ed['correctOptionIds'] ?? raw['correctOptionIds']) ?? null;

    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      passage: this.stringValue(ed['passage'] ?? raw['passage']),
      question: this.stringValue(ed['question'] ?? raw['question']) ?? '',
      options,
      correctOptionIds,
      explanation: this.stringValue(ed['explanation'] ?? raw['explanation']),
      optionExplanations,
      audioScript: this.stringValue(ed['audioScript']),
      audioUrl: this.stringValue(ed['audioUrl']) ?? this.activity.audioUrl,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
    };
  }

  get readingFillInBlanksContent(): ReadingFillInBlanksContent {
    const raw = this.raw;
    const gaps = this.arrayValue(raw['gaps']).map((gap) => {
      const obj = this.objectValue(gap) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? '',
        options: this.stringArray(obj['options']) ?? [],
      };
    }).filter(g => g.id);

    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(raw['instructions']) ?? this.activity.instructions,
      passageWithBlanks: this.stringValue(raw['passageWithBlanks']) ?? '',
      gaps,
    };
  }

  onReadingFillInBlanksSubmitted(answer: { answers: Record<string, string> }): void {
    this.answerSubmitted.emit({ kind: 'readingFillInBlanks', answers: answer.answers });
  }

  get readingWritingFillInBlanksContent(): ReadingWritingFillInBlanksContent {
    const raw = this.raw;
    const gaps = this.arrayValue(raw['gaps']).map((gap) => {
      const obj = this.objectValue(gap) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? '',
        options: this.stringArray(obj['options']) ?? [],
      };
    }).filter(g => g.id);

    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(raw['instructions']) ?? this.activity.instructions,
      passageWithBlanks: this.stringValue(raw['passageWithBlanks']) ?? '',
      gaps,
    };
  }

  onReadingWritingFillInBlanksSubmitted(answer: { answers: Record<string, string> }): void {
    this.answerSubmitted.emit({ kind: 'readingWritingFillInBlanks', answers: answer.answers });
  }

  get listeningFillInBlanksContent(): ListeningFillInBlanksContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const gaps = this.arrayValue(ed['gaps'] ?? raw['gaps']).map((gap) => {
      const obj = this.objectValue(gap) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? '',
        options: this.stringArray(obj['options']) ?? [],
      };
    }).filter(g => g.id);

    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      audioScript: this.stringValue(ed['audioScript'] ?? raw['audioScript']),
      audioUrl: this.stringValue(ed['audioUrl'] ?? raw['audioUrl']) ?? this.activity.audioUrl,
      passageWithBlanks: this.stringValue(ed['passageWithBlanks'] ?? raw['passageWithBlanks']) ?? '',
      gaps,
    };
  }

  onListeningFillInBlanksSubmitted(answer: { answers: Record<string, string> }): void {
    this.answerSubmitted.emit({ kind: 'listeningFillInBlanks', answers: answer.answers });
  }

  get highlightCorrectSummaryContent(): HighlightCorrectSummaryContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const options = this.arrayValue(ed['options'] ?? raw['options']).map((opt, index) => {
      const obj = this.objectValue(opt) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? String.fromCharCode(65 + index),
        text: this.stringValue(obj['text']) ?? '',
      };
    });

    const distractorExplanationsObj = this.objectValue(ed['distractorExplanations'] ?? raw['distractorExplanations']);
    const distractorExplanations = distractorExplanationsObj
      ? Object.fromEntries(
          Object.entries(distractorExplanationsObj)
            .map(([key, value]) => [key, this.stringValue(value) ?? ''])
            .filter(([, value]) => value),
        )
      : null;

    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      audioScript: this.stringValue(ed['audioScript'] ?? raw['audioScript']),
      audioUrl: this.stringValue(ed['audioUrl'] ?? raw['audioUrl']) ?? this.activity.audioUrl,
      question: this.stringValue(ed['question'] ?? raw['question']) ?? '',
      options,
      correctOptionId: this.stringValue(ed['correctOptionId'] ?? raw['correctOptionId']),
      explanation: this.stringValue(ed['explanation'] ?? raw['explanation']),
      distractorExplanations,
    };
  }

  onHighlightCorrectSummarySubmitted(answer: { selectedOptionId: string }): void {
    this.answerSubmitted.emit({ kind: 'highlightCorrectSummary', selectedOptionId: answer.selectedOptionId });
  }

  get highlightIncorrectWordsContent(): HighlightIncorrectWordsContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const tokens = this.arrayValue(ed['tokens'] ?? raw['tokens']).map((tok, index) => {
      const obj = this.objectValue(tok) ?? {};
      const positionRaw = obj['position'];
      const position = typeof positionRaw === 'number' ? positionRaw : index;
      return {
        id: this.stringValue(obj['id']) ?? `t${index}`,
        text: this.stringValue(obj['text']) ?? '',
        position,
      };
    });

    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      audioScript: this.stringValue(ed['audioScript'] ?? raw['audioScript']),
      audioUrl: this.stringValue(ed['audioUrl'] ?? raw['audioUrl']) ?? this.activity.audioUrl,
      displayTranscript: this.stringValue(ed['displayTranscript'] ?? raw['displayTranscript']),
      tokens,
      question: this.stringValue(ed['question'] ?? raw['question']),
    };
  }

  onHighlightIncorrectWordsSubmitted(answer: { selectedTokenIds: string[] }): void {
    this.answerSubmitted.emit({ kind: 'highlightIncorrectWords', selectedTokenIds: answer.selectedTokenIds });
  }

  get writeFromDictationContent(): WriteFromDictationContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const items = this.arrayValue(ed['items'] ?? raw['items']).map((item, index) => {
      const obj = this.objectValue(item) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? `item${index + 1}`,
        audioScript: this.stringValue(obj['audioScript']),
        audioUrl: this.stringValue(obj['audioUrl']),
      };
    });

    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      items,
    };
  }

  onWriteFromDictationSubmitted(answer: { items: { itemId: string; submittedText: string }[] }): void {
    this.answerSubmitted.emit({ kind: 'writeFromDictation', items: answer.items });
  }

  get summarizeSpokenTextContent(): SummarizeSpokenTextContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      audioScript: this.stringValue(ed['audioScript'] ?? raw['audioScript']),
      audioUrl: this.stringValue(ed['audioUrl'] ?? raw['audioUrl']) ?? this.activity.audioUrl,
      prompt: this.stringValue(ed['prompt'] ?? raw['prompt']),
      summaryRequirements: this.stringArray(ed['summaryRequirements'] ?? raw['summaryRequirements']) ?? [],
    };
  }

  onSummarizeSpokenTextSubmitted(answer: { summaryText: string }): void {
    this.answerSubmitted.emit({ kind: 'summarizeSpokenText', summaryText: answer.summaryText });
  }

  get answerShortQuestionContent(): AnswerShortQuestionContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const items = this.arrayValue(ed['items'] ?? raw['items']).map((item, index) => {
      const obj = this.objectValue(item) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? `q${index + 1}`,
        question: this.stringValue(obj['question']),
        audioScript: this.stringValue(obj['audioScript']),
        audioUrl: this.stringValue(obj['audioUrl']),
      };
    });
    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      items,
    };
  }

  onAnswerShortQuestionSubmitted(answer: { items: { itemId: string; answerText: string }[] }): void {
    this.answerSubmitted.emit({ kind: 'answerShortQuestion', items: answer.items });
  }

  get readAloudContent(): ReadAloudContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const items = this.arrayValue(ed['items'] ?? raw['items']).map((item, index) => {
      const obj = this.objectValue(item) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? `t${index + 1}`,
        text: this.stringValue(obj['text']),
        displayTitle: this.stringValue(obj['displayTitle']),
        difficulty: this.stringValue(obj['difficulty']),
        focusAreas: this.arrayValue(obj['focusAreas']).map(f => this.stringValue(f) ?? '').filter(f => f),
        explanation: this.stringValue(obj['explanation']),
      };
    });
    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      items,
    };
  }

  onReadAloudSubmitted(answer: { items: { itemId: string; answerText: string }[] }): void {
    this.answerSubmitted.emit({ kind: 'readAloud', items: answer.items });
  }

  get repeatSentenceContent(): RepeatSentenceContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const items = this.arrayValue(ed['items'] ?? raw['items']).map((item, index) => {
      const obj = this.objectValue(item) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? `s${index + 1}`,
        sentence: this.stringValue(obj['sentence']),
        audioScript: this.stringValue(obj['audioScript']),
        audioUrl: this.stringValue(obj['audioUrl']),
        displayTitle: this.stringValue(obj['displayTitle']),
        difficulty: this.stringValue(obj['difficulty']),
        focusAreas: this.arrayValue(obj['focusAreas']).map(f => this.stringValue(f) ?? '').filter(f => f),
        explanation: this.stringValue(obj['explanation']),
      };
    });
    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      items,
    };
  }

  onRepeatSentenceSubmitted(answer: { items: { itemId: string; answerText: string }[] }): void {
    this.answerSubmitted.emit({ kind: 'repeatSentence', items: answer.items });
  }

  get respondToSituationContent(): RespondToSituationContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const items = this.arrayValue(ed['items'] ?? raw['items']).map((item, index) => {
      const obj = this.objectValue(item) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? `sit${index + 1}`,
        situation: this.stringValue(obj['situation']) ?? '',
        contextLabel: this.stringValue(obj['contextLabel']),
        role: this.stringValue(obj['role']),
        audience: this.stringValue(obj['audience']),
        prompt: this.stringValue(obj['prompt']),
        audioUrl: this.stringValue(obj['audioUrl']),
        audioScript: this.stringValue(obj['audioScript']),
        focusAreas: this.arrayValue(obj['focusAreas']).map(f => this.stringValue(f) ?? '').filter(f => f),
        explanation: this.stringValue(obj['explanation']),
      };
    });
    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      items,
    };
  }

  onRespondToSituationSubmitted(answer: { items: { itemId: string; answerText: string }[] }): void {
    this.answerSubmitted.emit({ kind: 'respondToSituation', items: answer.items });
  }

  get describeImageContent(): DescribeImageContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const items = this.arrayValue(ed['items'] ?? raw['items']).map((item, index) => {
      const obj = this.objectValue(item) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? `img${index + 1}`,
        imagePrompt: this.stringValue(obj['imagePrompt']) ?? '',
        imageDescription: this.stringValue(obj['imageDescription']),
        imageUrl: this.stringValue(obj['imageUrl']),
        displayTitle: this.stringValue(obj['displayTitle']),
        contextLabel: this.stringValue(obj['contextLabel']),
        focusAreas: this.arrayValue(obj['focusAreas']).map(f => this.stringValue(f) ?? '').filter(f => f),
        explanation: this.stringValue(obj['explanation']),
      };
    });
    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      items,
    };
  }

  onDescribeImageSubmitted(answer: { items: { itemId: string; answerText: string }[] }): void {
    this.answerSubmitted.emit({ kind: 'describeImage', items: answer.items });
  }

  get retellLectureContent(): RetellLectureContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const items = this.arrayValue(ed['items'] ?? raw['items']).map((item, index) => {
      const obj = this.objectValue(item) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? `lec${index + 1}`,
        lectureTitle: this.stringValue(obj['lectureTitle']) ?? '',
        lectureTopic: this.stringValue(obj['lectureTopic']),
        audioScript: this.stringValue(obj['audioScript']) ?? '',
        audioUrl: this.stringValue(obj['audioUrl']),
        contextLabel: this.stringValue(obj['contextLabel']),
        difficulty: this.stringValue(obj['difficulty']),
        focusAreas: this.arrayValue(obj['focusAreas']).map(f => this.stringValue(f) ?? '').filter(f => f),
      };
    });
    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      items,
    };
  }

  onRetellLectureSubmitted(answer: { items: { itemId: string; answerText: string }[] }): void {
    this.answerSubmitted.emit({ kind: 'retellLecture', items: answer.items });
  }

  get summarizeGroupDiscussionContent(): SummarizeGroupDiscussionContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    const items = this.arrayValue(ed['items'] ?? raw['items']).map((item, index) => {
      const obj = this.objectValue(item) ?? {};
      const speakers = this.arrayValue(obj['speakers']).map(s => {
        const sp = this.objectValue(s) ?? {};
        return {
          name: this.stringValue(sp['name']) ?? '',
          role: this.stringValue(sp['role']),
          viewpoint: this.stringValue(sp['viewpoint']),
        };
      }).filter(s => s.name);
      return {
        id: this.stringValue(obj['id']) ?? `disc${index + 1}`,
        discussionTitle: this.stringValue(obj['discussionTitle']) ?? '',
        discussionTopic: this.stringValue(obj['discussionTopic']),
        audioScript: this.stringValue(obj['audioScript']) ?? '',
        audioUrl: this.stringValue(obj['audioUrl']),
        contextLabel: this.stringValue(obj['contextLabel']),
        speakers: speakers.length > 0 ? speakers : null,
        focusAreas: this.arrayValue(obj['focusAreas']).map(f => this.stringValue(f) ?? '').filter(f => f),
      };
    });
    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(this.objectValue(raw['practiceContent'])?.['instructions'])
        ?? this.stringValue(raw['instructions'])
        ?? this.activity.instructions,
      scenario: this.stringValue(this.objectValue(raw['practiceContent'])?.['scenario']),
      items,
    };
  }

  onSummarizeGroupDiscussionSubmitted(answer: { items: { itemId: string; answerText: string }[] }): void {
    this.answerSubmitted.emit({ kind: 'summarizeGroupDiscussion', items: answer.items });
  }

  get audioResponseContent(): AudioResponseContent {
    const raw = this.raw;
    const ed = this.stagedExerciseData;
    return {
      prompt: this.stringValue(ed['prompt'] ?? raw['prompt'])
        ?? this.stringValue(raw['taskDescription'])
        ?? this.activity.speakingPrompt
        ?? this.activity.learningGoal,
      situation: this.stringValue(ed['sourceText'] ?? ed['topic'] ?? raw['situation'])
        ?? this.activity.situation
        ?? this.activity.speakingScenario,
    };
  }

  onAudioResponseSubmitted(answer: { blob: Blob; mimeType: string; durationSeconds: number }): void {
    this.answerSubmitted.emit({ kind: 'audioResponse', ...answer });
  }

  get reorderParagraphsContent(): ReorderParagraphsContent {
    const raw = this.raw;
    const items = this.arrayValue(raw['items']).map((item) => {
      const obj = this.objectValue(item) ?? {};
      return {
        id: this.stringValue(obj['id']) ?? '',
        text: this.stringValue(obj['text']) ?? '',
      };
    }).filter(i => i.id);

    return {
      learningGoal: this.stringValue(raw['learningGoal']) ?? this.activity.learningGoal,
      instructions: this.stringValue(raw['instructions']) ?? this.activity.instructions,
      items,
    };
  }

  onReorderParagraphsSubmitted(answer: { orderedIds: string[] }): void {
    this.answerSubmitted.emit({ kind: 'reorderParagraphs', orderedIds: answer.orderedIds });
  }

  onReadingMultipleChoiceMultiSubmitted(answer: { selectedOptionIds: string[] }): void {
    this.answerSubmitted.emit({ kind: 'multipleChoiceMulti', selectedOptionIds: answer.selectedOptionIds });
  }

  onReadingMultipleChoiceSubmitted(answer: { selectedOptionId: string }): void {
    this.answerSubmitted.emit({ kind: 'multipleChoiceSingle', selectedOptionId: answer.selectedOptionId });
  }

  onFreeTextSubmitted(answer: { text: string }): void {
    this.answerSubmitted.emit({ kind: 'freeText', text: answer.text });
  }

  onEmailReplySubmitted(answer: { subject: string; body: string }): void {
    this.answerSubmitted.emit({ kind: 'emailReply', subject: answer.subject, body: answer.body });
  }

  onMatchingSubmitted(answer: { selections: Record<string, string> }): void {
    this.answerSubmitted.emit({
      kind: 'matchingPairs',
      submittedPairs: Object.entries(answer.selections).map(([leftId, rightId]) => ({ leftId, rightId })),
    });
  }

  onGapFillSubmitted(answer: { answers: Record<string, string> }): void {
    this.answerSubmitted.emit({
      kind: 'gapFill',
      answers: Object.entries(answer.answers).map(([gapId, value]) => ({ gapId, value })),
    });
  }

  onAudioFreeTextSubmitted(answer: { answers: Record<string, string>; responseText?: string }): void {
    this.answerSubmitted.emit({
      kind: 'audioFreeText',
      answers: Object.entries(answer.answers).map(([questionId, value]) => ({ questionId, answer: value })),
      responseText: answer.responseText ?? '',
    });
  }

  onAudioGapFillSubmitted(answer: { answers: Record<string, string> }): void {
    this.answerSubmitted.emit({
      kind: 'audioGapFill',
      answers: Object.entries(answer.answers).map(([questionId, value]) => ({ questionId, answer: value })),
    });
  }

  onChatReplySubmitted(answer: { text: string }): void {
    this.answerSubmitted.emit({ kind: 'chatReply', replyText: answer.text });
  }

  private mapGapItems(itemsValue: unknown, passageValue: unknown): GapFillItem[] {
    const items = this.arrayValue(itemsValue);
    if (items.length) {
      return items.map((item, index) => {
        const obj = this.objectValue(item) ?? {};
        const id = this.stringValue(obj['id']) ?? `gap_${index + 1}`;
        const sentence = this.stringValue(obj['sentence']) ?? '';
        const parts = this.splitBlank(sentence);
        return {
          id,
          before: parts.before || sentence,
          after: parts.after,
          acceptedAnswers: this.stringArray(obj['acceptedAnswers'])
            ?? this.stringValue(obj['answer'])?.split('|').map(a => a.trim()).filter(Boolean),
          hint: this.stringValue(obj['hint']),
        };
      });
    }

    const passage = this.stringValue(passageValue) ?? '';
    const markers = [...passage.matchAll(/___\[(.+?)\]___/g)];
    return markers.map((match, index) => {
      const previousEnd = index === 0 ? 0 : markers[index - 1].index! + markers[index - 1][0].length;
      const nextStart = index < markers.length - 1 ? markers[index + 1].index! : passage.length;
      return {
        id: match[1],
        before: passage.slice(previousEnd, match.index).trim(),
        after: passage.slice(match.index! + match[0].length, nextStart).trim(),
      };
    });
  }

  private mapAudioGaps(gapsValue: unknown, transcriptValue: unknown): AudioGapItem[] {
    const gaps = this.arrayValue(gapsValue);
    if (gaps.length) {
      return gaps.map((gap, index) => {
        const obj = this.objectValue(gap) ?? {};
        const id = this.stringValue(obj['id']) ?? String(index + 1);
        const sentence = this.stringValue(obj['sentenceWithBlank']) ?? '';
        const parts = this.splitBlank(sentence);
        return { id, before: parts.before || sentence, after: parts.after };
      });
    }

    return this.mapGapItems(undefined, transcriptValue).map(g => ({ id: g.id, before: g.before, after: g.after }));
  }

  private splitBlank(sentence: string): { before: string; after: string } {
    const token = sentence.includes('_____') ? '_____' : sentence.includes('___') ? '___' : '';
    if (!token) return { before: sentence, after: '' };
    const [before, ...rest] = sentence.split(token);
    return { before: before.trim(), after: rest.join(token).trim() };
  }

  private collectWordBankFromItems(itemsValue: unknown): string[] | undefined {
    const words = new Set<string>();
    for (const item of this.arrayValue(itemsValue)) {
      const obj = this.objectValue(item) ?? {};
      const answer = this.stringValue(obj['answer']);
      if (answer) words.add(answer);
      for (const distractor of this.stringArray(obj['distractors']) ?? []) {
        words.add(distractor);
      }
    }
    return words.size ? [...words] : undefined;
  }

  private arrayValue(value: unknown): unknown[] {
    return Array.isArray(value) ? value : [];
  }

  private objectValue(value: unknown): Record<string, unknown> | null {
    return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null;
  }

  private stringValue(value: unknown): string | null {
    return typeof value === 'string' && value.trim() ? value : null;
  }

  private stringArray(value: unknown): string[] | undefined {
    return Array.isArray(value)
      ? value.filter((item): item is string => typeof item === 'string' && item.trim().length > 0)
      : undefined;
  }

  private numberValue(value: unknown): number | null {
    return typeof value === 'number' ? value : null;
  }
}

