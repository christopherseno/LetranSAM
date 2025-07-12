using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models.ReportsDTO
{
    public class StudentCount
    {
        public int EducLevelId { get; set; }
        public string StudentNo { get; set; }
    }
    public class ARSetupSummaryConsolidatedItem
    {
        public string Item { get; set; }
        public decimal ARFeesSetup { get; set; }
        public decimal TotalFees { get; set; }
        public decimal ARBalance { get; set; }
        public bool IsCollegeOrGs { get; set; } = false;
        public decimal AgePercent
        {
            get
            {
                return this.TotalFees == 0 ? 0 : Math.Round(this.ARBalance / this.TotalFees, 2);
            }
        }
        public int Order { get; set; }
    }

    public class ARSetupSummaryItem
    {
        public bool IsBeginningBalance { get; set; }
        public bool IsARTotalUsingBeginningBalance { get; set; }
        public string Item { get; set; }
        public decimal Amount1 { get; set; } = 0;
        public decimal Amount2 { get; set; } = 0;
        public decimal Amount3 { get; set; } = 0;
        public decimal Amount4 { get; set; } = 0;
        public decimal AmountB2 { get; set; } = 0;
        public decimal AmountB3 { get; set; } = 0;
        public decimal AmountB4 { get; set; } = 0;
        public decimal BeginningBalanceTotal { get; set; }
        public decimal Total
        {
            get
            {
                return this.IsBeginningBalance ? this.Amount1 : (
                    this.IsARTotalUsingBeginningBalance ? this.Amount1 + this.Amount2 + this.Amount3 + this.Amount4 - this.AmountB2 - this.AmountB3 - this.AmountB4
                    : this.Amount1 + this.Amount2 + this.Amount3 + this.Amount4);
            }
        }
        public decimal TotalRW { get; set; } = 0;
       
    }
    public class ARSetupSummary
    {
        public string Header { get; set; }
        public string Subheader1 { get; set; }
        public string Subheader2 { get; set; }
        public string Subheader3 { get; set; }
        public string Subheader4 { get; set; }
        public string AsOfDate { get; set; }
        public string PreparedBy { get; set; }
        public List<string> Periods { get; set; } = new List<string>();
        public ARSetupSummaryItem Tuition { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem Miscellaneous { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem Laboratory { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem Various { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem TotalFees { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem BeginningBalance { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem Collection { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem Adjustment { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem Voucher { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem Discount { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem ARBalance { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem TotalStudent { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem TotalStudentsWithBalance { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem CollectionPercent1 { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem ARBalancePercent1 { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem CollectionPercent2 { get; set; } = new ARSetupSummaryItem();
        public ARSetupSummaryItem ARBalancePercent2 { get; set; } = new ARSetupSummaryItem();
        public Dictionary<int, ARSetupSummaryConsolidatedItem> ARSetupSummaryConsolidatedItems { get; set; } = new Dictionary<int, ARSetupSummaryConsolidatedItem>();
    }
    public class FeeSummaryElem
    {
        public string Header { get; set; }
        public string Subheader { get; set; }
        public string Period { get; set; }
        public string AsOfDate { get; set; }
        public string PreparedBy { get; set; }
        public List<FeeSummaryItemElem> NoOfEnrollees { get; set; }
        public List<FeeSummaryItemElem> TuitionFee { get; set; }
        public List<FeeSummaryItemElem> MiscellaneousFee { get; set; }
        public FeeSummaryItemElem MiscellaneousTotal { get; set; }
        public List<FeeSummaryItemElem> SupplementalFee { get; set; }
        public FeeSummaryItemElem SupplementalTotal { get; set; }
        public List<FeeSummaryItemElem> OtherFee { get; set; }
        public FeeSummaryItemElem OtherTotal { get; set; }
        public List<FeeSummaryItemElem> LabFee { get; set; }
        public FeeSummaryItemElem LabTotal { get; set; }
        public FeeSummaryItemElem TotalAssessmentFee { get; set; } = new FeeSummaryItemElem();
    }
    public class FeeSummaryItemElem
    {
        public string Item { get; set; }
        public decimal Kinder { get; set; }
        public decimal Grade1 { get; set; }
        public decimal Grade2 { get; set; }
        public decimal Grade3 { get; set; }
        public decimal Grade4 { get; set; }
        public decimal Grade5 { get; set; }
        public decimal Grade6 { get; set; }
        public decimal Total { get; set; }

    }
}