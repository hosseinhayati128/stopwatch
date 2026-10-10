# 🏆 Stopwatch — Productivity Scores & Patterns

> [!NAV] 🧭 **Stopwatch Dashboards**
> [[Stopwatch Dashboard|🏠 Overview]] · [[Stopwatch - Timeline & Deep Dive|🔍 Timeline & Sessions]] · [[Stopwatch - Network Performance|🌐 Network]] · [[Stopwatch - Activity & Trends|📈 Activity & Trends]] · **🏆 Productivity Scores** · [[Stopwatch - Mood & Feelings|😊 Mood & Feelings]]


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
// 1. Locate and Parse Log Files & Project Scores
// ==========================================
const stopwatchFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "stopwatch log.md");
const scoresFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "project scores.json" || f.name.toLowerCase() === "project-scores.json");

if (!stopwatchFile) {
    dv.paragraph("⚠️ *Could not find `Stopwatch Log.md` in your vault.*");
    return;
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

const rawSw = await getCachedParsedLog(stopwatchFile, "stopwatch_raw", parseStopwatchText) || [];
const allRecords = rawSw.map(r => {
    const scorePerHour = (typeof getProjectScore === "function") ? getProjectScore(r.project) : 1.0;
    return {
        ...r,
        scorePerHour,
        score: (r.minutes / 60) * scorePerHour
    };
});

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


// UI Container
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
    const isCurrent = ("productivity" === item.id);
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


const chartSection = root.createDiv({ cls: "stopwatch-charts" });

let scoreGranularity = "day";
let scoreWindowSize = 15;
let scoreOffset = 0;
let scoreDisplayMode = "stacked";
let scoreWheelMode = "pan";
let scoreConfigOpen = false;

let slotScope = "week";
let slotWeekOffset = 0;
let slotMonthOffset = 0;
let slotWeekStart = "mon";
let slotAverageMode = "all_days";

let dayScoreChartForm = "3col";
let dayScoreWindowSize = 14;
let dayScoreOffset = 0;
let dayScoreWheelMode = "pan";
let dayScoreTableOpen = true;

function renderDashboard() {
    chartSection.innerHTML = "";

    // 1. Daily Productivity Score Trend
    renderScoreTrendSection();

    // 2. Weekly Productivity Pattern by Time Slot
    renderWeeklyTimeSlotSection();

    // 3. Daily Productivity Score by Time Slot
    renderDailyScoreTimeSlotSection();
}

function renderScoreTrendSection() {
    if (allRecords.length === 0) return;

    const card = chartSection.createDiv();
    card.style.padding = "18px";
    card.style.borderRadius = "8px";
    card.style.backgroundColor = "var(--background-secondary)";
    card.style.border = "1px solid var(--background-modifier-border)";
    card.style.marginTop = "32px";
    card.style.marginBottom = "30px";
    card.style.position = "relative";

    // 1. Periods generator
    function getAllScorePeriods(granularity) {
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
                    fullLabel,
                    daysCount: 1
                });
                cur.setDate(cur.getDate() + 1);
            }
        } else if (granularity === "week") {
            function getMonday(d) {
                const date = new Date(d);
                const day = (date.getDay() + 6) % 7;
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
                
                let daysInPeriod = 7;
                if (todayMidnight >= cur.getTime() && todayMidnight <= sun.getTime()) {
                    daysInPeriod = Math.max(1, Math.min(7, Math.floor((todayMidnight - cur.getTime()) / (1000 * 60 * 60 * 24)) + 1));
                }

                periods.push({
                    key,
                    label,
                    fullLabel,
                    daysCount: daysInPeriod,
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
                
                const daysInMonth = new Date(y, m + 1, 0).getDate();
                let daysInPeriod = daysInMonth;
                if (now.getFullYear() === y && now.getMonth() === m) {
                    daysInPeriod = Math.max(1, now.getDate());
                }

                periods.push({
                    key,
                    label,
                    fullLabel,
                    year: y,
                    month: m,
                    daysCount: daysInPeriod
                });
                cur.setMonth(cur.getMonth() + 1);
            }
        }

        return periods;
    }

    // 2. Pre-aggregate score points per period
    function aggregateScoreData(periods, granularity) {
        const periodProjectScores = {};
        const periodTotalMinutes = {};
        for (const p of periods) {
            periodProjectScores[p.key] = {};
            periodTotalMinutes[p.key] = {};
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

            if (key && periodProjectScores[key]) {
                const sph = getProjectScore(r.project);
                const pts = (r.minutes / 60) * sph;
                periodProjectScores[key][r.project] = (periodProjectScores[key][r.project] || 0) + pts;
                periodTotalMinutes[key][r.project] = (periodTotalMinutes[key][r.project] || 0) + r.minutes;
            }
        }

        return { periodProjectScores, periodTotalMinutes };
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

    // Top Controls
    const topControls = headerRow.createDiv();
    topControls.style.display = "flex";
    topControls.style.gap = "6px";
    topControls.style.alignItems = "center";
    topControls.style.flexWrap = "wrap";

    // Granularity Tabs
    const granOpts = [
        { id: "day", label: "📅 Days", defaultSize: 15 },
        { id: "week", label: "📆 Weeks (Mean/Day)", defaultSize: 8 },
        { id: "month", label: "🗓️ Months (Mean/Day)", defaultSize: 6 }
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
            if (scoreGranularity !== opt.id) {
                scoreGranularity = opt.id;
                scoreWindowSize = opt.defaultSize;
                scoreOffset = 0;
                renderView();
            }
        });
    });

    // Display Mode Button (Stacked by project vs Unified total)
    const displayModeBtn = topControls.createEl("button");
    displayModeBtn.style.padding = "5px 12px";
    displayModeBtn.style.fontSize = "12px";
    displayModeBtn.style.borderRadius = "4px";
    displayModeBtn.style.border = "1px solid var(--background-modifier-border)";
    displayModeBtn.style.cursor = "pointer";
    displayModeBtn.title = "Toggle stacked project breakdown or single total score bar";
    displayModeBtn.addEventListener("click", () => {
        scoreDisplayMode = (scoreDisplayMode === "stacked") ? "total" : "stacked";
        updateDisplayModeBtn();
        renderView();
    });

    function updateDisplayModeBtn() {
        if (scoreDisplayMode === "stacked") {
            displayModeBtn.textContent = "🥞 Stacked Projects";
            displayModeBtn.style.backgroundColor = "var(--interactive-accent)";
            displayModeBtn.style.color = "var(--text-on-accent)";
            displayModeBtn.style.fontWeight = "bold";
        } else {
            displayModeBtn.textContent = "📊 Total Score Bar";
            displayModeBtn.style.backgroundColor = "var(--background-modifier-form-field)";
            displayModeBtn.style.color = "var(--text-normal)";
            displayModeBtn.style.fontWeight = "normal";
        }
    }
    updateDisplayModeBtn();

    // Wheel Mode Button
    const wheelModeBtn = topControls.createEl("button");
    wheelModeBtn.style.padding = "5px 12px";
    wheelModeBtn.style.fontSize = "12px";
    wheelModeBtn.style.borderRadius = "4px";
    wheelModeBtn.style.border = "1px solid var(--background-modifier-border)";
    wheelModeBtn.style.cursor = "pointer";
    wheelModeBtn.title = "Toggle whether mouse wheel on the chart scrolls through time or zooms window size";
    wheelModeBtn.addEventListener("click", () => {
        scoreWheelMode = (scoreWheelMode === "pan") ? "zoom" : "pan";
        updateWheelModeBtn();
    });

    function updateWheelModeBtn() {
        if (scoreWheelMode === "pan") {
            wheelModeBtn.textContent = "🖱️ Wheel: ↔️ Pan";
            wheelModeBtn.style.backgroundColor = "var(--background-modifier-form-field)";
            wheelModeBtn.style.color = "var(--text-normal)";
            wheelModeBtn.style.fontWeight = "normal";
        } else {
            wheelModeBtn.textContent = "🖱️ Wheel: 🔍 Zoom";
            wheelModeBtn.style.backgroundColor = "var(--interactive-accent)";
            wheelModeBtn.style.color = "var(--text-on-accent)";
            wheelModeBtn.style.fontWeight = "bold";
        }
    }
    updateWheelModeBtn();

    // Project Scores Config Button
    const configBtn = topControls.createEl("button", { text: "⚙️ Project Scores" });
    configBtn.style.padding = "5px 12px";
    configBtn.style.fontSize = "12px";
    configBtn.style.borderRadius = "4px";
    configBtn.style.border = "1px solid var(--background-modifier-border)";
    configBtn.style.cursor = "pointer";
    configBtn.title = "View or customize the score weight (points per hour) for each project";

    // Collapsible Score Weights Config Panel
    const configPanel = card.createDiv();
    configPanel.style.display = "none";
    configPanel.style.padding = "14px";
    configPanel.style.marginTop = "10px";
    configPanel.style.marginBottom = "14px";
    configPanel.style.borderRadius = "6px";
    configPanel.style.backgroundColor = "var(--background-primary)";
    configPanel.style.border = "1px solid var(--background-modifier-border)";

    configBtn.addEventListener("click", () => {
        scoreConfigOpen = !scoreConfigOpen;
        configPanel.style.display = scoreConfigOpen ? "block" : "none";
        configBtn.style.backgroundColor = scoreConfigOpen ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
        configBtn.style.color = scoreConfigOpen ? "var(--text-on-accent)" : "var(--text-normal)";
        if (scoreConfigOpen) renderConfigPanel();
    });

    function renderConfigPanel() {
        configPanel.innerHTML = "";
        const cHeader = configPanel.createDiv();
        cHeader.style.display = "flex";
        cHeader.style.justifyContent = "space-between";
        cHeader.style.alignItems = "center";
        cHeader.style.marginBottom = "10px";

        const cTitle = cHeader.createEl("div", { text: "⚖️ Project Score Weights (Points earned per hour of tracked time)" });
        cTitle.style.fontWeight = "bold";
        cTitle.style.fontSize = "13px";

        const saveBtn = cHeader.createEl("button", { text: "💾 Save Scores to Vault" });
        saveBtn.style.padding = "4px 12px";
        saveBtn.style.fontSize = "12px";
        saveBtn.style.borderRadius = "4px";
        saveBtn.style.backgroundColor = "var(--interactive-accent)";
        saveBtn.style.color = "var(--text-on-accent)";
        saveBtn.style.border = "none";
        saveBtn.style.cursor = "pointer";

        const grid = configPanel.createDiv();
        grid.style.display = "grid";
        grid.style.gridTemplateColumns = "repeat(auto-fill, minmax(180px, 1fr))";
        grid.style.gap = "8px";

        const inputs = {};
        for (const proj of allProjectNames) {
            const item = grid.createDiv();
            item.style.display = "flex";
            item.style.alignItems = "center";
            item.style.justifyContent = "space-between";
            item.style.padding = "4px 8px";
            item.style.borderRadius = "4px";
            item.style.backgroundColor = "var(--background-secondary)";

            const lbl = item.createEl("span", { text: proj });
            lbl.style.fontSize = "12px";
            lbl.style.fontWeight = "500";
            lbl.style.overflow = "hidden";
            lbl.style.textOverflow = "ellipsis";
            lbl.style.whiteSpace = "nowrap";
            lbl.style.maxWidth = "110px";

            const inp = item.createEl("input", { type: "number" });
            inp.value = String(getProjectScore(proj));
            inp.step = "0.1";
            inp.min = "0";
            inp.style.width = "52px";
            inp.style.padding = "2px 4px";
            inp.style.fontSize = "12px";
            inp.style.textAlign = "right";
            inp.style.borderRadius = "3px";
            inp.style.border = "1px solid var(--background-modifier-border)";
            inputs[proj] = inp;
        }

        saveBtn.addEventListener("click", async () => {
            for (const [proj, inp] of Object.entries(inputs)) {
                const val = parseFloat(inp.value);
                if (!isNaN(val) && val >= 0) {
                    projectScores[proj] = val;
                }
            }
            // Update records
            for (const r of allRecords) {
                r.scorePerHour = getProjectScore(r.project);
                r.score = (r.minutes / 60) * r.scorePerHour;
            }
            await saveProjectScores();
            saveBtn.textContent = "✅ Saved!";
            setTimeout(() => {
                saveBtn.textContent = "💾 Save Scores to Vault";
                renderDashboard();
            }, 600);
        });
    }

    // KPI Summary Bar
    const kpiBar = card.createDiv();
    kpiBar.style.display = "grid";
    kpiBar.style.gridTemplateColumns = "repeat(auto-fit, minmax(130px, 1fr))";
    kpiBar.style.gap = "8px";
    kpiBar.style.marginBottom = "12px";

    // Toolbar Row: Navigation, Window Status, Presets
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

    // Presets group (Right)
    const presetsGroup = toolbarRow.createDiv();
    presetsGroup.style.display = "flex";
    presetsGroup.style.gap = "4px";
    presetsGroup.style.alignItems = "center";

    const presetLabel = presetsGroup.createEl("span", { text: "Show: " });
    presetLabel.style.fontSize = "11px";
    presetLabel.style.opacity = "0.7";

    const presetsContainer = presetsGroup.createDiv();
    presetsContainer.style.display = "flex";
    presetsContainer.style.gap = "3px";

    // Chart container with floating buttons
    const chartWrapper = card.createDiv();
    chartWrapper.style.position = "relative";

    const floatLeft = chartWrapper.createEl("button", { text: "◀" });
    floatLeft.title = "Scroll back in history";
    floatLeft.style.position = "absolute";
    floatLeft.style.left = "4px";
    floatLeft.style.top = "50%";
    floatLeft.style.transform = "translateY(-50%)";
    floatLeft.style.zIndex = "10";
    floatLeft.style.width = "28px";
    floatLeft.style.height = "42px";
    floatLeft.style.borderRadius = "4px";
    floatLeft.style.border = "1px solid var(--background-modifier-border)";
    floatLeft.style.backgroundColor = "var(--background-secondary)";
    floatLeft.style.cursor = "pointer";
    floatLeft.style.opacity = "0.5";
    floatLeft.style.transition = "opacity 0.15s";
    floatLeft.addEventListener("mouseenter", () => floatLeft.style.opacity = "1");
    floatLeft.addEventListener("mouseleave", () => floatLeft.style.opacity = "0.5");

    const floatRight = chartWrapper.createEl("button", { text: "▶" });
    floatRight.title = "Scroll forward in history";
    floatRight.style.position = "absolute";
    floatRight.style.right = "4px";
    floatRight.style.top = "50%";
    floatRight.style.transform = "translateY(-50%)";
    floatRight.style.zIndex = "10";
    floatRight.style.width = "28px";
    floatRight.style.height = "42px";
    floatRight.style.borderRadius = "4px";
    floatRight.style.border = "1px solid var(--background-modifier-border)";
    floatRight.style.backgroundColor = "var(--background-secondary)";
    floatRight.style.cursor = "pointer";
    floatRight.style.opacity = "0.5";
    floatRight.style.transition = "opacity 0.15s";
    floatRight.addEventListener("mouseenter", () => floatRight.style.opacity = "1");
    floatRight.addEventListener("mouseleave", () => floatRight.style.opacity = "0.5");

    const chartDiv = chartWrapper.createDiv();

    function getStep() {
        if (scoreGranularity === "day") {
            if (scoreWindowSize === 7) return 7;
            if (scoreWindowSize === 15) return 15;
            return 7;
        }
        if (scoreGranularity === "week") return 4;
        if (scoreGranularity === "month") return 3;
        return 1;
    }

    function renderView() {
        // 1. Update Granularity Button styles
        granBtns.forEach(({ id, btn }) => {
            if (id === scoreGranularity) {
                btn.style.backgroundColor = "var(--interactive-accent)";
                btn.style.color = "var(--text-on-accent)";
                btn.style.fontWeight = "bold";
            } else {
                btn.style.backgroundColor = "var(--background-modifier-form-field)";
                btn.style.color = "var(--text-normal)";
                btn.style.fontWeight = "normal";
            }
        });

        // 2. Dynamic Title & Subtitle
        if (scoreGranularity === "day") {
            headingEl.textContent = "🏆 Daily Productivity Score Trend";
            subtitleEl.textContent = "Total score points earned each day (Points = Duration in hours × Score weight/hr). Options: last 7 or 15 days.";
        } else if (scoreGranularity === "week") {
            headingEl.textContent = "🏆 Weekly Mean Productivity Score";
            subtitleEl.textContent = "Mean daily score earned per week (Points/day = Total weekly points / 7 days). Compare across past weeks.";
        } else {
            headingEl.textContent = "🏆 Monthly Mean Productivity Score";
            subtitleEl.textContent = "Mean daily score earned per month (Points/day = Total monthly points / Days in month). Compare across past months.";
        }

        // 3. Rebuild Presets for current granularity
        presetsContainer.innerHTML = "";
        let presetValues = [];
        if (scoreGranularity === "day") {
            presetValues = [
                { id: 7, label: "7 Days" },
                { id: 15, label: "15 Days" },
                { id: 30, label: "30 Days" },
                { id: "all", label: "All" }
            ];
        } else if (scoreGranularity === "week") {
            presetValues = [
                { id: 4, label: "4 W" },
                { id: 8, label: "8 W" },
                { id: 12, label: "12 W" },
                { id: "all", label: "All" }
            ];
        } else {
            presetValues = [
                { id: 3, label: "3 M" },
                { id: 6, label: "6 M" },
                { id: 12, label: "12 M" },
                { id: "all", label: "All" }
            ];
        }

        presetValues.forEach(pv => {
            const pBtn = presetsContainer.createEl("button", { text: pv.label });
            pBtn.style.padding = "3px 8px";
            pBtn.style.fontSize = "11px";
            pBtn.style.borderRadius = "3px";
            pBtn.style.cursor = "pointer";
            pBtn.style.border = "1px solid var(--background-modifier-border)";

            if (scoreWindowSize === pv.id) {
                pBtn.style.backgroundColor = "var(--interactive-accent)";
                pBtn.style.color = "var(--text-on-accent)";
                pBtn.style.fontWeight = "bold";
            } else {
                pBtn.style.backgroundColor = "var(--background-modifier-form-field)";
                pBtn.style.color = "var(--text-normal)";
            }

            pBtn.addEventListener("click", () => {
                scoreWindowSize = pv.id;
                scoreOffset = 0;
                renderView();
            });
        });

        // 4. Calculate Periods & Data
        const allPeriods = getAllScorePeriods(scoreGranularity);
        const { periodProjectScores, periodTotalMinutes } = aggregateScoreData(allPeriods, scoreGranularity);
        const total = allPeriods.length;

        // Effective window
        const effectiveSize = (scoreWindowSize === "all") ? total : Math.min(scoreWindowSize, total);
        const maxOffset = Math.max(0, total - effectiveSize);
        if (scoreOffset > maxOffset) scoreOffset = maxOffset;
        if (scoreOffset < 0) scoreOffset = 0;

        const startIdx = Math.max(0, total - effectiveSize - scoreOffset);
        const endIdx = startIdx + effectiveSize;
        const visiblePeriods = allPeriods.slice(startIdx, endIdx);

        // 5. Update Navigation Buttons
        const isAtOldest = (scoreOffset >= maxOffset);
        const isAtLatest = (scoreOffset === 0);

        oldestBtn.disabled = isAtOldest;
        prevBtn.disabled = isAtOldest;
        floatLeft.disabled = isAtOldest;
        oldestBtn.style.opacity = isAtOldest ? "0.4" : "1";
        prevBtn.style.opacity = isAtOldest ? "0.4" : "1";
        floatLeft.style.opacity = isAtOldest ? "0.2" : "0.5";

        nextBtn.disabled = isAtLatest;
        latestBtn.disabled = isAtLatest;
        floatRight.disabled = isAtLatest;
        nextBtn.style.opacity = isAtLatest ? "0.4" : "1";
        latestBtn.style.opacity = isAtLatest ? "0.4" : "1";
        floatRight.style.opacity = isAtLatest ? "0.2" : "0.5";

        // 6. Calculate Summary Metrics for visible window
        let windowTotalScore = 0;
        let windowTotalDays = 0;
        let activePeriodsCount = 0;
        let peakScore = 0;
        let peakLabel = "-";

        visiblePeriods.forEach(p => {
            const pScores = periodProjectScores[p.key] || {};
            let periodTotalPts = 0;
            for (const pts of Object.values(pScores)) {
                periodTotalPts += pts;
            }
            windowTotalScore += periodTotalPts;
            windowTotalDays += p.daysCount;
            if (periodTotalPts > 0) activePeriodsCount++;
            if (periodTotalPts > peakScore) {
                peakScore = periodTotalPts;
                peakLabel = p.label;
            }
        });

        const windowDailyMean = windowTotalDays > 0 ? (windowTotalScore / windowTotalDays).toFixed(2) : "0.0";

        // 7. Update Window Badge
        if (visiblePeriods.length > 0) {
            const firstP = visiblePeriods[0];
            const lastP = visiblePeriods[visiblePeriods.length - 1];
            badgeEl.textContent = `${firstP.label} – ${lastP.label} (${visiblePeriods.length} ${scoreGranularity === 'day' ? 'days' : (scoreGranularity === 'week' ? 'weeks' : 'months')}) • Mean: ${windowDailyMean} pts/day • Total: ${windowTotalScore.toFixed(1)} pts`;
        } else {
            badgeEl.textContent = "No data in range";
        }

        // 8. Render Mini KPI Cards
        kpiBar.innerHTML = `
            <div style="background: var(--background-primary); border: 1px solid var(--background-modifier-border); border-radius: 6px; padding: 10px; text-align: center;">
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">Period Total</div>
                <div style="font-size: 18px; font-weight: bold; color: #8b5cf6;">${windowTotalScore.toFixed(1)} <span style="font-size: 11px;">pts</span></div>
            </div>
            <div style="background: var(--background-primary); border: 1px solid var(--background-modifier-border); border-radius: 6px; padding: 10px; text-align: center;">
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">Daily Mean</div>
                <div style="font-size: 18px; font-weight: bold; color: #34d399;">${windowDailyMean} <span style="font-size: 11px;">pts/day</span></div>
            </div>
            <div style="background: var(--background-primary); border: 1px solid var(--background-modifier-border); border-radius: 6px; padding: 10px; text-align: center;">
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">Peak Period</div>
                <div style="font-size: 18px; font-weight: bold; color: #fbbf24;">${peakScore > 0 ? peakScore.toFixed(1) + ' pts' : '-'} <span style="font-size: 11px; opacity: 0.7;">(${peakLabel})</span></div>
            </div>
            <div style="background: var(--background-primary); border: 1px solid var(--background-modifier-border); border-radius: 6px; padding: 10px; text-align: center;">
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">Active Periods</div>
                <div style="font-size: 18px; font-weight: bold; color: var(--text-accent);">${activePeriodsCount} <span style="font-size: 11px; opacity: 0.7;">/ ${visiblePeriods.length}</span></div>
            </div>
        `;

        // 9. Build Chart.js Datasets
        let datasets = [];

        if (scoreDisplayMode === "stacked") {
            const activeProjectsInWindow = allProjectNames.filter(proj => {
                return visiblePeriods.some(p => {
                    const sc = periodProjectScores[p.key]?.[proj] || 0;
                    return sc > 0;
                });
            });

            datasets = activeProjectsInWindow.map(proj => {
                const dataValues = visiblePeriods.map(p => {
                    const rawPts = periodProjectScores[p.key]?.[proj] || 0;
                    if (scoreGranularity === "day") {
                        return +(rawPts.toFixed(2));
                    } else {
                        // Mean daily points from this project for this period
                        return +((rawPts / p.daysCount).toFixed(2));
                    }
                });

                return {
                    label: proj,
                    data: dataValues,
                    backgroundColor: projectColorMap[proj] || "#38bdf8",
                    stack: 'scoreStack'
                };
            });
        } else {
            // Unified total bar
            const totalData = visiblePeriods.map(p => {
                const pScores = periodProjectScores[p.key] || {};
                let sum = 0;
                for (const pts of Object.values(pScores)) sum += pts;
                if (scoreGranularity === "day") {
                    return +(sum.toFixed(2));
                } else {
                    return +((sum / p.daysCount).toFixed(2));
                }
            });

            datasets = [{
                label: scoreGranularity === "day" ? "Total Score (pts)" : "Mean Score (pts/day)",
                data: totalData,
                backgroundColor: 'rgba(139, 92, 246, 0.85)',
                borderColor: '#7c3aed',
                borderWidth: 1,
                borderRadius: 4
            }];
        }

        // 10. Render Chart
        chartDiv.innerHTML = "";
        renderQuickChart({
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
                                if (scoreDisplayMode === "stacked") {
                                    return ` ${item.dataset.label}: ${item.raw} pts${scoreGranularity !== 'day' ? '/day' : ''}`;
                                } else {
                                    return ` Score: ${item.raw} pts${scoreGranularity !== 'day' ? '/day' : ''}`;
                                }
                            },
                            footer: function(items) {
                                let tot = 0;
                                items.forEach(it => { tot += (it.raw || 0); });
                                const idx = items[0]?.dataIndex;
                                const p = visiblePeriods[idx];
                                if (!p) return `Total: ${tot.toFixed(2)} pts`;

                                if (scoreGranularity === "day") {
                                    return `Day Total: ${tot.toFixed(2)} pts`;
                                } else {
                                    const pScores = periodProjectScores[p.key] || {};
                                    let actualTotal = 0;
                                    for (const v of Object.values(pScores)) actualTotal += v;
                                    return `Period Total: ${actualTotal.toFixed(1)} pts • Daily Mean: ${tot.toFixed(2)} pts/day (${p.daysCount} days)`;
                                }
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        stacked: scoreDisplayMode === "stacked",
                        title: {
                            display: true,
                            text: scoreGranularity === 'day' ? 'Date' : (scoreGranularity === 'week' ? 'Week' : 'Month'),
                            font: { size: 12, weight: 'bold' }
                        }
                    },
                    y: {
                        stacked: scoreDisplayMode === "stacked",
                        beginAtZero: true,
                        title: {
                            display: true,
                            text: scoreGranularity === 'day' ? 'Score Points (pts)' : 'Mean Score / Day (pts)',
                            font: { size: 12, weight: 'bold' }
                        }
                    }
                }
            }
        }, chartDiv);
    }

    // Event listeners for score trend navigation
    oldestBtn.addEventListener("click", () => {
        const allP = getAllScorePeriods(scoreGranularity);
        const eff = (scoreWindowSize === "all") ? allP.length : Math.min(scoreWindowSize, allP.length);
        scoreOffset = Math.max(0, allP.length - eff);
        renderView();
    });

    prevBtn.addEventListener("click", () => {
        const allP = getAllScorePeriods(scoreGranularity);
        const eff = (scoreWindowSize === "all") ? allP.length : Math.min(scoreWindowSize, allP.length);
        const maxO = Math.max(0, allP.length - eff);
        scoreOffset = Math.min(maxO, scoreOffset + getStep());
        renderView();
    });

    floatLeft.addEventListener("click", () => {
        const allP = getAllScorePeriods(scoreGranularity);
        const eff = (scoreWindowSize === "all") ? allP.length : Math.min(scoreWindowSize, allP.length);
        const maxO = Math.max(0, allP.length - eff);
        scoreOffset = Math.min(maxO, scoreOffset + getStep());
        renderView();
    });

    nextBtn.addEventListener("click", () => {
        scoreOffset = Math.max(0, scoreOffset - getStep());
        renderView();
    });

    floatRight.addEventListener("click", () => {
        scoreOffset = Math.max(0, scoreOffset - getStep());
        renderView();
    });

    latestBtn.addEventListener("click", () => {
        scoreOffset = 0;
        renderView();
    });

    // Wheel navigation (pan/zoom)
    chartWrapper.addEventListener("wheel", (e) => {
        e.preventDefault();
        const allP = getAllScorePeriods(scoreGranularity);
        const total = allP.length;
        if (total === 0) return;

        const isZoom = (scoreWheelMode === "zoom" && !e.shiftKey) || e.ctrlKey || e.metaKey;

        if (isZoom) {
            let cur = (scoreWindowSize === "all") ? total : scoreWindowSize;
            const step = 1;
            if (e.deltaY < 0) {
                scoreWindowSize = Math.max(3, cur - step);
            } else {
                scoreWindowSize = Math.min(total, cur + step);
            }
        } else {
            const delta = Math.abs(e.deltaX) > Math.abs(e.deltaY) ? e.deltaX : e.deltaY;
            const step = 1;
            let eff = (scoreWindowSize === "all") ? total : Math.min(scoreWindowSize, total);
            const maxO = Math.max(0, total - eff);

            if (delta < 0) {
                scoreOffset = Math.min(maxO, scoreOffset + step);
            } else {
                scoreOffset = Math.max(0, scoreOffset - step);
            }
        }

        renderView();
    }, { passive: false });

    // Initial render of Score Trend
    renderView();
}


function renderWeeklyTimeSlotSection() {
    if (allRecords.length === 0) return;

    const card = chartSection.createDiv();
    card.style.padding = "18px";
    card.style.borderRadius = "8px";
    card.style.backgroundColor = "var(--background-secondary)";
    card.style.border = "1px solid var(--background-modifier-border)";
    card.style.marginTop = "32px";
    card.style.marginBottom = "30px";
    card.style.position = "relative";

    // 1. Build all available weeks and months in history
    function getAllAvailableWeeks() {
        const weeks = [];
        const seen = new Set();
        const mNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

        function getMonday(d) {
            const date = new Date(d);
            const day = (date.getDay() + 6) % 7;
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
        if (todayMidnight > maxTime) maxTime = todayMidnight;

        const cur = getMonday(minTime);
        const end = getMonday(maxTime);

        while (cur <= end) {
            const sun = new Date(cur);
            sun.setDate(cur.getDate() + 6);
            sun.setHours(23, 59, 59, 999);
            const wNum = getWeekNum(cur);
            const key = `${cur.getFullYear()}-W${String(wNum).padStart(2, '0')}`;
            if (!seen.has(key)) {
                seen.add(key);
                const sStr = `${cur.getFullYear()}-${String(cur.getMonth() + 1).padStart(2, '0')}-${String(cur.getDate()).padStart(2, '0')}`;
                const eStr = `${sun.getFullYear()}-${String(sun.getMonth() + 1).padStart(2, '0')}-${String(sun.getDate()).padStart(2, '0')}`;
                weeks.push({
                    key,
                    label: `Week ${wNum} (${mNames[cur.getMonth()]} ${cur.getDate()} – ${mNames[sun.getMonth()]} ${sun.getDate()})`,
                    shortLabel: `W${wNum}`,
                    startDate: new Date(cur),
                    endDate: new Date(sun),
                    startDateStr: sStr,
                    endDateStr: eStr
                });
            }
            cur.setDate(cur.getDate() + 7);
        }
        return weeks;
    }

    function getAllAvailableMonths() {
        const months = [];
        const seen = new Set();
        const mNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

        for (const r of allRecords) {
            if (r.dateStr) {
                const mKey = r.dateStr.slice(0, 7);
                if (!seen.has(mKey)) {
                    seen.add(mKey);
                    const [y, m] = mKey.split("-").map(Number);
                    const sDate = new Date(y, m - 1, 1, 0, 0, 0, 0);
                    const eDate = new Date(y, m, 0, 23, 59, 59, 999);
                    months.push({
                        key: mKey,
                        label: `${mNames[m - 1]} ${y}`,
                        shortLabel: mKey,
                        year: y,
                        month: m,
                        startDate: sDate,
                        endDate: eDate
                    });
                }
            }
        }
        // Ensure current month is present
        const now = new Date();
        const curMKey = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
        if (!seen.has(curMKey)) {
            const y = now.getFullYear();
            const m = now.getMonth() + 1;
            const sDate = new Date(y, m - 1, 1, 0, 0, 0, 0);
            const eDate = new Date(y, m, 0, 23, 59, 59, 999);
            months.push({
                key: curMKey,
                label: `${mNames[m - 1]} ${y}`,
                shortLabel: curMKey,
                year: y,
                month: m,
                startDate: sDate,
                endDate: eDate
            });
        }
        return months.sort((a, b) => a.key.localeCompare(b.key));
    }

    // 2. Pre-calculate time slot scores for each date
    // Daily Waking Cycle:
    // Slot 1: 06:00 – 12:00 (Morning)
    // Slot 2: 12:00 – 18:00 (Afternoon)
    // Slot 3: 18:00 – 02:00 (Night: 18:00 to 24:00 on Day D + 00:00 to 02:00 on Day D+1)
    const daySlotScores = {};

    function initDaySlot(dateStr) {
        if (!daySlotScores[dateStr]) {
            daySlotScores[dateStr] = {
                s1: 0, s2: 0, s3: 0,
                s1Hours: 0, s2Hours: 0, s3Hours: 0,
                s1Projects: {}, s2Projects: {}, s3Projects: {}
            };
        }
        return daySlotScores[dateStr];
    }

    for (const r of allRecords) {
        if (!r.start || !r.end || !r.minutes) continue;
        const startH = toDecimalHour(r.start);
        let endH = toDecimalHour(r.end);
        if (startH < 0 || endH < 0) continue;
        if (endH < startH) endH += 24;

        const sph = getProjectScore(r.project);

        if (startH >= 6.0) {
            // Belongs to waking cycle of dateStr
            const ds = initDaySlot(r.dateStr);

            // Slot 1: 06:00 - 12:00
            const o1 = Math.max(0, Math.min(endH, 12.0) - Math.max(startH, 6.0));
            if (o1 > 0) {
                const pts = o1 * sph;
                ds.s1 += pts;
                ds.s1Hours += o1;
                ds.s1Projects[r.project] = (ds.s1Projects[r.project] || 0) + pts;
            }

            // Slot 2: 12:00 - 18:00
            const o2 = Math.max(0, Math.min(endH, 18.0) - Math.max(startH, 12.0));
            if (o2 > 0) {
                const pts = o2 * sph;
                ds.s2 += pts;
                ds.s2Hours += o2;
                ds.s2Projects[r.project] = (ds.s2Projects[r.project] || 0) + pts;
            }

            // Slot 3: 18:00 - 26:00 (18:00 to 02:00 next day)
            const o3 = Math.max(0, Math.min(endH, 26.0) - Math.max(startH, 18.0));
            if (o3 > 0) {
                const pts = o3 * sph;
                ds.s3 += pts;
                ds.s3Hours += o3;
                ds.s3Projects[r.project] = (ds.s3Projects[r.project] || 0) + pts;
            }
        } else {
            // Session started before 06:00
            // If before 02:00: belongs to previous evening's night slot!
            if (startH < 2.0) {
                const prevDateStr = shiftDate(r.dateStr, "day", -1);
                const dsPrev = initDaySlot(prevDateStr);
                const effStart = startH + 24.0;
                const effEnd = Math.min(endH, 2.0) + 24.0;
                const o3 = Math.max(0, Math.min(effEnd, 26.0) - Math.max(effStart, 24.0));
                if (o3 > 0) {
                    const pts = o3 * sph;
                    dsPrev.s3 += pts;
                    dsPrev.s3Hours += o3;
                    dsPrev.s3Projects[r.project] = (dsPrev.s3Projects[r.project] || 0) + pts;
                }
            }

            // If session ended after 06:00: morning overlap on dateStr
            if (endH > 6.0) {
                const ds = initDaySlot(r.dateStr);
                const o1 = Math.max(0, Math.min(endH, 12.0) - 6.0);
                if (o1 > 0) {
                    const pts = o1 * sph;
                    ds.s1 += pts;
                    ds.s1Hours += o1;
                    ds.s1Projects[r.project] = (ds.s1Projects[r.project] || 0) + pts;
                }
            }
        }
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
    const headingEl = titleCol.createEl("h3", { text: "📊 Weekly Productivity Pattern by Time Slot" });
    headingEl.style.margin = "0 0 4px 0";

    const subtitleEl = titleCol.createEl("p", {
        text: "Mean productivity scores for each day of the week, with 3 columns: 06:00–12:00, 12:00–18:00, and 18:00–02:00. View by week, month, or all time."
    });
    subtitleEl.style.opacity = "0.7";
    subtitleEl.style.fontSize = "12px";
    subtitleEl.style.margin = "0";

    // Top Controls
    const topControls = headerRow.createDiv();
    topControls.style.display = "flex";
    topControls.style.gap = "6px";
    topControls.style.alignItems = "center";
    topControls.style.flexWrap = "wrap";

    // Scope Tabs: Week, Month, All Time
    const scopeOpts = [
        { id: "week", label: "📆 Week" },
        { id: "month", label: "🗓️ Month" },
        { id: "all", label: "🌐 All Time" }
    ];
    const scopeBtns = [];

    scopeOpts.forEach(opt => {
        const btn = topControls.createEl("button", { text: opt.label });
        btn.style.padding = "5px 12px";
        btn.style.fontSize = "12px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        scopeBtns.push({ id: opt.id, btn });

        btn.addEventListener("click", () => {
            if (slotScope !== opt.id) {
                slotScope = opt.id;
                if (opt.id === "week") slotWeekOffset = 0;
                if (opt.id === "month") slotMonthOffset = 0;
                renderSlotView();
            }
        });
    });

    // Week Start Toggle (Mon vs Sat)
    const weekStartBtn = topControls.createEl("button");
    weekStartBtn.style.padding = "5px 10px";
    weekStartBtn.style.fontSize = "12px";
    weekStartBtn.style.borderRadius = "4px";
    weekStartBtn.style.border = "1px solid var(--background-modifier-border)";
    weekStartBtn.style.cursor = "pointer";
    weekStartBtn.addEventListener("click", () => {
        slotWeekStart = (slotWeekStart === "mon") ? "sat" : "mon";
        updateWeekStartBtn();
        renderSlotView();
    });

    function updateWeekStartBtn() {
        weekStartBtn.textContent = slotWeekStart === "mon" ? "Start: Mon" : "Start: Sat";
        weekStartBtn.title = slotWeekStart === "mon" ? "Week starts Monday (click to switch to Saturday)" : "Week starts Saturday (click to switch to Monday)";
    }
    updateWeekStartBtn();

    // Average mode toggle (All calendar days vs Active days only)
    const avgModeBtn = topControls.createEl("button");
    avgModeBtn.style.padding = "5px 10px";
    avgModeBtn.style.fontSize = "12px";
    avgModeBtn.style.borderRadius = "4px";
    avgModeBtn.style.border = "1px solid var(--background-modifier-border)";
    avgModeBtn.style.cursor = "pointer";
    avgModeBtn.addEventListener("click", () => {
        slotAverageMode = (slotAverageMode === "all_days") ? "active_only" : "all_days";
        updateAvgModeBtn();
        renderSlotView();
    });

    function updateAvgModeBtn() {
        avgModeBtn.textContent = slotAverageMode === "all_days" ? "Avg: All Days" : "Avg: Active Only";
        avgModeBtn.title = slotAverageMode === "all_days" ? "Mean divided by all days (click for active-only)" : "Mean divided by active days only (click for all calendar days)";
    }
    updateAvgModeBtn();

    // Mini KPI Cards Bar
    const kpiBar = card.createDiv();
    kpiBar.style.display = "grid";
    kpiBar.style.gridTemplateColumns = "repeat(auto-fit, minmax(130px, 1fr))";
    kpiBar.style.gap = "8px";
    kpiBar.style.marginBottom = "12px";

    // Toolbar Row: Period Navigation & Status
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

    // Navigation Controls
    const navGroup = toolbarRow.createDiv();
    navGroup.style.display = "flex";
    navGroup.style.gap = "4px";
    navGroup.style.alignItems = "center";

    const oldestBtn = navGroup.createEl("button", { text: "⏮️ Oldest" });
    const prevBtn = navGroup.createEl("button", { text: "◀ Prev" });
    const nextBtn = navGroup.createEl("button", { text: "Next ▶" });
    const latestBtn = navGroup.createEl("button", { text: "Current ⏭️" });

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

    // Period Quick Select Dropdown (Right)
    const selectorGroup = toolbarRow.createDiv();
    selectorGroup.style.display = "flex";
    selectorGroup.style.gap = "6px";
    selectorGroup.style.alignItems = "center";

    const selectorLabel = selectorGroup.createEl("span", { text: "Jump to: " });
    selectorLabel.style.fontSize = "11px";
    selectorLabel.style.opacity = "0.7";

    const periodSelect = selectorGroup.createEl("select");
    periodSelect.style.padding = "3px 6px";
    periodSelect.style.fontSize = "11px";
    periodSelect.style.borderRadius = "4px";
    periodSelect.style.border = "1px solid var(--background-modifier-border)";
    periodSelect.style.backgroundColor = "var(--background-secondary)";
    periodSelect.style.color = "var(--text-normal)";

    const chartDiv = card.createDiv();

    function renderSlotView() {
        // 1. Update Scope buttons
        scopeBtns.forEach(({ id, btn }) => {
            if (id === slotScope) {
                btn.style.backgroundColor = "var(--interactive-accent)";
                btn.style.color = "var(--text-on-accent)";
                btn.style.fontWeight = "bold";
            } else {
                btn.style.backgroundColor = "var(--background-modifier-form-field)";
                btn.style.color = "var(--text-normal)";
                btn.style.fontWeight = "normal";
            }
        });

        // 2. Fetch available periods
        const availableWeeks = getAllAvailableWeeks();
        const availableMonths = getAllAvailableMonths();

        // 3. Navigation setup based on scope
        periodSelect.innerHTML = "";

        let currentScopeLabel = "";
        let targetDates = []; // List of date objects/strings to evaluate

        if (slotScope === "week") {
            const totalW = availableWeeks.length;
            const maxWOffset = Math.max(0, totalW - 1);
            if (slotWeekOffset > maxWOffset) slotWeekOffset = maxWOffset;
            if (slotWeekOffset < 0) slotWeekOffset = 0;

            const selectedWeekIdx = totalW - 1 - slotWeekOffset;
            const selectedWeek = availableWeeks[selectedWeekIdx] || availableWeeks[availableWeeks.length - 1];

            oldestBtn.disabled = (slotWeekOffset >= maxWOffset);
            prevBtn.disabled = (slotWeekOffset >= maxWOffset);
            nextBtn.disabled = (slotWeekOffset <= 0);
            latestBtn.disabled = (slotWeekOffset <= 0);

            [oldestBtn, prevBtn].forEach(b => b.style.opacity = (slotWeekOffset >= maxWOffset) ? "0.4" : "1");
            [nextBtn, latestBtn].forEach(b => b.style.opacity = (slotWeekOffset <= 0) ? "0.4" : "1");

            // Populate selector
            availableWeeks.slice().reverse().forEach((w, rIdx) => {
                const opt = periodSelect.createEl("option", { text: w.label, value: String(rIdx) });
                if (rIdx === slotWeekOffset) opt.selected = true;
            });
            selectorGroup.style.display = "flex";

            if (selectedWeek) {
                currentScopeLabel = selectedWeek.label;
                badgeEl.textContent = `${selectedWeek.label} (${selectedWeek.startDateStr} to ${selectedWeek.endDateStr})`;

                // Collect all 7 dates of this week
                const d = new Date(selectedWeek.startDate);
                for (let i = 0; i < 7; i++) {
                    const y = d.getFullYear();
                    const m = String(d.getMonth() + 1).padStart(2, '0');
                    const day = String(d.getDate()).padStart(2, '0');
                    targetDates.push({ dateStr: `${y}-${m}-${day}`, dateObj: new Date(d) });
                    d.setDate(d.getDate() + 1);
                }
            }
        } else if (slotScope === "month") {
            const totalM = availableMonths.length;
            const maxMOffset = Math.max(0, totalM - 1);
            if (slotMonthOffset > maxMOffset) slotMonthOffset = maxMOffset;
            if (slotMonthOffset < 0) slotMonthOffset = 0;

            const selectedMonthIdx = totalM - 1 - slotMonthOffset;
            const selectedMonth = availableMonths[selectedMonthIdx] || availableMonths[availableMonths.length - 1];

            oldestBtn.disabled = (slotMonthOffset >= maxMOffset);
            prevBtn.disabled = (slotMonthOffset >= maxMOffset);
            nextBtn.disabled = (slotMonthOffset <= 0);
            latestBtn.disabled = (slotMonthOffset <= 0);

            [oldestBtn, prevBtn].forEach(b => b.style.opacity = (slotMonthOffset >= maxMOffset) ? "0.4" : "1");
            [nextBtn, latestBtn].forEach(b => b.style.opacity = (slotMonthOffset <= 0) ? "0.4" : "1");

            availableMonths.slice().reverse().forEach((m, rIdx) => {
                const opt = periodSelect.createEl("option", { text: m.label, value: String(rIdx) });
                if (rIdx === slotMonthOffset) opt.selected = true;
            });
            selectorGroup.style.display = "flex";

            if (selectedMonth) {
                currentScopeLabel = selectedMonth.label;
                badgeEl.textContent = `Month of ${selectedMonth.label} (Daily Means)`;

                const d = new Date(selectedMonth.startDate);
                const end = new Date(selectedMonth.endDate);
                const now = new Date();
                const todayEnd = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999);
                const effectiveEnd = (end > todayEnd) ? todayEnd : end;

                while (d <= effectiveEnd) {
                    const y = d.getFullYear();
                    const m = String(d.getMonth() + 1).padStart(2, '0');
                    const day = String(d.getDate()).padStart(2, '0');
                    targetDates.push({ dateStr: `${y}-${m}-${day}`, dateObj: new Date(d) });
                    d.setDate(d.getDate() + 1);
                }
            }
        } else {
            // All Time
            oldestBtn.disabled = true;
            prevBtn.disabled = true;
            nextBtn.disabled = true;
            latestBtn.disabled = true;
            [oldestBtn, prevBtn, nextBtn, latestBtn].forEach(b => b.style.opacity = "0.4");
            selectorGroup.style.display = "none";

            const allDatesSet = new Set(allRecords.map(r => r.dateStr));
            let minDate = new Date();
            let maxDate = new Date(0);
            allRecords.forEach(r => {
                if (r.dateObj) {
                    if (r.dateObj < minDate) minDate = r.dateObj;
                    if (r.dateObj > maxDate) maxDate = r.dateObj;
                }
            });

            const d = new Date(minDate);
            const now = new Date();
            const todayEnd = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999);
            const end = (maxDate > todayEnd) ? maxDate : todayEnd;

            while (d <= end) {
                const y = d.getFullYear();
                const m = String(d.getMonth() + 1).padStart(2, '0');
                const day = String(d.getDate()).padStart(2, '0');
                targetDates.push({ dateStr: `${y}-${m}-${day}`, dateObj: new Date(d) });
                d.setDate(d.getDate() + 1);
            }

            badgeEl.textContent = `All Time (${availableWeeks.length} weeks recorded)`;
        }

        // 4. Determine Weekday Columns Order
        const weekdayDefs = slotWeekStart === "mon"
            ? [
                { dayIdx: 1, name: "Monday", short: "Mon" },
                { dayIdx: 2, name: "Tuesday", short: "Tue" },
                { dayIdx: 3, name: "Wednesday", short: "Wed" },
                { dayIdx: 4, name: "Thursday", short: "Thu" },
                { dayIdx: 5, name: "Friday", short: "Fri" },
                { dayIdx: 6, name: "Saturday", short: "Sat" },
                { dayIdx: 0, name: "Sunday", short: "Sun" }
              ]
            : [
                { dayIdx: 6, name: "Saturday", short: "Sat" },
                { dayIdx: 0, name: "Sunday", short: "Sun" },
                { dayIdx: 1, name: "Monday", short: "Mon" },
                { dayIdx: 2, name: "Tuesday", short: "Tue" },
                { dayIdx: 3, name: "Wednesday", short: "Wed" },
                { dayIdx: 4, name: "Thursday", short: "Thu" },
                { dayIdx: 5, name: "Friday", short: "Fri" }
              ];

        // 5. Aggregate Slot Scores by Weekday
        const weekdaySlot1 = [];
        const weekdaySlot2 = [];
        const weekdaySlot3 = [];
        const weekdayMeta = [];

        let totalS1AllWeekdays = 0;
        let totalS2AllWeekdays = 0;
        let totalS3AllWeekdays = 0;
        let peakSlotVal = 0;
        let peakSlotDesc = "-";

        weekdayDefs.forEach(def => {
            const matchingDates = targetDates.filter(td => td.dateObj.getDay() === def.dayIdx);
            const totalOccurrences = matchingDates.length;

            let sumS1 = 0;
            let sumS2 = 0;
            let sumS3 = 0;
            let activeDaysCount = 0;
            const s1Projects = {};
            const s2Projects = {};
            const s3Projects = {};

            matchingDates.forEach(td => {
                const sc = daySlotScores[td.dateStr];
                if (sc) {
                    sumS1 += sc.s1;
                    sumS2 += sc.s2;
                    sumS3 += sc.s3;
                    if (sc.s1 > 0 || sc.s2 > 0 || sc.s3 > 0) activeDaysCount++;

                    for (const [p, pts] of Object.entries(sc.s1Projects)) s1Projects[p] = (s1Projects[p] || 0) + pts;
                    for (const [p, pts] of Object.entries(sc.s2Projects)) s2Projects[p] = (s2Projects[p] || 0) + pts;
                    for (const [p, pts] of Object.entries(sc.s3Projects)) s3Projects[p] = (s3Projects[p] || 0) + pts;
                }
            });

            let divisor = (slotScope === "week")
                ? 1
                : (slotAverageMode === "active_only" ? Math.max(1, activeDaysCount) : Math.max(1, totalOccurrences));

            const m1 = sumS1 / divisor;
            const m2 = sumS2 / divisor;
            const m3 = sumS3 / divisor;

            weekdaySlot1.push(Number(m1.toFixed(2)));
            weekdaySlot2.push(Number(m2.toFixed(2)));
            weekdaySlot3.push(Number(m3.toFixed(2)));

            totalS1AllWeekdays += m1;
            totalS2AllWeekdays += m2;
            totalS3AllWeekdays += m3;

            if (m1 > peakSlotVal) { peakSlotVal = m1; peakSlotDesc = `${def.short} 06–12 (${m1.toFixed(1)} pts)`; }
            if (m2 > peakSlotVal) { peakSlotVal = m2; peakSlotDesc = `${def.short} 12–18 (${m2.toFixed(1)} pts)`; }
            if (m3 > peakSlotVal) { peakSlotVal = m3; peakSlotDesc = `${def.short} 18–02 (${m3.toFixed(1)} pts)`; }

            weekdayMeta.push({
                def,
                totalOccurrences,
                activeDaysCount,
                sumS1, sumS2, sumS3,
                m1, m2, m3,
                s1Projects, s2Projects, s3Projects
            });
        });

        // 6. Update KPI Cards
        const daysInWeek = 7;
        const avgDailyS1 = (totalS1AllWeekdays / daysInWeek).toFixed(2);
        const avgDailyS2 = (totalS2AllWeekdays / daysInWeek).toFixed(2);
        const avgDailyS3 = (totalS3AllWeekdays / daysInWeek).toFixed(2);

        kpiBar.innerHTML = `
            <div style="background: var(--background-primary); border: 1px solid var(--background-modifier-border); border-radius: 6px; padding: 10px; text-align: center;">
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">🌅 06:00 – 12:00 Mean</div>
                <div style="font-size: 18px; font-weight: bold; color: #f59e0b;">${avgDailyS1} <span style="font-size: 11px;">pts/day</span></div>
            </div>
            <div style="background: var(--background-primary); border: 1px solid var(--background-modifier-border); border-radius: 6px; padding: 10px; text-align: center;">
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">☀️ 12:00 – 18:00 Mean</div>
                <div style="font-size: 18px; font-weight: bold; color: #0ea5e9;">${avgDailyS2} <span style="font-size: 11px;">pts/day</span></div>
            </div>
            <div style="background: var(--background-primary); border: 1px solid var(--background-modifier-border); border-radius: 6px; padding: 10px; text-align: center;">
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">🌙 18:00 – 02:00 Mean</div>
                <div style="font-size: 18px; font-weight: bold; color: #8b5cf6;">${avgDailyS3} <span style="font-size: 11px;">pts/day</span></div>
            </div>
            <div style="background: var(--background-primary); border: 1px solid var(--background-modifier-border); border-radius: 6px; padding: 10px; text-align: center;">
                <div style="font-size: 11px; opacity: 0.7; text-transform: uppercase;">⚡ Peak Slot</div>
                <div style="font-size: 16px; font-weight: bold; color: #fbbf24;">${peakSlotDesc}</div>
            </div>
        `;

        // 7. Render Chart.js Grouped Bar Chart (3 columns per weekday)
        chartDiv.innerHTML = "";
        renderQuickChart({
            type: 'bar',
            data: {
                labels: weekdayDefs.map(d => d.name),
                datasets: [
                    {
                        label: '🌅 06:00–12:00 (Morning)',
                        data: weekdaySlot1,
                        backgroundColor: 'rgba(245, 158, 11, 0.85)',
                        borderColor: '#d97706',
                        borderWidth: 1,
                        borderRadius: 4
                    },
                    {
                        label: '☀️ 12:00–18:00 (Afternoon)',
                        data: weekdaySlot2,
                        backgroundColor: 'rgba(14, 165, 233, 0.85)',
                        borderColor: '#0284c7',
                        borderWidth: 1,
                        borderRadius: 4
                    },
                    {
                        label: '🌙 18:00–02:00 (Evening & Night)',
                        data: weekdaySlot3,
                        backgroundColor: 'rgba(139, 92, 246, 0.85)',
                        borderColor: '#7c3aed',
                        borderWidth: 1,
                        borderRadius: 4
                    }
                ]
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
                                return weekdayDefs[idx].name;
                            },
                            label: function(item) {
                                return ` ${item.dataset.label}: ${item.raw} pts${slotScope !== 'week' ? '/day' : ''}`;
                            },
                            afterLabel: function(item) {
                                const idx = item.dataIndex;
                                const dsIdx = item.datasetIndex; // 0=s1, 1=s2, 2=s3
                                const meta = weekdayMeta[idx];
                                if (!meta) return '';

                                const projs = dsIdx === 0 ? meta.s1Projects : (dsIdx === 1 ? meta.s2Projects : meta.s3Projects);
                                const topEntries = Object.entries(projs).sort((a, b) => b[1] - a[1]).slice(0, 3);
                                if (topEntries.length === 0) return '   (No activity logged)';
                                return '   Top: ' + topEntries.map(([p, pts]) => `${p} (${pts.toFixed(1)} pts)`).join(', ');
                            },
                            footer: function(items) {
                                let tot = 0;
                                items.forEach(it => { tot += (it.raw || 0); });
                                const idx = items[0]?.dataIndex;
                                const meta = weekdayMeta[idx];
                                if (slotScope === "week") {
                                    return `Day Total: ${tot.toFixed(2)} pts`;
                                } else {
                                    const occurrences = meta ? meta.totalOccurrences : 1;
                                    const active = meta ? meta.activeDaysCount : 0;
                                    return `Day Mean: ${tot.toFixed(2)} pts/day • (${active}/${occurrences} active ${weekdayDefs[idx].short}s)`;
                                }
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        stacked: false,
                        title: { display: true, text: 'Day of Week', font: { size: 12, weight: 'bold' } }
                    },
                    y: {
                        stacked: false,
                        beginAtZero: true,
                        title: {
                            display: true,
                            text: slotScope === 'week' ? 'Score Points (pts)' : 'Mean Score / Day (pts)',
                            font: { size: 12, weight: 'bold' }
                        }
                    }
                }
            }
        }, chartDiv);
    }

    // Navigation Event Listeners
    oldestBtn.addEventListener("click", () => {
        if (slotScope === "week") {
            const totalW = getAllAvailableWeeks().length;
            slotWeekOffset = Math.max(0, totalW - 1);
        } else if (slotScope === "month") {
            const totalM = getAllAvailableMonths().length;
            slotMonthOffset = Math.max(0, totalM - 1);
        }
        renderSlotView();
    });

    prevBtn.addEventListener("click", () => {
        if (slotScope === "week") {
            const totalW = getAllAvailableWeeks().length;
            slotWeekOffset = Math.min(totalW - 1, slotWeekOffset + 1);
        } else if (slotScope === "month") {
            const totalM = getAllAvailableMonths().length;
            slotMonthOffset = Math.min(totalM - 1, slotMonthOffset + 1);
        }
        renderSlotView();
    });

    nextBtn.addEventListener("click", () => {
        if (slotScope === "week") {
            slotWeekOffset = Math.max(0, slotWeekOffset - 1);
        } else if (slotScope === "month") {
            slotMonthOffset = Math.max(0, slotMonthOffset - 1);
        }
        renderSlotView();
    });

    latestBtn.addEventListener("click", () => {
        if (slotScope === "week") slotWeekOffset = 0;
        if (slotScope === "month") slotMonthOffset = 0;
        renderSlotView();
    });

    periodSelect.addEventListener("change", (e) => {
        const val = parseInt(e.target.value, 10);
        if (!isNaN(val)) {
            if (slotScope === "week") slotWeekOffset = val;
            if (slotScope === "month") slotMonthOffset = val;
            renderSlotView();
        }
    });

    // Initial render of Weekly Time Slot Section
    renderSlotView();
}



function renderDailyScoreTimeSlotSection() {
    if (allRecords.length === 0) return;

    const card = chartSection.createDiv();
    card.style.padding = "18px";
    card.style.borderRadius = "8px";
    card.style.backgroundColor = "var(--background-secondary)";
    card.style.border = "1px solid var(--background-modifier-border)";
    card.style.marginTop = "32px";
    card.style.marginBottom = "30px";
    card.style.position = "relative";

    // 1. Header Row
    const headerRow = card.createDiv();
    headerRow.style.display = "flex";
    headerRow.style.justifyContent = "space-between";
    headerRow.style.alignItems = "center";
    headerRow.style.flexWrap = "wrap";
    headerRow.style.gap = "10px";
    headerRow.style.marginBottom = "14px";

    const titleBox = headerRow.createDiv();
    const heading = titleBox.createEl("h3", { text: "🏆 Daily Score by Time Slot (04-12, 12-18, 18-04)" });
    heading.style.margin = "0 0 4px 0";

    const sub = titleBox.createEl("p", {
        text: "Productivity scores partitioned into 3 daily waking periods: Morning (04:00–12:00), Afternoon (12:00–18:00), and Night (18:00–04:00)."
    });
    sub.style.fontSize = "11px";
    sub.style.opacity = "0.7";
    sub.style.margin = "0";

    const topControls = headerRow.createDiv();
    topControls.style.display = "flex";
    topControls.style.alignItems = "center";
    topControls.style.gap = "8px";
    topControls.style.flexWrap = "wrap";

    // Form Mode Toggle Buttons: [ 📊 3 Columns ] [ 📈 Daily Sum ]
    const formContainer = topControls.createDiv();
    formContainer.style.display = "flex";
    formContainer.style.alignItems = "center";
    formContainer.style.gap = "4px";

    const formLabel = formContainer.createEl("span", { text: "View:" });
    formLabel.style.fontSize = "12px";
    formLabel.style.fontWeight = "bold";

    const formOptions = [
        { id: "3col", label: "📊 3 Columns (4-12, 12-18, 18-4)" },
        { id: "sum", label: "📈 Daily Sum" }
    ];

    const formBtns = [];
    formOptions.forEach(opt => {
        const btn = formContainer.createEl("button", { text: opt.label });
        btn.style.padding = "4px 8px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        formBtns.push({ id: opt.id, btn });

        btn.addEventListener("click", () => {
            if (dayScoreChartForm !== opt.id) {
                dayScoreChartForm = opt.id;
                updateFormBtns();
                renderView();
            }
        });
    });

    function updateFormBtns() {
        formBtns.forEach(({ id, btn }) => {
            const active = (id === dayScoreChartForm);
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });
    }
    updateFormBtns();

    // Wheel Mode Button
    const wheelModeBtn = topControls.createEl("button");
    wheelModeBtn.style.padding = "4px 8px";
    wheelModeBtn.style.fontSize = "11px";
    wheelModeBtn.style.borderRadius = "4px";
    wheelModeBtn.style.border = "1px solid var(--background-modifier-border)";
    wheelModeBtn.style.cursor = "pointer";

    function updateWheelModeBtn() {
        if (dayScoreWheelMode === "pan") {
            wheelModeBtn.textContent = "🖱️ Wheel: ↔️ Pan";
            wheelModeBtn.style.backgroundColor = "var(--background-modifier-form-field)";
            wheelModeBtn.style.color = "var(--text-normal)";
            wheelModeBtn.style.fontWeight = "normal";
        } else {
            wheelModeBtn.textContent = "🖱️ Wheel: 🔍 Zoom";
            wheelModeBtn.style.backgroundColor = "var(--interactive-accent)";
            wheelModeBtn.style.color = "var(--text-on-accent)";
            wheelModeBtn.style.fontWeight = "bold";
        }
    }
    updateWheelModeBtn();

    wheelModeBtn.addEventListener("click", () => {
        dayScoreWheelMode = (dayScoreWheelMode === "pan") ? "zoom" : "pan";
        updateWheelModeBtn();
    });

    // 2. Navigation Bar & Presets
    const navBar = card.createDiv();
    navBar.style.display = "flex";
    navBar.style.justifyContent = "space-between";
    navBar.style.alignItems = "center";
    navBar.style.marginBottom = "14px";
    navBar.style.flexWrap = "wrap";
    navBar.style.gap = "8px";

    const navLeft = navBar.createDiv();
    navLeft.style.display = "flex";
    navLeft.style.alignItems = "center";
    navLeft.style.gap = "6px";
    navLeft.style.flexWrap = "wrap";

    const oldestBtn = navLeft.createEl("button", { text: "⏮️ Oldest" });
    const prevBtn = navLeft.createEl("button", { text: "◀ Prev" });
    const rangeBadge = navLeft.createEl("span");
    const nextBtn = navLeft.createEl("button", { text: "Next ▶" });
    const latestBtn = navLeft.createEl("button", { text: "⏭️ Latest" });

    [oldestBtn, prevBtn, nextBtn, latestBtn].forEach(b => {
        b.style.padding = "4px 8px";
        b.style.fontSize = "11px";
        b.style.borderRadius = "4px";
        b.style.border = "1px solid var(--background-modifier-border)";
        b.style.backgroundColor = "var(--background-modifier-form-field)";
        b.style.cursor = "pointer";
    });

    rangeBadge.style.fontSize = "11px";
    rangeBadge.style.fontWeight = "bold";
    rangeBadge.style.padding = "3px 10px";
    rangeBadge.style.borderRadius = "12px";
    rangeBadge.style.backgroundColor = "var(--background-primary)";
    rangeBadge.style.border = "1px solid var(--background-modifier-border)";

    const presetsContainer = navBar.createDiv();
    presetsContainer.style.display = "flex";
    presetsContainer.style.alignItems = "center";
    presetsContainer.style.gap = "4px";

    const presets = [
        { id: 7, label: "7 Days" },
        { id: 14, label: "14 Days" },
        { id: 30, label: "30 Days" },
        { id: "all", label: "All" }
    ];

    // 3. Chart Wrapper
    const chartWrapper = card.createDiv();
    chartWrapper.style.position = "relative";
    chartWrapper.style.minHeight = "280px";
    chartWrapper.style.marginBottom = "18px";

    const floatLeft = chartWrapper.createEl("button", { text: "◀" });
    floatLeft.title = "Scroll to earlier days";
    floatLeft.style.position = "absolute";
    floatLeft.style.left = "4px";
    floatLeft.style.top = "50%";
    floatLeft.style.transform = "translateY(-50%)";
    floatLeft.style.zIndex = "10";
    floatLeft.style.width = "28px";
    floatLeft.style.height = "52px";
    floatLeft.style.borderRadius = "4px";
    floatLeft.style.border = "1px solid var(--background-modifier-border)";
    floatLeft.style.backgroundColor = "var(--background-secondary)";
    floatLeft.style.cursor = "pointer";
    floatLeft.style.opacity = "0.45";
    floatLeft.style.fontSize = "14px";
    floatLeft.style.display = "flex";
    floatLeft.style.alignItems = "center";
    floatLeft.style.justifyContent = "center";
    floatLeft.style.transition = "opacity 0.15s, background-color 0.15s";
    floatLeft.addEventListener("mouseenter", () => floatLeft.style.opacity = "0.95");
    floatLeft.addEventListener("mouseleave", () => floatLeft.style.opacity = "0.45");

    const floatRight = chartWrapper.createEl("button", { text: "▶" });
    floatRight.title = "Scroll to newer days";
    floatRight.style.position = "absolute";
    floatRight.style.right = "4px";
    floatRight.style.top = "50%";
    floatRight.style.transform = "translateY(-50%)";
    floatRight.style.zIndex = "10";
    floatRight.style.width = "28px";
    floatRight.style.height = "52px";
    floatRight.style.borderRadius = "4px";
    floatRight.style.border = "1px solid var(--background-modifier-border)";
    floatRight.style.backgroundColor = "var(--background-secondary)";
    floatRight.style.cursor = "pointer";
    floatRight.style.opacity = "0.45";
    floatRight.style.fontSize = "14px";
    floatRight.style.display = "flex";
    floatRight.style.alignItems = "center";
    floatRight.style.justifyContent = "center";
    floatRight.style.transition = "opacity 0.15s, background-color 0.15s";
    floatRight.addEventListener("mouseenter", () => floatRight.style.opacity = "0.95");
    floatRight.addEventListener("mouseleave", () => floatRight.style.opacity = "0.45");

    const chartCanvasContainer = chartWrapper.createDiv();

    // 4. Table Container
    const tableSection = card.createDiv();

    // Data calculation
    function calculateDailyScoreData() {
        const dayMap = {};
        function initScore(d) {
            if (!dayMap[d]) dayMap[d] = { s1: 0, s2: 0, s3: 0, s1H: 0, s2H: 0, s3H: 0, projects: {} };
            return dayMap[d];
        }

        for (const r of allRecords) {
            if (!r.start || !r.end || !r.minutes) continue;
            const startH = toDecimalHour(r.start);
            let endH = toDecimalHour(r.end);
            if (startH < 0 || endH < 0) continue;
            if (endH < startH) endH += 24;

            const sph = getProjectScore(r.project);

            if (startH >= 4.0) {
                const ds = initScore(r.dateStr);

                // Slot 1: 04:00 - 12:00
                const o1 = Math.max(0, Math.min(endH, 12.0) - Math.max(startH, 4.0));
                if (o1 > 0) {
                    const pts = o1 * sph;
                    ds.s1 += pts; ds.s1H += o1;
                    ds.projects[r.project] = (ds.projects[r.project] || 0) + pts;
                }

                // Slot 2: 12:00 - 18:00
                const o2 = Math.max(0, Math.min(endH, 18.0) - Math.max(startH, 12.0));
                if (o2 > 0) {
                    const pts = o2 * sph;
                    ds.s2 += pts; ds.s2H += o2;
                    ds.projects[r.project] = (ds.projects[r.project] || 0) + pts;
                }

                // Slot 3: 18:00 - 28:00 (18:00 to 04:00 next day)
                const o3 = Math.max(0, Math.min(endH, 28.0) - Math.max(startH, 18.0));
                if (o3 > 0) {
                    const pts = o3 * sph;
                    ds.s3 += pts; ds.s3H += o3;
                    ds.projects[r.project] = (ds.projects[r.project] || 0) + pts;
                }
            } else {
                // Session started before 04:00 -> belongs to night slot of previous date
                const prevDateStr = shiftDate(r.dateStr, "day", -1);
                const dsPrev = initScore(prevDateStr);

                const effStart = startH + 24.0;
                const effEnd = Math.min(endH, 4.0) + 24.0;
                const o3 = Math.max(0, Math.min(effEnd, 28.0) - Math.max(effStart, 24.0));
                if (o3 > 0) {
                    const pts = o3 * sph;
                    dsPrev.s3 += pts; dsPrev.s3H += o3;
                    dsPrev.projects[r.project] = (dsPrev.projects[r.project] || 0) + pts;
                }

                if (endH > 4.0) {
                    const ds = initScore(r.dateStr);
                    const o1 = Math.max(0, Math.min(endH, 12.0) - 4.0);
                    if (o1 > 0) {
                        const pts = o1 * sph;
                        ds.s1 += pts; ds.s1H += o1;
                        ds.projects[r.project] = (ds.projects[r.project] || 0) + pts;
                    }
                }
            }
        }

        const mNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
        const dNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

        return Object.keys(dayMap).sort().map(dateStr => {
            const [y, m, d] = dateStr.split('-').map(Number);
            const dt = new Date(y, m - 1, d);
            const ds = dayMap[dateStr];
            const totalScore = +(ds.s1 + ds.s2 + ds.s3).toFixed(2);
            const totalHours = +(ds.s1H + ds.s2H + ds.s3H).toFixed(1);

            return {
                dateStr,
                dateObj: dt,
                label: `${mNames[m - 1]} ${d}`,
                fullLabel: `${dNames[dt.getDay()]}, ${mNames[m - 1]} ${d}, ${y}`,
                s1: +ds.s1.toFixed(2),
                s2: +ds.s2.toFixed(2),
                s3: +ds.s3.toFixed(2),
                s1H: +ds.s1H.toFixed(1),
                s2H: +ds.s2H.toFixed(1),
                s3H: +ds.s3H.toFixed(1),
                totalScore,
                totalHours,
                projects: ds.projects
            };
        });
    }

    function renderView() {
        chartCanvasContainer.innerHTML = "";
        tableSection.innerHTML = "";

        const allDays = calculateDailyScoreData();
        const total = allDays.length;

        // Render Presets
        presetsContainer.querySelectorAll("button").forEach(b => b.remove());
        presets.forEach(pv => {
            const pBtn = presetsContainer.createEl("button", { text: pv.label });
            pBtn.style.padding = "3px 7px";
            pBtn.style.fontSize = "11px";
            pBtn.style.borderRadius = "3px";
            pBtn.style.cursor = "pointer";
            pBtn.style.border = "1px solid var(--background-modifier-border)";

            if (dayScoreWindowSize === pv.id) {
                pBtn.style.backgroundColor = "var(--interactive-accent)";
                pBtn.style.color = "var(--text-on-accent)";
                pBtn.style.fontWeight = "bold";
            } else {
                pBtn.style.backgroundColor = "var(--background-modifier-form-field)";
                pBtn.style.color = "var(--text-normal)";
            }

            pBtn.addEventListener("click", () => {
                dayScoreWindowSize = pv.id;
                dayScoreOffset = 0;
                renderView();
            });
        });

        // Window calculation
        const effectiveSize = (dayScoreWindowSize === "all") ? total : Math.min(dayScoreWindowSize, total);
        const maxOffset = Math.max(0, total - effectiveSize);
        if (dayScoreOffset > maxOffset) dayScoreOffset = maxOffset;
        if (dayScoreOffset < 0) dayScoreOffset = 0;

        const startIdx = Math.max(0, total - effectiveSize - dayScoreOffset);
        const endIdx = startIdx + effectiveSize;
        const visibleDays = allDays.slice(startIdx, endIdx);

        // Update Nav button states
        const isAtOldest = (dayScoreOffset >= maxOffset);
        const isAtLatest = (dayScoreOffset === 0);

        oldestBtn.disabled = isAtOldest;
        prevBtn.disabled = isAtOldest;
        floatLeft.disabled = isAtOldest;
        oldestBtn.style.opacity = isAtOldest ? "0.4" : "1";
        prevBtn.style.opacity = isAtOldest ? "0.4" : "1";
        floatLeft.style.opacity = isAtOldest ? "0.2" : "0.5";

        nextBtn.disabled = isAtLatest;
        latestBtn.disabled = isAtLatest;
        floatRight.disabled = isAtLatest;
        nextBtn.style.opacity = isAtLatest ? "0.4" : "1";
        latestBtn.style.opacity = isAtLatest ? "0.4" : "1";
        floatRight.style.opacity = isAtLatest ? "0.2" : "0.5";

        // Aggregate statistics for range badge
        let winTotalScore = 0;
        let winTotalHours = 0;
        visibleDays.forEach(d => {
            winTotalScore += d.totalScore;
            winTotalHours += d.totalHours;
        });
        const dailyAvg = visibleDays.length > 0 ? (winTotalScore / visibleDays.length).toFixed(1) : "0.0";

        if (visibleDays.length > 0) {
            const firstD = visibleDays[0];
            const lastD = visibleDays[visibleDays.length - 1];
            rangeBadge.textContent = `${firstD.label} – ${lastD.label} (${visibleDays.length} days) • Total: ${winTotalScore.toFixed(1)} pts • Avg: ${dailyAvg} pts/day`;
        } else {
            rangeBadge.textContent = "No data";
        }

        // Prepare Chart Datasets
        const labels = visibleDays.map(d => d.label);
        let datasets = [];

        if (dayScoreChartForm === "3col") {
            datasets = [
                {
                    type: 'bar',
                    label: 'Morning (04:00–12:00)',
                    data: visibleDays.map(d => d.s1),
                    backgroundColor: 'rgba(56, 189, 248, 0.85)',
                    borderColor: '#38bdf8',
                    borderWidth: 1,
                    borderRadius: 3
                },
                {
                    type: 'bar',
                    label: 'Afternoon (12:00–18:00)',
                    data: visibleDays.map(d => d.s2),
                    backgroundColor: 'rgba(52, 211, 153, 0.85)',
                    borderColor: '#34d399',
                    borderWidth: 1,
                    borderRadius: 3
                },
                {
                    type: 'bar',
                    label: 'Night (18:00–04:00)',
                    data: visibleDays.map(d => d.s3),
                    backgroundColor: 'rgba(167, 139, 250, 0.85)',
                    borderColor: '#a78bfa',
                    borderWidth: 1,
                    borderRadius: 3
                }
            ];
        } else {
            datasets = [
                {
                    type: 'bar',
                    label: 'Total Daily Score (pts)',
                    data: visibleDays.map(d => d.totalScore),
                    backgroundColor: 'rgba(139, 92, 246, 0.75)',
                    borderColor: '#8b5cf6',
                    borderWidth: 1.5,
                    borderRadius: 4
                },
                {
                    type: 'line',
                    label: 'Trend Line',
                    data: visibleDays.map(d => d.totalScore),
                    borderColor: '#ec4899',
                    backgroundColor: 'transparent',
                    borderWidth: 2,
                    tension: 0.25,
                    pointRadius: 3
                }
            ];
        }

        renderQuickChart({
            type: 'bar',
            data: {
                labels: labels,
                datasets: datasets
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
                            title: function(items) {
                                const idx = items[0]?.dataIndex;
                                const d = visibleDays[idx];
                                return d ? d.fullLabel : '';
                            },
                            footer: function(items) {
                                const idx = items[0]?.dataIndex;
                                const d = visibleDays[idx];
                                if (!d) return '';
                                return `Day Total: ${d.totalScore.toFixed(2)} pts (${d.totalHours}h tracked)\nMorning: ${d.s1.toFixed(1)} pts (${d.s1H}h) • Afternoon: ${d.s2.toFixed(1)} pts (${d.s2H}h) • Night: ${d.s3.toFixed(1)} pts (${d.s3H}h)`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        title: { display: true, text: 'Day', font: { weight: 'bold' } },
                        ticks: { maxTicksLimit: Math.min(16, visibleDays.length) }
                    },
                    y: {
                        beginAtZero: true,
                        title: { display: true, text: 'Productivity Score Points (pts)', font: { weight: 'bold' } }
                    }
                }
            }
        }, chartCanvasContainer);

        // Render Table
        const tableDetails = tableSection.createEl("details");
        tableDetails.open = dayScoreTableOpen;
        tableDetails.style.marginTop = "14px";
        tableDetails.style.paddingTop = "10px";
        tableDetails.style.borderTop = "1px solid var(--background-modifier-border)";

        tableDetails.addEventListener("toggle", () => {
            dayScoreTableOpen = tableDetails.open;
        });

        const summary = tableDetails.createEl("summary");
        summary.style.fontWeight = "bold";
        summary.style.fontSize = "13px";
        summary.style.cursor = "pointer";
        summary.style.display = "flex";
        summary.style.justifyContent = "space-between";
        summary.style.alignItems = "center";
        summary.title = "Click to toggle score of day table";

        summary.innerHTML = `
            <span>📋 Score of Day Table (${visibleDays.length} days in view)</span>
            <span style="font-size: 11px; opacity: 0.7; font-weight: normal;">(click to collapse/expand)</span>
        `;

        const tblWrap = tableDetails.createDiv();
        tblWrap.style.marginTop = "10px";
        tblWrap.style.overflowX = "auto";

        const tbl = tblWrap.createEl("table");
        tbl.style.width = "100%";
        tbl.style.fontSize = "12px";
        tbl.style.borderCollapse = "collapse";

        tbl.innerHTML = `
            <thead>
                <tr style="border-bottom: 2px solid var(--background-modifier-border); text-align: left; opacity: 0.85;">
                    <th style="padding: 6px 8px;">Date</th>
                    <th style="padding: 6px 8px; color: #38bdf8;">🌅 Sum 04:00–12:00</th>
                    <th style="padding: 6px 8px; color: #34d399;">☀️ Sum 12:00–18:00</th>
                    <th style="padding: 6px 8px; color: #a78bfa;">🌙 Sum 18:00–04:00</th>
                    <th style="padding: 6px 8px; font-weight: bold; color: var(--text-accent);">🏆 Day Total Score</th>
                    <th style="padding: 6px 8px;">⏱️ Tracked Time</th>
                </tr>
            </thead>
            <tbody>
                ${visibleDays.slice().reverse().map(d => `
                    <tr style="border-bottom: 1px solid var(--background-modifier-border);">
                        <td style="padding: 6px 8px; font-weight: bold;">${d.dateStr} <span style="font-size: 10px; opacity: 0.6;">(${d.label})</span></td>
                        <td style="padding: 6px 8px;"><b>${d.s1.toFixed(2)} pts</b> <span style="font-size: 11px; opacity: 0.7;">(${d.s1H}h)</span></td>
                        <td style="padding: 6px 8px;"><b>${d.s2.toFixed(2)} pts</b> <span style="font-size: 11px; opacity: 0.7;">(${d.s2H}h)</span></td>
                        <td style="padding: 6px 8px;"><b>${d.s3.toFixed(2)} pts</b> <span style="font-size: 11px; opacity: 0.7;">(${d.s3H}h)</span></td>
                        <td style="padding: 6px 8px; font-weight: bold; font-size: 13px; color: #8b5cf6;">${d.totalScore.toFixed(2)} pts</td>
                        <td style="padding: 6px 8px; opacity: 0.85;">${d.totalHours} hrs</td>
                    </tr>
                `).join("")}
            </tbody>
        `;
    }

    // Navigation Click Listeners
    oldestBtn.addEventListener("click", () => {
        const allDays = calculateDailyScoreData();
        const eff = (dayScoreWindowSize === "all") ? allDays.length : Math.min(dayScoreWindowSize, allDays.length);
        dayScoreOffset = Math.max(0, allDays.length - eff);
        renderView();
    });

    prevBtn.addEventListener("click", () => {
        const allDays = calculateDailyScoreData();
        const eff = (dayScoreWindowSize === "all") ? allDays.length : Math.min(dayScoreWindowSize, allDays.length);
        const maxO = Math.max(0, allDays.length - eff);
        dayScoreOffset = Math.min(maxO, dayScoreOffset + 7);
        renderView();
    });

    floatLeft.addEventListener("click", () => {
        const allDays = calculateDailyScoreData();
        const eff = (dayScoreWindowSize === "all") ? allDays.length : Math.min(dayScoreWindowSize, allDays.length);
        const maxO = Math.max(0, allDays.length - eff);
        dayScoreOffset = Math.min(maxO, dayScoreOffset + 7);
        renderView();
    });

    nextBtn.addEventListener("click", () => {
        dayScoreOffset = Math.max(0, dayScoreOffset - 7);
        renderView();
    });

    floatRight.addEventListener("click", () => {
        dayScoreOffset = Math.max(0, dayScoreOffset - 7);
        renderView();
    });

    latestBtn.addEventListener("click", () => {
        dayScoreOffset = 0;
        renderView();
    });

    chartWrapper.addEventListener("wheel", (e) => {
        e.preventDefault();
        const allDays = calculateDailyScoreData();
        const total = allDays.length;
        if (total === 0) return;

        const isZoom = (dayScoreWheelMode === "zoom" && !e.shiftKey) || e.ctrlKey || e.metaKey;

        if (isZoom) {
            let cur = (dayScoreWindowSize === "all") ? total : dayScoreWindowSize;
            const step = 2;
            if (e.deltaY < 0) {
                dayScoreWindowSize = Math.max(3, cur - step);
            } else {
                dayScoreWindowSize = Math.min(total, cur + step);
            }
            renderView();
        } else {
            const step = 3;
            const eff = (dayScoreWindowSize === "all") ? total : Math.min(dayScoreWindowSize, total);
            const maxO = Math.max(0, total - eff);
            if (e.deltaY > 0) {
                dayScoreOffset = Math.min(maxO, dayScoreOffset + step);
            } else {
                dayScoreOffset = Math.max(0, dayScoreOffset - step);
            }
            renderView();
        }
    }, { passive: false });

    renderView();
}


renderDashboard();
```