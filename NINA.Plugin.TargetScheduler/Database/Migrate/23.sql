/*
*/

ALTER TABLE profilepreference ADD COLUMN enableAPI INTEGER DEFAULT 0;
ALTER TABLE profilepreference ADD COLUMN apiPort INTEGER DEFAULT 8188;

PRAGMA user_version = 23;
