// SpeakPath Brand & System canvas content. Uses window.SP components.
(function () {
  const { Icon, data, Logo, LogoMark, SkillIcon, SkillBadge, AIBadge, FallbackBadge,
    ProgressBar, ProgressRing, PhraseChip, VocabChip, ScoreBadge, CoachMessage,
    CoachAvatar, SkillCard, ModuleCard, ActivityCard, StatTile } = window.SP;
  const { DesignCanvas, DCSection, DCArtboard } = window;
  const { skills, modules, activities } = data;

  const PAD = { padding: 26, height: "100%", overflow: "hidden" };
  const EY = { fontSize: 11, fontWeight: 800, letterSpacing: ".08em", textTransform: "uppercase", color: "var(--muted)" };

  // ---------------------------------------------------------------- helpers
  function Swatch({ bg, name, hex, dark, big }) {
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 7 }}>
        <div style={{ height: big ? 64 : 52, borderRadius: 14, background: bg,
          border: "1px solid rgba(0,0,0,.06)", boxShadow: "inset 0 0 0 1px rgba(255,255,255,.25)" }} />
        <div>
          <div style={{ fontSize: 12.5, fontWeight: 800, color: "var(--ink)" }}>{name}</div>
          <div className="mono" style={{ color: "var(--muted)" }}>{hex}</div>
        </div>
      </div>
    );
  }
  function TypeRow({ children, spec, style }) {
    return (
      <div style={{ display: "flex", alignItems: "baseline", justifyContent: "space-between", gap: 16, paddingBottom: 14, borderBottom: "1px dashed var(--border-2)" }}>
        <div style={{ ...style, color: "var(--ink)", minWidth: 0 }}>{children}</div>
        <div className="mono" style={{ color: "var(--muted)", whiteSpace: "nowrap", flexShrink: 0 }}>{spec}</div>
      </div>
    );
  }

  // ---------------------------------------------------------------- BRAND
  function LogoPanel() {
    return (
      <div style={PAD}>
        <div style={EY}>Logo &amp; lockups</div>
        <div style={{ display: "flex", flexDirection: "column", gap: 18, marginTop: 18 }}>
          <div style={{ display: "flex", gap: 16, alignItems: "stretch" }}>
            <div className="sp-card" style={{ flex: 1, padding: 22, display: "flex", alignItems: "center", justifyContent: "center" }}>
              <Logo size={42} />
            </div>
            <div className="grad-brand" style={{ width: 150, borderRadius: 22, display: "flex", alignItems: "center", justifyContent: "center" }}>
              <Logo size={36} color="#fff" />
            </div>
          </div>
          <div style={{ display: "flex", gap: 16 }}>
            <div className="sp-card" style={{ flex: 1, padding: 18, display: "flex", alignItems: "center", gap: 14 }}>
              <LogoMark size={56} />
              <div>
                <div style={{ fontSize: 12.5, fontWeight: 800, color: "var(--ink)" }}>App icon</div>
                <div style={{ fontSize: 11.5, color: "var(--muted)", fontWeight: 600 }}>Rising path → speech bubble</div>
              </div>
            </div>
            <div style={{ flex: 1, padding: 18, borderRadius: 22, background: "#211B36", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <Logo size={34} color="#fff" />
            </div>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 10, color: "var(--text)", fontSize: 12.5, fontWeight: 600 }}>
            <Icon.spark size={15} style={{ color: "var(--magenta)" }} />
            The mark reads as a learner's journey climbing toward confident speech — three nodes rising into a speech bubble.
          </div>
        </div>
      </div>
    );
  }

  function ColourPanel() {
    const skillList = ["writing", "speaking", "listening", "vocabulary", "pronunciation"];
    const hexes = { writing: "#5B4BE8", speaking: "#FB6B57", listening: "#9B5CF6", vocabulary: "#F0982C", pronunciation: "#10B5A4" };
    return (
      <div style={PAD}>
        <div style={EY}>Colour system</div>
        <div className="grad-brand" style={{ height: 78, borderRadius: 18, marginTop: 16, padding: 16, display: "flex", flexDirection: "column", justifyContent: "space-between", color: "#fff" }}>
          <span style={{ fontWeight: 800, fontSize: 15 }}>Signature gradient</span>
          <span className="mono" style={{ opacity: .92 }}>#FF7A59 → #B45CF0 → #5B4BE8</span>
        </div>
        <div style={{ ...EY, marginTop: 20 }}>Skill accents</div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(5,1fr)", gap: 12, marginTop: 11 }}>
          {skillList.map((s) => <Swatch key={s} bg={hexes[s]} name={skills[s].label} hex={hexes[s]} dark />)}
        </div>
        <div style={{ ...EY, marginTop: 20 }}>Neutrals &amp; status</div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(6,1fr)", gap: 12, marginTop: 11 }}>
          <Swatch bg="#211B36" name="Ink" hex="#211B36" dark />
          <Swatch bg="#4B4462" name="Text" hex="#4B4462" dark />
          <Swatch bg="#8B85A0" name="Muted" hex="#8B85A0" dark />
          <Swatch bg="#ECE9F5" name="Border" hex="#ECE9F5" />
          <Swatch bg="#F6F4FB" name="Canvas" hex="#F6F4FB" />
          <Swatch bg="#13B07C" name="Success" hex="#13B07C" dark />
        </div>
      </div>
    );
  }

  function TypePanel() {
    return (
      <div style={PAD}>
        <div style={EY}>Typeface</div>
        <div style={{ display: "flex", alignItems: "baseline", gap: 12, marginTop: 12, marginBottom: 8 }}>
          <span style={{ fontSize: 30, fontWeight: 800, color: "var(--ink)", letterSpacing: "-.03em" }}>Plus Jakarta Sans</span>
          <span style={{ fontSize: 12, color: "var(--muted)", fontWeight: 600 }}>geometric · friendly · professional</span>
        </div>
        <div style={{ display: "flex", gap: 14, marginBottom: 18, color: "var(--muted)", fontSize: 12, fontWeight: 600 }}>
          <span style={{ fontWeight: 400, color: "var(--ink)" }}>Regular</span>
          <span style={{ fontWeight: 600, color: "var(--ink)" }}>Semibold</span>
          <span style={{ fontWeight: 700, color: "var(--ink)" }}>Bold</span>
          <span style={{ fontWeight: 800, color: "var(--ink)" }}>Extrabold</span>
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <TypeRow spec="32 / 800" style={{ fontSize: 32, fontWeight: 800, letterSpacing: "-.03em" }}>Speak with confidence</TypeRow>
          <TypeRow spec="22 / 800" style={{ fontSize: 22, fontWeight: 800, letterSpacing: "-.02em" }}>Ask for a deadline extension</TypeRow>
          <TypeRow spec="16 / 800" style={{ fontSize: 16, fontWeight: 800 }}>Practise your skills</TypeRow>
          <TypeRow spec="15 / 500" style={{ fontSize: 15, fontWeight: 500, color: "var(--text)" }}>Body copy is warm, plain and easy to read for new English speakers.</TypeRow>
          <TypeRow spec="11 / 800 · caps" style={{ fontSize: 11, fontWeight: 800, letterSpacing: ".08em", textTransform: "uppercase", color: "var(--muted)" }}>Section label</TypeRow>
        </div>
      </div>
    );
  }

  function IconsPanel() {
    const names = ["home", "path", "pen", "mic", "ear", "book", "sound", "spark", "flame", "target", "bulb", "chat", "trophy", "checkCircle", "lock", "star"];
    return (
      <div style={PAD}>
        <div style={EY}>Iconography · 2px line, rounded</div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(8,1fr)", gap: 10, marginTop: 14 }}>
          {names.map((n) => {
            const I = Icon[n];
            return <div key={n} className="sp-icobox" style={{ aspectRatio: "1", borderRadius: 13, background: "var(--canvas-2)", color: "var(--ink)" }}><I size={21} /></div>;
          })}
        </div>
        <div style={{ ...EY, marginTop: 22 }}>Motifs</div>
        <div style={{ display: "flex", gap: 12, marginTop: 12 }}>
          <div className="sp-card" style={{ flex: 1, padding: 14, display: "flex", alignItems: "center", gap: 10 }}>
            <svg width="60" height="34" viewBox="0 0 60 34"><circle cx="8" cy="26" r="4.5" fill="#FF7A59" /><circle cx="30" cy="17" r="4.5" fill="#B45CF0" /><circle cx="52" cy="8" r="4.5" fill="#5B4BE8" /><path d="M12 24 26 19M34 15 48 10" stroke="#C9C2DE" strokeWidth="2" strokeLinecap="round" strokeDasharray="0.1 4" /></svg>
            <span style={{ fontSize: 11.5, fontWeight: 700, color: "var(--muted)" }}>Rising path</span>
          </div>
          <div className="grad-brand" style={{ flex: 1, borderRadius: 16, minHeight: 60 }} />
          <div className="sp-card" style={{ flex: 1, padding: 14, display: "flex", alignItems: "center", justifyContent: "center", gap: 7 }}>
            {["#5B4BE8", "#FB6B57", "#9B5CF6", "#F0982C", "#10B5A4"].map((c) => <span key={c} style={{ width: 16, height: 16, borderRadius: "50%", background: c }} />)}
          </div>
        </div>
      </div>
    );
  }

  // ---------------------------------------------------------------- COMPONENTS
  function BadgesPanel() {
    return (
      <div style={PAD}>
        <div style={EY}>Badges, chips &amp; pills</div>
        <div className="sp-col" style={{ gap: 16, marginTop: 16 }}>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
            {["writing", "speaking", "listening", "vocabulary", "pronunciation"].map((s) => <SkillBadge key={s} skill={s} />)}
          </div>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "center" }}>
            <AIBadge /><FallbackBadge />
            <span className="sp-pill" style={{ background: "var(--success-soft)", color: "var(--success)" }}><Icon.check size={12} /> Completed</span>
            <span className="sp-pill" style={{ background: "var(--canvas-2)", color: "var(--muted)" }}><Icon.lock size={11} /> Locked</span>
          </div>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
            <PhraseChip>Would it be possible to…</PhraseChip>
            <PhraseChip active>I apologise for the…</PhraseChip>
          </div>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 9 }}>
            <VocabChip word="extension" meaning="extra time to finish" />
            <VocabChip word="accurate" meaning="correct, no mistakes" />
          </div>
        </div>
      </div>
    );
  }

  function ButtonsPanel() {
    return (
      <div style={PAD}>
        <div style={EY}>Buttons, progress &amp; stats</div>
        <div className="sp-col" style={{ gap: 14, marginTop: 16 }}>
          <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
            <button className="sp-btn sp-btn-primary"><Icon.play size={15} /> Primary</button>
            <button className="sp-btn sp-btn-ghost">Ghost</button>
            <button className="sp-btn sp-btn-soft">Soft</button>
          </div>
          <div className="sp-col" style={{ gap: 9 }}>
            <ProgressBar value={72} />
            <ProgressBar value={38} gradient="var(--speaking)" />
          </div>
          <div style={{ display: "flex", gap: 16, alignItems: "center" }}>
            <ProgressRing value={86} size={64} stroke={8} color="var(--success)"><span style={{ fontWeight: 800, fontSize: 17, color: "var(--ink)" }}>86</span></ProgressRing>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10, flex: 1 }}>
              <StatTile icon="flame" value="6" label="Day streak" color="var(--speaking)" bg="var(--speaking-soft)" />
              <StatTile icon="trophy" value="27" label="Activities" color="var(--vocabulary)" bg="var(--vocabulary-soft)" />
            </div>
          </div>
        </div>
      </div>
    );
  }

  function CardsPanel() {
    const cur = modules.find((m) => m.state === "current");
    const lock = modules.find((m) => m.state === "locked");
    return (
      <div style={PAD}>
        <div style={EY}>Path &amp; activity cards</div>
        <div className="sp-col" style={{ gap: 11, marginTop: 16 }}>
          <ModuleCard mod={cur} onOpen={() => {}} />
          <ModuleCard mod={lock} onOpen={() => {}} />
          <ActivityCard act={activities.find((a) => a.recommended)} onOpen={() => {}} />
        </div>
      </div>
    );
  }

  function CoachPanel() {
    return (
      <div style={PAD}>
        <div style={EY}>Coach &amp; score</div>
        <div className="sp-col" style={{ gap: 16, marginTop: 16 }}>
          <div className="sp-card sp-card-pad" style={{ background: "var(--grad-brand-soft, #F4ECFF)" }}>
            <CoachMessage tone="brand">Lovely work — your tone is warm and professional. One small fix and it's perfect.</CoachMessage>
          </div>
          <div className="sp-card sp-card-pad"><ScoreBadge score={86} band="Great work" size={84} /></div>
          <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 8, fontSize: 12.5 }}>
            <span style={{ textDecoration: "line-through", color: "var(--speaking)", fontWeight: 600, background: "var(--speaking-soft)", padding: "3px 8px", borderRadius: 8 }}>is possible</span>
            <Icon.arrowRight size={13} style={{ color: "var(--faint)" }} />
            <span style={{ color: "var(--success)", fontWeight: 700, background: "var(--success-soft)", padding: "3px 8px", borderRadius: 8 }}>would it be possible</span>
          </div>
        </div>
      </div>
    );
  }

  function StatesPanel() {
    const State = ({ icon, color, bg, title, body }) => {
      const I = Icon[icon];
      return (
        <div className="sp-card" style={{ padding: 16, display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", gap: 8 }}>
          <div className="sp-icobox" style={{ width: 44, height: 44, borderRadius: 14, background: bg, color }}><I size={22} /></div>
          <div style={{ fontWeight: 800, fontSize: 13.5, color: "var(--ink)" }}>{title}</div>
          <div style={{ fontSize: 11.5, color: "var(--muted)", fontWeight: 600, lineHeight: 1.4 }}>{body}</div>
        </div>
      );
    };
    return (
      <div style={PAD}>
        <div style={EY}>System states</div>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 11, marginTop: 16 }}>
          <State icon="book" color="var(--indigo)" bg="var(--writing-soft)" title="Empty" body="No activities yet — let's build your path." />
          <State icon="spark" color="var(--magenta)" bg="#F4ECFF" title="Loading" body="Your AI coach is preparing feedback…" />
          <State icon="refresh" color="var(--speaking)" bg="var(--speaking-soft)" title="Error" body="Something went wrong. Tap to retry." />
        </div>
        <div style={{ display: "flex", gap: 8, marginTop: 14, alignItems: "center", flexWrap: "wrap" }}>
          <AIBadge /><span style={{ fontSize: 11.5, color: "var(--muted)", fontWeight: 600 }}>AI-generated content</span>
          <FallbackBadge /><span style={{ fontSize: 11.5, color: "var(--muted)", fontWeight: 600 }}>SystemFallback backup</span>
        </div>
      </div>
    );
  }

  // ---------------------------------------------------------------- DEVICE FRAMES
  function PhoneFrame({ screen, stage }) {
    const q = "SpeakPath.html?screen=" + screen + (stage ? "&stage=" + stage : "");
    return (
      <div style={{ position: "relative", height: "100%", background: "#15111F" }}>
        <div style={{ position: "absolute", top: 9, left: "50%", transform: "translateX(-50%)", width: 96, height: 26, borderRadius: 99, background: "#000", zIndex: 5 }} />
        <iframe src={q} title={screen} style={{ width: "100%", height: "100%", border: "none", display: "block" }} />
      </div>
    );
  }
  function DesktopFrame({ screen }) {
    return (
      <div style={{ height: "100%", display: "flex", flexDirection: "column", background: "#fff" }}>
        <div style={{ height: 42, flexShrink: 0, background: "#EFEAF7", borderBottom: "1px solid var(--border)", display: "flex", alignItems: "center", gap: 8, padding: "0 14px" }}>
          <span style={{ display: "flex", gap: 6 }}>
            {["#FF7A59", "#F0982C", "#13B07C"].map((c) => <span key={c} style={{ width: 11, height: 11, borderRadius: "50%", background: c }} />)}
          </span>
          <div style={{ flex: 1, maxWidth: 320, margin: "0 auto", height: 24, borderRadius: 99, background: "#fff", border: "1px solid var(--border)", display: "flex", alignItems: "center", justifyContent: "center", gap: 6, fontSize: 11.5, color: "var(--muted)", fontWeight: 600 }}>
            <Icon.shield size={12} style={{ color: "var(--success)" }} /> speakpath.app/dashboard
          </div>
          <span style={{ width: 60 }} />
        </div>
        <iframe src={"SpeakPath.html?screen=" + screen} title={"desktop-" + screen} style={{ flex: 1, width: "100%", border: "none", display: "block" }} />
      </div>
    );
  }

  // ---------------------------------------------------------------- FUTURE TYPES
  function FuturePanel({ icon, skill, title, desc, children }) {
    const s = skills[skill];
    return (
      <div style={PAD}>
        <div style={{ display: "flex", alignItems: "center", gap: 11, marginBottom: 4 }}>
          <SkillIcon skill={skill} size={40} soft />
          <div>
            <div style={{ fontSize: 11, fontWeight: 800, color: `var(--${s.color}-ink)`, letterSpacing: ".04em", textTransform: "uppercase" }}>{title}</div>
            <div style={{ fontSize: 16, fontWeight: 800, color: "var(--ink)" }}>{s.label}</div>
          </div>
        </div>
        <p style={{ fontSize: 12.5, color: "var(--muted)", fontWeight: 600, lineHeight: 1.5, marginTop: 8 }}>{desc}</p>
        <div style={{ marginTop: 16 }}>{children}</div>
      </div>
    );
  }

  function FutureSpeaking() {
    return (
      <FuturePanel skill="speaking" title="SpeakingRolePlay" desc="Chat with an AI character. Tap the mic, speak your line, get instant coaching on fluency and tone.">
        <div className="sp-card" style={{ padding: 14, display: "flex", flexDirection: "column", gap: 9 }}>
          <div style={{ display: "flex", gap: 8, alignItems: "flex-end" }}>
            <CoachAvatar size={28} />
            <div style={{ background: "var(--canvas-2)", borderRadius: "4px 14px 14px 14px", padding: "8px 12px", fontSize: 12.5, fontWeight: 600, color: "var(--ink)" }}>Hi! Can you tell me about your weekend?</div>
          </div>
          <div style={{ alignSelf: "flex-end", background: "var(--writing-soft)", borderRadius: "14px 4px 14px 14px", padding: "8px 12px", fontSize: 12.5, fontWeight: 600, color: "var(--writing-ink)" }}>I went to the…</div>
          <div style={{ display: "flex", justifyContent: "center", marginTop: 4 }}>
            <span className="sp-icobox" style={{ width: 48, height: 48, borderRadius: "50%", background: "var(--grad-brand, #B45CF0)", color: "#fff", boxShadow: "var(--sh-glow)" }}><Icon.mic size={22} /></span>
          </div>
        </div>
      </FuturePanel>
    );
  }
  function FutureListening() {
    return (
      <FuturePanel skill="listening" title="ListeningComprehension" desc="Play a short workplace clip, answer question cards, then reveal the transcript to check.">
        <div className="sp-card" style={{ padding: 14, display: "flex", flexDirection: "column", gap: 11 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span className="sp-icobox" style={{ width: 38, height: 38, borderRadius: "50%", background: "var(--listening)", color: "#fff" }}><Icon.play size={17} /></span>
            <div style={{ flex: 1, display: "flex", gap: 3, alignItems: "center", height: 26 }}>
              {[10, 18, 8, 22, 14, 26, 12, 20, 9, 24, 13, 19, 7, 16].map((h, i) => <span key={i} style={{ flex: 1, height: h, borderRadius: 3, background: i < 6 ? "var(--listening)" : "var(--border-2)" }} />)}
            </div>
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            <span className="sp-chip" style={{ flex: 1, justifyContent: "center", fontSize: 12 }}>Tomorrow</span>
            <span className="sp-chip is-on" style={{ flex: 1, justifyContent: "center", fontSize: 12, background: "var(--listening-soft)", color: "var(--listening-ink)" }}>Friday</span>
          </div>
        </div>
      </FuturePanel>
    );
  }
  function FutureVocab() {
    return (
      <FuturePanel skill="vocabulary" title="VocabularyPractice" desc="Flip flashcards, match words to meanings, or fill the gap — spaced to help words stick.">
        <div style={{ display: "flex", gap: 11 }}>
          <div className="grad-brand" style={{ flex: 1, borderRadius: 16, minHeight: 92, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", color: "#fff", gap: 4 }}>
            <span style={{ fontSize: 17, fontWeight: 800 }}>deadline</span>
            <span style={{ fontSize: 11, opacity: .85, fontWeight: 600 }}>tap to flip</span>
          </div>
          <div className="sp-card" style={{ flex: 1, minHeight: 92, display: "flex", alignItems: "center", justifyContent: "center", padding: 12, textAlign: "center", fontSize: 12.5, fontWeight: 600, color: "var(--text)" }}>the date something is due</div>
        </div>
      </FuturePanel>
    );
  }
  function FuturePron() {
    return (
      <FuturePanel skill="pronunciation" title="PronunciationPractice" desc="Listen, repeat into the mic, and get a clarity score with the exact sounds to refine.">
        <div className="sp-card" style={{ padding: 14, display: "flex", flexDirection: "column", gap: 12 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span className="sp-icobox" style={{ width: 34, height: 34, borderRadius: 10, background: "var(--pronunciation-soft)", color: "var(--pronunciation)" }}><Icon.sound size={17} /></span>
            <span style={{ fontWeight: 800, color: "var(--ink)", fontSize: 14 }}>schedule</span>
            <span style={{ marginLeft: "auto", fontWeight: 800, color: "var(--pronunciation)", fontSize: 14 }}>92%</span>
          </div>
          <ProgressBar value={92} gradient="var(--pronunciation)" />
        </div>
      </FuturePanel>
    );
  }
  function FutureReading() {
    return (
      <FuturePanel skill="vocabulary" title="ReadingTask" desc="Read a real workplace document, then answer comprehension questions in context.">
        <div className="sp-card" style={{ padding: 14, display: "flex", flexDirection: "column", gap: 9 }}>
          {[100, 88, 94, 60].map((w, i) => <span key={i} style={{ height: 7, width: w + "%", borderRadius: 99, background: i === 3 ? "var(--vocabulary-soft)" : "var(--canvas-2)" }} />)}
          <div style={{ marginTop: 4, fontSize: 12, fontWeight: 700, color: "var(--ink)" }}>Q: When is the report due?</div>
        </div>
      </FuturePanel>
    );
  }

  // ---------------------------------------------------------------- ROOT
  function BrandCanvas() {
    return (
      <DesignCanvas>
        <DCSection id="brand" title="SpeakPath — Brand foundations" subtitle="Confidence · progress · communication · warmth">
          <DCArtboard id="logo" label="Logo & lockups" width={560} height={400}><LogoPanel /></DCArtboard>
          <DCArtboard id="colour" label="Colour system" width={620} height={420}><ColourPanel /></DCArtboard>
          <DCArtboard id="type" label="Typography" width={560} height={420}><TypePanel /></DCArtboard>
          <DCArtboard id="icons" label="Icons & motifs" width={520} height={400}><IconsPanel /></DCArtboard>
        </DCSection>

        <DCSection id="components" title="Component library" subtitle="The reusable building blocks of every screen">
          <DCArtboard id="badges" label="Badges, chips & pills" width={460} height={400}><BadgesPanel /></DCArtboard>
          <DCArtboard id="buttons" label="Buttons, progress & stats" width={460} height={360}><ButtonsPanel /></DCArtboard>
          <DCArtboard id="cards" label="Path & activity cards" width={500} height={360}><CardsPanel /></DCArtboard>
          <DCArtboard id="coach" label="Coach & score" width={440} height={460}><CoachPanel /></DCArtboard>
          <DCArtboard id="states" label="System states" width={480} height={320}><StatesPanel /></DCArtboard>
        </DCSection>

        <DCSection id="product" title="The product — live & interactive" subtitle="Real working screens. Click the ⤢ on any frame to open it fullscreen.">
          <DCArtboard id="m-dash" label="Mobile · Dashboard" width={392} height={812} style={{ borderRadius: 40 }}><PhoneFrame screen="home" /></DCArtboard>
          <DCArtboard id="m-path" label="Mobile · My Path" width={392} height={812} style={{ borderRadius: 40 }}><PhoneFrame screen="path" /></DCArtboard>
          <DCArtboard id="m-feed" label="Mobile · AI feedback" width={392} height={812} style={{ borderRadius: 40 }}><PhoneFrame screen="activity" stage="feedback" /></DCArtboard>
          <DCArtboard id="d-dash" label="Desktop · Dashboard" width={1180} height={760}><DesktopFrame screen="home" /></DCArtboard>
        </DCSection>

        <DCSection id="future" title="Future activity types" subtitle="The system is built so these slot in without re-architecting">
          <DCArtboard id="f-speak" label="SpeakingRolePlay" width={420} height={420}><FutureSpeaking /></DCArtboard>
          <DCArtboard id="f-listen" label="ListeningComprehension" width={420} height={360}><FutureListening /></DCArtboard>
          <DCArtboard id="f-vocab" label="VocabularyPractice" width={420} height={360}><FutureVocab /></DCArtboard>
          <DCArtboard id="f-pron" label="PronunciationPractice" width={420} height={320}><FuturePron /></DCArtboard>
          <DCArtboard id="f-read" label="ReadingTask" width={420} height={320}><FutureReading /></DCArtboard>
        </DCSection>
      </DesignCanvas>
    );
  }

  window.SP.BrandCanvas = BrandCanvas;
})();
