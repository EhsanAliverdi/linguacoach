// SpeakPath shared UI components. Exported to window.SP.
(function () {
  const { Icon, data } = window.SP;
  const { skills } = data;

  // ---- Brand logo --------------------------------------------------------
  function LogoMark({ size = 36, radius }) {
    const r = radius != null ? radius : size * 0.29;
    const id = React.useMemo(() => "lg" + Math.random().toString(36).slice(2, 7), []);
    return (
      <svg width={size} height={size} viewBox="0 0 48 48" fill="none" aria-hidden="true">
        <defs>
          <linearGradient id={id} x1="6" y1="4" x2="42" y2="44" gradientUnits="userSpaceOnUse">
            <stop stopColor="#FF7A59" /><stop offset=".52" stopColor="#B45CF0" /><stop offset="1" stopColor="#5B4BE8" />
          </linearGradient>
        </defs>
        <rect x="2" y="2" width="44" height="44" rx={r} fill={`url(#${id})`} />
        {/* rising path of dots = the journey to speaking */}
        <circle cx="14" cy="33" r="3.4" fill="#fff" />
        <circle cx="24" cy="26" r="3.4" fill="#fff" opacity=".92" />
        {/* speech-bubble node at the summit */}
        <path d="M30 12.5h6.5A3.5 3.5 0 0 1 40 16v3.5a3.5 3.5 0 0 1-3.5 3.5H34l-3.2 2.3.2-2.3h-1A3.5 3.5 0 0 1 26.5 19.5V16A3.5 3.5 0 0 1 30 12.5Z" fill="#fff" />
        <path d="M15.4 30.6 22 27.2M25.7 23.4l3.2-2.6" stroke="#fff" strokeWidth="2.4" strokeLinecap="round" strokeOpacity=".75" />
      </svg>
    );
  }
  function Logo({ size = 36, color = "var(--ink)", showWord = true }) {
    return (
      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
        <LogoMark size={size} />
        {showWord && (
          <span style={{ fontWeight: 800, fontSize: size * 0.55, letterSpacing: "-.02em", color }}>
            Speak<span style={{ color: "var(--indigo)" }}>Path</span>
          </span>
        )}
      </div>
    );
  }

  // ---- Skill helpers -----------------------------------------------------
  const skillVars = (c) => ({
    "--c": `var(--${c})`, "--c-soft": `var(--${c}-soft)`, "--c-ink": `var(--${c}-ink)`,
  });
  function SkillIcon({ skill, size = 40, soft = false, radius = 13 }) {
    const s = skills[skill] || skills.writing;
    const Ico = Icon[s.icon];
    return (
      <div className="sp-icobox" style={{
        width: size, height: size, borderRadius: radius,
        background: soft ? `var(--${s.color}-soft)` : `var(--${s.color})`,
        color: soft ? `var(--${s.color}-ink)` : "#fff",
      }}>
        <Ico size={size * 0.5} />
      </div>
    );
  }
  function SkillBadge({ skill, label }) {
    const s = skills[skill] || skills.writing;
    const Ico = Icon[s.icon];
    return (
      <span className="sp-pill" style={{ background: `var(--${s.color}-soft)`, color: `var(--${s.color}-ink)` }}>
        <Ico size={13} /> {label || s.label}
      </span>
    );
  }

  // ---- AI / fallback badges ---------------------------------------------
  function AIBadge({ label = "AI generated" }) {
    return <span className="sp-ai"><Icon.spark size={12} /> {label}</span>;
  }
  function FallbackBadge() {
    return <span className="sp-ai sp-fallback" style={{ color: "#B26410" }}><Icon.shield size={12} /> Backup content</span>;
  }

  // ---- Progress ----------------------------------------------------------
  function ProgressBar({ value, gradient }) {
    return (
      <div className="sp-prog">
        <i style={{ width: `${value}%`, background: gradient || "linear-gradient(135deg,#FF7A59 0%,#B45CF0 52%,#5B4BE8 100%)" }} />
      </div>
    );
  }
  function ProgressRing({ value, size = 72, stroke = 8, color = "var(--indigo)", children }) {
    const r = (size - stroke) / 2, C = 2 * Math.PI * r;
    return (
      <div style={{ position: "relative", width: size, height: size }}>
        <svg width={size} height={size} style={{ transform: "rotate(-90deg)" }}>
          <circle cx={size / 2} cy={size / 2} r={r} stroke="var(--canvas-2)" strokeWidth={stroke} fill="none" />
          <circle cx={size / 2} cy={size / 2} r={r} stroke={color} strokeWidth={stroke} fill="none"
            strokeLinecap="round" strokeDasharray={C} strokeDashoffset={C * (1 - value / 100)}
            style={{ transition: "stroke-dashoffset 1s cubic-bezier(.22,1,.36,1)" }} />
        </svg>
        <div style={{ position: "absolute", inset: 0, display: "grid", placeItems: "center" }}>{children}</div>
      </div>
    );
  }

  // ---- Chips -------------------------------------------------------------
  function PhraseChip({ children, onClick, active, skill = "writing" }) {
    const s = skills[skill];
    return (
      <button className={"sp-chip" + (active ? " is-on" : "")} onClick={onClick}
        style={active ? { background: `var(--${s.color}-soft)`, color: `var(--${s.color}-ink)`, borderColor: "transparent" } : undefined}>
        <Icon.quote size={13} style={{ opacity: .6 }} /> {children}
      </button>
    );
  }
  function VocabChip({ word, meaning }) {
    return (
      <div style={{
        display: "inline-flex", flexDirection: "column", gap: 1, padding: "8px 13px",
        borderRadius: 14, background: "var(--vocabulary-soft)", border: "1px solid #F6E2C2",
      }}>
        <span style={{ fontWeight: 800, color: "var(--vocabulary-ink)", fontSize: 13.5 }}>{word}</span>
        {meaning && <span style={{ fontSize: 11.5, color: "#A9772F", fontWeight: 600 }}>{meaning}</span>}
      </div>
    );
  }

  // ---- Score badge -------------------------------------------------------
  function ScoreBadge({ score, band, size = 96 }) {
    const color = score >= 85 ? "var(--success)" : score >= 70 ? "var(--vocabulary)" : "var(--speaking)";
    return (
      <div style={{ display: "flex", alignItems: "center", gap: 16 }}>
        <ProgressRing value={score} size={size} stroke={9} color={color}>
          <div style={{ textAlign: "center" }}>
            <div style={{ fontSize: size * 0.34, fontWeight: 800, color: "var(--ink)", lineHeight: 1 }}>{score}</div>
            <div style={{ fontSize: 10, fontWeight: 700, color: "var(--muted)", letterSpacing: ".06em" }}>/ 100</div>
          </div>
        </ProgressRing>
        {band && (
          <div>
            <div style={{ display: "inline-flex" }}><span className="sp-pill" style={{ background: "var(--success-soft)", color: "var(--success)" }}><Icon.spark size={13} /> {band}</span></div>
            <p style={{ marginTop: 8, fontSize: 13, color: "var(--muted)", fontWeight: 600 }}>Your best score in this module yet ✨</p>
          </div>
        )}
      </div>
    );
  }

  // ---- Coach message -----------------------------------------------------
  function CoachAvatar({ size = 40 }) {
    return (
      <div className="sp-icobox" style={{ width: size, height: size, borderRadius: "50%", background: "var(--grad-brand)", color: "#fff", flexShrink: 0, boxShadow: "var(--sh-glow)" }}>
        <Icon.spark size={size * 0.5} />
      </div>
    );
  }
  function CoachMessage({ children, name = "Pace", title = "your AI coach", tone = "soft" }) {
    return (
      <div style={{ display: "flex", gap: 12 }}>
        <CoachAvatar />
        <div style={{ flex: 1 }}>
          <div style={{ display: "flex", alignItems: "baseline", gap: 7, marginBottom: 5 }}>
            <span style={{ fontWeight: 800, color: "var(--ink)", fontSize: 14 }}>{name}</span>
            <span style={{ fontSize: 11.5, color: "var(--muted)", fontWeight: 600 }}>{title}</span>
          </div>
          <div style={{
            background: tone === "brand" ? "var(--grad-brand-soft)" : "var(--surface)",
            border: "1px solid var(--border)", borderRadius: "4px 16px 16px 16px",
            padding: "13px 15px", color: "var(--text)", fontSize: 14, lineHeight: 1.55, fontWeight: 500,
          }}>{children}</div>
        </div>
      </div>
    );
  }

  // ---- Skill summary card (dashboard) -----------------------------------
  function SkillCard({ skill, level, pct, onClick }) {
    const s = skills[skill];
    return (
      <button onClick={onClick} className="sp-card" style={{
        ...skillVars(s.color), textAlign: "left", padding: 14, display: "flex",
        flexDirection: "column", gap: 11, transition: "transform .18s, box-shadow .2s", cursor: "pointer",
      }}
        onMouseEnter={(e) => { e.currentTarget.style.transform = "translateY(-3px)"; e.currentTarget.style.boxShadow = "var(--sh-md)"; }}
        onMouseLeave={(e) => { e.currentTarget.style.transform = ""; e.currentTarget.style.boxShadow = "var(--sh-sm)"; }}>
        <SkillIcon skill={skill} size={38} soft />
        <div>
          <div style={{ fontWeight: 800, color: "var(--ink)", fontSize: 14 }}>{s.label}</div>
          <div style={{ fontSize: 11.5, color: "var(--muted)", fontWeight: 600 }}>{level}</div>
        </div>
        <ProgressBar value={pct} gradient={`var(--${s.color})`} />
      </button>
    );
  }

  // ---- Module card (path) -----------------------------------------------
  function ModuleCard({ mod, onOpen }) {
    const s = skills[mod.skill];
    const pct = Math.round((mod.done / mod.activities) * 100);
    const locked = mod.state === "locked";
    const completed = mod.state === "completed";
    const current = mod.state === "current";
    return (
      <button onClick={() => !locked && onOpen && onOpen(mod)} disabled={locked}
        className="sp-card" style={{
          ...skillVars(s.color), textAlign: "left", padding: 16, display: "flex", gap: 14, alignItems: "center",
          width: "100%", cursor: locked ? "default" : "pointer", opacity: locked ? .72 : 1,
          borderColor: current ? `var(--${s.color})` : "var(--border)",
          boxShadow: current ? "var(--sh-md)" : "var(--sh-sm)",
          transition: "transform .18s, box-shadow .2s",
        }}
        onMouseEnter={(e) => { if (!locked) { e.currentTarget.style.transform = "translateY(-2px)"; e.currentTarget.style.boxShadow = "var(--sh-md)"; } }}
        onMouseLeave={(e) => { if (!locked) { e.currentTarget.style.transform = ""; e.currentTarget.style.boxShadow = current ? "var(--sh-md)" : "var(--sh-sm)"; } }}>
        <div style={{ position: "relative" }}>
          <SkillIcon skill={mod.skill} size={48} soft={!completed} radius={15} />
          {completed && <span style={{ position: "absolute", right: -4, bottom: -4, width: 20, height: 20, borderRadius: "50%", background: "var(--success)", color: "#fff", display: "grid", placeItems: "center", border: "2px solid #fff" }}><Icon.check size={11} /></span>}
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 3 }}>
            <span style={{ fontSize: 11, fontWeight: 800, color: `var(--${s.color}-ink)`, letterSpacing: ".04em" }}>MODULE {mod.n}</span>
            {current && <span className="sp-pill" style={{ padding: "2px 8px", fontSize: 10, background: `var(--${s.color}-soft)`, color: `var(--${s.color}-ink)` }}>IN PROGRESS</span>}
            {mod.soon && locked && <span className="sp-pill" style={{ padding: "2px 8px", fontSize: 10, background: "var(--canvas-2)", color: "var(--muted)" }}>SOON</span>}
          </div>
          <div style={{ fontWeight: 800, color: "var(--ink)", fontSize: 15, marginBottom: 4 }}>{mod.title}</div>
          <div style={{ fontSize: 12.5, color: "var(--muted)", fontWeight: 600, marginBottom: completed || current ? 9 : 0 }}>{mod.goal}</div>
          {(completed || current) && (
            <div style={{ display: "flex", alignItems: "center", gap: 9 }}>
              <div style={{ flex: 1 }}><ProgressBar value={pct} gradient={`var(--${s.color})`} /></div>
              <span style={{ fontSize: 11.5, fontWeight: 700, color: "var(--muted)" }}>{mod.done}/{mod.activities}</span>
            </div>
          )}
        </div>
        <div style={{ flexShrink: 0, color: locked ? "var(--faint)" : `var(--${s.color})` }}>
          {locked ? <Icon.lock size={18} /> : <Icon.chevronRight size={20} />}
        </div>
      </button>
    );
  }

  // ---- Activity row card -------------------------------------------------
  function ActivityCard({ act, onOpen }) {
    const s = skills[act.skill];
    const done = act.state === "done";
    return (
      <button onClick={() => onOpen && onOpen(act)} className="sp-card" style={{
        ...skillVars(s.color), textAlign: "left", padding: 14, display: "flex", gap: 13, alignItems: "center",
        width: "100%", cursor: "pointer", transition: "transform .18s, box-shadow .2s",
        borderColor: act.recommended ? `var(--${s.color})` : "var(--border)",
      }}
        onMouseEnter={(e) => { e.currentTarget.style.transform = "translateX(3px)"; e.currentTarget.style.boxShadow = "var(--sh-md)"; }}
        onMouseLeave={(e) => { e.currentTarget.style.transform = ""; e.currentTarget.style.boxShadow = "var(--sh-sm)"; }}>
        <SkillIcon skill={act.skill} size={42} soft radius={13} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontWeight: 800, color: "var(--ink)", fontSize: 14.5 }}>{act.title}</div>
          <div style={{ fontSize: 12, color: "var(--muted)", fontWeight: 600, marginTop: 2, display: "flex", alignItems: "center", gap: 8 }}>
            <span style={{ display: "inline-flex", alignItems: "center", gap: 3 }}><Icon.clock size={12} /> {act.minutes} min</span>
            <span>·</span><span>{s.label}</span>
          </div>
        </div>
        {done
          ? <span className="sp-pill" style={{ background: "var(--success-soft)", color: "var(--success)" }}><Icon.check size={12} /> {act.score}</span>
          : act.recommended
            ? <span className="sp-pill" style={{ background: "var(--grad-brand)", color: "#fff" }}>Start <Icon.arrowRight size={12} /></span>
            : <Icon.chevronRight size={18} style={{ color: "var(--faint)" }} />}
      </button>
    );
  }

  function StatTile({ icon, value, label, color = "var(--indigo)", bg = "var(--canvas-2)" }) {
    const Ico = Icon[icon];
    return (
      <div className="sp-card" style={{ padding: 14, display: "flex", flexDirection: "column", gap: 8 }}>
        <div className="sp-icobox" style={{ width: 34, height: 34, borderRadius: 11, background: bg, color }}><Ico size={18} /></div>
        <div><div style={{ fontWeight: 800, fontSize: 21, color: "var(--ink)", lineHeight: 1 }}>{value}</div>
          <div style={{ fontSize: 11.5, color: "var(--muted)", fontWeight: 600, marginTop: 3 }}>{label}</div></div>
      </div>
    );
  }

  function SectionHeader({ title, action, onAction }) {
    return (
      <div className="sp-section-h">
        <h3>{title}</h3>
        {action && <a onClick={onAction} style={{ cursor: "pointer" }}>{action} <Icon.chevronRight size={13} /></a>}
      </div>
    );
  }

  Object.assign(window.SP, {
    Logo, LogoMark, SkillIcon, SkillBadge, AIBadge, FallbackBadge, ProgressBar, ProgressRing,
    PhraseChip, VocabChip, ScoreBadge, CoachAvatar, CoachMessage, SkillCard, ModuleCard,
    ActivityCard, StatTile, SectionHeader, skillVars,
  });
})();
