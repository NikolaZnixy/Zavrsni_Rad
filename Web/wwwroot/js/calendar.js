// Calendar control behaviour - click a day to render its transactions into the inline detail panel.
// Only loaded on pages that embed the Calendar view component in non-compact mode.
(function () {
    var grid = document.querySelector("[data-calendar-grid]");
    var panel = document.getElementById("calendarDetailPanel");
    if (!grid || !panel) return;

    var dayButtons = Array.prototype.slice.call(grid.querySelectorAll(".calendar-day[data-transactions]"));
    if (dayButtons.length === 0) return;

    function formatAmount(amount, currency) {
        var sign = amount < 0 ? "-" : "+";
        return sign + Math.abs(amount).toFixed(2) + " " + currency;
    }

    function renderDetail(button) {
        dayButtons.forEach(function (b) { b.classList.remove("is-selected"); });
        button.classList.add("is-selected");

        var dateValue = button.getAttribute("data-date");
        var transactions = [];
        try {
            transactions = JSON.parse(button.getAttribute("data-transactions") || "[]");
        } catch (err) {
            transactions = [];
        }

        var label = new Date(dateValue + "T00:00:00").toLocaleDateString(undefined, {
            weekday: "long",
            month: "long",
            day: "numeric"
        });

        if (transactions.length === 0) {
            panel.innerHTML =
                '<p class="calendar-detail-date">' + label + "</p>" +
                '<p class="calendar-detail-empty">No transactions</p>';
            return;
        }

        var rows = transactions.map(function (t) {
            var amountClass = t.amount < 0 ? "is-negative" : "is-positive";
            var recurringIcon = t.recurring
                ? '<i class="fa-solid fa-arrows-rotate calendar-icon-recurring" aria-hidden="true"></i>'
                : "";
            var categoryBadge = t.category
                ? '<span class="calendar-detail-category">' + t.category + "</span>"
                : "";

            return (
                '<div class="calendar-detail-row">' +
                '<div class="calendar-detail-desc">' +
                recurringIcon +
                "<span>" + t.desc + "</span>" +
                categoryBadge +
                "</div>" +
                '<span class="calendar-detail-amount ' + amountClass + '">' + formatAmount(t.amount, t.currency) + "</span>" +
                "</div>"
            );
        }).join("");

        panel.innerHTML = '<p class="calendar-detail-date">' + label + "</p>" + rows;
    }

    dayButtons.forEach(function (button) {
        button.addEventListener("click", function () { renderDetail(button); });
    });

    var openDay = grid.getAttribute("data-open-day");
    var initial = null;

    if (openDay && openDay !== "0") {
        initial = dayButtons.filter(function (b) { return b.getAttribute("data-day") === openDay; })[0] || null;
    }
    if (!initial) {
        initial = dayButtons.filter(function (b) { return b.classList.contains("is-today"); })[0] || null;
    }
    if (initial) renderDetail(initial);
})();
