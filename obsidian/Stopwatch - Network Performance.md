# 🌐 Stopwatch — Internet & Network Performance

> [!NAV] 🧭 **Stopwatch Dashboards**
> [[Stopwatch Dashboard|🏠 Overview]] · [[Stopwatch - Timeline & Deep Dive|🔍 Timeline & Sessions]] · **🌐 Network** · [[Stopwatch - Activity & Trends|📈 Activity & Trends]] · [[Stopwatch - Productivity Scores|🏆 Productivity Scores]] · [[Stopwatch - Mood & Feelings|😊 Mood & Feelings]]


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
// 1. Locate and Parse Internet Log
// ==========================================
const internetFile = app.vault.getFiles().find(f => f.name.toLowerCase() === "internet log.md");

if (!internetFile) {
    dv.paragraph("⚠️ *Could not find `Internet Log.md` in your vault.*");
    return;
}


function getCleanNetworkName(raw) {
    if (!raw) return "Unknown";
    let s = raw.trim();
    s = s.replace(/^[^\w\s\.-]+/, "").trim();
    if (s.includes(" (Saved)")) s = s.replace(" (Saved)", "");
    return s.trim() || "Unknown";
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

const allInternetRecords = await getCachedParsedLog(internetFile, "internet", parseInternetText) || [];

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
    const isCurrent = ("network" === item.id);
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

let netSelectedWifi = "__all__";
let netAggregation = "1d";
let netWindowSize = 30;
let netOffset = 0;
let netWheelMode = "pan";
let netChartType = "line";
let netShow24hProfile = true;
let netShowRecentLog = false;
let netHourlyScope = "7";
let netShowAvgSpeed = true;
let netShowFluctuations = true;
let netShowLatency = true;
let netProfileResolution = 15;
let netProfileChartType = "bar";

function renderDashboard() {
    chartSection.innerHTML = "";
    renderInternetSection();
}

function renderInternetSection() {
    if (allInternetRecords.length === 0) return;

    // Filter by Wi-Fi network if specified
    const allKnownNetworks = Array.from(new Set(allInternetRecords.map(r => r.cleanNetwork))).filter(Boolean).sort();
    if (netSelectedWifi !== "__all__" && !allKnownNetworks.includes(netSelectedWifi)) {
        netSelectedWifi = "__all__";
    }

    const scopedRecords = (netSelectedWifi === "__all__")
        ? allInternetRecords
        : allInternetRecords.filter(r => r.cleanNetwork === netSelectedWifi);

    if (scopedRecords.length === 0) return;

    const card = chartSection.createDiv();
    card.style.padding = "18px";
    card.style.borderRadius = "8px";
    card.style.backgroundColor = "var(--background-secondary)";
    card.style.border = "1px solid var(--background-modifier-border)";
    card.style.marginBottom = "30px";

    // 1. Header & Controls Row
    const headerRow = card.createDiv();
    headerRow.style.display = "flex";
    headerRow.style.justifyContent = "space-between";
    headerRow.style.alignItems = "flex-start";
    headerRow.style.marginBottom = "12px";
    headerRow.style.flexWrap = "wrap";
    headerRow.style.gap = "10px";

    const titleContainer = headerRow.createDiv();
    const titleEl = titleContainer.createEl("h3", { text: "🌐 Internet Connection & Network Performance" });
    titleEl.style.margin = "0 0 4px 0";

    const subEl = titleContainer.createEl("p", {
        text: "Full timeline from the beginning with pan & zoom. Aggregate by 1h, 6h, 12h, 1d, 1w, 1m or raw checks."
    });
    subEl.style.fontSize = "11px";
    subEl.style.opacity = "0.7";
    subEl.style.margin = "0";

    // Controls container
    const topControls = headerRow.createDiv();
    topControls.style.display = "flex";
    topControls.style.alignItems = "center";
    topControls.style.gap = "8px";
    topControls.style.flexWrap = "wrap";

    // 1.1 Wi-Fi Filter Dropdown
    const wifiContainer = topControls.createDiv();
    wifiContainer.style.display = "flex";
    wifiContainer.style.alignItems = "center";
    wifiContainer.style.gap = "4px";

    const wifiLabel = wifiContainer.createEl("span", { text: "📶 Wi-Fi:" });
    wifiLabel.style.fontSize = "12px";
    wifiLabel.style.fontWeight = "bold";

    const wifiSelect = wifiContainer.createEl("select");
    wifiSelect.style.padding = "4px 8px";
    wifiSelect.style.borderRadius = "4px";
    wifiSelect.style.fontSize = "12px";
    wifiSelect.style.backgroundColor = "var(--background-modifier-form-field)";
    wifiSelect.style.color = "var(--text-normal)";
    wifiSelect.style.border = "1px solid var(--background-modifier-border)";

    const allWifiOpt = wifiSelect.createEl("option", { text: "🌐 All Networks", value: "__all__" });
    if (netSelectedWifi === "__all__") allWifiOpt.selected = true;

    allKnownNetworks.forEach(net => {
        const opt = wifiSelect.createEl("option", { text: `📶 ${net}`, value: net });
        if (net === netSelectedWifi) opt.selected = true;
    });

    wifiSelect.addEventListener("change", () => {
        netSelectedWifi = wifiSelect.value;
        netOffset = 0;
        renderTimelineView();
    });

    // 1.2 Aggregation Granularity Buttons (1h, 6h, 12h, 1d, 1w, 1m, raw)
    const aggContainer = topControls.createDiv();
    aggContainer.style.display = "flex";
    aggContainer.style.alignItems = "center";
    aggContainer.style.gap = "4px";

    const aggLabel = aggContainer.createEl("span", { text: "Interval:" });
    aggLabel.style.fontSize = "12px";
    aggLabel.style.fontWeight = "bold";

    const aggOptions = [
        { id: "1h", label: "1h", defaultSize: 24 },
        { id: "6h", label: "6h", defaultSize: 28 },
        { id: "12h", label: "12h", defaultSize: 30 },
        { id: "1d", label: "1d", defaultSize: 30 },
        { id: "1w", label: "1w", defaultSize: 26 },
        { id: "1m", label: "1m", defaultSize: 12 },
        { id: "raw", label: "⚡ Raw", defaultSize: 50 }
    ];

    const aggBtns = [];
    aggOptions.forEach(opt => {
        const btn = aggContainer.createEl("button", { text: opt.label });
        btn.style.padding = "4px 8px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        aggBtns.push({ id: opt.id, btn, defaultSize: opt.defaultSize });

        btn.addEventListener("click", () => {
            if (netAggregation !== opt.id) {
                netAggregation = opt.id;
                netWindowSize = opt.defaultSize;
                netOffset = 0;
                updateAggBtns();
                renderTimelineView();
            }
        });
    });

    function updateAggBtns() {
        aggBtns.forEach(({ id, btn }) => {
            const active = (id === netAggregation);
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });
    }
    updateAggBtns();

    // 1.3 Chart Type Toggle Button (Line & Area vs Bar)
    const chartTypeBtn = topControls.createEl("button");
    chartTypeBtn.style.padding = "4px 8px";
    chartTypeBtn.style.fontSize = "11px";
    chartTypeBtn.style.borderRadius = "4px";
    chartTypeBtn.style.border = "1px solid var(--background-modifier-border)";
    chartTypeBtn.style.cursor = "pointer";
    chartTypeBtn.title = "Toggle line & area chart or bar chart";

    function updateChartTypeBtn() {
        if (netChartType === "line") {
            chartTypeBtn.textContent = "📈 Line";
            chartTypeBtn.style.backgroundColor = "var(--interactive-accent)";
            chartTypeBtn.style.color = "var(--text-on-accent)";
            chartTypeBtn.style.fontWeight = "bold";
        } else {
            chartTypeBtn.textContent = "📊 Bar";
            chartTypeBtn.style.backgroundColor = "var(--background-modifier-form-field)";
            chartTypeBtn.style.color = "var(--text-normal)";
            chartTypeBtn.style.fontWeight = "normal";
        }
    }
    updateChartTypeBtn();

    chartTypeBtn.addEventListener("click", () => {
        netChartType = (netChartType === "line") ? "bar" : "line";
        updateChartTypeBtn();
        renderTimelineView();
    });

    // 1.4 Wheel Mode Toggle Button
    const wheelModeBtn = topControls.createEl("button");
    wheelModeBtn.style.padding = "4px 8px";
    wheelModeBtn.style.fontSize = "11px";
    wheelModeBtn.style.borderRadius = "4px";
    wheelModeBtn.style.border = "1px solid var(--background-modifier-border)";
    wheelModeBtn.style.cursor = "pointer";
    wheelModeBtn.title = "Toggle mouse wheel: pan horizontally or zoom window size";

    function updateWheelModeBtn() {
        if (netWheelMode === "pan") {
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
        netWheelMode = (netWheelMode === "pan") ? "zoom" : "pan";
        updateWheelModeBtn();
    });

    // 1.5 24h Profile Toggle Button
    const profileToggleBtn = topControls.createEl("button");
    profileToggleBtn.style.padding = "4px 8px";
    profileToggleBtn.style.fontSize = "11px";
    profileToggleBtn.style.borderRadius = "4px";
    profileToggleBtn.style.border = "1px solid var(--background-modifier-border)";
    profileToggleBtn.style.cursor = "pointer";
    profileToggleBtn.title = "Show or hide 24-hour daily hourly profile analysis";

    function updateProfileToggleBtn() {
        profileToggleBtn.textContent = "🕒 24h Profile";
        if (netShow24hProfile) {
            profileToggleBtn.style.backgroundColor = "var(--interactive-accent)";
            profileToggleBtn.style.color = "var(--text-on-accent)";
            profileToggleBtn.style.fontWeight = "bold";
        } else {
            profileToggleBtn.style.backgroundColor = "var(--background-modifier-form-field)";
            profileToggleBtn.style.color = "var(--text-normal)";
            profileToggleBtn.style.fontWeight = "normal";
        }
    }
    updateProfileToggleBtn();

    profileToggleBtn.addEventListener("click", () => {
        netShow24hProfile = !netShow24hProfile;
        updateProfileToggleBtn();
        profileContainer.style.display = netShow24hProfile ? "block" : "none";
        if (netShow24hProfile) updateHourlyProfileView();
    });

    // 1.6 Recent Log Table Toggle Button
    const logToggleBtn = topControls.createEl("button");
    logToggleBtn.style.padding = "4px 8px";
    logToggleBtn.style.fontSize = "11px";
    logToggleBtn.style.borderRadius = "4px";
    logToggleBtn.style.border = "1px solid var(--background-modifier-border)";
    logToggleBtn.style.cursor = "pointer";
    logToggleBtn.title = "Show or hide recent network check log table";

    function updateLogToggleBtn() {
        logToggleBtn.textContent = "📋 Recent Log";
        if (netShowRecentLog) {
            logToggleBtn.style.backgroundColor = "var(--interactive-accent)";
            logToggleBtn.style.color = "var(--text-on-accent)";
            logToggleBtn.style.fontWeight = "bold";
        } else {
            logToggleBtn.style.backgroundColor = "var(--background-modifier-form-field)";
            logToggleBtn.style.color = "var(--text-normal)";
            logToggleBtn.style.fontWeight = "normal";
        }
    }
    updateLogToggleBtn();

    logToggleBtn.addEventListener("click", () => {
        netShowRecentLog = !netShowRecentLog;
        updateLogToggleBtn();
        logContainer.style.display = netShowRecentLog ? "block" : "none";
    });

    // 2. Navigation Bar & Presets Container
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

    const presetsLabel = presetsContainer.createEl("span", { text: "Window:" });
    presetsLabel.style.fontSize = "11px";
    presetsLabel.style.opacity = "0.7";

    // 3. Dynamic KPI Cards Bar for Visible Window
    const kpiBar = card.createDiv();
    kpiBar.style.display = "grid";
    kpiBar.style.gridTemplateColumns = "repeat(auto-fit, minmax(130px, 1fr))";
    kpiBar.style.gap = "10px";
    kpiBar.style.padding = "12px";
    kpiBar.style.borderRadius = "6px";
    kpiBar.style.backgroundColor = "var(--background-modifier-form-field)";
    kpiBar.style.marginBottom = "14px";
    kpiBar.style.textAlign = "center";

    // 4. Chart Wrapper with Floating Arrows
    const chartWrapper = card.createDiv();
    chartWrapper.style.position = "relative";
    chartWrapper.style.minHeight = "280px";
    chartWrapper.style.marginBottom = "18px";

    const floatLeft = chartWrapper.createEl("button", { text: "◀" });
    floatLeft.title = "Scroll to earlier checks (or use mouse wheel)";
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
    floatRight.title = "Scroll to newer checks (or use mouse wheel)";
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

    // 5. Data Generation & Period Resolution Helper
    function getAllNetPeriods(granularity, records) {
        if (!records || records.length === 0) return [];
        let minTime = Infinity;
        let maxTime = -Infinity;
        for (const r of records) {
            const rTime = r.timestamp || (r.dateObj ? new Date(r.dateObj.getFullYear(), r.dateObj.getMonth(), r.dateObj.getDate(), r.hour || 0, r.minute || 0).getTime() : null);
            if (rTime) {
                if (rTime < minTime) minTime = rTime;
                if (rTime > maxTime) maxTime = rTime;
            }
        }
        if (minTime === Infinity) return [];

        const now = new Date();
        const nowTime = now.getTime();
        if (nowTime > maxTime) maxTime = nowTime;

        const minDate = new Date(minTime);
        const maxDate = new Date(maxTime);

        const mNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
        const fullMNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
        const dNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

        const periods = [];

        if (granularity === "1m") {
            const cur = new Date(minDate.getFullYear(), minDate.getMonth(), 1);
            const end = new Date(maxDate.getFullYear(), maxDate.getMonth(), 1);
            while (cur <= end) {
                const y = cur.getFullYear();
                const m = cur.getMonth();
                const key = `${y}-${String(m + 1).padStart(2, '0')}`;
                periods.push({
                    key,
                    label: `${mNames[m]} ${y}`,
                    fullLabel: `${fullMNames[m]} ${y}`,
                    unit: '1m'
                });
                cur.setMonth(cur.getMonth() + 1);
            }
        } else if (granularity === "1w") {
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
                if (target.getDay() !== 4) target.setMonth(0, 1 + ((4 - target.getDay()) + 7) % 7);
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
                periods.push({
                    key,
                    label: `W${wNum} (${mNames[cur.getMonth()]} ${cur.getDate()})`,
                    fullLabel: `Week ${wNum}: ${mNames[cur.getMonth()]} ${cur.getDate()} – ${mNames[sun.getMonth()]} ${sun.getDate()}, ${cur.getFullYear()}`,
                    unit: '1w'
                });
                cur.setDate(cur.getDate() + 7);
            }
        } else if (granularity === "1d") {
            const cur = new Date(minDate.getFullYear(), minDate.getMonth(), minDate.getDate());
            const end = new Date(maxDate.getFullYear(), maxDate.getMonth(), maxDate.getDate());
            while (cur <= end) {
                const y = cur.getFullYear();
                const m = String(cur.getMonth() + 1).padStart(2, '0');
                const d = String(cur.getDate()).padStart(2, '0');
                const key = `${y}-${m}-${d}`;
                periods.push({
                    key,
                    label: `${mNames[cur.getMonth()]} ${cur.getDate()}`,
                    fullLabel: `${dNames[cur.getDay()]}, ${mNames[cur.getMonth()]} ${cur.getDate()}, ${y}`,
                    unit: '1d'
                });
                cur.setDate(cur.getDate() + 1);
            }
        } else if (granularity === "12h") {
            const cur = new Date(minDate.getFullYear(), minDate.getMonth(), minDate.getDate());
            const end = new Date(maxDate.getFullYear(), maxDate.getMonth(), maxDate.getDate());
            while (cur <= end) {
                const y = cur.getFullYear();
                const m = String(cur.getMonth() + 1).padStart(2, '0');
                const d = String(cur.getDate()).padStart(2, '0');
                const dayStr = `${y}-${m}-${d}`;
                periods.push({
                    key: `${dayStr} AM`,
                    label: `${mNames[cur.getMonth()]} ${cur.getDate()} AM`,
                    fullLabel: `${dNames[cur.getDay()]}, ${mNames[cur.getMonth()]} ${cur.getDate()} (00:00–12:00)`,
                    unit: '12h'
                });
                periods.push({
                    key: `${dayStr} PM`,
                    label: `${mNames[cur.getMonth()]} ${cur.getDate()} PM`,
                    fullLabel: `${dNames[cur.getDay()]}, ${mNames[cur.getMonth()]} ${cur.getDate()} (12:00–24:00)`,
                    unit: '12h'
                });
                cur.setDate(cur.getDate() + 1);
            }
        } else if (granularity === "6h") {
            const blockShort = ["00-06", "06-12", "12-18", "18-24"];
            const blockNames = ["00:00–06:00", "06:00–12:00", "12:00–18:00", "18:00–24:00"];
            const cur = new Date(minDate.getFullYear(), minDate.getMonth(), minDate.getDate());
            const end = new Date(maxDate.getFullYear(), maxDate.getMonth(), maxDate.getDate());
            while (cur <= end) {
                const y = cur.getFullYear();
                const m = String(cur.getMonth() + 1).padStart(2, '0');
                const d = String(cur.getDate()).padStart(2, '0');
                const dayStr = `${y}-${m}-${d}`;
                for (let b = 0; b < 4; b++) {
                    periods.push({
                        key: `${dayStr} ${blockShort[b]}`,
                        label: `${mNames[cur.getMonth()]} ${cur.getDate()} ${blockShort[b]}`,
                        fullLabel: `${dNames[cur.getDay()]}, ${mNames[cur.getMonth()]} ${cur.getDate()} (${blockNames[b]})`,
                        unit: '6h'
                    });
                }
                cur.setDate(cur.getDate() + 1);
            }
        } else if (granularity === "1h") {
            const cur = new Date(minDate.getFullYear(), minDate.getMonth(), minDate.getDate());
            const end = new Date(maxDate.getFullYear(), maxDate.getMonth(), maxDate.getDate());
            while (cur <= end) {
                const y = cur.getFullYear();
                const m = String(cur.getMonth() + 1).padStart(2, '0');
                const d = String(cur.getDate()).padStart(2, '0');
                const dayStr = `${y}-${m}-${d}`;
                for (let h = 0; h < 24; h++) {
                    const hStr = `${String(h).padStart(2, '0')}:00`;
                    periods.push({
                        key: `${dayStr} ${hStr}`,
                        label: `${mNames[cur.getMonth()]} ${cur.getDate()} ${hStr}`,
                        fullLabel: `${dNames[cur.getDay()]}, ${mNames[cur.getMonth()]} ${cur.getDate()} at ${hStr}`,
                        unit: '1h'
                    });
                }
                cur.setDate(cur.getDate() + 1);
            }
        } else if (granularity === "raw") {
            records.forEach((r, idx) => {
                periods.push({
                    key: `raw_${idx}`,
                    label: `${r.dateStr.slice(5)} ${r.time}`,
                    fullLabel: `${r.dateStr} ${r.time} • ${r.cleanNetwork}`,
                    unit: 'raw',
                    rawRecord: r
                });
            });
        }

        return periods;
    }

    function aggregateNetData(periods, granularity, records) {
        const periodMap = new Map();
        periods.forEach(p => periodMap.set(p.key, []));

        function getWeekKey(d) {
            const target = new Date(d.valueOf());
            const dayNr = (d.getDay() + 6) % 7;
            target.setDate(target.getDate() - dayNr + 3);
            const firstThursday = target.valueOf();
            target.setMonth(0, 1);
            if (target.getDay() !== 4) target.setMonth(0, 1 + ((4 - target.getDay()) + 7) % 7);
            const wn = 1 + Math.ceil((firstThursday - target) / 604800000);
            return `${d.getFullYear()}-W${String(wn).padStart(2, '0')}`;
        }

        const blockShort = ["00-06", "06-12", "12-18", "18-24"];

        if (granularity === "raw") {
            periods.forEach(p => {
                if (p.rawRecord) periodMap.get(p.key).push(p.rawRecord);
            });
        } else {
            records.forEach(r => {
                let key = null;
                if (granularity === "1m") {
                    key = r.dateStr.slice(0, 7);
                } else if (granularity === "1w") {
                    key = getWeekKey(r.dateObj);
                } else if (granularity === "1d") {
                    key = r.dateStr;
                } else if (granularity === "12h") {
                    key = `${r.dateStr} ${Math.floor(r.hour / 12) === 0 ? 'AM' : 'PM'}`;
                } else if (granularity === "6h") {
                    key = `${r.dateStr} ${blockShort[Math.floor(r.hour / 6)]}`;
                } else if (granularity === "1h") {
                    key = `${r.dateStr} ${String(r.hour).padStart(2, '0')}:00`;
                }

                if (key && periodMap.has(key)) {
                    periodMap.get(key).push(r);
                }
            });
        }

        const stats = {};
        periods.forEach(p => {
            const recs = periodMap.get(p.key) || [];
            const total = recs.length;
            if (total === 0) {
                stats[p.key] = {
                    avgSpeed: null, maxSpeed: null, minSpeed: null,
                    avgPing: null, uptimePct: null, totalChecks: 0,
                    onlineChecks: 0, slowChecks: 0, offlineChecks: 0,
                    networks: [], records: []
                };
                return;
            }

            const speeds = recs.map(r => r.isOffline ? 0 : r.speedMbps).filter(v => v != null);
            const avgSpeed = speeds.length ? +(speeds.reduce((a, b) => a + b, 0) / speeds.length).toFixed(1) : null;
            const maxSpeed = speeds.length ? +Math.max(...speeds).toFixed(1) : null;
            const minSpeed = speeds.length ? +Math.min(...speeds).toFixed(1) : null;

            const pings = recs.map(r => r.pingMs).filter(v => v != null && v > 0);
            const avgPing = pings.length ? Math.round(pings.reduce((a, b) => a + b, 0) / pings.length) : null;

            const online = recs.filter(r => r.isOnline).length;
            const slow = recs.filter(r => r.isSlow).length;
            const offline = recs.filter(r => r.isOffline).length;
            const uptimePct = Math.round((online / total) * 100);

            const nets = Array.from(new Set(recs.map(r => r.cleanNetwork))).filter(Boolean);

            stats[p.key] = {
                avgSpeed, maxSpeed, minSpeed,
                avgPing, uptimePct, totalChecks: total,
                onlineChecks: online, slowChecks: slow, offlineChecks: offline,
                networks: nets, records: recs
            };
        });

        return stats;
    }

    function getNetStep() {
        if (netAggregation === "1h") return 6;
        if (netAggregation === "6h" || netAggregation === "12h") return 4;
        if (netAggregation === "1d") return 7;
        if (netAggregation === "1w") return 4;
        if (netAggregation === "1m") return 3;
        if (netAggregation === "raw") return 15;
        return 5;
    }

    // 6. Main Timeline Renderer
    function renderTimelineView() {
        chartCanvasContainer.innerHTML = "";

        // Resolve current scoped records based on netSelectedWifi
        const currentScopedRecords = (netSelectedWifi === "__all__")
            ? allInternetRecords
            : allInternetRecords.filter(r => r.cleanNetwork === netSelectedWifi);

        if (currentScopedRecords.length === 0) {
            chartCanvasContainer.createEl("p", {
                text: `No network records found for "${netSelectedWifi}".`
            }).style.opacity = "0.6";
            return;
        }

        // Calculate all periods and statistics
        const allPeriods = getAllNetPeriods(netAggregation, currentScopedRecords);
        const statsMap = aggregateNetData(allPeriods, netAggregation, currentScopedRecords);
        const total = allPeriods.length;

        // Render Presets
        presetsContainer.querySelectorAll("button").forEach(b => b.remove());
        let presets = [];
        if (netAggregation === "1h") {
            presets = [
                { id: 12, label: "12h" },
                { id: 24, label: "24h" },
                { id: 48, label: "48h" },
                { id: 168, label: "7D" },
                { id: "all", label: "All" }
            ];
        } else if (netAggregation === "6h") {
            presets = [
                { id: 14, label: "3.5D" },
                { id: 28, label: "7D" },
                { id: 56, label: "14D" },
                { id: 120, label: "30D" },
                { id: "all", label: "All" }
            ];
        } else if (netAggregation === "12h") {
            presets = [
                { id: 14, label: "7D" },
                { id: 30, label: "15D" },
                { id: 60, label: "30D" },
                { id: "all", label: "All" }
            ];
        } else if (netAggregation === "1d") {
            presets = [
                { id: 7, label: "7D" },
                { id: 14, label: "14D" },
                { id: 30, label: "30D" },
                { id: 90, label: "90D" },
                { id: "all", label: "All" }
            ];
        } else if (netAggregation === "1w") {
            presets = [
                { id: 4, label: "4W" },
                { id: 12, label: "12W" },
                { id: 26, label: "26W" },
                { id: 52, label: "52W (1 Year)" },
                { id: "all", label: "All" }
            ];
        } else if (netAggregation === "1m") {
            presets = [
                { id: 3, label: "3M" },
                { id: 6, label: "6M" },
                { id: 12, label: "12M (1 Year)" },
                { id: 24, label: "24M (2 Years)" },
                { id: "all", label: "All" }
            ];
        } else {
            presets = [
                { id: 25, label: "25" },
                { id: 50, label: "50" },
                { id: 100, label: "100" },
                { id: 250, label: "250" },
                { id: "all", label: "All" }
            ];
        }

        presets.forEach(pv => {
            const pBtn = presetsContainer.createEl("button", { text: pv.label });
            pBtn.style.padding = "3px 7px";
            pBtn.style.fontSize = "11px";
            pBtn.style.borderRadius = "3px";
            pBtn.style.cursor = "pointer";
            pBtn.style.border = "1px solid var(--background-modifier-border)";

            if (netWindowSize === pv.id) {
                pBtn.style.backgroundColor = "var(--interactive-accent)";
                pBtn.style.color = "var(--text-on-accent)";
                pBtn.style.fontWeight = "bold";
            } else {
                pBtn.style.backgroundColor = "var(--background-modifier-form-field)";
                pBtn.style.color = "var(--text-normal)";
            }

            pBtn.addEventListener("click", () => {
                netWindowSize = pv.id;
                netOffset = 0;
                renderTimelineView();
            });
        });

        // Window calculation
        const effectiveSize = (netWindowSize === "all") ? total : Math.min(netWindowSize, total);
        const maxOffset = Math.max(0, total - effectiveSize);
        if (netOffset > maxOffset) netOffset = maxOffset;
        if (netOffset < 0) netOffset = 0;

        const startIdx = Math.max(0, total - effectiveSize - netOffset);
        const endIdx = startIdx + effectiveSize;
        const visiblePeriods = allPeriods.slice(startIdx, endIdx);

        // Update Nav button states
        const isAtOldest = (netOffset >= maxOffset);
        const isAtLatest = (netOffset === 0);

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

        // Aggregate statistics for the visible window
        let winTotalChecks = 0;
        let winOnlineChecks = 0;
        let winSlowChecks = 0;
        let winOfflineChecks = 0;
        const winSpeeds = [];
        const winPings = [];
        const winNetworks = new Set();

        visiblePeriods.forEach(p => {
            const st = statsMap[p.key];
            if (!st || st.totalChecks === 0) return;
            winTotalChecks += st.totalChecks;
            winOnlineChecks += st.onlineChecks;
            winSlowChecks += st.slowChecks;
            winOfflineChecks += st.offlineChecks;
            st.networks.forEach(n => winNetworks.add(n));
            st.records.forEach(r => {
                if (r.isOffline) {
                    winSpeeds.push(0);
                } else if (r.speedMbps != null) {
                    winSpeeds.push(r.speedMbps);
                }
                if (r.pingMs != null && r.pingMs > 0) {
                    winPings.push(r.pingMs);
                }
            });
        });

        const winAvgSpeed = winSpeeds.length ? (winSpeeds.reduce((a, b) => a + b, 0) / winSpeeds.length).toFixed(1) : "-";
        const winPeakSpeed = winSpeeds.length ? Math.max(...winSpeeds).toFixed(1) : "-";
        const winAvgPing = winPings.length ? Math.round(winPings.reduce((a, b) => a + b, 0) / winPings.length) : "-";
        const winUptime = winTotalChecks > 0 ? Math.round((winOnlineChecks / winTotalChecks) * 100) : 0;
        const winNetworksArr = Array.from(winNetworks);

        // Update Range Badge
        if (visiblePeriods.length > 0) {
            const firstP = visiblePeriods[0];
            const lastP = visiblePeriods[visiblePeriods.length - 1];
            const unitLabel = netAggregation === "1m" ? "months" : (netAggregation === "1w" ? "weeks" : (netAggregation === "1d" ? "days" : (netAggregation === "raw" ? "checks" : "intervals")));
            rangeBadge.textContent = `${firstP.label} – ${lastP.label} (${visiblePeriods.length} ${unitLabel}) • Avg: ${winAvgSpeed} Mbps • Ping: ${winAvgPing} ms • ${winUptime}% Uptime`;
        } else {
            rangeBadge.textContent = "No data in range";
        }

        // Update Dynamic KPI Cards
        kpiBar.innerHTML = `
            <div>
                <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Active Network(s)</div>
                <div style="font-size: 14px; font-weight: bold; color: var(--text-accent); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;" title="${winNetworksArr.join(', ') || 'None'}">${netSelectedWifi !== '__all__' ? netSelectedWifi : (winNetworksArr.length === 1 ? winNetworksArr[0] : (winNetworksArr.length ? winNetworksArr.length + ' Networks' : 'None'))}</div>
                <div style="font-size: 11px; opacity: 0.7;">${winTotalChecks} checks in view</div>
            </div>
            <div>
                <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Avg Download Speed</div>
                <div style="font-size: 16px; font-weight: bold; color: #38bdf8;">${winAvgSpeed} ${winAvgSpeed !== '-' ? 'Mbps' : ''}</div>
                <div style="font-size: 11px; opacity: 0.7;">Peak: ${winPeakSpeed} Mbps</div>
            </div>
            <div>
                <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Avg Latency (Ping)</div>
                <div style="font-size: 16px; font-weight: bold; color: ${winAvgPing !== '-' && winAvgPing < 80 ? '#10b981' : (winAvgPing < 200 ? '#fbbf24' : '#ef4444')};">${winAvgPing} ${winAvgPing !== '-' ? 'ms' : ''}</div>
                <div style="font-size: 11px; opacity: 0.7;">${winAvgPing !== '-' && winAvgPing < 50 ? 'Low latency' : 'Cloudflare 1.1.1.1'}</div>
            </div>
            <div>
                <div style="font-size: 10px; opacity: 0.7; text-transform: uppercase;">Connection Uptime</div>
                <div style="font-size: 16px; font-weight: bold; color: ${winUptime >= 90 ? '#10b981' : (winUptime >= 75 ? '#fbbf24' : '#ef4444')};">${winUptime}%</div>
                <div style="font-size: 11px; opacity: 0.7;">${winOnlineChecks}/${winTotalChecks} online (${winSlowChecks} slow, ${winOfflineChecks} off)</div>
            </div>
        `;

        // Prepare Chart Datasets
        const labels = visiblePeriods.map(p => p.label);
        const speedData = visiblePeriods.map(p => {
            const st = statsMap[p.key];
            return st && st.totalChecks > 0 ? st.avgSpeed : null;
        });
        const pingData = visiblePeriods.map(p => {
            const st = statsMap[p.key];
            return st && st.totalChecks > 0 ? st.avgPing : null;
        });

        const isBar = netChartType === "bar";

        const speedDataset = {
            type: isBar ? 'bar' : 'line',
            label: netAggregation === 'raw' ? 'Download Speed (Mbps)' : `Avg Speed (${netAggregation} Mbps)`,
            data: speedData,
            borderColor: '#38bdf8',
            backgroundColor: isBar ? 'rgba(56, 189, 248, 0.65)' : 'rgba(56, 189, 248, 0.12)',
            borderWidth: isBar ? 1 : 2.5,
            fill: !isBar,
            tension: 0.25,
            spanGaps: false,
            yAxisID: 'y',
            pointBackgroundColor: speedData.map((v, i) => {
                const p = visiblePeriods[i];
                const st = statsMap[p?.key];
                if (st && st.offlineChecks > 0 && st.onlineChecks === 0) return '#ef4444';
                return '#38bdf8';
            }),
            pointBorderColor: speedData.map((v, i) => {
                const p = visiblePeriods[i];
                const st = statsMap[p?.key];
                if (st && st.offlineChecks > 0 && st.onlineChecks === 0) return '#ef4444';
                return '#38bdf8';
            }),
            pointRadius: speedData.map((v, i) => {
                if (v == null) return 0;
                const p = visiblePeriods[i];
                const st = statsMap[p?.key];
                if (st && st.offlineChecks > 0 && st.onlineChecks === 0) return 4.0;
                return (visiblePeriods.length > 50) ? 1.5 : 3.0;
            }),
            pointHoverRadius: 6
        };

        const pingDataset = {
            type: 'line',
            label: netAggregation === 'raw' ? 'Ping (ms)' : `Avg Ping (${netAggregation} ms)`,
            data: pingData,
            borderColor: '#fb923c',
            backgroundColor: 'transparent',
            borderDash: [4, 4],
            borderWidth: 1.8,
            tension: 0.2,
            spanGaps: false,
            yAxisID: 'y1',
            pointRadius: pingData.map(v => v == null ? 0 : (visiblePeriods.length > 50 ? 1.5 : 2.5)),
            pointHoverRadius: 6
        };

        renderQuickChart({
            type: 'bar', // Allows mixing bar and line
            data: {
                labels: labels,
                datasets: [speedDataset, pingDataset]
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
                                const p = visiblePeriods[idx];
                                return p ? p.fullLabel : '';
                            },
                            label: function(context) {
                                const isPing = context.dataset.yAxisID === 'y1';
                                const val = context.raw;
                                const idx = context.dataIndex;
                                const p = visiblePeriods[idx];
                                const st = statsMap[p?.key];

                                if (isPing) {
                                    if (val == null) return ` ${context.dataset.label}: - (No ping recorded)`;
                                    return ` ${context.dataset.label}: ${val} ms`;
                                } else {
                                    if (val == null) return ` ${context.dataset.label}: - (No checks recorded)`;
                                    if (val === 0 && st && st.onlineChecks === 0) {
                                        return ` ${context.dataset.label}: 🔴 0.0 Mbps (Offline)`;
                                    }
                                    let str = ` ${context.dataset.label}: ${val} Mbps`;
                                    if (st && st.minSpeed != null && st.maxSpeed != null && st.minSpeed !== st.maxSpeed) {
                                        str += ` (Min: ${st.minSpeed}, Peak: ${st.maxSpeed})`;
                                    }
                                    return str;
                                }
                            },
                            footer: function(items) {
                                const idx = items[0]?.dataIndex;
                                const p = visiblePeriods[idx];
                                const st = statsMap[p?.key];
                                if (!st || st.totalChecks === 0) return 'No checks recorded in this period';
                                const netsStr = st.networks.length ? `• Wi-Fi: ${st.networks.join(', ')}` : '';
                                return `${st.totalChecks} checks (${st.uptimePct}% uptime, ${st.offlineChecks} offline) ${netsStr}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        ticks: {
                            maxTicksLimit: Math.min(16, visiblePeriods.length)
                        }
                    },
                    y: {
                        beginAtZero: true,
                        title: { display: true, text: 'Download Speed (Mbps)' },
                        position: 'left'
                    },
                    y1: {
                        beginAtZero: true,
                        title: { display: true, text: 'Ping Latency (ms)' },
                        position: 'right',
                        grid: { drawOnChartArea: false }
                    }
                }
            }
        }, chartCanvasContainer);
    }

    // Navigation button click handlers
    oldestBtn.addEventListener("click", () => {
        const curRecords = (netSelectedWifi === "__all__") ? allInternetRecords : allInternetRecords.filter(r => r.cleanNetwork === netSelectedWifi);
        const allP = getAllNetPeriods(netAggregation, curRecords);
        const eff = (netWindowSize === "all") ? allP.length : Math.min(netWindowSize, allP.length);
        netOffset = Math.max(0, allP.length - eff);
        renderTimelineView();
    });

    prevBtn.addEventListener("click", () => {
        const curRecords = (netSelectedWifi === "__all__") ? allInternetRecords : allInternetRecords.filter(r => r.cleanNetwork === netSelectedWifi);
        const allP = getAllNetPeriods(netAggregation, curRecords);
        const eff = (netWindowSize === "all") ? allP.length : Math.min(netWindowSize, allP.length);
        const maxO = Math.max(0, allP.length - eff);
        netOffset = Math.min(maxO, netOffset + getNetStep());
        renderTimelineView();
    });

    floatLeft.addEventListener("click", () => {
        const curRecords = (netSelectedWifi === "__all__") ? allInternetRecords : allInternetRecords.filter(r => r.cleanNetwork === netSelectedWifi);
        const allP = getAllNetPeriods(netAggregation, curRecords);
        const eff = (netWindowSize === "all") ? allP.length : Math.min(netWindowSize, allP.length);
        const maxO = Math.max(0, allP.length - eff);
        netOffset = Math.min(maxO, netOffset + getNetStep());
        renderTimelineView();
    });

    nextBtn.addEventListener("click", () => {
        netOffset = Math.max(0, netOffset - getNetStep());
        renderTimelineView();
    });

    floatRight.addEventListener("click", () => {
        netOffset = Math.max(0, netOffset - getNetStep());
        renderTimelineView();
    });

    latestBtn.addEventListener("click", () => {
        netOffset = 0;
        renderTimelineView();
    });

    // Wheel navigation handler (pan / zoom)
    chartWrapper.addEventListener("wheel", (e) => {
        e.preventDefault();
        const curRecords = (netSelectedWifi === "__all__") ? allInternetRecords : allInternetRecords.filter(r => r.cleanNetwork === netSelectedWifi);
        const allP = getAllNetPeriods(netAggregation, curRecords);
        const total = allP.length;
        if (total === 0) return;

        const isZoom = (netWheelMode === "zoom" && !e.shiftKey) || e.ctrlKey || e.metaKey;

        if (isZoom) {
            let cur = (netWindowSize === "all") ? total : netWindowSize;
            const step = Math.max(1, Math.round(cur * 0.15));
            if (e.deltaY < 0) {
                netWindowSize = Math.max(3, cur - step);
            } else {
                netWindowSize = Math.min(total, cur + step);
            }
            renderTimelineView();
        } else {
            const step = getNetStep();
            const eff = (netWindowSize === "all") ? total : Math.min(netWindowSize, total);
            const maxO = Math.max(0, total - eff);
            if (e.deltaY > 0) {
                netOffset = Math.min(maxO, netOffset + step);
            } else {
                netOffset = Math.max(0, netOffset - step);
            }
            renderTimelineView();
        }
    }, { passive: false });

    // Initial render of timeline
    renderTimelineView();

    // ==============================================================================
    // 7. HOURLY PERFORMANCE PROFILE & WI-FI DEEP DIVE (COLLAPSIBLE / TOGGLEABLE)
    // ==============================================================================
    const profileContainer = card.createDiv();
    profileContainer.style.marginTop = "22px";
    profileContainer.style.paddingTop = "18px";
    profileContainer.style.borderTop = "1px solid var(--background-modifier-border)";
    profileContainer.style.display = netShow24hProfile ? "block" : "none";

    const profileHeader = profileContainer.createEl("h4", { text: "🕒 24-Hour Performance Profile & Wi-Fi Analysis" });
    profileHeader.style.margin = "0 0 4px 0";

    const profileSub = profileContainer.createEl("p", { 
        text: "Analyze 24-hour daily patterns grouped into 15-minute mean slices (Wheel or Radar clock) or linear bars across past 7, 14, or 30 days." 
    });
    profileSub.style.fontSize = "11px";
    profileSub.style.opacity = "0.7";
    profileSub.style.margin = "0 0 16px 0";

    // Controls Row 1: Wi-Fi Selector & Days Horizon
    const controlsRow1 = profileContainer.createDiv();
    controlsRow1.style.display = "flex";
    controlsRow1.style.alignItems = "center";
    controlsRow1.style.gap = "14px";
    controlsRow1.style.marginBottom = "10px";
    controlsRow1.style.flexWrap = "wrap";

    // Wi-Fi Filter Dropdown
    const profWifiContainer = controlsRow1.createDiv();
    profWifiContainer.style.display = "flex";
    profWifiContainer.style.alignItems = "center";
    profWifiContainer.style.gap = "6px";

    const profWifiLabelEl = profWifiContainer.createEl("span", { text: "📶 Wi-Fi:" });
    profWifiLabelEl.style.fontSize = "12px";
    profWifiLabelEl.style.fontWeight = "bold";

    const profWifiSelect = profWifiContainer.createEl("select");
    profWifiSelect.style.padding = "5px 10px";
    profWifiSelect.style.borderRadius = "6px";
    profWifiSelect.style.fontSize = "12px";
    profWifiSelect.style.backgroundColor = "var(--background-modifier-form-field)";
    profWifiSelect.style.color = "var(--text-normal)";
    profWifiSelect.style.border = "1px solid var(--background-modifier-border)";

    const profAllWifiOpt = profWifiSelect.createEl("option", { 
        text: `🌐 All Networks`, 
        value: "__all__" 
    });
    if (netSelectedWifi === "__all__") profAllWifiOpt.selected = true;

    allKnownNetworks.forEach(net => {
        const opt = profWifiSelect.createEl("option", { 
            text: `📶 ${net}`, 
            value: net 
        });
        if (net === netSelectedWifi) opt.selected = true;
    });

    profWifiSelect.addEventListener("change", () => {
        netSelectedWifi = profWifiSelect.value;
        wifiSelect.value = netSelectedWifi;
        updateHourlyProfileView();
        renderTimelineView();
    });

    // Scope / Days Horizon Selector
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
        { id: "all", label: "All History" }
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

    // Controls Row 2: Elements to Show (Avg Speed, Peak/Min Fluctuations, Latency) & Resolution
    const controlsRow2 = profileContainer.createDiv();
    controlsRow2.style.display = "flex";
    controlsRow2.style.alignItems = "center";
    controlsRow2.style.gap = "14px";
    controlsRow2.style.marginBottom = "16px";
    controlsRow2.style.flexWrap = "wrap";

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

    // View Type Selector: Wheel (Polar Clock) vs Radar vs Linear Bar
    const profileTypeContainer = controlsRow2.createDiv();
    profileTypeContainer.style.display = "flex";
    profileTypeContainer.style.alignItems = "center";
    profileTypeContainer.style.gap = "4px";

    const profileTypeLabel = profileTypeContainer.createEl("span", { text: "View:" });
    profileTypeLabel.style.fontSize = "12px";
    profileTypeLabel.style.fontWeight = "bold";

    const profileTypeOptions = [
        { id: "bar", label: "📊 Linear" },
        { id: "area", label: "🌊 Area Ribbon" },
        { id: "wheel", label: "🎡 Wheel" },
        { id: "radar", label: "🎯 Radar" }
    ];

    const profileTypeBtns = [];
    profileTypeOptions.forEach(opt => {
        const btn = profileTypeContainer.createEl("button", { text: opt.label });
        btn.style.padding = "4px 8px";
        btn.style.fontSize = "11px";
        btn.style.borderRadius = "4px";
        btn.style.border = "1px solid var(--background-modifier-border)";
        btn.style.cursor = "pointer";
        profileTypeBtns.push({ id: opt.id, btn });

        btn.addEventListener("click", () => {
            netProfileChartType = opt.id;
            updateProfileTypeBtns();
            updateHourlyProfileView();
        });
    });

    function updateProfileTypeBtns() {
        profileTypeBtns.forEach(({ id, btn }) => {
            const active = (id === netProfileChartType);
            btn.style.backgroundColor = active ? "var(--interactive-accent)" : "var(--background-modifier-form-field)";
            btn.style.color = active ? "var(--text-on-accent)" : "var(--text-normal)";
            btn.style.fontWeight = active ? "bold" : "normal";
        });
    }
    updateProfileTypeBtns();

    // Right-aligned resolution buttons
    const profResContainer = controlsRow2.createDiv();
    profResContainer.style.display = "flex";
    profResContainer.style.alignItems = "center";
    profResContainer.style.gap = "4px";
    profResContainer.style.marginLeft = "auto";

    const profResLabel = profResContainer.createEl("span", { text: "Bucket:" });
    profResLabel.style.fontSize = "12px";
    profResLabel.style.fontWeight = "bold";

    const profResOptions = [
        { val: 15, label: "15m" },
        { val: 30, label: "30m" },
        { val: 60, label: "1h" }
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
    updateProfResBtns();

    // Dynamic Profile Content
    const profileContent = profileContainer.createDiv();

    function updateHourlyProfileView() {
        profileContent.innerHTML = "";

        // Resolve scope records
        let baseRecords;
        let scopeLabelStr;
        if (netHourlyScope === "all") {
            baseRecords = allInternetRecords;
            scopeLabelStr = "All Recorded History";
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

        // Multi-Network Comparison Cards (when All Networks is selected)
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
                    profWifiSelect.value = net;
                    updateHourlyProfileView();
                    renderTimelineView();
                });
            });
        }

        // Profile Chart Container
        const chartDiv = profileContent.createDiv();
        chartDiv.style.marginBottom = "14px";

        const bucketSize = [15, 30, 60].includes(netProfileResolution) ? netProfileResolution : 15;
        const numBuckets = Math.floor(1440 / bucketSize);
        const bucketLabel = bucketSize === 60 ? '1h' : `${bucketSize}m`;
        const bucketName = bucketSize === 60 ? '1-hour' : `${bucketSize}-minute`;

        const chartHeader = chartDiv.createEl("h4");
        chartHeader.style.margin = "0 0 4px 0";

        const chartSub = chartDiv.createEl("p");
        chartSub.style.fontSize = "11px";
        chartSub.style.opacity = "0.7";
        chartSub.style.margin = "0 0 10px 0";

        if (netProfileChartType === "wheel") {
            chartHeader.textContent = `🎡 24-Hour Wheel Profile (${bucketLabel} Mean Slices) · ${displayNetName} · ${scopeLabelStr}`;
            chartSub.textContent = `Circular 24-hour clock wheel showing average internet performance in ${bucketName} mean slices across ${scopeLabelStr} (${activeNetRecords.length} checks analyzed).`;
        } else if (netProfileChartType === "radar") {
            chartHeader.textContent = `🎯 24-Hour Radar Profile (${bucketLabel} Mean) · ${displayNetName} · ${scopeLabelStr}`;
            chartSub.textContent = `Radial 24-hour clock face showing average speed and latency across ${scopeLabelStr} in ${bucketName} intervals.`;
        } else if (netProfileChartType === "area") {
            chartHeader.textContent = `🌊 24-Hour Area Ribbon Profile (${bucketLabel} Mean) · ${displayNetName} · ${scopeLabelStr}`;
            chartSub.textContent = `Continuous smooth speed area ribbon showing min/max bandwidth span and mean throughput across ${scopeLabelStr} in ${bucketName} intervals.`;
        } else {
            chartHeader.textContent = `📊 24h Linear Profile (${bucketLabel} Buckets) · ${displayNetName} · ${scopeLabelStr}`;
            chartSub.textContent = `Showing 24-hour daily patterns grouped into ${bucketName} averages across ${scopeLabelStr} (${activeNetRecords.length} checks analyzed).`;
        }

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
                startLabel: hourLabels[i],
                endLabel: hourLabels[(i + 1) % numBuckets],
                count: inBucket.length,
                offlineCount: offlineCount,
                avgSpeed: allSpeeds.length ? +(allSpeeds.reduce((a, b) => a + b, 0) / allSpeeds.length).toFixed(1) : null,
                minSpeed: onlineSpeeds.length ? Math.min(...onlineSpeeds) : (allSpeeds.length ? 0 : null),
                maxSpeed: onlineSpeeds.length ? Math.max(...onlineSpeeds) : (allSpeeds.length ? 0 : null),
                avgPing: p.length ? Math.round(p.reduce((a, b) => a + b, 0) / p.length) : null
            };
        });

        // Summary KPIs & Legend for Wheel and Radar views
        if (netProfileChartType === "wheel" || netProfileChartType === "radar") {
            const validBucketSpeeds = hourlyStats.filter(h => h.count > 0 && h.avgSpeed != null);
            const bestBucket = validBucketSpeeds.length > 0
                ? validBucketSpeeds.reduce((max, h) => (h.avgSpeed > max.avgSpeed ? h : max), validBucketSpeeds[0])
                : null;
            const lowestBucket = validBucketSpeeds.length > 0
                ? validBucketSpeeds.reduce((min, h) => (h.avgSpeed < min.avgSpeed ? h : min), validBucketSpeeds[0])
                : null;
            const overallMeanSpeed = validBucketSpeeds.length > 0
                ? (validBucketSpeeds.reduce((sum, h) => sum + h.avgSpeed, 0) / validBucketSpeeds.length).toFixed(1)
                : null;
            const validBucketPings = hourlyStats.filter(h => h.count > 0 && h.avgPing != null);
            const overallMeanPing = validBucketPings.length > 0
                ? Math.round(validBucketPings.reduce((sum, h) => sum + h.avgPing, 0) / validBucketPings.length)
                : null;

            const wheelSummary = chartDiv.createDiv();
            wheelSummary.style.display = "flex";
            wheelSummary.style.justifyContent = "center";
            wheelSummary.style.gap = "14px";
            wheelSummary.style.flexWrap = "wrap";
            wheelSummary.style.padding = "8px 12px";
            wheelSummary.style.marginBottom = "10px";
            wheelSummary.style.borderRadius = "6px";
            wheelSummary.style.backgroundColor = "var(--background-modifier-form-field)";
            wheelSummary.style.fontSize = "11px";

            wheelSummary.innerHTML = `
                <span>🌐 Overall Mean: <b style="color: #38bdf8;">${overallMeanSpeed != null ? overallMeanSpeed + ' Mbps' : '-'}</b></span>
                <span>🚀 Peak Window: <b>${bestBucket ? `${bestBucket.startLabel} (${bestBucket.avgSpeed} Mbps)` : '-'}</b></span>
                <span>⚠️ Lowest Window: <b>${lowestBucket ? `${lowestBucket.startLabel} (${lowestBucket.avgSpeed} Mbps)` : '-'}</b></span>
                <span>⏱️ Mean Ping: <b>${overallMeanPing != null ? overallMeanPing + ' ms' : '-'}</b></span>
                <span>📊 Active Slices: <b>${validBucketSpeeds.length}/${numBuckets}</b></span>
            `;

            const isPingWheel = !netShowAvgSpeed && netShowLatency;
            const legendRow = chartDiv.createDiv();
            legendRow.style.display = "flex";
            legendRow.style.justifyContent = "center";
            legendRow.style.gap = "12px";
            legendRow.style.flexWrap = "wrap";
            legendRow.style.fontSize = "10px";
            legendRow.style.opacity = "0.85";
            legendRow.style.marginBottom = "10px";

            if (isPingWheel) {
                legendRow.innerHTML = `
                    <span><span style="color: #38bdf8; font-weight: bold;">●</span> Excellent (&lt;40 ms)</span>
                    <span><span style="color: #0ea5e9; font-weight: bold;">●</span> Good (40-80 ms)</span>
                    <span><span style="color: #f59e0b; font-weight: bold;">●</span> Fair (80-150 ms)</span>
                    <span><span style="color: #ef4444; font-weight: bold;">●</span> High / Offline (&gt;150 ms)</span>
                    <span><span style="color: rgba(148, 163, 184, 0.4); font-weight: bold;">●</span> No Checks</span>
                `;
            } else {
                legendRow.innerHTML = `
                    <span><span style="color: #38bdf8; font-weight: bold;">●</span> Fast (&gt;30 Mbps)</span>
                    <span><span style="color: #0ea5e9; font-weight: bold;">●</span> Good (15-30 Mbps)</span>
                    <span><span style="color: #f59e0b; font-weight: bold;">●</span> Moderate (5-15 Mbps)</span>
                    <span><span style="color: #f97316; font-weight: bold;">●</span> Slow (&lt;5 Mbps)</span>
                    <span><span style="color: #ef4444; font-weight: bold;">●</span> Offline</span>
                    <span><span style="color: rgba(148, 163, 184, 0.4); font-weight: bold;">●</span> No Checks</span>
                `;
            }
        }

        if (netProfileChartType === "wheel") {
            const wheelWrapper = chartDiv.createDiv();
            wheelWrapper.style.maxWidth = "540px";
            wheelWrapper.style.margin = "0 auto";
            wheelWrapper.style.position = "relative";

            const isPingWheel = !netShowAvgSpeed && netShowLatency;
            const wheelMetricLabel = isPingWheel ? `${bucketLabel} Mean Ping (ms)` : `${bucketLabel} Mean Speed (Mbps)`;

            const bgColors = hourlyStats.map(h => {
                if (h.count === 0) return 'rgba(148, 163, 184, 0.12)';
                if (isPingWheel) {
                    if (h.avgPing == null) return 'rgba(239, 68, 68, 0.85)';
                    if (h.avgPing < 40) return 'rgba(56, 189, 248, 0.85)';
                    if (h.avgPing < 80) return 'rgba(14, 165, 233, 0.8)';
                    if (h.avgPing < 150) return 'rgba(245, 158, 11, 0.8)';
                    return 'rgba(239, 68, 68, 0.85)';
                }
                if (h.avgSpeed === 0 || (h.offlineCount === h.count)) return 'rgba(239, 68, 68, 0.85)';
                if (h.avgSpeed >= 30) return 'rgba(56, 189, 248, 0.85)';
                if (h.avgSpeed >= 15) return 'rgba(14, 165, 233, 0.8)';
                if (h.avgSpeed >= 5) return 'rgba(245, 158, 11, 0.8)';
                return 'rgba(249, 115, 22, 0.85)';
            });

            const borderColors = hourlyStats.map(h => {
                if (h.count === 0) return 'rgba(148, 163, 184, 0.25)';
                if (isPingWheel) {
                    if (h.avgPing == null) return '#ef4444';
                    if (h.avgPing < 40) return '#38bdf8';
                    if (h.avgPing < 80) return '#0ea5e9';
                    if (h.avgPing < 150) return '#f59e0b';
                    return '#ef4444';
                }
                if (h.avgSpeed === 0 || (h.offlineCount === h.count)) return '#ef4444';
                if (h.avgSpeed >= 30) return '#38bdf8';
                if (h.avgSpeed >= 15) return '#0ea5e9';
                if (h.avgSpeed >= 5) return '#f59e0b';
                return '#f97316';
            });

            const maxVal = Math.max(1, ...hourlyStats.map(h => isPingWheel ? (h.avgPing || 0) : (h.avgSpeed || 0)));
            const baselineVal = +(maxVal * 0.04).toFixed(2);

            const wheelData = hourlyStats.map(h => {
                if (h.count === 0) return baselineVal;
                const val = isPingWheel ? h.avgPing : h.avgSpeed;
                if (val == null) return baselineVal;
                return Math.max(baselineVal, val);
            });

            renderQuickChart({
                type: 'polarArea',
                data: {
                    labels: hourLabels,
                    datasets: [{
                        label: wheelMetricLabel,
                        data: wheelData,
                        backgroundColor: bgColors,
                        borderColor: borderColors,
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: true,
                    scales: {
                        r: {
                            beginAtZero: true,
                            ticks: {
                                backdropColor: 'transparent',
                                color: 'var(--text-muted)',
                                font: { size: 9 },
                                z: 1
                            },
                            grid: {
                                color: 'var(--background-modifier-border)'
                            },
                            angleLines: {
                                display: true,
                                color: 'var(--background-modifier-border)'
                            },
                            pointLabels: {
                                display: true,
                                centerPointLabels: true,
                                font: { size: 10, weight: 'bold' },
                                color: 'var(--text-muted)',
                                callback: function(label, index) {
                                    if (bucketSize === 15) {
                                        if (index % 12 === 0) return label;
                                        if (index % 4 === 0) return label.split(":")[0] + "h";
                                        return "";
                                    } else if (bucketSize === 30) {
                                        if (index % 6 === 0) return label;
                                        if (index % 2 === 0) return label.split(":")[0] + "h";
                                        return "";
                                    } else {
                                        if (index % 3 === 0) return label;
                                        return "";
                                    }
                                }
                            }
                        }
                    },
                    plugins: {
                        legend: { display: false },
                        tooltip: {
                            callbacks: {
                                title: function(items) {
                                    if (!items.length) return "";
                                    const idx = items[0].dataIndex;
                                    const stat = hourlyStats[idx];
                                    return `🕒 ${stat.startLabel} – ${stat.endLabel} (${bucketName} Window)`;
                                },
                                label: function(context) {
                                    const stat = hourlyStats[context.dataIndex];
                                    if (!stat || stat.count === 0) {
                                        return " No checks recorded in this window";
                                    }
                                    const lines = [];
                                    if (stat.offlineCount === stat.count) {
                                        lines.push(` 🔴 Offline (all ${stat.count} checks offline)`);
                                    } else if (stat.avgSpeed != null) {
                                        const offStr = stat.offlineCount > 0 ? ` (${stat.offlineCount} offline)` : '';
                                        lines.push(` 🚀 ${bucketLabel} Mean Speed: ${stat.avgSpeed} Mbps${offStr}`);
                                    }
                                    if (stat.minSpeed != null && stat.maxSpeed != null) {
                                        lines.push(` 📊 Fluctuation: ${stat.minSpeed} – ${stat.maxSpeed} Mbps`);
                                    }
                                    if (stat.avgPing != null) {
                                        lines.push(` ⏱️ Mean Latency: ${stat.avgPing} ms`);
                                    }
                                    lines.push(` 📋 Total Checks: ${stat.count}`);
                                    return lines;
                                }
                            }
                        }
                    }
                }
            }, wheelWrapper);
        } else if (netProfileChartType === "radar") {
            const radarWrapper = chartDiv.createDiv();
            radarWrapper.style.maxWidth = "540px";
            radarWrapper.style.margin = "0 auto";
            radarWrapper.style.position = "relative";

            const radarDatasets = [];

            if (netShowAvgSpeed) {
                radarDatasets.push({
                    label: `Mean Speed (Mbps)`,
                    data: hourlyStats.map(h => h.count > 0 ? (h.avgSpeed != null ? h.avgSpeed : 0) : null),
                    borderColor: '#38bdf8',
                    backgroundColor: 'rgba(56, 189, 248, 0.25)',
                    borderWidth: 2,
                    pointRadius: bucketSize === 15 ? 1.5 : 2.5,
                    pointHoverRadius: 5,
                    tension: 0.1
                });
            }

            if (netShowFluctuations) {
                radarDatasets.push({
                    label: `Peak Speed (Mbps)`,
                    data: hourlyStats.map(h => h.count > 0 ? (h.maxSpeed != null ? h.maxSpeed : 0) : null),
                    borderColor: '#0ea5e9',
                    backgroundColor: 'transparent',
                    borderDash: [3, 3],
                    borderWidth: 1.5,
                    pointRadius: 1,
                    pointHoverRadius: 4,
                    tension: 0.1
                });
            }

            if (netShowLatency) {
                radarDatasets.push({
                    label: `Mean Ping (ms)`,
                    data: hourlyStats.map(h => h.count > 0 ? (h.avgPing != null ? h.avgPing : null) : null),
                    borderColor: '#fb923c',
                    backgroundColor: 'transparent',
                    borderDash: [4, 4],
                    borderWidth: 1.5,
                    pointRadius: bucketSize === 15 ? 1.5 : 2.5,
                    pointHoverRadius: 4,
                    tension: 0.1
                });
            }

            renderQuickChart({
                type: 'radar',
                data: {
                    labels: hourLabels,
                    datasets: radarDatasets
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: true,
                    scales: {
                        r: {
                            beginAtZero: true,
                            ticks: {
                                backdropColor: 'transparent',
                                color: 'var(--text-muted)',
                                font: { size: 9 },
                                z: 1
                            },
                            grid: {
                                color: 'var(--background-modifier-border)'
                            },
                            angleLines: {
                                display: true,
                                color: 'var(--background-modifier-border)'
                            },
                            pointLabels: {
                                display: true,
                                font: { size: 10, weight: 'bold' },
                                color: 'var(--text-muted)',
                                callback: function(label, index) {
                                    if (bucketSize === 15) {
                                        if (index % 12 === 0) return label;
                                        if (index % 4 === 0) return label.split(":")[0] + "h";
                                        return "";
                                    } else if (bucketSize === 30) {
                                        if (index % 6 === 0) return label;
                                        if (index % 2 === 0) return label.split(":")[0] + "h";
                                        return "";
                                    } else {
                                        if (index % 3 === 0) return label;
                                        return "";
                                    }
                                }
                            }
                        }
                    },
                    plugins: {
                        legend: {
                            display: true,
                            position: 'top',
                            labels: { boxWidth: 12, font: { size: 11 } }
                        },
                        tooltip: {
                            callbacks: {
                                title: function(items) {
                                    if (!items.length) return "";
                                    const idx = items[0].dataIndex;
                                    const stat = hourlyStats[idx];
                                    return `🕒 ${stat.startLabel} – ${stat.endLabel} (${bucketName} Window)`;
                                },
                                label: function(context) {
                                    const stat = hourlyStats[context.dataIndex];
                                    if (!stat || stat.count === 0) return ` ${context.dataset.label}: No checks`;
                                    const val = context.raw;
                                    if (val == null) return ` ${context.dataset.label}: -`;
                                    return ` ${context.dataset.label}: ${val}`;
                                }
                            }
                        }
                    }
                }
            }, radarWrapper);
        } else if (netProfileChartType === "area") {
            // Continuous Area Ribbon Band
            const datasets = [];

            // Upper Peak Boundary
            if (netShowFluctuations) {
                datasets.push({
                    type: 'line',
                    label: 'Peak Speed (Mbps)',
                    data: hourlyStats.map(h => (h.count > 0 && h.maxSpeed != null) ? h.maxSpeed : null),
                    borderColor: 'rgba(56, 189, 248, 0.4)',
                    borderDash: [3, 3],
                    borderWidth: 1.5,
                    pointRadius: 0,
                    pointHoverRadius: 4,
                    fill: false,
                    tension: 0.35,
                    spanGaps: true,
                    yAxisID: 'y'
                });
            }

            // Central Mean Speed Area
            if (netShowAvgSpeed) {
                datasets.push({
                    type: 'line',
                    label: `${bucketLabel} Mean Speed (Mbps)`,
                    data: hourlyStats.map(h => (h.count > 0 && h.avgSpeed != null) ? h.avgSpeed : null),
                    borderColor: '#38bdf8',
                    backgroundColor: 'rgba(56, 189, 248, 0.18)',
                    borderWidth: 2.5,
                    pointRadius: bucketSize === 15 ? 1.5 : 2.5,
                    pointHoverRadius: 6,
                    fill: true,
                    tension: 0.35,
                    spanGaps: true,
                    yAxisID: 'y'
                });
            }

            // Lower Min Boundary
            if (netShowFluctuations) {
                datasets.push({
                    type: 'line',
                    label: 'Min Speed (Mbps)',
                    data: hourlyStats.map(h => (h.count > 0 && h.minSpeed != null) ? h.minSpeed : null),
                    borderColor: 'rgba(56, 189, 248, 0.35)',
                    borderDash: [2, 2],
                    borderWidth: 1.2,
                    pointRadius: 0,
                    pointHoverRadius: 4,
                    fill: false,
                    tension: 0.35,
                    spanGaps: true,
                    yAxisID: 'y'
                });
            }

            // Latency Ping Line
            if (netShowLatency) {
                const pingAxis = (!netShowAvgSpeed && !netShowFluctuations) ? 'y' : 'y1';
                datasets.push({
                    type: 'line',
                    label: 'Latency (Ping ms)',
                    data: hourlyStats.map(h => (h.count > 0 && h.avgPing != null) ? h.avgPing : null),
                    borderColor: '#fb923c',
                    backgroundColor: 'transparent',
                    borderDash: [4, 4],
                    borderWidth: 2,
                    pointRadius: bucketSize === 15 ? 1.5 : 2.5,
                    pointHoverRadius: 5,
                    tension: 0.25,
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

            renderQuickChart({
                type: 'line',
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
                                        if (val == null) return ` ${context.dataset.label}: 🔴 Offline / Timed out`;
                                        return ` ${context.dataset.label}: ${val} ms`;
                                    } else {
                                        if (val === 0) return ` ${context.dataset.label}: 🔴 0.0 Mbps (Offline)`;
                                        if (val == null) return ` ${context.dataset.label}: - (No Data)`;
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
            // Linear Bar Profile
            const datasets = [];

            // Peak/Min Fluctuations
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

            // Avg Speed Line
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

            // Latency Line
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

            renderQuickChart({
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
                                        if (val == null) return ` ${context.dataset.label}: 🔴 Offline / Timed out`;
                                        return ` ${context.dataset.label}: ${val} ms`;
                                    } else {
                                        if (val === 0) return ` ${context.dataset.label}: 🔴 0.0 Mbps (Offline)`;
                                        if (val == null) return ` ${context.dataset.label}: - (No Data)`;
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
        }
    }

    if (netShow24hProfile) {
        updateHourlyProfileView();
    }

    // ==============================================================================
    // 8. DETAILED NETWORK CHECKS TABLE (COLLAPSIBLE / TOGGLEABLE)
    // ==============================================================================
    const logContainer = card.createDiv();
    logContainer.style.marginTop = "20px";
    logContainer.style.paddingTop = "10px";
    logContainer.style.borderTop = "1px solid var(--background-modifier-border)";
    logContainer.style.display = netShowRecentLog ? "block" : "none";

    const logTitle = logContainer.createEl("h4", { text: "📋 Recent Network Checks Log" });
    logTitle.style.margin = "0 0 10px 0";

    const tableDiv = logContainer.createDiv();
    tableDiv.style.overflowX = "auto";

    const netTable = tableDiv.createEl("table");
    netTable.style.width = "100%";
    netTable.style.fontSize = "11px";
    netTable.style.borderCollapse = "collapse";

    netTable.innerHTML = `
        <thead>
            <tr style="border-bottom: 1px solid var(--background-modifier-border); text-align: left; opacity: 0.7;">
                <th style="padding: 4px 6px;">Date</th>
                <th style="padding: 4px 6px;">Time</th>
                <th style="padding: 4px 6px;">Status</th>
                <th style="padding: 4px 6px;">Network / Wi-Fi</th>
                <th style="padding: 4px 6px;">Ping</th>
                <th style="padding: 4px 6px;">Download Speed</th>
                <th style="padding: 4px 6px;">Notes</th>
            </tr>
        </thead>
        <tbody>
            ${scopedRecords.slice(-50).reverse().map(r => `
                <tr style="border-bottom: 1px solid var(--background-modifier-border);">
                    <td style="padding: 4px 6px;">${r.dateStr}</td>
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



renderDashboard();
```