# 📈 Stopwatch — Activity & Project Trends

> [!NAV] 🧭 **Stopwatch Dashboards**
> [[Stopwatch Dashboard|🏠 Overview]] · [[Stopwatch - Timeline & Deep Dive|🔍 Timeline & Sessions]] · [[Stopwatch - Network Performance|🌐 Network]] · **📈 Activity & Trends** · [[Stopwatch - Productivity Scores|🏆 Productivity Scores]] · [[Stopwatch - Mood & Feelings|😊 Mood & Feelings]]


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
// 1. Locate and Parse Stopwatch Log
// ==========================================
const stopwatchFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "stopwatch log.md");

if (!stopwatchFile) {
    dv.paragraph("⚠️ *Could not find `Stopwatch Log.md` in your vault.*");
    return;
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
                    durationText,
                    scorePerHour: 1.0,
                    score: minutes / 60
                });
            }
        }
    }
    return records;
}

const allRecords = await getCachedParsedLog(stopwatchFile, "stopwatch_raw", parseStopwatchText) || [];

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
    const isCurrent = ("trends" === item.id);
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

let hourlyActivityScope = "7d";
let trendGranularity = "day";
let trendWindowSize = 14;
let trendOffset = 0;
let trendWheelMode = "pan";

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
        stopwatch: allRecords.filter(matchFn)
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

    // 1. Time of Day Breakdown (Hourly Stacked Bar)
    renderHourlyViewSection();

    // 2. Daily Trend (Hours by Project)
    renderTrendSection();
}

function renderHourlyViewSection() {
    if (allRecords.length === 0) return;
    const { stopwatch: records } = getFilteredRecords(activeFilter);

    const hourlyCard = chartSection.createDiv();
    hourlyCard.style.padding = "18px";
    hourlyCard.style.borderRadius = "8px";
    hourlyCard.style.backgroundColor = "var(--background-secondary)";
    hourlyCard.style.border = "1px solid var(--background-modifier-border)";
    hourlyCard.style.marginBottom = "30px";

    const hourlyHeaderRow = hourlyCard.createDiv();
    hourlyHeaderRow.style.display = "flex";
    hourlyHeaderRow.style.justifyContent = "space-between";
    hourlyHeaderRow.style.alignItems = "center";
    hourlyHeaderRow.style.flexWrap = "wrap";
    hourlyHeaderRow.style.gap = "10px";
    hourlyHeaderRow.style.marginBottom = "8px";

    const titleBox = hourlyHeaderRow.createDiv();
    const hourlyHeading = titleBox.createEl("h3", { text: "🕒 Activity by Time of Day" });
    hourlyHeading.style.margin = "0 0 4px 0";

    const hourlySubtitle = titleBox.createEl("p");
    hourlySubtitle.style.opacity = "0.7";
    hourlySubtitle.style.fontSize = "12px";
    hourlySubtitle.style.margin = "0";

    const scopeBox = hourlyHeaderRow.createDiv();
    scopeBox.style.display = "flex";
    scopeBox.style.alignItems = "center";
    scopeBox.style.gap = "4px";

    const scopeLabel = scopeBox.createEl("span", { text: "Scope:" });
    scopeLabel.style.fontSize = "11px";
    scopeLabel.style.opacity = "0.7";
    scopeLabel.style.fontWeight = "bold";

    const activeFilterLabel = filters.find(f => f.id === activeFilter)?.label || "Active Filter";
    const hourlyScopeOptions = [
        { id: "7d", label: "🔄 Last 7 Days" },
        { id: "30d", label: "📅 Last 30 Days" },
        { id: "filter", label: `📅 ${activeFilterLabel}` },
        { id: "month", label: "📆 This Month" },
        { id: "all", label: "🌐 All Time" }
    ];

    const hourlyScopeBtns = [];
    const hourlyChartContainer = hourlyCard.createDiv();

    function renderHourlyView() {
        hourlyChartContainer.innerHTML = "";

        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const cutoff7d = new Date(today);
        cutoff7d.setDate(today.getDate() - 6);
        const cutoff30d = new Date(today);
        cutoff30d.setDate(today.getDate() - 29);
        const startOfMonth = new Date(now.getFullYear(), now.getMonth(), 1);

        let curHourlyRecords = records;
        let curScopeName = activeFilterLabel;
        let isMinutes = (activeFilter === "today" && hourlyActivityScope === "filter");

        if (hourlyActivityScope === "7d") {
            curHourlyRecords = allRecords.filter(r => r.dateObj >= cutoff7d);
            curScopeName = "Last 7 Days";
            isMinutes = false;
        } else if (hourlyActivityScope === "30d") {
            curHourlyRecords = allRecords.filter(r => r.dateObj >= cutoff30d);
            curScopeName = "Last 30 Days";
            isMinutes = false;
        } else if (hourlyActivityScope === "month") {
            curHourlyRecords = allRecords.filter(r => r.dateObj >= startOfMonth);
            curScopeName = "This Month";
            isMinutes = false;
        } else if (hourlyActivityScope === "all") {
            curHourlyRecords = allRecords;
            curScopeName = "All Time";
            isMinutes = false;
        }

        if (hourlyActivityScope === "filter" && activeFilter === "week") {
            hourlySubtitle.textContent = `Total hours worked across ${curScopeName} (${curHourlyRecords.length} sessions). Tip: Monday starts fresh; select "Last 7 Days" to view the rolling 7-day pattern.`;
        } else {
            hourlySubtitle.textContent = isMinutes 
                ? `Minutes worked during each hour of the day by project (${curHourlyRecords.length} sessions).`
                : `Total hours worked across ${curScopeName} by time of day and project (${curHourlyRecords.length} sessions).`;
        }

        const curProjectLabels = Array.from(new Set(curHourlyRecords.map(r => r.project)));
        const hours24 = Array.from({ length: 24 }, (_, i) => `${String(i).padStart(2, '0')}:00`);

        const projectHourly = {};
        for (const proj of curProjectLabels) {
            projectHourly[proj] = new Array(24).fill(0);
        }

        for (const r of curHourlyRecords) {
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

        const hourlyDatasets = curProjectLabels.map(proj => {
            const rawMinutes = projectHourly[proj];
            const dataValues = isMinutes 
                ? rawMinutes 
                : rawMinutes.map(m => +(m / 60).toFixed(1));

            return {
                label: proj,
                data: dataValues,
                backgroundColor: projectColorMap[proj] || '#8b5cf6',
                stack: 'timeOfDay'
            };
        });

        renderQuickChart({
            type: 'bar',
            data: {
                labels: hours24,
                datasets: hourlyDatasets
            },
            options: {
                responsive: true,
                scales: {
                    x: {
                        stacked: true,
                        title: { display: true, text: 'Hour of Day' }
                    },
                    y: {
                        stacked: true,
                        beginAtZero: true,
                        title: { display: true, text: isMinutes ? 'Minutes' : 'Hours' }
                    }
                }
            }
        }, hourlyChartContainer);
    }

    hourlyScopeOptions.forEach(opt => {
        const btn = scopeBox.createEl("button", { text: opt.label });
        btn.style.padding = "3px 8px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        hourlyScopeBtns.push({ id: opt.id, btn });

        btn.addEventListener("click", () => {
            hourlyActivityScope = opt.id;
            updateHourlyScopeBtns();
            renderHourlyView();
        });
    });

    function updateHourlyScopeBtns() {
        hourlyScopeBtns.forEach(({ id, btn }) => {
            const active = (id === hourlyActivityScope);
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });
    }

    updateHourlyScopeBtns();
    renderHourlyView();
}

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

renderDashboard();
```