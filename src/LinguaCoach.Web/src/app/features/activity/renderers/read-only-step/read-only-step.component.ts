import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface ReadOnlyContent {
  coachNote?: string | null;
  phrasesToRemember?: string[];
  reflectionPrompts?: string[];
  title?: string | null;
  body?: string | null;
}

@Component({
  selector: 'app-read-only-step',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './read-only-step.component.html',
})
export class ReadOnlyStepComponent {
  @Input() content!: ReadOnlyContent;
  @Input() title = '';
  @Output() done = new EventEmitter<void>();
}
