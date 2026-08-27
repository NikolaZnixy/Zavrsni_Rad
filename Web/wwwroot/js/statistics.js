// Builds the 5 statistics charts from the JSON payload the view embeds. Only loaded on /Statistics.
(function () {
    var dataScript = document.getElementById("statisticsData");
    if (!dataScript || typeof Chart === "undefined") return;

    var data = JSON.parse(dataScript.textContent);

    Chart.defaults.color = "rgba(245, 245, 245, 0.7)";
    Chart.defaults.borderColor = "rgba(255, 255, 255, 0.08)";
    Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;

    function currency(value) {
        return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(value);
    }

    function chart(canvasId, config) {
        var canvas = document.getElementById(canvasId);
        if (!canvas) return;
        new Chart(canvas, config);
    }

    chart("chartSpendingOverTime", {
        type: "line",
        data: {
            labels: data.monthLabels,
            datasets: [{
                label: "Spent",
                data: data.monthlySpending,
                borderColor: "#ff6b6b",
                backgroundColor: "rgba(255, 107, 107, 0.15)",
                fill: true,
                tension: 0.3,
                pointRadius: 3,
                pointBackgroundColor: "#ff6b6b"
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { beginAtZero: true, ticks: { callback: currency } },
                x: { grid: { display: false } }
            }
        }
    });

    chart("chartIncomeVsExpenses", {
        type: "bar",
        data: {
            labels: data.monthLabels,
            datasets: [
                { label: "Income", data: data.monthlyIncome, backgroundColor: "#51cf66", borderRadius: 4 },
                { label: "Expenses", data: data.monthlySpending, backgroundColor: "#ff6b6b", borderRadius: 4 }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: "top", labels: { boxWidth: 12, usePointStyle: true } } },
            scales: {
                y: { beginAtZero: true, ticks: { callback: currency } },
                x: { grid: { display: false } }
            }
        }
    });

    function capitalize(text) {
        return text.charAt(0).toUpperCase() + text.slice(1);
    }

    chart("chartCategoryBreakdown", {
        type: "doughnut",
        data: {
            labels: data.categoryBreakdown.map(function (c) { return capitalize(c.name); }),
            datasets: [{
                data: data.categoryBreakdown.map(function (c) { return c.amount; }),
                backgroundColor: data.categoryBreakdown.map(function (c) { return c.color; }),
                borderColor: "#202020",
                borderWidth: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: "right", labels: { boxWidth: 12, usePointStyle: true } } }
        }
    });

    chart("chartTopMerchants", {
        type: "bar",
        data: {
            labels: data.topMerchants.map(function (m) { return m.description; }),
            datasets: [{
                data: data.topMerchants.map(function (m) { return m.amount; }),
                backgroundColor: "#4dabf7",
                borderRadius: 4
            }]
        },
        options: {
            indexAxis: "y",
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { beginAtZero: true, ticks: { callback: currency } },
                y: { grid: { display: false } }
            }
        }
    });

    chart("chartWeekdayAverages", {
        type: "bar",
        data: {
            labels: data.weekdayLabels,
            datasets: [{
                data: data.weekdayAverages,
                backgroundColor: "#ffd43b",
                borderRadius: 4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { beginAtZero: true, ticks: { callback: currency } },
                x: { grid: { display: false } }
            }
        }
    });
})();
