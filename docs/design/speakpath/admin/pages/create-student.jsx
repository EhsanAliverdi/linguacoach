// SpeakPath Admin — Create Student (production SaaS redesign)
(function () {
  const { useState } = React;
  const { AIcon } = window;

  function Toggle({ on, onChange, label, sub }) {
    return (
      <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between',
        padding:'13px 0', borderBottom:'1px solid var(--border)' }}>
        <div>
          <div style={{ fontSize:13.5, fontWeight:600, color:'var(--ink)' }}>{label}</div>
          {sub && <div style={{ fontSize:12, color:'var(--muted)', marginTop:2 }}>{sub}</div>}
        </div>
        <button
          onClick={() => onChange(!on)}
          style={{
            width:40, height:23, borderRadius:99, border:'none', cursor:'pointer',
            position:'relative', padding:0, flexShrink:0,
            background: on ? '#5B4BE8' : '#DDD9EC',
            transition:'background .18s',
          }}>
          <span style={{
            position:'absolute', top:3, left: on ? 20 : 3,
            width:17, height:17, borderRadius:'50%',
            background:'#fff', boxShadow:'0 1px 3px rgba(0,0,0,.15)',
            transition:'left .18s',
          }}/>
        </button>
      </div>
    );
  }

  function FormSection({ title, sub, children }) {
    return (
      <div className="adm-card adm-card-p" style={{ marginBottom:16 }}>
        <div style={{ marginBottom:20 }}>
          <div style={{ fontSize:14.5, fontWeight:800, color:'var(--ink)' }}>{title}</div>
          {sub && <div style={{ fontSize:13, color:'var(--muted)', marginTop:3 }}>{sub}</div>}
        </div>
        {children}
      </div>
    );
  }

  function Field({ label, hint, required, children, half }) {
    return (
      <div style={{ display:'flex', flexDirection:'column', gap:6, flex: half ? '1 1 calc(50% - 8px)' : '1 1 100%' }}>
        <label style={{ fontSize:13, fontWeight:700, color:'var(--ink)', display:'flex', gap:4, alignItems:'center' }}>
          {label}
          {required && <span style={{ color:'#EF4444', fontSize:13 }}>*</span>}
        </label>
        {children}
        {hint && <span style={{ fontSize:12, color:'var(--muted)' }}>{hint}</span>}
      </div>
    );
  }

  function AdminCreateStudent({ navigate }) {
    // Account
    const [email, setEmail]         = useState('');
    const [password, setPassword]   = useState('');
    const [showPw, setShowPw]       = useState(false);
    const [requireChg, setRequireChg] = useState(true);
    const [sendEmail, setSendEmail] = useState(true);

    // Profile
    const [displayName, setDisplayName] = useState('');
    const [jobTitle, setJobTitle]       = useState('');
    const [industry, setIndustry]       = useState('');
    const [company, setCompany]         = useState('');
    const [yearsExp, setYearsExp]       = useState('');

    // Learning context
    const [nativeLang, setNativeLang]   = useState('');
    const [knownLevel, setKnownLevel]   = useState('');
    const [goal, setGoal]               = useState('');
    const [weeklyGoal, setWeeklyGoal]   = useState('3');

    // Admin
    const [notes, setNotes]             = useState('');
    const [lifecycle, setLifecycle]     = useState('Onboarding required');
    const [cohort, setCohort]           = useState('');

    const [errors, setErrors]   = useState({});
    const [submitted, setSubmitted] = useState(false);

    function validate() {
      const errs = {};
      if (!email) errs.email = 'Email is required';
      else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errs.email = 'Enter a valid email address';
      if (!password) errs.password = 'Temporary password is required';
      else if (password.length < 8) errs.password = 'Minimum 8 characters';
      else if (!/\d/.test(password)) errs.password = 'Must include at least one number';
      return errs;
    }

    function handleSubmit(e) {
      e.preventDefault();
      const errs = validate();
      setErrors(errs);
      if (Object.keys(errs).length > 0) return;
      setSubmitted(true);
      setTimeout(() => navigate('students'), 1800);
    }

    if (submitted) {
      return (
        <div style={{ maxWidth:520, margin:'80px auto', textAlign:'center', padding:'0 20px' }}>
          <div style={{ width:64, height:64, borderRadius:16, background:'#E0F6EE',
            display:'grid', placeItems:'center', margin:'0 auto 20px' }}>
            <AIcon n="check" s={28} c="#13B07C" w={2.5}/>
          </div>
          <div style={{ fontSize:22, fontWeight:800, color:'var(--ink)', marginBottom:8 }}>Student created</div>
          <div style={{ fontSize:14, color:'var(--muted)', lineHeight:1.6 }}>
            {email} has been added to the pilot cohort.{sendEmail ? ' A welcome email is on its way.' : ''} Redirecting to student list…
          </div>
        </div>
      );
    }

    return (
      <div>
        <div style={{ marginBottom:24 }}>
          <button className="adm-btn adm-btn-ghost adm-btn-sm" onClick={() => navigate('students')}
            style={{ marginBottom:16 }}>
            <AIcon n="arrowLeft" s={14}/>Back to Students
          </button>
          <h1 style={{ fontSize:24, fontWeight:800, color:'var(--ink)', letterSpacing:'-.035em' }}>Create student</h1>
          <p style={{ fontSize:13, color:'var(--muted)', marginTop:4 }}>Add a new pilot account to the SpeakPath platform</p>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <div style={{ display:'grid', gridTemplateColumns:'1fr 300px', gap:20, alignItems:'start' }}>
            {/* ── LEFT: form sections ── */}
            <div>

              {/* 1. Account credentials */}
              <FormSection title="Account credentials"
                sub="The student will use these to log in. They'll be prompted to change their password on first login.">
                <div style={{ display:'flex', flexWrap:'wrap', gap:16, marginBottom:16 }}>
                  <Field label="Student email" required>
                    <input className="adm-input" type="email" placeholder="student@company.com"
                      value={email} onChange={e => { setEmail(e.target.value); setErrors(x => ({...x, email:null})); }}
                      style={{ borderColor: errors.email ? '#EF4444' : undefined }}/>
                    {errors.email && <span style={{ fontSize:12, color:'#EF4444' }}>{errors.email}</span>}
                  </Field>
                </div>
                <div style={{ display:'flex', flexWrap:'wrap', gap:16, marginBottom:4 }}>
                  <Field label="Temporary password" required
                    hint="Min 8 characters, at least one number">
                    <div style={{ position:'relative' }}>
                      <input className="adm-input" type={showPw ? 'text' : 'password'}
                        placeholder="Min 8 characters, include a number"
                        value={password} onChange={e => { setPassword(e.target.value); setErrors(x => ({...x, password:null})); }}
                        style={{ borderColor: errors.password ? '#EF4444' : undefined, paddingRight:40 }}/>
                      <button type="button" onClick={() => setShowPw(v => !v)}
                        style={{ position:'absolute', right:10, top:'50%', transform:'translateY(-50%)',
                          background:'none', border:'none', cursor:'pointer', color:'var(--muted)', padding:0 }}>
                        <AIcon n="eye" s={16} c="currentColor"/>
                      </button>
                    </div>
                    {errors.password && <span style={{ fontSize:12, color:'#EF4444' }}>{errors.password}</span>}
                  </Field>
                </div>
                <div style={{ borderTop:'1px solid var(--border)', marginTop:16 }}>
                  <Toggle on={requireChg} onChange={setRequireChg}
                    label="Require password change on first login"
                    sub="Recommended for real students. Disable for test accounts."/>
                  <Toggle on={sendEmail} onChange={setSendEmail}
                    label="Send welcome email"
                    sub="Sends login link and onboarding instructions to the student."/>
                </div>
              </FormSection>

              {/* 2. Student profile */}
              <FormSection title="Student profile"
                sub="Profile information helps personalise the learning path. Pre-filling these fields skips the corresponding onboarding steps.">
                <div style={{ display:'flex', flexWrap:'wrap', gap:16, marginBottom:16 }}>
                  <Field label="Display name" half>
                    <input className="adm-input" placeholder="e.g. Alice Nguyen"
                      value={displayName} onChange={e => setDisplayName(e.target.value)}/>
                  </Field>
                  <Field label="Job title" half>
                    <input className="adm-input" placeholder="e.g. Senior Engineer"
                      value={jobTitle} onChange={e => setJobTitle(e.target.value)}/>
                  </Field>
                </div>
                <div style={{ display:'flex', flexWrap:'wrap', gap:16, marginBottom:16 }}>
                  <Field label="Industry" half>
                    <select className="adm-select" style={{ width:'100%' }} value={industry} onChange={e => setIndustry(e.target.value)}>
                      <option value="">Select industry</option>
                      {['Technology','Finance & Fintech','Healthcare','Legal','Education','Marketing & Media','Retail & E-commerce','Engineering','Other'].map(o => <option key={o}>{o}</option>)}
                    </select>
                  </Field>
                  <Field label="Company" half>
                    <input className="adm-input" placeholder="e.g. Acme Corp"
                      value={company} onChange={e => setCompany(e.target.value)}/>
                  </Field>
                </div>
                <div style={{ display:'flex', flexWrap:'wrap', gap:16 }}>
                  <Field label="Years of work experience" half>
                    <select className="adm-select" style={{ width:'100%' }} value={yearsExp} onChange={e => setYearsExp(e.target.value)}>
                      <option value="">Not specified</option>
                      <option>0–1 years (early career)</option>
                      <option>2–5 years (junior–mid)</option>
                      <option>6–10 years (mid–senior)</option>
                      <option>10+ years (senior–lead)</option>
                    </select>
                  </Field>
                </div>
              </FormSection>

              {/* 3. Learning context */}
              <FormSection title="Learning context"
                sub="Used to personalise exercises and set the right CEFR baseline. Leave blank to let the student complete the placement assessment.">
                <div style={{ display:'flex', flexWrap:'wrap', gap:16, marginBottom:16 }}>
                  <Field label="Native language" half>
                    <select className="adm-select" style={{ width:'100%' }} value={nativeLang} onChange={e => setNativeLang(e.target.value)}>
                      <option value="">Not specified</option>
                      {['Arabic','Farsi / Persian','French','German','Hindi','Italian','Japanese','Korean','Mandarin','Polish','Portuguese','Russian','Spanish','Turkish','Other'].map(o => <option key={o}>{o}</option>)}
                    </select>
                  </Field>
                  <Field label="Known English level" half
                    hint="Skip the placement test and set this directly">
                    <select className="adm-select" style={{ width:'100%' }} value={knownLevel} onChange={e => setKnownLevel(e.target.value)}>
                      <option value="">Run placement assessment</option>
                      {['A1 — Beginner','A2 — Elementary','B1 — Intermediate','B2 — Upper intermediate','C1 — Advanced','C2 — Proficient'].map(o => <option key={o}>{o}</option>)}
                    </select>
                  </Field>
                </div>
                <div style={{ display:'flex', flexWrap:'wrap', gap:16, marginBottom:16 }}>
                  <Field label="Primary learning goal" half>
                    <select className="adm-select" style={{ width:'100%' }} value={goal} onChange={e => setGoal(e.target.value)}>
                      <option value="">Not specified</option>
                      <option>Professional email & writing</option>
                      <option>Meetings & spoken communication</option>
                      <option>Presentations & public speaking</option>
                      <option>Job interviews</option>
                      <option>General workplace English</option>
                    </select>
                  </Field>
                  <Field label="Weekly sessions target" half>
                    <select className="adm-select" style={{ width:'100%' }} value={weeklyGoal} onChange={e => setWeeklyGoal(e.target.value)}>
                      <option value="2">2 per week</option>
                      <option value="3">3 per week</option>
                      <option value="5">5 per week (daily)</option>
                      <option value="7">7 per week (intensive)</option>
                    </select>
                  </Field>
                </div>
              </FormSection>

              {/* 4. Admin settings */}
              <FormSection title="Admin settings"
                sub="Internal settings — not visible to the student.">
                <div style={{ display:'flex', flexWrap:'wrap', gap:16, marginBottom:16 }}>
                  <Field label="Initial lifecycle stage" half>
                    <select className="adm-select" style={{ width:'100%' }} value={lifecycle} onChange={e => setLifecycle(e.target.value)}>
                      {['Onboarding required','Placement required','Course ready','Active learning'].map(o => <option key={o}>{o}</option>)}
                    </select>
                  </Field>
                  <Field label="Cohort / group tag" half hint="Optional — used for filtering">
                    <input className="adm-input" placeholder="e.g. Q3-2026 pilot"
                      value={cohort} onChange={e => setCohort(e.target.value)}/>
                  </Field>
                </div>
                <Field label="Internal notes">
                  <textarea className="adm-textarea" rows={3}
                    placeholder="Any context about this student (referral source, special requirements, etc.)…"
                    value={notes} onChange={e => setNotes(e.target.value)}/>
                </Field>
              </FormSection>

              {/* Submit */}
              <button type="submit" style={{
                width:'100%', padding:'14px', borderRadius:10, border:'none', cursor:'pointer',
                background:'linear-gradient(135deg, #FF7A59 0%, #B45CF0 52%, #5B4BE8 100%)',
                color:'#fff', fontSize:15, fontWeight:700, fontFamily:'inherit',
                boxShadow:'0 4px 16px rgba(91,75,232,.28)', transition:'opacity .15s',
              }}
              onMouseOver={e => e.currentTarget.style.opacity='.88'}
              onMouseOut={e => e.currentTarget.style.opacity='1'}>
                Create student account
              </button>
            </div>

            {/* ── RIGHT: summary panel ── */}
            <div style={{ position:'sticky', top:80 }}>
              <div className="adm-card adm-card-p" style={{ marginBottom:16 }}>
                <div style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)', marginBottom:16 }}>What happens next</div>
                {[
                  { icon:'check', bg:'#E0F6EE', ic:'#13B07C', title:'Account created', sub:'Student can log in immediately with the temporary password' },
                  ...(sendEmail ? [{ icon:'mail', bg:'#EDEBFF', ic:'#5B4BE8', title:'Welcome email sent', sub:'Includes login link and onboarding instructions' }] : []),
                  ...(requireChg ? [{ icon:'key', bg:'#FFF1DC', ic:'#F0982C', title:'Password reset required', sub:'Student must set a new password before accessing the app' }] : []),
                  ...(knownLevel ? [{ icon:'target', bg:'#F2E9FF', ic:'#B45CF0', title:`CEFR set to ${knownLevel.split(' ')[0]}`, sub:'Placement assessment will be skipped' }] : [{ icon:'target', bg:'#F2E9FF', ic:'#B45CF0', title:'Placement assessment', sub:'Student will be assessed before their first lesson' }]),
                ].map((item, i) => (
                  <div key={i} style={{ display:'flex', gap:12, marginBottom:14 }}>
                    <div style={{ width:30, height:30, borderRadius:8, background:item.bg,
                      display:'grid', placeItems:'center', flexShrink:0 }}>
                      <AIcon n={item.icon} s={14} c={item.ic} w={2}/>
                    </div>
                    <div>
                      <div style={{ fontSize:13, fontWeight:700, color:'var(--ink)', lineHeight:1.3 }}>{item.title}</div>
                      <div style={{ fontSize:12, color:'var(--muted)', marginTop:2, lineHeight:1.4 }}>{item.sub}</div>
                    </div>
                  </div>
                ))}
              </div>

              {/* Cohort stats */}
              <div className="adm-card adm-card-p">
                <div style={{ fontSize:13.5, fontWeight:700, color:'var(--ink)', marginBottom:14 }}>Current cohort</div>
                {[
                  ['Total students', '8'],
                  ['Onboarded', '8 (100%)'],
                  ['Active this week', '5'],
                  ['Avg CEFR', 'B1–B2'],
                ].map(([k,v]) => (
                  <div key={k} style={{ display:'flex', justifyContent:'space-between', padding:'7px 0',
                    borderBottom:'1px solid var(--border)', fontSize:13 }}>
                    <span style={{ color:'var(--muted)' }}>{k}</span>
                    <span style={{ fontWeight:700, color:'var(--ink)' }}>{v}</span>
                  </div>
                ))}
                <div style={{ marginTop:14, padding:12, background:'#F6F4FB', borderRadius:8 }}>
                  <div style={{ fontSize:12, color:'var(--muted)', lineHeight:1.55 }}>
                    Adding this student will bring the cohort to <strong style={{ color:'var(--ink)' }}>9 students</strong>.
                  </div>
                </div>
              </div>
            </div>
          </div>
        </form>
      </div>
    );
  }

  window.AdminCreateStudent = AdminCreateStudent;
})();
