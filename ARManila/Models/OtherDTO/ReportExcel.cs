using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace ARManila.Models.OtherDTO
{
    // Builds .xlsx workbooks (EPPlus) for the reports that are also rendered to PDF via Rotativa.
    public static class ReportExcel
    {
        private const string Money = "#,##0.00";
        private static readonly Color HeaderBlue = Color.FromArgb(29, 78, 216);   // matches the PDF accent
        private static readonly Color SectionGray = Color.FromArgb(238, 241, 244);

        static ReportExcel()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        private static void MergeSafe(ExcelWorksheet ws, int r1, int c1, int r2, int c2)
        {
            if (r2 > r1 || c2 > c1) { ws.Cells[r1, c1, r2, c2].Merge = true; }
        }

        // ---- Memo / Discount / Adjustment-Discount (shared MemoAdjustmentQueryDTO) ----
        // Amount-then-Posted grouped matrix, one row per fee, grouped into memo-type sections.
        public static byte[] MemoMatrix(MemoAdjustmentQueryDTO m, string reportTitle)
        {
            using (var pkg = new ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("Report");
                int depts = m.Columns.Count;
                int amtStart = 4;                        // after Particular, COA, QNE
                int amtTotal = amtStart + depts;
                int postStart = amtTotal + 1;
                int postTotal = postStart + depts;
                int lastCol = postTotal;

                int r = 1;
                r = WriteTitle(ws, r, reportTitle, m.PeriodName, m.AsOfDate, m.NthMonth, m.NoOfMonths, m.IsFinal, lastCol);

                int h1 = r, h2 = r + 1;
                ws.Cells[h1, 1].Value = "Particular";
                ws.Cells[h1, 2].Value = "Chart of Account";
                ws.Cells[h1, 3].Value = "QNE Code";
                MergeSafe(ws, h1, 1, h2, 1);
                MergeSafe(ws, h1, 2, h2, 2);
                MergeSafe(ws, h1, 3, h2, 3);

                ws.Cells[h1, amtStart].Value = "Amount";
                MergeSafe(ws, h1, amtStart, h1, amtTotal);
                ws.Cells[h1, postStart].Value = "Posted";
                MergeSafe(ws, h1, postStart, h1, postTotal);
                for (int i = 0; i < depts; i++)
                {
                    ws.Cells[h2, amtStart + i].Value = m.Columns[i].Header;
                    ws.Cells[h2, postStart + i].Value = m.Columns[i].Header;
                }
                ws.Cells[h2, amtTotal].Value = "Total";
                ws.Cells[h2, postTotal].Value = "Total";
                StyleHeader(ws.Cells[h1, 1, h2, lastCol]);

                r = h2 + 1;
                foreach (var sec in m.Sections)
                {
                    ws.Cells[r, 1].Value = sec.Title;
                    MergeSafe(ws, r, 1, r, lastCol);
                    StyleSection(ws.Cells[r, 1, r, lastCol]);
                    r++;

                    foreach (var row in sec.Rows)
                    {
                        ws.Cells[r, 1].Value = row.Particular;
                        ws.Cells[r, 2].Value = row.ChartOfAccount;
                        ws.Cells[r, 3].Value = row.QNECode;
                        FillMemoAmounts(ws, r, row, m.Columns, amtStart, postStart, amtTotal, postTotal);
                        r++;
                    }
                    if (sec.Subtotal != null)
                    {
                        ws.Cells[r, 1].Value = "Subtotal — " + sec.Title;
                        FillMemoAmounts(ws, r, sec.Subtotal, m.Columns, amtStart, postStart, amtTotal, postTotal);
                        ws.Cells[r, 1, r, lastCol].Style.Font.Bold = true;
                        r++;
                    }
                }
                if (m.GrandTotal != null)
                {
                    ws.Cells[r, 1].Value = m.GrandTotal.Particular;
                    FillMemoAmounts(ws, r, m.GrandTotal, m.Columns, amtStart, postStart, amtTotal, postTotal);
                    StyleGrand(ws.Cells[r, 1, r, lastCol]);
                    r++;
                }

                if (ws.Dimension != null) { ws.Cells[ws.Dimension.Address].AutoFitColumns(); }
                return pkg.GetAsByteArray();
            }
        }

        private static void FillMemoAmounts(ExcelWorksheet ws, int r, MemoRowDTO row, List<MemoColumnDTO> cols, int amtStart, int postStart, int amtTotal, int postTotal)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                ws.Cells[r, amtStart + i].Value = row.AmountFor(cols[i].AcademicDepartmentId);
                ws.Cells[r, postStart + i].Value = row.PostedFor(cols[i].AcademicDepartmentId);
            }
            ws.Cells[r, amtTotal].Value = row.TotalAmount;
            ws.Cells[r, postTotal].Value = row.TotalPosted;
            ws.Cells[r, amtStart, r, postTotal].Style.Numberformat.Format = Money;
        }

        // ---- Deferred Income (DeferredIncomeQueryDTO) ----
        // Actual-then-Deferred grouped matrix, fees grouped into categories.
        public static byte[] DeferredMatrix(DeferredIncomeQueryDTO m)
        {
            using (var pkg = new ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("Deferred Income");
                int depts = m.Columns.Count;
                int actStart = 2;                        // after Fee
                int actTotal = actStart + depts;
                int defStart = actTotal + 1;
                int defTotal = defStart + depts;
                int lastCol = defTotal;

                int r = 1;
                r = WriteTitle(ws, r, "Deferred Income", m.PeriodName, m.AsOfDate, m.NthMonth, m.NoOfMonths,
                    m.Columns.All(c => c.IsFinal), lastCol);

                int h1 = r, h2 = r + 1;
                ws.Cells[h1, 1].Value = "Fee";
                MergeSafe(ws, h1, 1, h2, 1);
                ws.Cells[h1, actStart].Value = "Actual";
                MergeSafe(ws, h1, actStart, h1, actTotal);
                ws.Cells[h1, defStart].Value = "Deferred";
                MergeSafe(ws, h1, defStart, h1, defTotal);
                for (int i = 0; i < depts; i++)
                {
                    ws.Cells[h2, actStart + i].Value = m.Columns[i].Header;
                    ws.Cells[h2, defStart + i].Value = m.Columns[i].Header;
                }
                ws.Cells[h2, actTotal].Value = "Total";
                ws.Cells[h2, defTotal].Value = "Total";
                StyleHeader(ws.Cells[h1, 1, h2, lastCol]);

                r = h2 + 1;
                foreach (var cat in m.Categories)
                {
                    if (cat.IsSectionHeader)
                    {
                        ws.Cells[r, 1].Value = cat.Name;
                        MergeSafe(ws, r, 1, r, lastCol);
                        StyleSection(ws.Cells[r, 1, r, lastCol]);
                        r++;
                    }
                    foreach (var row in cat.Rows)
                    {
                        ws.Cells[r, 1].Value = row.Description;
                        FillDeferredAmounts(ws, r, row, m.Columns, actStart, defStart, actTotal, defTotal);
                        if (!cat.IsSectionHeader) { ws.Cells[r, 1, r, lastCol].Style.Font.Bold = true; }
                        r++;
                    }
                    if (cat.ShowSubtotal && cat.Subtotal != null)
                    {
                        ws.Cells[r, 1].Value = "Subtotal";
                        FillDeferredAmounts(ws, r, cat.Subtotal, m.Columns, actStart, defStart, actTotal, defTotal);
                        ws.Cells[r, 1, r, lastCol].Style.Font.Bold = true;
                        r++;
                    }
                }
                if (m.TotalAssessment != null)
                {
                    ws.Cells[r, 1].Value = m.TotalAssessment.Description;
                    FillDeferredAmounts(ws, r, m.TotalAssessment, m.Columns, actStart, defStart, actTotal, defTotal);
                    StyleGrand(ws.Cells[r, 1, r, lastCol]);
                    r++;
                }

                if (ws.Dimension != null) { ws.Cells[ws.Dimension.Address].AutoFitColumns(); }
                return pkg.GetAsByteArray();
            }
        }

        private static void FillDeferredAmounts(ExcelWorksheet ws, int r, DeferredIncomeRowDTO row, List<DeferredIncomeColumnDTO> cols, int actStart, int defStart, int actTotal, int defTotal)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                ws.Cells[r, actStart + i].Value = row.AmountFor(cols[i].AcademicDepartmentId);
                ws.Cells[r, defStart + i].Value = row.PostedFor(cols[i].AcademicDepartmentId);
            }
            ws.Cells[r, actTotal].Value = row.TotalAmount;
            ws.Cells[r, defTotal].Value = row.TotalPosted;
            ws.Cells[r, actStart, r, defTotal].Style.Numberformat.Format = Money;
        }

        // ---- Revenue-recognition Journal Entry (JournalEntryReportDTO) ----
        // One block per department: Account Code / Dept / Account Name / Debit / Credit.
        public static byte[] JournalEntry(JournalEntryReportDTO m)
        {
            using (var pkg = new ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("Journal Entry");
                const int lastCol = 5;

                int r = 1;
                ws.Cells[r, 1].Value = "Journal Entries - Revenue Recognition"; ws.Cells[r, 1].Style.Font.Bold = true; ws.Cells[r, 1].Style.Font.Size = 14; r++;
                if (!string.IsNullOrEmpty(m.EducLevel)) { ws.Cells[r, 1].Value = m.EducLevel; r++; }
                ws.Cells[r, 1].Value = m.PeriodName; r++;
                ws.Cells[r, 1].Value = "As of " + m.AsOfDate.ToString("MMMM dd, yyyy") + "  (Month " + m.NthMonth + " of " + m.NoOfMonths + ")  " + (m.IsFinal ? "FINAL" : "DRAFT"); r++;
                if (!string.IsNullOrEmpty(m.CodeSourceLabel)) { ws.Cells[r, 1].Value = "Account codes: " + m.CodeSourceLabel; r++; }
                r++;

                foreach (var dept in m.Departments)
                {
                    ws.Cells[r, 1].Value = dept.Acronym + " (" + dept.GLCode + ")";
                    ws.Cells[r, 1].Style.Font.Bold = true;
                    r++;

                    ws.Cells[r, 1].Value = "Account Code";
                    ws.Cells[r, 2].Value = "Dept";
                    ws.Cells[r, 3].Value = "Account Name";
                    ws.Cells[r, 4].Value = "Debit";
                    ws.Cells[r, 5].Value = "Credit";
                    StyleHeader(ws.Cells[r, 1, r, lastCol]);
                    r++;

                    foreach (var line in dept.Lines)
                    {
                        ws.Cells[r, 1].Value = line.AcctNo;
                        ws.Cells[r, 2].Value = line.GLCode;
                        ws.Cells[r, 3].Value = line.AccountName;
                        if (line.Debit != 0) { ws.Cells[r, 4].Value = line.Debit; }
                        if (line.Credit != 0) { ws.Cells[r, 5].Value = line.Credit; }
                        ws.Cells[r, 4, r, 5].Style.Numberformat.Format = Money;
                        r++;
                    }

                    ws.Cells[r, 3].Value = "Total";
                    ws.Cells[r, 4].Value = dept.TotalDebit;
                    ws.Cells[r, 5].Value = dept.TotalCredit;
                    ws.Cells[r, 3, r, 5].Style.Font.Bold = true;
                    ws.Cells[r, 4, r, 5].Style.Numberformat.Format = Money;
                    r++;

                    ws.Cells[r, 1].Value = m.Description;
                    MergeSafe(ws, r, 1, r, lastCol);
                    ws.Cells[r, 1].Style.Font.Italic = true;
                    r += 2;
                }

                if (ws.Dimension != null) { ws.Cells[ws.Dimension.Address].AutoFitColumns(); }
                return pkg.GetAsByteArray();
            }
        }

        // ---- Period-wide Summary (deferred + recognition pivot, dynamic month columns) ----
        public static byte[] Summary(SummaryReportDTO m)
        {
            using (var pkg = new ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("Summary");
                int mm = m.MonthCount;
                int cAcct = 1, cPart = 2;
                int defStart = 3;
                int recStart = defStart + mm;
                int recTotal = recStart + mm;
                int cAdj = recTotal + 1;
                int cNet = cAdj + 1;
                int lastCol = m.ShowAdjustments ? cNet : recTotal;

                int r = 1;
                ws.Cells[r, 1].Value = m.ReportTitle + " — Summary"; ws.Cells[r, 1].Style.Font.Bold = true; ws.Cells[r, 1].Style.Font.Size = 14; r++;
                ws.Cells[r, 1].Value = m.PeriodName; r++;
                if (!string.IsNullOrEmpty(m.PeriodSubtitle)) { ws.Cells[r, 1].Value = m.PeriodSubtitle; r++; }
                r++;

                int h1 = r, h2 = r + 1;
                ws.Cells[h1, cAcct].Value = "ACCT CODE"; MergeSafe(ws, h1, cAcct, h2, cAcct);
                ws.Cells[h1, cPart].Value = "Particular"; MergeSafe(ws, h1, cPart, h2, cPart);
                if (mm > 0)
                {
                    ws.Cells[h1, defStart].Value = "Summary of Tuition and Other Fees - Deferred Accounts";
                    MergeSafe(ws, h1, defStart, h1, recStart - 1);
                    ws.Cells[h1, recStart].Value = "Revenue Accounts Recognition";
                    MergeSafe(ws, h1, recStart, h1, recTotal);
                    for (int j = 0; j < mm; j++)
                    {
                        ws.Cells[h2, defStart + j].Value = m.Months[j];
                        ws.Cells[h2, recStart + j].Value = m.Months[j];
                    }
                    ws.Cells[h2, recTotal].Value = "TOTAL";
                }
                else
                {
                    ws.Cells[h1, recTotal].Value = "TOTAL"; MergeSafe(ws, h1, recTotal, h2, recTotal);
                }
                if (m.ShowAdjustments)
                {
                    ws.Cells[h1, cAdj].Value = "Adjustments Debit (Credit)"; MergeSafe(ws, h1, cAdj, h2, cAdj);
                    ws.Cells[h1, cNet].Value = "Net Adjusted Fees"; MergeSafe(ws, h1, cNet, h2, cNet);
                }
                StyleHeader(ws.Cells[h1, 1, h2, lastCol]);
                ws.Cells[h1, 1, h2, lastCol].Style.WrapText = true;

                r = h2 + 1;
                foreach (var sec in m.Sections)
                {
                    if (sec.SingleRow && sec.Rows.Count > 0)
                    {
                        var row = sec.Rows[0];
                        ws.Cells[r, cAcct].Value = sec.AcctCode ?? row.AcctCode;
                        ws.Cells[r, cPart].Value = row.Particular;
                        SummaryVals(ws, r, row, defStart, recStart, recTotal, mm, m.ShowAdjustments, cAdj, cNet, lastCol);
                        ws.Cells[r, 1, r, lastCol].Style.Font.Bold = true;
                        r++;
                        continue;
                    }

                    ws.Cells[r, cAcct].Value = sec.AcctCode;
                    ws.Cells[r, cPart].Value = sec.Title;
                    StyleSection(ws.Cells[r, 1, r, lastCol]);
                    r++;

                    foreach (var row in sec.Rows)
                    {
                        ws.Cells[r, cAcct].Value = row.AcctCode;
                        ws.Cells[r, cPart].Value = row.Particular;
                        SummaryVals(ws, r, row, defStart, recStart, recTotal, mm, m.ShowAdjustments, cAdj, cNet, lastCol);
                        r++;
                    }
                    if (sec.Subtotal != null)
                    {
                        ws.Cells[r, cPart].Value = sec.Subtotal.Particular;
                        SummaryVals(ws, r, sec.Subtotal, defStart, recStart, recTotal, mm, m.ShowAdjustments, cAdj, cNet, lastCol);
                        ws.Cells[r, 1, r, lastCol].Style.Font.Bold = true;
                        r++;
                    }
                }
                if (m.GrandTotal != null)
                {
                    ws.Cells[r, cPart].Value = m.GrandTotal.Particular;
                    SummaryVals(ws, r, m.GrandTotal, defStart, recStart, recTotal, mm, m.ShowAdjustments, cAdj, cNet, lastCol);
                    StyleGrand(ws.Cells[r, 1, r, lastCol]);
                    r++;
                }

                if (ws.Dimension != null) { ws.Cells[ws.Dimension.Address].AutoFitColumns(); }
                return pkg.GetAsByteArray();
            }
        }

        private static void SummaryVals(ExcelWorksheet ws, int r, SummaryRowDTO row, int defStart, int recStart, int recTotal, int mm, bool showAdj, int cAdj, int cNet, int lastCol)
        {
            for (int j = 0; j < mm; j++)
            {
                if (row.Deferred[j].HasValue) { ws.Cells[r, defStart + j].Value = row.Deferred[j].Value; }
                if (row.Recognized[j].HasValue) { ws.Cells[r, recStart + j].Value = row.Recognized[j].Value; }
            }
            ws.Cells[r, recTotal].Value = row.RecognizedTotal;
            if (showAdj)
            {
                ws.Cells[r, cAdj].Value = row.Adjustments;
                ws.Cells[r, cNet].Value = row.NetAdjusted;
            }
            ws.Cells[r, defStart, r, lastCol].Style.Numberformat.Format = Money;
        }

        // ---- shared styling helpers ----
        private static int WriteTitle(ExcelWorksheet ws, int r, string title, string period, DateTime asOf, int nth, int noOf, bool isFinal, int lastCol)
        {
            ws.Cells[r, 1].Value = title;
            ws.Cells[r, 1].Style.Font.Bold = true;
            ws.Cells[r, 1].Style.Font.Size = 14;
            r++;
            ws.Cells[r, 1].Value = period;
            r++;
            ws.Cells[r, 1].Value = "As of " + asOf.ToString("MMMM dd, yyyy") + "  —  Month " + nth + " of " + noOf + "  —  " + (isFinal ? "FINAL" : "DRAFT");
            r++;
            return r + 1; // blank line before the table
        }

        private static void StyleHeader(ExcelRange rng)
        {
            rng.Style.Font.Bold = true;
            rng.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            rng.Style.Fill.PatternType = ExcelFillStyle.Solid;
            rng.Style.Fill.BackgroundColor.SetColor(HeaderBlue);
            rng.Style.Font.Color.SetColor(Color.White);
        }

        private static void StyleSection(ExcelRange rng)
        {
            rng.Style.Font.Bold = true;
            rng.Style.Fill.PatternType = ExcelFillStyle.Solid;
            rng.Style.Fill.BackgroundColor.SetColor(SectionGray);
        }

        private static void StyleGrand(ExcelRange rng)
        {
            rng.Style.Font.Bold = true;
            rng.Style.Fill.PatternType = ExcelFillStyle.Solid;
            rng.Style.Fill.BackgroundColor.SetColor(HeaderBlue);
            rng.Style.Font.Color.SetColor(Color.White);
        }
    }
}
