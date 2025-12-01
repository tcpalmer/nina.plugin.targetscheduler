using System.Reflection;
using System.Runtime.InteropServices;

[assembly: Guid("B4541BA9-7B07-4D71-B8E1-6C73D4933EA0")]
[assembly: AssemblyTitle("Target Scheduler")]
[assembly: AssemblyDescription("An automated target scheduler for NINA")]
[assembly: AssemblyCompany("Tom Palmer @tcpalmer")]
[assembly: AssemblyProduct("Target Scheduler")]
[assembly: AssemblyCopyright("Copyright © 2025")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("windows")]
[assembly: AssemblyVersion("5.8.3.0")]
[assembly: AssemblyFileVersion("5.8.3.0")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.1.2.9001")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/tcpalmer/nina.plugin.targetscheduler/")]
[assembly: AssemblyMetadata("Homepage", "https://tcpalmer.github.io/nina-scheduler/")]
[assembly: AssemblyMetadata("Tags", "Automation,Scheduling,Sequencer")]
[assembly: AssemblyMetadata("FeaturedImageURL", "https://raw.githubusercontent.com/tcpalmer/nina.plugin.targetscheduler/main/NINA.Plugin.TargetScheduler/assets/target-scheduler-logo.png?raw=true")]
[assembly: AssemblyMetadata("ScreenshotURL", "https://raw.githubusercontent.com/tcpalmer/nina.plugin.targetscheduler/main/NINA.Plugin.TargetScheduler/assets/screenshot-1.png?raw=true")]
[assembly: AssemblyMetadata("AltScreenshotURL", "https://raw.githubusercontent.com/tcpalmer/nina.plugin.targetscheduler/main/NINA.Plugin.TargetScheduler/assets/screenshot-2.png?raw=true")]
[assembly: AssemblyMetadata("LongDescription", @"The Target Scheduler Plugin is designed to provide a higher level of automation than is typically achievable with NINA. Specifically, it maintains a database of imaging projects describing DSO targets and associated exposure plans. Based on various criteria and preferences, it can decide at any given time what project/target should be actively imaging. A user will enter the desired projects, targets, and preferences into a UI exposed by the plugin. At runtime, a single new instruction for the NINA Advanced Sequencer will interact with the planning engine to determine the best target for imaging at each point throughout a night. The instruction will manage the slew/center to the target, switching filters, taking exposures, and dithering - all while transparently interacting with the surrounding NINA triggers and conditions.

## Major Features
* Easy to use interface for entering and managing projects, targets, and exposure plans
* Multiple target import options: NINA catalog, NINA Framing Assistant (including mosaic panels), attached planetarium programs, Telescopius CSV, and more
* Dynamic target selection based on visibility and weighted scoring rules
* Flexible exposure/filter planning including enhanced moon avoidance
* Plan previewing
* Image grading
* Reporting
* Automated flats support
* Synchronization across multiple instances of NINA
* Support for 24x7 sequences for long term unattended operation

If you like this plugin and it saves you time, please consider [Buying Me a Beer](https://www.buymeacoffee.com/tcpalmer).

## Documentation
The [plugin documentation](https://tcpalmer.github.io/nina-scheduler/) provides a detailed description of the plugin and how to use it.  See the [release notes](https://tcpalmer.github.io/nina-scheduler/release.html) for changes in recent versions.

## Acknowledgements
* Chris Woodhouse has been involved since the beginning and was instrumental in hashing out the initial design and requirements.
* Matt Nudi also provided design feedback and critical early beta testing.
* Linda Thomas-Fowler and Chris Kagy continue to provide invaluable testing and feedback on features.
* The concept for the plugin was originally inspired by the AIC video [Tim Hutchison: Automating a Backyard Observatory](https://www.youtube.com/watch?v=a4IkAUZkXH0).
* Any automated scheduler is going to owe a debt of gratitude to [ACP](https://acpx.dc3.com/) for pioneering in this area.
* Finally, it's a tribute to the elegant design of NINA and the Advanced Sequencer that a plugin of this type is not only possible, but can also interact correctly with other elements in a sequence.

# Getting Help #
* Review the [plugin documentation](https://tcpalmer.github.io/nina-scheduler/)
* Ask for help (tag @tcpalmer) in the #target-scheduler channel on the NINA project [Discord server](https://discord.com/invite/rWRbVbw).
* [Plugin source code](https://github.com/tcpalmer/nina.plugin.targetscheduler)
* [Change log](https://github.com/tcpalmer/nina.plugin.targetscheduler/blob/main/CHANGELOG.md)

The Target Scheduler plugin is provided 'as is' under the terms of the [Mozilla Public License 2.0](https://github.com/tcpalmer/nina.plugin.targetscheduler/blob/main/LICENSE.txt)")]

[assembly: ComVisible(false)]