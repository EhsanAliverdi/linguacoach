// SpeakPath Admin — mock data
(function () {
  const students = [
    { id: 's1', name: 'QA FullAudit2', email: 'qa.fullaudit2.202606131530@example.com', lifecycle: 'Active learning', onboarding: 'Complete', cefr: 'B2', profile: 'Junior software engineer', joined: '2026-06-13', streak: 4, minutesWeek: 62, activitiesDone: 14, archived: false },
    { id: 's2', name: 'QA FullAudit', email: 'qa.fullaudit.202606131530@example.com', lifecycle: 'Active learning', onboarding: 'Complete', cefr: 'B1', profile: 'Senior QA Engineer at a fintech startup', joined: '2026-06-13', streak: 6, minutesWeek: 84, activitiesDone: 27, archived: false },
    { id: 's3', name: 'Test5@dev.com', email: 'Test5@dev.com', lifecycle: 'Placement required', onboarding: 'Complete', cefr: null, profile: 'Junior software engineer', joined: '2026-06-09', streak: 0, minutesWeek: 0, activitiesDone: 0, archived: false },
    { id: 's4', name: 'Test4@dev.com', email: 'Test4@dev.com', lifecycle: 'Placement required', onboarding: 'Complete', cefr: null, profile: null, joined: '2026-06-09', streak: 0, minutesWeek: 0, activitiesDone: 0, archived: false },
    { id: 's5', name: 'Test3@dev.com', email: 'Test3@dev.com', lifecycle: 'Placement required', onboarding: 'Complete', cefr: null, profile: null, joined: '2026-06-09', streak: 0, minutesWeek: 0, activitiesDone: 0, archived: false },
    { id: 's6', name: 'Test 2', email: 'Test2@dev.com', lifecycle: 'Course ready', onboarding: 'Complete', cefr: 'B2', profile: null, joined: '2026-06-09', streak: 1, minutesWeek: 12, activitiesDone: 3, archived: false },
    { id: 's7', name: 'Test1@dev.com', email: 'Test1@dev.com', lifecycle: 'In lesson', onboarding: 'Complete', cefr: 'A2', profile: null, joined: '2026-06-09', streak: 2, minutesWeek: 30, activitiesDone: 7, archived: false },
    { id: 's8', name: 'Ehsan', email: 'aliverdi.ehsan@hutch.dev', lifecycle: 'Course ready', onboarding: 'Complete', cefr: 'B2', profile: null, joined: '2026-06-03', streak: 0, minutesWeek: 0, activitiesDone: 2, archived: false },
  ];

  const studentActivities = {
    s1: [
      { id: 'a1', title: 'Writing a project status email', skill: 'writing', type: 'WritingScenario', score: 91, minutes: 8, date: '2026-06-13' },
      { id: 'a2', title: 'Asking for a deadline extension', skill: 'writing', type: 'WritingScenario', score: 86, minutes: 7, date: '2026-06-12' },
      { id: 'a3', title: 'Speaking in a team standup', skill: 'speaking', type: 'SpeakingPrompt', score: 78, minutes: 10, date: '2026-06-11' },
      { id: 'a4', title: 'Professional email introduction', skill: 'writing', type: 'WritingScenario', score: 94, minutes: 6, date: '2026-06-10' },
    ],
    s2: [
      { id: 'b1', title: 'Ask for a deadline extension', skill: 'writing', type: 'WritingScenario', score: 86, minutes: 8, date: '2026-06-13' },
      { id: 'b2', title: 'Follow up on an unanswered email', skill: 'writing', type: 'WritingScenario', score: 79, minutes: 7, date: '2026-06-12' },
      { id: 'b3', title: 'Introduce yourself to a new team', skill: 'writing', type: 'WritingScenario', score: 88, minutes: 6, date: '2026-06-10' },
    ],
  };

  const prompts = [
    { id: 'p1', name: 'WritingScenario — Request', type: 'Writing', status: 'Active', updated: '2026-06-10', preview: 'You are an English language tutor. The student needs to write a professional email making a request...' },
    { id: 'p2', name: 'WritingScenario — Follow-up', type: 'Writing', status: 'Active', updated: '2026-06-10', preview: 'You are an English language tutor. The student needs to write a polite follow-up email...' },
    { id: 'p3', name: 'WritingScenario — Decline', type: 'Writing', status: 'Active', updated: '2026-06-10', preview: 'You are an English language tutor. Help the student politely decline a request...' },
    { id: 'p4', name: 'Feedback — Writing', type: 'Feedback', status: 'Active', updated: '2026-06-08', preview: 'Analyse the student\'s writing for grammar, tone, vocabulary and task completion...' },
    { id: 'p5', name: 'Feedback — Speaking', type: 'Feedback', status: 'Draft', updated: '2026-06-07', preview: 'Analyse the student\'s spoken response for fluency, pronunciation and accuracy...' },
    { id: 'p6', name: 'SpeakingPrompt — Standup', type: 'Speaking', status: 'Active', updated: '2026-06-05', preview: 'You are an English language coach. The student will participate in a daily team standup...' },
    { id: 'p7', name: 'Placement — CEFR Assessment', type: 'Assessment', status: 'Active', updated: '2026-06-01', preview: 'Conduct a structured CEFR level assessment across writing and comprehension...' },
  ];

  const exerciseTypes = [
    { id: 'e1', name: 'WritingScenario', description: 'Real-world writing task with AI feedback and corrections', count: 12, enabled: true },
    { id: 'e2', name: 'SpeakingPrompt', description: 'Voice response to a workplace scenario with pronunciation feedback', count: 8, enabled: true },
    { id: 'e3', name: 'ListeningQuiz', description: 'Audio clip with comprehension questions', count: 5, enabled: true },
    { id: 'e4', name: 'GrammarDrill', description: 'Targeted grammar practice with adaptive difficulty', count: 0, enabled: false, comingSoon: true },
    { id: 'e5', name: 'VocabFlashcard', description: 'Spaced repetition vocabulary builder', count: 0, enabled: false, comingSoon: true },
  ];

  // AI cost data: 30 days
  const aiCost30d = [0.28,0.35,0.31,0.42,0.38,0.29,0.22,0.45,0.52,0.48,0.39,0.33,0.41,0.55,0.62,0.58,0.44,0.37,0.29,0.41,0.48,0.53,0.46,0.38,0.42,0.51,0.60,0.55,0.48,0.47];
  const aiCostLabels = ['May 23','','','','','May 28','','','','','Jun 2','','','','','Jun 7','','','','','Jun 12','','','','','Jun 17','','','','Jun 21'];

  // Activities per day: 14 days
  const actPerDay14 = [7,9,12,8,11,5,3,10,13,9,7,11,12,12];
  const actDayLabels = ['Jun 8','','','','Jun 12','','','Jun 15','','','Jun 18','','','Jun 21'];

  // Heatmap: 7 rows (Mon-Sun) × 12 cols (weeks)
  const heatmap7x12 = [
    [3,5,4,6,5,3,2,4,6,5,4,2],
    [4,3,5,7,4,4,1,5,7,6,5,3],
    [2,4,6,5,6,3,3,4,5,7,6,4],
    [3,5,4,6,5,2,2,5,6,5,4,3],
    [4,4,5,5,4,3,1,4,6,5,3,2],
    [1,2,2,3,2,1,0,2,3,2,2,1],
    [0,1,1,2,1,1,0,1,2,1,1,0],
  ];

  const notifications = [
    { id: 'n1', event: 'Student signs up', email: true, webhook: false, description: 'Triggered when a new student account is created' },
    { id: 'n2', event: 'Onboarding complete', email: true, webhook: false, description: 'Triggered when a student finishes onboarding + CEFR placement' },
    { id: 'n3', event: '7-day inactivity', email: true, webhook: false, description: 'Triggered when a student has not logged in for 7 days' },
    { id: 'n4', event: 'Low engagement alert', email: false, webhook: false, description: 'Triggered when streak drops to zero and no activity in 3 days' },
    { id: 'n5', event: 'AI provider error', email: true, webhook: true, description: 'Triggered when the AI API returns an error or rate limit' },
    { id: 'n6', event: 'Weekly digest', email: true, webhook: false, description: 'Sent every Monday with the past week\'s activity summary' },
  ];

  const recentNotifsSent = [
    { id: 'rn1', event: 'Onboarding complete', recipient: 'qa.fullaudit2.202606...', sent: '2026-06-13 15:31', status: 'Delivered' },
    { id: 'rn2', event: 'Onboarding complete', recipient: 'qa.fullaudit.202606...', sent: '2026-06-13 15:30', status: 'Delivered' },
    { id: 'rn3', event: 'Student signs up', recipient: 'qa.fullaudit2.202606...', sent: '2026-06-13 15:30', status: 'Delivered' },
    { id: 'rn4', event: 'Weekly digest', recipient: 'admin@speakpath.app', sent: '2026-06-16 08:00', status: 'Delivered' },
    { id: 'rn5', event: 'AI provider error', recipient: 'admin@speakpath.app', sent: '2026-06-09 14:22', status: 'Delivered' },
  ];

  const diagLogs = [
    { id: 'l1', time: '15:48:02', level: 'INFO', msg: 'WritingScenario feedback generated in 1842ms', cls: 'adm-log-info' },
    { id: 'l2', time: '15:44:17', level: 'INFO', msg: 'Student s1 completed activity a1 (score: 91)', cls: 'adm-log-info' },
    { id: 'l3', time: '15:31:05', level: 'INFO', msg: 'Onboarding completed for qa.fullaudit2.202606131530@example.com', cls: 'adm-log-info' },
    { id: 'l4', time: '14:22:33', level: 'WARN', msg: 'OpenAI rate limit approached (89% of RPM quota)', cls: 'adm-log-warn' },
    { id: 'l5', time: '14:18:09', level: 'ERROR', msg: 'OpenAI API timeout after 30s — retried successfully', cls: 'adm-log-error' },
    { id: 'l6', time: '13:55:44', level: 'INFO', msg: 'CEFR placement assessment completed for student s2', cls: 'adm-log-info' },
    { id: 'l7', time: '13:20:01', level: 'INFO', msg: 'Health check passed — all services operational', cls: 'adm-log-info' },
    { id: 'l8', time: '12:00:00', level: 'INFO', msg: 'Weekly digest sent to admin@speakpath.app', cls: 'adm-log-info' },
  ];

  window.ADMIN_DATA = {
    students, studentActivities, prompts, exerciseTypes,
    aiCost30d, aiCostLabels, actPerDay14, actDayLabels, heatmap7x12,
    notifications, recentNotifsSent, diagLogs,
    stats: {
      totalStudents: 8, activeThisWeek: 5, onboardedPct: 100,
      aiCallsToday: 47, activitiesToday: 12, avgCefr: 'B1',
      streakRate: 62, errorRate: 0, totalCost30d: 12.40,
      totalCalls30d: 847, avgCostPerStudent: 1.55,
    },
  };
})();
