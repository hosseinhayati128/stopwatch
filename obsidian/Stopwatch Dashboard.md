# ⏱️ Stopwatch & Activity Dashboard

> [!NAV] 🧭 **Stopwatch Dashboards**
> **🏠 Overview** · [[Stopwatch - Timeline & Deep Dive|🔍 Timeline & Sessions]] · [[Stopwatch - Network Performance|🌐 Network]] · [[Stopwatch - Activity & Trends|📈 Activity & Trends]] · [[Stopwatch - Productivity Scores|🏆 Productivity Scores]] · [[Stopwatch - Mood & Feelings|😊 Mood & Feelings]]


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
const scoresFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "project scores.json" || f.name.toLowerCase() === "project-scores.json");

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


// Load Project Scores / Weights (points per hour)
let projectScores = {
    "Apply": 2.0,
    "Gym": 2.0,
    "Drawing": 2.0,
    "Navid": 1.0,
    "Learning": 1.0,
    "Art": 1.0,
    "Arash": 1.0,
    "Programming": 1.0,
    "Read": 1.0,
    "Job Iran": 1.0,
    "Therapy": 1.0,
    "Friends": 0.7,
    "Family": 0.7,
    "basic system problems": 0.5,
    "Routine": 0.5,
    "Social Media": 0.0,
    "Sleep": 0.0
};

if (scoresFile) {
    try {
        const scoresContent = await app.vault.read(scoresFile);
        const parsed = JSON.parse(scoresContent);
        if (parsed && typeof parsed === "object") {
            Object.assign(projectScores, parsed);
        }
    } catch (e) {
        console.error("Error reading Project Scores.json:", e);
    }
} else {
    try {
        if (typeof require !== "undefined") {
            const fs = require("fs");
            const path = require("path");
            const appData = process.env.APPDATA;
            if (appData) {
                const confPath = path.join(appData, "StopwatchOverlay", "project-scores.json");
                if (fs.existsSync(confPath)) {
                    const parsed = JSON.parse(fs.readFileSync(confPath, "utf-8"));
                    if (parsed && typeof parsed === "object") {
                        Object.assign(projectScores, parsed);
                    }
                }
            }
        }
    } catch (e) {
        // Fallback
    }
}

async function saveProjectScores() {
    const jsonStr = JSON.stringify(projectScores, null, 2);
    let sFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "project scores.json" || f.name.toLowerCase() === "project-scores.json");
    if (sFile) {
        await app.vault.modify(sFile, jsonStr);
    } else {
        const folder = stopwatchFile.parent ? stopwatchFile.parent.path : "";
        const targetPath = folder ? `${folder}/Project Scores.json` : "Project Scores.json";
        await app.vault.create(targetPath, jsonStr);
    }
}

function getProjectScore(projectName) {
    if (!projectName) return 1.0;
    if (projectScores[projectName] !== undefined) {
        return projectScores[projectName];
    }
    const lower = projectName.toLowerCase();
    for (const [k, v] of Object.entries(projectScores)) {
        if (k.toLowerCase() === lower) return v;
    }
    return 1.0;
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
    const isCurrent = ("overview" === item.id);
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

let activeFilter = "week";

const availableDates = Array.from(new Set([
    ...allRecords.map(r => r.dateStr),
    ...allActivityRecords.map(r => r.dateStr),
    ...allInternetRecords.map(r => r.dateStr),
    ...allFocusRecords.map(r => r.dateStr)
])).sort().reverse();

let timeByProjectScope = "week";
let timeByProjectDate = availableDates[0] || new Date().toISOString().split("T")[0];

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

    let topProject = "-";
    let maxMin = 0;
    for (const [proj, min] of Object.entries(projectTotals)) {
        if (min > maxMin) {
            maxMin = min;
            topProject = proj;
        }
    }

    // In-Timer overlap calculation (Optimized Date-Indexed)
    let focusedMinDuringTimers = 0;
    let distractedMinDuringTimers = 0;

    for (let i = 0; i < records.length; i++) {
        const sw = records[i];
        const dayAws = awByDate[sw.dateStr];
        if (!dayAws) continue;
        const swStart = sw.startHour;
        const swEnd = sw.endHour;

        for (let j = 0; j < dayAws.length; j++) {
            const aw = dayAws[j];
            const overlap = Math.max(0, Math.min(swEnd, aw.endHour) - Math.max(swStart, aw.startHour));
            if (overlap > 0) {
                const overlapMin = Math.round(overlap * 60);
                if (isDistraction(aw.activity, aw.details, sw.project)) {
                    distractedMinDuringTimers += overlapMin;
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


    // Render Quick Launch Hub Cards
    const hubGrid = chartSection.createDiv();
    hubGrid.style.display = "grid";
    hubGrid.style.gridTemplateColumns = "repeat(auto-fit, minmax(240px, 1fr))";
    hubGrid.style.gap = "14px";
    hubGrid.style.marginBottom = "28px";

    const hubCards = [
        {
            title: "🔍 Timeline & Deep Dive",
            desc: "Dual synchronized timeline, project drill-down & distraction pauses.",
            file: "Stopwatch - Timeline & Deep Dive",
            color: "#8b5cf6",
            badge: `${sessionCount} sessions`
        },
        {
            title: "🌐 Network Performance",
            desc: "Bandwidth speed, ping latency, 24h hourly profiles & connection logs.",
            file: "Stopwatch - Network Performance",
            color: "#38bdf8",
            badge: avgNetSpeed ? `${avgNetSpeed} Mbps` : "Telemetry"
        },
        {
            title: "📈 Activity & Trends",
            desc: "Time-of-day distribution & interactive daily/weekly/monthly project trends.",
            file: "Stopwatch - Activity & Trends",
            color: "#10b981",
            badge: `${totalHours} hrs`
        },
        {
            title: "🏆 Productivity Scores",
            desc: "Point trends, weekly 3-slot schedules & time-of-day score distribution.",
            file: "Stopwatch - Productivity Scores",
            color: "#f59e0b",
            badge: "Points & Slots"
        },
        {
            title: "😊 Mood & Feelings",
            desc: "Daily mood shifts across time slots, 3-column analysis & candlesticks.",
            file: "Stopwatch - Mood & Feelings",
            color: "#ec4899",
            badge: "Well-being"
        }
    ];

    hubCards.forEach(c => {
        const cardEl = hubGrid.createDiv();
        cardEl.style.padding = "14px 16px";
        cardEl.style.borderRadius = "8px";
        cardEl.style.backgroundColor = "var(--background-secondary)";
        cardEl.style.border = "1px solid var(--background-modifier-border)";
        cardEl.style.cursor = "pointer";
        cardEl.style.transition = "transform 0.15s ease, border-color 0.15s ease";
        cardEl.style.display = "flex";
        cardEl.style.flexDirection = "column";
        cardEl.style.justifyContent = "space-between";

        cardEl.addEventListener("mouseenter", () => {
            cardEl.style.transform = "translateY(-2px)";
            cardEl.style.borderColor = c.color;
        });
        cardEl.addEventListener("mouseleave", () => {
            cardEl.style.transform = "translateY(0)";
            cardEl.style.borderColor = "var(--background-modifier-border)";
        });
        cardEl.addEventListener("click", () => {
            app.workspace.openLinkText(c.file, "");
        });

        const topRow = cardEl.createDiv();
        topRow.style.display = "flex";
        topRow.style.justifyContent = "space-between";
        topRow.style.alignItems = "center";
        topRow.style.marginBottom = "6px";

        const titleEl = topRow.createDiv({ text: c.title });
        titleEl.style.fontWeight = "bold";
        titleEl.style.fontSize = "13px";
        titleEl.style.color = "var(--text-normal)";

        const badgeEl = topRow.createDiv({ text: c.badge });
        badgeEl.style.fontSize = "10px";
        badgeEl.style.padding = "2px 6px";
        badgeEl.style.borderRadius = "4px";
        badgeEl.style.backgroundColor = "var(--background-primary)";
        badgeEl.style.color = c.color;
        badgeEl.style.fontWeight = "bold";

        const descEl = cardEl.createDiv({ text: c.desc });
        descEl.style.fontSize = "11px";
        descEl.style.opacity = "0.7";
        descEl.style.lineHeight = "1.4";
    });


    // Render Time by Project (Doughnut + Breakdown)
    renderTimeByProjectSection();
}

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

        renderQuickChart({
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