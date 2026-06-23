// SpeakPath Admin — Curriculum page
(function () {
  const { AIcon } = window;

  const TRACKS = [
    { id: 'c1', name: 'Professional Communication', desc: 'Workplace emails, meetings, presentations and negotiations.', exercises: 24, enabled: true, icon: 'mail', bg:'#EDEBFF', ic:'#5B4BE8' },
    { id: 'c2', name: 'Career Vocabulary', desc: 'Industry-specific vocabulary sets for tech, finance, healthcare and marketing.', exercises: 18, enabled: true, icon: 'curriculum', bg:'#FFF1DC', ic:'#F0982C' },
    { id: 'c3', name: 'Formal Writing', desc: 'Reports, proposals, cover letters and formal correspondence.', exercises: 12, enabled: true, icon: 'pen', bg:'#F2E9FF', ic:'#B45CF0' },
    { id: 'c4', name: 'Meetings & Speaking', desc: 'Standups, presentations, negotiations and telephone English.', exercises: 8, enabled: false, icon: 'mic', bg:'#FFEAE4', ic:'#FF7A59', soon: true },
    { id: 'c5', name: 'Listening Comprehension', desc: 'Audio clips from real workplace scenarios with comprehension tasks.', exercises: 0, enabled: false, icon: 'headphones', bg:'#E0F6EE', ic:'#13B07C', soon: true },
  ];

  function AdminCurriculum() {
    return (
      <div>
        <div className="adm-page-header">
          <div>
            <h1 className="adm-page-h1">Curriculum</h1>
            <p className="adm-page-sub">Manage learning tracks, vocabulary sets and content</p>
          </div>
          <button className="adm-btn adm-btn-primary">
            <AIcon n="plus" s={15} c="#fff"/>
            New track
          </button>
        </div>

        <div className="adm-col">
          {TRACKS.map(t => (
            <div key={t.id} className="adm-card adm-card-p" style={{ opacity: t.enabled ? 1 : 0.65 }}>
              <div style={{ display:'flex', alignItems:'center', gap:16 }}>
                <div style={{ width:46, height:46, borderRadius:12, background:t.bg, display:'grid', placeItems:'center', flexShrink:0 }}>
                  <AIcon n={t.icon} s={20} c={t.ic} w={2}/>
                </div>
                <div style={{ flex:1, minWidth:0 }}>
                  <div style={{ display:'flex', alignItems:'center', gap:8, marginBottom:3 }}>
                    <span style={{ fontSize:15, fontWeight:800, color:'#211B36' }}>{t.name}</span>
                    {t.soon && <span className="adm-badge adm-badge-muted">Coming soon</span>}
                    {!t.soon && <span className="adm-badge adm-badge-indigo">{t.exercises} exercises</span>}
                  </div>
                  <div style={{ fontSize:13.5, color:'#4B4462' }}>{t.desc}</div>
                </div>
                {!t.soon && (
                  <div style={{ display:'flex', gap:8, flexShrink:0 }}>
                    <button className="adm-btn adm-btn-ghost adm-btn-sm">
                      <AIcon n="edit" s={14}/>Manage
                    </button>
                  </div>
                )}
                {t.soon && (
                  <span style={{ fontSize:13, color:'#BDB8CC', fontWeight:600, flexShrink:0 }}>Not yet available</span>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
    );
  }

  window.AdminCurriculum = AdminCurriculum;
})();
