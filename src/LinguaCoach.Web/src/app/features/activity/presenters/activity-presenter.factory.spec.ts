import { ActivityPresenterFactory } from './activity-presenter.factory';
import { LegacyListeningPresenter } from './legacy-listening.presenter';
import { LegacySpeakingPresenter } from './legacy-speaking.presenter';
import { LegacyVocabPresenter } from './legacy-vocab.presenter';
import { LegacyWritingPresenter } from './legacy-writing.presenter';
import { PatternBackedPresenter } from './pattern-backed.presenter';
import { makeActivity } from './test-helpers';

describe('ActivityPresenterFactory', () => {
  it('returns pattern-backed presenter when interactionMode is set', () => {
    const activity = makeActivity({ activityType: 'writingScenario', interactionMode: 'gapFill' });
    expect(ActivityPresenterFactory.for(activity)).toBeInstanceOf(PatternBackedPresenter);
  });

  it('returns pattern-backed presenter for writingScenario with contentJson', () => {
    const activity = makeActivity({ activityType: 'writingScenario', contentJson: '{}' });
    expect(ActivityPresenterFactory.for(activity)).toBeInstanceOf(PatternBackedPresenter);
  });

  it('never returns pattern-backed presenter for speakingRolePlay', () => {
    const activity = makeActivity({ activityType: 'speakingRolePlay', interactionMode: 'chatReply' });
    expect(ActivityPresenterFactory.for(activity)).toBeInstanceOf(LegacySpeakingPresenter);
  });

  it('returns legacy vocab presenter for vocabularyPractice without interactionMode', () => {
    const activity = makeActivity({ activityType: 'vocabularyPractice' });
    expect(ActivityPresenterFactory.for(activity)).toBeInstanceOf(LegacyVocabPresenter);
  });

  it('returns legacy listening presenter for listeningComprehension without interactionMode', () => {
    const activity = makeActivity({ activityType: 'listeningComprehension' });
    expect(ActivityPresenterFactory.for(activity)).toBeInstanceOf(LegacyListeningPresenter);
  });

  it('returns legacy writing presenter for writingScenario without interactionMode or contentJson', () => {
    const activity = makeActivity({ activityType: 'writingScenario' });
    expect(ActivityPresenterFactory.for(activity)).toBeInstanceOf(LegacyWritingPresenter);
  });
});
