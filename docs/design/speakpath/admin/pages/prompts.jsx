// SpeakPath Admin — Prompts page
(function () {
  const { useState } = React;
  const { AIcon } = window;
  const { prompts } = window.ADMIN_DATA;

  const TABS = ['All','Writing','Speaking','Feedback','Assessment'];

  function StatusBadge({ status }) {
    return (
      <span className={`adm-badge ${status === 'Active' ? 'adm-badge-success' : 'adm-badge-muted'}`}>
        {status}
      </span>
    );
  }

  function TypeBadge({ type }) {
    const map = {
      Writing: 'adm-badge-indigo',
      Speaking: 'adm-badge-coral',
      Feedback: 'adm-badge-success',
      Assessment: 'adm-badge-magenta',
    };
    return <span className={`adm-badge ${map[type] || 'adm-badge-muted'}`}>{type}</span>;
  }

  function PromptModal({ prompt, onClose }) {
    return (
      <>
        <div style={{ position:'fixed',inset:0,zIndex:199,background:'rgba(33,27,54,.35)',backdropFilter:'blur(4px)' }} onClick={onClose}/>
        <div style={{
          position:'fixed',top:'50%',left:'50%',transform:'translate(-50%,-50%)',
          zIndex:200,width:'min(640px,92vw)',background:'#fff',borderRadius:16,
          boxShadow:'0 24px 64px rgba(33,27,54,.22)',overflow:'hidden',
        }}>
          <div style={{ padding:'24px 24px 0', display:'flex', alignItems:'flex-start', justifyContent:'space-between' }}>
            <div>
              <div style={{ fontSize:17, fontWeight:800, color:'#211B36', marginBottom:4 }}>{prompt.name}</div>
              <div style={{ display:'flex', gap:8 }}>
                <TypeBadge type={prompt.type}/>
                <StatusBadge status={prompt.status}/>
              </div>
            </div>
            <button className="adm-btn adm-btn-ghost adm-btn-sm" onClick={onClose} style={{ padding:'6px 8px' }}>
              <AIcon n="x" s={16}/>
            </button>
          </div>
          <div style={{ padding:'20px 24px' }}>
            <div className="adm-form-lbl" style={{ marginBottom:8 }}>Prompt template</div>
            <textarea className="adm-textarea" rows={8} defaultValue={prompt.preview + '\n\nStudent profile: {{profile}}\n\nSkill focus: {{skill}}\n\nScenario: {{scenario}}\n\nTarget phrases: {{phrases}}'}/>
          </div>
          <div style={{ padding:'0 24px 20px', display:'flex', justifyContent:'flex-end', gap:8 }}>
            <button className="adm-btn adm-btn-ghost" onClick={onClose}>Cancel</button>
            <button className="adm-btn adm-btn-indigo">Save changes</button>
          </div>
        </div>
      </>
    );
  }

  function AdminPrompts() {
    const [activeTab, setActiveTab] = useState('All');
    const [editPrompt, setEditPrompt] = useState(null);
    const [search, setSearch] = useState('');

    const filtered = prompts.filter(p => {
      if (activeTab !== 'All' && p.type !== activeTab) return false;
      if (search && !p.name.toLowerCase().includes(search.toLowerCase())) return false;
      return true;
    });

    return (
      <div>
        <div className="adm-page-header">
          <div>
            <h1 className="adm-page-h1">Prompts</h1>
            <p className="adm-page-sub">Manage AI prompt templates · {prompts.length} templates</p>
          </div>
          <button className="adm-btn adm-btn-primary">
            <AIcon n="plus" s={15} c="#fff"/>
            New prompt
          </button>
        </div>

        <div style={{ display:'flex', alignItems:'center', gap:12, marginBottom:20, flexWrap:'wrap' }}>
          <div className="adm-tabs" style={{ marginBottom:0, borderBottom:'none', flex:1 }}>
            {TABS.map(t => (
              <button key={t} className={`adm-tab${activeTab === t ? ' active' : ''}`}
                onClick={() => setActiveTab(t)} style={{ borderBottom:'none' }}>
                {t}
              </button>
            ))}
          </div>
          <div className="adm-search-wrap" style={{ maxWidth:220 }}>
            <span className="adm-search-ico"><AIcon n="search" s={14}/></span>
            <input className="adm-input adm-search-input" placeholder="Search prompts…"
              value={search} onChange={e => setSearch(e.target.value)}/>
          </div>
        </div>

        <div className="adm-card">
          {filtered.length === 0 ? (
            <div className="adm-empty">
              <div className="adm-empty-ico"><AIcon n="prompts" s={22} c="#BDB8CC"/></div>
              <div className="adm-empty-title">No prompts found</div>
              <div className="adm-empty-sub">Try a different filter or create a new prompt.</div>
            </div>
          ) : filtered.map(p => (
            <div key={p.id} className="adm-prompt-row" onClick={() => setEditPrompt(p)}>
              <div style={{ flex:1, minWidth:0 }}>
                <div style={{ display:'flex', alignItems:'center', gap:8, marginBottom:4 }}>
                  <span className="adm-prompt-name">{p.name}</span>
                  <TypeBadge type={p.type}/>
                  <StatusBadge status={p.status}/>
                </div>
                <div className="adm-prompt-preview">{p.preview}</div>
                <div className="adm-prompt-meta">Updated {p.updated}</div>
              </div>
              <div style={{ display:'flex', gap:6, flexShrink:0, marginLeft:16 }}>
                <button className="adm-btn adm-btn-ghost adm-btn-xs"
                  onClick={e => { e.stopPropagation(); setEditPrompt(p); }}>
                  <AIcon n="eye" s={13}/>
                  Preview
                </button>
                <button className="adm-btn adm-btn-ghost adm-btn-xs"
                  onClick={e => { e.stopPropagation(); setEditPrompt(p); }}>
                  <AIcon n="edit" s={13}/>
                  Edit
                </button>
                <button className="adm-btn adm-btn-ghost adm-btn-xs"
                  onClick={e => e.stopPropagation()}
                  style={{ color:'#DC2626' }}>
                  <AIcon n="trash" s={13}/>
                </button>
              </div>
            </div>
          ))}
          <div style={{ padding:'12px 16px', borderTop:'1px solid #ECE9F5', display:'flex', justifyContent:'space-between', alignItems:'center' }}>
            <span style={{ fontSize:13, color:'#8B85A0' }}>
              {filtered.length} of {prompts.length} templates
            </span>
          </div>
        </div>

        {editPrompt && <PromptModal prompt={editPrompt} onClose={() => setEditPrompt(null)}/>}
      </div>
    );
  }

  window.AdminPrompts = AdminPrompts;
})();
