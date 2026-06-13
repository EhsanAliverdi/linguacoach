import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivityDto } from '../../../core/models/activity.models';

@Component({
  selector: 'app-activity-teach-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './activity-teach-page.component.html',
})
export class ActivityTeachPageComponent {
  @Input({ required: true }) activity!: ActivityDto;
  @Input() isVocabPractice = false;
  @Input() isListeningComprehension = false;
  @Input() isSpeakingRolePlay = false;
  @Input() usesExerciseRenderer = false;
  @Input() isAiGenerated = false;

  @Output() startPractice = new EventEmitter<void>();
  @Output() startWriting = new EventEmitter<void>();
  @Output() backToDashboard = new EventEmitter<void>();
}
