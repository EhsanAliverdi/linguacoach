// SpeakPath Admin — Students page
(function () {
  const { useState, useMemo } = React;
  const { AIcon, getAvatarColor, getInitials, cefrClass, lifecycleBadge } = window;
  const { students, studentActivities } = window.ADMIN_DATA;

  const PAGE_SIZE_OPTIONS = [5, 10, 25, 50];

  // ── SORT HELPER ───────────────────────────────────────────────
  function sortStudents(list, key, dir) {
    return [...list].sort((a, b) => {
      let av = a[key] ?? '', bv = b[key] ?? '';
      if (key === 'joined') { av = new Date(av); bv = new Date(bv); }
      if (av < bv) return dir === 'asc' ? -1 : 1;
      if (av > bv) return dir === 'asc' ? 1 : -1;
      return 0;
    });
  }

  // ── ACTION MENU ───────────────────────────────────────────────
  function ActionMenu({ student, onView }) {
    const [open, setOpen] = useState(false);
    return (
      <div style={{ position:'relative' }}>
        <button
          className="adm-btn adm-btn-ghost adm-btn-xs"
          onClick={e => { e.stopPropagation(); setOpen(v => !v); }}
          style={{ padding:'5px 8px' }}>
          <AIcon n="moreH" s={15}/>
        </button>
        {open && (
          <>
            <div style={{ position:'fixed', inset:0, zIndex:99 }} onClick={() => setOpen(false)}/>
            <div style={{
              position:'absolute', right:0, top:'calc(100% + 4px)', zIndex:100,
              background:'#fff', border:'1px solid #ECE9F5', borderRadius:10,
              boxShadow:'0 8px 24px rgba(60,48,140,.10)', minWidth:176, overflow:'hidden',
            }}>
              {[
                { label:'View profile',   icon:'eye',     action: () => { setOpen(false); onView(student); } },
                { label:'Edit student',   icon:'edit',    action: () => setOpen(false) },
                { label:'Reset password', icon:'key',     action: () => setOpen(false) },
                { label:'Reset data',     icon:'refresh', action: () => setOpen(false) },
                { label:'Archive',        icon:'archive', action: () => setOpen(false), danger: true },
              ].map(item => (
                <button key={item.label}
                  onClick={e => { e.stopPropagation(); item.action(); }}
                  style={{
                    display:'flex', alignItems:'center', gap:10, width:'100%', padding:'10px 14px',
                    fontSize:13.5, fontWeight:600, fontFamily:'inherit', cursor:'pointer',
                    background:'none', border:'none',
                    color: item.danger ? '#DC2626' : '#211B36',
                    borderTop: item.danger ? '1px solid #ECE9F5' : 'none',
                  }}>
                  <AIcon n={item.icon} s={14} c="currentColor"/>
                  {item.label}
                </button>
              ))}
            </div>
          </>
        )}
      </div>
    );
  }

  // ── SORT ICON ─────────────────────────────────────────────────
  function SortIcon({ active, dir }) {
    return (
      <span style={{ display:'inline-flex', flexDirection:'column', gap:1.5, marginLeft:5, verticalAlign:'middle', opacity: active ? 1 : 0.3 }}>
        <span style={{ width:0, height:0, borderLeft:'4px solid transparent', borderRight:'4px solid transparent',
          borderBottom:`4px solid ${active && dir === 'asc' ? '#5B4BE8' : '#8B85A0'}` }}/>
        <span style={{ width:0, height:0, borderLeft:'4px solid transparent', borderRight:'4px solid transparent',
          borderTop:`4px solid ${active && dir === 'desc' ? '#5B4BE8' : '#8B85A0'}` }}/>
      </span>
    );
  }

  // ── STUDENT LIST ──────────────────────────────────────────────
  function StudentList({ navigate }) {
    const [search, setSearch]           = useState('');
    const [showArchived, setShowArchived] = useState(false);
    const [selectedId, setSelectedId]   = useState(null);
    const [sortKey, setSortKey]         = useState('joined');
    const [sortDir, setSortDir]         = useState('desc');
    const [page, setPage]               = useState(1);
    const [pageSize, setPageSize]       = useState(10);
    const [lcFilter, setLcFilter]       = useState('All');

    const lifecycleOptions = ['All', ...Array.from(new Set(students.map(s => s.lifecycle)))];

    // Filter
    const filtered = useMemo(() => {
      return students.filter(s => {
        if (!showArchived && s.archived) return false;
        if (lcFilter !== 'All' && s.lifecycle !== lcFilter) return false;
        const q = search.toLowerCase();
        if (q && !s.name.toLowerCase().includes(q) && !s.email.toLowerCase().includes(q)) return false;
        return true;
      });
    }, [search, showArchived, lcFilter]);

    // Sort
    const sorted = useMemo(() => sortStudents(filtered, sortKey, sortDir), [filtered, sortKey, sortDir]);

    // Paginate
    const totalPages = Math.max(1, Math.ceil(sorted.length / pageSize));
    const safePage   = Math.min(page, totalPages);
    const pageItems  = sorted.slice((safePage - 1) * pageSize, safePage * pageSize);
    const firstRow   = sorted.length === 0 ? 0 : (safePage - 1) * pageSize + 1;
    const lastRow    = Math.min(safePage * pageSize, sorted.length);

    function handleSort(key) {
      if (sortKey === key) setSortDir(d => d === 'asc' ? 'desc' : 'asc');
      else { setSortKey(key); setSortDir('asc'); }
      setPage(1);
    }

    function handleSearch(val) { setSearch(val); setPage(1); }
    function handleLc(val)     { setLcFilter(val); setPage(1); }
    function handlePageSize(v) { setPageSize(Number(v)); setPage(1); }

    // Build page number array (max 7 buttons with ellipsis)
    function pageNums() {
      if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1);
      const pages = [];
      pages.push(1);
      if (safePage > 3) pages.push('…');
      for (let i = Math.max(2, safePage - 1); i <= Math.min(totalPages - 1, safePage + 1); i++) pages.push(i);
      if (safePage < totalPages - 2) pages.push('…');
      pages.push(totalPages);
      return pages;
    }

    const cols = [
      { key:'name',      label:'Student',    style:{ minWidth:220 } },
      { key:'lifecycle', label:'Lifecycle',  style:{ minWidth:160 } },
      { key:'onboarding',label:'Onboarding', style:{ minWidth:120 } },
      { key:'cefr',      label:'CEFR',       style:{ minWidth:80 } },
      { key:'streak',    label:'Streak',     style:{ minWidth:80, textAlign:'right' } },
      { key:'minutesWeek',label:'Mins / wk', style:{ minWidth:90, textAlign:'right' } },
      { key:'joined',    label:'Joined',     style:{ minWidth:120 } },
    ];

    if (selectedId) {
      const student = students.find(s => s.id === selectedId);
      return <StudentDetail student={student} onBack={() => setSelectedId(null)}/>;
    }

    return (
      <div>
        {/* Page header */}
        <div className="adm-page-header">
          <div>
            <h1 className="adm-page-h1">Students</h1>
            <p className="adm-page-sub">Manage pilot student accounts</p>
          </div>
          <button className="adm-btn adm-btn-indigo" onClick={() => navigate('create-student')}>
            <AIcon n="plus" s={14} c="#fff"/>
            Create student
          </button>
        </div>

        {/* Filter bar */}
        <div style={{ display:'flex', alignItems:'center', gap:10, marginBottom:16, flexWrap:'wrap' }}>
          {/* Search */}
          <div style={{ position:'relative', flex:'1 1 200px', maxWidth:280 }}>
            <span style={{ position:'absolute', left:10, top:'50%', transform:'translateY(-50%)', color:'#BDB8CC', pointerEvents:'none' }}>
              <AIcon n="search" s={14} c="currentColor"/>
            </span>
            <input className="adm-input" placeholder="Search name or email…"
              value={search} onChange={e => handleSearch(e.target.value)}
              style={{ paddingLeft:34 }}/>
          </div>

          {/* Lifecycle filter */}
          <select className="adm-select" value={lcFilter} onChange={e => handleLc(e.target.value)}
            style={{ width:'auto', minWidth:160 }}>
            {lifecycleOptions.map(o => <option key={o}>{o}</option>)}
          </select>

          {/* Archived toggle */}
          <label style={{ display:'flex', alignItems:'center', gap:7, cursor:'pointer', fontSize:13.5, fontWeight:600, color:'#4B4462', userSelect:'none' }}>
            <input type="checkbox" checked={showArchived} onChange={e => setShowArchived(e.target.checked)}
              style={{ width:15, height:15, accentColor:'#5B4BE8', cursor:'pointer', flexShrink:0 }}/>
            Show archived
          </label>

          {/* Spacer */}
          <div style={{ flex:1 }}/>

          {/* Rows per page */}
          <div style={{ display:'flex', alignItems:'center', gap:8, flexShrink:0 }}>
            <span style={{ fontSize:13, color:'#8B85A0', fontWeight:600, whiteSpace:'nowrap' }}>Rows per page</span>
            <select className="adm-select" value={pageSize} onChange={e => handlePageSize(e.target.value)}
              style={{ width:72 }}>
              {PAGE_SIZE_OPTIONS.map(n => <option key={n} value={n}>{n}</option>)}
            </select>
          </div>
        </div>

        {/* Table card */}
        <div className="adm-card" style={{ overflow:'hidden' }}>
          <div className="adm-table-wrap">
            <table className="adm-table" style={{ tableLayout:'auto' }}>
              <thead>
                <tr>
                  {cols.map(c => (
                    <th key={c.key} className="sort" style={c.style} onClick={() => handleSort(c.key)}>
                      {c.label}
                      <SortIcon active={sortKey === c.key} dir={sortDir}/>
                    </th>
                  ))}
                  <th style={{ width:56, textAlign:'center' }}></th>
                </tr>
              </thead>
              <tbody>
                {pageItems.length === 0 && (
                  <tr><td colSpan={8}>
                    <div className="adm-empty">
                      <div className="adm-empty-ico"><AIcon n="students" s={22} c="#BDB8CC"/></div>
                      <div className="adm-empty-title">No students found</div>
                      <div className="adm-empty-sub">Try adjusting your search or filters</div>
                    </div>
                  </td></tr>
                )}
                {pageItems.map((s, idx) => (
                  <tr key={s.id}
                    onClick={() => setSelectedId(s.id)}
                    style={{ background: idx % 2 === 1 ? '#FDFCFF' : '#fff' }}>

                    {/* Student */}
                    <td>
                      <div style={{ display:'flex', alignItems:'center', gap:10 }}>
                        <div style={{
                          width:32, height:32, borderRadius:'50%', flexShrink:0,
                          background: getAvatarColor(s.name), display:'grid', placeItems:'center',
                          fontSize:12, fontWeight:700, color:'#fff',
                        }}>
                          {getInitials(s.name, s.email)}
                        </div>
                        <div>
                          <div style={{ fontWeight:700, color:'#211B36', fontSize:13.5, lineHeight:1.2 }}>{s.name}</div>
                          <div style={{ display:'flex', alignItems:'center', gap:5, marginTop:1 }}>
                            <span style={{ fontSize:11.5, color:'#8B85A0', fontFamily:"'JetBrains Mono',monospace" }}>
                              {s.email}
                            </span>
                            <button style={{ border:'none', background:'none', cursor:'pointer', color:'#BDB8CC', padding:0, lineHeight:1, flexShrink:0 }}
                              onClick={e => { e.stopPropagation(); navigator.clipboard?.writeText(s.email); }}>
                              <AIcon n="copy" s={11} c="currentColor"/>
                            </button>
                          </div>
                        </div>
                      </div>
                    </td>

                    {/* Lifecycle */}
                    <td>
                      <span className={`adm-badge ${lifecycleBadge(s.lifecycle)}`}>{s.lifecycle}</span>
                    </td>

                    {/* Onboarding */}
                    <td>
                      <span className={`adm-badge ${s.onboarding === 'Complete' ? 'adm-badge-success' : 'adm-badge-warn'}`}>
                        {s.onboarding}
                      </span>
                    </td>

                    {/* CEFR */}
                    <td>
                      {s.cefr
                        ? <span className={`adm-cefr ${cefrClass(s.cefr)}`}>{s.cefr}</span>
                        : <span style={{ color:'#BDB8CC' }}>–</span>}
                    </td>

                    {/* Streak */}
                    <td style={{ textAlign:'right' }}>
                      {s.streak > 0
                        ? <span style={{ fontWeight:700, color:'#211B36', fontSize:13.5 }}>
                            {s.streak}
                            <span style={{ fontSize:11, color:'#F0982C', marginLeft:4 }}>🔥</span>
                          </span>
                        : <span style={{ color:'#BDB8CC' }}>0</span>}
                    </td>

                    {/* Mins/wk */}
                    <td style={{ textAlign:'right' }}>
                      <span style={{ fontWeight:600, color: s.minutesWeek > 0 ? '#211B36' : '#BDB8CC', fontSize:13.5 }}>
                        {s.minutesWeek > 0 ? s.minutesWeek : '–'}
                      </span>
                    </td>

                    {/* Joined */}
                    <td style={{ color:'#8B85A0', fontSize:13, whiteSpace:'nowrap' }}>
                      {new Date(s.joined).toLocaleDateString('en-US', { month:'short', day:'numeric', year:'numeric' })}
                    </td>

                    {/* Actions */}
                    <td style={{ textAlign:'center' }} onClick={e => e.stopPropagation()}>
                      <ActionMenu student={s} onView={() => setSelectedId(s.id)}/>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* ── PAGINATION ── */}
          <div style={{
            display:'flex', alignItems:'center', justifyContent:'space-between',
            padding:'12px 16px', borderTop:'1px solid #ECE9F5', gap:12, flexWrap:'wrap',
          }}>
            {/* Row info */}
            <span style={{ fontSize:13, color:'#8B85A0', fontWeight:600, whiteSpace:'nowrap' }}>
              {sorted.length === 0
                ? 'No results'
                : `Showing ${firstRow}–${lastRow} of ${sorted.length} student${sorted.length !== 1 ? 's' : ''}`}
            </span>

            {/* Page buttons */}
            <div style={{ display:'flex', alignItems:'center', gap:4 }}>
              {/* Prev */}
              <button
                className="adm-pag-btn"
                disabled={safePage === 1}
                onClick={() => setPage(p => p - 1)}
                style={{ padding:'0 10px', display:'flex', alignItems:'center', gap:4 }}>
                <AIcon n="arrowLeft" s={13} c="currentColor"/>
                Prev
              </button>

              {/* Page numbers */}
              {pageNums().map((p, i) =>
                p === '…'
                  ? <span key={`ellipsis-${i}`} style={{ padding:'0 4px', color:'#BDB8CC', fontSize:13, lineHeight:'30px' }}>…</span>
                  : <button key={p} className={`adm-pag-btn${safePage === p ? ' cur' : ''}`}
                      onClick={() => setPage(p)}
                      style={{ minWidth:32, padding:'0 6px' }}>
                      {p}
                    </button>
              )}

              {/* Next */}
              <button
                className="adm-pag-btn"
                disabled={safePage === totalPages}
                onClick={() => setPage(p => p + 1)}
                style={{ padding:'0 10px', display:'flex', alignItems:'center', gap:4 }}>
                Next
                <AIcon n="arrowRight" s={13} c="currentColor"/>
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ── STUDENT DETAIL ────────────────────────────────────────────
  function StudentDetail({ student: s, onBack }) {
    const [tab, setTab] = useState('overview');
    const activities = studentActivities[s.id] || [];

    return (
      <div>
        <button className="adm-btn adm-btn-ghost adm-btn-sm" onClick={onBack} style={{ marginBottom:20 }}>
          <AIcon n="arrowLeft" s={14}/>Back to Students
        </button>

        <div className="adm-detail-hero">
          <div className="adm-detail-ava" style={{ background: getAvatarColor(s.name) }}>
            {getInitials(s.name, s.email)}
          </div>
          <div style={{ flex:1, minWidth:0 }}>
            <div className="adm-detail-name">{s.name}</div>
            <div className="adm-detail-email">{s.email}</div>
            <div className="adm-detail-badges">
              <span className={`adm-badge ${lifecycleBadge(s.lifecycle)}`}>{s.lifecycle}</span>
              {s.cefr && <span className={`adm-cefr ${cefrClass(s.cefr)}`}>{s.cefr}</span>}
              <span className={`adm-badge ${s.onboarding === 'Complete' ? 'adm-badge-success' : 'adm-badge-warn'}`}>
                {s.onboarding}
              </span>
            </div>
          </div>
          <div className="adm-detail-acts">
            <button className="adm-btn adm-btn-ghost adm-btn-sm"><AIcon n="edit" s={14}/>Edit</button>
            <button className="adm-btn adm-btn-ghost adm-btn-sm"><AIcon n="key" s={14}/>Reset password</button>
            <button className="adm-btn adm-btn-danger adm-btn-sm"><AIcon n="archive" s={14}/>Archive</button>
          </div>
        </div>

        <div className="adm-tabs">
          {['overview','activity','settings'].map(t => (
            <button key={t} className={`adm-tab${tab === t ? ' active' : ''}`} onClick={() => setTab(t)}>
              {t.charAt(0).toUpperCase() + t.slice(1)}
            </button>
          ))}
        </div>

        {tab === 'overview' && (
          <div className="adm-g2">
            <div className="adm-col">
              <div className="adm-card adm-card-p">
                <div className="adm-card-title" style={{ marginBottom:16 }}>Profile</div>
                {[
                  ['Email',        s.email],
                  ['Joined',       s.joined],
                  ['Lifecycle',    s.lifecycle],
                  ['Onboarding',   s.onboarding],
                  ['Role / profile', s.profile || '—'],
                ].map(([l, v]) => (
                  <div key={l} className="adm-info-item">
                    <span className="adm-info-lbl">{l}</span>
                    <span className="adm-info-val">{v}</span>
                  </div>
                ))}
              </div>
            </div>
            <div className="adm-col">
              <div className="adm-card">
                <div className="adm-g3" style={{ padding:20, gap:0 }}>
                  {[
                    [s.streak,         'Day streak'],
                    [s.minutesWeek,    'Mins this week'],
                    [s.activitiesDone, 'Activities done'],
                  ].map(([v, l], i) => (
                    <div key={l} className="adm-stat"
                      style={{ borderRight: i < 2 ? '1px solid #ECE9F5' : 'none' }}>
                      <div className="adm-stat-val">{v}</div>
                      <div className="adm-stat-lbl">{l}</div>
                    </div>
                  ))}
                </div>
              </div>
              <div className="adm-card adm-card-p">
                <div className="adm-card-title" style={{ marginBottom:12 }}>CEFR Level</div>
                <div style={{ display:'flex', alignItems:'center', gap:14 }}>
                  <div style={{ width:52, height:52, borderRadius:13, background:'#EDEBFF',
                    display:'grid', placeItems:'center', fontSize:18, fontWeight:800, color:'#3A2EA8' }}>
                    {s.cefr || '—'}
                  </div>
                  <div>
                    <div style={{ fontSize:14, fontWeight:700, color:'#211B36' }}>
                      {s.cefr === 'B2' ? 'Upper Intermediate'
                        : s.cefr === 'B1' ? 'Intermediate'
                        : s.cefr === 'A2' ? 'Elementary'
                        : 'Not assessed'}
                    </div>
                    <div style={{ fontSize:12.5, color:'#8B85A0' }}>Assessed on {s.joined}</div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}

        {tab === 'activity' && (
          <div className="adm-card">
            <div className="adm-card-p" style={{ paddingBottom:0 }}>
              <div className="adm-card-title">Activity history</div>
            </div>
            {activities.length === 0 ? (
              <div className="adm-empty">
                <div className="adm-empty-ico"><AIcon n="activity2" s={22} c="#BDB8CC"/></div>
                <div className="adm-empty-title">No activities yet</div>
              </div>
            ) : (
              <div className="adm-table-wrap">
                <table className="adm-table">
                  <thead>
                    <tr>
                      <th>Activity</th>
                      <th>Skill</th>
                      <th>Score</th>
                      <th>Duration</th>
                      <th>Date</th>
                    </tr>
                  </thead>
                  <tbody>
                    {activities.map(a => (
                      <tr key={a.id}>
                        <td style={{ fontWeight:600, color:'#211B36' }}>{a.title}</td>
                        <td><span className="adm-badge adm-badge-indigo" style={{ textTransform:'capitalize' }}>{a.skill}</span></td>
                        <td><span style={{ fontWeight:700, color: a.score >= 85 ? '#13B07C' : a.score >= 70 ? '#F0982C' : '#EF4444' }}>{a.score}/100</span></td>
                        <td style={{ color:'#8B85A0', fontSize:13 }}>{a.minutes} min</td>
                        <td style={{ color:'#8B85A0', fontSize:13 }}>{a.date}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {tab === 'settings' && (
          <div className="adm-col">
            <div className="adm-card adm-card-p">
              <div className="adm-card-title" style={{ marginBottom:20 }}>Edit student</div>
              <div className="adm-form-row">
                <div className="adm-form-group">
                  <label className="adm-form-lbl">Display name</label>
                  <input className="adm-input" defaultValue={s.name}/>
                </div>
                <div className="adm-form-group">
                  <label className="adm-form-lbl">Email</label>
                  <input className="adm-input" defaultValue={s.email}/>
                </div>
              </div>
              <div className="adm-form-row" style={{ marginBottom:20 }}>
                <div className="adm-form-group">
                  <label className="adm-form-lbl">Lifecycle stage</label>
                  <select className="adm-select" defaultValue={s.lifecycle} style={{ width:'100%' }}>
                    {['Active learning','In lesson','Course ready','Placement required','Onboarding required'].map(o => (
                      <option key={o}>{o}</option>
                    ))}
                  </select>
                </div>
                <div className="adm-form-group">
                  <label className="adm-form-lbl">CEFR level</label>
                  <select className="adm-select" defaultValue={s.cefr || ''} style={{ width:'100%' }}>
                    <option value="">Not set</option>
                    {['A1','A2','B1','B2','C1','C2'].map(o => <option key={o}>{o}</option>)}
                  </select>
                </div>
              </div>
              <button className="adm-btn adm-btn-indigo">Save changes</button>
            </div>
            <div className="adm-danger-zone">
              <div className="adm-danger-title">Danger zone</div>
              {[
                { label:'Reset password',  sub:'Send a password reset email to the student', action:'Send reset email', icon:'key' },
                { label:'Reset data',      sub:'Delete all activity history and scores',      action:'Reset data',      icon:'refresh' },
                { label:'Archive student', sub:'Hide this student and revoke app access',     action:'Archive',         icon:'archive' },
              ].map(item => (
                <div key={item.label} className="adm-danger-row">
                  <div>
                    <div className="adm-danger-item-label">{item.label}</div>
                    <div className="adm-danger-item-sub">{item.sub}</div>
                  </div>
                  <button className="adm-btn adm-btn-danger adm-btn-sm" style={{ flexShrink:0, marginLeft:16 }}>
                    <AIcon n={item.icon} s={13}/>
                    {item.action}
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    );
  }

  function AdminStudents({ navigate }) {
    return <StudentList navigate={navigate}/>;
  }

  window.AdminStudents = AdminStudents;
})();
