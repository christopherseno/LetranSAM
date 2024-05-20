using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public class ARItem
    {
        public string Particular { get; set; }
        public double Debit { get; set; }
        public double Credit { get; set; }
        public string DocumentNo { get; set; }
        public DateTime? DocumentDate { get; set; }
        public string Remark { get; set; }        
    }
    public class ARWrapper
    {
        public List<ARItem> ARItems { get; set; }
        public EnrolledStudent Student { get; set; }
        public double? TotalDebit { get; set; }
        public double? TotalCredit { get; set; }
        public double? TotalBalance { get; set; }
        public string ARRemark { get; set; }
        public List<ARDueDate> ARDueDates { get; set; }
    }
    public class ARDueDate
    {
        public int PaycodeId { get; set; }
        public string Payment { get; set; }
        public double Amount { get; set; }
        public string DueDate { get; set; }
    }

}