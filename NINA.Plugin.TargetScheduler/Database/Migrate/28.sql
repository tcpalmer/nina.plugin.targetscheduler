/*
*/

ALTER TABLE profilepreference ADD COLUMN autoRejectLevelHFR REAL DEFAULT 0;
ALTER TABLE profilepreference ADD COLUMN autoRejectLevelFWHM REAL DEFAULT 0;
ALTER TABLE profilepreference ADD COLUMN autoRejectLevelEccentricity REAL DEFAULT 0;

PRAGMA user_version = 28;
