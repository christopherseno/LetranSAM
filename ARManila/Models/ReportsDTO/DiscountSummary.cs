using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models.ReportsDTO
{
    public class DiscountSummaryDTO
    {
        public string StudentNo { get; set; }
        public string StudentName { get; set; }
        public string ProgramCode { get; set; }
        public string GradeYear { get; set; }
        public string AcademicDepartment { get; set; }
        public string Category { get; set; }
        public string AccountNo { get; set; }
        public string AccountName { get; set; }
        public decimal DiscountT { get; set; } = 0;
        public decimal DiscountA { get; set; } = 0;
        public decimal DiscountL { get; set; } = 0;
        public decimal DiscountM { get; set; } = 0;
        public decimal DiscountS { get; set; } = 0;
        public decimal DiscountO { get; set; } = 0;
        public decimal DiscountV { get; set; } = 0;
        public decimal DiscountTotal { get; set; } = 0;
        public decimal DiscountPercentNonTuition { get; set; }= 0;
        public decimal DiscountPercentTuition { get; set; } = 0;
        public decimal DiscountPercentTotal { get; set; } = 0;
        public string PeriodFullName { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string EducationalLevel { get; set; }
        public string Source { get; set; }
    }
}