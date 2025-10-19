/*
*/

ALTER TABLE profilepreference ADD COLUMN enableProfileTargetCompletionReset INTEGER DEFAULT 0;

PRAGMA user_version = 22;
