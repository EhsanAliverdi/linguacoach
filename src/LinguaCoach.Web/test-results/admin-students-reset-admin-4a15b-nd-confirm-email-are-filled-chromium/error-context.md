# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: admin-students-reset.spec.ts >> admin: reset data submit disabled until reason and confirm email are filled
- Location: e2e\admin-students-reset.spec.ts:112:5

# Error details

```
Test timeout of 30000ms exceeded.
```

```
Error: locator.fill: Test timeout of 30000ms exceeded.
Call log:
  - waiting for getByRole('dialog', { name: 'Reset student data' }).locator('textarea[name="reason"]')

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
        - paragraph [ref=e21]: Menu
        - list [ref=e22]:
          - listitem [ref=e23]:
            - link "Dashboard" [ref=e24] [cursor=pointer]:
              - /url: /admin
              - img [ref=e26]
              - generic [ref=e31]: Dashboard
          - listitem [ref=e32]:
            - link "Students" [ref=e33] [cursor=pointer]:
              - /url: /admin/students
              - img [ref=e35]
              - generic [ref=e39]: Students
          - listitem [ref=e40]:
            - link "AI Config" [ref=e41] [cursor=pointer]:
              - /url: /admin/ai-config
              - img [ref=e43]
              - generic [ref=e46]: AI Config
          - listitem [ref=e47]:
            - link "Prompts" [ref=e48] [cursor=pointer]:
              - /url: /admin/prompts
              - img [ref=e50]
              - generic [ref=e52]: Prompts
          - listitem [ref=e53]:
            - link "AI Usage" [ref=e54] [cursor=pointer]:
              - /url: /admin/usage
              - img [ref=e56]
              - generic [ref=e57]: AI Usage
          - listitem [ref=e58]:
            - link "Exercise Types" [ref=e59] [cursor=pointer]:
              - /url: /admin/exercise-types
              - img [ref=e61]
              - generic [ref=e63]: Exercise Types
      - generic [ref=e64]:
        - paragraph [ref=e65]: System
        - list [ref=e66]:
          - listitem [ref=e67]:
            - link "Integrations" [ref=e68] [cursor=pointer]:
              - /url: /admin/integrations
              - img [ref=e70]
              - generic [ref=e74]: Integrations
          - listitem [ref=e75]:
            - link "Diagnostics" [ref=e76] [cursor=pointer]:
              - /url: /admin/diagnostics
              - img [ref=e78]
              - generic [ref=e80]: Diagnostics
    - button "Sign out" [ref=e82]:
      - img [ref=e84]
      - generic [ref=e87]: Sign out
  - generic [ref=e88]:
    - banner [ref=e90]:
      - generic [ref=e91]:
        - button "Toggle sidebar" [ref=e93] [cursor=pointer]:
          - img [ref=e94]
        - generic [ref=e95]:
          - button "Switch to dark mode" [ref=e98]:
            - img [ref=e99]
          - button "Profile menu" [ref=e105] [cursor=pointer]: A
    - main [ref=e106]:
      - generic [ref=e107]:
        - generic "Students" [ref=e108]:
          - generic [ref=e109]:
            - generic [ref=e110]:
              - heading "Students" [level=1] [ref=e111]
              - paragraph [ref=e112]: Manage pilot student accounts
            - link "Create student" [ref=e114] [cursor=pointer]:
              - /url: /admin/create-student
        - generic [ref=e117]:
          - generic [ref=e118]:
            - checkbox "Show archived students" [ref=e119]
            - generic [ref=e120]: Show archived students
          - searchbox "Search students" [ref=e121]
          - generic [ref=e122]: 1 shown
        - table [ref=e126]:
          - rowgroup [ref=e127]:
            - row "Student Lifecycle Onboarding CEFR Profile Joined ▼ Actions" [ref=e128]:
              - columnheader "Student" [ref=e129] [cursor=pointer]
              - columnheader "Lifecycle" [ref=e130]
              - columnheader "Onboarding" [ref=e131] [cursor=pointer]
              - columnheader "CEFR" [ref=e132]
              - columnheader "Profile" [ref=e133]
              - columnheader "Joined ▼" [ref=e134] [cursor=pointer]
              - columnheader "Actions" [ref=e135]
          - rowgroup [ref=e136]:
            - row "Alice Nguyen alice@corp.com CourseReady Complete B1 Project coordination Jan 15, 2026 Row actions" [ref=e137]:
              - cell "Alice Nguyen alice@corp.com" [ref=e138]:
                - generic [ref=e139]: Alice Nguyen
                - generic [ref=e140]: alice@corp.com
              - cell "CourseReady" [ref=e141]:
                - generic [ref=e143]: CourseReady
              - cell "Complete" [ref=e144]:
                - generic [ref=e146]: Complete
              - cell "B1" [ref=e147]:
                - generic [ref=e149]: B1
              - cell "Project coordination" [ref=e150]
              - cell "Jan 15, 2026" [ref=e151]
              - cell "Row actions" [ref=e152]:
                - button "Row actions" [ref=e155]:
                  - img [ref=e156]
        - dialog "Reset student data" [ref=e162]:
          - button "Close dialog" [ref=e163]:
            - img [ref=e164]
          - generic [ref=e166]:
            - generic [ref=e167]: Reset student data
            - generic [ref=e168]: alice@corp.com
          - generic [ref=e170]:
            - generic [ref=e172]:
              - generic [ref=e173]: Preset
              - combobox "Preset" [ref=e174]:
                - option "Fix password"
                - option "Restart onboarding" [selected]
                - option "Restart placement"
                - option "Reset course only"
                - option "Full clean reset"
                - option "Custom"
            - generic [ref=e175]:
              - generic [ref=e176] [cursor=pointer]:
                - checkbox "Clear onboarding answers" [checked] [ref=e177]
                - text: Clear onboarding answers
              - generic [ref=e178] [cursor=pointer]:
                - checkbox "Clear placement results" [ref=e179]
                - text: Clear placement results
              - generic [ref=e180] [cursor=pointer]:
                - checkbox "Clear courses and sessions" [ref=e181]
                - text: Clear courses and sessions
              - generic [ref=e182] [cursor=pointer]:
                - checkbox "Clear activity attempts" [ref=e183]
                - text: Clear activity attempts
              - generic [ref=e184] [cursor=pointer]:
                - checkbox "Clear vocabulary" [ref=e185]
                - text: Clear vocabulary
              - generic [ref=e186] [cursor=pointer]:
                - checkbox "Clear learning memory" [ref=e187]
                - text: Clear learning memory
              - generic [ref=e188] [cursor=pointer]:
                - checkbox "Delete audio files" [ref=e189]
                - text: Delete audio files
              - generic [ref=e190] [cursor=pointer]:
                - checkbox "Recalculate progress data" [ref=e191]
                - text: Recalculate progress data
            - generic [ref=e193]:
              - generic [ref=e194]: Reason (required)
              - textbox "Reason (required)" [ref=e196]:
                - /placeholder: Why is this reset being performed?
            - generic [ref=e198]:
              - generic [ref=e199]: "Type the student email to confirm: alice@corp.com"
              - 'textbox "Type the student email to confirm: alice@corp.com" [ref=e201]':
                - /placeholder: ""
            - generic [ref=e202]:
              - button "Cancel" [ref=e204] [cursor=pointer]
              - button "Reset data" [disabled] [ref=e206]
      - generic:
        - region "Notifications"
```

# Test source

```ts
  23  |     await route.fulfill({
  24  |       status: 200, contentType: 'application/json',
  25  |       body: JSON.stringify({ token: fakeJwt('admin@example.com', 'Admin'), role: 'Admin', mustChangePassword: false }),
  26  |     });
  27  |   });
  28  |   await page.route('**/api/admin/students/*/reset', async route => {
  29  |     if (resetHandler) {
  30  |       await resetHandler(route);
  31  |       return;
  32  |     }
  33  |     await route.fulfill({
  34  |       status: 200, contentType: 'application/json',
  35  |       body: JSON.stringify({
  36  |         studentId: STUDENT.studentProfileId,
  37  |         previousStage: 'CourseReady',
  38  |         newStage: 'OnboardingRequired',
  39  |         clearedItems: {
  40  |           onboardingAnswers: true, placementResults: false, coursesAndSessions: false,
  41  |           activityAttempts: false, vocabulary: false, learningMemory: false,
  42  |           audioFilesDeleted: 0, progressData: false,
  43  |         },
  44  |         resetLogId: 'reset-log-1',
  45  |         performedByAdminId: 'admin-1',
  46  |         performedAtUtc: '2026-06-14T10:00:00Z',
  47  |         correlationId: 'corr-1',
  48  |       }),
  49  |     });
  50  |   });
  51  |   await page.route('**/api/admin/students*', async route => {
  52  |     if (route.request().method() !== 'GET') {
  53  |       await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ studentProfileId: 'x', userId: 'y' }) });
  54  |       return;
  55  |     }
  56  |     await route.fulfill({
  57  |       status: 200, contentType: 'application/json',
  58  |       body: JSON.stringify([STUDENT]),
  59  |     });
  60  |   });
  61  |   await page.route('**/api/admin/**', async route => {
  62  |     const url = route.request().url();
  63  |     if (url.includes('/api/admin/students')) {
  64  |       await route.fallback();
  65  |       return;
  66  |     }
  67  |     await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  68  |   });
  69  | }
  70  | 
  71  | async function adminLogin(page: Page) {
  72  |   await page.goto('/login');
  73  |   await page.getByLabel('Email').fill('admin@example.com');
  74  |   await page.getByLabel('Password').fill('Admin@1234');
  75  |   await page.getByRole('button', { name: 'Sign in' }).click();
  76  |   await page.waitForURL(/\/admin/, { timeout: 10000 });
  77  |   await page.waitForTimeout(600);
  78  | }
  79  | 
  80  | async function gotoStudents(page: Page) {
  81  |   await page.getByRole('link', { name: 'Students', exact: true }).click();
  82  |   await page.waitForURL(/\/admin\/students/, { timeout: 5000 });
  83  |   await page.waitForTimeout(300);
  84  | }
  85  | 
  86  | /**
  87  |  * Opens the first row's sp-admin-table-actions dropdown, then clicks the
  88  |  * action item matching the given label.
  89  |  * Phase 10X-F: row actions moved into dropdown trigger (.sp-adm-actions-trigger).
  90  |  * Projected content buttons use text matching (not role=menuitem).
  91  |  */
  92  | async function clickRowAction(page: Page, label: string) {
  93  |   await page.locator('.sp-adm-actions-trigger').first().click();
  94  |   await page.locator('[role="menu"] button, [role="menu"] a').filter({ hasText: label }).first().click();
  95  | }
  96  | 
  97  | test('admin: reset data modal opens with restart-onboarding preset by default', async ({ page }) => {
  98  |   await mockAdmin(page);
  99  |   await adminLogin(page);
  100 |   await gotoStudents(page);
  101 | 
  102 |   await clickRowAction(page, 'Reset data');
  103 | 
  104 |   const dialog = page.getByRole('dialog', { name: 'Reset student data' });
  105 |   await expect(dialog).toBeVisible();
  106 |   await expect(dialog.locator('select[name="preset"]')).toHaveValue(/restartOnboarding/);
  107 |   await expect(dialog.locator('input[name="clearOnboardingAnswers"]')).toBeChecked();
  108 |   await expect(dialog.locator('input[name="clearPlacementResults"]')).not.toBeChecked();
  109 |   await expect(dialog.locator('input[name="clearVocabulary"]')).not.toBeChecked();
  110 | });
  111 | 
  112 | test('admin: reset data submit disabled until reason and confirm email are filled', async ({ page }) => {
  113 |   await mockAdmin(page);
  114 |   await adminLogin(page);
  115 |   await gotoStudents(page);
  116 | 
  117 |   await clickRowAction(page, 'Reset data');
  118 |   const dialog = page.getByRole('dialog', { name: 'Reset student data' });
  119 | 
  120 |   const submit = dialog.locator('button[type="submit"]');
  121 |   await expect(submit).toBeDisabled();
  122 | 
> 123 |   await dialog.locator('textarea[name="reason"]').fill('QA needs to rerun onboarding');
      |                                                   ^ Error: locator.fill: Test timeout of 30000ms exceeded.
  124 |   await expect(submit).toBeDisabled();
  125 | 
  126 |   await dialog.locator('input[name="confirmEmail"]').fill('wrong@corp.com');
  127 |   await expect(submit).toBeDisabled();
  128 | 
  129 |   await dialog.locator('input[name="confirmEmail"]').fill(STUDENT.email);
  130 |   await expect(submit).toBeEnabled();
  131 | });
  132 | 
  133 | test('admin: applying full-clean-reset preset checks all clear flags', async ({ page }) => {
  134 |   await mockAdmin(page);
  135 |   await adminLogin(page);
  136 |   await gotoStudents(page);
  137 | 
  138 |   await clickRowAction(page, 'Reset data');
  139 |   const dialog = page.getByRole('dialog', { name: 'Reset student data' });
  140 | 
  141 |   await dialog.locator('select[name="preset"]').selectOption({ label: 'Full clean reset' });
  142 | 
  143 |   for (const flag of [
  144 |     'clearOnboardingAnswers', 'clearPlacementResults', 'clearCoursesAndSessions',
  145 |     'clearActivityAttempts', 'clearVocabulary', 'clearLearningMemory',
  146 |     'clearAudioFiles', 'clearProgressData',
  147 |   ]) {
  148 |     await expect(dialog.locator(`input[name="${flag}"]`)).toBeChecked();
  149 |   }
  150 | });
  151 | 
  152 | test('admin: successful reset shows new stage, cleared items and reset log id', async ({ page }) => {
  153 |   await mockAdmin(page);
  154 |   await adminLogin(page);
  155 |   await gotoStudents(page);
  156 | 
  157 |   await clickRowAction(page, 'Reset data');
  158 |   const dialog = page.getByRole('dialog', { name: 'Reset student data' });
  159 | 
  160 |   await dialog.locator('textarea[name="reason"]').fill('Stuck mid-onboarding after crash');
  161 |   await dialog.locator('input[name="confirmEmail"]').fill(STUDENT.email);
  162 |   await dialog.locator('button[type="submit"]').click();
  163 | 
  164 |   await expect(dialog.getByText(/New stage: OnboardingRequired/)).toBeVisible();
  165 |   await expect(dialog.getByText(/was CourseReady/)).toBeVisible();
  166 |   await expect(dialog.getByText(/Reset log: reset-log-1/)).toBeVisible();
  167 | 
  168 |   await dialog.getByRole('button', { name: 'Done' }).click();
  169 |   await expect(dialog).not.toBeVisible();
  170 | });
  171 | 
  172 | test('admin: reset failure shows error message', async ({ page }) => {
  173 |   await mockAdmin(page, async route => {
  174 |     await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ error: 'Reason is required.' }) });
  175 |   });
  176 |   await adminLogin(page);
  177 |   await gotoStudents(page);
  178 | 
  179 |   await clickRowAction(page, 'Reset data');
  180 |   const dialog = page.getByRole('dialog', { name: 'Reset student data' });
  181 | 
  182 |   await dialog.locator('textarea[name="reason"]').fill('Stuck mid-onboarding after crash');
  183 |   await dialog.locator('input[name="confirmEmail"]').fill(STUDENT.email);
  184 |   await dialog.locator('button[type="submit"]').click();
  185 | 
  186 |   await expect(dialog.locator('.sp-admin-alert-error')).toBeVisible();
  187 | });
  188 | 
  189 | test('admin: reset data modal can be cancelled', async ({ page }) => {
  190 |   await mockAdmin(page);
  191 |   await adminLogin(page);
  192 |   await gotoStudents(page);
  193 | 
  194 |   await clickRowAction(page, 'Reset data');
  195 |   const dialog = page.getByRole('dialog', { name: 'Reset student data' });
  196 |   await expect(dialog).toBeVisible();
  197 | 
  198 |   await dialog.getByRole('button', { name: 'Cancel' }).click();
  199 |   await expect(dialog).not.toBeVisible();
  200 | });
  201 | 
```