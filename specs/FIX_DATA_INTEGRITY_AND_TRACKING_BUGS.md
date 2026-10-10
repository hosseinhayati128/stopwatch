# Spec & Prompt: Fix Time-Tracking Data Integrity & Stopwatch Engine Bugs

> **Instructions for AI Agent / Gemini:**
> Execute the following tasks step-by-step to fix 5 identified data integrity bugs and engine flaws in the `StopwatchOverlay` project.
> Thoroughly verify the root causes, apply the code modifications across the solution, add/update unit tests, and provide a one-time historical data sanitization migration.

---

## Background & Problem Summary

A comprehensive audit of `project-history.json`, `Stopwatch Log.md`, and `Focus Log.md` revealed 5 specific bugs and architectural gaps in how time intervals and session records are created, edited, and synchronized:

1. **Periodic Review Duplication Bug:** Saving a Periodic Review duplicates all existing intervals in the review period (24 duplicate pairs accumulated across 6 projects).
2. **Runaway / Overnight Timers:** Timers forgotten overnight run unchecked (e.g., `Apply` logged 14h 23m on 2026-09-30), swallowing other sessions.
3. **Micro-Interval Time Inflation:** Accidental 1–2 second clicks create records with `Duration (min) = 1`, inflating project totals by dozens of minutes.
4. **Concurrent Multi-Timer Collisions:** Running multiple overlays simultaneously creates overlapping intervals, causing hourly and daily summaries to exceed 24 hours.
5. **Excessive Multi-Hour Pauses in Focus Log:** Pausing a timer for 6+ hours is recorded as a single massive in-timer break (e.g., 366 minutes) rather than concluding the session.

---

## Step 1: Fix Periodic Review Deletion & Interval Duplication

### 1.1 Root Cause Analysis
In `PeriodicReviewWindow.xaml.cs` (`ExecuteSave`, line ~2319), cleanup slots for existing intervals are constructed as:
```csharp
slotsToSave.Add(new PeriodicReviewStopwatchSlot
{
    ExistingIntervalId = existingSlot.ExistingIntervalId,
    SelectedProjectName = null, // Intended to signal deletion
    StartUtc = existingSlot.StartUtc,
    EndUtc = existingSlot.EndUtc
});
```
`OriginalProjectName` is omitted and defaults to `null`.

In `StopwatchOverlay.Core\PeriodicReview\PeriodicReviewDataAggregator.cs`:
```csharp
public bool HasChanged =>
    !string.Equals(OriginalProjectName?.Trim() ?? "", SelectedProjectName?.Trim() ?? "", StringComparison.OrdinalIgnoreCase)
    || (IsManuallyAdded && !string.IsNullOrWhiteSpace(SelectedProjectName));
```
Because both `OriginalProjectName` and `SelectedProjectName` are `null`, `string.Equals("", "")` is `true`, causing `HasChanged` to evaluate to `false`.

Consequently, in `ControllerWindow.xaml.cs` (`ApplyPeriodicReviewSlots`, line ~537):
```csharp
if (slot.HasChanged)
{
    ...
    else if (slot.ExistingIntervalId.HasValue && string.IsNullOrWhiteSpace(slot.SelectedProjectName))
    {
        _projectHistory.DeleteClosedInterval(slot.ExistingIntervalId.Value); // SKIPPED!
    }
}
```
The pre-existing interval is never deleted. The repack then adds a new manual interval (`IsManuallyAdded = true`), creating duplicate records for every session in the reviewed period.

### 1.2 Implementation Steps
1. **Fix `PeriodicReviewStopwatchSlot.HasChanged`** in `PeriodicReviewDataAggregator.cs`:
   ```csharp
   public bool HasChanged =>
       (ExistingIntervalId.HasValue && string.IsNullOrWhiteSpace(SelectedProjectName)) // Explicit deletion
       || !string.Equals(OriginalProjectName?.Trim() ?? "", SelectedProjectName?.Trim() ?? "", StringComparison.OrdinalIgnoreCase)
       || (IsManuallyAdded && !string.IsNullOrWhiteSpace(SelectedProjectName));
   ```
2. **Preserve `OriginalProjectName` in `PeriodicReviewWindow.xaml.cs`**:
   In `ExecuteSave`:
   ```csharp
   slotsToSave.Add(new PeriodicReviewStopwatchSlot
   {
       ExistingIntervalId = existingSlot.ExistingIntervalId,
       OriginalProjectName = existingSlot.OriginalProjectName ?? existingSlot.SelectedProjectName,
       SelectedProjectName = null,
       StartUtc = existingSlot.StartUtc,
       EndUtc = existingSlot.EndUtc
   });
   ```
3. **Unit Tests:** Add unit tests in `StopwatchOverlay.Tests` verifying that `HasChanged` returns `true` when `ExistingIntervalId` is set and `SelectedProjectName` is null, and verify that `ApplyPeriodicReviewSlots` successfully calls `DeleteClosedInterval`.

---

## Step 2: Discard Micro-Intervals & Fix Duration Rounding Inflation

### 2.1 Problem
Accidental start-and-immediate-stop misclicks (1–2 seconds) create permanent database intervals. In `ObsidianLogSync.cs`:
- Duration string outputs `< 1m`.
- But `Duration (min)` column outputs `1`.
- Dataview and dashboard scripts sum the `Duration (min)` column, turning twenty 1-second misclicks into 20 extra minutes of tracked time.

### 2.2 Implementation Steps
1. **Minimum Duration Filtering on Session Stop:**
   - In `ProjectTimeHistory.cs` / `ControllerWindow.xaml.cs` (when closing an interval on timer stop):
   - If `(endUtc - startUtc).TotalSeconds < 5`, do not record the interval to history (or provide an app setting `MinimumIntervalSeconds = 5`, default 5).
2. **Fix `ObsidianLogSync.cs` Duration (min) Column:**
   - When `Math.Round(duration.TotalMinutes) <= 0`:
     - The numeric minute column should output `0` instead of `1` (or calculate fractional/floor minutes so `< 1m` records do not add full minutes to cumulative totals).
3. **Unit Tests:** Verify that intervals < 5s are discarded or recorded as `0` min in export sync.

---

## Step 3: Runaway Timer Protection & Midnight Crossover Safeguards

### 3.1 Problem
When Idle Stop is inactive or unconfigured for a specific project, an unattended timer can run indefinitely (e.g. `Apply` running 14 hours overnight from 16:05 to 06:28).

### 3.2 Implementation Steps
1. **Global Maximum Runaway Ceiling:**
   - Add a global safety setting: `MaxContinuousTimerHours` (default: 8 hours).
   - If an active timer exceeds this continuous threshold without user keyboard/mouse activity:
     - Automatically pause the timer at the threshold.
     - Post a notification or visual banner: *"Timer paused automatically after reaching maximum continuous limit."*
2. **Post-Resume Runaway Validation:**
   - If a timer has been running unattended across midnight (e.g. > 6 hours), prompt the user on stop:
     - *"This timer ran for [X] hours. Would you like to keep the full duration, cap it at [Y] hours, or discard it?"*
3. **Unit Tests:** Verify timer pausing or clamping when exceeding the runaway limit.

---

## Step 4: Concurrent Multi-Timer Management & Overlap Handling

### 4.1 Problem
When multiple timer overlays run concurrently, their intervals overlap in time (e.g., `Family` running for 2 hours while `Navid` is started for 15 minutes). 
In daily summaries and stacked charts, time double-counts and can exceed 24 hours in a single day.

### 4.2 Implementation Steps
1. **Exclusive Timer Mode Option:**
   - Add a setting in `AppSettings.cs`: `ExclusiveTimerMode` (default: false or true based on user preference).
   - When enabled: Starting any timer automatically pauses any other currently running timer.
2. **Category / Project Collision Warning (Optional UI signal):**
   - In `ControllerWindow`, when starting a second timer while one is already running, indicate in the status bar or overlay that multiple timers are concurrent.
3. **Dashboard Union Calculation:**
   - In `Stopwatch Dashboard.md` (DataviewJS):
   - When calculating **Total Tracked Time** for a day, compute the temporal union (merged non-overlapping minutes) alongside the sum of individual project cards, preventing totals > 24 hours.

---

## Step 5: Cap Multi-Hour In-Timer Pauses in Focus Log

### 5.1 Problem
In `Focus Log.md`, when a timer is paused for 6+ hours (e.g. at 11:58 and resumed at 18:04), the pause is recorded as a single massive distraction/break (`366m / 6h 6m`), skewing break analytics.

### 5.2 Implementation Steps
1. **Focus Pause Threshold:**
   - In `FocusBreakTracker.cs` (or where pause intervals are finalized):
   - If a pause exceeds `MaxTrackedPauseMinutes` (e.g. 120 minutes / 2 hours):
     - Treat the timer session as effectively completed/reset upon resume, or record the break capped at the threshold with a note `Extended break / inactive`.
2. **Unit Tests:** Verify that extended pauses exceeding the threshold do not log unbounded multi-hour break durations.

---

## Step 6: Historical Data Sanitization & Deduplication Migration

### 6.1 Problem
`project-history.json` and `Stopwatch Log.md` currently contain:
- 24 near-duplicate interval pairs (same project, start and end within 60 seconds).
- 22 zero/micro-intervals (< 3 seconds).
- One runaway 14h 23m interval on 2026-09-30 (`Apply`).

### 6.2 Implementation Steps
Create a data migration utility (or run on startup in `ProjectTimeStore.cs`):
1. **Deduplication:**
   - Scan all closed intervals grouped by project.
   - If interval `A` (high-precision timestamp) and interval `B` (rounded minute timestamp) have `abs(A.Start - B.Start) <= 65s` and `abs(A.End - B.End) <= 65s`:
     - Keep interval `A` (original precision) and remove redundant duplicate `B`.
2. **Micro-Interval Cleanup:**
   - Remove intervals where `(EndUtc - StartUtc).TotalSeconds < 3`.
3. **Sync to Obsidian:**
   - Save the cleaned `project-history.json` and invoke `ObsidianLogSync.SyncHistory()` to regenerate a clean, accurate [Stopwatch Log.md](file:///c:/Users/h128/Documents/HCourses/SecondBrain/Time/Stopwatch%20Log.md).

---

## Verification & Acceptance Criteria
- [ ] Run `dotnet test` on `StopwatchOverlay.Tests` — all existing and new tests pass.
- [ ] Periodic Review save test: existing intervals in the review window are correctly replaced without duplication.
- [ ] Starting and stopping a timer within 2 seconds does not pollute `project-history.json`.
- [ ] Historical duplicates in `project-history.json` are cleanly removed and `Stopwatch Log.md` is re-synced.
