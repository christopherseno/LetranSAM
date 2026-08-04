using System;
using System.Collections.Generic;
using System.Linq;

namespace ARManila.Models.OtherDTO
{
    // ---- Index list ----

    public class DeferredIncomeListDTO
    {
        public DeferredIncomeListDTO()
        {
            Batches = new List<DeferredIncomeBatchDTO>();
        }
        public string PeriodName { get; set; }
        public List<DeferredIncomeBatchDTO> Batches { get; set; }
    }

    public class DeferredIncomeBatchDTO
    {
        public DateTime PostingDate { get; set; }
        public int DepartmentCount { get; set; }
        public int FinalCount { get; set; }
        public DateTime DateGenerated { get; set; }
        public string GeneratedBy { get; set; }
        public int NthMonth { get; set; }
        public int NoOfMonths { get; set; }
        public decimal TotalPosted { get; set; }

        public bool AllFinal { get { return DepartmentCount > 0 && FinalCount == DepartmentCount; } }
        public bool AnyFinal { get { return FinalCount > 0; } }
    }

    // ---- Crystal report binding: College (Educational Level 4, up to 4 department columns) ----
    // The .rpt binds to this class as a .NET object data source. Column headers are supplied as
    // parameter fields (ColHeader1..4) since the departments are positional.

    public class DeferredIncomeCollegeReportRow
    {
        public string Section { get; set; }       // category name (blank for tuition/total rows)
        public string Description { get; set; }
        public string RowType { get; set; }        // SectionHeader | Item | Subtotal | GrandTotal
        public decimal Amount1 { get; set; }
        public decimal Amount2 { get; set; }
        public decimal Amount3 { get; set; }
        public decimal Amount4 { get; set; }
        public decimal AmountTotal { get; set; }
        public decimal Posted1 { get; set; }
        public decimal Posted2 { get; set; }
        public decimal Posted3 { get; set; }
        public decimal Posted4 { get; set; }
        public decimal PostedTotal { get; set; }
    }

    // ---- Crystal report binding: Generic (all other levels; group by Department in the .rpt) ----

    public class DeferredIncomeReportRow
    {
        public string Department { get; set; }
        public string Section { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public decimal PostedAmount { get; set; }
    }

    // ---- GL account view of a saved posting date ----

    public class DeferredIncomeGLDTO
    {
        public DeferredIncomeGLDTO()
        {
            Departments = new List<DeferredIncomeGLDeptDTO>();
        }
        public string PeriodName { get; set; }
        public DateTime AsOfDate { get; set; }
        public int NthMonth { get; set; }
        public int NoOfMonths { get; set; }
        public bool AllFinal { get; set; }
        public List<DeferredIncomeGLDeptDTO> Departments { get; set; }
    }

    public class DeferredIncomeGLDeptDTO
    {
        public DeferredIncomeGLDeptDTO()
        {
            Fees = new List<DeferredIncomeGLRowDTO>();
        }
        public string DepartmentName { get; set; }
        public string Acronym { get; set; }
        public bool IsFinal { get; set; }
        public List<DeferredIncomeGLRowDTO> Fees { get; set; }

        public decimal TotalActual { get { return Fees.Sum(f => f.Actual); } }
        public decimal TotalDeferred { get { return Fees.Sum(f => f.Deferred); } }
    }

    public class DeferredIncomeGLRowDTO
    {
        public string FeeName { get; set; }
        public string ChartOfAccount { get; set; }   // null => not mapped (view shows a message)
        public string QNEAccountCode { get; set; }   // null => not mapped (view shows a message)
        public decimal Actual { get; set; }
        public decimal Deferred { get; set; }
    }
}
