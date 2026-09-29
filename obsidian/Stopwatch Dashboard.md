# ⏱️ Stopwatch & Activity Dashboard

```dataviewjs
// ==========================================
// 1. Locate and Parse Log Files & Project Rules
// ==========================================
const stopwatchFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "stopwatch log.md");
const activityFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "activitywatch log.md");
const internetFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "internet log.md");
const focusFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "focus log.md");
const rulesFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "project rules.json");

if (!stopwatchFile) {
    dv.paragraph("⚠️ *Could not find `Stopwatch Log.md` in your vault.*");
    return;
}

// Load Project-Specific Distraction Rules
let projectRules = {};
if (rulesFile) {
    try {
        const rulesContent = await app.vault.read(rulesFile);
        projectRules = JSON.parse(rulesContent);
    } catch (e) {
        console.error("Error reading Project Rules.json:", e);
    }
}

async function saveProjectRules() {
    const jsonStr = JSON.stringify(projectRules, null, 2);
    let rFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "project rules.json");
    if (rFile) {
        await app.vault.modify(rFile, jsonStr);
    } else {
        const folder = stopwatchFile.parent ? stopwatchFile.parent.path : "";
        const targetPath = folder ? `${folder}/Project Rules.json` : "Project Rules.json";
        await app.vault.create(targetPath, jsonStr);
    }
}

// 2. Read and parse Stopwatch Log
const stopwatchContent = await app.vault.read(stopwatchFile);
const swLines = stopwatchContent.split(/\r?\n/);
const allRecords = [];

for (const line of swLines) {
    if (!line.startsWith("|") || line.includes("---") || line.includes("Duration (min)")) continue;
    const parts = line.split("|").map(p => p.trim());
    if (parts.length >= 8) {
        const dateStr = parts[1];
        const project = parts[2];
        const start = parts[3];
        const end = parts[4];
        const minutes = parseFloat(parts[5]) || 0;
        const durationText = parts[6];

        if (dateStr && project && minutes > 0) {
            const [y, m, d] = dateStr.split("-").map(Number);
            allRecords.push({
                dateStr,
                dateObj: new Date(y, m - 1, d),
                project,
                start,
                end,
                minutes,
                durationText
            });
        }
    }
}

// 3. Read and parse ActivityWatch Log (if present)
const allActivityRecords = [];
if (activityFile) {
    const awContent = await app.vault.read(activityFile);
    const awLines = awContent.split(/\r?\n/);
    for (const line of awLines) {
        if (!line.startsWith("|") || line.includes("---") || line.includes("Duration (min)")) continue;
        const trimmed = line.trim().replace(/^\|/, "").replace(/\|$/, "");
        const rawParts = trimmed.split("|").map(p => p.trim());
        if (rawParts.length >= 7) {
            const dateStr = rawParts[0];
            const activity = rawParts[1].replace(/\*\*/g, "").trim();
            const type = rawParts[rawParts.length - 1];
            const durationText = rawParts[rawParts.length - 2];
            const minutes = parseFloat(rawParts[rawParts.length - 3]) || 0;
            const end = rawParts[rawParts.length - 4];
            const start = rawParts[rawParts.length - 5];
            const details = rawParts.slice(2, rawParts.length - 5).join(" - ").replace(/\\\|/g, " - ");

            if (/^\d{4}-\d{2}-\d{2}$/.test(dateStr) && /^\d{1,2}:\d{2}$/.test(start) && /^\d{1,2}:\d{2}$/.test(end) && minutes > 0) {
                const [y, m, d] = dateStr.split("-").map(Number);
                allActivityRecords.push({
                    dateStr,
                    dateObj: new Date(y, m - 1, d),
                    activity,
                    details,
                    start,
                    end,
                    minutes,
                    durationText,
                    type
                });
            }
        }
    }
}

// 3b. Read and parse Internet Log (if present)
function getCleanNetworkName(raw) {
    if (!raw) return "Unknown";
    let s = raw.trim();
    s = s.replace(/^[📶🔌🌐\s]+/, "");
    s = s.replace(/\s*\(\d+%\)$/, "");
    return s.trim() || raw.trim();
}

const allInternetRecords = [];
if (internetFile) {
    const netContent = await app.vault.read(internetFile);
    const netLines = netContent.split(/\r?\n/);
    let currentDateStr = null;

    for (const line of netLines) {
        const dateMatch = line.match(/^##\s*📅\s*(\d{4}-\d{2}-\d{2})/);
        if (dateMatch) {
            currentDateStr = dateMatch[1];
            continue;
        }

        if (!line.startsWith("|") || line.includes("---") || line.includes("Network / Wi-Fi") || !currentDateStr) continue;
        const trimmed = line.trim().replace(/^\|/, "").replace(/\|$/, "");
        const rawParts = trimmed.split("|").map(p => p.trim());
        if (rawParts.length >= 6) {
            const time = rawParts[0];
            const status = rawParts[1];
            const network = rawParts[2];
            const pingStr = rawParts[3];
            const speedStr = rawParts[4];
            const notes = rawParts[5];

            const isOffline = status.includes("Offline");
            const parsedPing = parseFloat(pingStr);
            const pingMs = (!isNaN(parsedPing) && parsedPing > 0) ? parsedPing : null;
            const parsedSpeed = parseFloat(speedStr);
            let speedMbps = !isNaN(parsedSpeed) ? parsedSpeed : (isOffline ? 0 : null);
            let speedEstimated = false;

            // Handle legacy records where speed test timed out on low-throughput connection (<0.8 Mbps for 1MB in 10s)
            if (speedMbps == null && !isOffline && notes && (notes.includes("HttpClient.Timeout") || notes.includes("operation was canceled"))) {
                speedMbps = 0.4;
                speedEstimated = true;
            }

            const [y, m, d] = currentDateStr.split("-").map(Number);
            const [hh, mm] = time.split(":").map(Number);
            const cleanNet = getCleanNetworkName(network);

            allInternetRecords.push({
                dateStr: currentDateStr,
                dateObj: new Date(y, m - 1, d),
                time,
                hour: isNaN(hh) ? 0 : hh,
                minute: isNaN(mm) ? 0 : mm,
                status,
                network,
                cleanNetwork: cleanNet,
                pingMs,
                speedMbps,
                speedEstimated,
                notes,
                isOnline: !isOffline,
                isOptimal: status.includes("Online"),
                isSlow: status.includes("Slow"),
                isOffline
            });
        }
    }
}

// 3c. Read and parse Focus & Interruption Log (if present)
const allFocusRecords = [];
if (focusFile) {
    const focusContent = await app.vault.read(focusFile);
    const fLines = focusContent.split(/\r?\n/);
    for (const line of fLines) {
        if (!line.startsWith("|") || line.includes("---") || line.includes("Duration (min)")) continue;
        const trimmed = line.trim().replace(/^\|/, "").replace(/\|$/, "");
        const rawParts = trimmed.split("|").map(p => p.trim());
        if (rawParts.length >= 8) {
            const dateStr = rawParts[0];
            const project = rawParts[1];
            const start = rawParts[2];
            const resume = rawParts[3];
            const minutes = parseFloat(rawParts[4]) || 0;
            const durationText = rawParts[5];
            const category = rawParts[6];
            const reason = rawParts[7];
            const note = rawParts.length > 8 ? rawParts[8] : "";

            if (/^\d{4}-\d{2}-\d{2}$/.test(dateStr) && minutes >= 0) {
                const [y, m, d] = dateStr.split("-").map(Number);
                allFocusRecords.push({
                    dateStr,
                    dateObj: new Date(y, m - 1, d),
                    project,
                    start,
                    resume,
                    minutes,
                    durationText,
                    category,
                    reason,
                    isDistraction: reason === "Distraction",
                    isBreak: reason === "Break" || reason === "ValidBreak",
                    isIdea: reason === "Idea" || reason === "IdeaBrainstorming",
                    isTaskSwitch: reason === "TaskSwitch" || reason.toLowerCase().includes("switch")
                });
            }
        }
    }
}

// Distraction detection helper with project-specific rules
const defaultDistractions = [
    'telegram', 'youtube', 'netflix', 'instagram', 'twitter', 'x.com',
    'reddit', 'facebook', 'tiktok', 'discord', 'spotify', 'twitch', 'whatsapp', 'animworld'
];

function isDistraction(activity, details, projectName) {
    const act = (activity || "").toLowerCase().trim();
    const text = ((activity || "") + " " + (details || "")).toLowerCase();

    // 1. Check Project-Specific Rules
    if (projectName && projectRules[projectName]) {
        const rules = projectRules[projectName];
        if (rules.workTools && rules.workTools.some(w => act === w.toLowerCase() || text.includes(w.toLowerCase()))) {
            return false;
        }
        if (rules.distractions && rules.distractions.some(d => act === d.toLowerCase() || text.includes(d.toLowerCase()))) {
            return true;
        }
    }

    // 2. Global Fallback
    return defaultDistractions.some(p => text.includes(p));
}

async function toggleAppCategory(activity, projectName) {
    if (!projectName || projectName === "__all__") return;
    const act = (activity || "").trim();
    if (!act) return;

    if (!projectRules[projectName]) {
        projectRules[projectName] = { distractions: [], workTools: [] };
    }
    const rules = projectRules[projectName];
    if (!rules.distractions) rules.distractions = [];
    if (!rules.workTools) rules.workTools = [];

    const isDist = isDistraction(act, "", projectName);
    const actLower = act.toLowerCase();

    if (isDist) {
        rules.distractions = rules.distractions.filter(d => d.toLowerCase() !== actLower);
        if (!rules.workTools.some(w => w.toLowerCase() === actLower)) {
            rules.workTools.push(act);
        }
    } else {
        rules.workTools = rules.workTools.filter(w => w.toLowerCase() !== actLower);
        if (!rules.distractions.some(d => d.toLowerCase() === actLower)) {
            rules.distractions.push(act);
        }
    }

    await saveProjectRules();
    renderDashboard();
}

async function removeAppRule(activity, projectName) {
    if (!projectName || !projectRules[projectName]) return;
    const actLower = (activity || "").trim().toLowerCase();
    const rules = projectRules[projectName];
    if (rules.distractions) rules.distractions = rules.distractions.filter(d => d.toLowerCase() !== actLower);
    if (rules.workTools) rules.workTools = rules.workTools.filter(w => w.toLowerCase() !== actLower);
    await saveProjectRules();
    renderDashboard();
}

function toDecimalHour(timeStr) {
    if (!timeStr || typeof timeStr !== "string") return -1;
    const parts = timeStr.trim().split(":");
    if (parts.length !== 2) return -1;
    const h = parseInt(parts[0], 10);
    const m = parseInt(parts[1], 10);
    return isNaN(h) || isNaN(m) ? -1 : h + m / 60;
}

function toTotalMinutes(timeStr) {
    if (!timeStr || typeof timeStr !== "string") return -1;
    const parts = timeStr.trim().split(":");
    if (parts.length !== 2) return -1;
    const h = parseInt(parts[0], 10);
    const m = parseInt(parts[1], 10);
    return isNaN(h) || isNaN(m) ? -1 : h * 60 + m;
}

function formatMinutes(min) {
    const h = Math.floor(min / 60);
    const m = Math.round(min % 60);
    if (h === 0) return `${m}m`;
    if (m === 0) return `${h}h`;
    return `${h}h ${m}m`;
}

// 4. UI Container & Button Bar
const root = this.container;
root.innerHTML = "";

const filterBar = root.createDiv({ cls: "stopwatch-filters" });
filterBar.style.display = "flex";
filterBar.style.gap = "8px";
filterBar.style.marginBottom = "18px";
filterBar.style.flexWrap = "wrap";

const statsCard = root.createDiv({ cls: "stopwatch-stats" });
statsCard.style.padding = "12px 16px";
statsCard.style.marginBottom = "20px";
statsCard.style.borderRadius = "8px";
statsCard.style.backgroundColor = "var(--background-secondary)";
statsCard.style.border = "1px solid var(--background-modifier-border)";

const chartSection = root.createDiv({ cls: "stopwatch-charts" });

const filters = [
    { id: "today", label: "Today" },
    { id: "week", label: "This Week" },
    { id: "month", label: "This Month" },
    { id: "all", label: "All Time" }
];

let activeFilter = "week"; // Default filter

// Zoom window state for Dual Timeline (in hours: 0 to 24)
let zoomStartHour = 0;
let zoomEndHour = 24;

// Selected date for daily timelines (defaults to most recent date available)
const availableDates = Array.from(new Set([
    ...allRecords.map(r => r.dateStr),
    ...allActivityRecords.map(r => r.dateStr),
    ...allInternetRecords.map(r => r.dateStr),
    ...allFocusRecords.map(r => r.dateStr)
])).sort().reverse();

let selectedDailyDate = availableDates[0] || new Date().toISOString().split("T")[0];

function getFilteredRecords(filterId) {
    const now = new Date();
    const todayStr = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;

    let matchFn;
    if (filterId === "today") {
        matchFn = r => r.dateStr === todayStr;
    } else if (filterId === "week") {
        const startOfWeek = new Date(now);
        const day = (now.getDay() + 6) % 7;
        startOfWeek.setDate(now.getDate() - day);
        startOfWeek.setHours(0, 0, 0, 0);
        matchFn = r => r.dateObj >= startOfWeek;
    } else if (filterId === "month") {
        matchFn = r => r.dateObj.getFullYear() === now.getFullYear() && r.dateObj.getMonth() === now.getMonth();
    } else {
        matchFn = () => true;
    }

    return {
        stopwatch: allRecords.filter(matchFn),
        activity: allActivityRecords.filter(matchFn),
        internet: allInternetRecords.filter(matchFn),
        focus: allFocusRecords.filter(matchFn)
    };
}

const palette = [
    '#38bdf8', '#34d399', '#fbbf24', '#a78bfa',
    '#f472b6', '#4ade80', '#fb923c', '#818cf8',
    '#e879f9', '#2dd4bf', '#f87171', '#60a5fa'
];

// Project color assignment helper
const allProjectNames = Array.from(new Set(allRecords.map(r => r.project))).sort();
const projectColorMap = {};
allProjectNames.forEach((proj, idx) => {
    projectColorMap[proj] = palette[idx % palette.length];
});

// Project Deep Dive state (separated Project, Scope, Date)
let deepDiveProject = allProjectNames[0] || "__all__";
let deepDiveScope = "day"; // "day", "week", "month", "all"
let deepDiveDate = availableDates[0] || new Date().toISOString().split("T")[0];
let deepDiveSelectedSessionIdx = -1; // -1 = all sessions in this period combined
let deepDiveAppFilter = "App"; // "App", "Web", "all"
let deepDiveTimelineOpen = false; // Collapsed by default when choosing a project

// Internet Performance & Wi-Fi state
let netSelectedWifi = "__all__"; // "__all__" or specific clean network name
let netViewMode = "hourly_profile"; // "hourly_profile" or "timeline"
let netHourlyScope = "7"; // "7", "14", "30", "period"
let netShowAvgSpeed = true;
let netShowFluctuations = true;
let netShowLatency = true;
let netGranularity = 15; // 5, 15, 30, 60
let netTimelineAggregation = "15"; // "15", "30", "60", "raw"
let netProfileResolution = 60; // 15 (15m), 30 (30m), 60 (1h)

// Trend (Hours by Project) State
let trendGranularity = "day"; // "day", "week", "month"
let trendWindowSize = 14;     // default 14 for day, 8 for week, 6 for month, or "all"
let trendOffset = 0;          // 0 = latest period, > 0 = shifted back into history
let trendWheelMode = "pan";   // "pan" (scroll navigates periods) or "zoom" (scroll changes window size)

// Time by Project State
let timeByProjectScope = "week"; // "day", "week", "month", "year", "all"
let timeByProjectDate = availableDates[0] || new Date().toISOString().split("T")[0];

function getScopeRange(scope, dateStr) {
    if (!dateStr) dateStr = new Date().toISOString().split("T")[0];
    const [y, m, d] = dateStr.split("-").map(Number);
    const refDate = new Date(y, m - 1, d);

    if (scope === "day") {
        const start = new Date(y, m - 1, d, 0, 0, 0, 0);
        const end = new Date(y, m - 1, d, 23, 59, 59, 999);
        return { start, end, label: dateStr };
    } else if (scope === "week") {
        const start = new Date(refDate);
        const day = (refDate.getDay() + 6) % 7; // Monday = 0
        start.setDate(refDate.getDate() - day);
        start.setHours(0, 0, 0, 0);
        const end = new Date(start);
        end.setDate(start.getDate() + 6);
        end.setHours(23, 59, 59, 999);
        const sStr = `${start.getFullYear()}-${String(start.getMonth() + 1).padStart(2, '0')}-${String(start.getDate()).padStart(2, '0')}`;
        const eStr = `${end.getFullYear()}-${String(end.getMonth() + 1).padStart(2, '0')}-${String(end.getDate()).padStart(2, '0')}`;
        return { start, end, label: `Week of ${sStr} to ${eStr}` };
    } else if (scope === "month") {
        const start = new Date(y, m - 1, 1, 0, 0, 0, 0);
        const end = new Date(y, m, 0, 23, 59, 59, 999);
        const mNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
        return { start, end, label: `${mNames[m - 1]} ${y}` };
    } else if (scope === "year") {
        const start = new Date(y, 0, 1, 0, 0, 0, 0);
        const end = new Date(y, 11, 31, 23, 59, 59, 999);
        return { start, end, label: `Year ${y}` };
    } else {
        return { start: new Date(2000, 0, 1), end: new Date(2100, 0, 1), label: "All Time" };
    }
}

function shiftDate(dateStr, scope, delta) {
    if (!dateStr) dateStr = new Date().toISOString().split("T")[0];
    const [y, m, d] = dateStr.split("-").map(Number);
    const dt = new Date(y, m - 1, d);
    if (scope === "day") {
        dt.setDate(dt.getDate() + delta);
    } else if (scope === "week") {
        dt.setDate(dt.getDate() + delta * 7);
    } else if (scope === "month") {
        dt.setMonth(dt.getMonth() + delta);
    } else if (scope === "year") {
        dt.setFullYear(dt.getFullYear() + delta);
    }
    const sy = dt.getFullYear();
    const sm = String(dt.getMonth() + 1).padStart(2, '0');
    const sd = String(dt.getDate()).padStart(2, '0');
    return `${sy}-${sm}-${sd}`;
}

// 5. Main Render Function
function renderDashboard() {
    filterButtons.forEach(({ id, btn }) => {
        if (id === activeFilter) {
            btn.style.backgroundColor = "var(--interactive-accent)";
            btn.style.color = "var(--text-on-accent)";
            btn.style.fontWeight = "bold";
        } else {
            btn.style.backgroundColor = "var(--background-modifier-form-field)";
            btn.style.color = "var(--text-normal)";
            btn.style.fontWeight = "normal";
        }
    });

    const { stopwatch: records, activity: awRecords, internet: netRecords, focus: focusRecords } = getFilteredRecords(activeFilter);
    chartSection.innerHTML = "";

    // Calculate Summary Stats
    const totalMinutes = records.reduce((sum, r) => sum + r.minutes, 0);
    const totalHours = (totalMinutes / 60).toFixed(1);
    const sessionCount = records.length;

    const projectTotals = {};
    for (const r of records) {
        projectTotals[r.project] = (projectTotals[r.project] || 0) + r.minutes;
    }
    const projectLabels = Object.keys(projectTotals);

    let topProject = "-";
    let maxMin = 0;
    for (const [proj, min] of Object.entries(projectTotals)) {
        if (min > maxMin) {
            maxMin = min;
            topProject = proj;
        }
    }

    // In-Timer overlap calculation
    let focusedMinDuringTimers = 0;
    let distractedMinDuringTimers = 0;
    const distractionsMap = {};
    const inTimerAppTotals = {};

    for (const sw of records) {
        const swStart = toDecimalHour(sw.start);
        let swEnd = toDecimalHour(sw.end);
        if (swEnd < swStart) swEnd += 24;

        for (const aw of awRecords) {
            if (aw.dateStr !== sw.dateStr || aw.type === "Idle") continue;
            const awStart = toDecimalHour(aw.start);
            let awEnd = toDecimalHour(aw.end);
            if (awEnd < awStart) awEnd += 24;

            const overlap = Math.max(0, Math.min(swEnd, awEnd) - Math.max(swStart, awStart));
            const overlapMin = Math.round(overlap * 60);

            if (overlapMin > 0) {
                inTimerAppTotals[aw.activity] = (inTimerAppTotals[aw.activity] || 0) + overlapMin;
                if (isDistraction(aw.activity, aw.details, sw.project)) {
                    distractedMinDuringTimers += overlapMin;
                    distractionsMap[aw.activity] = (distractionsMap[aw.activity] || 0) + overlapMin;
                } else {
                    focusedMinDuringTimers += overlapMin;
                }
            }
        }
    }

    const trackedWorkMinutes = focusedMinDuringTimers + distractedMinDuringTimers;
    const focusRate = trackedWorkMinutes > 0
        ? Math.round((focusedMinDuringTimers / trackedWorkMinutes) * 100)
        : (records.length > 0 ? 100 : 0);

    const validNetSpeeds = netRecords.filter(r => r.speedMbps != null && r.speedMbps > 0);
    const avgNetSpeed = validNetSpeeds.length > 0
        ? (validNetSpeeds.reduce((s, r) => s + r.speedMbps, 0) / validNetSpeeds.length).toFixed(1)
        : null;

    // Render Stats Card
    statsCard.innerHTML = `
        <div style="display: flex; justify-content: space-around; text-align: center; flex-wrap: wrap; gap: 8px;">
            <div>
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">Total Time</div>
                <div style="font-size: 20px; font-weight: bold; color: var(--text-accent);">${totalHours} hrs</div>
            </div>
            <div>
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">Sessions</div>
                <div style="font-size: 20px; font-weight: bold;">${sessionCount}</div>
            </div>
            <div>
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">Top Project</div>
                <div style="font-size: 20px; font-weight: bold;">${topProject}</div>
            </div>
            ${awRecords.length > 0 ? `
            <div>
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">Focus Rate</div>
                <div style="font-size: 20px; font-weight: bold; color: ${focusRate >= 80 ? '#34d399' : (focusRate >= 60 ? '#fbbf24' : '#f87171')};">${focusRate}%</div>
            </div>
            ` : ''}
            ${avgNetSpeed ? `
            <div>
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">Internet</div>
                <div style="font-size: 20px; font-weight: bold; color: #38bdf8;">${avgNetSpeed} <span style="font-size: 12px;">Mbps</span></div>
            </div>
            ` : ''}
            ${focusRecords.length > 0 ? `
            <div>
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">Pauses</div>
                <div style="font-size: 20px; font-weight: bold; color: ${focusRecords.filter(f => f.isDistraction).length > 0 ? '#f87171' : '#34d399'};">
                    ${focusRecords.length} <span style="font-size: 12px; opacity: 0.7;">(${focusRecords.filter(f => f.isDistraction).length} dist)</span>
                </div>
            </div>
            ` : ''}
        </div>
    `;

    // ==============================================================================
    // --- FEATURE 1: DUAL SYNCHRONIZED TIMELINE (ZOOMABLE & PROJECT ALIGNED) -------
    // ==============================================================================
    renderDualTimelineSection();

    // ==============================================================================
    // --- FEATURE 2: PROJECT-BY-PROJECT DEEP-DIVE (PROJECT DRILL-DOWN) -------------
    // ==============================================================================
    renderProjectDeepDiveSection();

    // ==============================================================================
    // --- FEATURE 3: INTERNET CONNECTION & WI-FI PERFORMANCE -----------------------
    // ==============================================================================
    renderInternetSection();

    // ==============================================================================
    // --- FEATURE 4: FOCUS CONTINUITY & DISTRACTION ANALYSIS -----------------------
    // ==============================================================================
    renderFocusContinuitySection();

    // ==============================================================================
    // --- Chart 1: Time by Project (Doughnut + Right-Hand Word Map) ----------------
    // ==============================================================================
    renderTimeByProjectSection();

    // ==============================================================================
    // --- Chart 2: Time of Day Breakdown (Hourly Stacked Bar by Project) [ORIGINAL] -
    // ==============================================================================
    if (records.length > 0) {
        const hourlyHeading = chartSection.createEl("h3", { text: "🕒 Activity by Time of Day" });
        hourlyHeading.style.marginTop = "32px";

        const isToday = activeFilter === "today";
        const hourlySubtitle = chartSection.createEl("p", { 
            text: isToday 
                ? "Minutes worked during each hour of the day by project." 
                : "Total hours worked across this period by time of day and project." 
        });
        hourlySubtitle.style.opacity = "0.7";
        hourlySubtitle.style.fontSize = "12px";
        hourlySubtitle.style.marginTop = "-6px";

        const hourlyContainer = chartSection.createDiv();
        const hours24 = Array.from({ length: 24 }, (_, i) => `${String(i).padStart(2, '0')}:00`);

        const projectHourly = {};
        for (const proj of projectLabels) {
            projectHourly[proj] = new Array(24).fill(0);
        }

        for (const r of records) {
            if (!r.start || !r.end) continue;
            const startDecimal = toDecimalHour(r.start);
            let endDecimal = toDecimalHour(r.end);
            if (endDecimal < startDecimal) endDecimal += 24;

            for (let h = 0; h < 24; h++) {
                const o1Start = Math.max(startDecimal, h);
                const o1End = Math.min(endDecimal, h + 1);
                const overlap1 = Math.max(0, o1End - o1Start);

                const o2Start = Math.max(startDecimal, h + 24);
                const o2End = Math.min(endDecimal, h + 25);
                const overlap2 = Math.max(0, o2End - o2Start);

                const totalOverlapMinutes = Math.round((overlap1 + overlap2) * 60);
                if (totalOverlapMinutes > 0 && projectHourly[r.project]) {
                    projectHourly[r.project][h] += totalOverlapMinutes;
                }
            }
        }

        const hourlyDatasets = projectLabels.map(proj => {
            const rawMinutes = projectHourly[proj];
            const dataValues = isToday 
                ? rawMinutes 
                : rawMinutes.map(m => +(m / 60).toFixed(1));

            return {
                label: proj,
                data: dataValues,
                backgroundColor: projectColorMap[proj],
                stack: 'timeOfDay'
            };
        });

        window.renderChart({
            type: 'bar',
            data: {
                labels: hours24,
                datasets: hourlyDatasets
            },
            options: {
                scales: {
                    x: {
                        stacked: true,
                        title: { display: true, text: 'Hour of Day' }
                    },
                    y: {
                        stacked: true,
                        beginAtZero: true,
                        title: { display: true, text: isToday ? 'Minutes' : 'Hours' }
                    }
                }
            }
        }, hourlyContainer);
    }

    // ========================================================
    // --- Chart 3: Trend by Project (Stacked Bar) ------------
    // ========================================================
    renderTrendSection();
}

// ==============================================================================
// RENDER HELPER: TIME BY PROJECT (DOUGHNUT + RIGHT-HAND WORD MAP)
// ==============================================================================
function renderTimeByProjectSection() {
    if (allRecords.length === 0) return;

    const card = chartSection.createDiv();
    card.style.padding = "18px";
    card.style.borderRadius = "8px";
    card.style.backgroundColor = "var(--background-secondary)";
    card.style.border = "1px solid var(--background-modifier-border)";
    card.style.marginBottom = "30px";

    // Header Row: Title on Left, Controls on Right
    const headerRow = card.createDiv();
    headerRow.style.display = "flex";
    headerRow.style.justifyContent = "space-between";
    headerRow.style.alignItems = "center";
    headerRow.style.flexWrap = "wrap";
    headerRow.style.gap = "10px";
    headerRow.style.marginBottom = "14px";

    const titleContainer = headerRow.createDiv();
    const heading = titleContainer.createEl("h3", { text: "📊 Time by Project" });
    heading.style.margin = "0 0 2px 0";

    const subTitle = titleContainer.createEl("p");
    subTitle.style.fontSize = "11px";
    subTitle.style.opacity = "0.7";
    subTitle.style.margin = "0";

    // Controls: Scope Buttons (Day / Week / Month / Year / All Time) + Date Navigation
    const controlsContainer = headerRow.createDiv();
    controlsContainer.style.display = "flex";
    controlsContainer.style.alignItems = "center";
    controlsContainer.style.flexWrap = "wrap";
    controlsContainer.style.gap = "8px";

    // Scope Buttons
    const scopeButtonGroup = controlsContainer.createDiv();
    scopeButtonGroup.style.display = "flex";
    scopeButtonGroup.style.alignItems = "center";
    scopeButtonGroup.style.gap = "4px";

    const scopeOptions = [
        { id: "day", label: "Day" },
        { id: "week", label: "Week" },
        { id: "month", label: "Month" },
        { id: "year", label: "Year" },
        { id: "all", label: "All Time" }
    ];

    const scopeBtns = [];
    scopeOptions.forEach(opt => {
        const btn = scopeButtonGroup.createEl("button", { text: opt.label });
        btn.style.padding = "4px 8px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        scopeBtns.push({ id: opt.id, btn });

        btn.addEventListener("click", () => {
            timeByProjectScope = opt.id;
            updateScopeBtns();
            updateDateNavVisibility();
            updateTimeByProject();
        });
    });

    function updateScopeBtns() {
        scopeBtns.forEach(({ id, btn }) => {
            const active = (id === timeByProjectScope);
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });
    }

    // Date Navigation: [<] [Date Dropdown] [>]
    const dateNavContainer = controlsContainer.createDiv();
    dateNavContainer.style.display = "flex";
    dateNavContainer.style.alignItems = "center";
    dateNavContainer.style.gap = "4px";

    const prevBtn = dateNavContainer.createEl("button", { text: "◀" });
    prevBtn.style.padding = "4px 8px";
    prevBtn.style.fontSize = "11px";
    prevBtn.style.borderRadius = "4px";
    prevBtn.style.border = "1px solid var(--background-modifier-border)";
    prevBtn.style.backgroundColor = "var(--background-modifier-form-field)";
    prevBtn.style.color = "var(--text-normal)";
    prevBtn.style.cursor = "pointer";
    prevBtn.title = "Previous period";

    const dateSelect = dateNavContainer.createEl("select");
    dateSelect.style.padding = "4px 8px";
    dateSelect.style.borderRadius = "4px";
    dateSelect.style.fontSize = "11px";
    dateSelect.style.backgroundColor = "var(--background-modifier-form-field)";
    dateSelect.style.color = "var(--text-normal)";
    dateSelect.style.border = "1px solid var(--background-modifier-border)";

    availableDates.forEach(d => {
        const opt = dateSelect.createEl("option", { text: d, value: d });
        if (d === timeByProjectDate) opt.selected = true;
    });

    const nextBtn = dateNavContainer.createEl("button", { text: "▶" });
    nextBtn.style.padding = "4px 8px";
    nextBtn.style.fontSize = "11px";
    nextBtn.style.borderRadius = "4px";
    nextBtn.style.border = "1px solid var(--background-modifier-border)";
    nextBtn.style.backgroundColor = "var(--background-modifier-form-field)";
    nextBtn.style.color = "var(--text-normal)";
    nextBtn.style.cursor = "pointer";
    nextBtn.title = "Next period";

    function updateDateNavVisibility() {
        dateNavContainer.style.display = timeByProjectScope === "all" ? "none" : "flex";
    }

    prevBtn.addEventListener("click", () => {
        timeByProjectDate = shiftDate(timeByProjectDate, timeByProjectScope, -1);
        syncDateSelect();
        updateTimeByProject();
    });

    nextBtn.addEventListener("click", () => {
        timeByProjectDate = shiftDate(timeByProjectDate, timeByProjectScope, 1);
        syncDateSelect();
        updateTimeByProject();
    });

    dateSelect.addEventListener("change", () => {
        timeByProjectDate = dateSelect.value;
        updateTimeByProject();
    });

    function syncDateSelect() {
        if (!availableDates.includes(timeByProjectDate)) {
            const opt = dateSelect.createEl("option", { text: timeByProjectDate, value: timeByProjectDate });
            opt.selected = true;
        } else {
            dateSelect.value = timeByProjectDate;
        }
    }

    // Dynamic Content Container
    const contentContainer = card.createDiv();

    function updateTimeByProject() {
        contentContainer.innerHTML = "";

        const { start: scopeStart, end: scopeEnd, label: scopeLabel } = getScopeRange(timeByProjectScope, timeByProjectDate);

        // Filter stopwatch records
        const pRecords = allRecords.filter(r => r.dateObj >= scopeStart && r.dateObj <= scopeEnd);

        const projTotals = {};
        for (const r of pRecords) {
            projTotals[r.project] = (projTotals[r.project] || 0) + r.minutes;
        }

        const sortedProjects = Object.entries(projTotals).sort((a, b) => b[1] - a[1]);
        const totalMinutes = sortedProjects.reduce((sum, p) => sum + p[1], 0);
        const totalHours = (totalMinutes / 60).toFixed(1);

        subTitle.textContent = `${totalHours}h tracked across ${sortedProjects.length} project${sortedProjects.length === 1 ? '' : 's'} · ${scopeLabel}`;

        if (sortedProjects.length === 0) {
            const emptyMsg = contentContainer.createDiv();
            emptyMsg.style.padding = "24px 16px";
            emptyMsg.style.textAlign = "center";
            emptyMsg.style.opacity = "0.7";
            emptyMsg.style.backgroundColor = "var(--background-modifier-form-field)";
            emptyMsg.style.borderRadius = "6px";
            emptyMsg.innerHTML = `No project time recorded for <strong>${scopeLabel}</strong>.`;
            return;
        }

        // Layout: Side-by-side flex row (Doughnut on Left, Word Map on Right)
        const row = contentContainer.createDiv();
        row.style.display = "flex";
        row.style.alignItems = "center";
        row.style.justifyContent = "space-around";
        row.style.flexWrap = "wrap";
        row.style.gap = "24px";
        row.style.padding = "8px 0";

        // LEFT: Doughnut Chart
        const chartWrapper = row.createDiv();
        chartWrapper.style.position = "relative";
        chartWrapper.style.width = "220px";
        chartWrapper.style.height = "220px";
        chartWrapper.style.flexShrink = "0";

        const pLabels = sortedProjects.map(p => p[0]);
        const pHours = sortedProjects.map(p => +(p[1] / 60).toFixed(1));
        const pColors = pLabels.map(p => projectColorMap[p] || '#38bdf8');

        // Doughnut center total text
        const centerDiv = chartWrapper.createDiv();
        centerDiv.style.position = "absolute";
        centerDiv.style.top = "50%";
        centerDiv.style.left = "50%";
        centerDiv.style.transform = "translate(-50%, -50%)";
        centerDiv.style.textAlign = "center";
        centerDiv.style.pointerEvents = "none";
        centerDiv.innerHTML = `
            <div style="font-size: 20px; font-weight: bold; color: var(--text-normal);">${totalMinutes < 60 ? totalMinutes + 'm' : totalHours + 'h'}</div>
            <div style="font-size: 10px; opacity: 0.6; text-transform: uppercase;">Total</div>
        `;

        window.renderChart({
            type: 'doughnut',
            data: {
                labels: pLabels,
                datasets: [{
                    label: 'Hours',
                    data: pHours,
                    backgroundColor: pColors,
                    borderWidth: 2,
                    borderColor: 'var(--background-secondary)'
                }]
            },
            options: {
                cutout: '68%',
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false }
                }
            }
        }, chartWrapper);

        // RIGHT: Word Map (Project Breakdown with names, hours, percentages, and mini bars)
        const wordMap = row.createDiv();
        wordMap.style.flex = "1";
        wordMap.style.minWidth = "260px";
        wordMap.style.maxWidth = "480px";
        wordMap.style.display = "flex";
        wordMap.style.flexDirection = "column";
        wordMap.style.gap = "7px";

        sortedProjects.forEach(([proj, mins]) => {
            const pct = totalMinutes > 0 ? Math.round((mins / totalMinutes) * 100) : 0;
            const color = projectColorMap[proj] || '#38bdf8';
            const hoursDisplay = formatMinutes(mins);

            const item = wordMap.createDiv();
            item.style.padding = "7px 10px";
            item.style.borderRadius = "6px";
            item.style.backgroundColor = "var(--background-modifier-form-field)";
            item.style.border = "1px solid var(--background-modifier-border)";
            item.style.transition = "background-color 0.15s ease";
            item.style.cursor = "pointer";
            item.title = `Click to inspect ${proj} in Deep Dive`;

            item.addEventListener("mouseenter", () => {
                item.style.backgroundColor = "var(--background-modifier-hover)";
            });
            item.addEventListener("mouseleave", () => {
                item.style.backgroundColor = "var(--background-modifier-form-field)";
            });
            item.addEventListener("click", () => {
                deepDiveProject = proj;
                deepDiveScope = timeByProjectScope === "all" ? "all" : (timeByProjectScope === "year" ? "month" : timeByProjectScope);
                deepDiveDate = timeByProjectDate;
                deepDiveTimelineOpen = false;
                renderDashboard();
            });

            item.innerHTML = `
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px;">
                    <div style="display: flex; align-items: center; gap: 8px; min-width: 0;">
                        <span style="display: inline-block; width: 10px; height: 10px; border-radius: 3px; background-color: ${color}; flex-shrink: 0;"></span>
                        <span style="font-weight: bold; font-size: 13px; color: var(--text-normal); overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">${proj}</span>
                    </div>
                    <div style="display: flex; align-items: center; gap: 8px; flex-shrink: 0; font-size: 12px;">
                        <span style="font-weight: 600; opacity: 0.9;">${hoursDisplay}</span>
                        <span style="font-size: 11px; font-weight: bold; color: ${color}; min-width: 32px; text-align: right;">${pct}%</span>
                    </div>
                </div>
                <div style="width: 100%; height: 4px; border-radius: 2px; background-color: rgba(255, 255, 255, 0.08); overflow: hidden;">
                    <div style="width: ${pct}%; height: 100%; border-radius: 2px; background-color: ${color};"></div>
                </div>
            `;
        });
    }

    updateScopeBtns();
    updateDateNavVisibility();
    updateTimeByProject();
}

// ==============================================================================
// RENDER HELPER 1: DUAL SYNCHRONIZED TIMELINE (TWO ROWS, ZOOMABLE, ALIGNED)
// ==============================================================================
function renderDualTimelineSection() {
    const card = chartSection.createDiv();
    card.style.padding = "18px";
    card.style.borderRadius = "8px";
    card.style.backgroundColor = "var(--background-secondary)";
    card.style.border = "1px solid var(--background-modifier-border)";
    card.style.marginBottom = "30px";

    // Header & Date Picker
    const headerRow = card.createDiv();
    headerRow.style.display = "flex";
    headerRow.style.justifyContent = "space-between";
    headerRow.style.alignItems = "center";
    headerRow.style.flexWrap = "wrap";
    headerRow.style.gap = "10px";

    const titleDiv = headerRow.createDiv();
    titleDiv.createEl("h3", { text: "🔍 Dual Synchronized Timeline" }).style.margin = "0";
    const subText = titleDiv.createEl("p", { 
        text: "Row 1: Stopwatch Projects | Row 2: Active Apps & Telegram. Click any project to zoom into it." 
    });
    subText.style.fontSize = "11px";
    subText.style.opacity = "0.7";
    subText.style.margin = "2px 0 0 0";

    // Date selector dropdown
    const dateSelectContainer = headerRow.createDiv();
    dateSelectContainer.style.display = "flex";
    dateSelectContainer.style.alignItems = "center";
    dateSelectContainer.style.gap = "6px";
    dateSelectContainer.createEl("span", { text: "Date:" }).style.fontSize = "12px";

    const dateSelect = dateSelectContainer.createEl("select");
    dateSelect.style.padding = "4px 8px";
    dateSelect.style.borderRadius = "4px";
    dateSelect.style.fontSize = "12px";
    dateSelect.style.backgroundColor = "var(--background-modifier-form-field)";
    dateSelect.style.color = "var(--text-normal)";
    dateSelect.style.border = "1px solid var(--background-modifier-border)";

    availableDates.forEach(d => {
        const opt = dateSelect.createEl("option", { text: d, value: d });
        if (d === selectedDailyDate) opt.selected = true;
    });

    const netBadgeContainer = dateSelectContainer.createDiv();
    function updateNetBadge() {
        netBadgeContainer.innerHTML = "";
        const dayNet = allInternetRecords.filter(r => r.dateStr === selectedDailyDate);
        if (dayNet.length > 0) {
            const last = dayNet[dayNet.length - 1];
            const badge = netBadgeContainer.createEl("span", {
                text: `${last.network}${last.speedMbps != null ? ' (' + last.speedMbps.toFixed(1) + ' Mbps)' : ''}`
            });
            badge.style.fontSize = "11px";
            badge.style.padding = "2px 7px";
            badge.style.borderRadius = "4px";
            badge.style.backgroundColor = "rgba(56, 189, 248, 0.15)";
            badge.style.color = "#38bdf8";
            badge.style.fontWeight = "bold";
            badge.title = `Active network connection on ${selectedDailyDate}`;
        }
    }
    updateNetBadge();

    dateSelect.addEventListener("change", (e) => {
        selectedDailyDate = e.target.value;
        updateNetBadge();
        drawTimelineView();
    });

    // Zoom Controls Bar
    const zoomBar = card.createDiv();
    zoomBar.style.display = "flex";
    zoomBar.style.alignItems = "center";
    zoomBar.style.gap = "8px";
    zoomBar.style.marginTop = "14px";
    zoomBar.style.marginBottom = "14px";
    zoomBar.style.flexWrap = "wrap";

    const zoomLabel = zoomBar.createEl("span", { text: "Zoom Window:" });
    zoomLabel.style.fontSize = "12px";
    zoomLabel.style.fontWeight = "bold";

    const zoomPresets = [
        { label: "Full Day (24h)", start: 0, end: 24 },
        { label: "Working Hours (09:00 - 18:00)", start: 9, end: 18 },
        { label: "Evening (18:00 - 24:00)", start: 18, end: 24 },
        { label: "Night (20:00 - 02:00)", start: 20, end: 26 }
    ];

    zoomPresets.forEach(preset => {
        const btn = zoomBar.createEl("button", { text: preset.label });
        btn.style.padding = "3px 10px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        btn.style.backgroundColor = "var(--background-modifier-form-field)";

        btn.addEventListener("click", () => {
            zoomStartHour = preset.start;
            zoomEndHour = preset.end;
            sliderStart.value = zoomStartHour;
            sliderEnd.value = zoomEndHour;
            drawTimelineView();
        });
    });

    // Filter for Row 2 (Apps vs Web vs All)
    let timelineTrackFilter = "App"; // default to foreground apps

    const filterRow = card.createDiv();
    filterRow.style.display = "flex";
    filterRow.style.alignItems = "center";
    filterRow.style.gap = "8px";
    filterRow.style.marginBottom = "10px";
    filterRow.style.fontSize = "11px";

    const filterRowLabel = filterRow.createEl("span", { text: "Row 2 Display:" });
    filterRowLabel.style.fontWeight = "bold";

    const trackFilterOptions = [
        { id: "App", label: "🖥️ Foreground Apps (Recommended)" },
        { id: "Web", label: "🌐 Web Sites" },
        { id: "all", label: "All Activity" }
    ];

    const trackFilterBtns = [];
    trackFilterOptions.forEach(opt => {
        const btn = filterRow.createEl("button", { text: opt.label });
        btn.style.padding = "3px 10px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        trackFilterBtns.push({ id: opt.id, btn });

        btn.addEventListener("click", () => {
            timelineTrackFilter = opt.id;
            updateTrackFilterButtons();
            drawTimelineView();
        });
    });

    function updateTrackFilterButtons() {
        trackFilterBtns.forEach(({ id, btn }) => {
            if (id === timelineTrackFilter) {
                btn.style.backgroundColor = "var(--interactive-accent)";
                btn.style.color = "var(--text-on-accent)";
                btn.style.fontWeight = "bold";
            } else {
                btn.style.backgroundColor = "var(--background-modifier-form-field)";
                btn.style.color = "var(--text-normal)";
                btn.style.fontWeight = "normal";
            }
        });
    }
    updateTrackFilterButtons();

    // Custom Hour Sliders
    const sliderContainer = zoomBar.createDiv();
    sliderContainer.style.display = "flex";
    sliderContainer.style.alignItems = "center";
    sliderContainer.style.gap = "6px";
    sliderContainer.style.marginLeft = "auto";

    const sliderStart = sliderContainer.createEl("input");
    sliderStart.type = "range";
    sliderStart.min = "0";
    sliderStart.max = "23";
    sliderStart.value = zoomStartHour;
    sliderStart.style.width = "70px";

    const sliderEnd = sliderContainer.createEl("input");
    sliderEnd.type = "range";
    sliderEnd.min = "1";
    sliderEnd.max = "24";
    sliderEnd.value = zoomEndHour;
    sliderEnd.style.width = "70px";

    const zoomDisplay = sliderContainer.createEl("span", { 
        text: `${String(zoomStartHour).padStart(2, '0')}:00 – ${String(zoomEndHour).padStart(2, '0')}:00` 
    });
    zoomDisplay.style.fontSize = "11px";
    zoomDisplay.style.minWidth = "85px";

    function onSliderChange() {
        let s = parseInt(sliderStart.value);
        let e = parseInt(sliderEnd.value);
        if (s >= e) e = s + 1;
        zoomStartHour = s;
        zoomEndHour = e;
        zoomDisplay.textContent = `${String(s).padStart(2, '0')}:00 – ${String(e).padStart(2, '0')}:00`;
        drawTimelineView();
    }

    sliderStart.addEventListener("input", onSliderChange);
    sliderEnd.addEventListener("input", onSliderChange);

    // Timeline View Area
    const timelineViewArea = card.createDiv();
    timelineViewArea.style.marginTop = "8px";

    // Live Inspection Box for clicked element
    const inspectionBox = card.createDiv();
    inspectionBox.style.padding = "8px 12px";
    inspectionBox.style.borderRadius = "6px";
    inspectionBox.style.marginTop = "12px";
    inspectionBox.style.fontSize = "12px";
    inspectionBox.style.backgroundColor = "var(--background-modifier-form-field)";
    inspectionBox.style.display = "none";

    function drawTimelineView() {
        timelineViewArea.innerHTML = "";
        zoomDisplay.textContent = `${String(zoomStartHour).padStart(2, '0')}:00 – ${String(zoomEndHour).padStart(2, '0')}:00`;

        const daySw = allRecords.filter(r => r.dateStr === selectedDailyDate);
        const dayAw = allActivityRecords.filter(r => r.dateStr === selectedDailyDate);

        const totalWinMinutes = (zoomEndHour - zoomStartHour) * 60;
        const winStartMinutes = zoomStartHour * 60;

        // Container
        const container = timelineViewArea.createDiv();
        container.style.position = "relative";
        container.style.border = "1px solid var(--background-modifier-border)";
        container.style.borderRadius = "6px";
        container.style.backgroundColor = "var(--background-primary)";
        container.style.overflow = "hidden";

        // ROW 1: STOPWATCH PROJECTS TRACK
        const row1Wrapper = container.createDiv();
        row1Wrapper.style.display = "flex";
        row1Wrapper.style.alignItems = "center";
        row1Wrapper.style.borderBottom = "1px solid var(--background-modifier-border)";

        const label1 = row1Wrapper.createDiv({ text: "⏱️ Timers" });
        label1.style.width = "75px";
        label1.style.padding = "8px";
        label1.style.fontSize = "11px";
        label1.style.fontWeight = "bold";
        label1.style.flexShrink = "0";

        const track1 = row1Wrapper.createDiv();
        track1.style.position = "relative";
        track1.style.height = "36px";
        track1.style.flexGrow = "1";
        track1.style.backgroundColor = "var(--background-secondary)";

        daySw.forEach(sw => {
            const startM = toTotalMinutes(sw.start);
            let endM = toTotalMinutes(sw.end);
            if (startM < 0 || endM < 0) return;
            if (endM < startM) endM += 1440;

            const visStart = Math.max(startM, winStartMinutes);
            const visEnd = Math.min(endM, winStartMinutes + totalWinMinutes);

            if (visEnd > visStart) {
                const left = ((visStart - winStartMinutes) / totalWinMinutes) * 100;
                const width = ((visEnd - visStart) / totalWinMinutes) * 100;

                const block = track1.createDiv();
                block.style.position = "absolute";
                block.style.left = `${left}%`;
                block.style.width = `${Math.max(0.5, width)}%`;
                block.style.top = "4px";
                block.style.bottom = "4px";
                block.style.backgroundColor = projectColorMap[sw.project] || '#38bdf8';
                block.style.borderRadius = "4px";
                block.style.color = "#000";
                block.style.fontSize = "10px";
                block.style.fontWeight = "bold";
                block.style.padding = "2px 6px";
                block.style.overflow = "hidden";
                block.style.textOverflow = "ellipsis";
                block.style.whiteSpace = "nowrap";
                block.style.cursor = "pointer";
                block.textContent = sw.project;
                block.title = `⏱️ Project: ${sw.project}\nTime: ${sw.start} - ${sw.end} (${sw.durationText || sw.minutes + 'm'})\n👉 Click to zoom into this session`;

                block.addEventListener("click", () => {
                    const sH = Math.max(0, Math.floor(startM / 60) - 1);
                    const eH = Math.min(24, Math.ceil(endM / 60) + 1);
                    zoomStartHour = sH;
                    zoomEndHour = Math.max(sH + 2, eH);
                    sliderStart.value = zoomStartHour;
                    sliderEnd.value = zoomEndHour;
                    inspectionBox.style.display = "block";
                    inspectionBox.innerHTML = `<strong>🔍 Zoomed into Project:</strong> ${sw.project} (${sw.start} – ${sw.end}, ${sw.durationText || sw.minutes + 'm'})`;
                    drawTimelineView();
                });
            }
        });

        // ROW 2: ACTIVITYWATCH APPS TRACK
        const row2Wrapper = container.createDiv();
        row2Wrapper.style.display = "flex";
        row2Wrapper.style.alignItems = "center";
        row2Wrapper.style.borderBottom = "1px solid var(--background-modifier-border)";

        const label2Text = timelineTrackFilter === "Web" ? "🌐 Web" : (timelineTrackFilter === "all" ? "💻 All" : "💻 Apps");
        const label2 = row2Wrapper.createDiv({ text: label2Text });
        label2.style.width = "75px";
        label2.style.padding = "8px";
        label2.style.fontSize = "11px";
        label2.style.fontWeight = "bold";
        label2.style.flexShrink = "0";

        const track2 = row2Wrapper.createDiv();
        track2.style.position = "relative";
        track2.style.height = "36px";
        track2.style.flexGrow = "1";
        track2.style.backgroundColor = "var(--background-secondary)";

        const eventsToRender = dayAw.filter(aw => {
            if (timelineTrackFilter === "App") return aw.type === "App";
            if (timelineTrackFilter === "Web") return aw.type === "Web";
            return true;
        });

        eventsToRender.forEach(aw => {
            const startM = toTotalMinutes(aw.start);
            let endM = toTotalMinutes(aw.end);
            if (startM < 0 || endM < 0) return;
            if (endM < startM) endM += 1440;

            const visStart = Math.max(startM, winStartMinutes);
            const visEnd = Math.min(endM, winStartMinutes + totalWinMinutes);

            if (visEnd > visStart) {
                const left = ((visStart - winStartMinutes) / totalWinMinutes) * 100;
                const width = ((visEnd - visStart) / totalWinMinutes) * 100;

                const block = track2.createDiv();
                block.style.position = "absolute";
                block.style.left = `${left}%`;
                block.style.width = `${Math.max(0.4, width)}%`;
                block.style.top = "4px";
                block.style.bottom = "4px";

                let activeSwProj = null;
                const awMidMinutes = (startM + endM) / 2;
                for (const sw of daySw) {
                    const sStart = toTotalMinutes(sw.start);
                    let sEnd = toTotalMinutes(sw.end);
                    if (sEnd < sStart) sEnd += 1440;
                    if (awMidMinutes >= sStart && awMidMinutes <= sEnd) {
                        activeSwProj = sw.project;
                        break;
                    }
                }
                const isDist = isDistraction(aw.activity, aw.details, activeSwProj);

                let bg = "#34d399"; // default focus
                if (aw.type === "Idle") bg = "#64748b";
                else if (isDist) bg = "#ef4444";
                else if (aw.type === "Web") bg = "#06b6d4";

                block.style.backgroundColor = bg;
                block.style.borderRadius = "3px";
                block.style.color = "#fff";
                block.style.fontSize = "10px";
                block.style.padding = "2px 4px";
                block.style.overflow = "hidden";
                block.style.textOverflow = "ellipsis";
                block.style.whiteSpace = "nowrap";
                block.style.cursor = "pointer";
                block.textContent = aw.activity;
                block.title = `${aw.activity} (${aw.start} - ${aw.end}, ${aw.durationText || aw.minutes + 'm'})\nDetails: ${aw.details || ''}\nType: ${aw.type}`;

                block.addEventListener("click", () => {
                    inspectionBox.style.display = "block";
                    inspectionBox.innerHTML = `
                        <strong>${aw.activity}</strong> (${aw.start} – ${aw.end}, ${aw.durationText || aw.minutes + 'm'}) &nbsp;|&nbsp; 
                        <span style="color: ${isDist ? '#ef4444' : '#34d399'}">${isDist ? '⚠️ Distraction' : '🟢 Work Tool'}${activeSwProj ? ' (' + activeSwProj + ')' : ''}</span><br/>
                        <span style="opacity: 0.8; font-size: 11px;">Details: ${aw.details || 'No details'}</span>
                    `;
                });
            }
        });

        // TIME TICKS AXIS
        const axisWrapper = container.createDiv();
        axisWrapper.style.display = "flex";
        axisWrapper.style.alignItems = "center";

        const axisLabel = axisWrapper.createDiv({ text: "Time" });
        axisLabel.style.width = "75px";
        axisLabel.style.padding = "4px 8px";
        axisLabel.style.fontSize = "10px";
        axisLabel.style.opacity = "0.6";
        axisLabel.style.flexShrink = "0";

        const axisTrack = axisWrapper.createDiv();
        axisTrack.style.position = "relative";
        axisTrack.style.height = "20px";
        axisTrack.style.flexGrow = "1";

        const hourCount = zoomEndHour - zoomStartHour;
        const tickStep = hourCount <= 6 ? 1 : (hourCount <= 14 ? 2 : 3);

        for (let h = zoomStartHour; h <= zoomEndHour; h += tickStep) {
            const left = ((h - zoomStartHour) / (zoomEndHour - zoomStartHour)) * 100;
            const tick = axisTrack.createDiv({ text: `${String(h % 24).padStart(2, '0')}:00` });
            tick.style.position = "absolute";
            tick.style.left = `${left}%`;
            tick.style.transform = "translateX(-50%)";
            tick.style.fontSize = "9px";
            tick.style.opacity = "0.6";
        }
    }

    drawTimelineView();
}

// ==============================================================================
// RENDER HELPER 2: PROJECT-BY-PROJECT DEEP DIVE (INSPECTOR & DRILL DOWN)
// ==============================================================================
function renderProjectDeepDiveSection() {
    const card = chartSection.createDiv();
    card.style.padding = "18px";
    card.style.borderRadius = "8px";
    card.style.backgroundColor = "var(--background-secondary)";
    card.style.border = "1px solid var(--background-modifier-border)";
    card.style.marginBottom = "30px";

    const header = card.createEl("h3", { text: "🎯 Project-by-Project Deep Dive" });
    header.style.margin = "0 0 4px 0";

    const sub = card.createEl("p", { 
        text: "Analyze all activity across your sessions for any project. Choose a project, time scope (Day, Week, Month), and date." 
    });
    sub.style.fontSize = "11px";
    sub.style.opacity = "0.7";
    sub.style.margin = "0 0 16px 0";

    if (allRecords.length === 0) {
        card.createEl("p", { text: "No stopwatch sessions recorded in your vault." }).style.opacity = "0.6";
        return;
    }

    // Controls Row
    const controlsRow = card.createDiv();
    controlsRow.style.display = "flex";
    controlsRow.style.alignItems = "center";
    controlsRow.style.gap = "14px";
    controlsRow.style.marginBottom = "16px";
    controlsRow.style.flexWrap = "wrap";

    // 1. Project Dropdown
    const projContainer = controlsRow.createDiv();
    projContainer.style.display = "flex";
    projContainer.style.alignItems = "center";
    projContainer.style.gap = "6px";
    const projLabel = projContainer.createEl("span", { text: "Project:" });
    projLabel.style.fontSize = "12px";
    projLabel.style.fontWeight = "bold";

    const projSelect = projContainer.createEl("select");
    projSelect.style.padding = "5px 10px";
    projSelect.style.borderRadius = "6px";
    projSelect.style.fontSize = "12px";
    projSelect.style.backgroundColor = "var(--background-modifier-form-field)";
    projSelect.style.color = "var(--text-normal)";
    projSelect.style.border = "1px solid var(--background-modifier-border)";

    allProjectNames.forEach(p => {
        const opt = projSelect.createEl("option", { text: p, value: p });
        if (p === deepDiveProject) opt.selected = true;
    });
    const allOpt = projSelect.createEl("option", { text: "🌐 (All Projects)", value: "__all__" });
    if (deepDiveProject === "__all__") allOpt.selected = true;

    // 2. Scope Dropdown
    const scopeContainer = controlsRow.createDiv();
    scopeContainer.style.display = "flex";
    scopeContainer.style.alignItems = "center";
    scopeContainer.style.gap = "6px";
    const scopeLabelEl = scopeContainer.createEl("span", { text: "Scope:" });
    scopeLabelEl.style.fontSize = "12px";
    scopeLabelEl.style.fontWeight = "bold";

    const scopeSelect = scopeContainer.createEl("select");
    scopeSelect.style.padding = "5px 10px";
    scopeSelect.style.borderRadius = "6px";
    scopeSelect.style.fontSize = "12px";
    scopeSelect.style.backgroundColor = "var(--background-modifier-form-field)";
    scopeSelect.style.color = "var(--text-normal)";
    scopeSelect.style.border = "1px solid var(--background-modifier-border)";

    const scopeList = [
        { id: "day", label: "📅 Whole Day" },
        { id: "week", label: "🗓️ Whole Week" },
        { id: "month", label: "📆 Whole Month" },
        { id: "all", label: "🌐 All Time" }
    ];
    scopeList.forEach(s => {
        const opt = scopeSelect.createEl("option", { text: s.label, value: s.id });
        if (s.id === deepDiveScope) opt.selected = true;
    });

    // 3. Date Dropdown
    const dateContainer = controlsRow.createDiv();
    dateContainer.style.display = "flex";
    dateContainer.style.alignItems = "center";
    dateContainer.style.gap = "6px";
    const dateLabelEl = dateContainer.createEl("span", { text: "Date:" });
    dateLabelEl.style.fontSize = "12px";
    dateLabelEl.style.fontWeight = "bold";

    const dateSelect = dateContainer.createEl("select");
    dateSelect.style.padding = "5px 10px";
    dateSelect.style.borderRadius = "6px";
    dateSelect.style.fontSize = "12px";
    dateSelect.style.backgroundColor = "var(--background-modifier-form-field)";
    dateSelect.style.color = "var(--text-normal)";
    dateSelect.style.border = "1px solid var(--background-modifier-border)";

    availableDates.forEach(d => {
        const opt = dateSelect.createEl("option", { text: d, value: d });
        if (d === deepDiveDate) opt.selected = true;
    });

    // 4. App / Web Display Filter
    const filterContainer = controlsRow.createDiv();
    filterContainer.style.display = "flex";
    filterContainer.style.alignItems = "center";
    filterContainer.style.gap = "6px";
    filterContainer.style.marginLeft = "auto";

    const filterLabel = filterContainer.createEl("span", { text: "Show:" });
    filterLabel.style.fontSize = "12px";
    filterLabel.style.fontWeight = "bold";

    const appFilterOptions = [
        { id: "App", label: "🖥️ Apps (Focused)" },
        { id: "Web", label: "🌐 Web Sites" },
        { id: "all", label: "All Activity" }
    ];

    const filterBtns = [];
    appFilterOptions.forEach(opt => {
        const btn = filterContainer.createEl("button", { text: opt.label });
        btn.style.padding = "3px 8px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        filterBtns.push({ id: opt.id, btn });

        btn.addEventListener("click", () => {
            deepDiveAppFilter = opt.id;
            updateFilterBtns();
            updateDeepDive();
        });
    });

    function updateFilterBtns() {
        filterBtns.forEach(({ id, btn }) => {
            if (id === deepDiveAppFilter) {
                btn.style.backgroundColor = "var(--interactive-accent)";
                btn.style.color = "var(--text-on-accent)";
                btn.style.fontWeight = "bold";
            } else {
                btn.style.backgroundColor = "var(--background-modifier-form-field)";
                btn.style.color = "var(--text-normal)";
                btn.style.fontWeight = "normal";
            }
        });
    }
    updateFilterBtns();

    // Event listeners
    projSelect.addEventListener("change", (e) => {
        deepDiveProject = e.target.value;
        deepDiveSelectedSessionIdx = -1;
        deepDiveTimelineOpen = false;
        updateDeepDive();
    });

    scopeSelect.addEventListener("change", (e) => {
        deepDiveScope = e.target.value;
        deepDiveSelectedSessionIdx = -1;
        deepDiveTimelineOpen = false;
        if (deepDiveScope === "all") {
            dateSelect.disabled = true;
            dateContainer.style.opacity = "0.5";
        } else {
            dateSelect.disabled = false;
            dateContainer.style.opacity = "1";
        }
        updateDeepDive();
    });

    dateSelect.addEventListener("change", (e) => {
        deepDiveDate = e.target.value;
        deepDiveSelectedSessionIdx = -1;
        deepDiveTimelineOpen = false;
        updateDeepDive();
    });

    const resultsContainer = card.createDiv();

    function updateDeepDive() {
        resultsContainer.innerHTML = "";

        const { start: scopeStart, end: scopeEnd, label: scopeLabel } = getScopeRange(deepDiveScope, deepDiveDate);

        // Filter stopwatch records by date range and project
        const matchingSw = allRecords.filter(r => {
            const inScope = r.dateObj >= scopeStart && r.dateObj <= scopeEnd;
            if (!inScope) return false;
            if (deepDiveProject !== "__all__" && r.project !== deepDiveProject) return false;
            return true;
        }).sort((a, b) => a.dateObj - b.dateObj || toDecimalHour(a.start) - toDecimalHour(b.start));

        const projectNameDisplay = deepDiveProject === "__all__" ? "All Projects" : deepDiveProject;

        if (matchingSw.length === 0) {
            const emptyMsg = resultsContainer.createDiv();
            emptyMsg.style.padding = "24px 16px";
            emptyMsg.style.textAlign = "center";
            emptyMsg.style.opacity = "0.7";
            emptyMsg.style.backgroundColor = "var(--background-modifier-form-field)";
            emptyMsg.style.borderRadius = "6px";
            emptyMsg.innerHTML = `No stopwatch sessions recorded for <strong>${projectNameDisplay}</strong> in <strong>${scopeLabel}</strong>.`;
            return;
        }

        // Determine active sessions to analyze (either all combined or a specific clicked session)
        const isSingleSession = deepDiveSelectedSessionIdx >= 0 && deepDiveSelectedSessionIdx < matchingSw.length;
        const activeSessions = isSingleSession ? [matchingSw[deepDiveSelectedSessionIdx]] : matchingSw;

        const totalPeriodSwMinutes = matchingSw.reduce((sum, r) => sum + r.minutes, 0);
        const activeSwMinutes = activeSessions.reduce((sum, r) => sum + r.minutes, 0);

        // Overlap calculation with ActivityWatch records
        const sessionApps = {};
        const detailedIntervals = [];
        let focusMin = 0;
        let distractMin = 0;

        for (const sw of activeSessions) {
            const swStart = toDecimalHour(sw.start);
            let swEnd = toDecimalHour(sw.end);
            if (swStart < 0 || swEnd < 0) continue;
            if (swEnd < swStart) swEnd += 24;

            for (const aw of allActivityRecords) {
                if (aw.dateStr !== sw.dateStr || aw.type === "Idle") continue;
                if (deepDiveAppFilter === "App" && aw.type !== "App") continue;
                if (deepDiveAppFilter === "Web" && aw.type !== "Web") continue;

                const awStart = toDecimalHour(aw.start);
                let awEnd = toDecimalHour(aw.end);
                if (awStart < 0 || awEnd < 0) continue;
                if (awEnd < awStart) awEnd += 24;

                const overlap = Math.max(0, Math.min(swEnd, awEnd) - Math.max(swStart, awStart));
                const overlapMin = Math.round(overlap * 60);

                if (overlapMin > 0) {
                    const isDist = isDistraction(aw.activity, aw.details, sw.project);
                    sessionApps[aw.activity] = (sessionApps[aw.activity] || 0) + overlapMin;
                    if (isDist) distractMin += overlapMin;
                    else focusMin += overlapMin;

                    detailedIntervals.push({
                        dateStr: sw.dateStr,
                        project: sw.project,
                        sessionLabel: `${sw.project} (${sw.start}–${sw.end})`,
                        activity: aw.activity,
                        details: aw.details,
                        start: aw.start,
                        end: aw.end,
                        overlapMin,
                        type: aw.type,
                        isDistraction: isDist
                    });
                }
            }
        }

        const totalActive = focusMin + distractMin;
        const focusRate = totalActive > 0 ? Math.round((focusMin / totalActive) * 100) : 100;

        // Render KPI Cards
        const kpi = resultsContainer.createDiv();
        kpi.style.display = "grid";
        kpi.style.gridTemplateColumns = "repeat(auto-fit, minmax(130px, 1fr))";
        kpi.style.gap = "10px";
        kpi.style.padding = "12px";
        kpi.style.borderRadius = "6px";
        kpi.style.backgroundColor = "var(--background-modifier-form-field)";
        kpi.style.marginBottom = "14px";
        kpi.style.textAlign = "center";

        kpi.innerHTML = `
            <div>
                <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Project & Scope</div>
                <div style="font-size: 16px; font-weight: bold; color: ${projectColorMap[deepDiveProject] || 'var(--text-accent)'};">${projectNameDisplay}</div>
                <div style="font-size: 11px; opacity: 0.7;">${scopeLabel}</div>
            </div>
            <div>
                <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Stopwatch Time</div>
                <div style="font-size: 16px; font-weight: bold;">${formatMinutes(activeSwMinutes)}</div>
                <div style="font-size: 11px; opacity: 0.7;">${activeSessions.length} session${activeSessions.length > 1 ? 's' : ''}</div>
            </div>
            <div>
                <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Focused Work</div>
                <div style="font-size: 16px; font-weight: bold; color: #10b981;">${formatMinutes(focusMin)}</div>
                <div style="font-size: 11px; opacity: 0.7;">Work tools</div>
            </div>
            <div>
                <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Distractions</div>
                <div style="font-size: 16px; font-weight: bold; color: #ef4444;">${formatMinutes(distractMin)}</div>
                <div style="font-size: 11px; opacity: 0.7;">Telegram / social</div>
            </div>
            <div>
                <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Focus Rate</div>
                <div style="font-size: 16px; font-weight: bold; color: ${focusRate >= 80 ? '#10b981' : (focusRate >= 60 ? '#fbbf24' : '#ef4444')};">${focusRate}%</div>
                <div style="font-size: 11px; opacity: 0.7;">${focusRate >= 80 ? 'Excellent' : 'Watch out'}</div>
            </div>
        `;

        // Interactive Sessions Pill Bar (shown when there are multiple sessions)
        if (matchingSw.length > 1) {
            const sessionBar = resultsContainer.createDiv();
            sessionBar.style.display = "flex";
            sessionBar.style.alignItems = "center";
            sessionBar.style.gap = "6px";
            sessionBar.style.flexWrap = "wrap";
            sessionBar.style.marginBottom = "16px";
            sessionBar.style.padding = "8px 12px";
            sessionBar.style.borderRadius = "6px";
            sessionBar.style.backgroundColor = "var(--background-secondary)";
            sessionBar.style.border = "1px solid var(--background-modifier-border)";

            const sLabel = sessionBar.createEl("span", { text: `Sessions (${matchingSw.length}):` });
            sLabel.style.fontSize = "11px";
            sLabel.style.fontWeight = "bold";

            // All Combined Button
            const allBtn = sessionBar.createEl("button", { text: `All ${matchingSw.length} Sessions Combined (${formatMinutes(totalPeriodSwMinutes)})` });
            allBtn.style.padding = "3px 8px";
            allBtn.style.fontSize = "11px";
            allBtn.style.borderRadius = "4px";
            allBtn.style.cursor = "pointer";
            allBtn.style.border = "1px solid var(--background-modifier-border)";
            if (deepDiveSelectedSessionIdx === -1) {
                allBtn.style.backgroundColor = "var(--interactive-accent)";
                allBtn.style.color = "var(--text-on-accent)";
                allBtn.style.fontWeight = "bold";
            } else {
                allBtn.style.backgroundColor = "var(--background-modifier-form-field)";
                allBtn.style.color = "var(--text-normal)";
            }
            allBtn.addEventListener("click", () => {
                deepDiveSelectedSessionIdx = -1;
                updateDeepDive();
            });

            // Individual session buttons
            matchingSw.forEach((sw, idx) => {
                const datePrefix = deepDiveScope !== "day" ? `${sw.dateStr} ` : "";
                const sBtn = sessionBar.createEl("button", { 
                    text: `${datePrefix}${sw.start}–${sw.end} (${sw.durationText || sw.minutes + 'm'})` 
                });
                sBtn.style.padding = "3px 8px";
                sBtn.style.fontSize = "11px";
                sBtn.style.borderRadius = "4px";
                sBtn.style.cursor = "pointer";
                sBtn.style.border = "1px solid var(--background-modifier-border)";
                if (deepDiveSelectedSessionIdx === idx) {
                    sBtn.style.backgroundColor = "var(--interactive-accent)";
                    sBtn.style.color = "var(--text-on-accent)";
                    sBtn.style.fontWeight = "bold";
                } else {
                    sBtn.style.backgroundColor = "var(--background-modifier-form-field)";
                    sBtn.style.color = "var(--text-normal)";
                }
                sBtn.addEventListener("click", () => {
                    deepDiveSelectedSessionIdx = idx;
                    updateDeepDive();
                });
            });
        }

        // Bar Chart of Apps
        const sortedApps = Object.entries(sessionApps).sort((a, b) => b[1] - a[1]);
        const targetProjForRule = deepDiveProject !== "__all__" ? deepDiveProject : (activeSessions.length > 0 ? activeSessions[0].project : null);

        if (sortedApps.length > 0) {
            const chartDiv = resultsContainer.createDiv();
            const chartTitle = isSingleSession 
                ? `Apps Used During Session: ${activeSessions[0].start} – ${activeSessions[0].end}` 
                : `Apps Used Across ${activeSessions.length} Session${activeSessions.length > 1 ? 's' : ''} of ${projectNameDisplay}`;
            chartDiv.createEl("h4", { text: chartTitle }).style.margin = "0 0 4px 0";

            if (targetProjForRule) {
                const hintEl = chartDiv.createEl("div");
                hintEl.style.fontSize = "11px";
                hintEl.style.opacity = "0.75";
                hintEl.style.marginBottom = "8px";
                hintEl.innerHTML = `💡 <i>Click any bar or table badge to toggle between 🟢 Work Tool and ⚠️ Distraction for <b>${targetProjForRule}</b>.</i>`;
            }

            window.renderChart({
                type: 'bar',
                data: {
                    labels: sortedApps.map(a => a[0]),
                    datasets: [{
                        label: 'Minutes Active',
                        data: sortedApps.map(a => a[1]),
                        backgroundColor: sortedApps.map(a => isDistraction(a[0], "", targetProjForRule) ? '#ef4444' : '#10b981'),
                        borderRadius: 4
                    }]
                },
                options: {
                    indexAxis: 'y',
                    scales: {
                        x: { beginAtZero: true, title: { display: true, text: 'Minutes' } }
                    },
                    onClick: (evt, elements) => {
                        if (elements && elements.length > 0) {
                            const index = elements[0].index;
                            const appName = sortedApps[index][0];
                            if (targetProjForRule) {
                                toggleAppCategory(appName, targetProjForRule);
                            }
                        }
                    },
                    onHover: (evt, elements, chart) => {
                        const canvas = (evt && evt.native && evt.native.target) || (chart && chart.canvas) || (evt && evt.chart && evt.chart.canvas);
                        if (canvas && canvas.style) {
                            canvas.style.cursor = (elements && elements.length > 0) ? 'pointer' : 'default';
                        }
                    }
                }
            }, chartDiv);

            // Detailed In-Session Activity Timeline (Collapsible, collapsed by default when choosing a project)
            const details = resultsContainer.createEl("details");
            details.style.marginTop = "20px";
            details.style.borderRadius = "6px";
            details.style.backgroundColor = "var(--background-primary)";
            details.style.border = "1px solid var(--background-modifier-border)";
            details.style.padding = "10px 14px";
            if (deepDiveTimelineOpen) {
                details.open = true;
            }

            const summary = details.createEl("summary");
            summary.style.fontWeight = "bold";
            summary.style.fontSize = "13px";
            summary.style.cursor = "pointer";
            summary.style.userSelect = "none";
            summary.style.display = "flex";
            summary.style.alignItems = "center";
            summary.style.justifyContent = "space-between";
            summary.title = "Click to expand/collapse activity timeline";

            const summaryTitle = summary.createDiv();
            summaryTitle.style.display = "flex";
            summaryTitle.style.alignItems = "center";
            summaryTitle.style.gap = "8px";
            summaryTitle.innerHTML = `
                <span>Detailed In-Session Activity Timeline</span>
                <span class="toggle-hint" style="font-size: 11px; opacity: 0.6; font-weight: normal;">${deepDiveTimelineOpen ? "(click to collapse)" : "(click to expand)"}</span>
            `;

            const summaryBadge = summary.createEl("span", {
                text: `${detailedIntervals.length} record${detailedIntervals.length === 1 ? '' : 's'}`
            });
            summaryBadge.style.fontSize = "11px";
            summaryBadge.style.padding = "2px 8px";
            summaryBadge.style.borderRadius = "10px";
            summaryBadge.style.backgroundColor = "var(--background-modifier-form-field)";
            summaryBadge.style.border = "1px solid var(--background-modifier-border)";
            summaryBadge.style.opacity = "0.75";
            summaryBadge.style.fontWeight = "normal";

            details.addEventListener("toggle", () => {
                deepDiveTimelineOpen = details.open;
                const hint = summaryTitle.querySelector(".toggle-hint");
                if (hint) {
                    hint.textContent = details.open ? "(click to collapse)" : "(click to expand)";
                }
            });

            const tableContainer = details.createDiv();
            tableContainer.style.overflowX = "auto";
            tableContainer.style.marginTop = "10px";

            const logTable = tableContainer.createEl("table");
            logTable.style.width = "100%";
            logTable.style.fontSize = "11px";
            logTable.style.borderCollapse = "collapse";

            const showDateCol = deepDiveScope !== "day";

            logTable.innerHTML = `
                <thead>
                    <tr style="border-bottom: 1px solid var(--background-modifier-border); text-align: left; opacity: 0.7;">
                        ${showDateCol ? '<th style="padding: 4px 6px;">Date</th>' : ''}
                        <th style="padding: 4px 6px;">Session</th>
                        <th style="padding: 4px 6px;">Active Time</th>
                        <th style="padding: 4px 6px;">App / Site</th>
                        <th style="padding: 4px 6px;">Window / Tab Detail</th>
                        <th style="padding: 4px 6px;">Duration</th>
                        <th style="padding: 4px 6px;">Category (Click to Toggle)</th>
                    </tr>
                </thead>
                <tbody>
                    ${detailedIntervals.map(i => {
                        const itemProj = deepDiveProject !== "__all__" ? deepDiveProject : i.project;
                        return `
                        <tr style="border-bottom: 1px solid var(--background-modifier-border);">
                            ${showDateCol ? `<td style="padding: 4px 6px;">${i.dateStr}</td>` : ''}
                            <td style="padding: 4px 6px; opacity: 0.8;">${i.sessionLabel}</td>
                            <td style="padding: 4px 6px;">${i.start} – ${i.end}</td>
                            <td style="padding: 4px 6px; font-weight: bold;">${i.activity}</td>
                            <td style="padding: 4px 6px; opacity: 0.8; max-width: 250px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">${i.details || '-'}</td>
                            <td style="padding: 4px 6px;">${i.overlapMin}m</td>
                            <td style="padding: 4px 6px;">
                                <button class="rule-toggle-btn" data-app="${encodeURIComponent(i.activity)}" data-proj="${encodeURIComponent(itemProj)}" style="
                                    background: ${i.isDistraction ? 'rgba(239, 68, 68, 0.15)' : 'rgba(16, 185, 129, 0.15)'};
                                    color: ${i.isDistraction ? '#ef4444' : '#10b981'};
                                    border: 1px solid ${i.isDistraction ? '#ef4444' : '#10b981'};
                                    border-radius: 4px;
                                    padding: 2px 7px;
                                    font-size: 11px;
                                    font-weight: bold;
                                    cursor: pointer;
                                    display: inline-flex;
                                    align-items: center;
                                    gap: 4px;
                                " title="Click to toggle between Work Tool and Distraction for ${itemProj}">
                                    ${i.isDistraction ? '⚠️ Distraction' : '🟢 Work Tool'} <span style="opacity: 0.6; font-size: 9px;">⇄</span>
                                </button>
                            </td>
                        </tr>
                        `;
                    }).join("")}
                </tbody>
            `;

            logTable.querySelectorAll(".rule-toggle-btn").forEach(btn => {
                btn.addEventListener("click", (e) => {
                    e.stopPropagation();
                    const app = decodeURIComponent(btn.getAttribute("data-app"));
                    const proj = decodeURIComponent(btn.getAttribute("data-proj"));
                    if (app && proj) {
                        toggleAppCategory(app, proj);
                    }
                });
            });
        } else {
            const noAppsDiv = resultsContainer.createDiv();
            noAppsDiv.style.padding = "16px";
            noAppsDiv.style.opacity = "0.6";
            noAppsDiv.style.textAlign = "center";
            noAppsDiv.textContent = `No background app activity recorded during ${isSingleSession ? "this session" : "these sessions"}.`;
        }

        // ⚙️ Custom Rules Management Box (Project Specific)
        const rulesBox = resultsContainer.createDiv();
        rulesBox.style.marginTop = "24px";
        rulesBox.style.padding = "14px 18px";
        rulesBox.style.borderRadius = "8px";
        rulesBox.style.backgroundColor = "var(--background-secondary)";
        rulesBox.style.border = "1px solid var(--background-modifier-border)";

        if (!targetProjForRule || targetProjForRule === "__all__") {
            rulesBox.innerHTML = `
                <div style="font-weight: bold; font-size: 13px; margin-bottom: 4px;">⚙️ Project Tool Rules</div>
                <div style="font-size: 12px; opacity: 0.7;">Select a specific project from the dropdown above to manage custom work tools and distractions.</div>
            `;
        } else {
            const currentRules = projectRules[targetProjForRule] || { distractions: [], workTools: [] };
            const distList = currentRules.distractions || [];
            const workList = currentRules.workTools || [];

            const rulesHeading = rulesBox.createDiv();
            rulesHeading.style.display = "flex";
            rulesHeading.style.justifyContent = "space-between";
            rulesHeading.style.alignItems = "center";
            rulesHeading.style.marginBottom = "12px";

            rulesHeading.innerHTML = `
                <div>
                    <span style="font-weight: bold; font-size: 13px;">⚙️ Custom Tool Rules for <span style="color: ${projectColorMap[targetProjForRule] || 'var(--text-accent)'};">${targetProjForRule}</span></span>
                    <div style="font-size: 11px; opacity: 0.7; margin-top: 2px;">Customize which apps count as Distractions or Work Tools specifically during <b>${targetProjForRule}</b> sessions. Saved to <code>Project Rules.json</code>.</div>
                </div>
            `;

            if (distList.length > 0 || workList.length > 0) {
                const resetBtn = rulesHeading.createEl("button", { text: "Reset to Defaults" });
                resetBtn.style.padding = "3px 10px";
                resetBtn.style.fontSize = "11px";
                resetBtn.style.borderRadius = "4px";
                resetBtn.style.cursor = "pointer";
                resetBtn.style.border = "1px solid var(--background-modifier-border)";
                resetBtn.style.backgroundColor = "var(--background-modifier-form-field)";
                resetBtn.addEventListener("click", async () => {
                    delete projectRules[targetProjForRule];
                    await saveProjectRules();
                    renderDashboard();
                });
            }

            // Distractions list
            const distRow = rulesBox.createDiv();
            distRow.style.marginBottom = "10px";
            distRow.style.display = "flex";
            distRow.style.alignItems = "center";
            distRow.style.gap = "8px";
            distRow.style.flexWrap = "wrap";

            const distLabel = distRow.createEl("span", { text: "⚠️ Distractions:" });
            distLabel.style.fontSize = "12px";
            distLabel.style.fontWeight = "bold";
            distLabel.style.color = "#ef4444";
            distLabel.style.minWidth = "120px";

            if (distList.length === 0) {
                const noneEl = distRow.createEl("span", { text: "None (using global default list: Telegram, Youtube, etc.)" });
                noneEl.style.fontSize = "11px";
                noneEl.style.opacity = "0.6";
                noneEl.style.fontStyle = "italic";
            } else {
                distList.forEach(app => {
                    const tag = distRow.createDiv();
                    tag.style.display = "inline-flex";
                    tag.style.alignItems = "center";
                    tag.style.gap = "6px";
                    tag.style.backgroundColor = "rgba(239, 68, 68, 0.15)";
                    tag.style.color = "#ef4444";
                    tag.style.border = "1px solid #ef4444";
                    tag.style.borderRadius = "4px";
                    tag.style.padding = "2px 8px";
                    tag.style.fontSize = "11px";
                    tag.style.fontWeight = "bold";

                    tag.createEl("span", { text: app });
                    const del = tag.createEl("span", { text: "✕" });
                    del.style.cursor = "pointer";
                    del.style.opacity = "0.7";
                    del.style.fontWeight = "bold";
                    del.title = `Remove override for ${app}`;
                    del.addEventListener("click", () => removeAppRule(app, targetProjForRule));
                });
            }

            // Work tools list
            const workRow = rulesBox.createDiv();
            workRow.style.marginBottom = "14px";
            workRow.style.display = "flex";
            workRow.style.alignItems = "center";
            workRow.style.gap = "8px";
            workRow.style.flexWrap = "wrap";

            const workLabel = workRow.createEl("span", { text: "🟢 Work Tools:" });
            workLabel.style.fontSize = "12px";
            workLabel.style.fontWeight = "bold";
            workLabel.style.color = "#10b981";
            workLabel.style.minWidth = "120px";

            if (workList.length === 0) {
                const noneEl = workRow.createEl("span", { text: "None (all non-distraction apps are work tools)" });
                noneEl.style.fontSize = "11px";
                noneEl.style.opacity = "0.6";
                noneEl.style.fontStyle = "italic";
            } else {
                workList.forEach(app => {
                    const tag = workRow.createDiv();
                    tag.style.display = "inline-flex";
                    tag.style.alignItems = "center";
                    tag.style.gap = "6px";
                    tag.style.backgroundColor = "rgba(16, 185, 129, 0.15)";
                    tag.style.color = "#10b981";
                    tag.style.border = "1px solid #10b981";
                    tag.style.borderRadius = "4px";
                    tag.style.padding = "2px 8px";
                    tag.style.fontSize = "11px";
                    tag.style.fontWeight = "bold";

                    tag.createEl("span", { text: app });
                    const del = tag.createEl("span", { text: "✕" });
                    del.style.cursor = "pointer";
                    del.style.opacity = "0.7";
                    del.style.fontWeight = "bold";
                    del.title = `Remove override for ${app}`;
                    del.addEventListener("click", () => removeAppRule(app, targetProjForRule));
                });
            }

            // Quick Add Input
            const addRow = rulesBox.createDiv();
            addRow.style.display = "flex";
            addRow.style.alignItems = "center";
            addRow.style.gap = "8px";
            addRow.style.flexWrap = "wrap";
            addRow.style.paddingTop = "8px";
            addRow.style.borderTop = "1px solid var(--background-modifier-border)";

            const addInput = addRow.createEl("input", { type: "text" });
            addInput.placeholder = "e.g. Antigravity, Photoshop, Chrome...";
            addInput.style.padding = "4px 8px";
            addInput.style.fontSize = "12px";
            addInput.style.borderRadius = "4px";
            addInput.style.border = "1px solid var(--background-modifier-border)";
            addInput.style.backgroundColor = "var(--background-modifier-form-field)";
            addInput.style.width = "200px";

            const addDistBtn = addRow.createEl("button", { text: "+ Add as Distraction" });
            addDistBtn.style.padding = "4px 10px";
            addDistBtn.style.fontSize = "11px";
            addDistBtn.style.fontWeight = "bold";
            addDistBtn.style.borderRadius = "4px";
            addDistBtn.style.cursor = "pointer";
            addDistBtn.style.border = "1px solid #ef4444";
            addDistBtn.style.backgroundColor = "rgba(239, 68, 68, 0.15)";
            addDistBtn.style.color = "#ef4444";

            addDistBtn.addEventListener("click", async () => {
                const val = addInput.value.trim();
                if (!val) return;
                if (!projectRules[targetProjForRule]) projectRules[targetProjForRule] = { distractions: [], workTools: [] };
                const r = projectRules[targetProjForRule];
                if (!r.distractions) r.distractions = [];
                if (!r.workTools) r.workTools = [];
                r.workTools = r.workTools.filter(w => w.toLowerCase() !== val.toLowerCase());
                if (!r.distractions.some(d => d.toLowerCase() === val.toLowerCase())) {
                    r.distractions.push(val);
                }
                await saveProjectRules();
                renderDashboard();
            });

            const addWorkBtn = addRow.createEl("button", { text: "+ Add as Work Tool" });
            addWorkBtn.style.padding = "4px 10px";
            addWorkBtn.style.fontSize = "11px";
            addWorkBtn.style.fontWeight = "bold";
            addWorkBtn.style.borderRadius = "4px";
            addWorkBtn.style.cursor = "pointer";
            addWorkBtn.style.border = "1px solid #10b981";
            addWorkBtn.style.backgroundColor = "rgba(16, 185, 129, 0.15)";
            addWorkBtn.style.color = "#10b981";

            addWorkBtn.addEventListener("click", async () => {
                const val = addInput.value.trim();
                if (!val) return;
                if (!projectRules[targetProjForRule]) projectRules[targetProjForRule] = { distractions: [], workTools: [] };
                const r = projectRules[targetProjForRule];
                if (!r.distractions) r.distractions = [];
                if (!r.workTools) r.workTools = [];
                r.distractions = r.distractions.filter(d => d.toLowerCase() !== val.toLowerCase());
                if (!r.workTools.some(w => w.toLowerCase() === val.toLowerCase())) {
                    r.workTools.push(val);
                }
                await saveProjectRules();
                renderDashboard();
            });
        }
    }

    updateDeepDive();
}

// ==============================================================================
// RENDER HELPER 3: INTERNET & NETWORK CONNECTION REPORT
// ==============================================================================
function renderInternetSection() {
    if (allInternetRecords.length === 0) return;

    const { internet: allNetRecordsInPeriod } = getFilteredRecords(activeFilter);
    if (allNetRecordsInPeriod.length === 0) return;

    const card = chartSection.createDiv();
    card.style.padding = "18px";
    card.style.borderRadius = "8px";
    card.style.backgroundColor = "var(--background-secondary)";
    card.style.border = "1px solid var(--background-modifier-border)";
    card.style.marginBottom = "30px";

    const header = card.createEl("h3", { text: "🌐 Internet Connection & Network Performance" });
    header.style.margin = "0 0 4px 0";

    const sub = card.createEl("p", { 
        text: "Background latency (ping), download speed tests, and connected Wi-Fi / network adapters recorded during this period." 
    });
    sub.style.fontSize = "11px";
    sub.style.opacity = "0.7";
    sub.style.margin = "0 0 16px 0";

    // Period summary metrics
    const validSpeeds = allNetRecordsInPeriod.filter(r => r.speedMbps != null && r.speedMbps > 0).map(r => r.speedMbps);
    const validPings = allNetRecordsInPeriod.filter(r => r.pingMs != null && r.pingMs > 0).map(r => r.pingMs);

    const avgSpeed = validSpeeds.length > 0
        ? (validSpeeds.reduce((a, b) => a + b, 0) / validSpeeds.length).toFixed(1)
        : "-";
    const maxSpeed = validSpeeds.length > 0 ? Math.max(...validSpeeds).toFixed(1) : "-";

    const avgPing = validPings.length > 0
        ? Math.round(validPings.reduce((a, b) => a + b, 0) / validPings.length)
        : "-";

    const totalNetChecks = allNetRecordsInPeriod.length;
    const connectedCount = allNetRecordsInPeriod.filter(r => r.isOnline).length;
    const optimalCount = allNetRecordsInPeriod.filter(r => r.isOptimal).length;
    const slowCount = allNetRecordsInPeriod.filter(r => r.isSlow).length;
    const offlineCount = allNetRecordsInPeriod.filter(r => r.isOffline).length;
    const uptimePercent = totalNetChecks > 0 ? Math.round((connectedCount / totalNetChecks) * 100) : 0;

    const periodNetworks = Array.from(new Set(allNetRecordsInPeriod.map(r => r.cleanNetwork))).filter(Boolean);
    const activeNetwork = allNetRecordsInPeriod[allNetRecordsInPeriod.length - 1]?.cleanNetwork || periodNetworks[0] || "Unknown";

    // Period KPI Cards
    const kpi = card.createDiv();
    kpi.style.display = "grid";
    kpi.style.gridTemplateColumns = "repeat(auto-fit, minmax(130px, 1fr))";
    kpi.style.gap = "10px";
    kpi.style.padding = "12px";
    kpi.style.borderRadius = "6px";
    kpi.style.backgroundColor = "var(--background-modifier-form-field)";
    kpi.style.marginBottom = "18px";
    kpi.style.textAlign = "center";

    kpi.innerHTML = `
        <div>
            <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Active Network</div>
            <div style="font-size: 14px; font-weight: bold; color: var(--text-accent); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;" title="${activeNetwork}">${activeNetwork}</div>
            <div style="font-size: 11px; opacity: 0.7;">${periodNetworks.length > 1 ? periodNetworks.length + ' networks used' : 'Connected'}</div>
        </div>
        <div>
            <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Avg Download Speed</div>
            <div style="font-size: 16px; font-weight: bold; color: #38bdf8;">${avgSpeed} ${avgSpeed !== '-' ? 'Mbps' : ''}</div>
            <div style="font-size: 11px; opacity: 0.7;">Peak: ${maxSpeed} Mbps</div>
        </div>
        <div>
            <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Avg Latency (Ping)</div>
            <div style="font-size: 16px; font-weight: bold; color: ${avgPing !== '-' && avgPing < 80 ? '#10b981' : (avgPing < 200 ? '#fbbf24' : '#ef4444')};">${avgPing} ${avgPing !== '-' ? 'ms' : ''}</div>
            <div style="font-size: 11px; opacity: 0.7;">${avgPing !== '-' && avgPing < 50 ? 'Low latency' : 'Cloudflare 1.1.1.1'}</div>
        </div>
        <div>
            <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Connection Uptime</div>
            <div style="font-size: 16px; font-weight: bold; color: ${uptimePercent >= 90 ? '#10b981' : (uptimePercent >= 75 ? '#fbbf24' : '#ef4444')};">${uptimePercent}%</div>
            <div style="font-size: 11px; opacity: 0.7;" title="${optimalCount} optimal, ${slowCount} slow, ${offlineCount} offline">${connectedCount}/${totalNetChecks} checks online (${slowCount} slow)</div>
        </div>
    `;

    // ==============================================================================
    // 1. ORIGINAL PREVIOUS CHART: SPEED & PING TIMELINE (WITH RAW / 30m / 1h INTERVALS)
    // ==============================================================================
    if (allNetRecordsInPeriod.length >= 2) {
        const tlSection = card.createDiv();
        tlSection.style.marginBottom = "24px";

        const tlHeaderRow = tlSection.createDiv();
        tlHeaderRow.style.display = "flex";
        tlHeaderRow.style.justifyContent = "space-between";
        tlHeaderRow.style.alignItems = "center";
        tlHeaderRow.style.marginBottom = "8px";
        tlHeaderRow.style.flexWrap = "wrap";
        tlHeaderRow.style.gap = "10px";

        const tlTitleContainer = tlHeaderRow.createDiv();
        const tlTitle = tlTitleContainer.createEl("h4", { text: "Connection Speed (Mbps) & Latency (ms) Timeline" });
        tlTitle.style.margin = "0 0 2px 0";

        const tlSub = tlTitleContainer.createEl("p");
        tlSub.style.fontSize = "11px";
        tlSub.style.opacity = "0.7";
        tlSub.style.margin = "0";

        const tlAggContainer = tlHeaderRow.createDiv();
        tlAggContainer.style.display = "flex";
        tlAggContainer.style.alignItems = "center";
        tlAggContainer.style.gap = "4px";

        const tlAggLabel = tlAggContainer.createEl("span", { text: "Interval:" });
        tlAggLabel.style.fontSize = "12px";
        tlAggLabel.style.fontWeight = "bold";

        const tlAggOptions = [
            { id: "15", label: "⏱️ 15m Avg" },
            { id: "30", label: "⏱️ 30m Avg" },
            { id: "60", label: "🕒 1h Avg" },
            { id: "raw", label: "⚡ Raw Checks" }
        ];

        const tlAggBtns = [];
        const tlChartContainer = tlSection.createDiv();

        function renderTimelineChart() {
            tlChartContainer.innerHTML = "";

            let labels = [];
            let speedData = [];
            let pingData = [];

            if (netTimelineAggregation === "raw") {
                tlSub.textContent = `Showing all ${allNetRecordsInPeriod.length} individual check points recorded in this period.`;
                labels = [];
                speedData = [];
                pingData = [];

                for (let i = 0; i < allNetRecordsInPeriod.length; i++) {
                    const r = allNetRecordsInPeriod[i];
                    if (i > 0) {
                        const prev = allNetRecordsInPeriod[i - 1];
                        // If same day and gap > 25 minutes, insert a break so the line doesn't bridge across hours of missing checks
                        const diffMin = (r.dateStr === prev.dateStr)
                            ? (r.hour * 60 + r.minute) - (prev.hour * 60 + prev.minute)
                            : 999;
                        if (diffMin > 25) {
                            labels.push("•");
                            speedData.push(null);
                            pingData.push(null);
                        }
                    }
                    labels.push(`${r.dateStr !== allNetRecordsInPeriod[0].dateStr ? r.dateStr.slice(5) + ' ' : ''}${r.time}`);
                    speedData.push(r.isOffline ? 0 : (r.speedMbps != null ? r.speedMbps : null));
                    pingData.push(r.pingMs != null ? r.pingMs : null);
                }
            } else {
                const intervalMin = parseInt(netTimelineAggregation, 10);
                const intervalName = intervalMin === 60 ? '1-hour' : `${intervalMin}-minute`;
                tlSub.textContent = `Averaged into ${intervalName} intervals across this period (${allNetRecordsInPeriod.length} checks).`;

                const recordsByBucket = new Map();
                for (const r of allNetRecordsInPeriod) {
                    const totalMin = r.hour * 60 + r.minute;
                    const bMin = Math.floor(totalMin / intervalMin) * intervalMin;
                    const bh = Math.floor(bMin / 60);
                    const bm = bMin % 60;
                    const bucketTime = `${String(bh).padStart(2, '0')}:${String(bm).padStart(2, '0')}`;
                    const key = `${r.dateStr} ${bucketTime}`;
                    if (!recordsByBucket.has(key)) {
                        recordsByBucket.set(key, []);
                    }
                    recordsByBucket.get(key).push(r);
                }

                const uniqueDates = Array.from(new Set(allNetRecordsInPeriod.map(r => r.dateStr))).sort();
                const now = new Date();
                const todayStr = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;

                const buckets = [];

                for (const dStr of uniqueDates) {
                    const isToday = (dStr === todayStr);
                    let startMin = 0;
                    let endMin = 24 * 60 - intervalMin;

                    if (isToday) {
                        const currentTotalMin = now.getHours() * 60 + now.getMinutes();
                        endMin = Math.min(24 * 60 - intervalMin, Math.ceil(currentTotalMin / intervalMin) * intervalMin);
                    }

                    for (let m = startMin; m <= endMin; m += intervalMin) {
                        const bh = Math.floor(m / 60);
                        const bm = m % 60;
                        const bucketTime = `${String(bh).padStart(2, '0')}:${String(bm).padStart(2, '0')}`;
                        const key = `${dStr} ${bucketTime}`;
                        buckets.push({
                            dateStr: dStr,
                            time: bucketTime,
                            records: recordsByBucket.get(key) || []
                        });
                    }
                }

                const firstDate = buckets[0]?.dateStr;
                labels = buckets.map(b => `${uniqueDates.length > 1 && b.dateStr !== firstDate ? b.dateStr.slice(5) + ' ' : ''}${b.time}`);
                speedData = buckets.map(b => {
                    if (!b.records || b.records.length === 0) return null;
                    const speeds = b.records.map(r => r.isOffline ? 0 : r.speedMbps).filter(v => v != null);
                    if (speeds.length === 0) return null;
                    const avg = speeds.reduce((a, c) => a + c, 0) / speeds.length;
                    return +avg.toFixed(1);
                });
                pingData = buckets.map(b => {
                    if (!b.records || b.records.length === 0) return null;
                    const p = b.records.map(r => r.pingMs).filter(v => v != null && v > 0);
                    return p.length ? Math.round(p.reduce((a, c) => a + c, 0) / p.length) : null;
                });
            }

            const intervalTag = netTimelineAggregation === '60' ? '1h' : (netTimelineAggregation === 'raw' ? '' : `${netTimelineAggregation}m`);
            window.renderChart({
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [
                        {
                            label: netTimelineAggregation === 'raw' ? 'Speed (Mbps)' : `Avg Speed (${intervalTag} Mbps)`,
                            data: speedData,
                            borderColor: '#38bdf8',
                            backgroundColor: 'rgba(56, 189, 248, 0.1)',
                            fill: true,
                            tension: 0.3,
                            spanGaps: false,
                            yAxisID: 'y',
                            pointBackgroundColor: speedData.map((v, idx) => {
                                const r = (netTimelineAggregation === 'raw') ? allNetRecordsInPeriod[idx] : null;
                                if (v === 0) return '#ef4444';
                                if (r && r.speedEstimated) return '#f59e0b';
                                return '#38bdf8';
                            }),
                            pointBorderColor: speedData.map((v, idx) => {
                                const r = (netTimelineAggregation === 'raw') ? allNetRecordsInPeriod[idx] : null;
                                if (v === 0) return '#ef4444';
                                if (r && r.speedEstimated) return '#f59e0b';
                                return '#38bdf8';
                            }),
                            pointRadius: speedData.map((v, idx) => {
                                if (v == null) return 0;
                                const r = (netTimelineAggregation === 'raw') ? allNetRecordsInPeriod[idx] : null;
                                if (v === 0) return 3.5;
                                if (r && r.speedEstimated) return 3.0;
                                return (netTimelineAggregation === 'raw' && labels.length > 50) ? 1.5 : 2.5;
                            }),
                            pointHoverRadius: 6
                        },
                        {
                            label: netTimelineAggregation === 'raw' ? 'Ping (ms)' : `Avg Ping (${intervalTag} ms)`,
                            data: pingData,
                            borderColor: '#fb923c',
                            backgroundColor: 'transparent',
                            borderDash: [4, 4],
                            tension: 0.2,
                            spanGaps: false,
                            yAxisID: 'y1',
                            pointRadius: pingData.map(v => v == null ? 0 : ((netTimelineAggregation === 'raw' && labels.length > 50) ? 1.5 : 2.5)),
                            pointHoverRadius: 6
                        }
                    ]
                },
                options: {
                    responsive: true,
                    interaction: {
                        mode: 'index',
                        intersect: false
                    },
                    plugins: {
                        tooltip: {
                            callbacks: {
                                label: function(context) {
                                    const isPing = context.dataset.yAxisID === 'y1';
                                    const val = context.raw;
                                    const idx = context.dataIndex;
                                    const r = (netTimelineAggregation === 'raw') ? allNetRecordsInPeriod[idx] : null;

                                    if (isPing) {
                                        if (val == null) {
                                            return ` ${context.dataset.label}: - (No checks recorded)`;
                                        }
                                        return ` ${context.dataset.label}: ${val} ms`;
                                    } else {
                                        if (val === 0) {
                                            return ` ${context.dataset.label}: 🔴 0.0 Mbps (Offline)`;
                                        }
                                        if (val == null) {
                                            return ` ${context.dataset.label}: - (No checks recorded)`;
                                        }
                                        if (r && r.speedEstimated) {
                                            return ` ${context.dataset.label}: ~${val} Mbps (Low speed: timeout <0.8 Mbps)`;
                                        }
                                        return ` ${context.dataset.label}: ${val} Mbps`;
                                    }
                                }
                            }
                        }
                    },
                    scales: {
                        x: { ticks: { maxTicksLimit: 12 } },
                        y: {
                            beginAtZero: true,
                            title: { display: true, text: 'Mbps' },
                            position: 'left'
                        },
                        y1: {
                            beginAtZero: true,
                            title: { display: true, text: 'Ping (ms)' },
                            position: 'right',
                            grid: { drawOnChartArea: false }
                        }
                    }
                }
            }, tlChartContainer);
        }

        tlAggOptions.forEach(opt => {
            const btn = tlAggContainer.createEl("button", { text: opt.label });
            btn.style.padding = "4px 8px";
            btn.style.fontSize = "11px";
            btn.style.borderRadius = "4px";
            btn.style.border = "1px solid var(--background-modifier-border)";
            btn.style.cursor = "pointer";
            tlAggBtns.push({ id: opt.id, btn });

            btn.addEventListener("click", () => {
                netTimelineAggregation = opt.id;
                updateTlAggBtns();
                renderTimelineChart();
            });
        });

        function updateTlAggBtns() {
            tlAggBtns.forEach(({ id, btn }) => {
                const active = (id === netTimelineAggregation);
                btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
                btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
                btn.style.fontWeight = active ? "bold" : "normal";
            });
        }
        updateTlAggBtns();
        renderTimelineChart();
    }

    // ==============================================================================
    // 2. HOURLY PERFORMANCE PROFILE & WI-FI DEEP DIVE (WITH 7/14/30 DAYS SCOPE & ELEMENT TOGGLES)
    // ==============================================================================
    const profileSection = card.createDiv();
    profileSection.style.marginTop = "22px";
    profileSection.style.paddingTop = "18px";
    profileSection.style.borderTop = "1px solid var(--background-modifier-border)";

    const profileHeader = profileSection.createEl("h4", { text: "🕒 Hourly Performance Profile & Wi-Fi Analysis" });
    profileHeader.style.margin = "0 0 4px 0";

    const profileSub = profileSection.createEl("p", { 
        text: "Analyze average speed, peak/min fluctuations, and latency at each hour of the day across last 7, 14, or 30 days, filtered by Wi-Fi network." 
    });
    profileSub.style.fontSize = "11px";
    profileSub.style.opacity = "0.7";
    profileSub.style.margin = "0 0 16px 0";

    // Known networks across all records for full coverage
    const allKnownNetworks = Array.from(new Set(allInternetRecords.map(r => r.cleanNetwork))).filter(Boolean).sort();
    if (netSelectedWifi !== "__all__" && !allKnownNetworks.includes(netSelectedWifi)) {
        netSelectedWifi = "__all__";
    }

    // Controls Row 1: Wi-Fi Selector & Days Horizon
    const controlsRow1 = profileSection.createDiv();
    controlsRow1.style.display = "flex";
    controlsRow1.style.alignItems = "center";
    controlsRow1.style.gap = "14px";
    controlsRow1.style.marginBottom = "10px";
    controlsRow1.style.flexWrap = "wrap";

    // 1. Wi-Fi Filter Dropdown
    const wifiContainer = controlsRow1.createDiv();
    wifiContainer.style.display = "flex";
    wifiContainer.style.alignItems = "center";
    wifiContainer.style.gap = "6px";

    const wifiLabelEl = wifiContainer.createEl("span", { text: "📶 Wi-Fi:" });
    wifiLabelEl.style.fontSize = "12px";
    wifiLabelEl.style.fontWeight = "bold";

    const wifiSelect = wifiContainer.createEl("select");
    wifiSelect.style.padding = "5px 10px";
    wifiSelect.style.borderRadius = "6px";
    wifiSelect.style.fontSize = "12px";
    wifiSelect.style.backgroundColor = "var(--background-modifier-form-field)";
    wifiSelect.style.color = "var(--text-normal)";
    wifiSelect.style.border = "1px solid var(--background-modifier-border)";

    const allWifiOpt = wifiSelect.createEl("option", { 
        text: `🌐 All Networks`, 
        value: "__all__" 
    });
    if (netSelectedWifi === "__all__") allWifiOpt.selected = true;

    allKnownNetworks.forEach(net => {
        const opt = wifiSelect.createEl("option", { 
            text: `📶 ${net}`, 
            value: net 
        });
        if (net === netSelectedWifi) opt.selected = true;
    });

    wifiSelect.addEventListener("change", () => {
        netSelectedWifi = wifiSelect.value;
        updateHourlyProfileView();
    });

    // 2. Scope / Days Horizon Selector
    const scopeContainer = controlsRow1.createDiv();
    scopeContainer.style.display = "flex";
    scopeContainer.style.alignItems = "center";
    scopeContainer.style.gap = "4px";

    const scopeLabel = scopeContainer.createEl("span", { text: "Days:" });
    scopeLabel.style.fontSize = "12px";
    scopeLabel.style.fontWeight = "bold";

    const scopeOptions = [
        { id: "7", label: "7 Days" },
        { id: "14", label: "14 Days" },
        { id: "30", label: "30 Days" },
        { id: "period", label: `Filter Period (${filters.find(f => f.id === activeFilter)?.label || "Active"})` }
    ];

    const scopeBtns = [];
    scopeOptions.forEach(opt => {
        const btn = scopeContainer.createEl("button", { text: opt.label });
        btn.style.padding = "4px 8px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        scopeBtns.push({ id: opt.id, btn });

        btn.addEventListener("click", () => {
            netHourlyScope = opt.id;
            updateScopeBtns();
            updateHourlyProfileView();
        });
    });

    function updateScopeBtns() {
        scopeBtns.forEach(({ id, btn }) => {
            const active = (id === netHourlyScope);
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });
    }
    updateScopeBtns();

    // Controls Row 2: Elements to Show (Avg Speed, Peak/Min Fluctuations, Latency) & View Format
    const controlsRow2 = profileSection.createDiv();
    controlsRow2.style.display = "flex";
    controlsRow2.style.alignItems = "center";
    controlsRow2.style.gap = "14px";
    controlsRow2.style.marginBottom = "16px";
    controlsRow2.style.flexWrap = "wrap";

    // 3. Elements to Show
    const elementsContainer = controlsRow2.createDiv();
    elementsContainer.style.display = "flex";
    elementsContainer.style.alignItems = "center";
    elementsContainer.style.gap = "4px";

    const elementsLabel = elementsContainer.createEl("span", { text: "Show:" });
    elementsLabel.style.fontSize = "12px";
    elementsLabel.style.fontWeight = "bold";

    const elementToggles = [
        { key: "avg", label: "🚀 Avg Speed", get: () => netShowAvgSpeed, set: v => { netShowAvgSpeed = v; } },
        { key: "fluc", label: "📊 Peak/Min Fluctuations", get: () => netShowFluctuations, set: v => { netShowFluctuations = v; } },
        { key: "lat", label: "⏱️ Latency (Ping)", get: () => netShowLatency, set: v => { netShowLatency = v; } }
    ];

    const elementBtns = [];
    elementToggles.forEach(toggle => {
        const btn = elementsContainer.createEl("button", { text: toggle.label });
        btn.style.padding = "4px 8px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        elementBtns.push({ toggle, btn });

        btn.addEventListener("click", () => {
            const nextVal = !toggle.get();
            // Prevent turning off all 3 elements
            const othersActive = elementToggles.filter(t => t.key !== toggle.key && t.get()).length > 0;
            if (!nextVal && !othersActive) return;

            toggle.set(nextVal);
            updateElementBtns();
            updateHourlyProfileView();
        });
    });

    function updateElementBtns() {
        elementBtns.forEach(({ toggle, btn }) => {
            const active = toggle.get();
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });
    }
    updateElementBtns();

    // Right-aligned container for Format and Resolution/Granularity
    const rightControls = controlsRow2.createDiv();
    rightControls.style.display = "flex";
    rightControls.style.alignItems = "center";
    rightControls.style.gap = "10px";
    rightControls.style.marginLeft = "auto";
    rightControls.style.flexWrap = "wrap";

    // 4. View Format (Hourly Profile vs Candles)
    const viewContainer = rightControls.createDiv();
    viewContainer.style.display = "flex";
    viewContainer.style.alignItems = "center";
    viewContainer.style.gap = "4px";

    const viewLabel = viewContainer.createEl("span", { text: "Format:" });
    viewLabel.style.fontSize = "12px";
    viewLabel.style.fontWeight = "bold";

    const viewOptions = [
        { id: "hourly_profile", label: "🕒 24h Profile" },
        { id: "timeline", label: "📈 Candles" }
    ];

    const viewBtns = [];
    viewOptions.forEach(opt => {
        const btn = viewContainer.createEl("button", { text: opt.label });
        btn.style.padding = "4px 8px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        viewBtns.push({ id: opt.id, btn });

        btn.addEventListener("click", () => {
            netViewMode = opt.id;
            updateViewBtns();
            updateHourlyProfileView();
        });
    });

    // Resolution buttons for 24h Profile
    const profResContainer = rightControls.createDiv();
    profResContainer.style.display = netViewMode === "hourly_profile" ? "flex" : "none";
    profResContainer.style.alignItems = "center";
    profResContainer.style.gap = "4px";

    const profResOptions = [
        { val: 60, label: "1h" },
        { val: 30, label: "30m" },
        { val: 15, label: "15m" }
    ];

    const profResBtns = [];
    profResOptions.forEach(opt => {
        const btn = profResContainer.createEl("button", { text: opt.label });
        btn.style.padding = "4px 6px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        profResBtns.push({ val: opt.val, btn });

        btn.addEventListener("click", () => {
            netProfileResolution = opt.val;
            updateProfResBtns();
            updateHourlyProfileView();
        });
    });

    function updateProfResBtns() {
        profResBtns.forEach(({ val, btn }) => {
            const active = (val === netProfileResolution);
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });
    }

    // Granularity buttons for candles
    const granContainer = rightControls.createDiv();
    granContainer.style.display = netViewMode === "timeline" ? "flex" : "none";
    granContainer.style.alignItems = "center";
    granContainer.style.gap = "4px";

    const granOptions = [
        { val: 5, label: "5m" },
        { val: 15, label: "15m" },
        { val: 30, label: "30m" },
        { val: 60, label: "1h" }
    ];

    const granBtns = [];
    granOptions.forEach(opt => {
        const btn = granContainer.createEl("button", { text: opt.label });
        btn.style.padding = "4px 6px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        granBtns.push({ val: opt.val, btn });

        btn.addEventListener("click", () => {
            netGranularity = opt.val;
            updateGranBtns();
            updateHourlyProfileView();
        });
    });

    function updateGranBtns() {
        granBtns.forEach(({ val, btn }) => {
            const active = (val === netGranularity);
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });
    }

    function updateViewBtns() {
        viewBtns.forEach(({ id, btn }) => {
            const active = (id === netViewMode);
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });
        profResContainer.style.display = netViewMode === "hourly_profile" ? "flex" : "none";
        granContainer.style.display = netViewMode === "timeline" ? "flex" : "none";
        updateProfResBtns();
        updateGranBtns();
    }
    updateViewBtns();

    // Dynamic Profile Container
    const profileContent = profileSection.createDiv();

    function updateHourlyProfileView() {
        profileContent.innerHTML = "";

        // Resolve scope records
        let baseRecords;
        let scopeLabelStr;
        if (netHourlyScope === "period") {
            baseRecords = allNetRecordsInPeriod;
            scopeLabelStr = filters.find(f => f.id === activeFilter)?.label || "Active Period";
        } else {
            const days = parseInt(netHourlyScope, 10);
            const latestDate = allInternetRecords.length ? allInternetRecords[allInternetRecords.length - 1].dateObj : new Date();
            const cutoff = new Date(latestDate.getFullYear(), latestDate.getMonth(), latestDate.getDate() - (days - 1), 0, 0, 0, 0);
            baseRecords = allInternetRecords.filter(r => r.dateObj >= cutoff);
            scopeLabelStr = `Last ${days} Days`;
        }

        // Filter by selected Wi-Fi
        const activeNetRecords = netSelectedWifi === "__all__"
            ? baseRecords
            : baseRecords.filter(r => r.cleanNetwork === netSelectedWifi);

        const currentNetworksInScope = Array.from(new Set(baseRecords.map(r => r.cleanNetwork))).filter(Boolean);
        const displayNetName = netSelectedWifi === "__all__"
            ? (currentNetworksInScope.length > 1 ? `All Networks (${currentNetworksInScope.length})` : (currentNetworksInScope[0] || "All Networks"))
            : netSelectedWifi;

        if (activeNetRecords.length === 0) {
            profileContent.createEl("p", { 
                text: `No records found for "${netSelectedWifi}" in ${scopeLabelStr}.` 
            }).style.opacity = "0.6";
            return;
        }

        // Multi-Network Comparison Cards (when All Networks is selected and multiple networks exist in this scope)
        if (netSelectedWifi === "__all__" && currentNetworksInScope.length > 1) {
            const compSection = profileContent.createDiv();
            compSection.style.marginBottom = "14px";
            
            const compHeading = compSection.createEl("div");
            compHeading.style.fontSize = "11px";
            compHeading.style.fontWeight = "bold";
            compHeading.style.opacity = "0.75";
            compHeading.style.textTransform = "uppercase";
            compHeading.style.marginBottom = "8px";
            compHeading.textContent = `📶 Wi-Fi Networks Breakdown (${scopeLabelStr})`;

            const compGrid = compSection.createDiv();
            compGrid.style.display = "grid";
            compGrid.style.gridTemplateColumns = "repeat(auto-fit, minmax(200px, 1fr))";
            compGrid.style.gap = "8px";

            currentNetworksInScope.forEach((net, idx) => {
                const netItems = baseRecords.filter(r => r.cleanNetwork === net);
                const nSpeeds = netItems.map(r => r.speedMbps).filter(s => s != null && s > 0);
                const nPings = netItems.map(r => r.pingMs).filter(p => p != null && p > 0);
                const nAvgSpeed = nSpeeds.length ? (nSpeeds.reduce((a, b) => a + b, 0) / nSpeeds.length).toFixed(1) : "-";
                const nPeakSpeed = nSpeeds.length ? Math.max(...nSpeeds).toFixed(1) : "-";
                const nAvgPing = nPings.length ? Math.round(nPings.reduce((a, b) => a + b, 0) / nPings.length) : "-";
                const nUptime = Math.round((netItems.filter(r => r.isOnline).length / netItems.length) * 100);
                const netColor = palette[idx % palette.length];

                const netCard = compGrid.createDiv();
                netCard.style.padding = "8px 12px";
                netCard.style.borderRadius = "6px";
                netCard.style.border = `1px solid var(--background-modifier-border)`;
                netCard.style.borderLeft = `4px solid ${netColor}`;
                netCard.style.backgroundColor = "var(--background-primary)";
                netCard.style.cursor = "pointer";
                netCard.title = `Click to filter specifically to ${net}`;

                netCard.innerHTML = `
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px;">
                        <span style="font-weight: bold; font-size: 12px; color: ${netColor};">${net}</span>
                        <span style="font-size: 10px; opacity: 0.7;">${netItems.length} checks</span>
                    </div>
                    <div style="display: flex; justify-content: space-between; font-size: 11px;">
                        <span>Avg: <b>${nAvgSpeed} Mbps</b> <span style="opacity: 0.6;">(peak: ${nPeakSpeed})</span></span>
                        <span>Ping: <b>${nAvgPing} ms</b> · ${nUptime}%</span>
                    </div>
                `;

                netCard.addEventListener("click", () => {
                    netSelectedWifi = net;
                    wifiSelect.value = net;
                    updateHourlyProfileView();
                });
            });
        }

        // Chart Container
        const chartDiv = profileContent.createDiv();
        chartDiv.style.marginBottom = "14px";

        if (netViewMode === "hourly_profile") {
            const bucketSize = [15, 30, 60].includes(netProfileResolution) ? netProfileResolution : 60;
            const numBuckets = Math.floor(1440 / bucketSize);
            const bucketLabel = bucketSize === 60 ? '1h' : `${bucketSize}m`;
            const bucketName = bucketSize === 60 ? '1-hour' : `${bucketSize}-minute`;

            // Mode 1: 24h Profile
            const chartHeader = chartDiv.createEl("h4");
            chartHeader.style.margin = "0 0 4px 0";
            chartHeader.textContent = `🕒 24h Profile (${bucketLabel} Buckets) · ${displayNetName} · ${scopeLabelStr}`;

            const chartSub = chartDiv.createEl("p");
            chartSub.style.fontSize = "11px";
            chartSub.style.opacity = "0.7";
            chartSub.style.margin = "0 0 10px 0";
            chartSub.textContent = `Showing 24-hour daily patterns grouped into ${bucketName} averages across ${scopeLabelStr} (${activeNetRecords.length} checks analyzed).`;

            const hourLabels = Array.from({ length: numBuckets }, (_, i) => {
                const totalMinutes = i * bucketSize;
                const h = Math.floor(totalMinutes / 60);
                const m = totalMinutes % 60;
                return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
            });

            const hourlyStats = Array.from({ length: numBuckets }, (_, i) => {
                const bStart = i * bucketSize;
                const bEnd = bStart + bucketSize;
                const inBucket = activeNetRecords.filter(r => {
                    const m = r.hour * 60 + r.minute;
                    return m >= bStart && m < bEnd;
                });
                const onlineSpeeds = inBucket.map(r => r.speedMbps).filter(v => v != null && v > 0);
                const allSpeeds = inBucket.map(r => r.isOffline ? 0 : r.speedMbps).filter(v => v != null);
                const p = inBucket.map(r => r.pingMs).filter(v => v != null && v > 0);
                const offlineCount = inBucket.filter(r => r.isOffline).length;
                return {
                    count: inBucket.length,
                    offlineCount: offlineCount,
                    avgSpeed: allSpeeds.length ? +(allSpeeds.reduce((a, b) => a + b, 0) / allSpeeds.length).toFixed(1) : null,
                    minSpeed: onlineSpeeds.length ? Math.min(...onlineSpeeds) : (allSpeeds.length ? 0 : null),
                    maxSpeed: onlineSpeeds.length ? Math.max(...onlineSpeeds) : (allSpeeds.length ? 0 : null),
                    avgPing: p.length ? Math.round(p.reduce((a, b) => a + b, 0) / p.length) : null
                };
            });

            const datasets = [];

            // 1. Peak/Min Fluctuations (Floating range bar)
            if (netShowFluctuations) {
                datasets.push({
                    type: 'bar',
                    label: 'Peak/Min Fluctuations (Mbps)',
                    data: hourlyStats.map(h => {
                        if (h.minSpeed == null || h.maxSpeed == null) return null;
                        const low = h.minSpeed === h.maxSpeed ? Math.max(0, +(h.minSpeed - 0.08).toFixed(2)) : h.minSpeed;
                        const high = h.minSpeed === h.maxSpeed ? +(h.maxSpeed + 0.08).toFixed(2) : h.maxSpeed;
                        return [low, high];
                    }),
                    backgroundColor: 'rgba(56, 189, 248, 0.25)',
                    borderColor: '#38bdf8',
                    borderWidth: 1,
                    borderRadius: 4,
                    borderSkipped: false,
                    yAxisID: 'y'
                });
            }

            // 2. Average Speed Line
            if (netShowAvgSpeed) {
                datasets.push({
                    type: 'line',
                    label: 'Avg Speed (Mbps)',
                    data: hourlyStats.map(h => h.avgSpeed),
                    borderColor: '#38bdf8',
                    backgroundColor: '#38bdf8',
                    borderWidth: 2.5,
                    pointRadius: 3.5,
                    pointHoverRadius: 6,
                    tension: 0.2,
                    spanGaps: true,
                    yAxisID: 'y'
                });
            }

            // 3. Latency Line
            if (netShowLatency) {
                const pingAxis = (!netShowAvgSpeed && !netShowFluctuations) ? 'y' : 'y1';
                datasets.push({
                    type: 'line',
                    label: 'Latency (Ping ms)',
                    data: hourlyStats.map(h => h.avgPing),
                    borderColor: '#fb923c',
                    backgroundColor: '#fb923c',
                    borderDash: [4, 4],
                    borderWidth: 2,
                    pointRadius: 3,
                    pointHoverRadius: 5,
                    tension: 0.2,
                    spanGaps: true,
                    yAxisID: pingAxis
                });
            }

            const hasSpeed = netShowAvgSpeed || netShowFluctuations;
            const scales = {
                x: {
                    title: { display: true, text: 'Time of Day (Local Time)' },
                    ticks: { maxTicksLimit: bucketSize === 60 ? 24 : (bucketSize === 30 ? 24 : 16) }
                },
                y: {
                    beginAtZero: true,
                    title: { display: true, text: hasSpeed ? 'Speed (Mbps)' : 'Latency (ms)' },
                    position: 'left'
                }
            };

            if (hasSpeed && netShowLatency) {
                scales.y1 = {
                    beginAtZero: true,
                    title: { display: true, text: 'Ping (ms)' },
                    position: 'right',
                    grid: { drawOnChartArea: false }
                };
            }

            window.renderChart({
                type: 'bar',
                data: {
                    labels: hourLabels,
                    datasets: datasets
                },
                options: {
                    responsive: true,
                    plugins: {
                        tooltip: {
                            callbacks: {
                                label: function(context) {
                                    const stat = hourlyStats[context.dataIndex];
                                    if (!stat || stat.count === 0) {
                                        return ` ${context.dataset.label}: - (No checks recorded)`;
                                    }
                                    const isPing = context.dataset.yAxisID === 'y1' || context.dataset.label.includes('Ping');
                                    const val = context.raw;
                                    if (isPing) {
                                        if (val == null) {
                                            return ` ${context.dataset.label}: 🔴 Offline / Timed out`;
                                        }
                                        return ` ${context.dataset.label}: ${val} ms`;
                                    } else {
                                        if (val === 0) {
                                            return ` ${context.dataset.label}: 🔴 0.0 Mbps (Offline)`;
                                        }
                                        if (val == null) {
                                            return ` ${context.dataset.label}: - (No Data)`;
                                        }
                                        if (Array.isArray(val)) {
                                            const offStr = stat.offlineCount > 0 ? ` (${stat.offlineCount} offline)` : '';
                                            return ` ${context.dataset.label}: ${val[0]} – ${val[1]} Mbps${offStr}`;
                                        }
                                        return ` ${context.dataset.label}: ${val} Mbps`;
                                    }
                                }
                            }
                        }
                    },
                    scales: scales
                }
            }, chartDiv);

        } else {
            // Mode 2: Timeline Candles (5m, 15m, 30m, 1h)
            const chartHeader = chartDiv.createEl("h4");
            chartHeader.style.margin = "0 0 4px 0";
            chartHeader.textContent = `📈 Connection Timeline (${netGranularity}m Candles) · ${displayNetName} · ${scopeLabelStr}`;

            const chartSub = chartDiv.createEl("p");
            chartSub.style.fontSize = "11px";
            chartSub.style.opacity = "0.7";
            chartSub.style.margin = "0 0 10px 0";
            chartSub.textContent = `Chronological progression aggregated into ${netGranularity}-minute candles across ${scopeLabelStr}.`;

            const candleMap = new Map();
            for (const r of activeNetRecords) {
                const totalMin = r.hour * 60 + r.minute;
                const bMin = Math.floor(totalMin / netGranularity) * netGranularity;
                const bh = Math.floor(bMin / 60);
                const bm = bMin % 60;
                const bucketTime = `${String(bh).padStart(2, '0')}:${String(bm).padStart(2, '0')}`;
                const key = `${r.dateStr} ${bucketTime}`;
                if (!candleMap.has(key)) {
                    candleMap.set(key, { dateStr: r.dateStr, time: bucketTime, records: [] });
                }
                candleMap.get(key).records.push(r);
            }

            const candles = [];
            for (const [key, val] of candleMap.entries()) {
                const onlineSpeeds = val.records.map(r => r.speedMbps).filter(v => v != null && v > 0);
                const allSpeeds = val.records.map(r => r.isOffline ? 0 : r.speedMbps).filter(v => v != null);
                const p = val.records.map(r => r.pingMs).filter(v => v != null && v > 0);
                const offlineCount = val.records.filter(r => r.isOffline).length;
                candles.push({
                    key,
                    dateStr: val.dateStr,
                    time: val.time,
                    count: val.records.length,
                    offlineCount: offlineCount,
                    avgSpeed: allSpeeds.length ? +(allSpeeds.reduce((a, b) => a + b, 0) / allSpeeds.length).toFixed(1) : null,
                    minSpeed: onlineSpeeds.length ? Math.min(...onlineSpeeds) : (allSpeeds.length ? 0 : null),
                    maxSpeed: onlineSpeeds.length ? Math.max(...onlineSpeeds) : (allSpeeds.length ? 0 : null),
                    avgPing: p.length ? Math.round(p.reduce((a, b) => a + b, 0) / p.length) : null
                });
            }

            if (candles.length === 0) {
                chartDiv.createEl("p", { text: "No candle data available." }).style.opacity = "0.6";
                return;
            }

            const firstDate = candles[0]?.dateStr;
            const candleLabels = candles.map(c => 
                `${c.dateStr !== firstDate ? c.dateStr.slice(5) + ' ' : ''}${c.time}`
            );

            const datasets = [];

            if (netShowFluctuations) {
                datasets.push({
                    type: 'bar',
                    label: `Speed Candle (Min – Max)`,
                    data: candles.map(c => {
                        if (c.minSpeed == null || c.maxSpeed == null) return null;
                        const low = c.minSpeed === c.maxSpeed ? Math.max(0, +(c.minSpeed - 0.08).toFixed(2)) : c.minSpeed;
                        const high = c.minSpeed === c.maxSpeed ? +(c.maxSpeed + 0.08).toFixed(2) : c.maxSpeed;
                        return [low, high];
                    }),
                    backgroundColor: 'rgba(56, 189, 248, 0.25)',
                    borderColor: '#38bdf8',
                    borderWidth: 1,
                    borderRadius: 4,
                    borderSkipped: false,
                    yAxisID: 'y'
                });
            }

            if (netShowAvgSpeed) {
                datasets.push({
                    type: 'line',
                    label: 'Avg Speed (Mbps)',
                    data: candles.map(c => c.avgSpeed),
                    borderColor: '#38bdf8',
                    backgroundColor: '#38bdf8',
                    borderWidth: 2,
                    pointRadius: candles.length > 50 ? 1 : 2.5,
                    pointHoverRadius: 5,
                    tension: 0.2,
                    spanGaps: true,
                    yAxisID: 'y'
                });
            }

            if (netShowLatency) {
                const pingAxis = (!netShowAvgSpeed && !netShowFluctuations) ? 'y' : 'y1';
                datasets.push({
                    type: 'line',
                    label: 'Latency (Ping ms)',
                    data: candles.map(c => c.avgPing),
                    borderColor: '#fb923c',
                    backgroundColor: '#fb923c',
                    borderDash: [4, 4],
                    borderWidth: 1.5,
                    pointRadius: candles.length > 50 ? 1 : 2,
                    pointHoverRadius: 5,
                    tension: 0.2,
                    spanGaps: true,
                    yAxisID: pingAxis
                });
            }

            const hasSpeed = netShowAvgSpeed || netShowFluctuations;
            const scales = {
                x: {
                    ticks: { maxTicksLimit: 14 }
                },
                y: {
                    beginAtZero: true,
                    title: { display: true, text: hasSpeed ? 'Speed (Mbps)' : 'Latency (ms)' },
                    position: 'left'
                }
            };

            if (hasSpeed && netShowLatency) {
                scales.y1 = {
                    beginAtZero: true,
                    title: { display: true, text: 'Ping (ms)' },
                    position: 'right',
                    grid: { drawOnChartArea: false }
                };
            }

            window.renderChart({
                type: 'bar',
                data: {
                    labels: candleLabels,
                    datasets: datasets
                },
                options: {
                    responsive: true,
                    plugins: {
                        tooltip: {
                            callbacks: {
                                label: function(context) {
                                    const c = candles[context.dataIndex];
                                    if (!c || c.count === 0) {
                                        return ` ${context.dataset.label}: - (No checks recorded)`;
                                    }
                                    const isPing = context.dataset.yAxisID === 'y1' || context.dataset.label.includes('Ping');
                                    const val = context.raw;
                                    if (isPing) {
                                        if (val == null) {
                                            return ` ${context.dataset.label}: 🔴 Offline / Timed out`;
                                        }
                                        return ` ${context.dataset.label}: ${val} ms`;
                                    } else {
                                        if (val === 0) {
                                            return ` ${context.dataset.label}: 🔴 0.0 Mbps (Offline)`;
                                        }
                                        if (val == null) {
                                            return ` ${context.dataset.label}: - (No Data)`;
                                        }
                                        if (Array.isArray(val)) {
                                            const offStr = c.offlineCount > 0 ? ` (${c.offlineCount} offline)` : '';
                                            return ` ${context.dataset.label}: ${val[0]} – ${val[1]} Mbps${offStr}`;
                                        }
                                        return ` ${context.dataset.label}: ${val} Mbps`;
                                    }
                                }
                            }
                        }
                    },
                    scales: scales
                }
            }, chartDiv);
        }
    }

    // Initial profile render
    updateHourlyProfileView();

    // ==============================================================================
    // 3. DETAILED NETWORK CHECKS TABLE (COLLAPSIBLE)
    // ==============================================================================
    const details = card.createEl("details");
    details.style.marginTop = "20px";
    details.style.paddingTop = "10px";
    details.style.borderTop = "1px solid var(--background-modifier-border)";

    const summary = details.createEl("summary");
    summary.style.fontWeight = "bold";
    summary.style.fontSize = "13px";
    summary.style.cursor = "pointer";
    summary.style.userSelect = "none";
    summary.style.display = "flex";
    summary.style.alignItems = "center";
    summary.style.justifyContent = "space-between";
    summary.style.padding = "4px 2px";
    summary.title = "Click to expand/collapse network check history";

    const summaryTitle = summary.createDiv();
    summaryTitle.style.display = "flex";
    summaryTitle.style.alignItems = "center";
    summaryTitle.style.gap = "8px";
    summaryTitle.innerHTML = `
        <span>📶 Network & Speed Check History</span>
        <span class="toggle-hint" style="font-size: 11px; opacity: 0.6; font-weight: normal;">(click to expand)</span>
    `;

    const summaryBadge = summary.createEl("span", {
        text: `${allNetRecordsInPeriod.length} checks recorded`
    });
    summaryBadge.style.fontSize = "11px";
    summaryBadge.style.padding = "2px 8px";
    summaryBadge.style.borderRadius = "10px";
    summaryBadge.style.backgroundColor = "var(--background-modifier-form-field)";
    summaryBadge.style.border = "1px solid var(--background-modifier-border)";
    summaryBadge.style.opacity = "0.75";
    summaryBadge.style.fontWeight = "normal";

    details.addEventListener("toggle", () => {
        const hint = summaryTitle.querySelector(".toggle-hint");
        if (hint) {
            hint.textContent = details.open ? "(click to collapse)" : "(click to expand)";
        }
    });

    const tableDiv = details.createDiv();
    tableDiv.style.marginTop = "10px";
    tableDiv.style.overflowX = "auto";

    const netTable = tableDiv.createEl("table");
    netTable.style.width = "100%";
    netTable.style.fontSize = "11px";
    netTable.style.borderCollapse = "collapse";

    const showDateCol = activeFilter !== "today";

    netTable.innerHTML = `
        <thead>
            <tr style="border-bottom: 1px solid var(--background-modifier-border); text-align: left; opacity: 0.7;">
                ${showDateCol ? '<th style="padding: 4px 6px;">Date</th>' : ''}
                <th style="padding: 4px 6px;">Time</th>
                <th style="padding: 4px 6px;">Status</th>
                <th style="padding: 4px 6px;">Network / Wi-Fi</th>
                <th style="padding: 4px 6px;">Ping</th>
                <th style="padding: 4px 6px;">Download Speed</th>
                <th style="padding: 4px 6px;">Notes</th>
            </tr>
        </thead>
        <tbody>
            ${allNetRecordsInPeriod.slice(-25).reverse().map(r => `
                <tr style="border-bottom: 1px solid var(--background-modifier-border);">
                    ${showDateCol ? `<td style="padding: 4px 6px;">${r.dateStr}</td>` : ''}
                    <td style="padding: 4px 6px; font-weight: bold;">${r.time}</td>
                    <td style="padding: 4px 6px;">${r.status}</td>
                    <td style="padding: 4px 6px; font-weight: bold; color: var(--text-accent);">${r.network}</td>
                    <td style="padding: 4px 6px;">${r.pingMs != null ? r.pingMs + ' ms' : '-'}</td>
                    <td style="padding: 4px 6px; font-weight: bold; color: ${r.isOffline ? 'inherit' : '#38bdf8'};">${!r.isOffline && r.speedMbps != null ? r.speedMbps.toFixed(1) + ' Mbps' : '-'}</td>
                    <td style="padding: 4px 6px; opacity: 0.8;">${r.notes || '-'}</td>
                </tr>
            `).join("")}
        </tbody>
    `;
}

// ==============================================================================
// RENDER HELPER 4: FOCUS CONTINUITY & DISTRACTION ANALYSIS
// ==============================================================================
function renderFocusContinuitySection() {
    const { stopwatch: records, focus: focusRecords } = getFilteredRecords(activeFilter);

    // Prepare interruption dataset
    let pauses = [...focusRecords];
    let isSynthetic = false;

    // If no explicit focus log records yet, derive inter-session pauses from consecutive stopwatch intervals on the same day
    if (pauses.length === 0 && records.length > 1) {
        const sorted = [...records].sort((a, b) => {
            if (a.dateStr !== b.dateStr) return a.dateStr.localeCompare(b.dateStr);
            return toTotalMinutes(a.start) - toTotalMinutes(b.start);
        });

        for (let i = 0; i < sorted.length - 1; i++) {
            const cur = sorted[i];
            const next = sorted[i + 1];
            if (cur.dateStr === next.dateStr) {
                const curEndMin = toTotalMinutes(cur.end);
                const nextStartMin = toTotalMinutes(next.start);
                const gap = nextStartMin - curEndMin;
                if (gap >= 0.2 && gap <= 180) { // between 12s and 3h
                    // If next project is different and lasted > 30s (0.5m), it's a switch between tasks!
                    const isSwitch = cur.project !== next.project && next.minutes >= 0.5;
                    const isDist = !isSwitch && gap < 5.0;
                    const isBrk = !isSwitch && !isDist;
                    pauses.push({
                        dateStr: cur.dateStr,
                        project: cur.project,
                        start: cur.end,
                        resume: next.start,
                        minutes: gap,
                        durationText: formatMinutes(gap),
                        category: isSwitch ? "🔀 Switch Between Tasks" : (isDist ? "⚡ Distraction (<5m)" : "☕ Break (≥5m)"),
                        reason: isSwitch ? "TaskSwitch" : (isDist ? "Distraction" : "Break"),
                        note: isSwitch ? `Switched to ${next.project}` : "Inter-session interval gap",
                        isDistraction: isDist,
                        isBreak: isBrk,
                        isIdea: false,
                        isTaskSwitch: isSwitch,
                        isSynthetic: true
                    });
                }
            }
        }
        if (pauses.length > 0) isSynthetic = true;
    }

    const card = chartSection.createDiv();
    card.style.padding = "18px";
    card.style.borderRadius = "8px";
    card.style.backgroundColor = "var(--background-secondary)";
    card.style.border = "1px solid var(--background-modifier-border)";
    card.style.marginBottom = "30px";

    // Header Row
    const headerRow = card.createDiv();
    headerRow.style.display = "flex";
    headerRow.style.justifyContent = "space-between";
    headerRow.style.alignItems = "center";
    headerRow.style.flexWrap = "wrap";
    headerRow.style.gap = "10px";
    headerRow.style.marginBottom = "14px";

    const titleDiv = headerRow.createDiv();
    titleDiv.createEl("h3", { text: "🎯 Focus Continuity & Distraction Analysis" }).style.margin = "0";
    const subText = titleDiv.createEl("p", { 
        text: "Track focus fragmentation, distinguish planned breaks from short interruptions (< 5m), and monitor refocusing habits." 
    });
    subText.style.fontSize = "11px";
    subText.style.opacity = "0.7";
    subText.style.margin = "2px 0 0 0";

    const filterBadge = headerRow.createEl("span", {
        text: `Filter: ${filters.find(f => f.id === activeFilter)?.label || "Active"}`
    });
    filterBadge.style.fontSize = "11px";
    filterBadge.style.padding = "3px 8px";
    filterBadge.style.borderRadius = "4px";
    filterBadge.style.backgroundColor = "var(--background-modifier-form-field)";
    filterBadge.style.border = "1px solid var(--background-modifier-border)";
    filterBadge.style.opacity = "0.8";

    // Metrics Computation
    const totalPauses = pauses.length;
    const distractions = pauses.filter(p => p.isDistraction);
    const breaks = pauses.filter(p => p.isBreak);
    const ideas = pauses.filter(p => p.isIdea);
    const taskSwitches = pauses.filter(p => p.isTaskSwitch);

    const distractionMin = Math.round(distractions.reduce((s, p) => s + p.minutes, 0));
    const breakMin = Math.round(breaks.reduce((s, p) => s + p.minutes, 0));
    const ideaMin = Math.round(ideas.reduce((s, p) => s + p.minutes, 0));
    const taskSwitchMin = Math.round(taskSwitches.reduce((s, p) => s + p.minutes, 0));
    const totalWorkMin = records.reduce((s, r) => s + r.minutes, 0);

    const continuityScore = (totalWorkMin + distractionMin > 0)
        ? Math.round((totalWorkMin / (totalWorkMin + distractionMin)) * 100)
        : 100;

    const avgStreakMin = totalPauses > 0
        ? Math.round(totalWorkMin / (totalPauses + 1))
        : Math.round(totalWorkMin / Math.max(1, records.length));

    // KPI Summary Grid
    const kpiRow = card.createDiv();
    kpiRow.style.display = "grid";
    kpiRow.style.gridTemplateColumns = "repeat(auto-fit, minmax(130px, 1fr))";
    kpiRow.style.gap = "10px";
    kpiRow.style.marginBottom = "18px";

    function createKpiCard(label, mainVal, subVal, color) {
        const c = kpiRow.createDiv();
        c.style.padding = "10px 12px";
        c.style.borderRadius = "6px";
        c.style.backgroundColor = "var(--background-primary)";
        c.style.border = "1px solid var(--background-modifier-border)";
        c.style.display = "flex";
        c.style.flexDirection = "column";
        c.style.justifyContent = "center";

        const l = c.createDiv();
        l.style.fontSize = "10px";
        l.style.textTransform = "uppercase";
        l.style.opacity = "0.7";
        l.style.marginBottom = "2px";
        l.textContent = label;

        const v = c.createDiv();
        v.style.fontSize = "18px";
        v.style.fontWeight = "bold";
        v.style.color = color || "var(--text-normal)";
        v.textContent = mainVal;

        if (subVal) {
            const s = c.createDiv();
            s.style.fontSize = "11px";
            s.style.opacity = "0.6";
            s.style.marginTop = "2px";
            s.textContent = subVal;
        }
    }

    const scoreColor = continuityScore >= 85 ? "#34d399" : (continuityScore >= 70 ? "#fbbf24" : "#f87171");
    createKpiCard("Continuity Score", `${continuityScore}%`, "Focus index", scoreColor);
    createKpiCard("⚡ Distractions (<5m)", `${distractions.length}`, `${distractionMin} min total`, distractions.length > 0 ? "#f87171" : "var(--text-normal)");
    createKpiCard("☕ Valid Breaks", `${breaks.length}`, `${breakMin} min total`, "#34d399");
    createKpiCard("💡 Ideas Captured", `${ideas.length}`, `${ideaMin} min total`, "#fbbf24");
    createKpiCard("🔀 Task Switches", `${taskSwitches.length}`, `${taskSwitchMin} min total`, "#38bdf8");
    createKpiCard("Avg Focus Block", `${avgStreakMin}m`, "Uninterrupted streak", "var(--text-accent)");

    // Onboarding Banner if using synthetic or empty
    if (!focusFile || allFocusRecords.length === 0) {
        const hintBanner = card.createDiv();
        hintBanner.style.padding = "8px 12px";
        hintBanner.style.marginBottom = "16px";
        hintBanner.style.borderRadius = "6px";
        hintBanner.style.fontSize = "12px";
        hintBanner.style.backgroundColor = "rgba(56, 189, 248, 0.1)";
        hintBanner.style.border = "1px solid rgba(56, 189, 248, 0.3)";
        hintBanner.style.color = "var(--text-normal)";
        hintBanner.innerHTML = `
            💡 <b>Track Distraction Reasons Automatically:</b> Enable <i>"Focus Continuity & Distraction Tracking"</i> in Stopwatch Overlay Settings → Behavior. 
            When resuming from a pause, a 1-click dialog (shortcuts <code>1</code>, <code>2</code>, <code>3</code>, <code>4</code>) will let you categorize pauses into Valid Breaks, Distractions, Quick Ideas, or Task Switches.
            ${isSynthetic ? '<br><span style="opacity: 0.8; font-size: 11px;">* Metrics below are currently estimated from interval gaps between your logged stopwatch sessions.</span>' : ''}
        `;
    }

    // Visual Charts if there are pauses
    if (totalPauses > 0) {
        const chartsGrid = card.createDiv();
        chartsGrid.style.display = "grid";
        chartsGrid.style.gridTemplateColumns = "repeat(auto-fit, minmax(280px, 1fr))";
        chartsGrid.style.gap = "16px";
        chartsGrid.style.marginBottom = "20px";

        // Chart 1: Pause Reason Breakdown Doughnut
        const doughnutBox = chartsGrid.createDiv();
        doughnutBox.style.padding = "12px";
        doughnutBox.style.borderRadius = "6px";
        doughnutBox.style.backgroundColor = "var(--background-primary)";
        doughnutBox.style.border = "1px solid var(--background-modifier-border)";

        const chart1Title = doughnutBox.createEl("div", { text: "Pause Reason Distribution" });
        chart1Title.style.fontSize = "12px";
        chart1Title.style.fontWeight = "bold";
        chart1Title.style.marginBottom = "8px";
        chart1Title.style.opacity = "0.8";

        const doughnutCanvasContainer = doughnutBox.createDiv();
        doughnutCanvasContainer.style.maxWidth = "240px";
        doughnutCanvasContainer.style.margin = "0 auto";

        window.renderChart({
            type: 'doughnut',
            data: {
                labels: ['Distractions (<5m)', 'Valid Breaks', 'Quick Ideas', 'Task Switches'],
                datasets: [{
                    label: 'Count',
                    data: [distractions.length, breaks.length, ideas.length, taskSwitches.length],
                    backgroundColor: ['#f87171', '#34d399', '#fbbf24', '#38bdf8']
                }]
            },
            options: {
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 10 } } }
                }
            }
        }, doughnutCanvasContainer);

        // Chart 2: Distractions & Breaks by Project
        const barBox = chartsGrid.createDiv();
        barBox.style.padding = "12px";
        barBox.style.borderRadius = "6px";
        barBox.style.backgroundColor = "var(--background-primary)";
        barBox.style.border = "1px solid var(--background-modifier-border)";

        const chart2Title = barBox.createEl("div", { text: "Interruptions by Project" });
        chart2Title.style.fontSize = "12px";
        chart2Title.style.fontWeight = "bold";
        chart2Title.style.marginBottom = "8px";
        chart2Title.style.opacity = "0.8";

        const barCanvasContainer = barBox.createDiv();

        const projectList = Array.from(new Set(pauses.map(p => p.project))).sort();
        const distData = projectList.map(proj => pauses.filter(p => p.project === proj && p.isDistraction).length);
        const breakData = projectList.map(proj => pauses.filter(p => p.project === proj && p.isBreak).length);
        const ideaData = projectList.map(proj => pauses.filter(p => p.project === proj && p.isIdea).length);
        const switchData = projectList.map(proj => pauses.filter(p => p.project === proj && p.isTaskSwitch).length);

        window.renderChart({
            type: 'bar',
            data: {
                labels: projectList,
                datasets: [
                    { label: 'Distractions', data: distData, backgroundColor: '#f87171', stack: 'stack0' },
                    { label: 'Breaks', data: breakData, backgroundColor: '#34d399', stack: 'stack0' },
                    { label: 'Ideas', data: ideaData, backgroundColor: '#fbbf24', stack: 'stack0' },
                    { label: 'Task Switches', data: switchData, backgroundColor: '#38bdf8', stack: 'stack0' }
                ]
            },
            options: {
                scales: {
                    x: { stacked: true },
                    y: { stacked: true, beginAtZero: true, ticks: { precision: 0 } }
                },
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 10 } } }
                }
            }
        }, barCanvasContainer);
    }

    // Detailed Interruption History Table (Collapsible)
    if (totalPauses > 0) {
        const details = card.createEl("details");
        details.style.marginTop = "12px";
        details.style.borderRadius = "6px";
        details.style.backgroundColor = "var(--background-primary)";
        details.style.border = "1px solid var(--background-modifier-border)";
        details.style.padding = "10px 14px";

        const summary = details.createEl("summary");
        summary.style.cursor = "pointer";
        summary.style.fontWeight = "bold";
        summary.style.fontSize = "13px";
        summary.style.display = "flex";
        summary.style.justifyContent = "space-between";
        summary.style.alignItems = "center";

        const summaryTitle = summary.createDiv();
        summaryTitle.style.display = "flex";
        summaryTitle.style.alignItems = "center";
        summaryTitle.style.gap = "8px";
        summaryTitle.innerHTML = `
            <span>📋 Interruption & Pause History</span>
            <span class="toggle-hint" style="font-size: 11px; opacity: 0.6; font-weight: normal;">(click to expand)</span>
        `;

        const summaryBadge = summary.createEl("span", {
            text: `${totalPauses} interruptions logged`
        });
        summaryBadge.style.fontSize = "11px";
        summaryBadge.style.padding = "2px 8px";
        summaryBadge.style.borderRadius = "10px";
        summaryBadge.style.backgroundColor = "var(--background-modifier-form-field)";
        summaryBadge.style.border = "1px solid var(--background-modifier-border)";
        summaryBadge.style.opacity = "0.75";
        summaryBadge.style.fontWeight = "normal";

        details.addEventListener("toggle", () => {
            const hint = summaryTitle.querySelector(".toggle-hint");
            if (hint) {
                hint.textContent = details.open ? "(click to collapse)" : "(click to expand)";
            }
        });

        const tableContainer = details.createDiv();
        tableContainer.style.overflowX = "auto";
        tableContainer.style.marginTop = "10px";

        const recentPauses = [...pauses].reverse().slice(0, 30);
        tableContainer.innerHTML = `
            <table style="width: 100%; border-collapse: collapse; font-size: 12px;">
                <thead>
                    <tr style="text-align: left; border-bottom: 2px solid var(--background-modifier-border); opacity: 0.75;">
                        <th style="padding: 6px 8px;">Date</th>
                        <th style="padding: 6px 8px;">Time (Pause → Resume)</th>
                        <th style="padding: 6px 8px;">Project</th>
                        <th style="padding: 6px 8px;">Duration</th>
                        <th style="padding: 6px 8px;">Category</th>
                        <th style="padding: 6px 8px;">Notes</th>
                    </tr>
                </thead>
                <tbody>
                    ${recentPauses.map(p => {
                        let badgeBg = "rgba(52, 211, 153, 0.15)";
                        let badgeColor = "#34d399";
                        if (p.isDistraction) {
                            badgeBg = "rgba(248, 113, 113, 0.15)";
                            badgeColor = "#f87171";
                        } else if (p.isIdea) {
                            badgeBg = "rgba(251, 191, 36, 0.15)";
                            badgeColor = "#fbbf24";
                        } else if (p.isTaskSwitch) {
                            badgeBg = "rgba(56, 189, 248, 0.15)";
                            badgeColor = "#38bdf8";
                        }

                        return `
                        <tr style="border-bottom: 1px solid var(--background-modifier-border);">
                            <td style="padding: 6px 8px;">${p.dateStr}</td>
                            <td style="padding: 6px 8px; font-family: monospace;">${p.start} → ${p.resume}</td>
                            <td style="padding: 6px 8px; font-weight: bold; color: var(--text-accent);">${p.project}</td>
                            <td style="padding: 6px 8px; font-weight: 500;">${p.durationText || (Math.round(p.minutes) + 'm')}</td>
                            <td style="padding: 6px 8px;">
                                <span style="display: inline-block; padding: 2px 7px; border-radius: 4px; font-size: 11px; font-weight: bold; background-color: ${badgeBg}; color: ${badgeColor};">
                                    ${p.category || (p.isDistraction ? '⚡ Distraction' : (p.isIdea ? '💡 Quick Idea' : (p.isTaskSwitch ? '🔀 Switch Between Tasks' : '☕ Break')))}
                                </span>
                            </td>
                            <td style="padding: 6px 8px; opacity: 0.85;">${p.note || '-'}</td>
                        </tr>
                        `;
                    }).join("")}
                </tbody>
            </table>
        `;
    }
}

// ==============================================================================
// RENDER HELPER 5: DYNAMIC TREND BY PROJECT (DAILY / WEEKLY / MONTHLY)
// WITH INTERACTIVE HOVERABLE SLIDING WINDOWS & ZOOM/PAN CONTROLS
// ==============================================================================
function renderTrendSection() {
    if (allRecords.length === 0) return;

    const card = chartSection.createDiv();
    card.style.padding = "18px";
    card.style.borderRadius = "8px";
    card.style.backgroundColor = "var(--background-secondary)";
    card.style.border = "1px solid var(--background-modifier-border)";
    card.style.marginTop = "32px";
    card.style.marginBottom = "30px";
    card.style.position = "relative";

    // 1. Helper to build all chronological periods across all records
    function getAllPeriods(granularity) {
        let minTime = Infinity;
        let maxTime = -Infinity;
        for (const r of allRecords) {
            if (r.dateObj) {
                const t = r.dateObj.getTime();
                if (t < minTime) minTime = t;
                if (t > maxTime) maxTime = t;
            }
        }
        if (minTime === Infinity) return [];

        const now = new Date();
        const todayMidnight = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
        if (todayMidnight > maxTime) {
            maxTime = todayMidnight;
        }

        const minDate = new Date(minTime);
        const maxDate = new Date(maxTime);

        const mNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
        const fullMNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
        const dNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

        const periods = [];

        if (granularity === "day") {
            const cur = new Date(minDate.getFullYear(), minDate.getMonth(), minDate.getDate());
            const end = new Date(maxDate.getFullYear(), maxDate.getMonth(), maxDate.getDate());
            while (cur <= end) {
                const y = cur.getFullYear();
                const m = String(cur.getMonth() + 1).padStart(2, '0');
                const d = String(cur.getDate()).padStart(2, '0');
                const key = `${y}-${m}-${d}`;
                const label = `${mNames[cur.getMonth()]} ${cur.getDate()}`;
                const fullLabel = `${dNames[cur.getDay()]}, ${mNames[cur.getMonth()]} ${cur.getDate()}, ${y}`;
                periods.push({
                    key,
                    label,
                    fullLabel
                });
                cur.setDate(cur.getDate() + 1);
            }
        } else if (granularity === "week") {
            function getMonday(d) {
                const date = new Date(d);
                const day = (date.getDay() + 6) % 7; // Monday = 0
                date.setDate(date.getDate() - day);
                date.setHours(0, 0, 0, 0);
                return date;
            }
            function getWeekNum(d) {
                const target = new Date(d.valueOf());
                const dayNr = (d.getDay() + 6) % 7;
                target.setDate(target.getDate() - dayNr + 3);
                const firstThursday = target.valueOf();
                target.setMonth(0, 1);
                if (target.getDay() !== 4) {
                    target.setMonth(0, 1 + ((4 - target.getDay()) + 7) % 7);
                }
                return 1 + Math.ceil((firstThursday - target) / 604800000);
            }

            const cur = getMonday(minDate);
            const end = getMonday(maxDate);
            while (cur <= end) {
                const sun = new Date(cur);
                sun.setDate(cur.getDate() + 6);
                sun.setHours(23, 59, 59, 999);
                const wNum = getWeekNum(cur);
                const key = `${cur.getFullYear()}-W${String(wNum).padStart(2, '0')}`;
                const label = `W${wNum} (${mNames[cur.getMonth()]} ${cur.getDate()})`;
                const fullLabel = `Week ${wNum}: ${mNames[cur.getMonth()]} ${cur.getDate()} – ${mNames[sun.getMonth()]} ${sun.getDate()}, ${cur.getFullYear()}`;
                periods.push({
                    key,
                    label,
                    fullLabel,
                    startMs: cur.getTime(),
                    endMs: sun.getTime()
                });
                cur.setDate(cur.getDate() + 7);
            }
        } else if (granularity === "month") {
            const cur = new Date(minDate.getFullYear(), minDate.getMonth(), 1);
            const end = new Date(maxDate.getFullYear(), maxDate.getMonth(), 1);
            while (cur <= end) {
                const y = cur.getFullYear();
                const m = cur.getMonth();
                const key = `${y}-${String(m + 1).padStart(2, '0')}`;
                const label = `${mNames[m]} ${y}`;
                const fullLabel = `${fullMNames[m]} ${y}`;
                periods.push({
                    key,
                    label,
                    fullLabel,
                    year: y,
                    month: m
                });
                cur.setMonth(cur.getMonth() + 1);
            }
        }

        return periods;
    }

    // 2. Pre-aggregate project minutes per period
    function aggregateData(periods, granularity) {
        const periodProjectMinutes = {};
        for (const p of periods) {
            periodProjectMinutes[p.key] = {};
        }

        function getWeekKey(d) {
            const date = new Date(d);
            const day = (date.getDay() + 6) % 7;
            date.setDate(date.getDate() - day);
            date.setHours(0, 0, 0, 0);
            const target = new Date(date.valueOf());
            const dayNr = (date.getDay() + 6) % 7;
            target.setDate(target.getDate() - dayNr + 3);
            const firstThursday = target.valueOf();
            target.setMonth(0, 1);
            if (target.getDay() !== 4) {
                target.setMonth(0, 1 + ((4 - target.getDay()) + 7) % 7);
            }
            const wNum = 1 + Math.ceil((firstThursday - target) / 604800000);
            return `${date.getFullYear()}-W${String(wNum).padStart(2, '0')}`;
        }

        for (const r of allRecords) {
            if (!r.project || !r.minutes) continue;
            let key = null;
            if (granularity === "day") {
                key = r.dateStr;
            } else if (granularity === "week") {
                key = getWeekKey(r.dateObj);
            } else if (granularity === "month") {
                key = r.dateStr ? r.dateStr.slice(0, 7) : null;
            }

            if (key && periodProjectMinutes[key]) {
                periodProjectMinutes[key][r.project] = (periodProjectMinutes[key][r.project] || 0) + r.minutes;
            }
        }

        return periodProjectMinutes;
    }

    // Header container
    const headerRow = card.createDiv();
    headerRow.style.display = "flex";
    headerRow.style.justifyContent = "space-between";
    headerRow.style.alignItems = "flex-start";
    headerRow.style.flexWrap = "wrap";
    headerRow.style.gap = "10px";
    headerRow.style.marginBottom = "14px";

    const titleCol = headerRow.createDiv();
    const headingEl = titleCol.createEl("h3");
    headingEl.style.margin = "0 0 4px 0";

    const subtitleEl = titleCol.createEl("p");
    subtitleEl.style.opacity = "0.7";
    subtitleEl.style.fontSize = "12px";
    subtitleEl.style.margin = "0";

    // Top Controls: Granularity & Wheel Mode
    const topControls = headerRow.createDiv();
    topControls.style.display = "flex";
    topControls.style.gap = "6px";
    topControls.style.alignItems = "center";
    topControls.style.flexWrap = "wrap";

    // Granularity Tabs
    const granOpts = [
        { id: "day", label: "📅 Daily", defaultSize: 14 },
        { id: "week", label: "📆 Weekly", defaultSize: 8 },
        { id: "month", label: "🗓️ Monthly", defaultSize: 6 }
    ];
    const granBtns = [];

    granOpts.forEach(opt => {
        const btn = topControls.createEl("button", { text: opt.label });
        btn.style.padding = "5px 12px";
        btn.style.fontSize = "12px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        granBtns.push({ id: opt.id, btn, defaultSize: opt.defaultSize });

        btn.addEventListener("click", () => {
            if (trendGranularity !== opt.id) {
                trendGranularity = opt.id;
                trendWindowSize = opt.defaultSize;
                trendOffset = 0;
                renderView();
            }
        });
    });

    // Wheel Mode Button
    const wheelModeBtn = topControls.createEl("button");
    wheelModeBtn.style.padding = "5px 12px";
    wheelModeBtn.style.fontSize = "12px";
    wheelModeBtn.style.borderRadius = "4px";
    wheelModeBtn.style.border = "1px solid var(--background-modifier-border)";
    wheelModeBtn.style.cursor = "pointer";
    wheelModeBtn.title = "Toggle whether mouse wheel on the chart scrolls through time or zooms window size";
    wheelModeBtn.addEventListener("click", () => {
        trendWheelMode = (trendWheelMode === "pan") ? "zoom" : "pan";
        updateWheelModeBtn();
    });

    function updateWheelModeBtn() {
        if (trendWheelMode === "pan") {
            wheelModeBtn.textContent = "🖱️ Wheel: ↔️ Pan Time";
            wheelModeBtn.style.backgroundColor = "var(--background-modifier-form-field)";
            wheelModeBtn.style.color = "var(--text-normal)";
            wheelModeBtn.style.fontWeight = "normal";
        } else {
            wheelModeBtn.textContent = "🖱️ Wheel: 🔍 Zoom Window";
            wheelModeBtn.style.backgroundColor = "var(--interactive-accent)";
            wheelModeBtn.style.color = "var(--text-on-accent)";
            wheelModeBtn.style.fontWeight = "bold";
        }
    }
    updateWheelModeBtn();

    // Toolbar Row: Navigation, Window Status, Presets & Steppers
    const toolbarRow = card.createDiv();
    toolbarRow.style.display = "flex";
    toolbarRow.style.justifyContent = "space-between";
    toolbarRow.style.alignItems = "center";
    toolbarRow.style.flexWrap = "wrap";
    toolbarRow.style.gap = "8px";
    toolbarRow.style.padding = "8px 12px";
    toolbarRow.style.borderRadius = "6px";
    toolbarRow.style.backgroundColor = "var(--background-primary)";
    toolbarRow.style.border = "1px solid var(--background-modifier-border)";
    toolbarRow.style.marginBottom = "10px";

    // Nav group
    const navGroup = toolbarRow.createDiv();
    navGroup.style.display = "flex";
    navGroup.style.gap = "4px";
    navGroup.style.alignItems = "center";

    const oldestBtn = navGroup.createEl("button", { text: "⏮️ Oldest" });
    const prevBtn = navGroup.createEl("button", { text: "◀ Prev" });
    const nextBtn = navGroup.createEl("button", { text: "Next ▶" });
    const latestBtn = navGroup.createEl("button", { text: "⏭️ Latest" });

    [oldestBtn, prevBtn, nextBtn, latestBtn].forEach(b => {
        b.style.padding = "4px 8px";
        b.style.fontSize = "11px";
        b.style.borderRadius = "4px";
        b.style.border = "1px solid var(--background-modifier-border)";
        b.style.cursor = "pointer";
    });

    // Window badge (Center)
    const badgeEl = toolbarRow.createDiv();
    badgeEl.style.fontSize = "11px";
    badgeEl.style.fontWeight = "bold";
    badgeEl.style.color = "var(--text-accent)";
    badgeEl.style.textAlign = "center";

    // Presets & Steppers group (Right)
    const presetsGroup = toolbarRow.createDiv();
    presetsGroup.style.display = "flex";
    presetsGroup.style.gap = "4px";
    presetsGroup.style.alignItems = "center";
    presetsGroup.style.flexWrap = "wrap";

    // Timeline Slider Row
    const sliderContainer = card.createDiv();
    sliderContainer.style.display = "flex";
    sliderContainer.style.alignItems = "center";
    sliderContainer.style.gap = "10px";
    sliderContainer.style.marginBottom = "12px";

    const sliderPastLabel = sliderContainer.createEl("span", { text: "⏮️ Past" });
    sliderPastLabel.style.fontSize = "11px";
    sliderPastLabel.style.opacity = "0.7";
    sliderPastLabel.style.whiteSpace = "nowrap";

    const slider = sliderContainer.createEl("input");
    slider.type = "range";
    slider.style.flex = "1";
    slider.style.cursor = "pointer";

    const sliderLatestLabel = sliderContainer.createEl("span", { text: "Latest ⏭️" });
    sliderLatestLabel.style.fontSize = "11px";
    sliderLatestLabel.style.opacity = "0.7";
    sliderLatestLabel.style.whiteSpace = "nowrap";

    // Chart container
    const chartDivWrapper = card.createDiv();
    chartDivWrapper.style.position = "relative";
    chartDivWrapper.style.marginTop = "6px";

    // Hover guidance hint
    const hintEl = chartDivWrapper.createDiv();
    hintEl.style.fontSize = "10px";
    hintEl.style.opacity = "0.6";
    hintEl.style.textAlign = "right";
    hintEl.style.marginBottom = "4px";
    hintEl.textContent = "💡 Hover on chart to scroll • Wheel: pan time • Ctrl+Wheel: zoom window size";

    const chartDiv = chartDivWrapper.createDiv();

    // Floating overlay buttons on hover
    const floatLeft = chartDivWrapper.createEl("button", { text: "◀" });
    floatLeft.title = "View earlier period";
    floatLeft.style.position = "absolute";
    floatLeft.style.left = "6px";
    floatLeft.style.top = "50%";
    floatLeft.style.transform = "translateY(-50%)";
    floatLeft.style.zIndex = "10";
    floatLeft.style.padding = "10px 8px";
    floatLeft.style.borderRadius = "6px";
    floatLeft.style.border = "1px solid var(--background-modifier-border)";
    floatLeft.style.backgroundColor = "var(--background-secondary)";
    floatLeft.style.opacity = "0";
    floatLeft.style.transition = "opacity 0.2s ease, transform 0.1s ease";
    floatLeft.style.cursor = "pointer";
    floatLeft.style.fontSize = "14px";
    floatLeft.style.fontWeight = "bold";

    const floatRight = chartDivWrapper.createEl("button", { text: "▶" });
    floatRight.title = "View later period";
    floatRight.style.position = "absolute";
    floatRight.style.right = "6px";
    floatRight.style.top = "50%";
    floatRight.style.transform = "translateY(-50%)";
    floatRight.style.zIndex = "10";
    floatRight.style.padding = "10px 8px";
    floatRight.style.borderRadius = "6px";
    floatRight.style.border = "1px solid var(--background-modifier-border)";
    floatRight.style.backgroundColor = "var(--background-secondary)";
    floatRight.style.opacity = "0";
    floatRight.style.transition = "opacity 0.2s ease, transform 0.1s ease";
    floatRight.style.cursor = "pointer";
    floatRight.style.fontSize = "14px";
    floatRight.style.fontWeight = "bold";

    chartDivWrapper.addEventListener("mouseenter", () => {
        if (!prevBtn.disabled) floatLeft.style.opacity = "0.75";
        if (!nextBtn.disabled) floatRight.style.opacity = "0.75";
    });
    chartDivWrapper.addEventListener("mouseleave", () => {
        floatLeft.style.opacity = "0";
        floatRight.style.opacity = "0";
    });

    // Helper step calculation
    function getStep() {
        return (trendGranularity === "day") ? 7 : (trendGranularity === "week" ? 2 : 1);
    }

    function renderView() {
        // 1. Update Heading and Subtitle
        const granWord = trendGranularity === "day" ? "Daily" : (trendGranularity === "week" ? "Weekly" : "Monthly");
        headingEl.textContent = `📈 ${granWord} Trend (Hours by Project)`;
        subtitleEl.textContent = `Total hours worked per ${trendGranularity}, broken down and color-coded by project.`;

        // 2. Update Granularity Buttons
        granBtns.forEach(({ id, btn }) => {
            const active = (id === trendGranularity);
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });

        // 3. Periods & Aggregation
        const allPeriods = getAllPeriods(trendGranularity);
        const totalPeriods = allPeriods.length;
        if (totalPeriods === 0) {
            chartDiv.innerHTML = "<p style='opacity:0.6;font-size:12px;'>No records found.</p>";
            return;
        }

        const periodProjectMinutes = aggregateData(allPeriods, trendGranularity);

        // 4. Calculate effective window and offset
        let effSize = (trendWindowSize === "all") ? totalPeriods : Math.max(1, Math.min(trendWindowSize, totalPeriods));
        const maxOffset = Math.max(0, totalPeriods - effSize);
        trendOffset = Math.max(0, Math.min(trendOffset, maxOffset));

        const endIdx = totalPeriods - 1 - trendOffset;
        const startIdx = Math.max(0, endIdx - effSize + 1);
        const visiblePeriods = allPeriods.slice(startIdx, endIdx + 1);

        // 5. Update Navigation Buttons
        const canGoBack = (trendOffset < maxOffset);
        const canGoForward = (trendOffset > 0);

        oldestBtn.disabled = !canGoBack;
        oldestBtn.style.opacity = canGoBack ? "1" : "0.35";
        oldestBtn.style.cursor = canGoBack ? "pointer" : "default";

        prevBtn.disabled = !canGoBack;
        prevBtn.style.opacity = canGoBack ? "1" : "0.35";
        prevBtn.style.cursor = canGoBack ? "pointer" : "default";

        nextBtn.disabled = !canGoForward;
        nextBtn.style.opacity = canGoForward ? "1" : "0.35";
        nextBtn.style.cursor = canGoForward ? "pointer" : "default";

        latestBtn.disabled = !canGoForward;
        latestBtn.style.opacity = canGoForward ? "1" : "0.35";
        latestBtn.style.cursor = canGoForward ? "pointer" : "default";

        floatLeft.disabled = !canGoBack;
        floatLeft.style.display = canGoBack ? "block" : "none";
        floatRight.disabled = !canGoForward;
        floatRight.style.display = canGoForward ? "block" : "none";

        // 6. Update Badge
        const firstP = visiblePeriods[0];
        const lastP = visiblePeriods[visiblePeriods.length - 1];
        const unit = (trendGranularity === "day") ? "Days" : (trendGranularity === "week" ? "Weeks" : "Months");
        const statusText = (trendOffset === 0) ? "Latest" : `${trendOffset} ${unit.toLowerCase()} back`;
        badgeEl.textContent = `📅 ${firstP ? firstP.label : ''} – ${lastP ? lastP.label : ''} (${visiblePeriods.length} ${unit}) • [${statusText}] • ${visiblePeriods.length}/${totalPeriods} total`;

        // 7. Update Timeline Slider
        slider.min = "0";
        slider.max = String(maxOffset);
        slider.value = String(maxOffset - trendOffset);
        slider.disabled = (maxOffset === 0);

        // 8. Update Presets & Steppers
        presetsGroup.innerHTML = "";
        const lessBtn = presetsGroup.createEl("button", { text: "➖ Less" });
        lessBtn.style.padding = "4px 8px";
        lessBtn.style.fontSize = "11px";
        lessBtn.style.borderRadius = "4px";
        lessBtn.style.border = "1px solid var(--background-modifier-border)";
        lessBtn.style.cursor = "pointer";
        lessBtn.title = "Show fewer intervals (zoom in)";
        lessBtn.addEventListener("click", () => {
            const cur = (trendWindowSize === "all") ? totalPeriods : trendWindowSize;
            trendWindowSize = Math.max(3, cur - (trendGranularity === "day" ? 2 : 1));
            renderView();
        });

        const moreBtn = presetsGroup.createEl("button", { text: "➕ More" });
        moreBtn.style.padding = "4px 8px";
        moreBtn.style.fontSize = "11px";
        moreBtn.style.borderRadius = "4px";
        moreBtn.style.border = "1px solid var(--background-modifier-border)";
        moreBtn.style.cursor = "pointer";
        moreBtn.title = "Show more intervals (zoom out)";
        moreBtn.addEventListener("click", () => {
            const cur = (trendWindowSize === "all") ? totalPeriods : trendWindowSize;
            trendWindowSize = Math.min(totalPeriods, cur + (trendGranularity === "day" ? 2 : 1));
            renderView();
        });

        let presetsList = [];
        if (trendGranularity === "day") {
            presetsList = [{ id: 7, label: "7d" }, { id: 14, label: "14d" }, { id: 30, label: "30d" }, { id: "all", label: "All" }];
        } else if (trendGranularity === "week") {
            presetsList = [{ id: 4, label: "4w" }, { id: 8, label: "8w" }, { id: 12, label: "12w" }, { id: "all", label: "All" }];
        } else {
            presetsList = [{ id: 3, label: "3m" }, { id: 6, label: "6m" }, { id: 12, label: "12m" }, { id: "all", label: "All" }];
        }

        presetsList.forEach(p => {
            const pBtn = presetsGroup.createEl("button", { text: p.label });
            pBtn.style.padding = "4px 7px";
            pBtn.style.fontSize = "11px";
            pBtn.style.borderRadius = "4px";
            pBtn.style.border = "1px solid var(--background-modifier-border)";
            pBtn.style.cursor = "pointer";
            const isActive = (trendWindowSize === p.id);
            pBtn.style.backgroundColor = isActive ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            pBtn.style.color = isActive ? "var(--text-on-accent)" : "var(--text-normal)";
            pBtn.style.fontWeight = isActive ? "bold" : "normal";

            pBtn.addEventListener("click", () => {
                trendWindowSize = p.id;
                renderView();
            });
        });

        // 9. Prepare Datasets
        const activeProjectsInWindow = allProjectNames.filter(proj => {
            return visiblePeriods.some(p => (periodProjectMinutes[p.key]?.[proj] || 0) > 0);
        });
        const projectsForChart = activeProjectsInWindow.length > 0 ? activeProjectsInWindow : allProjectNames;

        const datasets = projectsForChart.map(proj => {
            const dataValues = visiblePeriods.map(p => {
                const min = periodProjectMinutes[p.key]?.[proj] || 0;
                return +(min / 60).toFixed(1);
            });
            return {
                label: proj,
                data: dataValues,
                backgroundColor: projectColorMap[proj] || '#6366f1',
                stack: 'trend',
                borderRadius: 2
            };
        });

        // 10. Render Chart
        chartDiv.innerHTML = "";
        window.renderChart({
            type: 'bar',
            data: {
                labels: visiblePeriods.map(p => p.label),
                datasets: datasets
            },
            options: {
                responsive: true,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { boxWidth: 12, font: { size: 11 } }
                    },
                    tooltip: {
                        callbacks: {
                            title: function(items) {
                                if (!items || items.length === 0) return '';
                                const idx = items[0].dataIndex;
                                return visiblePeriods[idx]?.fullLabel || items[0].label;
                            },
                            label: function(item) {
                                return ` ${item.dataset.label}: ${item.raw}h`;
                            },
                            footer: function(items) {
                                let tot = 0;
                                items.forEach(it => { tot += (it.raw || 0); });
                                return `Total: ${tot.toFixed(1)}h`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        stacked: true,
                        title: { 
                            display: true, 
                            text: trendGranularity === 'day' ? 'Date' : (trendGranularity === 'week' ? 'Week' : 'Month'),
                            font: { size: 12, weight: 'bold' }
                        }
                    },
                    y: {
                        stacked: true,
                        beginAtZero: true,
                        title: { display: true, text: 'Hours', font: { size: 12, weight: 'bold' } }
                    }
                }
            }
        }, chartDiv);
    }

    // Event listeners
    oldestBtn.addEventListener("click", () => {
        const allP = getAllPeriods(trendGranularity);
        const eff = (trendWindowSize === "all") ? allP.length : Math.min(trendWindowSize, allP.length);
        trendOffset = Math.max(0, allP.length - eff);
        renderView();
    });

    prevBtn.addEventListener("click", () => {
        const allP = getAllPeriods(trendGranularity);
        const eff = (trendWindowSize === "all") ? allP.length : Math.min(trendWindowSize, allP.length);
        const maxO = Math.max(0, allP.length - eff);
        trendOffset = Math.min(maxO, trendOffset + getStep());
        renderView();
    });

    floatLeft.addEventListener("click", () => {
        const allP = getAllPeriods(trendGranularity);
        const eff = (trendWindowSize === "all") ? allP.length : Math.min(trendWindowSize, allP.length);
        const maxO = Math.max(0, allP.length - eff);
        trendOffset = Math.min(maxO, trendOffset + getStep());
        renderView();
    });

    nextBtn.addEventListener("click", () => {
        trendOffset = Math.max(0, trendOffset - getStep());
        renderView();
    });

    floatRight.addEventListener("click", () => {
        trendOffset = Math.max(0, trendOffset - getStep());
        renderView();
    });

    latestBtn.addEventListener("click", () => {
        trendOffset = 0;
        renderView();
    });

    slider.addEventListener("input", (e) => {
        const val = parseInt(e.target.value, 10);
        const maxVal = parseInt(e.target.max, 10);
        trendOffset = Math.max(0, maxVal - val);
        renderView();
    });

    // Wheel Event Handler on chartDivWrapper
    chartDivWrapper.addEventListener("wheel", (e) => {
        e.preventDefault();
        e.stopPropagation();

        const allP = getAllPeriods(trendGranularity);
        const total = allP.length;
        if (total === 0) return;

        // Is zoom mode?
        const isZoom = (trendWheelMode === "zoom" && !e.shiftKey) || e.ctrlKey || e.metaKey;

        if (isZoom) {
            // Zoom: Show more or less
            let cur = (trendWindowSize === "all") ? total : trendWindowSize;
            const step = (trendGranularity === "day") ? 1 : 1;
            if (e.deltaY < 0) {
                // Zoom in -> show fewer
                trendWindowSize = Math.max(3, cur - step);
            } else {
                // Zoom out -> show more
                trendWindowSize = Math.min(total, cur + step);
            }
        } else {
            // Pan: See previous / next periods
            const delta = Math.abs(e.deltaX) > Math.abs(e.deltaY) ? e.deltaX : e.deltaY;
            const step = 1;
            let eff = (trendWindowSize === "all") ? total : Math.min(trendWindowSize, total);
            const maxO = Math.max(0, total - eff);

            if (delta < 0) {
                // Scroll up or left -> go back into past
                trendOffset = Math.min(maxO, trendOffset + step);
            } else {
                // Scroll down or right -> go forward to latest
                trendOffset = Math.max(0, trendOffset - step);
            }
        }

        renderView();
    }, { passive: false });

    // Initial render
    renderView();
}

// 6. Create Top Filter Buttons
const filterButtons = filters.map(f => {
    const btn = filterBar.createEl("button", { text: f.label });
    btn.style.padding = "6px 14px";
    btn.style.borderRadius = "6px";
    btn.style.border = "1px solid var(--background-modifier-border)";
    btn.style.cursor = "pointer";
    btn.style.fontSize = "13px";

    btn.addEventListener("click", () => {
        activeFilter = f.id;
        renderDashboard();
    });

    return { id: f.id, btn };
});

// Initial Render
renderDashboard();
```
