import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivityDto, ListeningAnswer, VocabAnswer } from '../../../core/models/activity.models';
import { ExerciseAnswerPayload, ExerciseRendererComponent } from '../exercise-renderer/exercise-renderer.component';
import { PracticeViewModel } from '../presenters/activity-page-presenter';

type PracticePageState =
  | 'loading' | 'learning' | 'writing' | 'submitting' | 'feedback' | 'error'
  | 'mic-unsupported' | 'mic-permission' | 'mic-denied'
  | 'ready' | 'recording' | 'recorded' | 'submitting-audio';

@Component({
  selector: 'app-activity-practice-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ExerciseRendererComponent],
  templateUrl: './activity-practice-page.component.html',
})
export class ActivityPracticePageComponent {
  @Input({ required: true }) activity!: ActivityDto;
  @Input({ required: true }) state!: PracticePageState;
  @Input({ required: true }) practice!: PracticeViewModel;
  @Input() isAiGenerated = false;
  @Input() attemptCount = 0;

  @Input() vocabAnswers: Record<string, string> = {};
  @Input() showHints: Record<string, boolean> = {};
  @Input() listeningAnswers: Record<string, string> = {};
  @Input() listeningResponseText = '';
  @Input() draftText = '';

  @Input() audioBlobUrl: string | null = null;
  @Input() errorMessage = '';

  @Output() listeningResponseTextChange = new EventEmitter<string>();
  @Output() draftTextChange = new EventEmitter<string>();

  @Output() toggleHint = new EventEmitter<string>();
  @Output() onSubmitVocab = new EventEmitter<void>();
  @Output() onSubmitListening = new EventEmitter<void>();
  @Output() onSubmit = new EventEmitter<void>();
  @Output() rendererSubmit = new EventEmitter<ExerciseAnswerPayload>();
  @Output() readOnlyDone = new EventEmitter<void>();
  @Output() startRecording = new EventEmitter<void>();
  @Output() stopRecording = new EventEmitter<void>();
  @Output() submitRecording = new EventEmitter<void>();
  @Output() reRecord = new EventEmitter<void>();
  @Output() backToDashboard = new EventEmitter<void>();

  vocabItemsFilled(): boolean {
    const items = this.activity.vocabItems ?? [];
    return items.length > 0 && items.every(i => (this.vocabAnswers[i.vocabularyItemId] ?? '').trim().length > 0);
  }

  listeningItemsFilled(): boolean {
    const questions = this.activity.listeningQuestions ?? [];
    return questions.length > 0 && questions.every(q => (this.listeningAnswers[q.id] ?? '').trim().length > 0);
  }

  get wordCount(): number {
    return this.draftText.trim().split(/\s+/).filter(Boolean).length;
  }

  appendPhrase(phrase: string): void {
    const sep = this.draftText.endsWith(' ') || !this.draftText ? '' : ' ';
    this.draftTextChange.emit(this.draftText + sep + phrase);
  }
}
