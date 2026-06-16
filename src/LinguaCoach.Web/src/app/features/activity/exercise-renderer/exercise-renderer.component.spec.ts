import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ExerciseRendererComponent } from './exercise-renderer.component';
import { ActivityDto } from '../../../core/models/activity.models';

function makeActivity(interactionMode: string, contentJson: object): ActivityDto {
  return {
    activityId: 'test-id',
    title: 'Test',
    interactionMode: interactionMode as any,
    contentJson: JSON.stringify(contentJson),
    activityType: 'SpeakingRolePlay',
    exercisePatternKey: null,
    audioUrl: null,
    audioDurationSeconds: null,
    audioUnavailableMessage: null,
    situation: null,
    speakingPrompt: null,
    speakingScenario: null,
    speakingGoal: null,
    learningGoal: null,
    instructions: null,
    targetPhrases: null,
    suggestedPhrases: null,
    exampleText: null,
    scenario: null,
    listeningQuestions: null,
    responseTask: null,
  } as any;
}

const ASQ_CONTENT = {
  schemaVersion: 'module_stage_v1',
  learnContent: {},
  practiceContent: {
    exerciseData: {
      items: [
        { id: 'q1', question: 'What is your name?', audioScript: null, audioUrl: null },
      ],
    },
  },
  feedbackPlan: {},
};

const READ_ALOUD_CONTENT = {
  schemaVersion: 'module_stage_v1',
  learnContent: {},
  practiceContent: {
    exerciseData: {
      items: [{ id: 't1', text: 'The quick brown fox.' }],
    },
  },
  feedbackPlan: {},
};

const REPEAT_SENTENCE_CONTENT = {
  schemaVersion: 'module_stage_v1',
  learnContent: {},
  practiceContent: {
    exerciseData: {
      items: [{ id: 's1', sentence: 'She sells seashells.', audioScript: null }],
    },
  },
  feedbackPlan: {},
};

const RESPOND_TO_SITUATION_CONTENT = {
  schemaVersion: 'module_stage_v1',
  learnContent: {},
  practiceContent: {
    exerciseData: {
      items: [{ id: 'sit1', situation: 'Your train is late. Call your boss.', prompt: null }],
    },
  },
  feedbackPlan: {},
};

const DESCRIBE_IMAGE_CONTENT = {
  schemaVersion: 'module_stage_v1',
  learnContent: {},
  practiceContent: {
    exerciseData: {
      items: [{ id: 'img1', imagePrompt: 'A busy city street at night.' }],
    },
  },
  feedbackPlan: {},
};

const RETELL_LECTURE_CONTENT = {
  schemaVersion: 'module_stage_v1',
  learnContent: {},
  practiceContent: {
    exerciseData: {
      items: [{ id: 'lec1', lectureTitle: 'Memory and Sleep', audioScript: 'Sleep helps memory consolidation.' }],
    },
  },
  feedbackPlan: {},
};

const SUMMARIZE_GROUP_DISCUSSION_CONTENT = {
  schemaVersion: 'module_stage_v1',
  learnContent: {},
  practiceContent: {
    exerciseData: {
      items: [{ id: 'disc1', discussionTitle: 'Project Planning', audioScript: 'Alice: Research first. Bob: Then prototype.' }],
    },
  },
  feedbackPlan: {},
};

describe('ExerciseRendererComponent — Phase 9 speaking/listening dispatch', () => {
  let fixture: ComponentFixture<ExerciseRendererComponent>;
  let component: ExerciseRendererComponent;

  async function setup(interactionMode: string, content: object): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [ExerciseRendererComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(ExerciseRendererComponent);
    component = fixture.componentInstance;
    component.activity = makeActivity(interactionMode, content);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('dispatches answerShortQuestion renderer for answerShortQuestion mode', async () => {
    await setup('answerShortQuestion', ASQ_CONTENT);
    expect(component.mode).toBe('answerShortQuestion');
    const el = fixture.nativeElement.querySelector('app-answer-short-question');
    expect(el).toBeTruthy();
  });

  it('answerShortQuestionContent extracts items from staged exerciseData', async () => {
    await setup('answerShortQuestion', ASQ_CONTENT);
    const content = component.answerShortQuestionContent;
    expect(content.items.length).toBe(1);
    expect(content.items[0].id).toBe('q1');
    expect(content.items[0].question).toBe('What is your name?');
  });

  it('onAnswerShortQuestionSubmitted emits answerShortQuestion payload', async () => {
    await setup('answerShortQuestion', ASQ_CONTENT);
    const emitted: any[] = [];
    component.answerSubmitted.subscribe(v => emitted.push(v));
    component.onAnswerShortQuestionSubmitted({ items: [{ itemId: 'q1', answerText: 'John' }] });
    expect(emitted.length).toBe(1);
    expect(emitted[0].kind).toBe('answerShortQuestion');
    expect(emitted[0].items[0].itemId).toBe('q1');
  });

  it('dispatches readAloud renderer for readAloud mode', async () => {
    await setup('readAloud', READ_ALOUD_CONTENT);
    expect(component.mode).toBe('readAloud');
    const el = fixture.nativeElement.querySelector('app-read-aloud');
    expect(el).toBeTruthy();
  });

  it('readAloudContent extracts items from staged exerciseData', async () => {
    await setup('readAloud', READ_ALOUD_CONTENT);
    const content = component.readAloudContent;
    expect(content.items.length).toBe(1);
    expect(content.items[0].id).toBe('t1');
    expect(content.items[0].text).toBe('The quick brown fox.');
  });

  it('onReadAloudSubmitted emits readAloud payload', async () => {
    await setup('readAloud', READ_ALOUD_CONTENT);
    const emitted: any[] = [];
    component.answerSubmitted.subscribe(v => emitted.push(v));
    component.onReadAloudSubmitted({ items: [{ itemId: 't1', answerText: 'The quick brown fox.' }] });
    expect(emitted[0].kind).toBe('readAloud');
    expect(emitted[0].items[0].itemId).toBe('t1');
  });

  it('dispatches repeatSentence renderer for repeatSentence mode', async () => {
    await setup('repeatSentence', REPEAT_SENTENCE_CONTENT);
    expect(component.mode).toBe('repeatSentence');
    const el = fixture.nativeElement.querySelector('app-repeat-sentence');
    expect(el).toBeTruthy();
  });

  it('repeatSentenceContent extracts items from staged exerciseData', async () => {
    await setup('repeatSentence', REPEAT_SENTENCE_CONTENT);
    const content = component.repeatSentenceContent;
    expect(content.items.length).toBe(1);
    expect(content.items[0].id).toBe('s1');
    expect(content.items[0].sentence).toBe('She sells seashells.');
  });

  it('onRepeatSentenceSubmitted emits repeatSentence payload', async () => {
    await setup('repeatSentence', REPEAT_SENTENCE_CONTENT);
    const emitted: any[] = [];
    component.answerSubmitted.subscribe(v => emitted.push(v));
    component.onRepeatSentenceSubmitted({ items: [{ itemId: 's1', answerText: 'She sells seashells.' }] });
    expect(emitted[0].kind).toBe('repeatSentence');
  });

  it('dispatches respondToSituation renderer for respondToSituation mode', async () => {
    await setup('respondToSituation', RESPOND_TO_SITUATION_CONTENT);
    expect(component.mode).toBe('respondToSituation');
    const el = fixture.nativeElement.querySelector('app-respond-to-situation');
    expect(el).toBeTruthy();
  });

  it('respondToSituationContent extracts items from staged exerciseData', async () => {
    await setup('respondToSituation', RESPOND_TO_SITUATION_CONTENT);
    const content = component.respondToSituationContent;
    expect(content.items.length).toBe(1);
    expect(content.items[0].id).toBe('sit1');
    expect(content.items[0].situation).toContain('train');
  });

  it('onRespondToSituationSubmitted emits respondToSituation payload', async () => {
    await setup('respondToSituation', RESPOND_TO_SITUATION_CONTENT);
    const emitted: any[] = [];
    component.answerSubmitted.subscribe(v => emitted.push(v));
    component.onRespondToSituationSubmitted({ items: [{ itemId: 'sit1', answerText: 'Sorry, my train is delayed.' }] });
    expect(emitted[0].kind).toBe('respondToSituation');
    expect(emitted[0].items[0].itemId).toBe('sit1');
  });

  it('dispatches describeImage renderer for describeImage mode', async () => {
    await setup('describeImage', DESCRIBE_IMAGE_CONTENT);
    expect(component.mode).toBe('describeImage');
    const el = fixture.nativeElement.querySelector('app-describe-image');
    expect(el).toBeTruthy();
  });

  it('describeImageContent extracts imagePrompt from staged exerciseData', async () => {
    await setup('describeImage', DESCRIBE_IMAGE_CONTENT);
    const content = component.describeImageContent;
    expect(content.items.length).toBe(1);
    expect(content.items[0].imagePrompt).toBe('A busy city street at night.');
  });

  it('onDescribeImageSubmitted emits describeImage payload', async () => {
    await setup('describeImage', DESCRIBE_IMAGE_CONTENT);
    const emitted: any[] = [];
    component.answerSubmitted.subscribe(v => emitted.push(v));
    component.onDescribeImageSubmitted({ items: [{ itemId: 'img1', answerText: 'I see cars and lights.' }] });
    expect(emitted[0].kind).toBe('describeImage');
  });

  it('dispatches retellLecture renderer for retellLecture mode', async () => {
    await setup('retellLecture', RETELL_LECTURE_CONTENT);
    expect(component.mode).toBe('retellLecture');
    const el = fixture.nativeElement.querySelector('app-retell-lecture');
    expect(el).toBeTruthy();
  });

  it('retellLectureContent extracts lectureTitle and audioScript from staged exerciseData', async () => {
    await setup('retellLecture', RETELL_LECTURE_CONTENT);
    const content = component.retellLectureContent;
    expect(content.items.length).toBe(1);
    expect(content.items[0].lectureTitle).toBe('Memory and Sleep');
    expect(content.items[0].audioScript).toContain('memory');
  });

  it('retellLectureContent audioScript used as fallback when audioUrl is null', async () => {
    await setup('retellLecture', RETELL_LECTURE_CONTENT);
    const content = component.retellLectureContent;
    expect(content.items[0].audioUrl == null).toBeTrue();
    expect(content.items[0].audioScript).toBeTruthy();
  });

  it('onRetellLectureSubmitted emits retellLecture payload', async () => {
    await setup('retellLecture', RETELL_LECTURE_CONTENT);
    const emitted: any[] = [];
    component.answerSubmitted.subscribe(v => emitted.push(v));
    component.onRetellLectureSubmitted({ items: [{ itemId: 'lec1', answerText: 'The lecture was about sleep and memory.' }] });
    expect(emitted[0].kind).toBe('retellLecture');
    expect(emitted[0].items[0].itemId).toBe('lec1');
  });

  it('dispatches summarizeGroupDiscussion renderer for summarizeGroupDiscussion mode', async () => {
    await setup('summarizeGroupDiscussion', SUMMARIZE_GROUP_DISCUSSION_CONTENT);
    expect(component.mode).toBe('summarizeGroupDiscussion');
    const el = fixture.nativeElement.querySelector('app-summarize-group-discussion');
    expect(el).toBeTruthy();
  });

  it('summarizeGroupDiscussionContent extracts items from staged exerciseData', async () => {
    await setup('summarizeGroupDiscussion', SUMMARIZE_GROUP_DISCUSSION_CONTENT);
    const content = component.summarizeGroupDiscussionContent;
    expect(content.items.length).toBe(1);
    expect(content.items[0].discussionTitle).toBe('Project Planning');
    expect(content.items[0].audioScript).toContain('Alice');
  });

  it('summarizeGroupDiscussionContent audioScript used as fallback when audioUrl is null', async () => {
    await setup('summarizeGroupDiscussion', SUMMARIZE_GROUP_DISCUSSION_CONTENT);
    const content = component.summarizeGroupDiscussionContent;
    expect(content.items[0].audioUrl == null).toBeTrue();
    expect(content.items[0].audioScript).toBeTruthy();
  });

  it('onSummarizeGroupDiscussionSubmitted emits summarizeGroupDiscussion payload', async () => {
    await setup('summarizeGroupDiscussion', SUMMARIZE_GROUP_DISCUSSION_CONTENT);
    const emitted: any[] = [];
    component.answerSubmitted.subscribe(v => emitted.push(v));
    component.onSummarizeGroupDiscussionSubmitted({
      items: [{ itemId: 'disc1', answerText: 'Alice wanted research first, Bob agreed then suggested prototyping.' }],
    });
    expect(emitted[0].kind).toBe('summarizeGroupDiscussion');
    expect(emitted[0].items[0].itemId).toBe('disc1');
  });
});
