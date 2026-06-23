// SpeakPath Admin — shell: sidebar, header, routing
(function () {
  const { useState } = React;

  // ── ICON SYSTEM ──────────────────────────────────────────────
  const ICONS = {
    dashboard:     (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/></svg>,
    students:      (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>,
    createstu:     (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><line x1="19" y1="8" x2="19" y2="14"/><line x1="22" y1="11" x2="16" y2="11"/></svg>,
    aiconfig:      (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><rect x="4" y="4" width="16" height="16" rx="2"/><rect x="9" y="9" width="6" height="6"/><path d="M15 2v2M9 2v2M15 20v2M9 20v2M2 15h2M2 9h2M20 15h2M20 9h2"/></svg>,
    prompts:       (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>,
    curriculum:    (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/></svg>,
    aiusage:       (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>,
    exercises:     (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polygon points="12 2 2 7 12 12 22 7 12 2"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/></svg>,
    notifications: (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.73 21a2 2 0 0 1-3.46 0"/></svg>,
    integrations:  (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M12 22v-5"/><path d="M9 8V2"/><path d="M15 8V2"/><rect x="5" y="8" width="14" height="6" rx="1"/><path d="M18 14v3"/><path d="M6 14v3"/></svg>,
    diagnostics:   (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>,
    menu:          (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><line x1="3" y1="12" x2="21" y2="12"/><line x1="3" y1="6" x2="21" y2="6"/><line x1="3" y1="18" x2="21" y2="18"/></svg>,
    plus:          (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>,
    edit:          (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>,
    trash:         (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>,
    arrowLeft:     (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/></svg>,
    arrowRight:    (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><line x1="5" y1="12" x2="19" y2="12"/><polyline points="12 5 19 12 12 19"/></svg>,
    check:         (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12"/></svg>,
    x:             (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>,
    moreH:         (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/><circle cx="5" cy="12" r="1"/></svg>,
    key:           (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><circle cx="7.5" cy="15.5" r="5.5"/><path d="M21 2l-9.6 9.6"/><path d="M15.5 7.5l3 3L22 7l-3-3"/></svg>,
    copy:          (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>,
    refresh:       (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg>,
    eye:           (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>,
    dollarSign:    (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>,
    zap:           (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/></svg>,
    flame:         (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 3z"/></svg>,
    percent:       (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><line x1="19" y1="5" x2="5" y2="19"/><circle cx="6.5" cy="6.5" r="2.5"/><circle cx="17.5" cy="17.5" r="2.5"/></svg>,
    alertCircle:   (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>,
    globe:         (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/></svg>,
    database:      (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/></svg>,
    settings:      (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>,
    mail:          (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg>,
    webhook:       (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="3"/><path d="M12 9V3"/><path d="M12 15v6"/><path d="M9 12H3"/><path d="M15 12h6"/></svg>,
    slack2:        (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><rect x="13" y="2" width="3" height="8" rx="1.5"/><path d="M19 8.5V10h1.5A1.5 1.5 0 0 0 19 8.5z"/><rect x="8" y="14" width="3" height="8" rx="1.5"/><path d="M5 15.5V14H3.5A1.5 1.5 0 0 0 5 15.5z"/><rect x="14" y="13" width="8" height="3" rx="1.5"/><path d="M15.5 19H14v1.5a1.5 1.5 0 0 0 1.5-1.5z"/><rect x="2" y="8" width="8" height="3" rx="1.5"/><path d="M8.5 5H10V3.5A1.5 1.5 0 0 0 8.5 5z"/></svg>,
    shieldCheck:   (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><polyline points="9 12 11 14 15 10"/></svg>,
    target:        (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="6"/><circle cx="12" cy="12" r="2"/></svg>,
    users2:        (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M14 19a6 6 0 0 0-12 0"/><circle cx="8" cy="9" r="4"/><path d="M22 19a6 6 0 0 0-6-6 4 4 0 0 0 0-8"/></svg>,
    archive:       (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polyline points="21 8 21 21 3 21 3 8"/><rect x="1" y="3" width="22" height="5"/><line x1="10" y1="12" x2="14" y2="12"/></svg>,
    code2:         (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polyline points="16 18 22 12 16 6"/><polyline points="8 6 2 12 8 18"/></svg>,
    send:          (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg>,
    activity2:     (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/><polyline points="7 10 10 7 13 10 17 6"/></svg>,
    server:        (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><rect x="2" y="2" width="20" height="8" rx="2"/><rect x="2" y="14" width="20" height="8" rx="2"/><line x1="6" y1="6" x2="6.01" y2="6"/><line x1="6" y1="18" x2="6.01" y2="18"/></svg>,
    pen:           (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>,
    mic:           (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"/><path d="M19 10v2a7 7 0 0 1-14 0v-2"/><line x1="12" y1="19" x2="12" y2="23"/><line x1="8" y1="23" x2="16" y2="23"/></svg>,
    headphones:    (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M3 18v-6a9 9 0 0 1 18 0v6"/><path d="M21 19a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3zM3 19a2 2 0 0 0 2 2h1a2 2 0 0 0 2-2v-3a2 2 0 0 0-2-2H3z"/></svg>,
    trendUp:       (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polyline points="23 6 13.5 15.5 8.5 10.5 1 18"/><polyline points="17 6 23 6 23 12"/></svg>,
    chevDown:      (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polyline points="6 9 12 15 18 9"/></svg>,
    chevUp:        (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polyline points="18 15 12 9 6 15"/></svg>,
    star:          (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>,
    logOut:        (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>,
    barChart:      (s,c,w) => <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="20" x2="12" y2="10"/><line x1="18" y1="20" x2="18" y2="4"/><line x1="6" y1="20" x2="6" y2="16"/></svg>,
  };

  function AIcon({ n, s = 18, c = 'currentColor', w = 1.75 }) {
    const fn = ICONS[n];
    return fn ? fn(s, c, w) : null;
  }

  // ── HELPERS ──────────────────────────────────────────────────
  function getAvatarColor(str) {
    const colors = ['#5B4BE8','#FF7A59','#B45CF0','#10B5A4','#F0982C','#FB6B57','#2A6FDB'];
    const hash = [...(str||'?')].reduce((a, ch) => a + ch.charCodeAt(0), 0);
    return colors[hash % colors.length];
  }
  function getInitials(name, email) {
    const s = (name && name !== email) ? name : email;
    const p = s.trim().split(/\s+/);
    return p.length >= 2 ? (p[0][0] + p[1][0]).toUpperCase() : s[0].toUpperCase();
  }
  function cefrClass(cefr) {
    if (!cefr) return 'adm-cefr-none';
    if (cefr.startsWith('A')) return 'adm-cefr-a';
    if (cefr.startsWith('B')) return 'adm-cefr-b';
    return 'adm-cefr-c';
  }
  function lifecycleBadge(lc) {
    const map = {
      'Active learning':      'adm-badge-success',
      'In lesson':            'adm-badge-teal',
      'Course ready':         'adm-badge-indigo',
      'Placement required':   'adm-badge-warn',
      'Onboarding required':  'adm-badge-coral',
      'Archived':             'adm-badge-muted',
    };
    return map[lc] || 'adm-badge-muted';
  }

  // ── NAV CONFIG ───────────────────────────────────────────────
  const NAV = [
    { section: 'OVERVIEW' },
    { id: 'dashboard',        label: 'Dashboard',          icon: 'dashboard' },
    { section: 'STUDENTS' },
    { id: 'students',         label: 'Students',           icon: 'students' },
    { section: 'AI SYSTEM' },
    { id: 'ai-config',        label: 'AI Config',          icon: 'aiconfig' },
    { id: 'prompts',          label: 'Prompts',            icon: 'prompts' },
    { id: 'curriculum',       label: 'Curriculum',         icon: 'curriculum' },
    { section: 'ANALYTICS' },
    { id: 'ai-usage',         label: 'Usage & Analytics',  icon: 'aiusage', soon: true },
    { section: 'SYSTEM' },
    { id: 'exercise-types',   label: 'Exercise Types',     icon: 'exercises' },
    { id: 'notifications',    label: 'Notifications',      icon: 'notifications' },
    { id: 'integrations',     label: 'Integrations',       icon: 'integrations' },
    { id: 'diagnostics',      label: 'Diagnostics',        icon: 'diagnostics' },
  ];

  const PAGE_TITLES = {
    dashboard: 'Dashboard', students: 'Students', 'create-student': 'Create student',
    'ai-config': 'AI Configuration', prompts: 'Prompts', curriculum: 'Curriculum',
    'ai-usage': 'Usage & Analytics', 'exercise-types': 'Exercise Types',
    notifications: 'Notifications', integrations: 'Integrations', diagnostics: 'Diagnostics',
  };

  // ── SIDEBAR ──────────────────────────────────────────────────
  function Sidebar({ page, setPage, collapsed, setCollapsed }) {
    return (
      <aside className={`adm-sidebar${collapsed ? ' collapsed' : ''}`}>
        <div className="adm-sidebar-logo">
          <div className="adm-logomark">SP</div>
          {!collapsed && (
            <div>
              <div className="adm-logotext">SpeakPath</div>
              <div className="adm-logosub">Admin Panel</div>
            </div>
          )}
        </div>

        <nav className="adm-nav">
          {NAV.map((item, i) => {
            if (item.section) {
              return <div key={i} className="adm-nav-section">{item.section}</div>;
            }
            return (
              <div
                key={item.id}
                className={`adm-nav-item${page === item.id ? ' active' : ''}`}
                onClick={() => setPage(item.id)}
                title={collapsed ? item.label : undefined}
              >
                <span className="adm-nav-icon" style={{ flexShrink:0 }}>
                  <AIcon n={item.icon} s={17} c="currentColor" w={1.75}/>
                </span>
                {!collapsed && <span style={{ flex:1 }}>{item.label}</span>}
                {!collapsed && item.soon && <span className="adm-nav-soon">SOON</span>}
              </div>
            );
          })}
        </nav>


      </aside>
    );
  }

  // ── HEADER ───────────────────────────────────────────────────
  function Header({ page, collapsed, setCollapsed }) {
    const [menuOpen, setMenuOpen] = useState(false);
    return (
      <header className="adm-header">
        <button className="adm-header-toggle" onClick={() => setCollapsed(!collapsed)}>
          <AIcon n="menu" s={18} c="currentColor"/>
        </button>
        <div style={{ flex:1 }}/>
        <div className="adm-hdr-right" style={{ gap:10, position:'relative' }}>
          <div
            onClick={() => setMenuOpen(v => !v)}
            style={{ display:'flex', alignItems:'center', gap:9, cursor:'pointer', padding:'5px 8px',
              borderRadius:9, border:'1px solid transparent',
              transition:'background .12s, border-color .12s',
              background: menuOpen ? 'var(--canvas)' : 'transparent',
              borderColor: menuOpen ? 'var(--border)' : 'transparent',
            }}
            onMouseOver={e => { if (!menuOpen) e.currentTarget.style.background='var(--canvas)'; }}
            onMouseOut={e => { if (!menuOpen) e.currentTarget.style.background='transparent'; }}
          >
            <div className="adm-hdr-avatar">E</div>
            <div style={{ lineHeight:1.25 }}>
              <div className="adm-hdr-email">Ehsan</div>
            </div>
            <AIcon n="chevDown" s={14} c="var(--muted)"/>
          </div>

          {menuOpen && (
            <>
              <div style={{ position:'fixed', inset:0, zIndex:199 }} onClick={() => setMenuOpen(false)}/>
              <div style={{
                position:'absolute', top:'calc(100% + 8px)', right:0, zIndex:200,
                background:'#fff', border:'1px solid var(--border)', borderRadius:12,
                boxShadow:'0 8px 24px rgba(33,27,54,.12)', minWidth:220, overflow:'hidden',
              }}>
                {/* User info header */}
                <div style={{ padding:'14px 16px 12px', borderBottom:'1px solid var(--border)' }}>
                  <div style={{ display:'flex', alignItems:'center', gap:10 }}>
                    <div style={{ width:36, height:36, borderRadius:10,
                      background:'linear-gradient(135deg,#FF7A59,#B45CF0)',
                      display:'grid', placeItems:'center', color:'#fff', fontSize:14, fontWeight:700, flexShrink:0 }}>E</div>
                    <div>
                      <div style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)' }}>Ehsan</div>
                      <div style={{ fontSize:12, color:'var(--muted)' }}>admin@speakpath.app</div>
                    </div>
                  </div>
                  <div style={{ marginTop:10 }}>
                    <span className="adm-hdr-badge">ADMIN</span>
                  </div>
                </div>
                {/* Menu items */}
                {[
                  { icon:'settings', label:'Account settings' },
                  { icon:'shieldCheck', label:'Security' },
                ].map(item => (
                  <button key={item.label}
                    style={{ display:'flex', alignItems:'center', gap:10, width:'100%',
                      padding:'10px 16px', background:'none', border:'none', cursor:'pointer',
                      fontSize:13.5, fontWeight:600, color:'var(--text)', fontFamily:'inherit',
                      textAlign:'left', transition:'background .08s' }}
                    onMouseOver={e => e.currentTarget.style.background='var(--canvas)'}
                    onMouseOut={e => e.currentTarget.style.background='none'}>
                    <AIcon n={item.icon} s={15} c="var(--muted)"/>
                    {item.label}
                  </button>
                ))}
                <div style={{ borderTop:'1px solid var(--border)' }}>
                  <button
                    style={{ display:'flex', alignItems:'center', gap:10, width:'100%',
                      padding:'10px 16px', background:'none', border:'none', cursor:'pointer',
                      fontSize:13.5, fontWeight:600, color:'var(--danger-ink)', fontFamily:'inherit',
                      textAlign:'left', transition:'background .08s' }}
                    onMouseOver={e => e.currentTarget.style.background='var(--danger-soft)'}
                    onMouseOut={e => e.currentTarget.style.background='none'}>
                    <AIcon n="logOut" s={15} c="currentColor"/>
                    Sign out
                  </button>
                </div>
              </div>
            </>
          )}
        </div>
      </header>
    );
  }

  // ── APP ──────────────────────────────────────────────────────
  function AdminApp() {
    const { useTweaks, TweaksPanel, TweakSection, TweakToggle, TweakSlider, TweakColor } = window;
    const [page, setPage] = useState(() => localStorage.getItem('adm_page') || 'dashboard');
    const [collapsed, setCollapsed] = useState(() => localStorage.getItem('adm_coll') === '1');
    const [t, setTweak] = useTweaks({ accent:'#5B4BE8', compactNav:false, tableSize:14 });

    function nav(p) { setPage(p); localStorage.setItem('adm_page', p); }
    function toggleCollapsed(v) { setCollapsed(v); localStorage.setItem('adm_coll', v ? '1' : '0'); }

    const PAGES = {
      dashboard:          window.AdminDashboard,
      students:           window.AdminStudents,
      'create-student':   window.AdminCreateStudent,
      'ai-config':        window.AdminAIConfig,
      prompts:            window.AdminPrompts,
      curriculum:         window.AdminCurriculum,
      'ai-usage':         window.AdminAIUsage,
      'exercise-types':   window.AdminExerciseTypes,
      notifications:      window.AdminNotifications,
      integrations:       window.AdminIntegrations,
      diagnostics:        window.AdminDiagnostics,
    };

    const Page = PAGES[page] || PAGES.dashboard;

    return (
      <div className="adm-layout" style={{ '--indigo': t.accent, '--indigo-ink': t.accent }}>
        <style>{`.adm-nav-item{padding-top:${t.compactNav ? 6 : 9}px;padding-bottom:${t.compactNav ? 6 : 9}px;}.adm-table td{font-size:${t.tableSize}px;}`}</style>
        <Sidebar page={page} setPage={nav} collapsed={collapsed} setCollapsed={toggleCollapsed}/>
        <div className="adm-body">
          <Header page={page} collapsed={collapsed} setCollapsed={toggleCollapsed}/>
          <main className="adm-main">
            <Page navigate={nav}/>
          </main>
        </div>
        <TweaksPanel>
          <TweakSection label="Appearance" />
          <TweakColor label="Accent colour" value={t.accent}
            options={['#5B4BE8','#7C6CFF','#2A6FDB','#0F7A6E','#B45CF0']}
            onChange={v => setTweak('accent', v)} />
          <TweakToggle label="Compact sidebar" value={t.compactNav}
            onChange={v => setTweak('compactNav', v)} />
          <TweakSection label="Tables" />
          <TweakSlider label="Table font size" value={t.tableSize} min={11} max={16} step={0.5} unit="px"
            onChange={v => setTweak('tableSize', v)} />
        </TweaksPanel>
      </div>
    );
  }

  // ── SLIDE-IN DRAWER (reusable app-wide) ──────────────────────
  function SlideIn({ open, onClose, title, sub, width = 500, children, footer }) {
    return (
      <>
        <div style={{
          position:'fixed', inset:0, zIndex:299,
          background:'rgba(33,27,54,.22)', backdropFilter:'blur(3px)',
          opacity: open ? 1 : 0, pointerEvents: open ? 'auto' : 'none',
          transition:'opacity .22s',
        }} onClick={onClose}/>
        <div style={{
          position:'fixed', top:0, right:0, bottom:0,
          width: Math.min(width, window.innerWidth * 0.94),
          background:'#fff',
          boxShadow:'-1px 0 0 var(--border), -6px 0 32px rgba(33,27,54,.10)',
          zIndex:300, display:'flex', flexDirection:'column',
          transform: open ? 'translateX(0)' : 'translateX(100%)',
          transition:'transform .26s cubic-bezier(.4,0,.2,1)',
          pointerEvents: open ? 'auto' : 'none',
        }}>
          <div style={{ display:'flex', alignItems:'flex-start', justifyContent:'space-between',
            padding:'22px 24px', borderBottom:'1px solid var(--border)', flexShrink:0 }}>
            <div>
              <div style={{ fontSize:17, fontWeight:800, color:'var(--ink)', letterSpacing:'-.02em' }}>{title}</div>
              {sub && <div style={{ fontSize:13, color:'var(--muted)', marginTop:3 }}>{sub}</div>}
            </div>
            <button onClick={onClose} style={{
              width:32, height:32, borderRadius:8, border:'1px solid var(--border)',
              background:'#fff', cursor:'pointer', display:'grid', placeItems:'center',
              color:'var(--muted)', flexShrink:0, transition:'background .12s',
            }}
            onMouseOver={e => e.currentTarget.style.background='var(--canvas)'}
            onMouseOut={e => e.currentTarget.style.background='#fff'}>
              <AIcon n="x" s={15}/>
            </button>
          </div>
          <div style={{ flex:1, overflowY:'auto', padding:'24px' }}>
            {children}
          </div>
          {footer && (
            <div style={{ padding:'16px 24px', borderTop:'1px solid var(--border)', flexShrink:0, display:'flex', gap:8 }}>
              {footer}
            </div>
          )}
        </div>
      </>
    );
  }

  Object.assign(window, { AIcon, getAvatarColor, getInitials, cefrClass, lifecycleBadge, AdminApp, SlideIn });
})();
