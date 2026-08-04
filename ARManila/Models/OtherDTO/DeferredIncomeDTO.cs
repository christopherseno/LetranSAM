using System;
using System.Collections.Generic;
using System.Linq;

namespace ARManila.Models.OtherDTO
{
    public class DeferredIncomeQueryDTO
    {
        public DeferredIncomeQueryDTO()
        {
            Departments = new List<DeferredIncomeDepartmentDTO>();
            Columns = new List<DeferredIncomeColumnDTO>();
            Categories = new List<DeferredIncomeCategoryDTO>();
        }

        public int PeriodId { get; set; }
        public string PeriodName { get; set; }
        public DateTime AsOfDate { get; set; }
        public int NthMonth { get; set; }
        public int NoOfMonths { get; set; }

        // Per-department, per-fee figures used when persisting to DeferredIncome/DeferredIncomeFee.
        public List<DeferredIncomeDepartmentDTO> Departments { get; set; }

        // Display matrix (like FeesSummary college format): departments as columns, fees
        // consolidated by name as rows, grouped by fee category.
        public List<DeferredIncomeColumnDTO> Columns { get; set; }
        public List<DeferredIncomeCategoryDTO> Categories { get; set; }
        public DeferredIncomeRowDTO TotalAssessment { get; set; }

        public decimal GrandTotalAmount
        {
            get { return Departments.Sum(m => m.TotalAmount); }
        }
        public decimal GrandTotalPostedAmount
        {
            get { return Departments.Sum(m => m.TotalPostedAmount); }
        }
    }

    // ---- Persistence shape (one per academic department) ----

    public class DeferredIncomeDepartmentDTO
    {
        public DeferredIncomeDepartmentDTO()
        {
            Fees = new List<DeferredIncomeFeeDTO>();
        }

        public int AcademicDepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string Acronym { get; set; }
        public bool HasExistingRecord { get; set; }
        public bool IsFinal { get; set; }
        public List<DeferredIncomeFeeDTO> Fees { get; set; }

        public decimal TotalAmount
        {
            get { return Fees.Sum(m => m.Amount); }
        }
        public decimal TotalPostedAmount
        {
            get { return Fees.Sum(m => m.PostedAmount); }
        }
    }

    public class DeferredIncomeFeeDTO
    {
        public int FeeId { get; set; }
        public string Description { get; set; }
        public string FeeType { get; set; }
        public decimal Amount { get; set; }
        public decimal PostedAmount { get; set; }
    }

    // ---- Display matrix shape ----

    public class DeferredIncomeColumnDTO
    {
        public int AcademicDepartmentId { get; set; }
        public string Header { get; set; }
        public bool HasExistingRecord { get; set; }
        public bool IsFinal { get; set; }
    }

    public class DeferredIncomeCategoryDTO
    {
        public DeferredIncomeCategoryDTO()
        {
            Rows = new List<DeferredIncomeRowDTO>();
        }

        public string Name { get; set; }
        public bool IsSectionHeader { get; set; } // render the "Miscellaneous Fees" style banner
        public bool ShowSubtotal { get; set; }
        public List<DeferredIncomeRowDTO> Rows { get; set; }
        public DeferredIncomeRowDTO Subtotal { get; set; }
    }

    public class DeferredIncomeRowDTO
    {
        public DeferredIncomeRowDTO()
        {
            AmountByDept = new Dictionary<int, decimal>();
            PostedByDept = new Dictionary<int, decimal>();
        }

        public string Description { get; set; }
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

        public decimal TotalAmount
        {
            get { return AmountByDept.Values.Sum(); }
        }
        public decimal TotalPosted
        {
            get { return PostedByDept.Values.Sum(); }
        }

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
}
