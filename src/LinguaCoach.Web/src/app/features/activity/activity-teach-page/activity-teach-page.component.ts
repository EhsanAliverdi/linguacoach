import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivityDto } from '../../../core/models/activity.models';
import { TeachViewModel } from '../presenters/activity-page-presenter';

@Component({
  selector: 'app-activity-teach-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './activity-teach-page.component.html',
})
export class ActivityTeachPageComponent {
  @Input({ required: true }) activity!: ActivityDto;
  @Input({ required: true }) teach!: TeachViewModel;
  @Input() isAiGenerated = false;

  @Output() startPractice = new EventEmitter<void>();
  @Output() startWriting = new EventEmitter<void>();
  @Output() backToDashboard = new EventEmitter<void>();

  onCta(): void {
    if (this.teach.ctaAction === 'startPractice') {
      this.startPractice.emit();
    } else {
      this.startWriting.emit();
    }
  }
}
