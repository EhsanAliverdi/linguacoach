import { Component, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ActivityService } from '../../core/services/activity.service';
import { ExerciseTypeDefinition } from '../../core/models/admin.models';

@Component({
  selector: 'app-practice-gym',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './practice-gym.component.html',
  styleUrl: './practice-gym.component.css',
})
export class PracticeGymComponent implements OnInit {
  exerciseTypes = signal<ExerciseTypeDefinition[]>([]);
  selectingSkill = signal<string | null>(null);
  selectionMessage = signal<string | null>(null);

  constructor(
    private activityService: ActivityService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.activityService.getExerciseTypes().subscribe({
      next: items => this.exerciseTypes.set(items),
      error: () => this.exerciseTypes.set([]),
    });
  }

  selectSkill(skill: string): void {
    if (this.selectingSkill()) return;

    this.selectionMessage.set(null);
    this.selectingSkill.set(skill);

    this.activityService.getPracticeGymNext({ skill }).subscribe({
      next: result => {
        this.selectingSkill.set(null);
        if (!result.hasActivity || !result.activityId) {
          this.selectionMessage.set(result.reason ?? 'This skill is not ready in Practice Gym yet.');
          return;
        }

        this.router.navigate(['/activity'], {
          queryParams: { activityId: result.activityId, returnTo: '/practice' },
        });
      },
      error: () => {
        this.selectingSkill.set(null);
        this.selectionMessage.set('Practice is temporarily unavailable. Please try again shortly.');
      },
    });
  }

  isSelecting(skill: string): boolean {
    return this.selectingSkill() === skill;
  }

  hasSkillAvailable(skill: string): boolean {
    return this.exerciseTypes().some(type =>
      type.primarySkill === skill &&
      type.isEnabled &&
      type.isAvailableForGeneration &&
      type.implementationStatus === 'ready' &&
      type.supportsPracticeGym);
  }

  skillStatusText(skill: string): string {
    return this.hasSkillAvailable(skill) ? 'Available' : 'Coming soon';
  }

  isAvailable(key: string): boolean {
    const item = this.exerciseTypes().find(type => type.key === key);
    return !item || (item.isEnabled && item.isAvailableForGeneration && item.supportsPracticeGym);
  }

  statusText(key: string): string {
    const item = this.exerciseTypes().find(type => type.key === key);
    if (!item) return 'Coming soon';
    if (!item.isEnabled) return 'Disabled';
    if (item.implementationStatus !== 'ready') return 'Coming soon';
    if (!item.supportsPracticeGym) return 'Not in Practice Gym';
    return 'Available';
  }
}
