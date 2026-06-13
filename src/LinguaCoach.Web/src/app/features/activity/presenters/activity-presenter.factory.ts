import { ActivityDto } from '../../../core/models/activity.models';
import { ActivityPagePresenter } from './activity-page-presenter';
import { PatternBackedPresenter } from './pattern-backed.presenter';
import { LegacySpeakingPresenter } from './legacy-speaking.presenter';
import { LegacyVocabPresenter } from './legacy-vocab.presenter';
import { LegacyListeningPresenter } from './legacy-listening.presenter';
import { LegacyWritingPresenter } from './legacy-writing.presenter';

const patternBacked = new PatternBackedPresenter();
const legacySpeaking = new LegacySpeakingPresenter();
const legacyVocab = new LegacyVocabPresenter();
const legacyListening = new LegacyListeningPresenter();
const legacyWriting = new LegacyWritingPresenter();

/**
 * Picks the presenter for an activity. Pattern-engine activities
 * (`interactionMode` set, or a writingScenario with `contentJson`) get the
 * generic `PatternBackedPresenter` — new pattern keys need no factory changes.
 * Everything else falls back to the matching `Legacy*Presenter` bridge.
 */
export class ActivityPresenterFactory {
  static for(activity: ActivityDto): ActivityPagePresenter {
    if (activity.activityType !== 'speakingRolePlay' && (
      activity.interactionMode != null
      || (activity.activityType === 'writingScenario' && !!activity.contentJson)
    )) {
      return patternBacked;
    }

    switch (activity.activityType) {
      case 'speakingRolePlay': return legacySpeaking;
      case 'vocabularyPractice': return legacyVocab;
      case 'listeningComprehension': return legacyListening;
      default: return legacyWriting;
    }
  }
}
