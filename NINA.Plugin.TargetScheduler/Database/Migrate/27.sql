/*
*/

ALTER TABLE profilepreference ADD COLUMN enableClientUpdatesExposurePlan INTEGER DEFAULT 1;

PRAGMA user_version = 27;
