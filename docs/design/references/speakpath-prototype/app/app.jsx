// SpeakPath app shell: sidebar (desktop) + bottom nav (mobile), routing.
(function () {
  const { Icon, data, Logo, LogoMark, Dashboard, MyPath, Progress, Profile, Activity } = window.SP;
  const { user } = data;

  const NAV = [
    { key: "home", label: "Home", icon: "home" },
    { key: "path", label: "My Path", icon: "path" },
    { key: "activity", label: "Practice", icon: "pen", center: true },
    { key: "progress", label: "Progress", icon: "chart" },
    { key: "profile", label: "Profile", icon: "user" },
  ];

  function Sidebar({ route, go }) {
    return (
      <aside className="sp-side">
        <div className="sp-sidebrand"><Logo size={32} /></div>
        <nav className="sp-col" style={{ gap: 4, flex: 1 }}>
          {NAV.map((n) => {
            const Ico = Icon[n.icon];
            const active = route === n.key;
            return (
              <button key={n.key} className={"sp-sidelink" + (active ? " is-active" : "")} onClick={() => go(n.key)}>
                <Ico size={20} style={{ color: active ? "var(--indigo)" : "var(--muted)" }} />
                {n.label === "Home" ? "Dashboard" : n.label === "Activity" ? "Practice" : n.label}
              </button>
            );
          })}
        </nav>
        <div className="sp-card sp-card-pad" style={{ background: "var(--grad-brand-soft)", border: "1px solid #EADBFF" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 8 }}>
            <Icon.flame size={18} style={{ color: "var(--speaking)" }} />
            <span style={{ fontWeight: 800, fontSize: 14, color: "var(--ink)" }}>{user.streak}-day streak</span>
          </div>
          <p style={{ fontSize: 12, color: "var(--text)", fontWeight: 600, lineHeight: 1.45 }}>You've practised 6 days running. One more for a new record!</p>
        </div>
      </aside>
    );
  }

  function BottomNav({ route, go }) {
    return (
      <nav className="sp-bottomnav">
        {NAV.map((n) => {
          const Ico = Icon[n.icon];
          const active = route === n.key;
          if (n.center) {
            return (
              <button key={n.key} onClick={() => go(n.key)} style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 4, flex: 1, marginTop: -22 }}>
                <span className="sp-icobox" style={{ width: 52, height: 52, borderRadius: 18, background: "var(--grad-brand)", color: "#fff", boxShadow: "var(--sh-glow)", border: "3px solid var(--surface)" }}><Ico size={23} /></span>
                <span style={{ fontSize: 10.5, fontWeight: 800, color: active ? "var(--indigo)" : "var(--muted)" }}>{n.label}</span>
              </button>
            );
          }
          return (
            <button key={n.key} className={"sp-navbtn" + (active ? " is-active" : "")} onClick={() => go(n.key)}>
              <Ico size={22} /><span>{n.label}</span>
            </button>
          );
        })}
      </nav>
    );
  }

  const GREET = {
    home: { sm: "Good afternoon 👋", lg: `Hi, ${user.name}` },
    path: { sm: "Your personalised journey", lg: "My Path" },
    activity: { sm: "Writing practice", lg: "Let's write" },
    progress: { sm: "Keep up the great work", lg: "Progress" },
    profile: { sm: "Account & settings", lg: "Profile" },
  };

  function TopBar({ route, go }) {
    const g = GREET[route] || GREET.home;
    return (
      <header className="sp-topbar">
        <div style={{ display: "flex", alignItems: "center", gap: 11 }}>
          <div style={{ display: "none" }} className="sp-mobile-logo"><LogoMark size={36} /></div>
          <div>
            <div className="sp-greet-sm">{g.sm}</div>
            <div className="sp-greet-lg">{g.lg}</div>
          </div>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
          <span className="sp-pill" style={{ background: "var(--surface)", color: "var(--speaking)", border: "1px solid var(--border)", boxShadow: "var(--sh-xs)", padding: "7px 12px" }}>
            <Icon.flame size={15} /> {user.streak}
          </span>
          <button onClick={() => go("profile")} className="sp-icobox" style={{ width: 40, height: 40, borderRadius: "50%", background: "var(--grad-brand)", color: "#fff", fontWeight: 800, fontSize: 16, cursor: "pointer", boxShadow: "var(--sh-sm)" }}>{user.name[0]}</button>
        </div>
      </header>
    );
  }

  function App() {
    const params = new URLSearchParams(location.search);
    const forced = params.get("screen");
    const [route, setRoute] = React.useState(() => {
      if (forced) return forced;
      try { return localStorage.getItem("sp_route") || "home"; } catch (e) { return "home"; }
    });
    const go = (r) => {
      setRoute(r);
      if (!forced) { try { localStorage.setItem("sp_route", r); } catch (e) {} }
      const el = document.querySelector(".sp-content"); if (el) el.scrollTop = 0;
    };

    let Screen;
    if (route === "home") Screen = <Dashboard go={go} />;
    else if (route === "path") Screen = <MyPath go={go} />;
    else if (route === "activity") Screen = <Activity go={go} />;
    else if (route === "progress") Screen = <Progress go={go} />;
    else Screen = <Profile go={go} />;

    return (
      <div className="sp-app">
        <div className="sp-shell">
          <Sidebar route={route} go={go} />
          <div className="sp-main">
            <TopBar route={route} go={go} />
            <main className="sp-content">{Screen}</main>
          </div>
        </div>
        <BottomNav route={route} go={go} />
      </div>
    );
  }

  Object.assign(window.SP, { App });
})();
