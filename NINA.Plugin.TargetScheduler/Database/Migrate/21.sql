/*
*/

ALTER TABLE profilepreference ADD COLUMN enableStopOnHumidity INTEGER DEFAULT 1;

ALTER TABLE exposuretemplate ADD COLUMN minutesOffset INTEGER DEFAULT 0;

PRAGMA user_version = 21;
