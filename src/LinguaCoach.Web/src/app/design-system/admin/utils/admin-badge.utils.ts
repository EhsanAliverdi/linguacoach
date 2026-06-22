import { SpAdminBadgeTone } from '../components/badge/sp-admin-badge.component';

const LIFECYCLE_LABELS: Record<string, string> = {
  Created: 'Created',
  PasswordChangeRequired: 'Password change required',
  OnboardingRequired: 'Onboarding required',
  OnboardingInProgress: 'Onboarding in progress',
  PlacementRequired: 'Placement required',
  PlacementInProgress: 'Placement in progress',
  PlacementCompleted: 'Placement completed',
  CourseReady: 'Course ready',
  InLesson: 'In lesson',
  ActiveLearning: 'Active learning',
  Paused: 'Paused',
  Archived: 'Archived',
};

const LIFECYCLE_TONES: Record<string, SpAdminBadgeTone> = {
  Created: 'neutral',
  PasswordChangeRequired: 'warning',
  OnboardingRequired: 'warning',
  OnboardingInProgress: 'info',
  PlacementRequired: 'warning',
  PlacementInProgress: 'info',
  PlacementCompleted: 'info',
  CourseReady: 'primary',
  InLesson: 'success',
  ActiveLearning: 'success',
  Paused: 'neutral',
  Archived: 'neutral',
};

const ONBOARDING_LABELS: Record<string, string> = {
  Complete: 'Complete',
  InProgress: 'In progress',
  NotStarted: 'Not started',
  Pending: 'Pending',
};

const ONBOARDING_TONES: Record<string, SpAdminBadgeTone> = {
  Complete: 'success',
  InProgress: 'info',
  NotStarted: 'warning',
  Pending: 'warning',
};

const EVENT_LEVEL_LABELS: Record<string, string> = {
  Information: 'Info',
  Warning: 'Warning',
  Error: 'Error',
  Debug: 'Debug',
  Critical: 'Critical',
};

export function lifecycleLabel(stage: string): string {
  return LIFECYCLE_LABELS[stage] ?? stage;
}

export function lifecycleTone(stage: string): SpAdminBadgeTone {
  return LIFECYCLE_TONES[stage] ?? 'neutral';
}

export function onboardingLabel(status: string): string {
  return ONBOARDING_LABELS[status] ?? status;
}

export function onboardingTone(status: string): SpAdminBadgeTone {
  return ONBOARDING_TONES[status] ?? 'neutral';
}

export function eventLevelLabel(level: string): string {
  return EVENT_LEVEL_LABELS[level] ?? level;
}
