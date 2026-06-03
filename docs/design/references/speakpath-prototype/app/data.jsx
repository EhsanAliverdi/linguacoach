// SpeakPath mock data — learning path, modules, activities, feedback.
(function () {
  // Skill definitions: each maps to a colour token + icon name.
  const skills = {
    writing:       { key: "writing",       label: "Writing",       icon: "pen",   color: "indigo" },
    speaking:      { key: "speaking",      label: "Speaking",      icon: "mic",   color: "coral" },
    listening:     { key: "listening",     label: "Listening",     icon: "ear",   color: "violet" },
    vocabulary:    { key: "vocabulary",    label: "Vocabulary",    icon: "book",  color: "amber" },
    pronunciation: { key: "pronunciation", label: "Pronunciation", icon: "sound", color: "teal" },
  };

  const user = {
    name: "Maryam",
    goal: "Communicate confidently at work",
    level: "B1 · Intermediate",
    streak: 6,
    streakDays: [true, true, true, true, true, true, false], // Mon→Sun, today = Sat
    minutesThisWeek: 84,
    activitiesDone: 27,
  };

  const path = {
    title: "Confident Workplace Communication",
    subtitle: "Personalised for a project coordinator role",
    description:
      "A journey built around the everyday English you use at work — writing clear emails, speaking up in meetings, and understanding colleagues with ease.",
    progress: 34, // %
    aiGenerated: true,
  };

  // Modules in the path
  const modules = [
    {
      id: "m1", n: 1, title: "Professional Email Foundations", skill: "writing",
      goal: "Write clear, polite emails that get a reply", activities: 5, done: 5,
      state: "completed",
    },
    {
      id: "m2", n: 2, title: "Requests & Follow-ups", skill: "writing",
      goal: "Make requests and chase replies without sounding pushy", activities: 5, done: 2,
      state: "current",
    },
    {
      id: "m3", n: 3, title: "Speaking Up in Meetings", skill: "speaking",
      goal: "Share an opinion and ask questions out loud", activities: 6, done: 0,
      state: "locked", soon: true,
    },
    {
      id: "m4", n: 4, title: "Understanding Colleagues", skill: "listening",
      goal: "Follow fast, casual workplace conversation", activities: 5, done: 0,
      state: "locked", soon: true,
    },
    {
      id: "m5", n: 5, title: "Workplace Vocabulary", skill: "vocabulary",
      goal: "Master the words your team uses every day", activities: 8, done: 0,
      state: "locked",
    },
    {
      id: "m6", n: 6, title: "Clear, Confident Pronunciation", skill: "pronunciation",
      goal: "Be understood the first time, every time", activities: 6, done: 0,
      state: "locked",
    },
  ];

  // Activities inside the CURRENT module (m2)
  const activities = [
    {
      id: "a1", title: "Ask for a deadline extension", skill: "writing",
      type: "WritingScenario", state: "current", recommended: true,
      minutes: 8, blurb: "Request two more days on a report — politely and clearly.",
    },
    {
      id: "a2", title: "Follow up on an unanswered email", skill: "writing",
      type: "WritingScenario", state: "todo", minutes: 7,
      blurb: "Send a friendly nudge without sounding annoyed.",
    },
    {
      id: "a3", title: "Politely decline an extra task", skill: "writing",
      type: "WritingScenario", state: "todo", minutes: 9,
      blurb: "Say no while protecting the relationship.",
    },
    {
      id: "a0", title: "Introduce yourself to a new team", skill: "writing",
      type: "WritingScenario", state: "done", minutes: 6, score: 88,
      blurb: "A warm, professional first message.",
    },
  ];

  // The implemented WritingScenario the activity flow renders
  const scenario = {
    id: "a1",
    title: "Ask for a deadline extension",
    module: "Requests & Follow-ups",
    skill: "writing",
    minutes: 8,
    situation:
      "You're finishing the Q3 project report, but two key numbers are still missing from the finance team. You need two more days. Email your manager, Daniel, to ask for an extension.",
    goal: "Make a polite, specific request and give a short, honest reason.",
    targetPhrases: [
      "I wanted to ask if",
      "Would it be possible to",
      "I apologise for the short notice",
      "I'd really appreciate it if",
      "to make sure the report is accurate",
    ],
    vocabulary: [
      { word: "extension", meaning: "extra time to finish something" },
      { word: "deadline", meaning: "the date something is due" },
      { word: "accurate", meaning: "correct, with no mistakes" },
      { word: "deliverable", meaning: "a finished piece of work you hand over" },
      { word: "follow up", meaning: "to check on or continue something" },
    ],
    example:
      "Hi Daniel,\n\nI wanted to ask if it would be possible to have until Thursday for the Q3 report. The finance team hasn't sent two figures yet, and I'd like to make sure the report is accurate before I share it.\n\nI apologise for the short notice, and I'd really appreciate your flexibility.\n\nThank you,\nMaryam",
    mistake: {
      title: "Don't just write \u201cI need more time.\u201d",
      body: "A bare request can sound demanding. Give a reason and propose a new date — it shows you're organised and respectful of your manager's time.",
    },
    task: "Write a short email to your manager Daniel asking for two more days to finish the Q3 report. Explain why, and suggest Thursday as the new date.",
  };

  // A pre-baked AI feedback result (also lightly adapts to user input length)
  const feedback = {
    score: 86,
    band: "Great work",
    summary:
      "This is a polite, well-structured request, Maryam. Your reason is clear and your tone is respectful — exactly right for a manager. A couple of small grammar fixes will make it read even more naturally.",
    userDraft:
      "Hi Daniel,\n\nI want to ask if is possible to get two more days for the Q3 report. Finance team don't send me two numbers yet and I want the report to be accurate. Sorry for short notice. Thank you for your understand.\n\nMaryam",
    corrected:
      "Hi Daniel,\n\nI wanted to ask if it would be possible to get two more days for the Q3 report. The finance team hasn't sent me two figures yet, and I'd like the report to be accurate. I apologise for the short notice, and thank you for your understanding.\n\nThank you,\nMaryam",
    diffs: [
      { from: "I want to ask if is possible", to: "I wanted to ask if it would be possible", note: "softer, more polite request" },
      { from: "Finance team don't send", to: "The finance team hasn't sent", note: "subject–verb agreement + article" },
      { from: "your understand", to: "your understanding", note: "noun form after \u201cyour\u201d" },
    ],
    wins: [
      "You gave a clear, honest reason for the request.",
      "Your tone is warm and professional — perfect for a manager.",
      "You remembered to apologise for the short notice.",
    ],
    improve: [
      { label: "Use \u201cwould\u201d for polite requests", detail: "\u201cWould it be possible\u2026\u201d sounds gentler than \u201cis it possible\u2026\u201d." },
      { label: "Watch subject\u2013verb agreement", detail: "\u201cThe finance team hasn't sent\u201d — team is singular here." },
    ],
    grammar:
      "Present perfect (\u201chasn't sent\u201d) is used for something that hasn't happened up to now but is still relevant — perfect for explaining a delay.",
    tone:
      "Your message is at just the right politeness level: friendly but professional. Phrases like \u201cI apologise\u201d and \u201cI'd appreciate\u201d show respect without sounding too formal or anxious.",
    vocabToRemember: ["extension", "accurate", "short notice", "flexibility"],
    suggestedPhrases: [
      "Would it be possible to\u2026",
      "I'd really appreciate it if\u2026",
      "to make sure it's accurate",
    ],
    rewrite:
      "Now try the same email, but imagine the deadline is today and you need until Monday. Keep it polite and give a reason.",
    next: {
      title: "Follow up on an unanswered email",
      skill: "writing",
      reason: "Builds on the polite-request phrases you just practised.",
    },
  };

  window.SP = window.SP || {};
  window.SP.data = { skills, user, path, modules, activities, scenario, feedback };
})();
