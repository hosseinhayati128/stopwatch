# 😊 Stopwatch — Daily Mood & Feelings

> [!NAV] 🧭 **Stopwatch Dashboards**
> [[Stopwatch Dashboard|🏠 Overview]] · [[Stopwatch - Timeline & Deep Dive|🔍 Timeline & Sessions]] · [[Stopwatch - Network Performance|🌐 Network]] · [[Stopwatch - Activity & Trends|📈 Activity & Trends]] · [[Stopwatch - Productivity Scores|🏆 Productivity Scores]] · **😊 Mood & Feelings**


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
// 1. Locate and Parse Mood Log
// ==========================================
const moodFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "mood log.md");

if (!moodFile) {
    dv.paragraph("⚠️ *Could not find `Mood Log.md` in your vault.*");
    return;
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

const allMoodRecords = await getCachedParsedLog(moodFile, "mood", parseMoodText) || [];

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
    const isCurrent = ("mood" === item.id);
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

let moodChartForm = "3col";
let moodWindowSize = 14;
let moodOffset = 0;
let moodWheelMode = "pan";
let moodTableOpen = true;

function renderDashboard() {
    chartSection.innerHTML = "";
    renderDailyMoodSection();
}

function renderDailyMoodSection() {
    if (allMoodRecords.length === 0) return;

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
    const heading = titleBox.createEl("h3", { text: "😊 Daily Mood & Feelings (04-12, 12-18, 18-04)" });
    heading.style.margin = "0 0 4px 0";

    const sub = titleBox.createEl("p", {
        text: "Average mood ratings (1.0–10.0) partitioned into 3 daily waking periods: Morning (04:00–12:00), Afternoon (12:00–18:00), and Night (18:00–04:00)."
    });
    sub.style.fontSize = "11px";
    sub.style.opacity = "0.7";
    sub.style.margin = "0";

    const topControls = headerRow.createDiv();
    topControls.style.display = "flex";
    topControls.style.alignItems = "center";
    topControls.style.gap = "8px";
    topControls.style.flexWrap = "wrap";

    // Form Mode Toggle Buttons: [ 📊 3 Columns ] [ 📈 Daily Mean ] [ 🕯️ Mood Candles ]
    const formContainer = topControls.createDiv();
    formContainer.style.display = "flex";
    formContainer.style.alignItems = "center";
    formContainer.style.gap = "4px";

    const formLabel = formContainer.createEl("span", { text: "Chart Form:" });
    formLabel.style.fontSize = "12px";
    formLabel.style.fontWeight = "bold";

    const formOptions = [
        { id: "3col", label: "📊 3 Columns (4-12, 12-18, 18-4)" },
        { id: "mean", label: "📈 Daily Mean" },
        { id: "candle", label: "🕯️ Mood Candles (Min–Max)" }
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
            if (moodChartForm !== opt.id) {
                moodChartForm = opt.id;
                updateFormBtns();
                renderView();
            }
        });
    });

    function updateFormBtns() {
        formBtns.forEach(({ id, btn }) => {
            const active = (id === moodChartForm);
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
        if (moodWheelMode === "pan") {
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
        moodWheelMode = (moodWheelMode === "pan") ? "zoom" : "pan";
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

    // Data Calculation
    function calculateDailyMoodData() {
        const dayMap = {};
        function initMood(d) {
            if (!dayMap[d]) dayMap[d] = { s1: [], s2: [], s3: [], all: [], keywords: new Set(), notes: [] };
            return dayMap[d];
        }

        for (const m of allMoodRecords) {
            let day = m.dateStr;
            let slot = 's1';
            if (m.hour >= 4 && m.hour < 12) {
                slot = 's1';
            } else if (m.hour >= 12 && m.hour < 18) {
                slot = 's2';
            } else {
                slot = 's3';
                if (m.hour < 4) day = shiftDate(day, "day", -1);
            }
            const dm = initMood(day);
            dm[slot].push(m.score);
            dm.all.push(m.score);
            if (m.keywords && m.keywords !== '-') dm.keywords.add(m.keywords);
            if (m.notes) dm.notes.push(m.notes);
        }

        const mNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
        const dNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

        return Object.keys(dayMap).sort().map(dateStr => {
            const [y, m, d] = dateStr.split('-').map(Number);
            const dt = new Date(y, m - 1, d);
            const dm = dayMap[dateStr];
            const mean = arr => arr.length ? +(arr.reduce((a, b) => a + b, 0) / arr.length).toFixed(2) : null;

            const m1 = mean(dm.s1);
            const m2 = mean(dm.s2);
            const m3 = mean(dm.s3);
            const dayMean = mean(dm.all);
            const min = dm.all.length ? Math.min(...dm.all) : null;
            const max = dm.all.length ? Math.max(...dm.all) : null;

            return {
                dateStr,
                dateObj: dt,
                label: `${mNames[m - 1]} ${d}`,
                fullLabel: `${dNames[dt.getDay()]}, ${mNames[m - 1]} ${d}, ${y}`,
                m1, m2, m3,
                c1: dm.s1.length, c2: dm.s2.length, c3: dm.s3.length,
                dayMean,
                min, max,
                totalReviews: dm.all.length,
                keywords: Array.from(dm.keywords)
            };
        });
    }

    function renderView() {
        chartCanvasContainer.innerHTML = "";
        tableSection.innerHTML = "";

        const allDays = calculateDailyMoodData();
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

            if (moodWindowSize === pv.id) {
                pBtn.style.backgroundColor = "var(--interactive-accent)";
                pBtn.style.color = "var(--text-on-accent)";
                pBtn.style.fontWeight = "bold";
            } else {
                pBtn.style.backgroundColor = "var(--background-modifier-form-field)";
                pBtn.style.color = "var(--text-normal)";
            }

            pBtn.addEventListener("click", () => {
                moodWindowSize = pv.id;
                moodOffset = 0;
                renderView();
            });
        });

        // Window calculation
        const effectiveSize = (moodWindowSize === "all") ? total : Math.min(moodWindowSize, total);
        const maxOffset = Math.max(0, total - effectiveSize);
        if (moodOffset > maxOffset) moodOffset = maxOffset;
        if (moodOffset < 0) moodOffset = 0;

        const startIdx = Math.max(0, total - effectiveSize - moodOffset);
        const endIdx = startIdx + effectiveSize;
        const visibleDays = allDays.slice(startIdx, endIdx);

        // Update Nav button states
        const isAtOldest = (moodOffset >= maxOffset);
        const isAtLatest = (moodOffset === 0);

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
        let sumMeans = 0;
        let countMeans = 0;
        let totalReviewsCount = 0;
        visibleDays.forEach(d => {
            if (d.dayMean != null) {
                sumMeans += d.dayMean;
                countMeans++;
            }
            totalReviewsCount += d.totalReviews;
        });
        const periodAvgMood = countMeans > 0 ? (sumMeans / countMeans).toFixed(2) : "-";

        if (visibleDays.length > 0) {
            const firstD = visibleDays[0];
            const lastD = visibleDays[visibleDays.length - 1];
            rangeBadge.textContent = `${firstD.label} – ${lastD.label} (${visibleDays.length} days) • Avg Mood: ${periodAvgMood} / 10 • ${totalReviewsCount} reviews`;
        } else {
            rangeBadge.textContent = "No mood data";
        }

        // Prepare Chart Datasets
        const labels = visibleDays.map(d => d.label);
        let datasets = [];

        if (moodChartForm === "3col") {
            datasets = [
                {
                    type: 'bar',
                    label: 'Morning (04:00–12:00)',
                    data: visibleDays.map(d => d.m1),
                    backgroundColor: 'rgba(245, 158, 11, 0.85)',
                    borderColor: '#f59e0b',
                    borderWidth: 1,
                    borderRadius: 3
                },
                {
                    type: 'bar',
                    label: 'Afternoon (12:00–18:00)',
                    data: visibleDays.map(d => d.m2),
                    backgroundColor: 'rgba(16, 185, 129, 0.85)',
                    borderColor: '#10b981',
                    borderWidth: 1,
                    borderRadius: 3
                },
                {
                    type: 'bar',
                    label: 'Night (18:00–04:00)',
                    data: visibleDays.map(d => d.m3),
                    backgroundColor: 'rgba(99, 102, 241, 0.85)',
                    borderColor: '#6366f1',
                    borderWidth: 1,
                    borderRadius: 3
                }
            ];
        } else if (moodChartForm === "mean") {
            datasets = [
                {
                    type: 'line',
                    label: 'Daily Average Mood',
                    data: visibleDays.map(d => d.dayMean),
                    borderColor: '#10b981',
                    backgroundColor: 'rgba(16, 185, 129, 0.15)',
                    fill: true,
                    tension: 0.25,
                    borderWidth: 2.5,
                    pointRadius: 4,
                    pointHoverRadius: 6,
                    pointBackgroundColor: visibleDays.map(d => {
                        if (d.dayMean == null) return '#10b981';
                        if (d.dayMean >= 7.0) return '#10b981';
                        if (d.dayMean >= 5.0) return '#fbbf24';
                        return '#ef4444';
                    })
                }
            ];
        } else {
            // Candle chart (Min to Max floating bar with Mean line)
            datasets = [
                {
                    type: 'bar',
                    label: 'Mood Range (Min – Max)',
                    data: visibleDays.map(d => {
                        if (d.min == null || d.max == null) return null;
                        const low = d.min === d.max ? Math.max(1.0, +(d.min - 0.08).toFixed(2)) : d.min;
                        const high = d.min === d.max ? Math.min(10.0, +(d.max + 0.08).toFixed(2)) : d.max;
                        return [low, high];
                    }),
                    backgroundColor: 'rgba(236, 72, 153, 0.35)',
                    borderColor: '#ec4899',
                    borderWidth: 1.5,
                    borderRadius: 4,
                    borderSkipped: false
                },
                {
                    type: 'line',
                    label: 'Daily Mean Marker',
                    data: visibleDays.map(d => d.dayMean),
                    borderColor: '#38bdf8',
                    backgroundColor: '#38bdf8',
                    borderWidth: 2,
                    tension: 0.2,
                    pointRadius: 4,
                    pointHoverRadius: 6
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
                            label: function(context) {
                                const val = context.raw;
                                const lbl = context.dataset.label;
                                if (Array.isArray(val)) {
                                    return ` ${lbl}: ${val[0]} – ${val[1]}`;
                                }
                                if (val == null) return ` ${lbl}: -`;
                                return ` ${lbl}: ${val}`;
                            },
                            footer: function(items) {
                                const idx = items[0]?.dataIndex;
                                const d = visibleDays[idx];
                                if (!d) return '';
                                const kw = d.keywords.length ? `• Keywords: ${d.keywords.join(', ')}` : '';
                                return `Day Mean: ${d.dayMean != null ? d.dayMean : '-'} (${d.totalReviews} reviews)\nMorning: ${d.m1 != null ? d.m1 : '-'} • Afternoon: ${d.m2 != null ? d.m2 : '-'} • Night: ${d.m3 != null ? d.m3 : '-'} ${kw}`;
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
                        min: 1.0,
                        max: 10.0,
                        title: { display: true, text: 'Mood Score (1.0 to 10.0)', font: { weight: 'bold' } }
                    }
                }
            }
        }, chartCanvasContainer);

        // Render Table
        const tableDetails = tableSection.createEl("details");
        tableDetails.open = moodTableOpen;
        tableDetails.style.marginTop = "14px";
        tableDetails.style.paddingTop = "10px";
        tableDetails.style.borderTop = "1px solid var(--background-modifier-border)";

        tableDetails.addEventListener("toggle", () => {
            moodTableOpen = tableDetails.open;
        });

        const summary = tableDetails.createEl("summary");
        summary.style.fontWeight = "bold";
        summary.style.fontSize = "13px";
        summary.style.cursor = "pointer";
        summary.style.display = "flex";
        summary.style.justifyContent = "space-between";
        summary.style.alignItems = "center";
        summary.title = "Click to toggle mood table";

        summary.innerHTML = `
            <span>📋 Mood of Day Table (${visibleDays.length} days in view)</span>
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
                    <th style="padding: 6px 8px; color: #f59e0b;">🌅 Mean 04:00–12:00</th>
                    <th style="padding: 6px 8px; color: #10b981;">☀️ Mean 12:00–18:00</th>
                    <th style="padding: 6px 8px; color: #6366f1;">🌙 Mean 18:00–04:00</th>
                    <th style="padding: 6px 8px; font-weight: bold; color: var(--text-accent);">⭐ Daily Mean</th>
                    <th style="padding: 6px 8px;">📊 Range (Min–Max)</th>
                    <th style="padding: 6px 8px;">💭 Feelings & Keywords</th>
                    <th style="padding: 6px 8px;">Checks</th>
                </tr>
            </thead>
            <tbody>
                ${visibleDays.slice().reverse().map(d => {
                    const meanColor = d.dayMean != null
                        ? (d.dayMean >= 7.0 ? '#10b981' : (d.dayMean >= 5.0 ? '#fbbf24' : '#ef4444'))
                        : 'inherit';
                    return `
                    <tr style="border-bottom: 1px solid var(--background-modifier-border);">
                        <td style="padding: 6px 8px; font-weight: bold;">${d.dateStr} <span style="font-size: 10px; opacity: 0.6;">(${d.label})</span></td>
                        <td style="padding: 6px 8px;">${d.m1 != null ? `<b>${d.m1}</b> <span style="font-size: 10px; opacity: 0.7;">(${d.c1})</span>` : '-'}</td>
                        <td style="padding: 6px 8px;">${d.m2 != null ? `<b>${d.m2}</b> <span style="font-size: 10px; opacity: 0.7;">(${d.c2})</span>` : '-'}</td>
                        <td style="padding: 6px 8px;">${d.m3 != null ? `<b>${d.m3}</b> <span style="font-size: 10px; opacity: 0.7;">(${d.c3})</span>` : '-'}</td>
                        <td style="padding: 6px 8px; font-weight: bold; font-size: 13px; color: ${meanColor};">${d.dayMean != null ? d.dayMean : '-'}</td>
                        <td style="padding: 6px 8px; opacity: 0.85;">${d.min != null && d.max != null ? `${d.min} – ${d.max}` : '-'}</td>
                        <td style="padding: 6px 8px; font-size: 11px; opacity: 0.85;">${d.keywords.length ? d.keywords.join(', ') : '-'}</td>
                        <td style="padding: 6px 8px; opacity: 0.75;">${d.totalReviews}</td>
                    </tr>
                    `;
                }).join("")}
            </tbody>
        `;
    }

    // Navigation Click Listeners
    oldestBtn.addEventListener("click", () => {
        const allDays = calculateDailyMoodData();
        const eff = (moodWindowSize === "all") ? allDays.length : Math.min(moodWindowSize, allDays.length);
        moodOffset = Math.max(0, allDays.length - eff);
        renderView();
    });

    prevBtn.addEventListener("click", () => {
        const allDays = calculateDailyMoodData();
        const eff = (moodWindowSize === "all") ? allDays.length : Math.min(moodWindowSize, allDays.length);
        const maxO = Math.max(0, allDays.length - eff);
        moodOffset = Math.min(maxO, moodOffset + 7);
        renderView();
    });

    floatLeft.addEventListener("click", () => {
        const allDays = calculateDailyMoodData();
        const eff = (moodWindowSize === "all") ? allDays.length : Math.min(moodWindowSize, allDays.length);
        const maxO = Math.max(0, allDays.length - eff);
        moodOffset = Math.min(maxO, moodOffset + 7);
        renderView();
    });

    nextBtn.addEventListener("click", () => {
        moodOffset = Math.max(0, moodOffset - 7);
        renderView();
    });

    floatRight.addEventListener("click", () => {
        moodOffset = Math.max(0, moodOffset - 7);
        renderView();
    });

    latestBtn.addEventListener("click", () => {
        moodOffset = 0;
        renderView();
    });

    chartWrapper.addEventListener("wheel", (e) => {
        e.preventDefault();
        const allDays = calculateDailyMoodData();
        const total = allDays.length;
        if (total === 0) return;

        const isZoom = (moodWheelMode === "zoom" && !e.shiftKey) || e.ctrlKey || e.metaKey;

        if (isZoom) {
            let cur = (moodWindowSize === "all") ? total : moodWindowSize;
            const step = 2;
            if (e.deltaY < 0) {
                moodWindowSize = Math.max(3, cur - step);
            } else {
                moodWindowSize = Math.min(total, cur + step);
            }
            renderView();
        } else {
            const step = 3;
            const eff = (moodWindowSize === "all") ? total : Math.min(moodWindowSize, total);
            const maxO = Math.max(0, total - eff);
            if (e.deltaY > 0) {
                moodOffset = Math.min(maxO, moodOffset + step);
            } else {
                moodOffset = Math.max(0, moodOffset - step);
            }
            renderView();
        }
    }, { passive: false });

    renderView();
}



renderDashboard();
```