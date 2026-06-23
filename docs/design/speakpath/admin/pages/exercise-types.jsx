// SpeakPath Admin — Exercise Types page
(function () {
  const { useState } = React;
  const { AIcon } = window;
  const { exerciseTypes: initialTypes } = window.ADMIN_DATA;

  const TYPE_ICONS = {
    WritingScenario: { icon:'pen', bg:'#EDEBFF', ic:'#5B4BE8' },
    SpeakingPrompt:  { icon:'mic', bg:'#FFEAE4', ic:'#FF7A59' },
    ListeningQuiz:   { icon:'headphones', bg:'#F2E9FF', ic:'#B45CF0' },
    GrammarDrill:    { icon:'code2', bg:'#E0F6EE', ic:'#13B07C' },
    VocabFlashcard:  { icon:'zap', bg:'#FFF1DC', ic:'#F0982C' },
  };

  function Toggle({ on, onChange }) {
    return (
      <button className={`adm-toggle ${on ? 'on' : 'off'}`} onClick={onChange}/>
    );
  }

  function AdminExerciseTypes() {
    const [types, setTypes] = useState(initialTypes.map(t => ({ ...t })));

    function toggleType(id) {
      setTypes(prev => prev.map(t => t.id === id ? { ...t, enabled: !t.enabled } : t));
    }

    return (
      <div>
        <div className="adm-page-header">
          <div>
            <h1 className="adm-page-h1">Exercise Types</h1>
            <p className="adm-page-sub">Manage available activity formats and their settings</p>
          </div>
          <button className="adm-btn adm-btn-primary">
            <AIcon n="plus" s={15} c="#fff"/>
            Add type
          </button>
        </div>

        <div className="adm-col">
          {types.map(t => {
            const meta = TYPE_ICONS[t.name] || { icon:'box', bg:'#F6F4FB', ic:'#8B85A0' };
            return (
              <div key={t.id} className="adm-card adm-card-p" style={{ opacity: t.enabled ? 1 : 0.6 }}>
                <div style={{ display:'flex', alignItems:'center', gap:16 }}>
                  <div style={{ width:44, height:44, borderRadius:12, background:meta.bg, display:'grid', placeItems:'center', flexShrink:0 }}>
                    <AIcon n={meta.icon} s={20} c={meta.ic} w={2}/>
                  </div>
                  <div style={{ flex:1, minWidth:0 }}>
                    <div style={{ display:'flex', alignItems:'center', gap:10, marginBottom:3 }}>
                      <span style={{ fontSize:14.5, fontWeight:800, color:'#211B36' }}>{t.name}</span>
                      {t.comingSoon && (
                        <span className="adm-badge adm-badge-muted">Coming soon</span>
                      )}
                      {!t.comingSoon && (
                        <span className="adm-badge adm-badge-indigo">{t.count} exercises</span>
                      )}
                    </div>
                    <div style={{ fontSize:13.5, color:'#4B4462' }}>{t.description}</div>
                  </div>
                  <div style={{ display:'flex', alignItems:'center', gap:12, flexShrink:0 }}>
                    {!t.comingSoon && (
                      <>
                        <button className="adm-btn adm-btn-ghost adm-btn-sm">
                          <AIcon n="edit" s={14}/>
                          Configure
                        </button>
                        <Toggle on={t.enabled} onChange={() => toggleType(t.id)}/>
                      </>
                    )}
                    {t.comingSoon && (
                      <span style={{ fontSize:13, color:'#BDB8CC', fontWeight:600 }}>Not yet available</span>
                    )}
                  </div>
                </div>

                {!t.comingSoon && t.enabled && (
                  <>
                    <div className="adm-hr" style={{ margin:'16px 0' }}/>
                    <div style={{ display:'grid', gridTemplateColumns:'repeat(3,1fr)', gap:16 }}>
                      {[
                        { label:'Total exercises', val: t.count },
                        { label:'Avg completion time', val: t.name === 'WritingScenario' ? '8 min' : t.name === 'SpeakingPrompt' ? '12 min' : '7 min' },
                        { label:'Avg score', val: t.name === 'WritingScenario' ? '84/100' : t.name === 'SpeakingPrompt' ? '76/100' : '88/100' },
                      ].map(item => (
                        <div key={item.label}>
                          <div style={{ fontSize:11.5, fontWeight:700, color:'#8B85A0', textTransform:'uppercase', letterSpacing:'.06em', marginBottom:4 }}>{item.label}</div>
                          <div style={{ fontSize:18, fontWeight:800, color:'#211B36', letterSpacing:'-.02em' }}>{item.val}</div>
                        </div>
                      ))}
                    </div>
                  </>
                )}
              </div>
            );
          })}
        </div>
      </div>
    );
  }

  window.AdminExerciseTypes = AdminExerciseTypes;
})();
