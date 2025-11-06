# Target Scheduler NINA Plugin

The Target Scheduler Plugin is designed to provide a higher level of automation than is typically achievable with NINA. Specifically, it maintains a database of imaging projects describing DSO targets and associated exposure plans. Based on various criteria and preferences, it can decide at any given time what project/target should be actively imaging. A user will enter the desired projects, targets, and preferences into a UI exposed by the plugin. At runtime, a single new instruction for the NINA Advanced Sequencer will interact with the planning engine to determine the best target for imaging at each point throughout a night. The instruction will manage the slew/center to the target, switching filters, taking exposures, and dithering - all while transparently interacting with the surrounding NINA triggers and conditions.

See the [plugin documentation](https://tcpalmer.github.io/nina-scheduler/) for more information.

## Maximum Horizon Integration

If the optional Maximum Horizon plugin is installed, Target Scheduler will automatically honor its maximum-altitude constraint during the planning engine’s visibility checks. When a target exceeds the allowed ceiling for its azimuth/time, that interval is treated as not visible, affecting candidate/ready filtering and scoring per the planning model.

When the plugin is not installed, behavior is unchanged.