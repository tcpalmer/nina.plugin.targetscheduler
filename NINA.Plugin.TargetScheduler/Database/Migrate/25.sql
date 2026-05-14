/*
*/

ALTER TABLE profilepreference ADD COLUMN enableSyncedAutoFocus INTEGER DEFAULT 0;

PRAGMA user_version = 25;
