// Admin panel - service health checks and the activity log window. Only loaded on /Dashboard/Admin.
(function () {
    // --- Service health cards -------------------------------------------------------------------
    document.querySelectorAll("[data-service-card]").forEach(function (card) {
        var btn = card.querySelector("[data-check-btn]");
        var dot = card.querySelector("[data-status-dot]");
        var text = card.querySelector("[data-status-text]");
        var detail = card.querySelector("[data-status-detail]");
        var metrics = card.querySelector("[data-status-metrics]");
        var endpoint = card.dataset.endpoint;

        function renderMetrics(list) {
            if (!metrics) return;
            metrics.innerHTML = "";

            if (!list || !list.length) {
                metrics.hidden = true;
                return;
            }

            list.forEach(function (metric) {
                var dt = document.createElement("dt");
                dt.textContent = metric.label;
                var dd = document.createElement("dd");
                dd.textContent = metric.value;
                dd.className = "metric-" + (metric.status || "neutral");
                metrics.appendChild(dt);
                metrics.appendChild(dd);
            });
            metrics.hidden = false;
        }

        btn.addEventListener("click", async function () {
            btn.disabled = true;
            var originalLabel = btn.textContent;
            btn.textContent = "Checking...";

            dot.className = "status-dot is-checking";
            text.textContent = "Checking...";
            detail.textContent = "";
            renderMetrics(null);

            try {
                var response = await fetch(endpoint, { method: "POST" });
                var result = await response.json();

                if (!result.configured) {
                    dot.className = "status-dot is-not-configured";
                    text.textContent = "Not configured";
                } else if (result.healthy) {
                    dot.className = "status-dot is-healthy";
                    text.textContent = "Healthy" + (result.provider ? " — " + result.provider : "");
                } else {
                    dot.className = "status-dot is-unhealthy";
                    text.textContent = "Unhealthy" + (result.provider ? " — " + result.provider : "");
                }

                var detailParts = [];
                if (result.message) detailParts.push(result.message);
                if (result.configured && typeof result.latencyMs === "number") detailParts.push(result.latencyMs + " ms total");
                detail.textContent = detailParts.join(" — ");

                renderMetrics(result.metrics);
            } catch (err) {
                dot.className = "status-dot is-unhealthy";
                text.textContent = "Check failed";
                detail.textContent = err.message;
            } finally {
                btn.disabled = false;
                btn.textContent = originalLabel;
            }
        });
    });

    // --- Activity log window -------------------------------------------------------------------
    var panel = document.querySelector("[data-log-panel]");
    if (!panel) return;

    var logsEndpoint = panel.dataset.logsEndpoint;
    var clearEndpoint = panel.dataset.clearEndpoint;
    var levelSelect = panel.querySelector("[data-log-level]");
    var categorySelect = panel.querySelector("[data-log-category]");
    var searchInput = panel.querySelector("[data-log-search]");
    var body = panel.querySelector("[data-log-body]");
    var table = panel.querySelector("[data-log-table]");
    var empty = panel.querySelector("[data-log-empty]");
    var count = panel.querySelector("[data-log-count]");
    var autoToggle = panel.querySelector("[data-log-auto]");
    var refreshBtn = panel.querySelector("[data-log-refresh]");
    var clearBtn = panel.querySelector("[data-log-clear]");

    var knownCategories = [];
    var timer = null;
    var searchDebounce = null;

    function formatTime(iso) {
        var date = new Date(iso);
        if (isNaN(date)) return iso;
        return date.toLocaleString(undefined, {
            month: "2-digit", day: "2-digit",
            hour: "2-digit", minute: "2-digit", second: "2-digit"
        });
    }

    function syncCategories(categories) {
        if (!categories) return;
        var same = categories.length === knownCategories.length
            && categories.every(function (c, i) { return c === knownCategories[i]; });
        if (same) return;

        knownCategories = categories;
        var current = categorySelect.value;
        categorySelect.innerHTML = '<option value="">All categories</option>';
        categories.forEach(function (category) {
            var option = document.createElement("option");
            option.value = category;
            option.textContent = category;
            categorySelect.appendChild(option);
        });
        categorySelect.value = current;
    }

    function buildRow(entry) {
        var row = document.createElement("tr");
        row.className = "log-row log-row-" + (entry.level || "Info").toLowerCase();

        function cell(value, className) {
            var td = document.createElement("td");
            td.textContent = value === null || value === undefined || value === "" ? "—" : value;
            if (className) td.className = className;
            return td;
        }

        row.appendChild(cell(formatTime(entry.timestamp), "log-cell-time"));

        var levelCell = document.createElement("td");
        var badge = document.createElement("span");
        badge.className = "log-level log-level-" + (entry.level || "Info").toLowerCase();
        badge.textContent = entry.level;
        levelCell.appendChild(badge);
        row.appendChild(levelCell);

        row.appendChild(cell(entry.category));
        row.appendChild(cell(entry.action));

        var messageCell = cell(entry.message, "log-cell-message");
        if (entry.statusCode) {
            var status = document.createElement("span");
            status.className = "log-status";
            status.textContent = entry.statusCode;
            messageCell.appendChild(status);
        }
        row.appendChild(messageCell);

        row.appendChild(cell(entry.userName));
        row.appendChild(cell(typeof entry.durationMs === "number" ? entry.durationMs + " ms" : null, "log-cell-time"));

        if (entry.detail || entry.path) {
            row.classList.add("log-row-expandable");
            row.title = "Click for details";
            var detailRow = document.createElement("tr");
            detailRow.className = "log-detail-row";
            detailRow.hidden = true;
            var detailCell = document.createElement("td");
            detailCell.colSpan = 7;
            var parts = [];
            if (entry.method || entry.path) parts.push((entry.method || "") + " " + (entry.path || ""));
            if (entry.ipAddress) parts.push("IP: " + entry.ipAddress);
            if (entry.detail) parts.push(entry.detail);
            detailCell.textContent = parts.join("\n");
            detailRow.appendChild(detailCell);

            row.addEventListener("click", function () {
                detailRow.hidden = !detailRow.hidden;
            });

            return [row, detailRow];
        }

        return [row];
    }

    async function load() {
        var params = new URLSearchParams();
        if (levelSelect.value) params.set("level", levelSelect.value);
        if (categorySelect.value) params.set("category", categorySelect.value);
        if (searchInput.value.trim()) params.set("search", searchInput.value.trim());
        params.set("take", "200");

        try {
            var response = await fetch(logsEndpoint + "?" + params.toString(), {
                headers: { "Accept": "application/json" }
            });
            if (!response.ok) throw new Error("Log request failed (" + response.status + ")");
            var data = await response.json();

            syncCategories(data.categories);

            body.innerHTML = "";
            (data.entries || []).forEach(function (entry) {
                buildRow(entry).forEach(function (node) { body.appendChild(node); });
            });

            var hasEntries = (data.entries || []).length > 0;
            table.hidden = !hasEntries;
            empty.hidden = hasEntries;
            if (!hasEntries) empty.textContent = "No activity matches these filters yet.";
            count.textContent = hasEntries ? data.entries.length + " entries" : "";
        } catch (err) {
            table.hidden = true;
            empty.hidden = false;
            empty.textContent = err.message;
        }
    }

    function restartTimer() {
        if (timer) clearInterval(timer);
        if (autoToggle.checked) timer = setInterval(load, 5000);
    }

    levelSelect.addEventListener("change", load);
    categorySelect.addEventListener("change", load);
    searchInput.addEventListener("input", function () {
        if (searchDebounce) clearTimeout(searchDebounce);
        searchDebounce = setTimeout(load, 300);
    });
    refreshBtn.addEventListener("click", load);
    autoToggle.addEventListener("change", restartTimer);

    clearBtn.addEventListener("click", async function () {
        if (!window.confirm("Delete all activity log entries?")) return;
        clearBtn.disabled = true;
        try {
            await fetch(clearEndpoint, { method: "POST" });
            await load();
        } finally {
            clearBtn.disabled = false;
        }
    });

    load();
    restartTimer();
})();
