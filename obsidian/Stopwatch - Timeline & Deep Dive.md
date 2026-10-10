# 🔍 Stopwatch — Timeline & Session Deep Dive

> [!NAV] 🧭 **Stopwatch Dashboards**
> [[Stopwatch Dashboard|🏠 Overview]] · **🔍 Timeline & Sessions** · [[Stopwatch - Network Performance|🌐 Network]] · [[Stopwatch - Activity & Trends|📈 Activity & Trends]] · [[Stopwatch - Productivity Scores|🏆 Productivity Scores]] · [[Stopwatch - Mood & Feelings|😊 Mood & Feelings]]


```dataviewjs

// Fast Instant Chart Renderer (zero-delay animation)
function renderQuickChart(config, el) {
    if (!config) return null;
    if (!config.options) config.options = {};
    if (config.options.animation === undefined) config.options.animation = false;
    if (!config.options.hover) config.options.hover = {};
    if (config.options.hover.animationDuration === undefined) config.options.hover.animationDuration = 0;
    config.options.responsiveAnimationDuration = 0;
    return window.renderChart(config, el);
}


// Global in-memory cache shared across note transitions
const _swCache = window.__stopwatch_cache = window.__stopwatch_cache || {};

async function getCachedParsedLog(file, key, parseFn) {
    if (!file) return null;
    const mtime = file.stat ? (file.stat.mtime || 0) : 0;
    if (_swCache[key] && _swCache[key].mtime === mtime) {
        return _swCache[key].data;
    }
    const text = await app.vault.read(file);
    const data = parseFn(text);
    _swCache[key] = { mtime, data };
    return data;
}

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



function getCleanNetworkName(raw) {
    if (!raw) return "Unknown";
    let s = raw.trim();
    s = s.replace(/^[^\w\s\.-]+/, "").trim();
    if (s.includes(" (Saved)")) s = s.replace(" (Saved)", "");
    return s.trim() || "Unknown";
}

function parseStopwatchText(content) {
    const swLines = content.split(/\r?\n/);
    const records = [];
    for (let i = 0; i < swLines.length; i++) {
        const line = swLines[i];
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
                const [sh, sm] = start.split(":").map(Number);
                const [eh, em] = end.split(":").map(Number);
                const startHour = isNaN(sh) || isNaN(sm) ? 0 : sh + sm / 60;
                let endHour = isNaN(eh) || isNaN(em) ? 0 : eh + em / 60;
                if (endHour < startHour) endHour += 24;

                records.push({
                    dateStr,
                    dateObj: new Date(y, m - 1, d),
                    project,
                    start,
                    end,
                    startHour,
                    endHour,
                    minutes,
                    durationText
                });
            }
        }
    }
    return records;
}

function parseActivityWatchText(content) {
    const awLines = content.split('\n');
    const allActivityRecords = [];
    const awByDate = {};
    for (let i = 0; i < awLines.length; i++) {
        const line = awLines[i];
        if (line.charCodeAt(0) !== 124 || line.charCodeAt(1) === 45) continue;
        const parts = line.split('|');
        if (parts.length < 8) continue;
        const dateStr = parts[1].trim();
        if (dateStr.length !== 10 || dateStr.charCodeAt(4) !== 45) continue;
        const activity = parts[2].replace(/\*/g, '').trim();
        const start = parts[parts.length - 6].trim();
        const end = parts[parts.length - 5].trim();
        const minutes = parseFloat(parts[parts.length - 4]) || 0;
        const durationText = parts[parts.length - 3].trim();
        const type = parts[parts.length - 2].trim();
        const details = parts.slice(3, parts.length - 6).join(' - ').trim();
        const y = parseInt(dateStr.slice(0, 4), 10);
        const m = parseInt(dateStr.slice(5, 7), 10);
        const d = parseInt(dateStr.slice(8, 10), 10);

        const [sh, sm] = start.split(":").map(Number);
        const [eh, em] = end.split(":").map(Number);
        const startHour = isNaN(sh) || isNaN(sm) ? 0 : sh + sm / 60;
        let endHour = isNaN(eh) || isNaN(em) ? 0 : eh + em / 60;
        if (endHour < startHour) endHour += 24;

        const rec = {
            dateStr,
            dateObj: new Date(y, m - 1, d),
            activity,
            details,
            start,
            end,
            startHour,
            endHour,
            minutes,
            durationText,
            type,
            lowerText: (activity + " " + details).toLowerCase()
        };
        allActivityRecords.push(rec);
        if (type !== "Idle") {
            if (!awByDate[dateStr]) awByDate[dateStr] = [];
            awByDate[dateStr].push(rec);
        }
    }
    return { allActivityRecords, awByDate };
}

function parseInternetText(content) {
    const netLines = content.split(/\r?\n/);
    const records = [];
    let currentDateStr = null;

    for (let i = 0; i < netLines.length; i++) {
        const line = netLines[i];
        if (line.startsWith("##")) {
            const dateMatch = line.match(/^##\s*.*?\s*(\d{4}-\d{2}-\d{2})/);
            if (dateMatch) {
                currentDateStr = dateMatch[1];
                continue;
            }
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

            if (speedMbps == null && !isOffline && notes && (notes.includes("HttpClient.Timeout") || notes.includes("operation timed out"))) {
                speedMbps = 0.4;
                speedEstimated = true;
            }

            const [y, m, d] = currentDateStr.split("-").map(Number);
            const [hh, mm] = time.split(":").map(Number);
            const cleanNet = getCleanNetworkName(network);

            records.push({
                dateStr: currentDateStr,
                dateObj: new Date(y, m - 1, d),
                time,
                hour: isNaN(hh) ? 0 : hh,
                minute: isNaN(mm) ? 0 : mm,
                timestamp: new Date(y, m - 1, d, isNaN(hh) ? 0 : hh, isNaN(mm) ? 0 : mm).getTime(),
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
    return records;
}

function parseFocusText(content) {
    const fLines = content.split(/\r?\n/);
    const records = [];
    for (let i = 0; i < fLines.length; i++) {
        const line = fLines[i];
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
                records.push({
                    dateStr,
                    dateObj: new Date(y, m - 1, d),
                    project,
                    start,
                    resume,
                    minutes,
                    durationText,
                    category,
                    reason,
                    note,
                    isDistraction: reason === "Distraction",
                    isBreak: reason === "Break" || reason === "ValidBreak",
                    isIdea: reason === "Idea" || reason === "IdeaBrainstorming",
                    isTaskSwitch: reason === "TaskSwitch" || reason.toLowerCase().includes("switch")
                });
            }
        }
    }
    return records;
}

function parseMoodText(content) {
    const moodLines = content.split(/\r?\n/);
    const records = [];
    let curMoodDate = null;
    for (let i = 0; i < moodLines.length; i++) {
        const line = moodLines[i];
        const dm = line.match(/^##\s*.*?(\d{4}-\d{2}-\d{2})/);
        if (dm) { curMoodDate = dm[1]; continue; }
        if (!line.startsWith("|") || line.includes("---") || line.includes("Date") || !curMoodDate) continue;
        const parts = line.trim().replace(/^\|/, "").replace(/\|$/, "").split("|").map(p => p.trim());
        if (parts.length >= 5) {
            const score = parseFloat(parts[4]);
            if (isNaN(score)) continue;
            const dateStr = parts[0] || curMoodDate;
            const [y, m, d] = dateStr.split("-").map(Number);
            const [hh, mm] = parts[1].split(":").map(Number);
            records.push({
                dateStr,
                dateObj: new Date(y, m - 1, d),
                time: parts[1],
                hour: isNaN(hh) ? 0 : hh,
                minute: isNaN(mm) ? 0 : mm,
                timestamp: new Date(y, m - 1, d, isNaN(hh) ? 0 : hh, isNaN(mm) ? 0 : mm).getTime(),
                period: parts[2],
                mood: parts[3],
                score,
                keywords: parts[5] || "",
                projects: parts[6] || "",
                notes: parts[7] || ""
            });
        }
    }
    return records;
}

const rawSw = await getCachedParsedLog(stopwatchFile, "stopwatch_raw", parseStopwatchText) || [];
const allRecords = rawSw.map(r => {
    const scorePerHour = (typeof getProjectScore === "function") ? getProjectScore(r.project) : 1.0;
    return {
        ...r,
        scorePerHour,
        score: (r.minutes / 60) * scorePerHour
    };
});

const cachedAw = await getCachedParsedLog(activityFile, "activitywatch", parseActivityWatchText);
const allActivityRecords = cachedAw ? cachedAw.allActivityRecords : [];
const awByDate = cachedAw ? cachedAw.awByDate : {};

const allInternetRecords = await getCachedParsedLog(internetFile, "internet", parseInternetText) || [];
const allFocusRecords = await getCachedParsedLog(focusFile, "focus", parseFocusText) || [];

function toDecimalHour(timeStr) {
    if (!timeStr || typeof timeStr !== "string") return -1;
    const parts = timeStr.trim().split(":");
    if (parts.length < 2) return -1;
    const h = parseInt(parts[0], 10);
    const m = parseInt(parts[1], 10);
    const s = parts.length > 2 ? parseInt(parts[2], 10) : 0;
    return isNaN(h) || isNaN(m) ? -1 : h + m / 60 + (isNaN(s) ? 0 : s / 3600);
}

function toTotalMinutes(timeStr) {
    if (!timeStr || typeof timeStr !== "string") return -1;
    const parts = timeStr.trim().split(":");
    if (parts.length < 2) return -1;
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


const palette = [
    '#38bdf8', '#34d399', '#fbbf24', '#a78bfa',
    '#f472b6', '#4ade80', '#fb923c', '#818cf8',
    '#e879f9', '#2dd4bf', '#f87171', '#60a5fa'
];

const allProjectNames = Array.from(new Set(allRecords.map(r => r.project))).sort();
const projectColorMap = {};
allProjectNames.forEach((proj, idx) => {
    projectColorMap[proj] = palette[idx % palette.length];
});


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


// UI Container & Button Bar
const root = this.container;
root.innerHTML = "";

// 0. Top Navigation Toolbar
const navBar = root.createDiv({ cls: "stopwatch-nav-bar" });
navBar.style.display = "flex";
navBar.style.gap = "8px";
navBar.style.marginBottom = "18px";
navBar.style.padding = "10px 14px";
navBar.style.borderRadius = "8px";
navBar.style.backgroundColor = "var(--background-secondary)";
navBar.style.border = "1px solid var(--background-modifier-border)";
navBar.style.alignItems = "center";
navBar.style.flexWrap = "wrap";

const navLabel = navBar.createEl("span", { text: "🧭 Dashboards:" });
navLabel.style.fontWeight = "bold";
navLabel.style.fontSize = "12px";
navLabel.style.opacity = "0.75";
navLabel.style.marginRight = "4px";

const navLinks = [
    { id: "overview", label: "🏠 Overview", file: "Stopwatch Dashboard" },
    { id: "timeline", label: "🔍 Timeline & Deep Dive", file: "Stopwatch - Timeline & Deep Dive" },
    { id: "network", label: "🌐 Network Performance", file: "Stopwatch - Network Performance" },
    { id: "trends", label: "📈 Activity & Trends", file: "Stopwatch - Activity & Trends" },
    { id: "productivity", label: "🏆 Productivity Scores", file: "Stopwatch - Productivity Scores" },
    { id: "mood", label: "😊 Mood & Feelings", file: "Stopwatch - Mood & Feelings" }
];

navLinks.forEach(item => {
    const isCurrent = ("timeline" === item.id);
    const btn = navBar.createEl("button", { text: item.label });
    btn.style.padding = "5px 12px";
    btn.style.borderRadius = "6px";
    btn.style.fontSize = "12px";
    btn.style.cursor = isCurrent ? "default" : "pointer";
    btn.style.border = isCurrent ? "1px solid var(--interactive-accent)" : "1px solid var(--background-modifier-border)";
    btn.style.backgroundColor = isCurrent ? "var(--interactive-accent)" : "var(--background-primary)";
    btn.style.color = isCurrent ? "var(--text-on-accent)" : "var(--text-normal)";
    btn.style.fontWeight = isCurrent ? "bold" : "normal";

    if (!isCurrent) {
        btn.addEventListener("click", () => {
            app.workspace.openLinkText(item.file, "");
        });
    }
});


const filterBar = root.createDiv({ cls: "stopwatch-filters" });
filterBar.style.display = "flex";
filterBar.style.gap = "8px";
filterBar.style.marginBottom = "18px";
filterBar.style.flexWrap = "wrap";

const chartSection = root.createDiv({ cls: "stopwatch-charts" });

const filters = [
    { id: "today", label: "Today" },
    { id: "week", label: "This Week" },
    { id: "month", label: "This Month" },
    { id: "all", label: "All Time" }
];

let activeFilter = "week";

let zoomStartHour = 0;
let zoomEndHour = 24;

const availableDates = Array.from(new Set([
    ...allRecords.map(r => r.dateStr),
    ...allActivityRecords.map(r => r.dateStr),
    ...allInternetRecords.map(r => r.dateStr),
    ...allFocusRecords.map(r => r.dateStr)
])).sort().reverse();

let selectedDailyDate = availableDates[0] || new Date().toISOString().split("T")[0];

let deepDiveProject = allProjectNames[0] || "__all__";
let deepDiveScope = "day";
let deepDiveDate = availableDates[0] || new Date().toISOString().split("T")[0];
let deepDiveSelectedSessionIdx = -1;
let deepDiveAppFilter = "App";
let deepDiveTimelineOpen = false;

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
        const startOfMonth = new Date(now.getFullYear(), now.getMonth(), 1);
        matchFn = r => r.dateObj >= startOfMonth;
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

    chartSection.innerHTML = "";

    // 1. Dual Synchronized Timeline
    renderDualTimelineSection();

    // 2. Project-by-Project Deep Dive
    renderProjectDeepDiveSection();

    // 3. Focus Continuity & Distraction Analysis
    renderFocusContinuitySection();
}

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

            renderQuickChart({
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

        renderQuickChart({
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

        renderQuickChart({
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

renderDashboard();
```