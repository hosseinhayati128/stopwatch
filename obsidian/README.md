# 📊 Obsidian Stopwatch & Activity Dashboard

An interactive dashboard for [Obsidian](https://obsidian.md) that visualizes your work sessions from **Stopwatch Overlay** and background application/web activity from **ActivityWatch**.

---

## 📁 Included Files

- **`Stopwatch Dashboard.md`**: The main DataviewJS dashboard note. Copy this file into your Obsidian vault.
- **`Project Rules.json`**: Per-project distraction vs. work tool rule overrides (persisted automatically by the dashboard).
- **`Internet Log.md`**: Sample/initial internet connection log note.

---

## ⚡ Prerequisites

To render the charts and timelines, make sure you have the following community plugins installed and enabled in Obsidian:

1. **[Dataview](https://github.com/blacksmithgu/obsidian-dataview)**
   - In Obsidian Settings → Dataview: enable **Enable JavaScript Queries** (`dataviewjs`).
2. **[Obsidian Charts](https://github.com/phibr0/obsidian-charts)**
   - Provides Chart.js rendering inside Obsidian notes.

---

## 🚀 Key Features

### 1. 🔍 Dual Synchronized Timeline
- Two aligned 24-hour visual tracks for any chosen day:
  - **Track 1:** Stopwatch project work sessions (color-coded by project).
  - **Track 2:** Foreground applications & web sites active during that day.
- **Active Network Badge:** Shows the connected Wi-Fi SSID (e.g. `📶 HUAWEI Y7 Prime (92%)`) or network adapter directly in the timeline header.
- **Interactive Zoom & Windowing:** Preset zoom buttons (`All Day`, `Morning 6–14`, `Afternoon 12–20`, `Evening 18–02`) and start/end hour selectors to inspect fine-grained activity.
- **Click to Inspect:** Click any block to view its exact duration, window title / URL, and project correlation.

### 2. 🎯 Project-by-Project Deep Dive
- Multi-dimensional filtering:
  - **Project Filter:** Choose any individual project or view all.
  - **Scope Filter:** Day, Week, Month, or All Time.
  - **Date Picker:** Jump directly to any date in your history.
  - **Session Pills:** Toggle between combined period statistics or inspect individual sessions.
  - **App vs. Web Filter:** Switch between Desktop Apps, Browser Sites, or All.

### 3. ⚙️ Interactive Per-Project Distraction Rules
- **Click-to-Toggle Bar Chart:** Click any bar in the *Apps Used Across Sessions* chart to toggle an application between `🟢 Work Tool` and `⚠️ Distraction` for the active project.
- **Table Category Toggles:** Click the category badge in the detailed activity table to toggle classifications on the fly.
- **Rules Manager Card:** Add custom overrides, remove rules with `✕`, or reset to defaults. Rules are saved in `Project Rules.json`.
- Dynamic recalculation of **Focus Rate (%)**, **Distraction Minutes**, and **Focused Work** across all cards and charts.

### 4. 🌐 Internet Connection & Wi-Fi Performance
- **Active Wi-Fi / Network Detection:** Records connected Wi-Fi SSID, signal percentage (`📶 HUAWEI Y7 Prime (92%)`), or wired Ethernet.
- **Background Speed & Latency Probing:** Measures ping latency (ms) and real download speed (Mbps) periodically in the background (default: every 10 min while timers run).
- **Network Health KPIs:** Active Network, Average Download Speed, Average Ping, and Connection Uptime.
- **Speed & Ping Timeline Chart:** Line chart tracking download speed and ping over time.
- **Detailed History Table:** Full audit table of all connection checks.

### 5. 📈 Standard Analytics & Trends
- **Top Summary KPIs:** Total hours, session count, top project, overall focus rate, and average internet speed.
- **Time by Project (Doughnut Chart):** Distribution of time spent across projects.
- **Activity by Time of Day (Hourly Stacked Bar):** 24-hour breakdown showing when you worked on each project.
- **Daily Trend (Bar Chart):** Daily work hour history over the active period.
