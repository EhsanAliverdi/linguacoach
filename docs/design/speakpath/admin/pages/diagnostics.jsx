// SpeakPath Admin — Diagnostics page (system status + events)
(function () {
  const { useState, useEffect, useRef } = React;
  const { AIcon } = window;
  const { diagLogs } = window.ADMIN_DATA;

  const SYSTEM_STATUS = [
    { label:'ENVIRONMENT',   value:'Testing',          dot:'grey' },
    { label:'VERSION',       value:'1.0.0',            dot:'grey' },
    { label:'UPTIME',        value:'2h 14m',           dot:'blue' },
    { label:'LOG LEVEL',     value:'Information',      dot:'grey' },
    { label:'DATABASE',      value:'Reachable',        dot:'green' },
    { label:'AI PROVIDER',   value:'OpenAI',           dot:'green' },
    { label:'EVENT BUFFER',  value:'42 events',        dot:'green' },
    { label:'SERVER TIME',   value:'23 Jun 14:22:08',  dot:'grey' },
  ];

  const DOT_COLOR = { green:'#13B07C', blue:'#5B4BE8', grey:'#BDB8CC', red:'#EF4444' };

  function StatusCard({ label, value, dot }) {
    return (
      <div className="adm-diag-card">
        <div className="adm-diag-label">
          {label}
          <span style={{ width:8, height:8, borderRadius:'50%', background:DOT_COLOR[dot], display:'inline-block', flexShrink:0 }}/>
        </div>
        <div className="adm-diag-val">{value}</div>
      </div>
    );
  }

  // ── SPINNER ───────────────────────────────────────────────────
  function Spinner() {
    return (
      <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap:12, padding:'48px 0' }}>
        <svg width="32" height="32" viewBox="0 0 32 32" style={{ animation:'adm-spin 0.8s linear infinite' }}>
          <style>{`@keyframes adm-spin{to{transform:rotate(360deg)}}`}</style>
          <circle cx="16" cy="16" r="12" fill="none" stroke="#ECE9F5" strokeWidth="3"/>
          <path d="M 16 4 A 12 12 0 0 1 28 16" fill="none" stroke="#5B4BE8" strokeWidth="3" strokeLinecap="round"/>
        </svg>
        <span style={{ fontSize:14, color:'#8B85A0' }}>Loading diagnostic events</span>
      </div>
    );
  }

  function AdminDiagnostics() {
    const [loading, setLoading] = useState(true);
    const [autoRefresh, setAutoRefresh] = useState(false);
    const [levelFilter, setLevelFilter] = useState('All levels');
    const [category, setCategory] = useState('');
    const [correlationId, setCorrelationId] = useState('');
    const [search, setSearch] = useState('');
    const [limit, setLimit] = useState('100');
    const intervalRef = useRef(null);

    useEffect(() => {
      const t = setTimeout(() => setLoading(false), 1200);
      return () => clearTimeout(t);
    }, []);

    useEffect(() => {
      if (autoRefresh) {
        intervalRef.current = setInterval(() => {/* refresh */}, 5000);
      } else {
        clearInterval(intervalRef.current);
      }
      return () => clearInterval(intervalRef.current);
    }, [autoRefresh]);

    const visibleLogs = loading ? [] : diagLogs.filter(l => {
      if (levelFilter === 'ERROR' && !l.cls.includes('error')) return false;
      if (levelFilter === 'WARN' && !l.cls.includes('warn')) return false;
      if (levelFilter === 'INFO' && !l.cls.includes('info')) return false;
      if (search && !l.msg.toLowerCase().includes(search.toLowerCase())) return false;
      return true;
    }).slice(0, parseInt(limit) || 100);

    return (
      <div>
        <div className="adm-page-header">
          <div>
            <h1 className="adm-page-h1">Diagnostics</h1>
            <p className="adm-page-sub">System status, recent log events, and correlation ID lookup.</p>
          </div>
        </div>

        {/* System status section */}
        <div style={{
          border:'1.5px solid var(--indigo)', borderRadius:14, padding:'20px',
          marginBottom:20, background:'#fff',
        }}>
          <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:20 }}>
            <span style={{ fontSize:15, fontWeight:700, color:'#211B36' }}>System status</span>
            <button className="adm-btn adm-btn-ghost adm-btn-sm">Refresh</button>
          </div>
          <div style={{ display:'grid', gridTemplateColumns:'repeat(4,1fr)', gap:12 }}>
            {SYSTEM_STATUS.map(s => <StatusCard key={s.label} {...s}/>)}
          </div>
        </div>

        {/* Recent events section */}
        <div style={{
          border:'1.5px solid var(--indigo)', borderRadius:14, padding:'20px',
          background:'#fff',
        }}>
          <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:20 }}>
            <span style={{ fontSize:15, fontWeight:700, color:'#211B36' }}>Recent events</span>
            <div style={{ display:'flex', gap:8 }}>
              <button
                className={`adm-btn adm-btn-sm ${autoRefresh ? 'adm-btn-indigo' : 'adm-btn-ghost'}`}
                onClick={() => setAutoRefresh(v => !v)}>
                Auto-refresh
              </button>
              <button className="adm-btn adm-btn-ghost adm-btn-sm" onClick={() => setLoading(true) || setTimeout(() => setLoading(false), 800)}>
                Refresh
              </button>
            </div>
          </div>

          {/* Filters */}
          <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr 1fr 1fr', gap:12, marginBottom:12 }}>
            <div>
              <div style={{ fontSize:12, fontWeight:700, color:'#5B4BE8', marginBottom:5 }}>Level</div>
              <select className="adm-select" style={{ width:'100%' }} value={levelFilter} onChange={e => setLevelFilter(e.target.value)}>
                {['All levels','INFO','WARN','ERROR'].map(o => <option key={o}>{o}</option>)}
              </select>
            </div>
            <div>
              <div style={{ fontSize:12, fontWeight:700, color:'#5B4BE8', marginBottom:5 }}>Category</div>
              <input className="adm-input" placeholder="e.g. Activity" value={category} onChange={e => setCategory(e.target.value)}/>
            </div>
            <div>
              <div style={{ fontSize:12, fontWeight:700, color:'#5B4BE8', marginBottom:5 }}>Correlation ID</div>
              <input className="adm-input" placeholder="e.g. abc123ef0012" value={correlationId} onChange={e => setCorrelationId(e.target.value)}/>
            </div>
            <div>
              <div style={{ fontSize:12, fontWeight:700, color:'#5B4BE8', marginBottom:5 }}>Search</div>
              <input className="adm-input" placeholder="Search messages" value={search} onChange={e => setSearch(e.target.value)}/>
            </div>
          </div>
          <div style={{ marginBottom:20 }}>
            <div style={{ fontSize:12, fontWeight:700, color:'#5B4BE8', marginBottom:5 }}>Limit</div>
            <select className="adm-select" style={{ width:160 }} value={limit} onChange={e => setLimit(e.target.value)}>
              {['25','50','100','250','500'].map(o => <option key={o}>{o}</option>)}
            </select>
          </div>

          {/* Log area */}
          {loading ? <Spinner/> : (
            visibleLogs.length === 0 ? (
              <div className="adm-empty">
                <div className="adm-empty-title">No events match</div>
                <div className="adm-empty-sub">Adjust your filters or wait for new events.</div>
              </div>
            ) : (
              <div style={{ background:'#1E1B3A', borderRadius:10, overflow:'hidden' }}>
                {visibleLogs.map(l => (
                  <div key={l.id} className={`adm-log-row ${l.cls}`}>
                    <span className="adm-log-time">{l.time}</span>
                    <span className="adm-log-level">{l.level}</span>
                    <span className="adm-log-msg">{l.msg}</span>
                  </div>
                ))}
              </div>
            )
          )}
        </div>
      </div>
    );
  }

  window.AdminDiagnostics = AdminDiagnostics;
})();
