import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
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

  constructor(private activityService: ActivityService) {}

  ngOnInit(): void {
    this.activityService.getExerciseTypes().subscribe({
      next: items => this.exerciseTypes.set(items),
      error: () => this.exerciseTypes.set([]),
    });
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
