# Target Scheduler

## 5.8.3.0 - 2025-12-01
* The first 'Switch filter' is now shown in the TS sequence progress display even if same filter was used by last exposure of previous target.
* Added selection of profile to constrain queries for Acquired Image and Reporting results.
* Fixed issue where targets could have no selectable exposure plan (related to exposure plan disable).

## 5.8.2.0 - 2025-10-29
* New profile preference to disable target completion reset at the profile level.
* Fixes for TS failure when it can't find a suitable exposure.
* Fix for override exposure orders and disabled exposure plans.
* Fixed SQLite dll load error on startup.
* Added a warning to the custom trigger sequence drop area.
* Added guids to most tables to support future api.
* Fix for threading problem in scheduler progress vm add.

## 5.8.1.0 (beta) - 2025-08-07
* Fixed issue with layout during exposure template editing.
* Added twilight offset and dither settings to the exposure template profile table.

## 5.8.0.0 (beta) - 2025-08-04
* Added support for exposure rejection for humidity.
* Added support for an offset in minutes from twilight acceptable times.
* Added ability to disable an exposure plan.

## 5.7.3.0 - 2025-07-28
* Fixed issue with reporting - was assuming that FWHM and Eccentricity are available via HocusFocus.

## 5.7.2.0 - 2025-07-24
* Fixed issue with Center After Drift and updating when same target.

## 5.7.1.0 - 2025-06-28
* Fixed issue with target move and how associated acquired image rows are updated.

## 5.7.0.0 - 2025-06-24
* Added ability to move a target from one project to another.
* Added display of the thumbnail image to the acquired image detail view.
* Added ability to manually change the grading status on acquired images.

## 5.6.5.0 - 2025-06-20
* Fixed issue where TS Sync Condition was not resetting properly.

## 5.6.4.0 - 2025-06-02
* Fixed issue with missing active/inactive icons on project and target panels.

## 5.6.3.0 - 2025-05-24
* Fixed issue with MF pause handling and project minimum time check.

## 5.6.2.0 - 2025-05-19
* Fixed project/target name truncation in new panel for Imaging tab.
* Fixed issue where previous target was allowed to continue even though next exposure was not suitable for current twilight level.

## 5.6.0.0 - 2025-05-05
* Added a TS dockable panel for the NINA Imaging tab.
* Added pause/resume buttons to TS instruction and dockable panel.

## 5.5.1.0 - 2025-05-02
* Fixed issue with meridian window and out-of-range target transit time.
* Fixed issue with targets not continuing after a meridian flip pause.

## 5.5.0.0 - 2025-04-26
* Added Dither Every setting to Exposure Templates, if set it will override the project value.
* Fixed issue with main panel height - was cutting off scoring rule weights list.
* Added better cancel/interrupt handling when taking flats.

## 5.4.0.1 - 2025-04-23
* Bug fix for new meridian pause handling for bad target transit times.

## 5.4.0.0 - 2025-04-22
* Added new scoring rule: Meridian Flip Penalty.
* Added special handling for users that need a pause before a meridian flip.
* Added support for exposure repeating for smart exposure when multiple have the same avoidance score.
* Relaxed profile import: user has option to continue if importing database is newer than the export.
* Fixed issue where flats could miss file name pattern substitutions.

## 5.3.0.0 - 2025-04-13
* Added ability to export all profile data (projects, targets, etc) to a zip file for later import.
* Fixed issue in Reporting where any NaN FWHM or Eccentricity values caused the corresponding range display to show 'n/a'. 
* Preview view details output was horked at TS Info log level.

## 5.2.0.1 - 2025-04-04
* Fixed a problem where filter cadence is not cleared during some target edits.

## 5.2.0.0 - 2025-04-02
Official release of version 5.

## 5.1.9.0 (unreleased beta) - 2025-03-XX
* Reduced tolerance for 'equal' moon avoidance scores (0.05 -> 0.01).
* Targets in Acquired Images dropdown are now sorted by name.

## 5.1.8.0 (beta) - 2025-03-22
* Added ability to manually grade all pending exposure plans for a target.
* Targets in Reporting dropdown are now sorted by name.
* Added Exposure Template name to Acquired Images row detail view.
* Added database busy timeout to avoid locked errors.
* Other misc fixes.

## 5.1.7.0 (beta) - 2025-03-18
* Smart exposure selector will now select the exposure with lowest percent complete when multiple plans have equal highest avoidance scores.
* Fixed issue with custom event containers finding the current target.
* Added additional cancellation check in TS Container.

## 5.1.6.0 (beta) - 2025-03-11
* Added target acquisition summary report to Reporting display.
* Allow the current target to continue if it can use remaining visibility time if less than project minimum.
* Above should also help with TS Condition checks stopping the container if that same timing applies.
* Fixed issue where targets could forget dither state.
* Fixed issues with last flat image missing TS image file pattern variable substitutions.
* Fixed issue where TS Background Condition was postponing rechecks too far in the future.

## 5.1.5.0 (beta) - 2025-03-07
* Added explicit display of regular or provisional percent complete on exposure plans.
* Fixed bug with grading (was impacting all previous delayed grading runs).

## 5.1.4.0 (beta) - 2025-03-03
* Revised target/exposure plan percent complete calculation to handle delayed grading.

## 5.1.3.0 (beta) - 2025-02-26
* Added 'After Target Complete' custom event container.
* Fixed bug where sync client was ignoring exposure length on exp template of same name.
* Sync client can now take multiple exposures per server exposure.
* Location in sequence of a Center After Drift trigger is relaxed, can now be in any container above TS container.
* Preview now shows end times for targets.
* Added new published message for target complete (developers only).
* Added details to the message published when starting a planned wait: the next target and the number of seconds until the wait ends (developers only).

## 5.1.2.0 (beta) - 2025-02-20
* Added 'After Each Exposure' custom event container.
* Added ability to set the number of flats to take in the TS flats instructions.

## 5.1.1.0 (beta) - 2025-02-18
* Reformulated the moon avoidance score.
* Scheduler Preview now includes an end time.

## 5.1.0.0 (beta) - 2025-02-17
* Planner will now attempt to continue with a selected target for the minimum time.
* Fixed display and threading problems with Scheduler Preview.
* Added profile preference to set the TS log level (independent of NINA log level).
* Fixed issue with moon avoidance scoring.
* Improved performance of scheduler preview by decreasing visibility sampling rate.

## 5.0.2.0 (beta) - 2025-02-12
* Fixed Telescopius CSV format change.
* Recheck project minimum time after a meridian window clip.
* Fixed bug with new slew disable capability.

## 5.0.1.0 (beta) - 2025-02-11
* Added profile preference to enable/disable slew/center for new targets.
* Fixed problem where (now slower) plan preview locks the main UI thread.
* Fixed serious problem with planner time handling.

## 5.0.0.0 (beta) - 2025-02-09
* Major release

## 4.9.1.0 - 2025-01-02
* Fix for entering fractional seconds in target coordinates.  It will behave like core NINA: the fractional seconds will be stored and used but will be rounded for display.

## 4.9.0.1 - 2024-12-21
* Patch to prevent TS Target coordinates from getting cleared in certain scenarios (typically with CenterAfterDrift handling and Powerups usage)

## 4.9.0.0 - 2024-10-24
* Added flag to trigger avoidance if moon altitude is above the relax maximum altitude - regardless of actual separation or moon phase
* Adjusted moon avoidance evaluation time for not yet visible targets
* Hopefully fixed issue with taking superfluous flats
* Raised the max for Exposure Plan Desired and Accepted counts to 99999

## 4.8.0.0 - 2024-09-12
* Fixed a bug where the planner could return a plan with no exposures, will now abort container and warn instead
* Fixed a bug where needed flats were being improperly culled
* Added test support for inter-plugin messaging: TS will now message when a wait starts and when a target plan starts

## 4.7.6.2 - 2024-09-07
* Don't abort a flat exposure if setting flat panel brightness throws an error, just warn
* Changed the time at which moon avoidance is evaluated, now halfway through target's minimum time
* Removed rotation as one of the comparison criteria for flats

## 4.7.5.0 - 2024-09-01
* Added ability to change scheduler preview start time to now.

## 4.7.4.0 - 2024-08-13
* Bug fix for immediate flats on sync client
* Bug fix for event container race condition.

## 4.7.3.0 - 2024-08-09
* Bug fix for event containers not waiting for completion before continuing.

## 4.7.1.0 - 2024-08-07
* Bug fix for event container naming.

## 4.7.0.0 - 2024-08-06
* Added support for custom event containers in the Target Scheduler Sync Container instruction.
* Added support for running TS Flats instruction in a sync client sequence.

## 4.6.0.0 - 2024-07-30
* Added ability to reset target completion at the profile, project, and target levels.
* Added support for TSPROJECTNAME path variable.
* TS Flats instruction no longer displays misleading progress when idle.
* Fixed bug with caching and project/target horizon changes.

## 4.5.1.0 - 2024-07-05
* Fixed bug with smart plan window and concurrent or future potential targets

## 4.5.0.0 - 2024-06-19
* Relaxed matching criteria for trained flats, will now match if gain or offset is not equal
* Added additional logging for flat panel operations

## 4.4.0.0 - 2024-06-08
* Added ability to progressively relax classic moon avoidance when the moon is near or below the horizon
* Fixed (hopefully) crash when making some TS database changes after other NINA operations
* Fixed bug with nighttime only exposures and high latitudes near summer solstice
* Fixed bug logging training flat details which broke taking some flats

## 4.3.8.0 - 2024-05-09
* Fixed problem impacting sequences used for sync clients.

## 4.3.7.0 - 2024-05-05
* Raised timeouts/deadlines for sync operations

## 4.3.6.0 - 2024-04-17
* Added Target Scheduler Background Condition
* TS Container UI reworked to be more like a standard container and with better scrolling behavior (thanks Stefan)
* Fixed problem with override exposure order not being copied on paste operations and bulk import
* Fixed bug where internal filter name is unknown for OSC users
* Fixed bug (hopefully) where sync client was failing to process images and update the database

## 4.3.5.0 - 2024-03-08
* Fixed problem with CSV import due to NINA package updates

## 4.3.4.0 - 2024-02-23
*## 4.3.5.0 - 2024-03-08
* Fixed problem with CSV import due to NINA package updates

 Added toggle in Projects navigation to color projects/targets by whether they are active or not
* Added toggle in Projects navigation to show/hide projects/targets by whether they are active or not
* Added copy/paste/reset for Project Scoring Rule Weights

## 4.3.3.0 - 2024-02-15
* Refactored target and exposure planning percent complete handling

## 4.3.2.1 - 2024-02-12
* Fixed exposure completion reversion caused by previous percent complete rule fix

## 4.3.2.0 - 2024-02-06
* Fixed bug in percent complete scoring rule for completed exposure plans

## 4.3.1.0 - 2024-02-02
* Another tweak to TS Condition to ensure loop remains completed
* Fixed bug where target from Framing Wizard would appear to replace target in TS target management panel
* Code clean up

## 4.3.0.0 - 2024-01-26
* Fixed issue where TS Condition wasn't working when called in outer containers
* Increased timeout for sync client registration
* Added validation of TS Container triggers and custom event containers
* Stopped cloning of TS Container triggers into plan sub-container (now run normally)
* Added additional logging of sequence item lifecycle events

## 4.2.0.0 - 2023-12-28
* Added ability to bulk load targets from CSV files

## 4.1.2.2 - 2023-12-21
* Fixed bug in readout mode handling
* Fixed bug with Percent Complete and Mosaic Complete scoring rules if image grading is off

## 4.1.2.0 - 2023-12-18
* Fixed bug in smart plan window - was skipping projects incorrectly
* Fixed another bug with determining target completed
* You can now choose to delete acquired image records when deleting the associated target
* If running as a sync client, TS Condition will now use the server's data for the targets remain or projects remain checks

## 4.1.1.3 - 2023-12-15
* Fixed bug in TS Flats with project flats cadence > 1
* Fixed bug with determining target completeness with exposure throttling
* Fixed missing TS version in TS log

## 4.1.1.1 - 2023-12-14
* Fixed bug in TS Condition - check wasn't running the first time through
* Immediate flats wasn't handling Repeat Flat Set off correctly
* Immediate flats instruction will now open a flip-flat cover when done
* Updated for latest NINA 3 beta libraries

## 4.1.0.8 - 2023-12-12
* Added support for taking automated flats
* Optimized the condition check in Target Scheduler condition
* Target Scheduler Container instruction has a new custom event container: _After Each Target_
* Added a 'need flats' check to Target Scheduler condition

## 4.0.5.1 - 2023-11-26
* Improved handling when TS is canceled/interrupted which means it behaves better in safety scenarios and with Powerups safety controls.

## 4.0.5.0 - 2023-11-17
* Added image grading on FWHM and Eccentricity (requires Hocus Focus plugin)
* Added option to move rejected images to a 'rejected' directory
* Added ability to purge acquired image records by date or date/target
* Added CSV output for acquired image records
* Added better support for the Center After Drift trigger (see release notes)
* Added smarter determination of plan stop times
* Added ability to delete all target exposure plans
* The rule weight list is now sorted when displayed
* Added target rotation and ROI to the set of data saved for acquired images.  A future release will use these values when selecting 'like' images for grading.
* Fixed issue where target rotation wasn't being sent to Framing Wizard
* Added experimental support for synchronization across multiple instances of NINA
* All sequencer instructions moved to new category "Target Scheduler"

## 3.3.3.1 - 2023-10-11
* Fixed bug with exposure planner.

## 3.3.3.0 - 2023-09-19
* Fixed edge case bug with custom horizons.

## 3.3.2.0 - 2023-09-07
* Fixed problem with override exposure ordering. Unfortunately, any existing override order had to be cleared (automatically) for this fix.  You'll have to manually redo any that you had already created.

## 3.3.1.0 - 2023-08-22
* Added ability to override exposure ordering.
* Added Mosaic Completion scoring rule
* Fixed bug with rotation not being set when importing from a saved Sequence Target.
* Fixed bug related to non-existent custom horizon

## 3.2.1.0 - 2023-08-09
* Fixed bug preventing target ROI from being applied properly.

## 3.2.0.0 - 2023-08-07
* Changed the behavior of project minimum altitude: now can be used with or without a custom horizon.  If used with, then the horizon at each azimuth is the greater of (custom horizon + horizon offset) or project minimum altitude.
* Added ability to copy/paste exposure plans.
* Added fixed date range options to Acquired Images viewer and improved performance.
* Added ability to select images in the Acquired Images table by filter used.
* Fixed issue with scheduler preview: wasn't picking up dynamic changes to target database.
* Added 5/10/20 minute options to project minimum time.
* Will automatically unpark the scope if parked before a target slew.
* Fixed the annoying bug related to editing Exposure Templates on Target Exposure Plans.
* Images in the acquired images table will now show 'not graded' as the Reject Reason if grading was disabled when the image completed.
* Now skips useless Target Scheduler Condition checks.

## 3.1.2.0 - 2023-07-20
* The execution of the Before/After Target containers was changed to mean run only for new or changed targets. 
* Added a 'View Details' button to the Scheduler Preview to show details of the planning process.  The same information is available in the TS log for actual runs via the sequencer.

## 3.1.0.0 - 2023-07-13
Limited release only.
* Added support for inserting arbitrary instructions to run at various points during scheduler operation: before/after each wait period and before/after each target plan.
* Removed Conditions and Instructions drop areas in the Target Scheduler Container (not used and just confusing).
* Improved the display of running instructions in Target Scheduler Container

## 3.0.0.0 - 2023-07-XX
Limited release only.
* Ported to NINA 3
* Target rotation values will be auto-converted to NINA 3 counter-clockwise notation

## 0.8.0.0 - 2023-06-12
* Revised dithering approach (see release notes)
* Now does a center with rotation even if target rotation is zero

## 0.7.1.1 - 2023-05-30
* Fixed problem with missing parent for internal container

## 0.7.1.0 - 2023-05-25
* Added support for meridian window restriction
* Added default exposure time to Exposure Templates
* Added airmass to acquired image data detail display
* Added option to park the mount while waiting for next target
* Added option to throttle exposure planning when not grading
* Added option to accept all improvements in image grading
* Added loop condition to support outer loops for safety or multi-night
* Fixed problem with ROI exposure capture
* Fixed problem with including rejected exposure plans

## 0.6.0.0 - 2023-04-27
* Added validation to detect when Loop Conditions or Instructions are added to the TS container

## 0.5.0.0 - 2023-04-26
* Added support for importing mosaic panels from Framing Assistant

## 0.4.1.0 - 2023-04-25
* Added support for managing profile preferences
* Added image grader reject reason to acquired image data

## 0.4.0.0 - 2023-04-24
* First cut at image grader

## 0.3.0.0 - 2023-04-21
* Removed start and end date fields from projects
* Created a custom log for the plugin
* Added support for database migration scripts
* Fixed bug with plan end time

## 0.2.0.1 - 2023-04-20
* Increased the timeout in the image save watcher for DB updates
* Fixed problem saving a sequence as a sequence template

## 0.2.0.0 - 2023-04-02
* Major refactoring of the plugin sequence containers.
* Added Setting Soonest scoring rule.  Although the database schema hasn't changed, any projects created prior to this release will not be able to use this rule.

