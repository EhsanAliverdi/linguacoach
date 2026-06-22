import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-exercise-lesson-intro',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './exercise-lesson-intro.component.html',
})
export class ExerciseLessonIntroComponent {
  @Input() goal?: string | null;
}
