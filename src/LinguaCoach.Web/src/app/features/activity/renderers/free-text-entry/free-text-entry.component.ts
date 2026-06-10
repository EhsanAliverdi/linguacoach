import { Component, Input, Output, EventEmitter, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface FreeTextEntryContent {
  situation?: string | null;
  prompt?: string | null;
  targetPhrases?: string[];
  exampleText?: string | null;
  coachNote?: string | null;
  wordCountTarget?: number | null;
}

export interface FreeTextAnswer {
  text: string;
}

@Component({
  selector: 'app-free-text-entry',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './free-text-entry.component.html',
})
export class FreeTextEntryComponent {
  @Input() content!: FreeTextEntryContent;
  @Input() disabled = false;
  @Input() attemptCount = 0;
  @Output() submitted = new EventEmitter<FreeTextAnswer>();

  draftText = '';

  get wordCount(): number {
    return this.draftText.trim().split(/\s+/).filter(Boolean).length;
  }

  submit(): void {
    if (this.draftText.trim()) {
      this.submitted.emit({ text: this.draftText });
    }
  }
}
