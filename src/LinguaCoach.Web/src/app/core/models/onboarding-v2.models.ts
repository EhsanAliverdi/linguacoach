// Form.io migration (onboarding half) — the student-facing surface is now just "the active
// published Form.io wizard plus a prior draft submission to prefill it with." All the old
// per-step-type progress-tracking models are gone: Form.io itself owns wizard-page navigation.

import { FormRendererKind } from '../../shared/formio/form-renderer-kind.model';

export interface StudentOnboardingActiveDto {
  templateVersionId: string;
  formIoSchemaJson: string;
  rendererKind: FormRendererKind;
  submissionJson: string | null;
  isComplete: boolean;
}

export interface SubmitOnboardingResult {
  success: boolean;
  preliminaryCefrLevel: string | null;
}
