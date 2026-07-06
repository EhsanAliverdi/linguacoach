/** Which rendering engine a schema is authored for. 'FormIo' renders generically via
 * @formio/js; 'Custom' renders via a hand-built, purpose-designed Angular component that parses
 * the same wizard schema but owns its own presentation (used by onboarding today). */
export type FormRendererKind = 'FormIo' | 'Custom';
