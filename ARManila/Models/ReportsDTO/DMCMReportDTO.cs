using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models.ReportsDTO
{
    public class DMCMReportDTO
    {
        public string EducationalLevelName { get; set; }
        public string Curriculum { get; set; }
        public string PeriodName { get; set; }
        public DateTime TransactionDate { get; set; }
        public int DocumentNumber { get; set; }
        public string StudentNumber { get; set; }
        public string StudentName { get; set; }
        public string AccountName { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Remarks { get; set; }
        public char Type { get; set; }
        public string Message { get; set; }
        public decimal Amount { get; set; }
        public string PreparedBy { get; set; }
    }
}