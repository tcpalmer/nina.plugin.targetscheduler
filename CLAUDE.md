# CLAUDE.md

This file is a guide for coding agents working in 'NINA.Plugin.TargetScheduler.sln'.

## Scope

This guide covers all projects listed in 'NINA.Plugin.TargetScheduler.sln'.

## Implicit Command Approvals

Proceed without asking for confirmation before running any read-only operation — shell commands, web fetches, file reads, build verification. Ask first for anything that writes to disk, modifies remote state, or could be destructive. If the intent is ambiguous, ask.

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

The NINA repository is https://github.com/isbeorn/nina. However, all TS interaction with runtime NINA is via the https://github.com/isbeorn/nina/tree/develop/NINA.Plugin package, also available from https://www.nuget.org/packages/NINA.Plugin/.

## Code Style And Formatting

- Maintain the existing line-ending style of every touched file; default to CRLF for new files unless the target location dictates LF.
- Treat the root [`.editorconfig`](.editorconfig) as the canonical C# style source. It covers indentation, line endings, namespace style, `using` placement, `var` preferences, naming, and selected analyzer severities.
- For XAML, follow surrounding file style; no repo-wide XAML formatter configuration is checked in.
- Prefer modern C# supported by the target project. For new or refactored MVVM code, prefer `CommunityToolkit.Mvvm` where it fits instead of expanding legacy relay-command patterns.
- Remove any unused `using` directives before saving a file.

## When Making Changes

- **Protected branches**: If the current branch is `main` or `release/nightly-3.3`, refuse all code changes and warn the user. Read-only tasks — analysis, research, design discussion — are permitted on any branch.
- Scope each change to the stated task only. Don't fix unrelated issues, restructure surrounding code, or expand the footprint beyond what was asked.
- Surface ambiguities before writing code rather than making assumptions. One clarifying question upfront beats an unwanted implementation.
- Propose targeted refactorings only when they directly serve the current task, and always call them out explicitly so the user can accept or skip them.
- After any code change, verify the project builds without new errors before reporting the task complete.
- Run affected tests when modifying logic that has test coverage; add tests for new behaviour where a clear test exists nearby to follow.
- Don't add new NuGet package dependencies without explicit approval.

## Plugin Components

### Database

The TS database code is under the Database folder. Subfolders include:
- Schema: EF files for each table
- Migrate: scripts to migrate to a new TS version that includes database changes. The scripts are all relative to the current database state, starting with tables defined in the Initial folder.

The `SchedulerDatabaseContext` class handles virtually all database operations as well as automatically detecting the need to run new migration scripts.

#### Querying the live database from PowerShell

`sqlite3` is not installed on this machine. Use the `System.Data.SQLite.dll` bundled with the built plugin instead:

```powershell
Add-Type -Path "C:\Users\Tom\source\repos\nina.plugin.targetscheduler\NINA.Plugin.TargetScheduler\bin\Debug\net10.0-windows7.0\System.Data.SQLite.dll"

$conn = New-Object System.Data.SQLite.SQLiteConnection("Data Source=C:\Users\Tom\AppData\Local\NINA\SchedulerPlugin\schedulerdb.sqlite;Version=3;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "-- SQL here"
# $reader = $cmd.ExecuteReader()   # SELECT multiple rows
# $cmd.ExecuteScalar()             # SELECT single value
# $cmd.ExecuteNonQuery()           # INSERT / UPDATE / DELETE
$conn.Close()
```

Always use parameterised queries (`$cmd.Parameters.AddWithValue("@name", $value)`) rather than string interpolation for values.

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
All supported symbols are listed in `SymbolPublisher.cs`. Symbol values are updated by `SymbolEventHandler` in response to `TargetSchedulerEventMediator` events.

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

#### Adding a new synced feature

Each synced feature follows the same pattern. Use `SyncSolveRotate` or `SyncAutoFocus` as the reference implementation. A new feature called `Foo` requires changes to five files, in this order:

**1. `Protos/schedulersync.proto`** — update the protocol first, before touching any C# code.
- Add a new `FooRequest` message (fields: `guid`, `fooId`; mirror `SolveRotateRequest`).
- Add `fooReady` (bool) and `fooId` (string) fields — and any extra data the client needs — to `ActionResponse`, using the next available field numbers.
- Add `rpc AcceptFoo (FooRequest) returns (StatusResponse)` and `rpc CompleteFoo (FooRequest) returns (StatusResponse)` to the service.
- If the client needs a new `ClientState` value, add it to the `ClientState` enum in the proto (not in C#).
- **Build the SyncService project** (`dotnet build`) after editing the proto. `Grpc.Tools` regenerates the C# stubs automatically; downstream code referencing new generated types won't compile until this build succeeds.

**2. `SyncManager.cs`** — add two poll-period constants:
```csharp
public static readonly int SERVER_AWAIT_FOO_POLL_PERIOD = 1000;
public static readonly int SERVER_AWAIT_FOO_COMPLETE_POLL_PERIOD = 1000;
```

**3. `SyncServer.cs`** — add six pieces:
- `AutoFocusReady` → `FooReady` in the `ServerState` enum.
- `private ClientActiveState clientActiveFoos = new ClientActiveState();` field.
- Include `State == ServerState.FooReady` in the `RequestAction` condition that returns `activeActionResponse`.
- `public async Task SyncFoo(string fooId, ..., int syncActionTimeout, CancellationToken token)` — sets `activeActionResponse` with `FooReady = true` and any extra fields, sets server state to `FooReady`, polls `AllClientsInState(ClientState.Foo)` until all clients have accepted or timeout.
- `public override Task<StatusResponse> AcceptFoo(FooRequest request, ServerCallContext context)` — sets client state to the new `ClientState.Foo` value.
- `public override Task<StatusResponse> CompleteFoo(FooRequest request, ServerCallContext context)` — calls `RemoveClientFromActiveFooList`, sets client state to `Actionready`.
- `public async Task WaitForClientFooCompletion(string fooId, int timeout, CancellationToken token)` — polls `clientActiveFoos.IsEmpty()`.
- Private helpers `SetClientActiveFooList` and `RemoveClientFromActiveFooList` (copy from the SolveRotate equivalents).

**4. `SyncClient.cs`** — add three pieces:
- `public class SyncedFoo : SyncedAction` with whatever properties clients need (at minimum, `FooId`).
- `private async Task AcceptFoo(string fooId)` — sends `FooRequest` via `base.AcceptFooAsync`.
- `public async Task CompleteFoo(string fooId)` — sends `FooRequest` via `base.CompleteFooAsync`.
- In `StartRequestAction()`, add a dispatch branch after the other `if (response.XxxReady)` blocks:
```csharp
if (response.FooReady) {
    await AcceptFoo(response.FooId);
    SetClientState(ClientState.Foo);
    return new SyncedFoo(response.FooId, /* other fields */);
}
```

**5. `TargetSchedulerSyncContainer.cs`** — wire up the client side:
- Add a detection block in the `Execute` loop after the other `syncedAction is SyncedXxx` blocks:
```csharp
if (syncedAction is SyncedFoo syncedFoo) {
    DisplayText = "Running synced foo";
    TSLogger.Info($"SYNC client received foo: {syncedFoo.FooId}");
    await DoSyncedFoo(syncedFoo, progress, token);
}
```
- Implement `DoSyncedFoo` to perform the actual work and then call `SyncClient.Instance.CompleteFoo(syncedFoo.FooId)`.

**Key design notes:**
- `ActionResponse` goes **server → client** (carries what the client should do). `FooRequest` goes **client → server** (acknowledgement/completion).
- `ClientActiveState` is a `ConcurrentDictionary` that tracks which registered clients are still executing an action. `SetClientActiveFooList` populates it from clients currently in the `Foo` state; `RemoveClientFromActiveFooList` removes one entry as each client calls `CompleteFoo`.
- `ServerState` is a C#-only enum in `SyncServer.cs`; `ClientState` lives in the proto and is shared across the wire.

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

## Release Process

The three PowerShell scripts in `Utilities/` automate the multi-step TS release workflow. All three accept a single mandatory `-Version` parameter (e.g. `'6.1.2.3'`) and must be run in order. Do **not** suggest running these scripts or run them yourself without explicit permission from the user.

### releasePart1.ps1 — Package artifacts

Prerequisite: a clean build must have been done manually beforehand.

What it does:
1. Renames the plugin DLL inside `%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINA.Plugin.TargetScheduler\` from `NINA.Plugin.TargetScheduler.dll` to `NINA.Plugin.TargetScheduler-{Version}.dll`.
2. Renames the plugin folder from `NINA.Plugin.TargetScheduler` to `NINA.Plugin.TargetScheduler-{Version}`.
3. Moves the versioned folder into the local package repo at `C:\Users\Tom\source\repos\package\NINA.Plugin.TargetScheduler\`.
4. Runs `CreateNET7Manifest.ps1` (from the package repo) against the versioned DLL to produce `manifest.json` and a zip archive. The `-installerUrl` points to the expected GitHub release download URL for the zip.
5. Moves the generated zip into the package subdirectory alongside the versioned folder.

Output: `C:\Users\Tom\source\repos\package\NINA.Plugin.TargetScheduler\NINA.Plugin.TargetScheduler-{Version}.zip` and a `manifest.json` in the package root.

### releasePart2.ps1 — Create GitHub release

Prerequisite: Part 1 must have completed successfully (zip must exist).

What it does:
1. Reads `CHANGELOG.md` and extracts the body of the topmost `##` section as release notes.
2. Creates a GitHub release tagged `v{Version}` on `tcpalmer/nina.plugin.targetscheduler` (via `gh release create`), marked as latest, with those release notes.
3. Attaches the plugin zip from Part 1 as a release asset.

GitHub automatically generates source-code zip and tar.gz archives for every tagged release.

### releasePart3.ps1 — Update NINA plugin manifests fork

Prerequisite: Parts 1 and 2 must have completed; `manifest.json` must exist at `C:\Users\Tom\source\repos\package\manifest.json`.

What it does:
1. Syncs the fork `tcpalmer/nina.plugin.manifests` with its upstream (via `gh repo sync`).
2. Checks out `main` and pulls in the local clone at `C:\Users\Tom\source\repos\nina.plugin.manifests`.
3. Creates a new branch `feature/Target_Scheduler-{Version}`.
4. Interactively prompts for each existing manifest JSON in `manifests\t\Target Scheduler\3.0.0\`: enter a new name to rename it, `d` to delete it, or Enter to keep it.
5. Copies `manifest.json` into that directory as `manifest-{Version}.json`.
6. Commits all changes with message `Target Scheduler {Version}` and pushes the branch to the fork.

After Part 3 completes, the user manually opens a pull request against the upstream manifests repo from the pushed branch.
