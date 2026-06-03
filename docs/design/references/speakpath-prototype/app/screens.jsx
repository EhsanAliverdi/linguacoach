// SpeakPath screens: Dashboard, MyPath, Progress, Profile.
(function () {
  const { Icon, data, Logo, SkillIcon, SkillBadge, AIBadge, ProgressBar, ProgressRing,
    SkillCard, ModuleCard, ActivityCard, StatTile, SectionHeader, CoachMessage, skillVars } = window.SP;
  const { skills, user, path, modules, activities, feedback } = data;

  // ============ DASHBOARD ============
  function Dashboard({ go }) {
    const rec = activities.find((a) => a.recommended);
    const curMod = modules.find((m) => m.state === "current");
    const skillLevels = [
      { skill: "writing", level: "Level 3 · Building", pct: 64 },
      { skill: "speaking", level: "Level 1 · Starting", pct: 18 },
      { skill: "listening", level: "Level 1 · Starting", pct: 12 },
      { skill: "vocabulary", level: "Level 2 · Growing", pct: 41 },
      { skill: "pronunciation", level: "Not started", pct: 6 },
    ];
    return (
      <div className="sp-fade">
        {/* Up-next hero */}
        <div className="sp-card" style={{ overflow: "hidden", padding: 0, border: "none", boxShadow: "var(--sh-lg)" }}>
          <div className="grad-brand" style={{ padding: "20px 20px 22px", color: "#fff" }}>
            <div>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
                <span className="sp-pill" style={{ background: "rgba(255,255,255,.22)", color: "#fff" }}><Icon.spark size={12} /> Recommended next</span>
              </div>
              <div style={{ fontSize: 12, fontWeight: 700, opacity: .85, marginBottom: 4 }}>{path.title} · {curMod.title}</div>
              <h2 style={{ color: "#fff", fontSize: 22, lineHeight: 1.18, marginBottom: 14, maxWidth: 360 }}>{rec.title}</h2>
              <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
                <button className="sp-btn" style={{ background: "#fff", color: "var(--indigo)", boxShadow: "0 8px 20px rgba(0,0,0,.18)" }} onClick={() => go("activity")}>
                  <Icon.play size={15} /> Continue learning
                </button>
                <span style={{ fontSize: 12.5, fontWeight: 600, opacity: .92, display: "inline-flex", alignItems: "center", gap: 5 }}>
                  <Icon.clock size={14} /> {rec.minutes} min · Writing
                </span>
              </div>
            </div>
          </div>
          <div style={{ background: "var(--surface)", padding: "13px 18px", display: "flex", alignItems: "center", gap: 12 }}>
            <div style={{ flex: 1 }}>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6 }}>
                <span style={{ fontSize: 12, fontWeight: 700, color: "var(--text)" }}>Module progress</span>
                <span style={{ fontSize: 12, fontWeight: 800, color: "var(--indigo)" }}>{curMod.done}/{curMod.activities} done</span>
              </div>
              <ProgressBar value={Math.round((curMod.done / curMod.activities) * 100)} />
            </div>
          </div>
        </div>

        {/* Stats */}
        <div className="sp-statgrid" style={{ marginTop: 16 }}>
          <StatTile icon="flame" value={`${user.streak} days`} label="Practice streak" color="var(--speaking)" bg="var(--speaking-soft)" />
          <StatTile icon="clock" value={user.minutesThisWeek} label="Minutes this week" color="var(--indigo)" bg="var(--writing-soft)" />
          <StatTile icon="trophy" value={user.activitiesDone} label="Activities done" color="var(--pronunciation)" bg="var(--pronunciation-soft)" />
        </div>

        <div className="sp-grid2" style={{ marginTop: 6 }}>
          <div className="sp-col">
            <SectionHeader title="Practise your skills" />
            <div className="sp-skillgrid sp-stagger">
              {skillLevels.map((sl) => <SkillCard key={sl.skill} {...sl} onClick={() => go("path")} />)}
            </div>

            <SectionHeader title="Up next in your path" action="My Path" onAction={() => go("path")} />
            <div className="sp-col" style={{ gap: 10 }}>
              {activities.filter((a) => a.state !== "done").slice(0, 2).map((a) => <ActivityCard key={a.id} act={a} onOpen={() => go("activity")} />)}
            </div>
          </div>

          <div className="sp-col">
            <SectionHeader title="Latest from your coach" />
            <div className="sp-card sp-card-pad" style={{ background: "var(--grad-brand-soft)", border: "1px solid #EADBFF" }}>
              <CoachMessage tone="brand">
                Lovely progress this week, {user.name}! Your last email scored <b>88</b> — your requests are sounding much more natural. Ready to ask for a deadline extension next?
              </CoachMessage>
              <div className="sp-card" style={{ marginTop: 13, padding: 13, display: "flex", alignItems: "center", gap: 12, background: "var(--surface)" }}>
                <div className="sp-icobox" style={{ width: 40, height: 40, borderRadius: 12, background: "var(--success-soft)", color: "var(--success)" }}><Icon.checkCircle size={20} /></div>
                <div style={{ flex: 1 }}>
                  <div style={{ fontWeight: 800, fontSize: 13.5, color: "var(--ink)" }}>Introduce yourself to a new team</div>
                  <div style={{ fontSize: 12, color: "var(--muted)", fontWeight: 600 }}>Completed · warm & professional tone 🎉</div>
                </div>
                <span className="sp-pill" style={{ background: "var(--success-soft)", color: "var(--success)" }}>88</span>
              </div>
            </div>

            <div className="sp-card sp-card-pad" style={{ marginTop: 14 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12 }}>
                <Icon.flame size={18} style={{ color: "var(--speaking)" }} />
                <span style={{ fontWeight: 800, fontSize: 14, color: "var(--ink)" }}>{user.streak}-day streak</span>
                <span style={{ fontSize: 12, color: "var(--muted)", fontWeight: 600, marginLeft: "auto" }}>Keep it going!</span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", gap: 6 }}>
                {["M", "T", "W", "T", "F", "S", "S"].map((d, i) => (
                  <div key={i} style={{ textAlign: "center", flex: 1 }}>
                    <div style={{
                      height: 34, borderRadius: 11, display: "grid", placeItems: "center", marginBottom: 5,
                      background: user.streakDays[i] ? "var(--grad-warm)" : "var(--canvas-2)",
                      color: user.streakDays[i] ? "#fff" : "var(--faint)",
                    }}>{user.streakDays[i] ? <Icon.flame size={15} /> : <Icon.x size={12} />}</div>
                    <span style={{ fontSize: 10.5, fontWeight: 700, color: "var(--muted)" }}>{d}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ============ MY PATH ============
  function MyPath({ go }) {
    return (
      <div className="sp-fade">
        <div className="sp-card" style={{ padding: 0, overflow: "hidden", border: "none", boxShadow: "var(--sh-md)" }}>
          <div className="grad-cool" style={{ padding: "22px 20px", color: "#fff" }}>
            <div>
              <div style={{ display: "flex", gap: 8, marginBottom: 12 }}><AIBadge label="AI-personalised path" /></div>
              <h2 style={{ color: "#fff", fontSize: 23, lineHeight: 1.18, maxWidth: 420 }}>{path.title}</h2>
              <p style={{ color: "rgba(255,255,255,.9)", fontSize: 13.5, marginTop: 8, fontWeight: 500, maxWidth: 460, lineHeight: 1.5 }}>{path.description}</p>
              <div style={{ display: "flex", alignItems: "center", gap: 14, marginTop: 16 }}>
                <ProgressRing value={path.progress} size={56} stroke={6} color="#fff">
                  <span style={{ fontSize: 13, fontWeight: 800, color: "#fff" }}>{path.progress}%</span>
                </ProgressRing>
                <div style={{ fontSize: 12.5, fontWeight: 600, color: "rgba(255,255,255,.92)" }}>
                  <div style={{ fontWeight: 800, fontSize: 14, color: "#fff" }}>2 of 6 modules</div>
                  on your way to confident workplace English
                </div>
              </div>
            </div>
          </div>
        </div>

        <SectionHeader title="Your journey" />
        <div className="sp-col" style={{ gap: 12, position: "relative" }}>
          {/* connecting spine */}
          <div aria-hidden style={{ position: "absolute", left: 39, top: 30, bottom: 30, width: 2, background: "repeating-linear-gradient(#E2DEF0 0 6px, transparent 6px 12px)", zIndex: 0 }} />
          <div className="sp-stagger sp-col" style={{ gap: 12, position: "relative", zIndex: 1 }}>
            {modules.map((m) => <ModuleCard key={m.id} mod={m} onOpen={() => go("activity")} />)}
          </div>
        </div>

        <div className="sp-card sp-card-pad" style={{ marginTop: 16, display: "flex", gap: 12, alignItems: "center", background: "var(--grad-brand-soft)", border: "1px solid #EADBFF" }}>
          <div className="sp-icobox" style={{ width: 42, height: 42, borderRadius: 13, background: "#fff", color: "var(--magenta)" }}><Icon.spark size={22} /></div>
          <div style={{ flex: 1 }}>
            <div style={{ fontWeight: 800, fontSize: 14, color: "var(--ink)" }}>Your path adapts as you learn</div>
            <div style={{ fontSize: 12.5, color: "var(--text)", fontWeight: 500 }}>SpeakPath adds new modules based on what you find easy or tricky.</div>
          </div>
        </div>
      </div>
    );
  }

  // ============ PROGRESS ============
  function Progress({ go }) {
    const skillLevels = [
      { skill: "writing", pct: 64, label: "Level 3 · Building" },
      { skill: "speaking", pct: 18, label: "Level 1 · Starting" },
      { skill: "listening", pct: 12, label: "Level 1 · Starting" },
      { skill: "vocabulary", pct: 41, label: "Level 2 · Growing" },
      { skill: "pronunciation", pct: 6, label: "Not started" },
    ];
    const recent = [
      { title: "Introduce yourself to a new team", score: 88, when: "Yesterday" },
      { title: "Reply to a meeting invitation", score: 82, when: "2 days ago" },
      { title: "Thank a colleague for help", score: 91, when: "4 days ago" },
    ];
    return (
      <div className="sp-fade">
        <h1 className="sp-h1" style={{ marginBottom: 4 }}>Your progress</h1>
        <p className="sp-muted" style={{ fontWeight: 600, marginBottom: 18 }}>Every activity moves you forward. Here's the picture so far.</p>

        <div className="sp-statgrid">
          <StatTile icon="flame" value={`${user.streak}`} label="Day streak" color="var(--speaking)" bg="var(--speaking-soft)" />
          <StatTile icon="trophy" value={user.activitiesDone} label="Activities" color="var(--vocabulary)" bg="var(--vocabulary-soft)" />
          <StatTile icon="star" value="86" label="Avg. score" color="var(--success)" bg="var(--success-soft)" />
        </div>

        <SectionHeader title="Skill levels" />
        <div className="sp-card sp-card-pad sp-col" style={{ gap: 16 }}>
          {skillLevels.map((sl) => {
            const s = skills[sl.skill];
            return (
              <div key={sl.skill} style={{ display: "flex", alignItems: "center", gap: 13 }}>
                <SkillIcon skill={sl.skill} size={38} soft />
                <div style={{ flex: 1 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6 }}>
                    <span style={{ fontWeight: 800, fontSize: 13.5, color: "var(--ink)" }}>{s.label}</span>
                    <span style={{ fontSize: 12, fontWeight: 700, color: "var(--muted)" }}>{sl.label}</span>
                  </div>
                  <ProgressBar value={sl.pct} gradient={`var(--${s.color})`} />
                </div>
              </div>
            );
          })}
        </div>

        <SectionHeader title="Recent results" />
        <div className="sp-col" style={{ gap: 10 }}>
          {recent.map((r, i) => (
            <div key={i} className="sp-card" style={{ padding: 14, display: "flex", alignItems: "center", gap: 13 }}>
              <div className="sp-icobox" style={{ width: 40, height: 40, borderRadius: 12, background: "var(--writing-soft)", color: "var(--writing)" }}><Icon.pen size={18} /></div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 800, fontSize: 14, color: "var(--ink)" }}>{r.title}</div>
                <div style={{ fontSize: 12, color: "var(--muted)", fontWeight: 600 }}>{r.when} · Writing</div>
              </div>
              <ProgressRing value={r.score} size={44} stroke={5} color={r.score >= 85 ? "var(--success)" : "var(--vocabulary)"}>
                <span style={{ fontSize: 13, fontWeight: 800, color: "var(--ink)" }}>{r.score}</span>
              </ProgressRing>
            </div>
          ))}
        </div>
      </div>
    );
  }

  // ============ PROFILE ============
  function Profile({ go }) {
    const rows = [
      { icon: "target", label: "Learning goal", value: user.goal },
      { icon: "flag", label: "Current level", value: user.level },
      { icon: "globe", label: "Practising", value: "English (Australian workplace)" },
      { icon: "bell", label: "Daily reminder", value: "8:00 PM" },
    ];
    return (
      <div className="sp-fade">
        <div className="sp-card grad-soft" style={{ padding: 22, display: "flex", alignItems: "center", gap: 16 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 16, width: "100%" }}>
            <div className="sp-icobox" style={{ width: 64, height: 64, borderRadius: 22, background: "var(--grad-brand)", color: "#fff", fontSize: 26, fontWeight: 800, boxShadow: "var(--sh-glow)" }}>{user.name[0]}</div>
            <div style={{ flex: 1 }}>
              <h2 style={{ fontSize: 21 }}>{user.name}</h2>
              <div style={{ display: "flex", gap: 7, marginTop: 7, flexWrap: "wrap" }}>
                <span className="sp-pill" style={{ background: "#fff", color: "var(--indigo)" }}><Icon.flag size={12} /> {user.level}</span>
                <span className="sp-pill" style={{ background: "#fff", color: "var(--speaking)" }}><Icon.flame size={12} /> {user.streak}-day streak</span>
              </div>
            </div>
          </div>
        </div>

        <SectionHeader title="Your learning" />
        <div className="sp-card sp-col" style={{ overflow: "hidden", padding: 0 }}>
          {rows.map((r, i) => {
            const Ico = Icon[r.icon];
            return (
              <div key={i} style={{ display: "flex", alignItems: "center", gap: 13, padding: "14px 16px", borderTop: i ? "1px solid var(--border)" : "none" }}>
                <div className="sp-icobox" style={{ width: 36, height: 36, borderRadius: 11, background: "var(--canvas-2)", color: "var(--indigo)" }}><Ico size={17} /></div>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 12, color: "var(--muted)", fontWeight: 700 }}>{r.label}</div>
                  <div style={{ fontSize: 14, color: "var(--ink)", fontWeight: 700 }}>{r.value}</div>
                </div>
                <Icon.chevronRight size={17} style={{ color: "var(--faint)" }} />
              </div>
            );
          })}
        </div>

        <SectionHeader title="Settings" />
        <div className="sp-card sp-col" style={{ overflow: "hidden", padding: 0 }}>
          {[{ icon: "settings", label: "Notifications & sound" }, { icon: "shield", label: "Privacy & data" }, { icon: "heart", label: "Rate SpeakPath" }, { icon: "logout", label: "Sign out", danger: true }].map((r, i) => {
            const Ico = Icon[r.icon];
            return (
              <button key={i} style={{ display: "flex", alignItems: "center", gap: 13, padding: "14px 16px", borderTop: i ? "1px solid var(--border)" : "none", width: "100%", textAlign: "left", cursor: "pointer" }}>
                <div className="sp-icobox" style={{ width: 36, height: 36, borderRadius: 11, background: r.danger ? "var(--speaking-soft)" : "var(--canvas-2)", color: r.danger ? "var(--speaking)" : "var(--text)" }}><Ico size={17} /></div>
                <span style={{ flex: 1, fontWeight: 700, fontSize: 14, color: r.danger ? "var(--speaking)" : "var(--ink)" }}>{r.label}</span>
                <Icon.chevronRight size={17} style={{ color: "var(--faint)" }} />
              </button>
            );
          })}
        </div>
      </div>
    );
  }

  Object.assign(window.SP, { Dashboard, MyPath, Progress, Profile });
})();
