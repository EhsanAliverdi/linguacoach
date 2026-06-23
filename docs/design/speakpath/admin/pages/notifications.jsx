// SpeakPath Admin — Notifications page
(function () {
  const { useState } = React;
  const { AIcon } = window;
  const { notifications: initialNotifs, recentNotifsSent } = window.ADMIN_DATA;

  function Toggle({ on, onChange }) {
    return <button className={`adm-toggle ${on ? 'on' : 'off'}`} onClick={onChange}/>;
  }

  function AdminNotifications() {
    const [notifs, setNotifs] = useState(initialNotifs.map(n => ({ ...n })));
    const [emailEnabled, setEmailEnabled] = useState(true);
    const [webhookEnabled, setWebhookEnabled] = useState(false);
    const [webhookUrl, setWebhookUrl] = useState('https://hooks.speakpath.app/webhook/abc123');

    function toggle(id, field) {
      setNotifs(prev => prev.map(n => n.id === id ? { ...n, [field]: !n[field] } : n));
    }

    return (
      <div>
        <div className="adm-page-header">
          <div>
            <h1 className="adm-page-h1">Notifications</h1>
            <p className="adm-page-sub">Email and webhook notification settings</p>
          </div>
          <button className="adm-btn adm-btn-indigo">Save settings</button>
        </div>

        {/* Channels */}
        <div className="adm-g2" style={{ marginBottom:20 }}>
          {/* Email */}
          <div className="adm-card adm-card-p">
            <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:16 }}>
              <div style={{ display:'flex', alignItems:'center', gap:12 }}>
                <div style={{ width:40, height:40, borderRadius:10, background:'#EDEBFF', display:'grid', placeItems:'center' }}>
                  <AIcon n="mail" s={18} c="#5B4BE8"/>
                </div>
                <div>
                  <div style={{ fontSize:14, fontWeight:700, color:'#211B36' }}>Email notifications</div>
                  <div style={{ fontSize:12.5, color:'#8B85A0' }}>admin@speakpath.app</div>
                </div>
              </div>
              <Toggle on={emailEnabled} onChange={() => setEmailEnabled(v => !v)}/>
            </div>
            <div className="adm-form-group">
              <label className="adm-form-lbl">Admin email address</label>
              <input className="adm-input" defaultValue="admin@speakpath.app"/>
            </div>
          </div>
          {/* Webhook */}
          <div className="adm-card adm-card-p">
            <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:16 }}>
              <div style={{ display:'flex', alignItems:'center', gap:12 }}>
                <div style={{ width:40, height:40, borderRadius:10, background:'#FFF1DC', display:'grid', placeItems:'center' }}>
                  <AIcon n="webhook" s={18} c="#F0982C"/>
                </div>
                <div>
                  <div style={{ fontSize:14, fontWeight:700, color:'#211B36' }}>Webhook</div>
                  <div style={{ fontSize:12.5, color:'#8B85A0' }}>POST JSON to your endpoint</div>
                </div>
              </div>
              <Toggle on={webhookEnabled} onChange={() => setWebhookEnabled(v => !v)}/>
            </div>
            <div className="adm-form-group" style={{ marginBottom:10 }}>
              <label className="adm-form-lbl">Endpoint URL</label>
              <div style={{ display:'flex', gap:8 }}>
                <input className="adm-input" value={webhookUrl} onChange={e => setWebhookUrl(e.target.value)}
                  disabled={!webhookEnabled} style={{ fontFamily:"'JetBrains Mono',monospace", fontSize:12 }}/>
                <button className="adm-btn adm-btn-ghost adm-btn-sm" disabled={!webhookEnabled}
                  style={{ flexShrink:0 }}>Test</button>
              </div>
            </div>
          </div>
        </div>

        {/* Event triggers */}
        <div className="adm-card adm-card-p" style={{ marginBottom:20 }}>
          <div className="adm-card-title" style={{ marginBottom:20 }}>Notification triggers</div>
          {notifs.map(n => (
            <div key={n.id} className="adm-notif-event-row">
              <div style={{ flex:1 }}>
                <div className="adm-notif-label">{n.event}</div>
                <div className="adm-notif-sub">{n.description}</div>
              </div>
              <div className="adm-notif-channels">
                <div style={{ display:'flex', alignItems:'center', gap:6 }}>
                  <span style={{ fontSize:12, color:'#8B85A0', fontWeight:600 }}>Email</span>
                  <Toggle on={n.email && emailEnabled} onChange={() => toggle(n.id, 'email')}/>
                </div>
                <div style={{ display:'flex', alignItems:'center', gap:6 }}>
                  <span style={{ fontSize:12, color:'#8B85A0', fontWeight:600 }}>Webhook</span>
                  <Toggle on={n.webhook && webhookEnabled} onChange={() => toggle(n.id, 'webhook')}/>
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* Recent sent */}
        <div className="adm-card">
          <div className="adm-card-p" style={{ paddingBottom:0 }}>
            <div className="adm-card-title">Recently sent</div>
          </div>
          <div className="adm-table-wrap">
            <table className="adm-table">
              <thead>
                <tr>
                  <th>Event</th>
                  <th>Recipient</th>
                  <th>Sent</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {recentNotifsSent.map(n => (
                  <tr key={n.id}>
                    <td style={{ fontWeight:600, color:'#211B36' }}>{n.event}</td>
                    <td style={{ color:'#8B85A0', fontSize:13, fontFamily:"'JetBrains Mono',monospace" }}>{n.recipient}</td>
                    <td style={{ color:'#8B85A0', fontSize:13 }}>{n.sent}</td>
                    <td><span className="adm-badge adm-badge-success">{n.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    );
  }

  window.AdminNotifications = AdminNotifications;
})();
