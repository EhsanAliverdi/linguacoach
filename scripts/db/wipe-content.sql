-- Full content reseed (2026-07-29) — dev/UAT-only hard delete of every content entity
-- (Skill Graph, Resource Bank, Lessons, Exercises, Modules) so the platform can be rebuilt from
-- data/seed-json/*.json via tools/LinguaCoach.ContentSeeder.
--
-- Order is FK-safe, proven against the real dev DB this session (including the import_packages /
-- import_upload_sessions fix — both are Restrict against cefr_resource_sources and were missing
-- from the first pass, which failed with a live FK violation before this order was corrected).
-- Module/Lesson/Exercise deletion cascades their own join tables (module_lesson_links,
-- module_exercise_links, module_skill_graph_node_links, lesson_resource_links,
-- exercise_resource_links) automatically via real DB-level ON DELETE CASCADE — no explicit
-- DELETE needed for those. resource_import_runs cascades resource_raw_records -> resource_candidates.
--
-- Run with:
--   docker exec -i linguacoach-db-1 psql -U postgres -d linguacoach_dev -f - < scripts/db/wipe-content.sql
-- or, from inside the container:
--   psql -U postgres -d linguacoach_dev -f scripts/db/wipe-content.sql

BEGIN;

DELETE FROM student_exercise_launches;
DELETE FROM student_practice_gym_module_assignments;
DELETE FROM student_today_plan_module_assignments;

DELETE FROM modules;
DELETE FROM lessons;
DELETE FROM exercises;

DELETE FROM skill_graph_prerequisite_edges;
DELETE FROM skill_graph_nodes;

DELETE FROM resource_bank_items;
DELETE FROM resource_import_runs;
DELETE FROM import_packages;
DELETE FROM import_upload_sessions;
DELETE FROM cefr_resource_sources;

COMMIT;

-- Verification (should all be 0):
SELECT
  (SELECT count(*) FROM student_exercise_launches) AS student_exercise_launches,
  (SELECT count(*) FROM student_practice_gym_module_assignments) AS practice_gym_assignments,
  (SELECT count(*) FROM student_today_plan_module_assignments) AS today_plan_assignments,
  (SELECT count(*) FROM modules) AS modules,
  (SELECT count(*) FROM lessons) AS lessons,
  (SELECT count(*) FROM exercises) AS exercises,
  (SELECT count(*) FROM skill_graph_prerequisite_edges) AS skill_graph_edges,
  (SELECT count(*) FROM skill_graph_nodes) AS skill_graph_nodes,
  (SELECT count(*) FROM resource_bank_items) AS resource_bank_items,
  (SELECT count(*) FROM resource_import_runs) AS resource_import_runs,
  (SELECT count(*) FROM import_packages) AS import_packages,
  (SELECT count(*) FROM import_upload_sessions) AS import_upload_sessions,
  (SELECT count(*) FROM cefr_resource_sources) AS cefr_resource_sources;
