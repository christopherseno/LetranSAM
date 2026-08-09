using System;
using System.Collections.Generic;
using System.Linq;

namespace ARManila.Models.OtherDTO
{
    // ---- Raw row shape returned by [AR].[ArTrailMemoDetail] ----
    // Property names must match the stored procedure's result columns.
    public class ArTrailMemoDetailRow
    {
        public int PeriodID { get; set; }
        public string MemoType { get; set; }            // DebitMemo | CreditMemo | DNForm | CMForm
        public int? AcaDeptID { get; set; }
        public string AcaAcronym { get; set; }
        public string AcaDepartmentName { get; set; }
        public int? FeeID { get; set; }
        public string Particular { get; set; }
        public string ChartOfAccount { get; set; }
        public string QNECode { get; set; }
        // The SP returns Amount as SQL float, so this must be double (not decimal) to materialize.
        public double? Amount { get; set; }
    }

    // ---- Persistence read shapes (raw SQL against AR.MemoAdjustmentPosting*) ----

    // Index list: one per saved (PeriodId, PostingDate) batch.
    public class MemoPostingBatchRow
    {
        public DateTime PostingDate { get; set; }
        public DateTime DateGenerated { get; set; }
        public int GeneratedBy { get; set; }
        public bool IsFinal { get; set; }
        public int NthMonth { get; set; }
        public int NoOfMonths { get; set; }
        public decimal? TotalPosted { get; set; }
    }

    // Saved detail rows joined to their header, for rebuilding a saved batch.
    public class SavedMemoDetailRow
    {
        public bool IsFinal { get; set; }
        public int NthMonth { get; set; }
        public int NoOfMonths { get; set; }
        public DateTime DateGenerated { get; set; }
        public int GeneratedBy { get; set; }
        public string MemoType { get; set; }
        public int AcaDeptId { get; set; }
        public int? FeeId { get; set; }
        public string Particular { get; set; }
        public string ChartOfAccount { get; set; }
        public string QNECode { get; set; }
        public decimal Amount { get; set; }
        public decimal PostedAmount { get; set; }
    }

    // Prior finalized posted totals, keyed by MemoType + Dept + Fee.
    public class MemoFinalizedPostedRow
    {
        public string MemoType { get; set; }
        public int AcaDeptId { get; set; }
        public int? FeeId { get; set; }
        public decimal PostedAmount { get; set; }
    }

    // One detail line to persist.
    public class MemoPostingDetailInput
    {
        public string MemoType { get; set; }
        public int AcaDeptId { get; set; }
        public int? FeeId { get; set; }
        public string Particular { get; set; }
        public string ChartOfAccount { get; set; }
        public string QNECode { get; set; }
        public decimal Amount { get; set; }
        public decimal PostedAmount { get; set; }
    }

    // ---- Display matrix: academic departments as columns, particulars as rows, grouped by memo type ----

    public class MemoAdjustmentQueryDTO
    {
        public MemoAdjustmentQueryDTO()
        {
            Columns = new List<MemoColumnDTO>();
            Sections = new List<MemoSectionDTO>();
        }

        public int PeriodId { get; set; }
        public string PeriodName { get; set; }
        public DateTime AsOfDate { get; set; }
        public int NthMonth { get; set; }
        public int NoOfMonths { get; set; }

        // Draft/final state of the saved batch this model represents (false for a fresh preview).
        public bool HasExistingRecord { get; set; }
        public bool IsFinal { get; set; }

        public List<MemoColumnDTO> Columns { get; set; }
        public List<MemoSectionDTO> Sections { get; set; }
        public MemoRowDTO GrandTotal { get; set; }
    }

    public class MemoColumnDTO
    {
        public int AcademicDepartmentId { get; set; }
        public string Header { get; set; }           // acronym
        public string DepartmentName { get; set; }
    }

    public class MemoSectionDTO
    {
        public MemoSectionDTO()
        {
            Rows = new List<MemoRowDTO>();
        }

        public string MemoType { get; set; }         // raw type from the SP
        public string Title { get; set; }            // friendly label
        public List<MemoRowDTO> Rows { get; set; }
        public MemoRowDTO Subtotal { get; set; }
    }

    public class MemoRowDTO
    {
        public MemoRowDTO()
        {
            AmountByDept = new Dictionary<int, decimal>();
            PostedByDept = new Dictionary<int, decimal>();
        }

        public int? FeeId { get; set; }
        public string Particular { get; set; }
        public string ChartOfAccount { get; set; }
        public string QNECode { get; set; }
        public Dictionary<int, decimal> AmountByDept { get; set; }
        public Dictionary<int, decimal> PostedByDept { get; set; }

        public decimal AmountFor(int deptId)
        {
            decimal v;
            return AmountByDept.TryGetValue(deptId, out v) ? v : 0;
        }
        public decimal PostedFor(int deptId)
        {
            decimal v;
            return PostedByDept.TryGetValue(deptId, out v) ? v : 0;
        }

        public decimal TotalAmount { get { return AmountByDept.Values.Sum(); } }
        public decimal TotalPosted { get { return PostedByDept.Values.Sum(); } }

        public void Add(int deptId, decimal amount, decimal posted)
        {
            decimal a;
            AmountByDept.TryGetValue(deptId, out a);
            AmountByDept[deptId] = a + amount;
            decimal p;
            PostedByDept.TryGetValue(deptId, out p);
            PostedByDept[deptId] = p + posted;
        }
    }

    // ---- Index list view model ----

    public class MemoPostingListDTO
    {
        public MemoPostingListDTO()
        {
            Batches = new List<MemoPostingBatchDTO>();
        }
        public string PeriodName { get; set; }
        public List<MemoPostingBatchDTO> Batches { get; set; }
    }

    public class MemoPostingBatchDTO
    {
        public DateTime PostingDate { get; set; }
        public DateTime DateGenerated { get; set; }
        public string GeneratedBy { get; set; }
        public bool IsFinal { get; set; }
        public int NthMonth { get; set; }
        public int NoOfMonths { get; set; }
        public decimal TotalPosted { get; set; }
    }

    // ---- GL-account rollup view (Chart of Account, within each memo type) ----

    public class MemoGLDTO
    {
        public MemoGLDTO()
        {
            Sections = new List<MemoGLSectionDTO>();
        }
        public string PeriodName { get; set; }
        public DateTime AsOfDate { get; set; }
        public int NthMonth { get; set; }
        public int NoOfMonths { get; set; }
        public bool IsFinal { get; set; }
        public List<MemoGLSectionDTO> Sections { get; set; }

        public decimal GrandAmount { get { return Sections.Sum(s => s.TotalAmount); } }
        public decimal GrandPosted { get { return Sections.Sum(s => s.TotalPosted); } }
    }

    public class MemoGLSectionDTO
    {
        public MemoGLSectionDTO()
        {
            Rows = new List<MemoGLRowDTO>();
        }
        public string MemoType { get; set; }
        public string Title { get; set; }
        public List<MemoGLRowDTO> Rows { get; set; }

        public decimal TotalAmount { get { return Rows.Sum(r => r.Amount); } }
        public decimal TotalPosted { get { return Rows.Sum(r => r.PostedAmount); } }
    }

    public class MemoGLRowDTO
    {
        public string ChartOfAccount { get; set; }   // null => not mapped (view shows a warning)
        public string QNECode { get; set; }          // null => not mapped
        public string Particular { get; set; }       // representative fee particular(s)
        public decimal Amount { get; set; }
        public decimal PostedAmount { get; set; }
    }
}
