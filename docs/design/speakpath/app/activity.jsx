// SpeakPath WritingScenario activity flow: intro → practice → feedback.
(function () {
  const { Icon, data, SkillBadge, AIBadge, VocabChip, PhraseChip, ProgressRing,
    ScoreBadge, CoachMessage, CoachAvatar } = window.SP;
  const { scenario, feedback } = data;

  function StepDots({ stage }) {
    const steps = ["Lesson", "Practice", "Feedback"];
    const idx = { intro: 0, practice: 1, feedback: 2 }[stage];
    return (
      <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
        {steps.map((s, i) => (
          <React.Fragment key={s}>
            <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
              <div style={{
                width: 22, height: 22, borderRadius: "50%", display: "grid", placeItems: "center",
                fontSize: 11, fontWeight: 800,
                background: i < idx ? "var(--success)" : i === idx ? "var(--grad-brand)" : "var(--canvas-2)",
                color: i <= idx ? "#fff" : "var(--faint)",
              }}>{i < idx ? <Icon.check size={12} /> : i + 1}</div>
              <span style={{ fontSize: 12, fontWeight: 700, color: i === idx ? "var(--ink)" : "var(--muted)", display: window.innerWidth < 420 && i !== idx ? "none" : "inline" }}>{s}</span>
            </div>
            {i < 2 && <div style={{ width: 14, height: 2, borderRadius: 2, background: i < idx ? "var(--success)" : "var(--border-2)" }} />}
          </React.Fragment>
        ))}
      </div>
    );
  }

  function Collapsible({ title, icon, children, defaultOpen = false, accent = "var(--indigo)" }) {
    const [open, setOpen] = React.useState(defaultOpen);
    const Ico = Icon[icon];
    return (
      <div className="sp-card" style={{ overflow: "hidden" }}>
        <button onClick={() => setOpen(!open)} style={{ width: "100%", display: "flex", alignItems: "center", gap: 10, padding: 15, textAlign: "left", cursor: "pointer" }}>
          <div className="sp-icobox" style={{ width: 32, height: 32, borderRadius: 10, background: "var(--canvas-2)", color: accent }}><Ico size={17} /></div>
          <span style={{ flex: 1, fontWeight: 800, fontSize: 14, color: "var(--ink)" }}>{title}</span>
          <Icon.chevronDown size={18} style={{ color: "var(--muted)", transform: open ? "rotate(180deg)" : "none", transition: "transform .2s" }} />
        </button>
        {open && <div style={{ padding: "0 15px 15px" }} className="sp-fade">{children}</div>}
      </div>
    );
  }

  function InfoBlock({ icon, label, accent, children }) {
    const Ico = Icon[icon];
    return (
      <div className="sp-card sp-card-pad">
        <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 9 }}>
          <div className="sp-icobox" style={{ width: 30, height: 30, borderRadius: 9, background: `${accent}1a`, color: accent }}><Ico size={16} /></div>
          <span style={{ fontWeight: 800, fontSize: 12.5, color: "var(--ink)", letterSpacing: ".02em", textTransform: "uppercase", whiteSpace: "nowrap" }}>{label}</span>
        </div>
        {children}
      </div>
    );
  }

  // ---------------- STATE 1: INTRO ----------------
  function Intro({ onStart }) {
    return (
      <div className="sp-fade sp-col" style={{ gap: 14 }}>
        <div>
          <div style={{ display: "flex", gap: 8, marginBottom: 10, flexWrap: "wrap" }}>
            <SkillBadge skill="writing" /><AIBadge /><span className="sp-pill" style={{ background: "var(--canvas-2)", color: "var(--muted)" }}><Icon.clock size={12} /> {scenario.minutes} min</span>
          </div>
          <h1 className="sp-h1">{scenario.title}</h1>
        </div>

        <InfoBlock icon="chat" label="The situation" accent="#5B4BE8">
          <p style={{ fontSize: 14.5, color: "var(--text)", lineHeight: 1.6, fontWeight: 500 }}>{scenario.situation}</p>
        </InfoBlock>

        <div className="sp-card sp-card-pad" style={{ background: "var(--grad-brand-soft)", border: "1px solid #EADBFF", display: "flex", gap: 12, alignItems: "flex-start" }}>
          <div className="sp-icobox" style={{ width: 34, height: 34, borderRadius: 11, background: "#fff", color: "var(--magenta)" }}><Icon.target size={18} /></div>
          <div>
            <div style={{ fontWeight: 800, fontSize: 12.5, color: "var(--ink)", textTransform: "uppercase", letterSpacing: ".02em", marginBottom: 3 }}>Your goal</div>
            <p style={{ fontSize: 14.5, color: "var(--text)", fontWeight: 600, lineHeight: 1.5 }}>{scenario.goal}</p>
          </div>
        </div>

        <InfoBlock icon="quote" label="Phrases to try" accent="#5B4BE8">
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
            {scenario.targetPhrases.map((p) => <PhraseChip key={p}>{p}</PhraseChip>)}
          </div>
        </InfoBlock>

        <InfoBlock icon="book" label="Vocabulary" accent="#F0982C">
          <div style={{ display: "flex", flexWrap: "wrap", gap: 9 }}>
            {scenario.vocabulary.map((v) => <VocabChip key={v.word} word={v.word} meaning={v.meaning} />)}
          </div>
        </InfoBlock>

        <Collapsible title="See an example message" icon="bulb" accent="var(--success)">
          <pre style={{ margin: 0, fontFamily: "inherit", whiteSpace: "pre-wrap", fontSize: 14, lineHeight: 1.6, color: "var(--text)", fontWeight: 500, background: "var(--surface-2)", padding: 14, borderRadius: 12, border: "1px solid var(--border)" }}>{scenario.example}</pre>
        </Collapsible>

        <div className="sp-card sp-card-pad" style={{ borderColor: "#F6E2C2", background: "var(--warn-soft)", display: "flex", gap: 12, alignItems: "flex-start" }}>
          <Icon.bulb size={20} style={{ color: "var(--vocabulary-ink)", flexShrink: 0, marginTop: 1 }} />
          <div>
            <div style={{ fontWeight: 800, fontSize: 13.5, color: "var(--vocabulary-ink)" }}>{scenario.mistake.title}</div>
            <p style={{ fontSize: 13, color: "#9A6A28", fontWeight: 500, lineHeight: 1.5, marginTop: 3 }}>{scenario.mistake.body}</p>
          </div>
        </div>

        <button className="sp-btn sp-btn-primary sp-btn-block" style={{ marginTop: 4, padding: "16px 20px", fontSize: 16 }} onClick={onStart}>
          <Icon.pen size={17} /> Start writing
        </button>
      </div>
    );
  }

  // ---------------- STATE 2: PRACTICE ----------------
  function Practice({ onSubmit }) {
    const [text, setText] = React.useState(feedback.userDraft);
    const [used, setUsed] = React.useState([]);
    const ref = React.useRef(null);
    const words = text.trim() ? text.trim().split(/\s+/).length : 0;

    const insert = (phrase) => {
      setUsed((u) => (u.includes(phrase) ? u : [...u, phrase]));
      setText((t) => (t ? t.replace(/\s*$/, " ") : "") + phrase + " ");
      if (ref.current) { ref.current.focus(); }
    };

    return (
      <div className="sp-fade sp-col" style={{ gap: 14 }}>
        <div>
          <div style={{ display: "flex", gap: 8, marginBottom: 8 }}><SkillBadge skill="writing" /></div>
          <h1 className="sp-h1" style={{ fontSize: 22 }}>Write your message</h1>
        </div>

        <div className="sp-card sp-card-pad" style={{ display: "flex", gap: 11, background: "var(--writing-soft)", border: "1px solid #DAD6FF" }}>
          <Icon.target size={18} style={{ color: "var(--writing)", flexShrink: 0, marginTop: 1 }} />
          <p style={{ fontSize: 13.5, color: "var(--writing-ink)", fontWeight: 600, lineHeight: 1.5 }}>{scenario.task}</p>
        </div>

        <div className="sp-card" style={{ padding: 0, overflow: "hidden" }}>
          <div style={{ padding: "10px 14px", borderBottom: "1px solid var(--border)", display: "flex", alignItems: "center", gap: 8 }}>
            <Icon.pen size={15} style={{ color: "var(--muted)" }} />
            <span style={{ fontSize: 12.5, fontWeight: 700, color: "var(--muted)" }}>To: Daniel (your manager)</span>
          </div>
          <textarea ref={ref} value={text} onChange={(e) => setText(e.target.value)} placeholder="Start typing your email here…"
            style={{ width: "100%", minHeight: 160, border: "none", outline: "none", resize: "vertical", padding: 15, fontSize: 14.5, lineHeight: 1.6, color: "var(--ink)", background: "transparent", fontWeight: 500 }} />
          <div style={{ padding: "9px 14px", borderTop: "1px solid var(--border)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
            <span style={{ fontSize: 11.5, fontWeight: 700, color: words >= 30 ? "var(--success)" : "var(--muted)", display: "inline-flex", alignItems: "center", gap: 5 }}>
              {words >= 30 && <Icon.check size={13} />}{words} words {words < 30 ? "· aim for 30+" : "· nice length!"}
            </span>
            <span style={{ fontSize: 11.5, color: "var(--faint)", fontWeight: 600 }}>Autosaved</span>
          </div>
        </div>

        <div>
          <div style={{ fontSize: 12.5, fontWeight: 800, color: "var(--muted)", marginBottom: 8, display: "flex", alignItems: "center", gap: 6 }}>
            <Icon.quote size={13} /> TAP A PHRASE TO ADD IT
          </div>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
            {scenario.targetPhrases.map((p) => <PhraseChip key={p} active={used.includes(p)} onClick={() => insert(p)}>{p}</PhraseChip>)}
          </div>
        </div>

        <Collapsible title="Vocabulary hints" icon="book" accent="var(--vocabulary)">
          <div style={{ display: "flex", flexWrap: "wrap", gap: 9 }}>
            {scenario.vocabulary.map((v) => <VocabChip key={v.word} word={v.word} meaning={v.meaning} />)}
          </div>
        </Collapsible>
        <Collapsible title="Peek at the example" icon="bulb" accent="var(--success)">
          <pre style={{ margin: 0, fontFamily: "inherit", whiteSpace: "pre-wrap", fontSize: 14, lineHeight: 1.6, color: "var(--text)", fontWeight: 500, background: "var(--surface-2)", padding: 14, borderRadius: 12, border: "1px solid var(--border)" }}>{scenario.example}</pre>
        </Collapsible>

        <div style={{ display: "flex", gap: 11, alignItems: "center", background: "var(--surface)", padding: 4 }}>
          <CoachAvatar size={34} />
          <span style={{ fontSize: 12.5, color: "var(--muted)", fontWeight: 600, flex: 1 }}>No pressure — there's no single right answer. Write naturally and I'll help you polish it.</span>
        </div>

        <button className="sp-btn sp-btn-primary sp-btn-block" style={{ padding: "16px 20px", fontSize: 16 }} onClick={onSubmit}>
          <Icon.spark size={17} /> Get coach feedback
        </button>
      </div>
    );
  }

  // ---------------- STATE 3: FEEDBACK ----------------
  function Feedback({ onRetry, onNext, onPath }) {
    return (
      <div className="sp-fade sp-col" style={{ gap: 14 }}>
        {/* Score header */}
        <div className="sp-card sp-card-pad" style={{ background: "var(--grad-brand-soft)", border: "1px solid #EADBFF" }}>
          <ScoreBadge score={feedback.score} band={feedback.band} />
        </div>

        <CoachMessage tone="soft">{feedback.summary}</CoachMessage>

        {/* Corrected version */}
        <InfoBlock icon="checkCircle" label="Polished version" accent="#13B07C">
          <pre style={{ margin: "0 0 12px", fontFamily: "inherit", whiteSpace: "pre-wrap", fontSize: 14, lineHeight: 1.6, color: "var(--ink)", fontWeight: 500, background: "var(--success-soft)", padding: 14, borderRadius: 12, border: "1px solid #C7EEDD" }}>{feedback.corrected}</pre>
          <div className="sp-col" style={{ gap: 8 }}>
            {feedback.diffs.map((d, i) => (
              <div key={i} style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 8, fontSize: 12.5 }}>
                <span style={{ textDecoration: "line-through", color: "var(--speaking)", fontWeight: 600, background: "var(--speaking-soft)", padding: "3px 8px", borderRadius: 8 }}>{d.from}</span>
                <Icon.arrowRight size={13} style={{ color: "var(--faint)" }} />
                <span style={{ color: "var(--success)", fontWeight: 700, background: "var(--success-soft)", padding: "3px 8px", borderRadius: 8 }}>{d.to}</span>
                <span style={{ color: "var(--muted)", fontWeight: 600 }}>— {d.note}</span>
              </div>
            ))}
          </div>
        </InfoBlock>

        {/* Two columns of did-well / improve */}
        <div className="sp-grid2" style={{ gridTemplateColumns: "1fr", gap: 14 }}>
          <InfoBlock icon="heart" label="What you did well" accent="#13B07C">
            <ul className="sp-col" style={{ gap: 9, margin: 0, padding: 0, listStyle: "none" }}>
              {feedback.wins.map((w, i) => (
                <li key={i} style={{ display: "flex", gap: 9, fontSize: 13.5, color: "var(--text)", fontWeight: 500, lineHeight: 1.45 }}>
                  <Icon.checkCircle size={17} style={{ color: "var(--success)", flexShrink: 0, marginTop: 1 }} />{w}
                </li>
              ))}
            </ul>
          </InfoBlock>
          <InfoBlock icon="spark" label="Gentle improvements" accent="#F0982C">
            <ul className="sp-col" style={{ gap: 11, margin: 0, padding: 0, listStyle: "none" }}>
              {feedback.improve.map((m, i) => (
                <li key={i} style={{ display: "flex", gap: 9 }}>
                  <div className="sp-icobox" style={{ width: 22, height: 22, borderRadius: 7, background: "var(--warn-soft)", color: "var(--vocabulary-ink)", flexShrink: 0, marginTop: 1 }}><Icon.arrowRight size={13} /></div>
                  <div><div style={{ fontWeight: 800, fontSize: 13.5, color: "var(--ink)" }}>{m.label}</div>
                    <div style={{ fontSize: 12.5, color: "var(--muted)", fontWeight: 500, lineHeight: 1.45 }}>{m.detail}</div></div>
                </li>
              ))}
            </ul>
          </InfoBlock>
        </div>

        <InfoBlock icon="book" label="Grammar focus" accent="#5B4BE8">
          <p style={{ fontSize: 13.5, color: "var(--text)", fontWeight: 500, lineHeight: 1.55 }}>{feedback.grammar}</p>
        </InfoBlock>
        <InfoBlock icon="chat" label="Tone & politeness" accent="#9B5CF6">
          <p style={{ fontSize: 13.5, color: "var(--text)", fontWeight: 500, lineHeight: 1.55 }}>{feedback.tone}</p>
        </InfoBlock>

        <InfoBlock icon="star" label="Vocabulary to remember" accent="#F0982C">
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
            {feedback.vocabToRemember.map((v) => <span key={v} className="sp-pill" style={{ background: "var(--vocabulary-soft)", color: "var(--vocabulary-ink)", padding: "7px 13px", fontSize: 13 }}>{v}</span>)}
          </div>
        </InfoBlock>

        {/* Rewrite challenge */}
        <div className="sp-card sp-card-pad" style={{ background: "var(--grad-cool)", color: "#fff", border: "none", boxShadow: "var(--sh-glow)" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
            <Icon.refresh size={17} /><span style={{ fontWeight: 800, fontSize: 13, textTransform: "uppercase", letterSpacing: ".03em", whiteSpace: "nowrap" }}>Rewrite challenge</span>
          </div>
          <p style={{ fontSize: 14, fontWeight: 500, lineHeight: 1.55, opacity: .95 }}>{feedback.rewrite}</p>
          <button className="sp-btn" style={{ marginTop: 13, background: "rgba(255,255,255,.9)", color: "var(--indigo)" }} onClick={onRetry}>
            <Icon.pen size={15} /> Take the challenge
          </button>
        </div>

        {/* Next suggestion */}
        <InfoBlock icon="arrowRight" label="Suggested next" accent="#5B4BE8">
          <button onClick={onNext} className="sp-card" style={{ width: "100%", padding: 14, display: "flex", alignItems: "center", gap: 13, cursor: "pointer", textAlign: "left", boxShadow: "none", borderColor: "var(--border)" }}>
            <div className="sp-icobox" style={{ width: 42, height: 42, borderRadius: 13, background: "var(--writing-soft)", color: "var(--writing)" }}><Icon.pen size={19} /></div>
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 800, fontSize: 14.5, color: "var(--ink)" }}>{feedback.next.title}</div>
              <div style={{ fontSize: 12.5, color: "var(--muted)", fontWeight: 600 }}>{feedback.next.reason}</div>
            </div>
            <Icon.arrowRight size={18} style={{ color: "var(--indigo)" }} />
          </button>
        </InfoBlock>

        {/* Actions */}
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <button className="sp-btn sp-btn-ghost" style={{ flex: "1 1 130px" }} onClick={onRetry}><Icon.refresh size={15} /> Try again</button>
          <button className="sp-btn sp-btn-primary" style={{ flex: "2 1 180px" }} onClick={onNext}>Continue to next <Icon.arrowRight size={15} /></button>
        </div>
        <button className="sp-btn sp-btn-soft sp-btn-block" onClick={onPath}><Icon.path size={15} /> Back to my path</button>
      </div>
    );
  }

  // ---------------- ORCHESTRATOR ----------------
  function Activity({ go }) {
    const initStage = new URLSearchParams(location.search).get("stage") || "intro";
    const [stage, setStage] = React.useState(initStage);
    const scrollTop = () => { const el = document.querySelector(".sp-content"); if (el) el.scrollTop = 0; window.scrollTo(0, 0); };
    const set = (s) => { setStage(s); scrollTop(); };

    return (
      <div>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 18, gap: 12 }}>
          <button className="sp-btn sp-btn-soft" style={{ padding: "9px 13px", fontSize: 13 }} onClick={() => go("path")}><Icon.arrowLeft size={16} /> Path</button>
          <StepDots stage={stage} />
          <div style={{ width: 70, textAlign: "right" }}>
            <span style={{ fontSize: 11.5, fontWeight: 700, color: "var(--muted)" }}>{scenario.module}</span>
          </div>
        </div>
        {stage === "intro" && <Intro onStart={() => set("practice")} />}
        {stage === "practice" && <Practice onSubmit={() => set("feedback")} />}
        {stage === "feedback" && <Feedback onRetry={() => set("practice")} onNext={() => set("intro")} onPath={() => go("path")} />}
      </div>
    );
  }

  Object.assign(window.SP, { Activity });
})();
