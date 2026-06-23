// SpeakPath Admin — Dashboard — Full production SaaS
(function () {
  const { useState } = React;
  const { AIcon, cefrClass, lifecycleBadge } = window;
  const { students, actPerDay14, aiCost30d, studentActivities } = window.ADMIN_DATA;

  // ── COMPUTED ──────────────────────────────────────────────────
  const totalStudents = students.length;
  const onboarded     = students.filter(s => s.onboarding === 'Complete').length;
  const activeWeek    = students.filter(s => s.minutesWeek > 0).length;
  const engagePct     = Math.round(activeWeek / totalStudents * 100);
  const totalActs     = students.reduce((a, s) => a + s.activitiesDone, 0);
  const cost7d        = aiCost30d.slice(-7).reduce((a, b) => a + b, 0);
  const cost30d       = aiCost30d.reduce((a, b) => a + b, 0);
  const allScores     = Object.values(studentActivities).flat().map(a => a.score);
  const avgScore      = allScores.length ? Math.round(allScores.reduce((a,b)=>a+b,0)/allScores.length) : 0;
  const atRisk        = students.filter(s => s.minutesWeek === 0 && s.activitiesDone > 0);
  const noActYet      = students.filter(s => s.activitiesDone === 0 && !s.archived);
  const noCefr        = students.filter(s => !s.cefr && !s.archived);
  const topStreaks     = [...students].sort((a,b)=>b.streak-a.streak).slice(0,5);

  function studentStatus(s) {
    if (s.minutesWeek > 0) return 'active';
    if (s.activitiesDone > 0) return 'at-risk';
    return 'inactive';
  }
  function relTime(d) {
    const days = Math.round((new Date('2026-06-23') - new Date(d)) / 86400000);
    return days === 0 ? 'Today' : days === 1 ? 'Yesterday' : `${days}d ago`;
  }

  const DAY_LABELS = ['Jun 8','','','','Jun 12','','','Jun 15','','','Jun 18','','','Jun 21'];

  // ── KPI CARD ──────────────────────────────────────────────────
  function KpiCard({ icon, iconBg, iconColor, label, value, delta, deltaColor }) {
    return (
      <div className="adm-card" style={{ display:'flex', alignItems:'stretch', overflow:'hidden' }}>
        <div style={{ width:56, flexShrink:0, background:iconBg, display:'grid', placeItems:'center', borderRight:'1px solid var(--border)' }}>
          <AIcon n={icon} s={20} c={iconColor} w={2}/>
        </div>
        <div style={{ padding:'13px 15px', flex:1 }}>
          <div style={{ fontSize:10.5, fontWeight:800, letterSpacing:'.09em', textTransform:'uppercase', color:'var(--muted)', marginBottom:6 }}>{label}</div>
          <div style={{ fontSize:24, fontWeight:800, color:'var(--ink)', letterSpacing:'-.04em', lineHeight:1 }}>{value}</div>
          {delta && <div style={{ fontSize:11.5, fontWeight:600, marginTop:5, color: deltaColor || 'var(--muted)' }}>{delta}</div>}
        </div>
      </div>
    );
  }

  // ── AREA CHART ────────────────────────────────────────────────
  function AreaChart({ data, labels, color = '#5B4BE8', H = 150 }) {
    const [hov, setHov] = useState(null);
    const W = 580; const pL = 32; const pR = 12; const pT = 14; const pB = 26;
    const pW = W-pL-pR; const pH = H-pT-pB;
    const max = Math.max(...data)+1;
    const xS = i => pL + (i/(data.length-1))*pW;
    const yS = v => pT + pH - (v/max)*pH;
    const pts = data.map((v,i) => [xS(i), yS(v)]);
    function bez(ps) {
      return ps.map(([x,y],i) => {
        if (!i) return `M${x.toFixed(1)},${y.toFixed(1)}`;
        const [px,py] = ps[i-1]; const mx = (px+x)/2;
        return `C${mx.toFixed(1)},${py.toFixed(1)} ${mx.toFixed(1)},${y.toFixed(1)} ${x.toFixed(1)},${y.toFixed(1)}`;
      }).join('');
    }
    const line = bez(pts);
    const last = pts[pts.length-1];
    const area = `${line}L${last[0].toFixed(1)},${pT+pH}L${pL},${pT+pH}Z`;
    const ticks = [0, Math.round(max/2), max];
    const showX = [0,3,6,9,12,13];
    return (
      <svg viewBox={`0 0 ${W} ${H}`} style={{ width:'100%', height:H, overflow:'visible' }}>
        <defs>
          <linearGradient id="cag" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={color} stopOpacity="0.13"/>
            <stop offset="100%" stopColor={color} stopOpacity="0"/>
          </linearGradient>
        </defs>
        {ticks.map(v => (
          <g key={v}>
            <line x1={pL} y1={yS(v)} x2={W-pR} y2={yS(v)} stroke="#F0EEF8" strokeWidth="1"/>
            <text x={pL-5} y={yS(v)+4} textAnchor="end" fontSize="9.5" fill="#C4C0D4" fontFamily="'Plus Jakarta Sans',sans-serif">{v}</text>
          </g>
        ))}
        <path d={area} fill="url(#cag)"/>
        <path d={line} fill="none" stroke={color} strokeWidth="2.25" strokeLinecap="round"/>
        {pts.map(([x,y],i) => (
          <g key={i} onMouseEnter={()=>setHov(i)} onMouseLeave={()=>setHov(null)}>
            <circle cx={x} cy={y} r="10" fill="transparent" style={{ cursor:'default' }}/>
            {hov===i && <>
              <line x1={x} y1={pT} x2={x} y2={pT+pH} stroke={color} strokeWidth="1" strokeDasharray="3 3" opacity=".4"/>
              <rect x={x-22} y={y-27} width={44} height={20} rx="5" fill={color}/>
              <text x={x} y={y-13} textAnchor="middle" fontSize="11" fontWeight="700" fill="#fff" fontFamily="'Plus Jakarta Sans',sans-serif">{data[i]}</text>
            </>}
            <circle cx={x} cy={y} r={hov===i?4:0} fill={color} stroke="#fff" strokeWidth="2"/>
          </g>
        ))}
        {showX.map(i => labels[i] && (
          <text key={i} x={xS(i)} y={H-3} textAnchor="middle" fontSize="9.5" fill="#C4C0D4" fontFamily="'Plus Jakarta Sans',sans-serif">{labels[i]}</text>
        ))}
      </svg>
    );
  }

  // ── SPARKLINE ─────────────────────────────────────────────────
  function Sparkline({ data, color, W=80, H=28 }) {
    const max=Math.max(...data); const min=Math.min(...data); const r=max-min||1;
    const pts=data.map((v,i)=>[(i/(data.length-1))*W, H-((v-min)/r)*H*.8-H*.1]);
    const d=pts.map(([x,y],i)=>`${i?'L':'M'}${x.toFixed(1)},${y.toFixed(1)}`).join('');
    return (
      <svg width={W} height={H} viewBox={`0 0 ${W} ${H}`} style={{display:'block',flexShrink:0}}>
        <path d={d} fill="none" stroke={color} strokeWidth="1.75" strokeLinecap="round"/>
        <circle cx={pts[pts.length-1][0]} cy={pts[pts.length-1][1]} r="2.5" fill={color} stroke="#fff" strokeWidth="1.5"/>
      </svg>
    );
  }

  // ── WEEKLY SNAPSHOT ───────────────────────────────────────────
  function WeeklySnapshot({ navigate }) {
    const pending = atRisk.length + noActYet.length;
    const actsThisWeek = actPerDay14.slice(-7).reduce((a,b)=>a+b,0);
    return (
      <div className="adm-card adm-card-p" style={{
        background:'linear-gradient(135deg,#211B36 0%,#2D2455 100%)',
        border:'none', marginBottom:20,
        display:'grid', gridTemplateColumns:'1fr 1fr 1fr 1fr', gap:0,
      }}>
        {[
          { label:'This week', value:`${actsThisWeek} activities`, sub:'↑ 18% vs last week', color:'#fff', sub_color:'rgba(255,255,255,.5)' },
          { label:'Engagement', value:`${engagePct}%`, sub:`${activeWeek} of ${totalStudents} students active`, color:'#fff', sub_color:'rgba(255,255,255,.5)' },
          { label:'Avg score', value:`${avgScore}/100`, sub:'Based on all activities', color:'#fff', sub_color:'rgba(255,255,255,.5)' },
          { label:'Action needed', value:`${pending} students`, sub: pending > 0 ? 'Require attention →' : 'All students on track', color: pending > 0 ? '#FBB040' : '#5DFFA0', sub_color: pending > 0 ? '#FBB040' : '#5DFFA0', onClick: () => navigate('students') },
        ].map((item, i) => (
          <div key={i} onClick={item.onClick}
            style={{ padding:'18px 22px', borderRight: i<3 ? '1px solid rgba(255,255,255,.1)' : 'none',
              cursor: item.onClick ? 'pointer' : 'default' }}>
            <div style={{ fontSize:11, fontWeight:800, letterSpacing:'.09em', textTransform:'uppercase', color:'rgba(255,255,255,.45)', marginBottom:8 }}>{item.label}</div>
            <div style={{ fontSize:22, fontWeight:800, color:item.color, letterSpacing:'-.03em', lineHeight:1 }}>{item.value}</div>
            <div style={{ fontSize:12, marginTop:6, color:item.sub_color, fontWeight:600 }}>{item.sub}</div>
          </div>
        ))}
      </div>
    );
  }

  // ── SYSTEM HEALTH ─────────────────────────────────────────────
  const SERVICES = [
    { name:'Writing AI',  ms:142 }, { name:'Feedback AI', ms:88 },
    { name:'Speaking AI', ms:218 }, { name:'Database',    ms:4  },
    { name:'Auth',        ms:11  },
  ];
  function SystemHealth({ navigate }) {
    return (
      <div className="adm-card adm-card-p" style={{ display:'flex', flexDirection:'column' }}>
        <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:14 }}>
          <span style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)' }}>System health</span>
          <span style={{ fontSize:12, fontWeight:700, color:'#13B07C', display:'flex', alignItems:'center', gap:4 }}>
            <span className="adm-dot adm-dot-g adm-dot-pulse"/>All clear
          </span>
        </div>
        {SERVICES.map((svc, i) => (
          <div key={svc.name} style={{ display:'flex', alignItems:'center', gap:10, padding:'8px 0', borderBottom: i<SERVICES.length-1 ? '1px solid var(--border)' : 'none' }}>
            <span className="adm-dot adm-dot-g" style={{ flexShrink:0 }}/>
            <span style={{ flex:1, fontSize:12.5, fontWeight:600, color:'var(--text)' }}>{svc.name}</span>
            <div style={{ width:48, height:4, borderRadius:99, background:'#F0EEF8', overflow:'hidden' }}>
              <div style={{ height:'100%', borderRadius:99, width:`${Math.min(100,(svc.ms/250)*100)}%`,
                background: svc.ms<100 ? '#13B07C' : svc.ms<200 ? '#F0982C' : '#EF4444' }}/>
            </div>
            <span style={{ fontSize:11, color:'var(--muted)', fontWeight:700, width:36, textAlign:'right' }}>{svc.ms}ms</span>
          </div>
        ))}
        <div style={{ marginTop:14, paddingTop:14, borderTop:'1px solid var(--border)', display:'flex', flexDirection:'column', gap:8 }}>
          {[['Provider','OpenAI · gpt-4o-mini'],['Error rate (24h)','0.12%'],['API calls today','47']].map(([k,v])=>(
            <div key={k} style={{ display:'flex', justifyContent:'space-between' }}>
              <span style={{ fontSize:12, color:'var(--muted)' }}>{k}</span>
              <span style={{ fontSize:12, fontWeight:700, color:'var(--ink)' }}>{v}</span>
            </div>
          ))}
        </div>
        <button className="adm-card-link" style={{ marginTop:14, fontSize:12.5 }} onClick={()=>navigate('diagnostics')}>
          View diagnostics →
        </button>
      </div>
    );
  }

  // ── ONBOARDING FUNNEL ─────────────────────────────────────────
  function OnboardingFunnel() {
    const stages = [
      { label:'Signed up',       count:totalStudents, pct:100 },
      { label:'Onboarded',       count:onboarded,     pct:Math.round(onboarded/totalStudents*100) },
      { label:'CEFR placed',     count:totalStudents-noCefr.length, pct:Math.round((totalStudents-noCefr.length)/totalStudents*100) },
      { label:'Active learner',  count:activeWeek,    pct:Math.round(activeWeek/totalStudents*100) },
    ];
    const colors = ['#5B4BE8','#7C6CFF','#B45CF0','#13B07C'];
    return (
      <div className="adm-card adm-card-p">
        <div style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)', marginBottom:16 }}>Onboarding funnel</div>
        <div style={{ display:'flex', flexDirection:'column', gap:10 }}>
          {stages.map((s, i) => (
            <div key={s.label}>
              <div style={{ display:'flex', justifyContent:'space-between', marginBottom:5 }}>
                <span style={{ fontSize:12.5, fontWeight:600, color:'var(--text)' }}>{s.label}</span>
                <span style={{ fontSize:12.5, fontWeight:800, color:'var(--ink)' }}>{s.count} <span style={{ color:'var(--muted)', fontWeight:600 }}>· {s.pct}%</span></span>
              </div>
              <div style={{ height:8, borderRadius:99, background:'#F0EEF8', overflow:'hidden' }}>
                <div style={{ width:`${s.pct}%`, height:'100%', borderRadius:99, background:colors[i], transition:'width .4s' }}/>
              </div>
            </div>
          ))}
        </div>
        {noCefr.length > 0 && (
          <div style={{ marginTop:14, padding:'10px 12px', background:'#FFF1DC', borderRadius:8, fontSize:12.5, color:'#B26410' }}>
            ⚠ {noCefr.length} student{noCefr.length!==1?'s':''} awaiting CEFR placement
          </div>
        )}
      </div>
    );
  }

  // ── AT-RISK ALERTS ────────────────────────────────────────────
  function AtRiskAlerts({ navigate }) {
    const alerts = [
      ...atRisk.map(s => ({ s, type:'at-risk', label:'No activity this week', color:'#F0982C', bg:'#FFF1DC' })),
      ...noActYet.map(s => ({ s, type:'inactive', label:'No activities started', color:'#EF4444', bg:'#FEE2E2' })),
    ];
    return (
      <div className="adm-card adm-card-p">
        <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:14 }}>
          <span style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)' }}>At-risk students</span>
          {alerts.length > 0
            ? <span className="adm-badge adm-badge-warn">{alerts.length} students</span>
            : <span className="adm-badge adm-badge-success">All on track</span>}
        </div>
        {alerts.length === 0 ? (
          <div style={{ textAlign:'center', padding:'24px 0', color:'var(--muted)' }}>
            <div style={{ fontSize:24, marginBottom:8 }}>✓</div>
            <div style={{ fontSize:13, fontWeight:600 }}>All students are engaged this week</div>
          </div>
        ) : (
          <div style={{ display:'flex', flexDirection:'column', gap:0 }}>
            {alerts.slice(0,5).map(({ s, label, color, bg }, i) => (
              <div key={s.id} style={{ display:'flex', alignItems:'center', gap:12, padding:'10px 0',
                borderBottom: i<Math.min(alerts.length,5)-1 ? '1px solid var(--border)' : 'none' }}>
                <div style={{ width:30, height:30, borderRadius:8, background:bg,
                  display:'grid', placeItems:'center', flexShrink:0 }}>
                  <span style={{ fontSize:13, fontWeight:800, color }}>{(s.name!==s.email?s.name:s.email)[0].toUpperCase()}</span>
                </div>
                <div style={{ flex:1, minWidth:0 }}>
                  <div style={{ fontSize:13, fontWeight:700, color:'var(--ink)', lineHeight:1.2,
                    overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap' }}>
                    {s.name!==s.email ? s.name : s.email.split('@')[0]}
                  </div>
                  <div style={{ fontSize:11.5, color, fontWeight:600, marginTop:1 }}>{label}</div>
                </div>
                <div style={{ fontSize:11.5, color:'var(--muted)', flexShrink:0 }}>
                  {relTime(s.joined)}
                </div>
              </div>
            ))}
          </div>
        )}
        {alerts.length > 0 && (
          <button className="adm-card-link" style={{ marginTop:12, fontSize:12.5 }}
            onClick={() => navigate('students')}>View all students →</button>
        )}
      </div>
    );
  }

  // ── SCORE DISTRIBUTION ────────────────────────────────────────
  function ScoreDistribution() {
    const bins = [
      { range:'< 60',  scores: allScores.filter(s=>s<60)  },
      { range:'60–69', scores: allScores.filter(s=>s>=60&&s<70) },
      { range:'70–79', scores: allScores.filter(s=>s>=70&&s<80) },
      { range:'80–89', scores: allScores.filter(s=>s>=80&&s<90) },
      { range:'90–100',scores: allScores.filter(s=>s>=90) },
    ];
    const maxCount = Math.max(...bins.map(b=>b.scores.length), 1);
    return (
      <div className="adm-card adm-card-p">
        <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:16 }}>
          <span style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)' }}>Score distribution</span>
          <span className="adm-badge adm-badge-success">Avg {avgScore}/100</span>
        </div>
        <div style={{ display:'flex', flexDirection:'column', gap:9 }}>
          {bins.map(b => (
            <div key={b.range} style={{ display:'flex', alignItems:'center', gap:10 }}>
              <span style={{ fontSize:12, fontWeight:700, color:'var(--muted)', minWidth:46, textAlign:'right' }}>{b.range}</span>
              <div style={{ flex:1, height:20, borderRadius:5, background:'#F6F4FB', overflow:'hidden', position:'relative' }}>
                <div style={{
                  height:'100%', borderRadius:5, transition:'width .4s',
                  background: b.range.startsWith('9') ? '#13B07C' : b.range.startsWith('8') ? '#5B4BE8' : b.range.startsWith('7') ? '#B45CF0' : '#F0982C',
                  width: `${(b.scores.length/maxCount)*100}%`,
                  minWidth: b.scores.length ? 6 : 0,
                }}/>
              </div>
              <span style={{ fontSize:13, fontWeight:800, color:'var(--ink)', minWidth:20 }}>{b.scores.length}</span>
            </div>
          ))}
        </div>
        <div style={{ marginTop:14, paddingTop:12, borderTop:'1px solid var(--border)', display:'flex', justifyContent:'space-between' }}>
          <span style={{ fontSize:12, color:'var(--muted)' }}>{allScores.length} activities graded</span>
          <span style={{ fontSize:12, fontWeight:700, color:'var(--ink)' }}>Target ≥ 80/100</span>
        </div>
      </div>
    );
  }

  // ── DONUT CHART (AI cost by type) ─────────────────────────────
  function DonutChart({ segments, size=88 }) {
    const r=36; const cx=50; const cy=50;
    const circ=2*Math.PI*r;
    let cum=0;
    return (
      <svg viewBox="0 0 100 100" width={size} height={size} style={{ flexShrink:0 }}>
        {segments.map((seg,i) => {
          const offset = circ*(0.25-cum);
          cum += seg.pct/100;
          return (
            <circle key={i} cx={cx} cy={cy} r={r} fill="none"
              stroke={seg.color} strokeWidth="14"
              strokeDasharray={`${(seg.pct/100)*circ} ${circ}`}
              strokeDashoffset={offset}/>
          );
        })}
        <circle cx={cx} cy={cy} r={22} fill="#fff"/>
      </svg>
    );
  }

  function CostByType() {
    const segs = [
      { label:'Writing',    pct:42, color:'#5B4BE8' },
      { label:'Feedback',   pct:38, color:'#B45CF0' },
      { label:'Speaking',   pct:12, color:'#FF7A59' },
      { label:'Assessment', pct:8,  color:'#13B07C' },
    ];
    return (
      <div className="adm-card adm-card-p">
        <div style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)', marginBottom:14 }}>AI cost by type</div>
        <div style={{ display:'flex', alignItems:'center', gap:16 }}>
          <DonutChart segments={segs} size={80}/>
          <div style={{ flex:1, display:'flex', flexDirection:'column', gap:7 }}>
            {segs.map(s => (
              <div key={s.label} style={{ display:'flex', alignItems:'center', gap:7 }}>
                <div style={{ width:9, height:9, borderRadius:3, background:s.color, flexShrink:0 }}/>
                <span style={{ flex:1, fontSize:12, color:'var(--text)' }}>{s.label}</span>
                <span style={{ fontSize:12, fontWeight:800, color:'var(--ink)' }}>{s.pct}%</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  }

  // ── SESSION DURATION ──────────────────────────────────────────
  function SessionDuration() {
    const data = [0, 14, 11, 7, 0, 16, 12];
    const labels = ['Mo','Tu','We','Th','Fr','Sa','Su'];
    const max = Math.max(...data);
    const avg = Math.round(data.filter(v=>v>0).reduce((a,b)=>a+b,0)/data.filter(v=>v>0).length);
    return (
      <div className="adm-card adm-card-p">
        <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:14 }}>
          <span style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)' }}>Avg session</span>
          <span style={{ fontSize:18, fontWeight:800, color:'var(--ink)' }}>{avg}<span style={{ fontSize:12, color:'var(--muted)', fontWeight:600 }}> min</span></span>
        </div>
        <div style={{ display:'flex', gap:4, height:64, alignItems:'flex-end' }}>
          {data.map((v, i) => (
            <div key={i} style={{ flex:1, display:'flex', flexDirection:'column', alignItems:'center', gap:4 }}>
              <div style={{ width:'100%', borderRadius:'4px 4px 0 0', transition:'height .3s',
                height: v ? `${Math.round((v/max)*56)+4}px` : 3,
                background: v ? (v===max ? '#5B4BE8' : '#EDEBFF') : '#F0EEF8' }}/>
              <span style={{ fontSize:10, color:'var(--faint)' }}>{labels[i]}</span>
            </div>
          ))}
        </div>
        <div style={{ marginTop:12, fontSize:12, color:'var(--muted)' }}>Average active days this week</div>
      </div>
    );
  }

  // ── STREAK LEADERBOARD ────────────────────────────────────────
  function StreakLeaderboard() {
    const medals = ['🥇','🥈','🥉','',''];
    return (
      <div className="adm-card adm-card-p">
        <div style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)', marginBottom:14 }}>Streak leaderboard</div>
        <div style={{ display:'flex', flexDirection:'column', gap:0 }}>
          {topStreaks.map((s, i) => (
            <div key={s.id} style={{ display:'flex', alignItems:'center', gap:10, padding:'8px 0',
              borderBottom: i<topStreaks.length-1 ? '1px solid var(--border)' : 'none' }}>
              <span style={{ fontSize:15, width:20, flexShrink:0 }}>{medals[i] || <span style={{ fontSize:11, color:'var(--faint)' }}>#{i+1}</span>}</span>
              <div style={{ flex:1, minWidth:0 }}>
                <div style={{ fontSize:13, fontWeight:700, color:'var(--ink)', lineHeight:1.2,
                  overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap' }}>
                  {s.name!==s.email ? s.name : s.email.split('@')[0]}
                </div>
                {s.cefr && <span className={`adm-cefr ${cefrClass(s.cefr)}`} style={{ fontSize:10, padding:'1px 6px' }}>{s.cefr}</span>}
              </div>
              <div style={{ textAlign:'right', flexShrink:0 }}>
                <div style={{ fontSize:16, fontWeight:800, color: i===0 ? '#5B4BE8' : 'var(--ink)', letterSpacing:'-.02em' }}>
                  {s.streak}
                </div>
                <div style={{ fontSize:10, color:'var(--muted)' }}>days</div>
              </div>
            </div>
          ))}
        </div>
      </div>
    );
  }

  // ── PENDING ACTIONS ───────────────────────────────────────────
  function PendingActions({ navigate }) {
    const actions = [
      noCefr.length > 0 && { icon:'target', bg:'#FFF1DC', ic:'#F0982C', title:`${noCefr.length} students without CEFR`, sub:'Run placement assessment', to:'students', urgent:true },
      noActYet.length > 0 && { icon:'zap', bg:'#FEE2E2', ic:'#EF4444', title:`${noActYet.length} students with 0 activities`, sub:'Check in or send a nudge', to:'students', urgent:true },
      atRisk.length > 0 && { icon:'alertCircle', bg:'#FFF1DC', ic:'#F0982C', title:`${atRisk.length} student inactive this week`, sub:'Last activity was 10+ days ago', to:'students', urgent:false },
      { icon:'aiconfig', bg:'#EDEBFF', ic:'#5B4BE8', title:'AI config: 3 categories not set', sub:'Default LLM only — set overrides', to:'ai-config', urgent:false },
      { icon:'shieldCheck', bg:'#E0F6EE', ic:'#13B07C', title:'System health: all clear', sub:'0 errors in the last 24 hours', to:'diagnostics', urgent:false },
    ].filter(Boolean);
    return (
      <div className="adm-card adm-card-p">
        <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:14 }}>
          <span style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)' }}>Admin actions</span>
          <span className={`adm-badge ${actions.some(a=>a.urgent) ? 'adm-badge-warn' : 'adm-badge-success'}`}>
            {actions.filter(a=>a.urgent).length > 0 ? `${actions.filter(a=>a.urgent).length} urgent` : 'All clear'}
          </span>
        </div>
        <div style={{ display:'flex', flexDirection:'column', gap:0 }}>
          {actions.map((a, i) => (
            <div key={i} onClick={() => navigate(a.to)}
              style={{ display:'flex', alignItems:'center', gap:12, padding:'10px 0',
                borderBottom: i<actions.length-1 ? '1px solid var(--border)' : 'none',
                cursor:'pointer' }}
              onMouseOver={e=>e.currentTarget.style.opacity='.75'}
              onMouseOut={e=>e.currentTarget.style.opacity='1'}>
              <div style={{ width:30, height:30, borderRadius:8, background:a.bg, display:'grid', placeItems:'center', flexShrink:0 }}>
                <AIcon n={a.icon} s={14} c={a.ic} w={2}/>
              </div>
              <div style={{ flex:1, minWidth:0 }}>
                <div style={{ fontSize:13, fontWeight:700, color: a.urgent?'var(--ink)':'var(--text)', lineHeight:1.2 }}>{a.title}</div>
                <div style={{ fontSize:11.5, color:'var(--muted)', marginTop:1 }}>{a.sub}</div>
              </div>
              <AIcon n="arrowRight" s={14} c="var(--faint)"/>
            </div>
          ))}
        </div>
      </div>
    );
  }

  // ── METRIC STRIP ──────────────────────────────────────────────
  function MetricStrip() {
    return (
      <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap:14, marginBottom:16 }}>
        {/* Engagement */}
        <div className="adm-card adm-card-p">
          <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:10 }}>
            <span style={{ fontSize:13, fontWeight:700, color:'var(--ink)' }}>Cohort engagement</span>
            <span className="adm-badge adm-badge-success">{engagePct}%</span>
          </div>
          <div style={{ display:'flex', height:6, gap:3, borderRadius:99, overflow:'hidden', marginBottom:8 }}>
            {students.map(s=>(
              <div key={s.id} style={{ flex:1, borderRadius:99,
                background: studentStatus(s)==='active'?'#5B4BE8':studentStatus(s)==='at-risk'?'#F0982C':'#ECE9F5' }}/>
            ))}
          </div>
          <div style={{ display:'flex', gap:10 }}>
            {[['Active','#5B4BE8',students.filter(s=>studentStatus(s)==='active').length],
              ['At risk','#F0982C',students.filter(s=>studentStatus(s)==='at-risk').length],
              ['Inactive','#ECE9F5',students.filter(s=>studentStatus(s)==='inactive').length]].map(([l,c,n])=>(
              <div key={l} style={{ display:'flex', alignItems:'center', gap:4 }}>
                <div style={{ width:8, height:8, borderRadius:2, background:c }}/>
                <span style={{ fontSize:11.5, color:'var(--muted)' }}>{l}</span>
                <span style={{ fontSize:12, fontWeight:800, color:'var(--ink)' }}>{n}</span>
              </div>
            ))}
          </div>
        </div>
        {/* Cost sparkline */}
        <div className="adm-card adm-card-p" style={{ display:'flex', alignItems:'center', gap:14 }}>
          <div style={{ flex:1 }}>
            <div style={{ fontSize:13, fontWeight:700, color:'var(--ink)', marginBottom:5 }}>AI spend (30d)</div>
            <div style={{ fontSize:22, fontWeight:800, color:'var(--ink)', letterSpacing:'-.03em' }}>${cost30d.toFixed(2)}</div>
            <div style={{ fontSize:12, color:'var(--muted)', marginTop:4 }}>
              ${(cost30d/totalStudents).toFixed(2)}/student
            </div>
          </div>
          <Sparkline data={aiCost30d.slice(-14)} color="#F0982C" W={80} H={32}/>
        </div>
        {/* CEFR */}
        <div className="adm-card adm-card-p">
          <div style={{ fontSize:13, fontWeight:700, color:'var(--ink)', marginBottom:10 }}>CEFR distribution</div>
          {[
            { level:'B2', count:3, color:'#5B4BE8', bg:'#EDEBFF' },
            { level:'B1', count:1, color:'#5B4BE8', bg:'#EDEBFF' },
            { level:'A2', count:1, color:'#0A7468', bg:'#DFF6F2' },
            { level:'—',  count:3, color:'#BDB8CC', bg:'#F6F4FB' },
          ].map(({ level, count, color, bg })=>(
            <div key={level} style={{ display:'flex', alignItems:'center', gap:10, marginBottom:7 }}>
              <span style={{ fontSize:11, fontWeight:700, padding:'2px 7px', borderRadius:5, background:bg, color, minWidth:36, textAlign:'center' }}>{level}</span>
              <div style={{ flex:1, height:5, borderRadius:99, background:'#F0EEF8', overflow:'hidden' }}>
                <div style={{ height:'100%', borderRadius:99, background:color, width:`${(count/totalStudents)*100}%` }}/>
              </div>
              <span style={{ fontSize:12, fontWeight:800, color:'var(--ink)', minWidth:14, textAlign:'right' }}>{count}</span>
            </div>
          ))}
        </div>
      </div>
    );
  }

  // ── STUDENTS TABLE ────────────────────────────────────────────
  function StudentsTable({ navigate }) {
    const STATUS_COLORS = { active:'#13B07C', 'at-risk':'#F0982C', inactive:'#BDB8CC' };
    const STATUS_LABELS = { active:'Active', 'at-risk':'At risk', inactive:'Inactive' };
    return (
      <div className="adm-card">
        <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', padding:'16px 16px 0' }}>
          <span style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)' }}>Students</span>
          <button className="adm-card-link" style={{ fontSize:13 }} onClick={()=>navigate('students')}>Manage all →</button>
        </div>
        <div style={{ display:'flex', alignItems:'center', gap:12, padding:'10px 16px 6px',
          fontSize:10.5, fontWeight:800, color:'var(--muted)', letterSpacing:'.07em', textTransform:'uppercase',
          borderBottom:'1px solid var(--border)' }}>
          <div style={{ width:30 }}/>
          <div style={{ flex:1 }}>Student</div>
          <div style={{ width:40, textAlign:'center' }}>CEFR</div>
          <div style={{ width:48, textAlign:'right' }}>Acts</div>
          <div style={{ width:54, textAlign:'right' }}>Joined</div>
          <div style={{ width:70 }}>Status</div>
        </div>
        {students.map(s => (
          <div key={s.id} onClick={()=>navigate('students')}
            style={{ display:'flex', alignItems:'center', gap:12, padding:'10px 16px',
              borderBottom:'1px solid var(--border)', cursor:'pointer', transition:'background .08s' }}
            onMouseOver={e=>e.currentTarget.style.background='#FAFAFE'}
            onMouseOut={e=>e.currentTarget.style.background='transparent'}>
            <div style={{ width:30, height:30, borderRadius:8, background:'#EDEBFF', flexShrink:0,
              display:'grid', placeItems:'center', fontSize:12, fontWeight:800, color:'#3A2EA8' }}>
              {(s.name!==s.email?s.name:s.email)[0].toUpperCase()}
            </div>
            <div style={{ flex:1, minWidth:0 }}>
              <div style={{ fontSize:13, fontWeight:700, color:'var(--ink)', lineHeight:1.2 }}>
                {s.name!==s.email?s.name:s.email.split('@')[0]}
              </div>
              <div style={{ fontSize:11.5, color:'var(--muted)', overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap' }}>{s.email}</div>
            </div>
            <div style={{ width:40, textAlign:'center' }}>
              {s.cefr ? <span className={`adm-cefr ${cefrClass(s.cefr)}`} style={{ fontSize:11 }}>{s.cefr}</span>
                : <span style={{ color:'var(--faint)', fontSize:13 }}>—</span>}
            </div>
            <div style={{ width:48, textAlign:'right', fontSize:13, fontWeight:700, color:'var(--ink)' }}>{s.activitiesDone}</div>
            <div style={{ width:54, textAlign:'right', fontSize:12, color:'var(--muted)' }}>{relTime(s.joined)}</div>
            <div style={{ width:70, display:'flex', alignItems:'center', gap:5 }}>
              <span style={{ width:7, height:7, borderRadius:'50%', background:STATUS_COLORS[studentStatus(s)], flexShrink:0 }}/>
              <span style={{ fontSize:12, fontWeight:600, color:STATUS_COLORS[studentStatus(s)] }}>{STATUS_LABELS[studentStatus(s)]}</span>
            </div>
          </div>
        ))}
      </div>
    );
  }

  // ── LIVE EVENTS ───────────────────────────────────────────────
  const FEED = [
    { id:'f1', icon:'check',    bg:'#E0F6EE', ic:'#13B07C', title:'Onboarding complete', sub:'qa.fullaudit2@example.com', time:'2m ago' },
    { id:'f2', icon:'pen',      bg:'#EDEBFF', ic:'#5B4BE8', title:'WritingScenario · 91/100', sub:'QA FullAudit2', time:'8m ago' },
    { id:'f3', icon:'check',    bg:'#E0F6EE', ic:'#13B07C', title:'Onboarding complete', sub:'qa.fullaudit@example.com', time:'14m ago' },
    { id:'f4', icon:'createstu',bg:'#EDEBFF', ic:'#5B4BE8', title:'New student signed up', sub:'qa.fullaudit2@example.com', time:'23m ago' },
    { id:'f5', icon:'target',   bg:'#F2E9FF', ic:'#B45CF0', title:'CEFR placed: B2', sub:'qa.fullaudit2@example.com', time:'31m ago' },
    { id:'f6', icon:'pen',      bg:'#EDEBFF', ic:'#5B4BE8', title:'WritingScenario · 86/100', sub:'QA FullAudit', time:'44m ago' },
    { id:'f7', icon:'alertCircle', bg:'#FFF1DC', ic:'#F0982C', title:'Rate limit 89% RPM', sub:'Resolved automatically', time:'1h ago' },
    { id:'f8', icon:'shieldCheck', bg:'#E0F6EE', ic:'#13B07C', title:'Health check passed', sub:'All services operational', time:'2h ago' },
  ];

  function LiveFeed() {
    return (
      <div className="adm-card" style={{ display:'flex', flexDirection:'column', maxHeight:460 }}>
        <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', padding:'16px 16px 0', flexShrink:0 }}>
          <span style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)' }}>Live events</span>
          <span style={{ fontSize:12, color:'#13B07C', fontWeight:600, display:'flex', alignItems:'center', gap:4 }}>
            <span className="adm-dot adm-dot-g adm-dot-pulse"/>Live
          </span>
        </div>
        <div style={{ flex:1, overflowY:'auto', paddingTop:8 }}>
          {FEED.map((e, i) => (
            <div key={e.id} style={{ display:'flex', alignItems:'flex-start', gap:10, padding:'9px 16px',
              borderBottom: i<FEED.length-1 ? '1px solid var(--border)' : 'none' }}>
              <div style={{ width:27, height:27, borderRadius:7, background:e.bg, display:'grid', placeItems:'center', flexShrink:0, marginTop:1 }}>
                <AIcon n={e.icon} s={13} c={e.ic} w={2}/>
              </div>
              <div style={{ flex:1, minWidth:0 }}>
                <div style={{ fontSize:12.5, fontWeight:700, color:'var(--ink)', lineHeight:1.3 }}>{e.title}</div>
                <div style={{ fontSize:11.5, color:'var(--muted)', marginTop:1, overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap' }}>{e.sub}</div>
              </div>
              <span style={{ fontSize:11, color:'var(--faint)', whiteSpace:'nowrap', marginTop:2 }}>{e.time}</span>
            </div>
          ))}
        </div>
      </div>
    );
  }

  // ── DASHBOARD ─────────────────────────────────────────────────
  function AdminDashboard({ navigate }) {
    return (
      <div>
        <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:20 }}>
          <div>
            <h1 style={{ fontSize:23, fontWeight:800, color:'var(--ink)', letterSpacing:'-.035em' }}>Dashboard</h1>
            <p style={{ fontSize:13, color:'var(--muted)', marginTop:3 }}>SpeakPath Admin · Jun 23, 2026</p>
          </div>
          <div/>
        </div>

        {/* ── Weekly snapshot banner ── */}
        <WeeklySnapshot navigate={navigate}/>

        {/* ── KPIs ── */}
        <div style={{ display:'grid', gridTemplateColumns:'repeat(4,1fr)', gap:14, marginBottom:20 }}>
          <KpiCard icon="students"   iconBg="#EDEBFF" iconColor="#5B4BE8" label="Total students" value={totalStudents} delta="Pilot cohort"/>
          <KpiCard icon="flame"      iconBg="#FFEAE4" iconColor="#FF7A59" label="Active this week" value={`${activeWeek}/${totalStudents}`} delta={`${engagePct}% engagement`} deltaColor="#13B07C"/>
          <KpiCard icon="activity2"  iconBg="#F2E9FF" iconColor="#B45CF0" label="Activities done" value={totalActs} delta="+12 since yesterday" deltaColor="#13B07C"/>
          <KpiCard icon="dollarSign" iconBg="#FFF1DC" iconColor="#F0982C" label="AI cost (7 days)" value={`$${cost7d.toFixed(2)}`} delta={`$${(cost7d/Math.max(activeWeek,1)).toFixed(2)} per active student`}/>
        </div>

        {/* ── Activity chart + System health ── */}
        <div style={{ display:'grid', gridTemplateColumns:'1fr 272px', gap:16, marginBottom:16 }}>
          <div className="adm-card adm-card-p">
            <div style={{ display:'flex', alignItems:'flex-start', justifyContent:'space-between', marginBottom:16 }}>
              <div>
                <div style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)' }}>Activities completed</div>
                <div style={{ fontSize:12, color:'var(--muted)', marginTop:2 }}>Past 14 days</div>
              </div>
              <div style={{ textAlign:'right' }}>
                <div style={{ fontSize:22, fontWeight:800, color:'var(--ink)', letterSpacing:'-.03em' }}>{actPerDay14.reduce((a,b)=>a+b,0)}</div>
                <div style={{ fontSize:11.5, fontWeight:700, color:'#13B07C' }}>↑ 18% vs prev period</div>
              </div>
            </div>
            <AreaChart data={actPerDay14} labels={DAY_LABELS} color="#5B4BE8" H={150}/>
          </div>
          <SystemHealth navigate={navigate}/>
        </div>

        {/* ── Funnel + At-risk + Score dist ── */}
        <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap:14, marginBottom:16 }}>
          <OnboardingFunnel/>
          <AtRiskAlerts navigate={navigate}/>
          <ScoreDistribution/>
        </div>

        {/* ── Cost by type + Session + Streak + Pending actions ── */}
        <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr 1fr 1fr', gap:14, marginBottom:16 }}>
          <CostByType/>
          <SessionDuration/>
          <StreakLeaderboard/>
          <PendingActions navigate={navigate}/>
        </div>

        {/* ── Metric strip ── */}
        <MetricStrip/>

        {/* ── Students + Feed ── */}
        <div style={{ display:'grid', gridTemplateColumns:'3fr 2fr', gap:16 }}>
          <StudentsTable navigate={navigate}/>
          <LiveFeed/>
        </div>
      </div>
    );
  }

  window.AdminDashboard = AdminDashboard;
})();
