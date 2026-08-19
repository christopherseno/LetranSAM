using System;
using System.Collections.Generic;
using System.Linq;

namespace ARManila.Models.OtherDTO
{
    // Period-wide summary: per account/fee, the deferred (assessed) amount per month and the
    // recognized (posted) amount per month + total, plus adjustments and net adjusted fees.
    // Month columns are dynamic (one per posted month in the period).
    public class SummaryReportDTO
    {
        public SummaryReportDTO()
        {
            Months = new List<string>();
            Sections = new List<SummarySectionDTO>();
        }

        public string ReportTitle { get; set; }
        public string PeriodName { get; set; }
        public string PeriodSubtitle { get; set; }
        public bool ShowAdjustments { get; set; }     // Adjustments / Net columns (Deferred Income only)
        public List<string> Months { get; set; }      // formatted "30-Jun"
        public int MonthCount { get { return Months.Count; } }
        public List<SummarySectionDTO> Sections { get; set; }
        public SummaryRowDTO GrandTotal { get; set; }
    }

    public class SummarySectionDTO
    {
        public SummarySectionDTO()
        {
            Rows = new List<SummaryRowDTO>();
        }
        public string AcctCode { get; set; }
        public string Title { get; set; }
        public bool SingleRow { get; set; }           // Tuition-style: no banner/subtotal, one line
        public List<SummaryRowDTO> Rows { get; set; }
        public SummaryRowDTO Subtotal { get; set; }
    }

    public class SummaryRowDTO
    {
        public SummaryRowDTO(int months)
        {
            Deferred = new decimal?[months];
            Recognized = new decimal?[months];
        }

        public string AcctCode { get; set; }
        public string Particular { get; set; }
        public decimal?[] Deferred { get; set; }      // null => blank cell
        public decimal?[] Recognized { get; set; }
        public decimal RecognizedTotal { get; set; }
        public decimal Adjustments { get; set; }
        public decimal NetAdjusted { get; set; }

        public void AddDeferred(int i, decimal v) { Deferred[i] = (Deferred[i] ?? 0m) + v; }
        public void AddRecognized(int i, decimal v) { Recognized[i] = (Recognized[i] ?? 0m) + v; }

        // Roll this row's values into an accumulator row (subtotal / grand total).
        public void AccumulateInto(SummaryRowDTO acc)
        {
            for (int i = 0; i < Recognized.Length; i++)
            {
                if (Deferred[i].HasValue) { acc.Deferred[i] = (acc.Deferred[i] ?? 0m) + Deferred[i].Value; }
                if (Recognized[i].HasValue) { acc.Recognized[i] = (acc.Recognized[i] ?? 0m) + Recognized[i].Value; }
            }
            acc.RecognizedTotal += RecognizedTotal;
            acc.Adjustments += Adjustments;
            acc.NetAdjusted += NetAdjusted;
        }

        // Finalize totals from the per-month recognized values.
        public void Finalize()
        {
            RecognizedTotal = Recognized.Where(v => v.HasValue).Sum(v => v.Value);
            NetAdjusted = RecognizedTotal + Adjustments;
        }
    }

    // Builds the summary for the memo-type reports (Memo/Discount/AdjDiscount) from their period-wide
    // saved details. Sections by MemoType, rows by fee; Deferred = Amount, Recognized = PostedAmount.
    public static class SummaryBuilder
    {
        public static SummaryReportDTO FromDetails(string title, string periodName, string periodSubtitle,
            List<SavedMemoDetailRow> details, Tuple<string, string>[] typeOrder)
        {
            var model = new SummaryReportDTO
            {
                ReportTitle = title,
                PeriodName = periodName,
                PeriodSubtitle = periodSubtitle,
                ShowAdjustments = false
            };

            var dates = (details ?? new List<SavedMemoDetailRow>())
                .Select(d => d.PostingDate.Date).Distinct().OrderBy(d => d).ToList();
            var idx = new Dictionary<DateTime, int>();
            for (int i = 0; i < dates.Count; i++) { idx[dates[i]] = i; model.Months.Add(dates[i].ToString("dd-MMM")); }
            int m = dates.Count;

            var grand = new SummaryRowDTO(m) { Particular = "GRAND TOTAL" };

            foreach (var t in typeOrder)
            {
                var typeRows = details.Where(d => string.Equals(d.MemoType, t.Item1, StringComparison.OrdinalIgnoreCase)).ToList();
                if (typeRows.Count == 0) { continue; }

                var section = new SummarySectionDTO { Title = t.Item2 };
                var sub = new SummaryRowDTO(m) { Particular = "Subtotal — " + t.Item2 };

                foreach (var g in typeRows.GroupBy(d => d.FeeId).OrderBy(x => x.First().Particular))
                {
                    var first = g.First();
                    var row = new SummaryRowDTO(m)
                    {
                        AcctCode = g.Select(x => x.ChartOfAccount).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                        Particular = first.Particular
                    };
                    foreach (var d in g)
                    {
                        int i;
                        if (!idx.TryGetValue(d.PostingDate.Date, out i)) { continue; }
                        row.AddDeferred(i, d.Amount);
                        row.AddRecognized(i, d.PostedAmount);
                    }
                    row.Finalize();
                    row.AccumulateInto(sub);
                    row.AccumulateInto(grand);
                    section.Rows.Add(row);
                }

                sub.Finalize();
                section.Subtotal = sub;
                model.Sections.Add(section);
            }

            grand.Finalize();
            model.GrandTotal = grand;
            return model;
        }
    }
}
