/*
*/

ALTER TABLE profilepreference ADD COLUMN enableStopOnHumidity INTEGER DEFAULT 1;

PRAGMA user_version = 21;
