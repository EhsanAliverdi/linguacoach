// SpeakPath Admin — Integrations page
(function () {
  const { useState } = React;
  const { AIcon } = window;

  const INTEGRATIONS = [
    {
      id: 'smtp', name: 'SMTP Email', icon: 'mail', bg: '#EDEBFF', ic: '#5B4BE8',
      status: 'configured', statusLabel: 'Configured',
      desc: 'Transactional emails for student notifications and admin alerts.',
      meta: 'smtp.speakpath.app · Port 587',
    },
    {
      id: 'webhook', name: 'Webhook', icon: 'webhook', bg: '#FFF1DC', ic: '#F0982C',
      status: 'connected', statusLabel: 'Connected',
      desc: 'Send event payloads to your own endpoint for custom automation.',
      meta: 'hooks.speakpath.app/webhook/abc123',
    },
    {
      id: 'slack', name: 'Slack', icon: 'slack2', bg: '#F2E9FF', ic: '#B45CF0',
      status: 'disconnected', statusLabel: 'Not connected',
      desc: 'Get admin alerts and weekly digests in your Slack workspace.',
      meta: null,
    },
    {
      id: 'analytics', name: 'Analytics', icon: 'aiusage', bg: '#E0F6EE', ic: '#13B07C',
      status: 'disconnected', statusLabel: 'Not connected',
      desc: 'Connect Google Analytics or Mixpanel for funnel and retention data.',
      meta: null,
    },
  ];

  function IntegrationCard({ int }) {
    const [open, setOpen] = useState(false);
    const isConnected = int.status !== 'disconnected';

    return (
      <div className={`adm-int-card${isConnected ? ' connected' : ''}`}>
        <div className="adm-int-header">
          <div className="adm-int-ico" style={{ background: int.bg }}>
            <AIcon n={int.icon} s={22} c={int.ic} w={1.75}/>
          </div>
          <div className="adm-int-meta">
            <div className="adm-int-name">{int.name}</div>
            <span className={`adm-badge ${isConnected ? 'adm-badge-success' : 'adm-badge-muted'}`}>
              {isConnected && <span className="adm-dot adm-dot-g adm-dot-pulse" style={{ marginRight:4 }}/>}
              {int.statusLabel}
            </span>
          </div>
        </div>
        <div className="adm-int-sub">{int.desc}</div>
        {int.meta && (
          <div style={{
            fontFamily:"'JetBrains Mono',monospace", fontSize:11.5, color:'#8B85A0',
            background:'#F6F4FB', borderRadius:6, padding:'6px 10px',
            overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
          }}>
            {int.meta}
          </div>
        )}
        <div style={{ display:'flex', gap:8, marginTop:4 }}>
          {isConnected ? (
            <>
              <button className="adm-btn adm-btn-ghost adm-btn-sm" style={{ flex:1 }}>
                <AIcon n="settings" s={13}/>
                Manage
              </button>
              <button className="adm-btn adm-btn-ghost adm-btn-sm" style={{ color:'#DC2626' }}>
                Disconnect
              </button>
            </>
          ) : (
            <button className="adm-btn adm-btn-indigo adm-btn-sm" style={{ flex:1 }}>
              <AIcon n="plus" s={13} c="#fff"/>
              Connect
            </button>
          )}
        </div>
      </div>
    );
  }

  function AdminIntegrations() {
    return (
      <div>
        <div className="adm-page-header">
          <div>
            <h1 className="adm-page-h1">Integrations</h1>
            <p className="adm-page-sub">Connect SpeakPath to external services and tools</p>
          </div>
        </div>

        <div className="adm-int-grid" style={{ marginBottom:28 }}>
          {INTEGRATIONS.map(int => (
            <IntegrationCard key={int.id} int={int}/>
          ))}
        </div>

        {/* API access */}
        <div className="adm-card adm-card-p">
          <div className="adm-card-header">
            <div>
              <div className="adm-card-title">Admin API</div>
              <div style={{ fontSize:12.5, color:'#8B85A0', marginTop:2 }}>
                Use the REST API to manage students and configuration programmatically
              </div>
            </div>
            <span className="adm-badge adm-badge-success">Enabled</span>
          </div>
          <div className="adm-form-group" style={{ marginBottom:16 }}>
            <label className="adm-form-lbl">API Key</label>
            <div style={{ display:'flex', gap:8 }}>
              <div className="adm-code" style={{ flex:1, padding:'9px 14px', borderRadius:8, fontSize:12.5, lineHeight:1.4 }}>
                sp_admin_••••••••••••••••••••••••••••••
              </div>
              <button className="adm-btn adm-btn-ghost adm-btn-sm"><AIcon n="copy" s={14}/>Copy</button>
              <button className="adm-btn adm-btn-ghost adm-btn-sm"><AIcon n="refresh" s={14}/>Rotate</button>
            </div>
          </div>
          <div>
            <div className="adm-form-lbl" style={{ marginBottom:8 }}>Base URL</div>
            <div className="adm-code" style={{ padding:'10px 14px', borderRadius:8 }}>
              <span className="cm">GET POST PUT DELETE</span>{'  '}
              https://api.speakpath.app/v1/admin/
            </div>
          </div>
        </div>
      </div>
    );
  }

  window.AdminIntegrations = AdminIntegrations;
})();
