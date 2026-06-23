// SpeakPath Admin — AI Configuration (redesigned with SlideIn)
(function () {
  const { useState } = React;
  const { AIcon, SlideIn } = window;

  // ── DATA ──────────────────────────────────────────────────────
  const PROVIDERS_LIST = [
    { id:'openai',       name:'OpenAI',       status:'connected',    key:'sk-proj-•••••••••••••••••••••••', org:'org-SpeakPath' },
    { id:'anthropic',    name:'Anthropic',    status:'not_set',      key:'',  org:'' },
    { id:'azure-openai', name:'Azure OpenAI', status:'not_set',      key:'',  org:'' },
    { id:'google',       name:'Google',       status:'not_set',      key:'',  org:'' },
  ];

  const LLM_CATEGORIES = [
    { id:'default',    name:'Default LLM',           code:'llm.default',    desc:'Fallback for all features without a category-specific override.',
      initProvider:'openai', initModel:'gpt-4o-mini', configured:true },
    { id:'generation', name:'Content Generation',    code:'llm.generation', desc:'Generates activity content: writing, listening, speaking, email reply.',
      initProvider:'',       initModel:'',            configured:false },
    { id:'evaluation', name:'Evaluation & Feedback', code:'llm.evaluation', desc:'Scores and provides feedback on student responses, placement.',
      initProvider:'',       initModel:'',            configured:false },
    { id:'memory',     name:'Memory & Learning Path',code:'llm.memory',     desc:'Builds and updates the student learning path and memory profile.',
      initProvider:'',       initModel:'',            configured:false },
    { id:'tts',        name:'Text-to-Speech',        code:'llm.tts',        desc:'Generates audio for listening comprehension exercises.',
      initProvider:'',       initModel:'',            configured:false },
  ];

  const MODELS = {
    openai:        ['gpt-4o','gpt-4o-mini','gpt-4-turbo','gpt-3.5-turbo'],
    anthropic:     ['claude-3-5-sonnet','claude-3-haiku','claude-3-opus'],
    'azure-openai':['gpt-4o','gpt-4-turbo','gpt-35-turbo'],
    google:        ['gemini-1.5-pro','gemini-1.5-flash','gemini-pro'],
    '':            [],
  };

  // ── PROVIDER SLIDE-IN ─────────────────────────────────────────
  function ProviderSlideIn({ provider, onClose }) {
    const [key, setKey]       = useState(provider.key);
    const [org, setOrg]       = useState(provider.org);
    const [testing, setTesting] = useState(false);
    const [testResult, setTestResult] = useState(null);

    function runTest() {
      setTesting(true); setTestResult(null);
      setTimeout(() => {
        setTesting(false);
        setTestResult(key ? { ok:true, msg:'Connection successful · 142ms' } : { ok:false, msg:'API key is required' });
      }, 1600);
    }

    return (
      <SlideIn open={!!provider} onClose={onClose}
        title={`${provider.name} credentials`}
        sub={`Configure API access for ${provider.name}`}
        width={480}
        footer={<>
          <button className="adm-btn adm-btn-indigo" style={{ flex:1 }}>Save credentials</button>
          <button className="adm-btn adm-btn-ghost" onClick={onClose}>Cancel</button>
        </>}>
        <div style={{ display:'flex', flexDirection:'column', gap:20 }}>
          <div style={{ display:'flex', flex:'column', gap:6 }}>
            <label style={{ fontSize:13, fontWeight:700, color:'var(--ink)', display:'block', marginBottom:6 }}>
              API Key <span style={{ color:'#EF4444' }}>*</span>
            </label>
            <div style={{ position:'relative' }}>
              <input className="adm-input" type="password" placeholder={`${provider.name} API key`}
                value={key} onChange={e => setKey(e.target.value)}
                style={{ fontFamily:"'JetBrains Mono',monospace", fontSize:13, paddingRight:40 }}/>
              {key && (
                <button onClick={() => setKey('')}
                  style={{ position:'absolute', right:10, top:'50%', transform:'translateY(-50%)',
                    background:'none', border:'none', cursor:'pointer', color:'var(--muted)', padding:0 }}>
                  <AIcon n="x" s={14}/>
                </button>
              )}
            </div>
            <div style={{ fontSize:12, color:'var(--muted)', marginTop:4 }}>
              Your key is encrypted at rest and never logged.
            </div>
          </div>

          {provider.id === 'openai' && (
            <div>
              <label style={{ fontSize:13, fontWeight:700, color:'var(--ink)', display:'block', marginBottom:6 }}>
                Organisation ID <span style={{ fontSize:12, color:'var(--muted)', fontWeight:500 }}>(optional)</span>
              </label>
              <input className="adm-input" placeholder="org-xxxxxxx"
                value={org} onChange={e => setOrg(e.target.value)}
                style={{ fontFamily:"'JetBrains Mono',monospace", fontSize:13 }}/>
            </div>
          )}

          <div>
            <label style={{ fontSize:13, fontWeight:700, color:'var(--ink)', display:'block', marginBottom:6 }}>
              Base URL <span style={{ fontSize:12, color:'var(--muted)', fontWeight:500 }}>(optional override)</span>
            </label>
            <input className="adm-input" placeholder="https://api.openai.com/v1"
              style={{ fontFamily:"'JetBrains Mono',monospace", fontSize:12 }}/>
            <div style={{ fontSize:12, color:'var(--muted)', marginTop:4 }}>
              Leave blank to use the default endpoint.
            </div>
          </div>

          <div style={{ borderTop:'1px solid var(--border)', paddingTop:20 }}>
            <div style={{ fontSize:13, fontWeight:700, color:'var(--ink)', marginBottom:12 }}>Connection test</div>
            <button className="adm-btn adm-btn-ghost adm-btn-sm" onClick={runTest} disabled={testing}
              style={{ marginBottom:12 }}>
              {testing ? '…Testing' : <><AIcon n="refresh" s={13}/>Run test</>}
            </button>
            {testResult && (
              <div style={{
                display:'flex', alignItems:'center', gap:8, padding:'10px 14px', borderRadius:8,
                background: testResult.ok ? '#E0F6EE' : '#FEE2E2',
                fontSize:13, fontWeight:600,
                color: testResult.ok ? '#0A7468' : '#DC2626',
              }}>
                <AIcon n={testResult.ok ? 'check' : 'x'} s={14} c="currentColor" w={2.5}/>
                {testResult.msg}
              </div>
            )}
          </div>

          {provider.status === 'connected' && (
            <div style={{ borderTop:'1px solid var(--border)', paddingTop:20 }}>
              <div style={{ fontSize:13, fontWeight:700, color:'var(--danger-ink)', marginBottom:8 }}>Danger zone</div>
              <button className="adm-btn adm-btn-danger adm-btn-sm">
                <AIcon n="trash" s={13}/>Revoke credentials
              </button>
            </div>
          )}
        </div>
      </SlideIn>
    );
  }

  // ── LLM CATEGORY SLIDE-IN ─────────────────────────────────────
  function CategorySlideIn({ cat, onClose }) {
    const [provider, setProvider] = useState(cat.initProvider || '');
    const [model, setModel]       = useState(cat.initModel || '');
    const [testing, setTesting]   = useState(false);
    const [testResult, setTestResult] = useState(null);
    const models = MODELS[provider] || [];

    function runTest() {
      if (!provider) { setTestResult({ ok:false, msg:'Select a provider first' }); return; }
      setTesting(true); setTestResult(null);
      setTimeout(() => {
        setTesting(false);
        setTestResult({ ok:true, msg:`Response received · ${Math.floor(80+Math.random()*200)}ms` });
      }, 1400);
    }

    const isInherit = !provider;

    return (
      <SlideIn open={!!cat} onClose={onClose}
        title={cat.name}
        sub={cat.code}
        width={500}
        footer={<>
          <button className="adm-btn adm-btn-indigo" style={{ flex:1 }}>Save</button>
          {!isInherit && (
            <button className="adm-btn adm-btn-ghost" onClick={() => { setProvider(''); setModel(''); }}>
              Reset to inherit
            </button>
          )}
          <button className="adm-btn adm-btn-ghost" onClick={onClose}>Cancel</button>
        </>}>
        <div style={{ display:'flex', flexDirection:'column', gap:20 }}>
          {/* Description */}
          <div style={{ padding:'12px 14px', background:'var(--canvas)', borderRadius:9, fontSize:13.5, color:'var(--text)', lineHeight:1.55 }}>
            {cat.desc}
          </div>

          {/* Resolution hint */}
          <div style={{ fontSize:12.5, color:'var(--muted)', lineHeight:1.6 }}>
            Resolution order: <strong style={{ color:'var(--ink)' }}>{cat.name}</strong> → Default LLM → 503 error.
            {isInherit && ' Currently inheriting from Default LLM.'}
          </div>

          <div>
            <label style={{ fontSize:13, fontWeight:700, color:'var(--ink)', display:'block', marginBottom:6 }}>Provider</label>
            <select className="adm-select" style={{ width:'100%' }} value={provider}
              onChange={e => { setProvider(e.target.value); setModel(''); }}>
              <option value="">— inherit from Default LLM —</option>
              {Object.keys(MODELS).filter(k=>k).map(p => <option key={p}>{p}</option>)}
            </select>
          </div>

          <div>
            <label style={{ fontSize:13, fontWeight:700, color:'var(--ink)', display:'block', marginBottom:6 }}>Model</label>
            <select className="adm-select" style={{ width:'100%', opacity: models.length===0?.5:1 }}
              value={model} onChange={e => setModel(e.target.value)} disabled={!models.length}>
              {models.length === 0 ? <option>— inherit —</option> : models.map(m => <option key={m}>{m}</option>)}
            </select>
            {provider && !model && (
              <div style={{ fontSize:12, color:'var(--warn-ink)', marginTop:4 }}>Select a model to complete configuration.</div>
            )}
          </div>

          <div style={{ borderTop:'1px solid var(--border)', paddingTop:20 }}>
            <div style={{ fontSize:13, fontWeight:700, color:'var(--ink)', marginBottom:12 }}>Test this category</div>
            <button className="adm-btn adm-btn-ghost adm-btn-sm" onClick={runTest} disabled={testing} style={{ marginBottom:12 }}>
              {testing ? '…Running' : <><AIcon n="zap" s={13}/>Run test prompt</>}
            </button>
            {testResult && (
              <div style={{ display:'flex', alignItems:'center', gap:8, padding:'10px 14px', borderRadius:8,
                background: testResult.ok ? '#E0F6EE' : '#FEE2E2',
                fontSize:13, fontWeight:600,
                color: testResult.ok ? '#0A7468' : '#DC2626' }}>
                <AIcon n={testResult.ok?'check':'x'} s={14} c="currentColor" w={2.5}/>
                {testResult.msg}
              </div>
            )}
          </div>
        </div>
      </SlideIn>
    );
  }

  // ── MAIN PAGE ─────────────────────────────────────────────────
  function AdminAIConfig() {
    const [providerOpen, setProviderOpen] = useState(null);
    const [categoryOpen, setCategoryOpen] = useState(null);
    const [categories, setCategories]     = useState(LLM_CATEGORIES.map(c => ({...c})));

    const configuredCount = categories.filter(c => c.configured || c.initProvider).length;

    return (
      <div>
        <div className="adm-page-header">
          <div>
            <h1 className="adm-page-h1">AI Configuration</h1>
            <p className="adm-page-sub">Category-level model routing, provider credentials and voice settings</p>
          </div>
          <button className="adm-btn adm-btn-ghost">
            <AIcon n="refresh" s={14}/>Test all connections
          </button>
        </div>

        {/* ── PROVIDER CREDENTIALS ── */}
        <div className="adm-card" style={{ marginBottom:20 }}>
          <div className="adm-card-p" style={{ paddingBottom:0 }}>
            <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:4 }}>
              <div>
                <div style={{ fontSize:14, fontWeight:800, color:'var(--ink)' }}>Provider credentials</div>
                <div style={{ fontSize:12.5, color:'var(--muted)', marginTop:2 }}>Add API keys for each AI provider you want to use across LLM categories.</div>
              </div>
              <span className={`adm-badge ${PROVIDERS_LIST.filter(p=>p.status==='connected').length > 0 ? 'adm-badge-success' : 'adm-badge-warn'}`}>
                {PROVIDERS_LIST.filter(p=>p.status==='connected').length} connected
              </span>
            </div>
          </div>
          {PROVIDERS_LIST.map((p, i) => (
            <div key={p.id} style={{
              display:'flex', alignItems:'center', gap:16,
              padding:'14px 20px',
              borderTop: i === 0 ? '1px solid var(--border)' : 'none',
              borderBottom:'1px solid var(--border)',
            }}>
              <div style={{ width:38, height:38, borderRadius:10, background:p.status==='connected'?'#EDEBFF':'#F6F4FB',
                display:'grid', placeItems:'center', flexShrink:0 }}>
                <AIcon n="aiconfig" s={18} c={p.status==='connected'?'#5B4BE8':'#BDB8CC'}/>
              </div>
              <div style={{ flex:1, minWidth:0 }}>
                <div style={{ fontSize:14, fontWeight:700, color:'var(--ink)' }}>{p.name}</div>
                {p.status === 'connected'
                  ? <div style={{ fontSize:12, color:'var(--muted)', fontFamily:"'JetBrains Mono',monospace", marginTop:2 }}>{p.key}</div>
                  : <div style={{ fontSize:12, color:'var(--muted)', marginTop:2 }}>No credentials configured</div>}
              </div>
              <span style={{
                fontSize:12, fontWeight:700, padding:'3px 10px', borderRadius:99,
                background: p.status==='connected' ? '#E0F6EE' : '#F6F4FB',
                color: p.status==='connected' ? '#13B07C' : '#BDB8CC',
              }}>
                {p.status==='connected' ? 'Connected' : 'Not set'}
              </span>
              <button className="adm-btn adm-btn-ghost adm-btn-sm"
                onClick={() => setProviderOpen(p)}>
                <AIcon n={p.status==='connected'?'edit':'plus'} s={13}/>
                {p.status==='connected' ? 'Update' : 'Configure'}
              </button>
            </div>
          ))}
        </div>

        {/* ── LLM CATEGORIES ── */}
        <div className="adm-card" style={{ marginBottom:20 }}>
          <div className="adm-card-p" style={{ paddingBottom:0 }}>
            <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:4 }}>
              <div>
                <div style={{ fontSize:14, fontWeight:800, color:'var(--ink)' }}>LLM categories</div>
                <div style={{ fontSize:12.5, color:'var(--muted)', marginTop:2 }}>
                  Route each feature to a specific model. Unset categories inherit from Default LLM.
                </div>
              </div>
              <span className="adm-badge adm-badge-indigo">{configuredCount}/{categories.length} configured</span>
            </div>
          </div>

          {/* Column headers */}
          <div style={{ display:'flex', alignItems:'center', gap:16, padding:'10px 20px',
            fontSize:10.5, fontWeight:800, color:'var(--muted)', letterSpacing:'.08em', textTransform:'uppercase',
            borderTop:'1px solid var(--border)', borderBottom:'1px solid var(--border)',
            background:'#FAFAFE' }}>
            <div style={{ flex:'0 0 200px' }}>Category</div>
            <div style={{ flex:'0 0 140px' }}>Code</div>
            <div style={{ flex:'0 0 120px' }}>Provider</div>
            <div style={{ flex:1 }}>Model</div>
            <div style={{ flex:'0 0 100px', textAlign:'center' }}>Status</div>
            <div style={{ flex:'0 0 90px' }}/>
          </div>

          {categories.map((cat, i) => {
            const isConfigured = cat.configured || !!cat.initProvider;
            return (
              <div key={cat.id} style={{
                display:'flex', alignItems:'center', gap:16, padding:'14px 20px',
                borderBottom: i<categories.length-1 ? '1px solid var(--border)' : 'none',
                transition:'background .08s',
              }}
              onMouseOver={e => e.currentTarget.style.background='#FAFAFE'}
              onMouseOut={e => e.currentTarget.style.background='transparent'}>
                <div style={{ flex:'0 0 200px' }}>
                  <div style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)' }}>{cat.name}</div>
                  <div style={{ fontSize:12, color:'var(--muted)', marginTop:1 }}>{cat.desc.slice(0,48)}…</div>
                </div>
                <div style={{ flex:'0 0 140px' }}>
                  <code style={{ fontSize:12, fontFamily:"'JetBrains Mono',monospace", color:'#B45CF0',
                    background:'#F2E9FF', padding:'2px 7px', borderRadius:5 }}>{cat.code}</code>
                </div>
                <div style={{ flex:'0 0 120px', fontSize:13, color: cat.initProvider ? 'var(--ink)' : 'var(--faint)', fontWeight:600 }}>
                  {cat.initProvider || '— inherit —'}
                </div>
                <div style={{ flex:1, fontSize:13, color: cat.initModel ? 'var(--text)' : 'var(--faint)' }}>
                  {cat.initModel || '— inherit —'}
                </div>
                <div style={{ flex:'0 0 100px', textAlign:'center' }}>
                  <span style={{
                    fontSize:12, fontWeight:700, padding:'3px 10px', borderRadius:99,
                    background: isConfigured ? '#E0F6EE' : '#FFF1DC',
                    color: isConfigured ? '#13B07C' : '#B26410',
                  }}>{isConfigured ? 'Configured' : 'Not set'}</span>
                </div>
                <div style={{ flex:'0 0 90px', display:'flex', justifyContent:'flex-end' }}>
                  <button className="adm-btn adm-btn-ghost adm-btn-xs"
                    onClick={() => setCategoryOpen(cat)}>
                    <AIcon n="settings" s={12}/>Configure
                  </button>
                </div>
              </div>
            );
          })}
        </div>

        {/* ── TTS ── */}
        <div className="adm-card adm-card-p" style={{ marginBottom:20 }}>
          <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:20 }}>
            <div>
              <div style={{ fontSize:14, fontWeight:800, color:'var(--ink)' }}>Text-to-Speech</div>
              <div style={{ fontSize:12.5, color:'var(--muted)', marginTop:2 }}>Audio generation for listening exercises and feedback playback.</div>
            </div>
            <span style={{ fontSize:12, fontWeight:700, padding:'3px 10px', borderRadius:99, background:'#FFF1DC', color:'#B26410' }}>Not set</span>
          </div>
          <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap:16 }}>
            {[
              { label:'Provider', opts:['OpenAI TTS','ElevenLabs','Azure TTS','Google TTS'] },
              { label:'Default voice', opts:['Alloy (neutral)','Nova (female)','Onyx (male)','Shimmer (soft)'] },
              { label:'Playback speed', opts:['0.85×','0.9×','1.0×','1.1×','1.25×'] },
            ].map(f => (
              <div key={f.label}>
                <div style={{ fontSize:12.5, fontWeight:700, color:'var(--ink)', marginBottom:6 }}>{f.label}</div>
                <select className="adm-select" style={{ width:'100%' }}>
                  {f.opts.map(o => <option key={o}>{o}</option>)}
                </select>
              </div>
            ))}
          </div>
        </div>

        {/* ── RATE LIMITS ── */}
        <div className="adm-card adm-card-p">
          <div style={{ fontSize:14, fontWeight:800, color:'var(--ink)', marginBottom:16 }}>Rate limits & quotas</div>
          <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap:20 }}>
            {[
              { label:'Requests per minute', used:12, limit:60, unit:'RPM' },
              { label:'Tokens per day', used:28400, limit:100000, unit:'tokens', fmt:(v)=>v>=1000?`${(v/1000).toFixed(0)}k`:v },
              { label:'Daily cost cap', used:1.82, limit:10, unit:'USD', fmt:(v)=>`$${v.toFixed(2)}` },
            ].map(item => {
              const pct = Math.round((item.used/item.limit)*100);
              const fmt = item.fmt || (v=>v);
              return (
                <div key={item.label}>
                  <div style={{ display:'flex', justifyContent:'space-between', marginBottom:8 }}>
                    <span style={{ fontSize:13, fontWeight:600, color:'var(--ink)' }}>{item.label}</span>
                    <span style={{ fontSize:12.5, color:'var(--muted)', fontWeight:600 }}>
                      {fmt(item.used)} / {fmt(item.limit)} {item.unit}
                    </span>
                  </div>
                  <div style={{ height:7, borderRadius:99, background:'#F0EEF8', overflow:'hidden' }}>
                    <div style={{ height:'100%', borderRadius:99, transition:'width .4s',
                      width:`${pct}%`, background: pct>80?'#F0982C':'#13B07C' }}/>
                  </div>
                  <div style={{ fontSize:11.5, color:'var(--muted)', marginTop:5 }}>{pct}% used</div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Slide-ins */}
        {providerOpen && <ProviderSlideIn provider={providerOpen} onClose={() => setProviderOpen(null)}/>}
        {categoryOpen && <CategorySlideIn cat={categoryOpen} onClose={() => setCategoryOpen(null)}/>}
      </div>
    );
  }

  window.AdminAIConfig = AdminAIConfig;
})();
