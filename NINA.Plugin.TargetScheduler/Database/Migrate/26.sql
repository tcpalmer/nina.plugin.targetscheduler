/*
*/

ALTER TABLE profilepreference ADD COLUMN enablePlannerReports INTEGER DEFAULT 0;

PRAGMA user_version = 26;
