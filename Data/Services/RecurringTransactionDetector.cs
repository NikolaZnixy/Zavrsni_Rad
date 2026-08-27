using Data.Model;
using System.Text.RegularExpressions;

namespace Data.Services
{
    /// <summary>
    /// Flags transactions that look like recurring payments (subscriptions, bills, etc.) purely by pattern -
    /// no stored flag, nothing persisted. Groups same-account expenses by a normalized description and a
    /// tolerant amount match, then checks whether the gaps between occurrences fall into a known cadence
    /// (weekly/bi-weekly/monthly) or are otherwise just consistent enough to call "recurring".
    /// </summary>
    public static class RecurringTransactionDetector
    {
        private const int MinOccurrences = 3;
        private const decimal AmountTolerancePct = 0.05m;
        private const double MaxCoefficientOfVariation = 0.15;

        private static readonly (double MinDays, double MaxDays)[] KnownCadences =
        {
            (6, 8),   // weekly
            (13, 16), // bi-weekly
            (27, 33)  // monthly
        };

        /// <summary>Returns the ids of every transaction that belongs to a detected recurring group.</summary>
        public static HashSet<Guid> Detect(IEnumerable<BankAccountTransaction> transactions)
        {
            var recurringIds = new HashSet<Guid>();

            var groups = transactions
                .Where(t => t.Amount < 0 && !string.IsNullOrWhiteSpace(t.Description))
                .GroupBy(t => (t.LinkedBankAccountId, Description: Normalize(t.Description!)));

            foreach (var group in groups)
            {
                var ordered = group.OrderBy(t => t.TransactionDate).ToList();

                foreach (var cluster in ClusterByAmount(ordered))
                {
                    if (cluster.Count < MinOccurrences || !HasConsistentCadence(cluster))
                        continue;

                    foreach (var transaction in cluster)
                        recurringIds.Add(transaction.Id);
                }
            }

            return recurringIds;
        }

        // Strips digits (dates, invoice numbers) so "NETFLIX.COM 08/2026" and "NETFLIX.COM 09/2026" group together.
        private static string Normalize(string description)
        {
            var noDigits = Regex.Replace(description, @"\d+", "");
            return Regex.Replace(noDigits, @"\s+", " ").Trim().ToLowerInvariant();
        }

        // Same description can still cover unrelated charges (e.g. a generic "PAYPAL" merchant string) -
        // splitting by amount keeps distinct recurring amounts (Netflix vs. Spotify via the same processor)
        // from being merged into one false-positive group.
        private static List<List<BankAccountTransaction>> ClusterByAmount(List<BankAccountTransaction> ordered)
        {
            var clusters = new List<List<BankAccountTransaction>>();

            foreach (var transaction in ordered)
            {
                var amount = Math.Abs(transaction.Amount);
                var cluster = clusters.FirstOrDefault(c =>
                {
                    var avg = c.Average(x => Math.Abs(x.Amount));
                    return avg != 0 && Math.Abs(amount - avg) / avg <= AmountTolerancePct;
                });

                if (cluster is not null)
                    cluster.Add(transaction);
                else
                    clusters.Add(new List<BankAccountTransaction> { transaction });
            }

            return clusters;
        }

        private static bool HasConsistentCadence(List<BankAccountTransaction> cluster)
        {
            var dates = cluster.Select(t => t.TransactionDate).OrderBy(d => d).ToList();

            var gaps = new List<double>();
            for (var i = 1; i < dates.Count; i++)
                gaps.Add(dates[i].DayNumber - dates[i - 1].DayNumber);

            var meanGap = gaps.Average();
            if (meanGap <= 0)
                return false;

            if (KnownCadences.Any(c => meanGap >= c.MinDays && meanGap <= c.MaxDays))
                return true;

            var variance = gaps.Sum(g => Math.Pow(g - meanGap, 2)) / gaps.Count;
            var coefficientOfVariation = Math.Sqrt(variance) / meanGap;

            return coefficientOfVariation <= MaxCoefficientOfVariation;
        }
    }
}
