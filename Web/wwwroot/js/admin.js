// Service health panel - each card checks itself only when its own button is clicked, nothing runs
// automatically. Only loaded on /Admin.
(function () {
    document.querySelectorAll("[data-service-card]").forEach(function (card) {
        var btn = card.querySelector("[data-check-btn]");
        var dot = card.querySelector("[data-status-dot]");
        var text = card.querySelector("[data-status-text]");
        var detail = card.querySelector("[data-status-detail]");
        var endpoint = card.dataset.endpoint;

        btn.addEventListener("click", async function () {
            btn.disabled = true;
            var originalLabel = btn.textContent;
            btn.textContent = "Checking...";

            dot.className = "status-dot is-checking";
            text.textContent = "Checking...";
            detail.textContent = "";

            try {
                var response = await fetch(endpoint, { method: "POST" });
                var result = await response.json();

                if (!result.configured) {
                    dot.className = "status-dot is-not-configured";
                    text.textContent = "Not configured";
                } else if (result.healthy) {
                    dot.className = "status-dot is-healthy";
                    text.textContent = "Healthy";
                } else {
                    dot.className = "status-dot is-unhealthy";
                    text.textContent = "Unhealthy";
                }

                var detailParts = [];
                if (result.message) detailParts.push(result.message);
                if (result.configured && typeof result.latencyMs === "number") detailParts.push(result.latencyMs + " ms");
                detail.textContent = detailParts.join(" — ");
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
})();
