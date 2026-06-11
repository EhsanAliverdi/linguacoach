import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivityDto, InteractionMode } from '../../../core/models/activity.models';
import { ReadOnlyContent, ReadOnlyStepComponent } from '../renderers/read-only-step/read-only-step.component';
import { FreeTextEntryComponent, FreeTextEntryContent } from '../renderers/free-text-entry/free-text-entry.component';
import { MatchingPairsComponent, MatchingPairsContent } from '../renderers/matching-pairs/matching-pairs.component';
import { GapFillComponent, GapFillContent, GapFillItem } from '../renderers/gap-fill/gap-fill.component';
import { AudioAndFreeTextComponent, AudioAndFreeTextContent } from '../renderers/audio-and-free-text/audio-and-free-text.component';
import { AudioAndGapFillComponent, AudioAndGapFillContent, AudioGapItem } from '../renderers/audio-and-gap-fill/audio-and-gap-fill.component';
import { ChatReplyComponent, ChatReplyContent } from '../renderers/chat-reply/chat-reply.component';

export type ExerciseAnswerPayload =
  | { kind: 'freeText'; text: string }
  | { kind: 'matchingPairs'; submittedPairs: { leftId: string; rightId: string }[] }
  | { kind: 'gapFill'; answers: { gapId: string; value: string }[] }
  | { kind: 'audioFreeText'; answers: { questionId: string; answer: string }[]; responseText: string }
  | { kind: 'audioGapFill'; answers: { questionId: string; answer: string }[] }
  | { kind: 'chatReply'; replyText: string };

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

  get freeTextContent(): FreeTextEntryContent {
    const raw = this.raw;
    const incomingEmail = this.objectValue(raw['incomingEmail']);
    const emailBody = this.stringValue(incomingEmail?.['body']);
    const prompt = this.stringValue(raw['taskDescription'])
      ?? this.stringValue(raw['prompt'])
      ?? this.activity.speakingPrompt
      ?? this.activity.learningGoal;

    return {
      situation: this.stringValue(raw['situation'])
        ?? this.activity.situation
        ?? this.activity.speakingScenario
        ?? emailBody,
      prompt,
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
      wordCountTarget: this.numberValue(raw['wordLimit']),
    };
  }

  get matchingPairsContent(): MatchingPairsContent {
    const raw = this.raw;
    const pairs = this.arrayValue(raw['pairs']).map((pair, index) => {
      const obj = this.objectValue(pair) ?? {};
      const id = this.stringValue(obj['id']) ?? String(index + 1);
      return {
        id,
        phrase: this.stringValue(obj['phrase']) ?? this.stringValue(obj['left']) ?? '',
        meaning: this.stringValue(obj['meaning']) ?? this.stringValue(obj['right']) ?? '',
      };
    }).filter(pair => pair.phrase || pair.meaning);

    return {
      instructions: this.stringValue(raw['instructions']) ?? this.activity.instructions,
      pairs,
    };
  }

  get gapFillContent(): GapFillContent {
    const raw = this.raw;
    const wordBank = this.stringArray(raw['wordBank']) ?? this.collectWordBankFromItems(raw['items']);
    return {
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

  onFreeTextSubmitted(answer: { text: string }): void {
    this.answerSubmitted.emit({ kind: 'freeText', text: answer.text });
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
        const id = this.stringValue(obj['id']) ?? String(index + 1);
        const sentence = this.stringValue(obj['sentence']) ?? '';
        const parts = this.splitBlank(sentence);
        return {
          id,
          before: parts.before || sentence,
          after: parts.after,
          acceptedAnswers: this.stringArray(obj['acceptedAnswers'])
            ?? this.stringValue(obj['answer'])?.split('|').map(a => a.trim()).filter(Boolean),
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
