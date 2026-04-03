const form = document.getElementById("profile-form");
const templateSelect = document.getElementById("templateKey");
const playerNameInput = document.getElementById("playerName");
const selectedPlayerIdInput = document.getElementById("selectedPlayerId");
const playerPickerPanel = document.getElementById("player-picker-panel");
const playerCandidateSelect = document.getElementById("playerCandidateSelect");
const playerPickerNote = document.getElementById("player-picker-note");
const statusText = document.getElementById("form-status");
const submitButton = document.getElementById("submitButton");
const summaryPanel = document.getElementById("summary-panel");
const comparisonPanel = document.getElementById("comparison-panel");
const notesPanel = document.getElementById("notes-panel");
const profileTitle = document.getElementById("profile-title");
const profileSubtitle = document.getElementById("profile-subtitle");
const rawApiLink = document.getElementById("raw-api-link");
const highlightGrid = document.getElementById("highlight-grid");
const comparisonGrid = document.getElementById("comparison-grid");
const notesList = document.getElementById("notes-list");
const hammyvizPanel = document.getElementById("hammyviz-panel");
const hammyvizSummary = document.getElementById("hammyviz-summary");
const hammyvizFormula = document.getElementById("hammyviz-formula");
const hammyvizBreakdown = document.getElementById("hammyviz-breakdown");
const compareForm = document.getElementById("compare-form");
const compareTemplateSelect = document.getElementById("compareTemplateKey");
const comparePlayerANameInput = document.getElementById("comparePlayerAName");
const comparePlayerBNameInput = document.getElementById("comparePlayerBName");
const comparePlayerASelectedIdInput = document.getElementById("comparePlayerASelectedId");
const comparePlayerBSelectedIdInput = document.getElementById("comparePlayerBSelectedId");
const comparePlayerASelect = document.getElementById("comparePlayerASelect");
const comparePlayerBSelect = document.getElementById("comparePlayerBSelect");
const comparePlayerAPicker = document.getElementById("compare-player-a-picker");
const comparePlayerBPicker = document.getElementById("compare-player-b-picker");
const comparePlayerANote = document.getElementById("comparePlayerANote");
const comparePlayerBNote = document.getElementById("comparePlayerBNote");
const compareSubmitButton = document.getElementById("compareSubmitButton");
const compareStatus = document.getElementById("compare-status");
const compareSummary = document.getElementById("compare-summary");
const compareResults = document.getElementById("compare-results");
const rankingsForm = document.getElementById("rankings-form");
const rankingsTemplateSelect = document.getElementById("rankingsTemplateKey");
const rankingsLimitInput = document.getElementById("rankingsLimit");
const rankingsSubmitButton = document.getElementById("rankingsSubmitButton");
const rankingsStatus = document.getElementById("rankings-status");
const rankingsResults = document.getElementById("rankings-results");
const exportRankingsButton = document.getElementById("exportRankingsButton");
const top10Button = document.getElementById("top10Button");
const top20Button = document.getElementById("top20Button");

let lastSearchFingerprint = "";
let currentCandidates = [];
let compareAFingerprint = "";
let compareBFingerprint = "";
let compareACandidates = [];
let compareBCandidates = [];
let latestRankings = null;

const hammyVizKeys = [
    "all-involved-overall",
    "own-team-overall",
    "opposition-overall",
    "own-team-top7",
    "opposition-top7",
    "player-sena",
    "own-team-sena",
    "opposition-sena",
    "toughest-hosts-top7",
    "player-toughest-hosts"
];

const formatMetric = (value) => {
    if (value === null || value === undefined) {
        return "n/a";
    }

    return Number(value).toFixed(2);
};

const createMetricPill = (label, value) => {
    const pill = document.createElement("div");
    pill.className = "metric-pill";

    const strong = document.createElement("strong");
    strong.textContent = value;

    const span = document.createElement("span");
    span.textContent = label;

    pill.append(strong, span);
    return pill;
};

const getHighlightOrder = (profile) => {
    if (profile.discipline.toLowerCase() === "bowling") {
        return [
            "player-overall",
            "own-team-overall",
            "opposition-overall",
            "own-team-top7",
            "opposition-top7",
            "player-sena",
            "own-team-sena",
            "opposition-sena",
            "player-vs-pace",
            "player-vs-spin"
        ];
    }

    return [
        "player-overall",
        "own-team-overall",
        "opposition-overall",
        "own-team-top7",
        "opposition-top7",
        "player-sena",
        "toughest-hosts-top7",
        "player-toughest-hosts"
    ];
};

const renderMetrics = (metrics, discipline, format, comparisonKey) => {
    const row = document.createElement("div");
    row.className = "metric-row";
    const isBowling = discipline.toLowerCase() === "bowling";
    const isTest = format.toLowerCase() === "test";
    const isOverallCard = comparisonKey === "player-overall";

    row.appendChild(createMetricPill("Average", formatMetric(metrics.average)));
    row.appendChild(createMetricPill("Baseline avg", formatMetric(metrics.baselineAverage)));
    row.appendChild(createMetricPill("Avg diff", formatMetric(metrics.averageDelta)));
    row.appendChild(createMetricPill("Diff %", formatMetric(metrics.averageDeltaPercent)));

    if (isOverallCard && !isTest) {
        row.appendChild(createMetricPill("Strike rate", formatMetric(metrics.strikeRate)));
    }

    if (isOverallCard) {
        row.appendChild(createMetricPill("Matches", metrics.matches ?? "n/a"));
        row.appendChild(createMetricPill("Innings", metrics.innings ?? "n/a"));
        row.appendChild(createMetricPill(isBowling ? "Wickets" : "Dismissals", metrics.dismissalsOrWickets ?? "n/a"));
        row.appendChild(createMetricPill(isBowling ? "Runs conceded" : "Runs", metrics.runsOrConceded ?? "n/a"));
    }

    if (metrics.hammyVizContribution !== null && metrics.hammyVizContribution !== undefined) {
        row.appendChild(createMetricPill("HammyViz pts", formatMetric(metrics.hammyVizContribution)));
    }

    return row;
};

const renderHighlights = (profile) => {
    highlightGrid.innerHTML = "";

    const comparisonMap = new Map(profile.comparisons.map((item) => [item.key, item]));
    const highlighted = getHighlightOrder(profile)
        .map((key) => comparisonMap.get(key))
        .filter((item) => item);

    highlighted.forEach((item) => {
        const card = document.createElement("article");
        card.className = "highlight-card";

        const title = document.createElement("h3");
        title.textContent = item.label;

        const scope = document.createElement("p");
        scope.className = "comparison-scope";
        scope.textContent = item.scope;

        card.append(title, renderMetrics(item.metrics, profile.discipline, profile.format, item.key), scope);
        highlightGrid.appendChild(card);
    });
};

const renderComparisons = (profile) => {
    comparisonGrid.innerHTML = "";

    profile.comparisons.forEach((item) => {
        const card = document.createElement("article");
        card.className = "comparison-card";

        const title = document.createElement("h3");
        title.textContent = item.label;

        const scope = document.createElement("p");
        scope.className = "comparison-scope";
        scope.textContent = item.scope;

        card.append(title, renderMetrics(item.metrics, profile.discipline, profile.format, item.key), scope);

        if (item.note) {
            const note = document.createElement("p");
            note.className = "comparison-note";
            note.textContent = item.note;
            card.appendChild(note);
        }

        comparisonGrid.appendChild(card);
    });
};

const renderNotes = (profile) => {
    notesList.innerHTML = "";

    profile.notes.forEach((noteText) => {
        const card = document.createElement("article");
        card.className = "note-card";

        const text = document.createElement("p");
        text.textContent = noteText;
        card.appendChild(text);

        notesList.appendChild(card);
    });
};

const renderHammyVizSummary = (container, profile) => {
    if (profile.hammyVizScore === null || profile.hammyVizScore === undefined) {
        return;
    }

    const card = document.createElement("article");
    card.className = "highlight-card";

    const title = document.createElement("h3");
    title.textContent = "HammyViz score";

    const scope = document.createElement("p");
    scope.className = "comparison-scope";
    scope.textContent = "Equal-weight score built from the selected comparison blocks. This is a score out of 100, not an adjusted batting or bowling average.";

    const metricRow = document.createElement("div");
    metricRow.className = "metric-row";
    metricRow.appendChild(createMetricPill("HammyViz score", formatMetric(profile.hammyVizScore)));
    metricRow.appendChild(createMetricPill("Block weight", formatMetric(profile.hammyVizBlockWeight)));

    card.append(title, metricRow, scope);
    container.prepend(card);
};

const compareClassName = (value) => {
    if (value === null || value === undefined) {
        return "";
    }

    if (value > 0) {
        return "compare-positive";
    }

    if (value < 0) {
        return "compare-negative";
    }

    return "";
};

const renderHammyVizBreakdown = (profile) => {
    hammyvizSummary.innerHTML = "";
    hammyvizBreakdown.innerHTML = "";
    hammyvizFormula.textContent = "";

    if (profile.hammyVizScore === null || profile.hammyVizScore === undefined) {
        hammyvizPanel.classList.add("hidden");
        return;
    }

    const summary = document.createElement("article");
    summary.className = "highlight-card";
    summary.innerHTML = `
        <h3>${profile.matchedPlayerName}</h3>
        <div class="metric-row">
            <div class="metric-pill"><strong>${formatMetric(profile.hammyVizScore)}</strong><span>HammyViz score</span></div>
            <div class="metric-pill"><strong>${formatMetric(profile.hammyVizBlockWeight)}</strong><span>Points per block</span></div>
        </div>
    `;
    hammyvizSummary.appendChild(summary);
    hammyvizFormula.textContent = profile.hammyVizFormula || "";

    const rows = profile.comparisons
        .filter((comparison) => hammyVizKeys.includes(comparison.key) && comparison.metrics.hammyVizContribution !== null && comparison.metrics.hammyVizContribution !== undefined);

    rows.forEach((row) => {
        const contribution = Number(row.metrics.hammyVizContribution ?? 0);
        const percent = Number(row.metrics.hammyVizWeightPercent ?? 0);
        const weight = Number(row.metrics.hammyVizWeightPercent ?? 0);
        const fillWidth = weight > 0 ? Math.min((contribution / weight) * 100, 100) : 0;

        const item = document.createElement("article");
        item.className = "hammyviz-row";
        item.innerHTML = `
            <div class="hammyviz-label">${row.label}</div>
            <div class="hammyviz-value ${compareClassName(contribution)}">${formatMetric(contribution)}</div>
            <div class="hammyviz-percent">${formatMetric(percent)}%</div>
            <div class="hammyviz-bar"><div class="hammyviz-bar-fill" style="width:${fillWidth}%"></div></div>
        `;
        hammyvizBreakdown.appendChild(item);
    });

    hammyvizPanel.classList.remove("hidden");
};

const buildBaseParams = () => {
    const formData = new FormData(form);
    const params = new URLSearchParams();

    for (const [key, value] of formData.entries()) {
        if (key === "selectedPlayerId") {
            continue;
        }

        const trimmed = String(value).trim();
        if (trimmed) {
            params.set(key, trimmed);
        }
    }

    return params;
};

const buildFingerprint = () => buildBaseParams().toString();

const buildProfileUrl = () => {
    const params = buildBaseParams();
    const selectedPlayerId = selectedPlayerIdInput.value.trim();

    if (selectedPlayerId) {
        params.set("selectedPlayerId", selectedPlayerId);
    }

    const bowlerTypeSelect = document.getElementById("bowlerType");
    const bowlerType = bowlerTypeSelect?.value || "";
    if (bowlerType) {
        params.set("bowlerType", bowlerType);
    }

    const hostGroupSelect = document.getElementById("hostGroup");
    const hostGroup = hostGroupSelect?.value || "SENA";
    if (hostGroup) {
        params.set("hostGroup", hostGroup);
    }

    return `/profiles/player?${params.toString()}`;
};

const buildPlayerSearchUrl = () => `/players/search?${buildBaseParams().toString()}`;

const showError = (message) => {
    statusText.textContent = message;
    statusText.classList.add("error");
};

const clearError = () => {
    statusText.classList.remove("error");
};

const hideResults = () => {
    summaryPanel.classList.add("hidden");
    hammyvizPanel.classList.add("hidden");
    comparisonPanel.classList.add("hidden");
    notesPanel.classList.add("hidden");
};

const resetPlayerSelection = () => {
    selectedPlayerIdInput.value = "";
    currentCandidates = [];
    lastSearchFingerprint = "";
    playerCandidateSelect.innerHTML = '<option value="">Choose a player</option>';
    playerPickerNote.textContent = "";
    playerPickerPanel.classList.add("hidden");
};

const populatePlayerPicker = (candidates) => {
    playerCandidateSelect.innerHTML = '<option value="">Choose a player</option>';

    candidates.forEach((candidate) => {
        const option = document.createElement("option");
        option.value = String(candidate.playerId);
        option.textContent = candidate.displayName;
        playerCandidateSelect.appendChild(option);
    });
};

const searchPlayers = async () => {
    const response = await fetch(buildPlayerSearchUrl());
    const data = await response.json();

    if (!response.ok) {
        throw new Error(data.error || "Unable to search Statsguru players.");
    }

    currentCandidates = data.candidates;
    lastSearchFingerprint = buildFingerprint();
    return data;
};

const searchComparePlayers = async (playerName, templateKey) => {
    const params = new URLSearchParams({
        playerName,
        templateKey
    });

    const response = await fetch(`/players/search?${params.toString()}`);
    const data = await response.json();

    if (!response.ok) {
        throw new Error(data.error || "Unable to search Statsguru players.");
    }

    return data;
};

const ensurePlayerSelection = async () => {
    const currentFingerprint = buildFingerprint();
    const shouldSearchAgain = currentFingerprint !== lastSearchFingerprint || currentCandidates.length === 0;

    if (shouldSearchAgain) {
        const searchResult = await searchPlayers();

        if (searchResult.candidates.length === 0) {
            resetPlayerSelection();
            throw new Error(`No Statsguru player match was found for '${playerNameInput.value.trim()}'.`);
        }

        if (searchResult.autoSelected && searchResult.selectedCandidate) {
            selectedPlayerIdInput.value = String(searchResult.selectedCandidate.playerId);
            playerPickerPanel.classList.add("hidden");
            playerPickerNote.textContent = "";
            statusText.textContent = `Using ${searchResult.selectedCandidate.displayName}.`;
            return;
        }

        populatePlayerPicker(searchResult.candidates);
        playerPickerPanel.classList.remove("hidden");
        playerPickerNote.textContent = "Multiple Statsguru matches were found. Pick the exact player and load again.";
        selectedPlayerIdInput.value = "";
        throw new Error("Pick the exact player from the Statsguru matches below.");
    }

    if (!selectedPlayerIdInput.value.trim()) {
        if (currentCandidates.length === 1) {
            selectedPlayerIdInput.value = String(currentCandidates[0].playerId);
            playerPickerPanel.classList.add("hidden");
            return;
        }

        playerPickerPanel.classList.add("hidden");
        throw new Error("Pick the exact player from the Statsguru matches below.");
    }
};

const applyTemplateHints = () => {
    const selected = templateSelect.options[templateSelect.selectedIndex]?.value ?? "";
    const isBowling = selected.toLowerCase().includes("bowling");
    playerNameInput.placeholder = isBowling ? "Enter bowler name" : "Enter batter name";
    
    const bowlerTypeLabel = document.getElementById("bowlerType")?.closest("label");
    if (bowlerTypeLabel) {
        if (isBowling) {
            bowlerTypeLabel.classList.remove("hidden");
        } else {
            bowlerTypeLabel.classList.add("hidden");
        }
    }
};

const applyCompareTemplateHints = () => {
    const selected = compareTemplateSelect.options[compareTemplateSelect.selectedIndex]?.value ?? "";
    const isBowling = selected.toLowerCase().includes("bowling");
    
    const bowlerTypeLabel = document.getElementById("compareBowlerType")?.closest("label");
    if (bowlerTypeLabel) {
        if (isBowling) {
            bowlerTypeLabel.classList.remove("hidden");
        } else {
            bowlerTypeLabel.classList.add("hidden");
        }
    }
};

const applyRankingsTemplateHints = () => {
    const selected = rankingsTemplateSelect.options[rankingsTemplateSelect.selectedIndex]?.value ?? "";
    const isBowling = selected.toLowerCase().includes("bowling");
    
    const bowlerTypeLabel = document.getElementById("rankingsBowlerType")?.closest("label");
    if (bowlerTypeLabel) {
        if (isBowling) {
            bowlerTypeLabel.classList.remove("hidden");
        } else {
            bowlerTypeLabel.classList.add("hidden");
        }
    }
};

const resetCompareSelection = (side) => {
    if (side === "A") {
        comparePlayerASelectedIdInput.value = "";
        compareACandidates = [];
        compareAFingerprint = "";
        comparePlayerASelect.innerHTML = '<option value="">Choose a player</option>';
        comparePlayerANote.textContent = "";
        comparePlayerAPicker.classList.add("hidden");
    } else {
        comparePlayerBSelectedIdInput.value = "";
        compareBCandidates = [];
        compareBFingerprint = "";
        comparePlayerBSelect.innerHTML = '<option value="">Choose a player</option>';
        comparePlayerBNote.textContent = "";
        comparePlayerBPicker.classList.add("hidden");
    }
};

const populateComparePicker = (select, candidates) => {
    select.innerHTML = '<option value="">Choose a player</option>';
    candidates.forEach((candidate) => {
        const option = document.createElement("option");
        option.value = String(candidate.playerId);
        option.textContent = candidate.displayName;
        select.appendChild(option);
    });
};

const ensureCompareSelection = async (side) => {
    const isA = side === "A";
    const nameInput = isA ? comparePlayerANameInput : comparePlayerBNameInput;
    const templateKey = compareTemplateSelect.value;
    const selectedIdInput = isA ? comparePlayerASelectedIdInput : comparePlayerBSelectedIdInput;
    const picker = isA ? comparePlayerAPicker : comparePlayerBPicker;
    const pickerNote = isA ? comparePlayerANote : comparePlayerBNote;
    const pickerSelect = isA ? comparePlayerASelect : comparePlayerBSelect;
    const fingerprint = `${nameInput.value.trim()}|${templateKey}`;
    const lastFingerprint = isA ? compareAFingerprint : compareBFingerprint;
    const candidates = isA ? compareACandidates : compareBCandidates;

    if (fingerprint !== lastFingerprint || candidates.length === 0) {
        const searchResult = await searchComparePlayers(nameInput.value.trim(), templateKey);
        if (searchResult.candidates.length === 0) {
            resetCompareSelection(side);
            throw new Error(`No Statsguru player match was found for '${nameInput.value.trim()}'.`);
        }

        if (isA) {
            compareACandidates = searchResult.candidates;
            compareAFingerprint = fingerprint;
        } else {
            compareBCandidates = searchResult.candidates;
            compareBFingerprint = fingerprint;
        }

        if (searchResult.autoSelected && searchResult.selectedCandidate) {
            selectedIdInput.value = String(searchResult.selectedCandidate.playerId);
            picker.classList.add("hidden");
            pickerNote.textContent = "";
            return;
        }

        populateComparePicker(pickerSelect, searchResult.candidates);
        picker.classList.remove("hidden");
        pickerNote.textContent = "Multiple matches found. Pick the exact player and compare again.";
        throw new Error(`Pick the exact ${isA ? "Player A" : "Player B"} from the list below.`);
    }

    if (!selectedIdInput.value.trim()) {
        picker.classList.remove("hidden");
        throw new Error(`Pick the exact ${isA ? "Player A" : "Player B"} from the list below.`);
    }
};

const renderCompareResults = (data) => {
    compareSummary.innerHTML = "";
    compareResults.innerHTML = "";

    const summaryA = document.createElement("article");
    summaryA.className = "highlight-card";
    summaryA.innerHTML = `<h3>${data.playerA.matchedPlayerName}</h3><div class="metric-row"><div class="metric-pill"><strong>${formatMetric(data.playerA.hammyVizScore)}</strong><span>HammyViz</span></div></div>`;

    const summaryB = document.createElement("article");
    summaryB.className = "highlight-card";
    summaryB.innerHTML = `<h3>${data.playerB.matchedPlayerName}</h3><div class="metric-row"><div class="metric-pill"><strong>${formatMetric(data.playerB.hammyVizScore)}</strong><span>HammyViz</span></div></div>`;

    compareSummary.append(summaryA, summaryB);
    compareSummary.classList.remove("hidden");

    const table = document.createElement("table");
    table.className = "compare-table";
    table.innerHTML = `
        <thead>
            <tr>
                <th>Metric</th>
                <th>${data.playerA.matchedPlayerName}</th>
                <th>${data.playerA.matchedPlayerName} diff</th>
                <th>${data.playerB.matchedPlayerName}</th>
                <th>${data.playerB.matchedPlayerName} diff</th>
            </tr>
        </thead>
        <tbody></tbody>
    `;

    const tbody = table.querySelector("tbody");
    data.rows.forEach((row) => {
        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${row.label}</td>
            <td>${formatMetric(row.playerAValue)} <div class="comparison-note">vs ${formatMetric(row.playerABaseline)}</div></td>
            <td class="${compareClassName(row.playerADelta)}">${formatMetric(row.playerADelta)}</td>
            <td>${formatMetric(row.playerBValue)} <div class="comparison-note">vs ${formatMetric(row.playerBBaseline)}</div></td>
            <td class="${compareClassName(row.playerBDelta)}">${formatMetric(row.playerBDelta)}</td>
        `;
        tbody.appendChild(tr);
    });

    compareResults.appendChild(table);
    compareResults.classList.remove("hidden");
};

const renderRankingsResults = (data) => {
    latestRankings = data;
    rankingsResults.innerHTML = "";

    const table = document.createElement("table");
    table.className = "compare-table";
    table.innerHTML = `
        <thead>
            <tr>
                <th>Rank</th>
                <th>Player</th>
                <th>HammyViz</th>
                <th>Raw avg</th>
                <th class="discipline-overall-header">Overall</th>
                <th class="pacer-only hidden">Pace</th>
                <th class="spinner-only hidden">Spin</th>
                <th>TOP 7</th>
                <th class="host-group-column-header">SENA</th>
                <th>TOP 5 HOSTS</th>
                <th>TOUGHEST TOP 7</th>
            </tr>
        </thead>
        <tbody></tbody>
    `;

    const tbody = table.querySelector("tbody");
    const hostGroupSelect = document.getElementById("rankingsHostGroup");
    const hostGroupName = hostGroupSelect?.value || "SENA";
    const isBowling = data.discipline.toLowerCase() === "bowling";

    if (isBowling) {
        table.querySelectorAll(".pacer-only, .spinner-only").forEach(el => el.classList.remove("hidden"));
        table.querySelector(".discipline-overall-header").textContent = "All bowlers";
    } else {
        table.querySelector(".discipline-overall-header").textContent = "All batters";
    }

    const header = table.querySelector(".host-group-column-header");
    if (header) {
        header.textContent = hostGroupName;
    }

    data.rows.forEach((row) => {
        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${row.rank}</td>
            <td>${row.playerName}</td>
            <td class="${compareClassName(row.hammyVizScore)}">${formatMetric(row.hammyVizScore)}</td>
            <td>${formatMetric(row.rawAverage)}</td>
            <td class="${compareClassName(row.overallDelta)}">${formatMetric(row.overallDelta)}</td>
            <td class="pacer-only hidden ${row.paceDelta === null ? "dimmed" : compareClassName(row.paceDelta)}">${formatMetric(row.paceDelta)}</td>
            <td class="spinner-only hidden ${row.spinDelta === null ? "dimmed" : compareClassName(row.spinDelta)}">${formatMetric(row.spinDelta)}</td>
            <td class="${compareClassName(row.top7Delta)}">${formatMetric(row.top7Delta)}</td>
            <td class="${compareClassName(row.senaDelta)}">${formatMetric(row.senaDelta)}</td>
            <td class="${compareClassName(row.toughestHostsDelta)}">${formatMetric(row.toughestHostsDelta)}</td>
            <td class="${compareClassName(row.toughestHostsTop7Delta)}">${formatMetric(row.toughestHostsTop7Delta)}</td>
        `;
        tbody.appendChild(tr);
    });

    rankingsResults.appendChild(table);
    rankingsResults.classList.remove("hidden");

    const csvRows = [
        ["Rank", "Player", "HammyViz", "RawAverage", "OverallDelta", "PaceDelta", "SpinDelta", "Top7Delta", "SenaDelta", "Top5HostsDelta"],
        ...data.rows.map((row) => [
            row.rank,
            row.playerName,
            row.hammyVizScore,
            row.rawAverage,
            row.overallDelta,
            row.paceDelta,
            row.spinDelta,
            row.top7Delta,
            row.senaDelta,
            row.toughestHostsDelta
        ])
    ];

    const csv = csvRows
        .map((row) => row.map((value) => `"${value ?? ""}"`).join(","))
        .join("\n");

    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    exportRankingsButton.href = URL.createObjectURL(blob);
};

const loadTemplates = async () => {
    const response = await fetch("/templates");
    if (!response.ok) {
        throw new Error("Could not load templates.");
    }

    const data = await response.json();
    templateSelect.innerHTML = "";
    compareTemplateSelect.innerHTML = "";

    data.templates.forEach((template) => {
        const option = document.createElement("option");
        option.value = template.key;
        option.textContent = template.label;
        templateSelect.appendChild(option);

        const compareOption = document.createElement("option");
        compareOption.value = template.key;
        compareOption.textContent = template.label;
        compareTemplateSelect.appendChild(compareOption);

        const rankingsOption = document.createElement("option");
        rankingsOption.value = template.key;
        rankingsOption.textContent = template.label;
        rankingsTemplateSelect.appendChild(rankingsOption);
    });

    templateSelect.value = "test-batting-profile";
    compareTemplateSelect.value = "test-batting-profile";
    rankingsTemplateSelect.value = "test-batting-profile";
    applyTemplateHints();
    statusText.textContent = "Templates loaded. Search any player profile.";
};

const loadProfile = async () => {
    clearError();
    submitButton.disabled = true;
    submitButton.textContent = "Loading...";
    statusText.textContent = "Searching Statsguru players...";

    try {
        await ensurePlayerSelection();

        statusText.textContent = "Fetching profile from Statsguru...";

        const profileUrl = buildProfileUrl();
        rawApiLink.href = profileUrl;

        const response = await fetch(profileUrl);
        const data = await response.json();

        if (!response.ok) {
            throw new Error(data.error || "Unable to load profile.");
        }

        profileTitle.textContent = data.matchedPlayerName;
        profileSubtitle.textContent = `${data.templateLabel} | ${data.format} | ${data.discipline}${data.teamName ? ` | ${data.teamName}` : ""}${data.hammyVizScore !== null && data.hammyVizScore !== undefined ? ` | HammyViz ${formatMetric(data.hammyVizScore)}` : ""}`;

        renderHighlights(data);
        renderHammyVizSummary(highlightGrid, data);
        renderHammyVizBreakdown(data);
        renderComparisons(data);
        renderNotes(data);

        summaryPanel.classList.remove("hidden");
        hammyvizPanel.classList.remove("hidden");
        comparisonPanel.classList.remove("hidden");
        notesPanel.classList.remove("hidden");
        statusText.textContent = data.servedFromCache
            ? `Loaded ${data.comparisons.length} comparison blocks for ${data.matchedPlayerName} from cache.`
            : `Loaded ${data.comparisons.length} comparison blocks for ${data.matchedPlayerName} from live Statsguru data.`;
    } catch (error) {
        hideResults();
        showError(error.message || "Something went wrong.");
    } finally {
        submitButton.disabled = false;
        submitButton.textContent = "Load profile";
    }
};

form.addEventListener("submit", async (event) => {
    event.preventDefault();
    await loadProfile();
});

compareForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    compareStatus.classList.remove("error");
    compareSubmitButton.disabled = true;
    compareSubmitButton.textContent = "Comparing...";

    try {
        await ensureCompareSelection("A");
        await ensureCompareSelection("B");

        const params = new URLSearchParams({
            playerAName: comparePlayerANameInput.value.trim(),
            playerBName: comparePlayerBNameInput.value.trim(),
            templateKey: compareTemplateSelect.value
        });

        if (comparePlayerASelectedIdInput.value.trim()) {
            params.set("playerASelectedPlayerId", comparePlayerASelectedIdInput.value.trim());
        }

        if (comparePlayerBSelectedIdInput.value.trim()) {
            params.set("playerBSelectedPlayerId", comparePlayerBSelectedIdInput.value.trim());
        }

        const bowlerTypeSelect = document.getElementById("compareBowlerType");
        if (bowlerTypeSelect?.value) {
            params.set("bowlerType", bowlerTypeSelect.value);
        }

        const hostGroupSelect = document.getElementById("compareHostGroup");
        if (hostGroupSelect?.value) {
            params.set("hostGroup", hostGroupSelect.value);
        }

        const response = await fetch(`/compare/players?${params.toString()}`);
        const data = await response.json();

        if (!response.ok) {
            throw new Error(data.error || "Unable to compare players.");
        }

        renderCompareResults(data);
        compareStatus.textContent = `Compared ${data.playerA.matchedPlayerName} and ${data.playerB.matchedPlayerName}.`;
    } catch (error) {
        compareSummary.classList.add("hidden");
        compareResults.classList.add("hidden");
        compareStatus.textContent = error.message || "Something went wrong.";
        compareStatus.classList.add("error");
    } finally {
        compareSubmitButton.disabled = false;
        compareSubmitButton.textContent = "Compare players";
    }
});

playerCandidateSelect.addEventListener("change", () => {
    selectedPlayerIdInput.value = playerCandidateSelect.value;

    if (selectedPlayerIdInput.value) {
        const selected = currentCandidates.find((candidate) => String(candidate.playerId) === selectedPlayerIdInput.value);
        playerPickerNote.textContent = selected
            ? `Selected ${selected.displayName}. Click Load profile to continue.`
            : "Click Load profile to continue.";
    } else {
        playerPickerNote.textContent = "Pick the exact player and then load the profile.";
    }
});

[playerNameInput, templateSelect]
    .forEach((element) => {
        element.addEventListener("input", resetPlayerSelection);
        element.addEventListener("change", resetPlayerSelection);
    });

templateSelect.addEventListener("change", applyTemplateHints);
rankingsTemplateSelect.addEventListener("change", applyRankingsTemplateHints);
compareTemplateSelect.addEventListener("change", () => {
    resetCompareSelection("A");
    resetCompareSelection("B");
    applyCompareTemplateHints();
});

comparePlayerASelect.addEventListener("change", () => {
    comparePlayerASelectedIdInput.value = comparePlayerASelect.value;
    comparePlayerANote.textContent = comparePlayerASelectedIdInput.value ? "Player A selected. Compare again to continue." : "Pick Player A.";
});

comparePlayerBSelect.addEventListener("change", () => {
    comparePlayerBSelectedIdInput.value = comparePlayerBSelect.value;
    comparePlayerBNote.textContent = comparePlayerBSelectedIdInput.value ? "Player B selected. Compare again to continue." : "Pick Player B.";
});

rankingsForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    rankingsStatus.classList.remove("error");
    rankingsSubmitButton.disabled = true;
    rankingsSubmitButton.textContent = "Loading...";

    try {
        const limit = Math.max(1, Math.min(100, Number(rankingsLimitInput.value || 10)));
        rankingsLimitInput.value = String(limit);
        const rankingsHostGroupSelect = document.getElementById("rankingsHostGroup");
        const rankingsHostGroup = rankingsHostGroupSelect?.value || "SENA";

        const rankingsBowlerTypeSelect = document.getElementById("rankingsBowlerType");
        const rankingsBowlerType = rankingsBowlerTypeSelect?.value || "";

        const params = new URLSearchParams({
            templateKey: rankingsTemplateSelect.value,
            limit: String(limit),
            hostGroup: rankingsHostGroup
        });

        if (rankingsBowlerType) {
            params.set("bowlerType", rankingsBowlerType);
        }

        const response = await fetch(`/rankings/hammyviz?${params.toString()}`);
        const data = await response.json();

        if (!response.ok) {
            throw new Error(data.error || "Unable to load HammyViz rankings.");
        }

        renderRankingsResults(data);
        rankingsStatus.textContent = `Loaded top ${data.rows.length} players sorted by HammyViz.`;
    } catch (error) {
        rankingsResults.classList.add("hidden");
        rankingsStatus.textContent = error.message || "Something went wrong.";
        rankingsStatus.classList.add("error");
    } finally {
        rankingsSubmitButton.disabled = false;
        rankingsSubmitButton.textContent = "Load rankings";
    }
});

top10Button.addEventListener("click", () => {
    rankingsLimitInput.value = "10";
    rankingsForm.requestSubmit();
});

top20Button.addEventListener("click", () => {
    rankingsLimitInput.value = "20";
    rankingsForm.requestSubmit();
});

[comparePlayerANameInput, comparePlayerBNameInput]
    .forEach((element, index) => {
        element.addEventListener("input", () => resetCompareSelection(index === 0 ? "A" : "B"));
        element.addEventListener("change", () => resetCompareSelection(index === 0 ? "A" : "B"));
    });

window.addEventListener("DOMContentLoaded", async () => {
    try {
        await loadTemplates();
        applyTemplateHints();
        applyCompareTemplateHints();
        applyRankingsTemplateHints();
    } catch (error) {
        showError(error.message || "Unable to initialize the page.");
    }
});
