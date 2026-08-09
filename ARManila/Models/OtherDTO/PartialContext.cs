using ARManila.Models.OtherDTO;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public partial class LetranIntegratedSystemEntities  : DbContext
    {
        public List<ARTrailWrapper> GetArTrailBySchoolYear(int schoolyearid, int educlevelid, string asofdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                var result = context.Database.SqlQuery<ARTrailWrapper>(
                    "EXEC [AR].[ArTrailBySchoolYear] @schoolyearid ,@educlevelid, @asofdate",
                    new SqlParameter("@schoolyearid", schoolyearid),
                    new SqlParameter("@educlevelid", educlevelid),
                    new SqlParameter("@asofdate", asofdate)
                ).ToList();

                return result;
            }
        }
        public List<ARTrailWrapper> GetArTrailBySchoolYearWithDept(int schoolyearid, int educlevelid, string asofdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                var result = context.Database.SqlQuery<ARTrailWrapper>(
                    "EXEC [AR].[ArTrailBySchoolYear] @schoolyearid ,@educlevelid, @asofdate",
                    new SqlParameter("@schoolyearid", schoolyearid),
                    new SqlParameter("@educlevelid", educlevelid),
                    new SqlParameter("@asofdate", asofdate)
                ).ToList();

                return result;
            }
        }

        // Debit/Credit Memo and Adjustments (to subject loads) detail, grouped by memo type,
        // academic department and fee. Backed by [AR].[ArTrailMemoDetail] @periodid, @asofdate.
        public List<ArTrailMemoDetailRow> GetArTrailMemoDetail(int periodid, string asofdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                context.Database.CommandTimeout = 300;
                var result = context.Database.SqlQuery<ArTrailMemoDetailRow>(
                    "EXEC [AR].[ArTrailMemoDetail] @periodid, @asofdate",
                    new SqlParameter("@periodid", periodid),
                    new SqlParameter("@asofdate", asofdate)
                ).ToList();

                return result;
            }
        }

        // ---- Memo/Adjustment posting persistence (AR.MemoAdjustmentPosting[Detail]) ----
        // Raw SQL so the EDMX does not need new entities. Run Database/MemoAdjustmentPosting.Tables.sql once.

        // Saved batches (one per posting date) for the current period, newest first.
        public List<MemoPostingBatchRow> GetMemoPostingBatches(int periodid)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<MemoPostingBatchRow>(
                    @"SELECT h.PostingDate, h.DateGenerated, h.GeneratedBy, h.IsFinal, h.NthMonth, h.NoOfMonths,
                             (SELECT SUM(d.PostedAmount) FROM AR.MemoAdjustmentPostingDetail d WHERE d.PostingId = h.Id) AS TotalPosted
                      FROM AR.MemoAdjustmentPosting h
                      WHERE h.PeriodId = @p
                      ORDER BY h.PostingDate DESC",
                    new SqlParameter("@p", periodid)
                ).ToList();
            }
        }

        // All saved detail rows (joined to their header) for one posting date.
        public List<SavedMemoDetailRow> GetSavedMemoDetails(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<SavedMemoDetailRow>(
                    @"SELECT h.IsFinal, h.NthMonth, h.NoOfMonths, h.DateGenerated, h.GeneratedBy,
                             d.MemoType, d.AcaDeptId, d.FeeId, d.Particular, d.ChartOfAccount, d.QNECode,
                             d.Amount, d.PostedAmount
                      FROM AR.MemoAdjustmentPosting h
                      JOIN AR.MemoAdjustmentPostingDetail d ON d.PostingId = h.Id
                      WHERE h.PeriodId = @p AND h.PostingDate = @d",
                    new SqlParameter("@p", periodid),
                    new SqlParameter("@d", postingdate.Date)
                ).ToList();
            }
        }

        // Cumulative posted amounts already recognized in finalized batches for the period,
        // keyed (by the caller) on MemoType + Dept + Fee.
        public List<MemoFinalizedPostedRow> GetMemoFinalizedPosted(int periodid)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<MemoFinalizedPostedRow>(
                    @"SELECT d.MemoType, d.AcaDeptId, d.FeeId, SUM(d.PostedAmount) AS PostedAmount
                      FROM AR.MemoAdjustmentPosting h
                      JOIN AR.MemoAdjustmentPostingDetail d ON d.PostingId = h.Id
                      WHERE h.PeriodId = @p AND h.IsFinal = 1
                      GROUP BY d.MemoType, d.AcaDeptId, d.FeeId",
                    new SqlParameter("@p", periodid)
                ).ToList();
            }
        }

        // True if any posting date earlier than the given one is still a draft (not finalized).
        public bool HasEarlierMemoDraft(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                int count = context.Database.SqlQuery<int>(
                    @"SELECT COUNT(*) FROM AR.MemoAdjustmentPosting
                      WHERE PeriodId = @p AND PostingDate < @d AND IsFinal = 0",
                    new SqlParameter("@p", periodid),
                    new SqlParameter("@d", postingdate.Date)
                ).First();
                return count > 0;
            }
        }

        // Inserts or replaces a draft batch. Returns 1 = saved, 0 = skipped (already final).
        public int SaveMemoPosting(int periodid, DateTime postingdate, int generatedby,
                                   int nthmonth, int noofmonths, List<MemoPostingDetailInput> details)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                int? existingId = context.Database.SqlQuery<int?>(
                    "SELECT Id FROM AR.MemoAdjustmentPosting WHERE PeriodId = @p AND PostingDate = @d",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).FirstOrDefault();

                if (existingId.HasValue)
                {
                    bool isFinal = context.Database.SqlQuery<bool>(
                        "SELECT IsFinal FROM AR.MemoAdjustmentPosting WHERE Id = @id",
                        new SqlParameter("@id", existingId.Value)).FirstOrDefault();
                    if (isFinal) { return 0; }

                    context.Database.ExecuteSqlCommand(
                        @"DELETE FROM AR.MemoAdjustmentPostingDetail WHERE PostingId = @id;
                          UPDATE AR.MemoAdjustmentPosting
                             SET DateGenerated = @dg, GeneratedBy = @gb, NthMonth = @n, NoOfMonths = @m
                           WHERE Id = @id;",
                        new SqlParameter("@id", existingId.Value),
                        new SqlParameter("@dg", DateTime.Now),
                        new SqlParameter("@gb", generatedby),
                        new SqlParameter("@n", nthmonth),
                        new SqlParameter("@m", noofmonths));
                }
                else
                {
                    existingId = context.Database.SqlQuery<int>(
                        @"INSERT INTO AR.MemoAdjustmentPosting
                              (PeriodId, PostingDate, DateGenerated, GeneratedBy, IsFinal, NthMonth, NoOfMonths)
                          VALUES (@p, @d, @dg, @gb, 0, @n, @m);
                          SELECT CAST(SCOPE_IDENTITY() AS int);",
                        new SqlParameter("@p", periodid),
                        new SqlParameter("@d", postingdate.Date),
                        new SqlParameter("@dg", DateTime.Now),
                        new SqlParameter("@gb", generatedby),
                        new SqlParameter("@n", nthmonth),
                        new SqlParameter("@m", noofmonths)).First();
                }

                foreach (var det in details)
                {
                    context.Database.ExecuteSqlCommand(
                        @"INSERT INTO AR.MemoAdjustmentPostingDetail
                              (PostingId, MemoType, AcaDeptId, FeeId, Particular, ChartOfAccount, QNECode, Amount, PostedAmount)
                          VALUES (@pid, @mt, @dept, @fee, @part, @coa, @qne, @amt, @posted);",
                        new SqlParameter("@pid", existingId.Value),
                        new SqlParameter("@mt", (object)det.MemoType ?? DBNull.Value),
                        new SqlParameter("@dept", det.AcaDeptId),
                        new SqlParameter("@fee", (object)det.FeeId ?? DBNull.Value),
                        new SqlParameter("@part", (object)det.Particular ?? DBNull.Value),
                        new SqlParameter("@coa", (object)det.ChartOfAccount ?? DBNull.Value),
                        new SqlParameter("@qne", (object)det.QNECode ?? DBNull.Value),
                        new SqlParameter("@amt", det.Amount),
                        new SqlParameter("@posted", det.PostedAmount));
                }
                return 1;
            }
        }

        // Marks all non-final rows for a posting date as final. Returns rows affected.
        public int FinalizeMemoPosting(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.ExecuteSqlCommand(
                    @"UPDATE AR.MemoAdjustmentPosting SET IsFinal = 1
                      WHERE PeriodId = @p AND PostingDate = @d AND IsFinal = 0",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date));
            }
        }

        // Deletes a posting-date batch and its details. Returns 1 = deleted, 0 = nothing,
        // -1 = blocked (already final).
        public int DeleteMemoPosting(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                int total = context.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM AR.MemoAdjustmentPosting WHERE PeriodId = @p AND PostingDate = @d",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).First();
                if (total == 0) { return 0; }

                int finals = context.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM AR.MemoAdjustmentPosting WHERE PeriodId = @p AND PostingDate = @d AND IsFinal = 1",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).First();
                if (finals > 0) { return -1; }

                context.Database.ExecuteSqlCommand(
                    @"DELETE d FROM AR.MemoAdjustmentPostingDetail d
                        JOIN AR.MemoAdjustmentPosting h ON d.PostingId = h.Id
                       WHERE h.PeriodId = @p AND h.PostingDate = @d;
                      DELETE FROM AR.MemoAdjustmentPosting WHERE PeriodId = @p AND PostingDate = @d;",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date));
                return 1;
            }
        }

        // ---- Discount detail (SP) + posting persistence (AR.DiscountPosting[Detail]) ----
        // Same shapes as the memo/adjustment feature; run Database/DiscountPosting.Tables.sql once.

        // Discount detail grouped by academic department and fee.
        // Backed by [AR].[ArTrailDiscountDetail2024] @periodid, @asofdate.
        public List<ArTrailMemoDetailRow> GetArTrailDiscountDetail(int periodid, string asofdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                context.Database.CommandTimeout = 300;
                return context.Database.SqlQuery<ArTrailMemoDetailRow>(
                    "EXEC [AR].[ArTrailDiscountDetail2024] @periodid, @asofdate",
                    new SqlParameter("@periodid", periodid),
                    new SqlParameter("@asofdate", asofdate)
                ).ToList();
            }
        }

        public List<MemoPostingBatchRow> GetDiscountPostingBatches(int periodid)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<MemoPostingBatchRow>(
                    @"SELECT h.PostingDate, h.DateGenerated, h.GeneratedBy, h.IsFinal, h.NthMonth, h.NoOfMonths,
                             (SELECT SUM(d.PostedAmount) FROM AR.DiscountPostingDetail d WHERE d.PostingId = h.Id) AS TotalPosted
                      FROM AR.DiscountPosting h
                      WHERE h.PeriodId = @p
                      ORDER BY h.PostingDate DESC",
                    new SqlParameter("@p", periodid)
                ).ToList();
            }
        }

        public List<SavedMemoDetailRow> GetSavedDiscountDetails(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<SavedMemoDetailRow>(
                    @"SELECT h.IsFinal, h.NthMonth, h.NoOfMonths, h.DateGenerated, h.GeneratedBy,
                             d.DiscType AS MemoType, d.AcaDeptId, d.FeeId, d.Particular, d.ChartOfAccount, d.QNECode,
                             d.Amount, d.PostedAmount
                      FROM AR.DiscountPosting h
                      JOIN AR.DiscountPostingDetail d ON d.PostingId = h.Id
                      WHERE h.PeriodId = @p AND h.PostingDate = @d",
                    new SqlParameter("@p", periodid),
                    new SqlParameter("@d", postingdate.Date)
                ).ToList();
            }
        }

        public List<MemoFinalizedPostedRow> GetDiscountFinalizedPosted(int periodid)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<MemoFinalizedPostedRow>(
                    @"SELECT d.DiscType AS MemoType, d.AcaDeptId, d.FeeId, SUM(d.PostedAmount) AS PostedAmount
                      FROM AR.DiscountPosting h
                      JOIN AR.DiscountPostingDetail d ON d.PostingId = h.Id
                      WHERE h.PeriodId = @p AND h.IsFinal = 1
                      GROUP BY d.DiscType, d.AcaDeptId, d.FeeId",
                    new SqlParameter("@p", periodid)
                ).ToList();
            }
        }

        public bool HasEarlierDiscountDraft(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                int count = context.Database.SqlQuery<int>(
                    @"SELECT COUNT(*) FROM AR.DiscountPosting
                      WHERE PeriodId = @p AND PostingDate < @d AND IsFinal = 0",
                    new SqlParameter("@p", periodid),
                    new SqlParameter("@d", postingdate.Date)
                ).First();
                return count > 0;
            }
        }

        // Inserts or replaces a draft batch. Returns 1 = saved, 0 = skipped (already final).
        public int SaveDiscountPosting(int periodid, DateTime postingdate, int generatedby,
                                       int nthmonth, int noofmonths, List<MemoPostingDetailInput> details)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                int? existingId = context.Database.SqlQuery<int?>(
                    "SELECT Id FROM AR.DiscountPosting WHERE PeriodId = @p AND PostingDate = @d",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).FirstOrDefault();

                if (existingId.HasValue)
                {
                    bool isFinal = context.Database.SqlQuery<bool>(
                        "SELECT IsFinal FROM AR.DiscountPosting WHERE Id = @id",
                        new SqlParameter("@id", existingId.Value)).FirstOrDefault();
                    if (isFinal) { return 0; }

                    context.Database.ExecuteSqlCommand(
                        @"DELETE FROM AR.DiscountPostingDetail WHERE PostingId = @id;
                          UPDATE AR.DiscountPosting
                             SET DateGenerated = @dg, GeneratedBy = @gb, NthMonth = @n, NoOfMonths = @m
                           WHERE Id = @id;",
                        new SqlParameter("@id", existingId.Value),
                        new SqlParameter("@dg", DateTime.Now),
                        new SqlParameter("@gb", generatedby),
                        new SqlParameter("@n", nthmonth),
                        new SqlParameter("@m", noofmonths));
                }
                else
                {
                    existingId = context.Database.SqlQuery<int>(
                        @"INSERT INTO AR.DiscountPosting
                              (PeriodId, PostingDate, DateGenerated, GeneratedBy, IsFinal, NthMonth, NoOfMonths)
                          VALUES (@p, @d, @dg, @gb, 0, @n, @m);
                          SELECT CAST(SCOPE_IDENTITY() AS int);",
                        new SqlParameter("@p", periodid),
                        new SqlParameter("@d", postingdate.Date),
                        new SqlParameter("@dg", DateTime.Now),
                        new SqlParameter("@gb", generatedby),
                        new SqlParameter("@n", nthmonth),
                        new SqlParameter("@m", noofmonths)).First();
                }

                foreach (var det in details)
                {
                    context.Database.ExecuteSqlCommand(
                        @"INSERT INTO AR.DiscountPostingDetail
                              (PostingId, DiscType, AcaDeptId, FeeId, Particular, ChartOfAccount, QNECode, Amount, PostedAmount)
                          VALUES (@pid, @dt, @dept, @fee, @part, @coa, @qne, @amt, @posted);",
                        new SqlParameter("@pid", existingId.Value),
                        new SqlParameter("@dt", (object)det.MemoType ?? "Discount"),
                        new SqlParameter("@dept", det.AcaDeptId),
                        new SqlParameter("@fee", (object)det.FeeId ?? DBNull.Value),
                        new SqlParameter("@part", (object)det.Particular ?? DBNull.Value),
                        new SqlParameter("@coa", (object)det.ChartOfAccount ?? DBNull.Value),
                        new SqlParameter("@qne", (object)det.QNECode ?? DBNull.Value),
                        new SqlParameter("@amt", det.Amount),
                        new SqlParameter("@posted", det.PostedAmount));
                }
                return 1;
            }
        }

        public int FinalizeDiscountPosting(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.ExecuteSqlCommand(
                    @"UPDATE AR.DiscountPosting SET IsFinal = 1
                      WHERE PeriodId = @p AND PostingDate = @d AND IsFinal = 0",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date));
            }
        }

        public int DeleteDiscountPosting(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                int total = context.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM AR.DiscountPosting WHERE PeriodId = @p AND PostingDate = @d",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).First();
                if (total == 0) { return 0; }

                int finals = context.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM AR.DiscountPosting WHERE PeriodId = @p AND PostingDate = @d AND IsFinal = 1",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).First();
                if (finals > 0) { return -1; }

                context.Database.ExecuteSqlCommand(
                    @"DELETE d FROM AR.DiscountPostingDetail d
                        JOIN AR.DiscountPosting h ON d.PostingId = h.Id
                       WHERE h.PeriodId = @p AND h.PostingDate = @d;
                      DELETE FROM AR.DiscountPosting WHERE PeriodId = @p AND PostingDate = @d;",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date));
                return 1;
            }
        }

        // ---- Adjustment-Discount detail (SP) + posting persistence (AR.AdjDiscountPosting[Detail]) ----
        // Same shapes as the discount feature; run Database/AdjDiscountPosting.Tables.sql once.

        // Adjustment-discount detail grouped by academic department and fee.
        // Backed by [AR].[ArTrailAdjDiscountDetail2024] @periodid, @asofdate.
        public List<ArTrailMemoDetailRow> GetArTrailAdjDiscountDetail(int periodid, string asofdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                context.Database.CommandTimeout = 300;
                return context.Database.SqlQuery<ArTrailMemoDetailRow>(
                    "EXEC [AR].[ArTrailAdjDiscountDetail2024] @periodid, @asofdate",
                    new SqlParameter("@periodid", periodid),
                    new SqlParameter("@asofdate", asofdate)
                ).ToList();
            }
        }

        public List<MemoPostingBatchRow> GetAdjDiscountPostingBatches(int periodid)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<MemoPostingBatchRow>(
                    @"SELECT h.PostingDate, h.DateGenerated, h.GeneratedBy, h.IsFinal, h.NthMonth, h.NoOfMonths,
                             (SELECT SUM(d.PostedAmount) FROM AR.AdjDiscountPostingDetail d WHERE d.PostingId = h.Id) AS TotalPosted
                      FROM AR.AdjDiscountPosting h
                      WHERE h.PeriodId = @p
                      ORDER BY h.PostingDate DESC",
                    new SqlParameter("@p", periodid)
                ).ToList();
            }
        }

        public List<SavedMemoDetailRow> GetSavedAdjDiscountDetails(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<SavedMemoDetailRow>(
                    @"SELECT h.IsFinal, h.NthMonth, h.NoOfMonths, h.DateGenerated, h.GeneratedBy,
                             d.DiscType AS MemoType, d.AcaDeptId, d.FeeId, d.Particular, d.ChartOfAccount, d.QNECode,
                             d.Amount, d.PostedAmount
                      FROM AR.AdjDiscountPosting h
                      JOIN AR.AdjDiscountPostingDetail d ON d.PostingId = h.Id
                      WHERE h.PeriodId = @p AND h.PostingDate = @d",
                    new SqlParameter("@p", periodid),
                    new SqlParameter("@d", postingdate.Date)
                ).ToList();
            }
        }

        public List<MemoFinalizedPostedRow> GetAdjDiscountFinalizedPosted(int periodid)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<MemoFinalizedPostedRow>(
                    @"SELECT d.DiscType AS MemoType, d.AcaDeptId, d.FeeId, SUM(d.PostedAmount) AS PostedAmount
                      FROM AR.AdjDiscountPosting h
                      JOIN AR.AdjDiscountPostingDetail d ON d.PostingId = h.Id
                      WHERE h.PeriodId = @p AND h.IsFinal = 1
                      GROUP BY d.DiscType, d.AcaDeptId, d.FeeId",
                    new SqlParameter("@p", periodid)
                ).ToList();
            }
        }

        public bool HasEarlierAdjDiscountDraft(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                int count = context.Database.SqlQuery<int>(
                    @"SELECT COUNT(*) FROM AR.AdjDiscountPosting
                      WHERE PeriodId = @p AND PostingDate < @d AND IsFinal = 0",
                    new SqlParameter("@p", periodid),
                    new SqlParameter("@d", postingdate.Date)
                ).First();
                return count > 0;
            }
        }

        // Inserts or replaces a draft batch. Returns 1 = saved, 0 = skipped (already final).
        public int SaveAdjDiscountPosting(int periodid, DateTime postingdate, int generatedby,
                                          int nthmonth, int noofmonths, List<MemoPostingDetailInput> details)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                int? existingId = context.Database.SqlQuery<int?>(
                    "SELECT Id FROM AR.AdjDiscountPosting WHERE PeriodId = @p AND PostingDate = @d",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).FirstOrDefault();

                if (existingId.HasValue)
                {
                    bool isFinal = context.Database.SqlQuery<bool>(
                        "SELECT IsFinal FROM AR.AdjDiscountPosting WHERE Id = @id",
                        new SqlParameter("@id", existingId.Value)).FirstOrDefault();
                    if (isFinal) { return 0; }

                    context.Database.ExecuteSqlCommand(
                        @"DELETE FROM AR.AdjDiscountPostingDetail WHERE PostingId = @id;
                          UPDATE AR.AdjDiscountPosting
                             SET DateGenerated = @dg, GeneratedBy = @gb, NthMonth = @n, NoOfMonths = @m
                           WHERE Id = @id;",
                        new SqlParameter("@id", existingId.Value),
                        new SqlParameter("@dg", DateTime.Now),
                        new SqlParameter("@gb", generatedby),
                        new SqlParameter("@n", nthmonth),
                        new SqlParameter("@m", noofmonths));
                }
                else
                {
                    existingId = context.Database.SqlQuery<int>(
                        @"INSERT INTO AR.AdjDiscountPosting
                              (PeriodId, PostingDate, DateGenerated, GeneratedBy, IsFinal, NthMonth, NoOfMonths)
                          VALUES (@p, @d, @dg, @gb, 0, @n, @m);
                          SELECT CAST(SCOPE_IDENTITY() AS int);",
                        new SqlParameter("@p", periodid),
                        new SqlParameter("@d", postingdate.Date),
                        new SqlParameter("@dg", DateTime.Now),
                        new SqlParameter("@gb", generatedby),
                        new SqlParameter("@n", nthmonth),
                        new SqlParameter("@m", noofmonths)).First();
                }

                foreach (var det in details)
                {
                    context.Database.ExecuteSqlCommand(
                        @"INSERT INTO AR.AdjDiscountPostingDetail
                              (PostingId, DiscType, AcaDeptId, FeeId, Particular, ChartOfAccount, QNECode, Amount, PostedAmount)
                          VALUES (@pid, @dt, @dept, @fee, @part, @coa, @qne, @amt, @posted);",
                        new SqlParameter("@pid", existingId.Value),
                        new SqlParameter("@dt", (object)det.MemoType ?? "AdjDiscount"),
                        new SqlParameter("@dept", det.AcaDeptId),
                        new SqlParameter("@fee", (object)det.FeeId ?? DBNull.Value),
                        new SqlParameter("@part", (object)det.Particular ?? DBNull.Value),
                        new SqlParameter("@coa", (object)det.ChartOfAccount ?? DBNull.Value),
                        new SqlParameter("@qne", (object)det.QNECode ?? DBNull.Value),
                        new SqlParameter("@amt", det.Amount),
                        new SqlParameter("@posted", det.PostedAmount));
                }
                return 1;
            }
        }

        public int FinalizeAdjDiscountPosting(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.ExecuteSqlCommand(
                    @"UPDATE AR.AdjDiscountPosting SET IsFinal = 1
                      WHERE PeriodId = @p AND PostingDate = @d AND IsFinal = 0",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date));
            }
        }

        public int DeleteAdjDiscountPosting(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                int total = context.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM AR.AdjDiscountPosting WHERE PeriodId = @p AND PostingDate = @d",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).First();
                if (total == 0) { return 0; }

                int finals = context.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM AR.AdjDiscountPosting WHERE PeriodId = @p AND PostingDate = @d AND IsFinal = 1",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).First();
                if (finals > 0) { return -1; }

                context.Database.ExecuteSqlCommand(
                    @"DELETE d FROM AR.AdjDiscountPostingDetail d
                        JOIN AR.AdjDiscountPosting h ON d.PostingId = h.Id
                       WHERE h.PeriodId = @p AND h.PostingDate = @d;
                      DELETE FROM AR.AdjDiscountPosting WHERE PeriodId = @p AND PostingDate = @d;",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date));
                return 1;
            }
        }

        // ---- Header lookups (so a saved zero batch with no detail rows is still viewable) ----

        public MemoPostingBatchRow GetMemoPostingHeader(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<MemoPostingBatchRow>(
                    @"SELECT TOP 1 h.PostingDate, h.DateGenerated, h.GeneratedBy, h.IsFinal, h.NthMonth, h.NoOfMonths,
                             CAST(NULL AS decimal(18,4)) AS TotalPosted
                      FROM AR.MemoAdjustmentPosting h WHERE h.PeriodId = @p AND h.PostingDate = @d",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).FirstOrDefault();
            }
        }

        public MemoPostingBatchRow GetDiscountPostingHeader(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<MemoPostingBatchRow>(
                    @"SELECT TOP 1 h.PostingDate, h.DateGenerated, h.GeneratedBy, h.IsFinal, h.NthMonth, h.NoOfMonths,
                             CAST(NULL AS decimal(18,4)) AS TotalPosted
                      FROM AR.DiscountPosting h WHERE h.PeriodId = @p AND h.PostingDate = @d",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).FirstOrDefault();
            }
        }

        public MemoPostingBatchRow GetAdjDiscountPostingHeader(int periodid, DateTime postingdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                return context.Database.SqlQuery<MemoPostingBatchRow>(
                    @"SELECT TOP 1 h.PostingDate, h.DateGenerated, h.GeneratedBy, h.IsFinal, h.NthMonth, h.NoOfMonths,
                             CAST(NULL AS decimal(18,4)) AS TotalPosted
                      FROM AR.AdjDiscountPosting h WHERE h.PeriodId = @p AND h.PostingDate = @d",
                    new SqlParameter("@p", periodid), new SqlParameter("@d", postingdate.Date)).FirstOrDefault();
            }
        }

    }
}