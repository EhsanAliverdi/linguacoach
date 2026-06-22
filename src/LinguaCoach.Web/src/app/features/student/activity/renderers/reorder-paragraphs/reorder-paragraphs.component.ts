import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExerciseLessonIntroComponent } from '../exercise-lesson-intro/exercise-lesson-intro.component';

export interface ReorderParagraphsItem {
  id: string;
  text: string;
}

export interface ReorderParagraphsContent {
  learningGoal?: string | null;
  instructions?: string | null;
  items: ReorderParagraphsItem[];
}

export interface ReorderParagraphsAnswer {
  orderedIds: string[];
}

@Component({
  selector: 'app-reorder-paragraphs',
  standalone: true,
  imports: [CommonModule, ExerciseLessonIntroComponent],
  templateUrl: './reorder-paragraphs.component.html',
})
export class ReorderParagraphsComponent {
  @Input() content!: ReorderParagraphsContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<ReorderParagraphsAnswer>();

  orderedItems: ReorderParagraphsItem[] = [];

  ngOnInit(): void {
    this.orderedItems = [...(this.content?.items ?? [])];
  }

  moveUp(index: number): void {
    if (this.disabled || index <= 0) return;
    const items = [...this.orderedItems];
    [items[index - 1], items[index]] = [items[index], items[index - 1]];
    this.orderedItems = items;
  }

  moveDown(index: number): void {
    if (this.disabled || index >= this.orderedItems.length - 1) return;
    const items = [...this.orderedItems];
    [items[index], items[index + 1]] = [items[index + 1], items[index]];
    this.orderedItems = items;
  }

  get canSubmit(): boolean {
    return this.orderedItems.length > 0;
  }

  submit(): void {
    if (this.canSubmit) {
      this.submitted.emit({ orderedIds: this.orderedItems.map(i => i.id) });
    }
  }
}
