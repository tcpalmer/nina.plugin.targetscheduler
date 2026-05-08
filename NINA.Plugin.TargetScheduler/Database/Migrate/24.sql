/*
*/

ALTER TABLE target ADD COLUMN priority INTEGER DEFAULT -1;

PRAGMA user_version = 24;