# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: admin-screenshots.spec.ts >> admin: diagnostics sidebar nav item present
- Location: e2e\admin-screenshots.spec.ts:457:5

# Error details

```
TimeoutError: page.waitForSelector: Timeout 5000ms exceeded.
Call log:
  - waiting for locator('[routerlink="/admin/diagnostics"]')

```

# Page snapshot

```yaml
- generic [ref=e5]:
  - complementary [ref=e7]:
    - link "SpeakPath Admin Panel" [ref=e9] [cursor=pointer]:
      - /url: /admin
      - img [ref=e10]
      - generic [ref=e16]:
        - generic [ref=e17]: SpeakPath
        - generic [ref=e18]: Admin Panel
    - navigation "Admin navigation" [ref=e19]:
      - generic [ref=e20]:
        - paragraph [ref=e22]: Menu
        - list [ref=e23]:
          - listitem [ref=e24]:
            - link "Dashboard" [ref=e26] [cursor=pointer]:
              - /url: /admin
              - img [ref=e28]
              - generic [ref=e33]: Dashboard
          - listitem [ref=e34]:
            - link "Students" [ref=e36] [cursor=pointer]:
              - /url: /admin/students
              - img [ref=e38]
              - generic [ref=e42]: Students
          - listitem [ref=e43]:
            - link "AI Config" [ref=e45] [cursor=pointer]:
              - /url: /admin/ai-config
              - img [ref=e47]
              - generic [ref=e50]: AI Config
          - listitem [ref=e51]:
            - link "Prompts" [ref=e53] [cursor=pointer]:
              - /url: /admin/prompts
              - img [ref=e55]
              - generic [ref=e57]: Prompts
          - listitem [ref=e58]:
            - link "AI Usage" [ref=e60] [cursor=pointer]:
              - /url: /admin/usage
              - img [ref=e62]
              - generic [ref=e63]: AI Usage
          - listitem [ref=e64]:
            - link "Exercise Types" [ref=e66] [cursor=pointer]:
              - /url: /admin/exercise-types
              - img [ref=e68]
              - generic [ref=e70]: Exercise Types
      - generic [ref=e71]:
        - paragraph [ref=e73]: System
        - list [ref=e74]:
          - listitem [ref=e75]:
            - link "Integrations" [ref=e77] [cursor=pointer]:
              - /url: /admin/integrations
              - img [ref=e79]
              - generic [ref=e83]: Integrations
          - listitem [ref=e84]:
            - link "Diagnostics" [ref=e86] [cursor=pointer]:
              - /url: /admin/diagnostics
              - img [ref=e88]
              - generic [ref=e90]: Diagnostics
  - generic [ref=e91]:
    - banner [ref=e93]:
      - generic [ref=e94]:
        - button "Toggle sidebar" [ref=e96] [cursor=pointer]:
          - img [ref=e97]
        - generic [ref=e98]:
          - button "Switch to dark mode" [ref=e100]:
            - img [ref=e101]
          - button "Profile menu" [ref=e108] [cursor=pointer]: A
    - main [ref=e109]:
      - generic [ref=e110]:
        - generic "Dashboard" [ref=e111]:
          - generic [ref=e113]:
            - heading "Dashboard" [level=1] [ref=e114]
            - paragraph [ref=e115]: SpeakPath platform overview
        - generic [ref=e116]:
          - generic [ref=e117]:
            - article [ref=e119]:
              - img [ref=e121]
              - generic [ref=e125]:
                - generic [ref=e126]: Total students
                - generic [ref=e127]: "0"
            - article [ref=e129]:
              - img [ref=e131]
              - generic [ref=e133]:
                - generic [ref=e134]: Onboarded
                - generic [ref=e135]: "0"
            - article [ref=e137]:
              - img [ref=e139]
              - generic [ref=e142]:
                - generic [ref=e143]: AI provider
                - generic [ref=e144]: Configured
            - article [ref=e146]:
              - img [ref=e148]
              - generic [ref=e149]:
                - generic [ref=e150]: Activities tracked
                - generic [ref=e151]: "0"
          - generic [ref=e152]:
            - generic "Quick actions" [ref=e153]:
              - generic [ref=e154]:
                - heading "Quick actions" [level=2] [ref=e156]
                - generic [ref=e158]:
                  - generic "Add student" [ref=e159]:
                    - link "Add student Create a pilot account" [ref=e160] [cursor=pointer]:
                      - /url: /admin/create-student
                      - img [ref=e162]
                      - generic [ref=e165]:
                        - generic [ref=e166]: Add student
                        - generic [ref=e167]: Create a pilot account
                  - generic "Manage students" [ref=e168]:
                    - link "Manage students View all accounts" [ref=e169] [cursor=pointer]:
                      - /url: /admin/students
                      - img [ref=e171]
                      - generic [ref=e175]:
                        - generic [ref=e176]: Manage students
                        - generic [ref=e177]: View all accounts
                  - generic "AI Config" [ref=e178]:
                    - link "AI Config Providers and models" [ref=e179] [cursor=pointer]:
                      - /url: /admin/ai-config
                      - img [ref=e181]
                      - generic [ref=e184]:
                        - generic [ref=e185]: AI Config
                        - generic [ref=e186]: Providers and models
                  - generic "Prompts" [ref=e187]:
                    - link "Prompts Manage templates" [ref=e188] [cursor=pointer]:
                      - /url: /admin/prompts
                      - img [ref=e190]
                      - generic [ref=e192]:
                        - generic [ref=e193]: Prompts
                        - generic [ref=e194]: Manage templates
            - generic "Recent students" [ref=e195]:
              - generic [ref=e196]:
                - generic [ref=e197]:
                  - heading "Recent students" [level=2] [ref=e198]
                  - link "View all →" [ref=e199] [cursor=pointer]:
                    - /url: /admin/students
                - table [ref=e204]:
                  - rowgroup [ref=e205]:
                    - row "Email Onboarding CEFR Joined" [ref=e206]:
                      - columnheader "Email" [ref=e207]
                      - columnheader "Onboarding" [ref=e208]
                      - columnheader "CEFR" [ref=e209]
                      - columnheader "Joined" [ref=e210]
                  - rowgroup [ref=e211]:
                    - row "alice@corp.com Complete B1 Jan 15, 2026" [ref=e212]:
                      - cell "alice@corp.com" [ref=e213]
                      - cell "Complete" [ref=e214]:
                        - generic [ref=e216]: Complete
                      - cell "B1" [ref=e217]:
                        - generic [ref=e219]: B1
                      - cell "Jan 15, 2026" [ref=e220]
                    - row "bob@corp.com Pending — May 20, 2026" [ref=e221]:
                      - cell "bob@corp.com" [ref=e222]
                      - cell "Pending" [ref=e223]:
                        - generic [ref=e225]: Pending
                      - cell "—" [ref=e226]
                      - cell "May 20, 2026" [ref=e227]
                    - row "carol@corp.com Complete A2 Apr 10, 2026" [ref=e228]:
                      - cell "carol@corp.com" [ref=e229]
                      - cell "Complete" [ref=e230]:
                        - generic [ref=e232]: Complete
                      - cell "A2" [ref=e233]:
                        - generic [ref=e235]: A2
                      - cell "Apr 10, 2026" [ref=e236]
          - generic "AI System" [ref=e237]:
            - generic [ref=e238]:
              - generic [ref=e239]:
                - heading "AI System" [level=2] [ref=e240]
                - generic [ref=e242]: Online
              - generic [ref=e244]:
                - generic [ref=e245]:
                  - generic [ref=e246]:
                    - generic [ref=e247]: Writing activities
                    - generic [ref=e249]: Active
                  - generic [ref=e251]:
                    - generic [ref=e252]: Feedback generation
                    - generic [ref=e254]: Active
                  - generic [ref=e256]:
                    - generic [ref=e257]: Speaking
                    - generic [ref=e259]: Active
                  - generic [ref=e261]:
                    - generic [ref=e262]: Listening
                    - generic [ref=e264]: Active
                - link "Manage AI config →" [ref=e267] [cursor=pointer]:
                  - /url: /admin/ai-config
      - generic:
        - region "Notifications"
```

# Test source

```ts
  362 |     localStorage.setItem('speakpath.sidebarCollapsed', 'true');
  363 |   });
  364 |   await studentLogin(page);
  365 |   await page.waitForTimeout(400);
  366 |   await page.screenshot({ path: 'e2e/screenshots/student-collapsed.png' });
  367 | });
  368 | 
  369 | test('landing page', async ({ page }) => {
  370 |   await page.goto('/');
  371 |   await page.waitForTimeout(400);
  372 |   await page.screenshot({ path: 'e2e/screenshots/landing-full.png', fullPage: true });
  373 | });
  374 | 
  375 | test('student: profile mobile', async ({ page }) => {
  376 |   await page.setViewportSize({ width: 390, height: 844 });
  377 |   await mockStudent(page);
  378 |   await studentLogin(page);
  379 |   await page.goto('/profile');
  380 |   await page.waitForTimeout(400);
  381 |   await page.screenshot({ path: 'e2e/screenshots/mobile-profile.png', fullPage: false });
  382 | });
  383 | 
  384 | test('admin: mobile hamburger', async ({ page }) => {
  385 |   await page.setViewportSize({ width: 390, height: 844 });
  386 |   await mockAdmin(page);
  387 |   await adminLogin(page);
  388 |   await page.waitForTimeout(400);
  389 |   await page.screenshot({ path: 'e2e/screenshots/mobile-admin.png', fullPage: false });
  390 |   // Open drawer
  391 |   await page.getByRole('button', { name: /Open navigation/i }).click();
  392 |   await page.waitForTimeout(300);
  393 |   await page.screenshot({ path: 'e2e/screenshots/mobile-admin-drawer.png', fullPage: false });
  394 | });
  395 | 
  396 | test('admin: migrated pages do not create mobile page overflow', async ({ page }) => {
  397 |   await page.setViewportSize({ width: 390, height: 844 });
  398 |   await mockAdmin(page);
  399 |   await adminLogin(page);
  400 | 
  401 |   for (const route of ['/admin/students', '/admin/ai-config']) {
  402 |     await page.goto(route);
  403 |     await page.waitForLoadState('networkidle');
  404 |     const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  405 |     expect(overflow).toBeLessThanOrEqual(1);
  406 |   }
  407 | });
  408 | 
  409 | test('admin: diagnostics page loads with status section', async ({ page }) => {
  410 |   await mockAdmin(page);
  411 |   // Mock diagnostics endpoints
  412 |   await page.route('**/api/admin/diagnostics/status', async route => {
  413 |     await route.fulfill({
  414 |       status: 200, contentType: 'application/json',
  415 |       body: JSON.stringify({
  416 |         environment: 'Testing',
  417 |         version: '1.0.0',
  418 |         serverTimeUtc: new Date().toISOString(),
  419 |         uptimeSeconds: 120,
  420 |         logLevel: 'Information',
  421 |         diagnosticEventsEnabled: true,
  422 |         diagnosticEventCount: 42,
  423 |         database: { reachable: true },
  424 |         ai: { providerConfigured: true, activeProvider: 'OpenAI', activeModel: 'gpt-4o-mini' },
  425 |       }),
  426 |     });
  427 |   });
  428 |   await page.route('**/api/admin/diagnostics/events', async route => {
  429 |     await route.fulfill({
  430 |       status: 200, contentType: 'application/json',
  431 |       body: JSON.stringify({
  432 |         enabled: true,
  433 |         total: 2,
  434 |         items: [
  435 |           { timestampUtc: new Date().toISOString(), level: 'Information', category: 'Activity.ActivityGetHandler', message: 'Next activity requested', correlationId: 'abc123', userId: null, path: '/api/activity/next', statusCode: null, elapsedMs: null },
  436 |           { timestampUtc: new Date().toISOString(), level: 'Warning', category: 'Activity.ActivityGetHandler', message: 'AI generation failed â€” using SystemFallback', correlationId: 'abc123', userId: null, path: '/api/activity/next', statusCode: null, elapsedMs: null },
  437 |         ],
  438 |       }),
  439 |     });
  440 |   });
  441 | 
  442 |   await adminLogin(page);
  443 |   await page.goto('/admin/diagnostics');
  444 |   await page.waitForTimeout(600);
  445 | 
  446 |   // Status section should be visible
  447 |   await page.waitForSelector('text=System status', { timeout: 5000 });
  448 |   await page.waitForSelector('text=Environment', { timeout: 3000 });
  449 |   await page.waitForSelector('text=Reachable', { timeout: 3000 });
  450 | 
  451 |   // Events section should be visible
  452 |   await page.waitForSelector('text=Recent events', { timeout: 3000 });
  453 | 
  454 |   await page.screenshot({ path: 'e2e/screenshots/admin-diagnostics.png', fullPage: true });
  455 | });
  456 | 
  457 | test('admin: diagnostics sidebar nav item present', async ({ page }) => {
  458 |   await mockAdmin(page);
  459 |   await adminLogin(page);
  460 | 
  461 |   // Diagnostics link should be in the DOM (may be collapsed/hidden in rail mode)
> 462 |   await page.waitForSelector('[routerlink="/admin/diagnostics"]', { state: 'attached', timeout: 5000 });
      |              ^ TimeoutError: page.waitForSelector: Timeout 5000ms exceeded.
  463 | });
  464 | 
  465 | test('student dashboard: no unexpected console errors', async ({ page }) => {
  466 |   const consoleErrors: string[] = [];
  467 |   page.on('console', msg => {
  468 |     if (msg.type() === 'error') {
  469 |       const text = msg.text();
  470 |       // Ignore known harmless errors (e.g. favicon 404, extension errors)
  471 |       if (!text.includes('favicon') && !text.includes('chrome-extension')) {
  472 |         consoleErrors.push(text);
  473 |       }
  474 |     }
  475 |   });
  476 | 
  477 |   await mockStudent(page);
  478 |   await studentLogin(page);
  479 |   await page.goto('/dashboard');
  480 |   await page.waitForTimeout(800);
  481 | 
  482 |   if (consoleErrors.length > 0) {
  483 |     throw new Error(`Unexpected console errors on dashboard:\n${consoleErrors.join('\n')}`);
  484 |   }
  485 | });
  486 | 
  487 | test('activity page: no unexpected console errors', async ({ page }) => {
  488 |   const consoleErrors: string[] = [];
  489 |   page.on('console', msg => {
  490 |     if (msg.type() === 'error') {
  491 |       const text = msg.text();
  492 |       if (!text.includes('favicon') && !text.includes('chrome-extension')) {
  493 |         consoleErrors.push(text);
  494 |       }
  495 |     }
  496 |   });
  497 | 
  498 |   await mockStudent(page);
  499 |   await studentLogin(page);
  500 |   await page.goto('/activity');
  501 |   await page.waitForTimeout(800);
  502 | 
  503 |   if (consoleErrors.length > 0) {
  504 |     throw new Error(`Unexpected console errors on activity page:\n${consoleErrors.join('\n')}`);
  505 |   }
  506 | });
  507 | 
```