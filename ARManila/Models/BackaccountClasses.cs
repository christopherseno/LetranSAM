using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public class BackaccountWrapper
    {
        [Key]
        public int BackaccountId { get; set; }
        public double? Amount { get; set; }
        public int? PeriodId { get; set; }
        public int? StudentId { get; set; }
        public int? FromAssessmentId { get; set; }
        public string StudentNo { get; set; }
        public string StudentName { get; set; }
        public string Period { get; set; }
        public int? AssessmentId { get; set; }
        public string AssessmentPeriod { get; set; }
        public int? AssessmentPeriodId { get; set; }
        public List<BackaccountPaymentWrapper> BackaccountPaymentWrappers { get; set; }
        public List<BackaccountDMCMWrapper> BackaccountDMCMWrappers { get; set; }
        public List<BackaccountPaymentWrapper> FloatingBackaccountPaymentWrappers { get; set; }

    }
    public class BackaccountPaymentWrapper
    {
        public int BackaccountPaymentId { get; set; }
        public int? BackaccountId { get; set; }
        public int? PaymentId { get; set; }
        public string ORNo { get; set; }
        public double? Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Remarks { get; set; }
    }
    public class BackaccountDMCMWrapper
    {
        public int BackaccountDMCMId { get; set; }
        public int BackaccountId { get; set; }
        public int? DMCMId { get; set; }        
        public double? Amount { get; set; }
        public DateTime? TransactionDate { get; set; }
        public int? DocNo { get; set; }
    }
}