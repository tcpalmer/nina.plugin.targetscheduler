# CLAUDE.md

This file is a guide for coding agents working in 'NINA.Plugin.TargetScheduler.sln'.

## Scope

This guide covers all projects listed in 'NINA.Plugin.TargetScheduler.sln'.

## Overview

NINA.Plugin.TargetScheduler (aka 'Target Scheduler', aka 'TS') is a plugin for the NINA astrophotography application.
Based on user-provided details about a set of imaging targets, TS can automatically determine the optimal target to be imaging at any point in time.
- It is written in C# exclusively for Windows
- It uses Windows Presentation Foundation (WPF) for the user interface
- It uses Entity Framework 6 (EF6) and SQLite for database operations

## Solution Structure

The solution contains four projects:
- **NINA.Plugin.TargetScheduler** — main plugin
- **NINA.Plugin.TargetScheduler.Shared** — shared utilities (`TSLogger`, `ImageMetadata`, `Common` constants)
- **NINA.Plugin.TargetScheduler.SyncService** — multi-instance synchronization server/client
- **NINA.Plugin.TargetScheduler.Test** — NUnit test suite

## Documentation

- Documentation for the plugin is available from https://tcpalmer.github.io/nina-scheduler/ (repository: https://github.com/tcpalmer/nina-scheduler).
- Documentation for NINA is available from https://nighttime-imaging.eu/docs/master/site/.

## NINA Interface

All TS interaction with runtime NINA is via the https://github.com/isbeorn/nina/tree/develop/NINA.Plugin package, also available from https://www.nuget.org/packages/NINA.Plugin/3.3.0.1035-nightly.

## Code Style And Formatting

- Maintain the existing line-ending style of every touched file; default to CRLF for new files unless the target location dictates LF.
- Treat the root [`.editorconfig`](.editorconfig) as the canonical C# style source. It covers indentation, line endings, namespace style, `using` placement, `var` preferences, naming, and selected analyzer severities.
- For XAML, follow surrounding file style; no repo-wide XAML formatter configuration is checked in.
- Prefer modern C# supported by the target project. For new or refactored MVVM code, prefer `CommunityToolkit.Mvvm` where it fits instead of expanding legacy relay-command patterns.

## Plugin Components

### Database

The TS database code is under the Database folder. Subfolders include:
- Schema: EF files for each table
- Migrate: scripts to migration to a new TS version that includes database changes. The scripts are all relative to the current database state, starting with tables defined in the Initial folder.

The `SchedulerDatabaseContext` class handles virtually all database operations.

### Planning Engine

TS's core planning logic lives in `Planning/`.
- **`Planner.cs`** — main scheduling algorithm; `PreviewPlanner.cs` and `PlannerEmulator.cs` are used for UI preview.
- **`Planning/Entities/`** — `PlanningProject`, `PlanningTarget`, `PlanningExposure`, etc. (planning-time domain model, separate from the DB schema).
- **`Planning/Scoring/`** — `ScoringEngine.cs` orchestrates 8 pluggable `ScoringRule` subclasses (e.g., `ProjectPriorityRule`, `SettingSoonestRule`, `MeridianFlipPenaltyRule`).
- **`Planning/Exposures/`** — exposure selection strategies (`BasicExposureSelector`, `SmartExposureSelector`, override/repeat variants).
- **`InstructionGenerator.cs`** — converts a `SchedulerPlan` into NINA sequencer instructions.

### Variables

Variables expose TS runtime state to the NINA 3.3 expression system via NINA's `ISymbolBroker`.

**Location:** `Symbol/` folder — `SymbolPublisher.cs` (singleton, registers all symbols), `SymbolEventHandler.cs` (subscribes to `TargetSchedulerEventMediator` events and pushes updates).

**Published symbols:**
- Plugin: `TS_Version`
- Container state: `TS_ContainerRunning`, `TS_ContainerWaiting`, `TS_ContainerPaused`
- Container timing: `TS_ContainerLastStarted`, `TS_ContainerLastStopped`
- Current target: `TS_CurrentTargetName`, `TS_CurrentProjectName`, `TS_CurrentTargetCoordinates`, `TS_CurrentTargetRotation`, `TS_CurrentTargetStarted`
- Current exposure: `TS_CurrentFilterName`, `TS_CurrentExposureLength`
- Next target: `TS_NextTargetStart`, `TS_NextTargetName`, `TS_NextProjectName`

Symbol values are updated by `SymbolEventHandler` in response to `TargetSchedulerEventMediator` events.

### Publish/Subscribe

TS uses an internal event mediator pattern for intra-plugin communication.

**`TargetSchedulerEventMediator`** (`Sequencer/TargetSchedulerEventMediator.cs`) — central singleton event broker. Events:
- `ContainerStarting`, `ContainerStopping`, `ContainerPaused`, `ContainerUnpaused`
- `WaitStarting` / `WaitStopping` — `WaitStarting` carries `WaitStartingEventArgs`
- `TargetStarting` / `TargetStopping` — `TargetStarting` carries `TargetStartingEventArgs`
- `ExposureStarting` / `ExposureStopping` — `ExposureStarting` carries `ExposureStartingEventArgs`
- `TargetCompleteEvent` — carries `TargetCompleteEventArgs`
- `SymbolResetEvent`

**`PubSub/`** folder contains publisher helpers: `TSPublisher`, `TargetStartPublisher`, `TargetCompletePublisher`, `ContainerStoppedPublisher`, `NewTargetStartPublisher`, `WaitStartPublisher`, and `TSLoggingSubscriber`.

New event types should be added to `TargetSchedulerEventMediator`; publishers go in `PubSub/`.

### Synchronization

Synchronization allows multiple NINA instances on the same machine to coordinate imaging across targets.

**Location:** `NINA.Plugin.TargetScheduler.SyncService/Sync/` — `SyncManager.cs` (singleton, named-pipe IPC via `"TargetScheduler.Sync"`), `SyncServer.cs`, `SyncClient.cs`, `SyncClientInstance.cs`. Protocol buffers are defined in `Protos/schedulersync.proto`.

**Sequencer integration** (`Sequencer/`): `TargetSchedulerSyncContainer.cs` (sync-aware sequence container), `SyncTakeExposure.cs`, `TargetSchedulerSyncWait.cs`.

Sync is opt-in and initialized via `SyncEnabled()` in `TargetScheduler.cs`. The server/client role is determined by plugin preferences at startup.

### API

TS exposes a REST API for external integrations.
- **`API/`** — `APIController.cs`, `APIServer.cs`, `TargetSchedulerAPI.yml` (OpenAPI spec).
- The server is initialized in `TargetScheduler.cs` alongside the sync service.

### Testing

Tests live in `NINA.Plugin.TargetScheduler.Test/`.
- **Framework:** NUnit (`[TestFixture]`, `[Test]`, `[SetUp]`)
- **Mocking:** Moq (`Mock<T>`, `.Setup()`, `.Returns()`)
- **Assertions:** FluentAssertions (`.Should()`, `.Be()`, etc.)

Coverage spans: planning engine, scoring rules, astrometry, database schema/migrations, sequencer logic, API. External native DLLs (NOVAS, SOFA, SQLite x64) required by tests are bundled under `Test/External/`.
