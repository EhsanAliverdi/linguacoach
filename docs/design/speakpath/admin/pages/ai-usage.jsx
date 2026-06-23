// SpeakPath Admin — AI Usage page with charts
(function () {
  const { useState } = React;
  const { AIcon } = window;
  const { aiCost30d, aiCostLabels, actPerDay14, actDayLabels, heatmap7x12, stats } = window.ADMIN_DATA;

  // ── LINE / AREA CHART ─────────────────────────────────────────
  function AreaChart({ data, labels, color = '#5B4BE8', H = 200, prefix = '$' }) {
    const padL = 48; const padR = 16; const padT = 20; const padB = 28;
    const VW = 600; const plotW = VW - padL - padR; const plotH = H - padT - padB;
    const max = Math.max(...data) * 1.15;
    const xS = (i) => padL + (i / (data.length - 1)) * plotW;
    const yS = (v) => padT + plotH - (v / max) * plotH;
    const pts = data.map((v, i) => [xS(i), yS(v)]);
    const line = pts.map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
    const area = `${line} L${pts[pts.length-1][0].toFixed(1)},${padT+plotH} L${padL},${padT+plotH} Z`;
    const yTicks = [0, max * 0.25, max * 0.5, max * 0.75, max].map(v => ({
      v, y: yS(v), label: `${prefix}${v.toFixed(2)}`
    }));
    const showLabels = labels ? labels.map((l, i) => [l, i]).filter(([l]) => !!l) : [];
    return (
      <svg viewBox={`0 0 ${VW} ${H}`} style={{ width:'100%', height:H }}>
        <defs>
          <linearGradient id="aug" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={color} stopOpacity="0.18"/>
            <stop offset="100%" stopColor={color} stopOpacity="0"/>
          </linearGradient>
        </defs>
        {/* Grid */}
        {yTicks.map(({ v, y, label }) => (
          <g key={v}>
            <line x1={padL} y1={y} x2={VW - padR} y2={y} stroke="#ECE9F5" strokeWidth="1"/>
            <text x={padL - 6} y={y + 4} textAnchor="end" fontSize="10"
              fill="#8B85A0" fontFamily="'Plus Jakarta Sans',sans-serif">{label}</text>
          </g>
        ))}
        <path d={area} fill="url(#aug)"/>
        <path d={line} fill="none" stroke={color} strokeWidth="2.5"
          strokeLinecap="round" strokeLinejoin="round"/>
        {/* Last dot */}
        <circle cx={pts[pts.length-1][0]} cy={pts[pts.length-1][1]} r="5"
          fill={color} stroke="#fff" strokeWidth="2.5"/>
        {/* X labels */}
        {showLabels.map(([l, i]) => (
          <text key={i} x={xS(i)} y={H - 4} textAnchor="middle" fontSize="10"
            fill="#8B85A0" fontFamily="'Plus Jakarta Sans',sans-serif">{l}</text>
        ))}
      </svg>
    );
  }

  // ── BAR CHART ─────────────────────────────────────────────────
  function BarChart({ data, labels, color = '#5B4BE8', H = 160 }) {
    const max = Math.max(...data) || 1;
    const bw = 24; const gap = 8;
    const VW = data.length * (bw + gap) - gap + 4;
    return (
      <svg viewBox={`0 0 ${VW} ${H + 22}`} style={{ width:'100%', height: H + 22 }}>
        {data.map((v, i) => {
          const bh = Math.max(3, (v / max) * H);
          const x = i * (bw + gap);
          const isMax = v === max;
          return (
            <g key={i}>
              <rect x={x} y={H - bh} width={bw} height={bh} rx="5"
                fill={isMax ? color : `${color}55`}/>
              {v > 0 && <text x={x + bw/2} y={H - bh - 6} textAnchor="middle"
                fontSize="9.5" fontWeight={isMax ? '800' : '700'}
                fill={isMax ? color : '#8B85A0'}
                fontFamily="'Plus Jakarta Sans',sans-serif">{v}</text>}
              {labels && labels[i] && (
                <text x={x + bw/2} y={H + 16} textAnchor="middle" fontSize="9.5" fill="#8B85A0"
                  fontFamily="'Plus Jakarta Sans',sans-serif">{labels[i]}</text>
              )}
            </g>
          );
        })}
      </svg>
    );
  }

  // ── HEATMAP ───────────────────────────────────────────────────
  function Heatmap({ data }) {
    const days = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];
    const W = data[0].length; const cellSz = 14; const gap = 3;
    const maxVal = Math.max(...data.flat()) || 1;
    const totalW = 28 + W * (cellSz + gap);
    const totalH = days.length * (cellSz + gap);
    return (
      <div style={{ overflowX:'auto' }}>
        <svg width={totalW} height={totalH + 4} style={{ display:'block' }}>
          {days.map((d, di) => (
            <text key={d} x={0} y={di * (cellSz + gap) + cellSz - 2}
              fontSize="9.5" fill="#8B85A0" fontFamily="'Plus Jakarta Sans',sans-serif">{d}</text>
          ))}
          {data.map((row, di) =>
            row.map((v, wi) => {
              const opacity = v === 0 ? 0 : 0.15 + (v / maxVal) * 0.85;
              return (
                <rect key={`${di}-${wi}`}
                  x={28 + wi * (cellSz + gap)} y={di * (cellSz + gap)}
                  width={cellSz} height={cellSz} rx={3}
                  fill={v === 0 ? '#ECE9F5' : '#5B4BE8'}
                  opacity={v === 0 ? 1 : opacity}/>
              );
            })
          )}
        </svg>
        <div style={{ display:'flex', justifyContent:'space-between', paddingLeft:28, marginTop:8, fontSize:10, color:'#8B85A0', fontFamily:"'Plus Jakarta Sans',sans-serif" }}>
          <span>12 weeks ago</span><span>This week</span>
        </div>
      </div>
    );
  }

  // ── KPI MINI CARD ─────────────────────────────────────────────
  function UsageKpi({ icon, iconBg, iconColor, value, label, sub }) {
    return (
      <div className="adm-card adm-card-p">
        <div style={{ display:'flex', alignItems:'center', gap:12, marginBottom:12 }}>
          <div className="adm-kpi-icon" style={{ background:iconBg }}>
            <AIcon n={icon} s={18} c={iconColor} w={2}/>
          </div>
        </div>
        <div className="adm-kpi-val">{value}</div>
        <div className="adm-kpi-label">{label}</div>
        {sub && <div style={{ fontSize:12, color:'#8B85A0', marginTop:4 }}>{sub}</div>}
      </div>
    );
  }

  function AdminAIUsage() {
    const [range, setRange] = useState('30d');
    const totalCost = aiCost30d.reduce((a,b) => a+b, 0);

    const displayData = range === '7d' ? aiCost30d.slice(-7) : range === '90d' ? [...aiCost30d, ...aiCost30d, ...aiCost30d] : aiCost30d;
    const displayLabels = range === '7d' ? aiCostLabels.slice(-7) : aiCostLabels;

    return (
      <div>
        <div className="adm-page-header">
          <div>
            <h1 className="adm-page-h1">AI Usage</h1>
            <p className="adm-page-sub">API usage, costs and student engagement analytics</p>
          </div>
          <div className="adm-date-pills">
            {[['7d','7 days'],['30d','30 days'],['90d','90 days']].map(([v, l]) => (
              <button key={v} className={`adm-date-pill${range === v ? ' active' : ''}`}
                onClick={() => setRange(v)}>{l}</button>
            ))}
          </div>
        </div>

        {/* KPI row */}
        <div className="adm-g3" style={{ marginBottom:20 }}>
          <UsageKpi icon="dollarSign" iconBg="#FFEAE4" iconColor="#FF7A59"
            value={`$${totalCost.toFixed(2)}`} label="Total cost" sub={`Last ${range === '7d' ? '7' : range === '30d' ? '30' : '90'} days`}/>
          <UsageKpi icon="zap" iconBg="#EDEBFF" iconColor="#5B4BE8"
            value={stats.totalCalls30d} label="Total API calls" sub="Avg 28 per day"/>
          <UsageKpi icon="percent" iconBg="#E0F6EE" iconColor="#13B07C"
            value={`$${stats.avgCostPerStudent}`} label="Avg cost per student" sub="Based on active students"/>
        </div>

        {/* Cost line chart */}
        <div className="adm-card adm-card-p" style={{ marginBottom:20 }}>
          <div className="adm-card-header">
            <div>
              <div className="adm-card-title">API cost over time</div>
              <div style={{ fontSize:12, color:'#8B85A0', marginTop:2 }}>Daily spend in USD</div>
            </div>
            <div style={{ textAlign:'right' }}>
              <div style={{ fontSize:20, fontWeight:800, color:'#211B36', letterSpacing:'-.03em' }}>
                ${totalCost.toFixed(2)}
              </div>
              <div style={{ fontSize:12, color:'#13B07C', fontWeight:700 }}>↑ Within budget</div>
            </div>
          </div>
          <div className="adm-chart-area">
            <AreaChart data={displayData} labels={displayLabels} color="#5B4BE8" H={200} prefix="$"/>
          </div>
        </div>

        {/* Bar + Heatmap */}
        <div className="adm-g2-skew">
          <div className="adm-card adm-card-p">
            <div className="adm-card-header">
              <div>
                <div className="adm-card-title">Activities completed per day</div>
                <div style={{ fontSize:12, color:'#8B85A0', marginTop:2 }}>Last 14 days</div>
              </div>
              <div style={{ fontSize:20, fontWeight:800, color:'#211B36', letterSpacing:'-.03em' }}>
                {actPerDay14.reduce((a,b) => a+b, 0)}
              </div>
            </div>
            <BarChart data={actPerDay14} labels={actDayLabels} color="#5B4BE8" H={160}/>
          </div>
          <div className="adm-card adm-card-p">
            <div className="adm-card-header">
              <div>
                <div className="adm-card-title">Student engagement</div>
                <div style={{ fontSize:12, color:'#8B85A0', marginTop:2 }}>Activity by day of week</div>
              </div>
            </div>
            <div style={{ marginTop:8 }}>
              <Heatmap data={heatmap7x12}/>
            </div>
            <div style={{ display:'flex', alignItems:'center', gap:6, marginTop:16 }}>
              <span style={{ fontSize:11.5, color:'#8B85A0' }}>Less</span>
              {[0,.2,.4,.65,.9].map((o, i) => (
                <div key={i} style={{ width:12, height:12, borderRadius:3,
                  background: o === 0 ? '#ECE9F5' : `rgba(91,75,232,${o})` }}/>
              ))}
              <span style={{ fontSize:11.5, color:'#8B85A0' }}>More</span>
            </div>
          </div>
        </div>
      </div>
    );
  }

  window.AdminAIUsage = AdminAIUsage;
})();
