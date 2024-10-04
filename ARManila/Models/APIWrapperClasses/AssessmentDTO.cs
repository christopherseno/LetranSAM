using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public class AlphaStudent
    {
        public int Id { get; set; }
        public string StudentNo { get; set; }
        public string FullName { get; set; }
        public string Level { get; set; }
        public string BADate { get; set; }
    }
    public class AssessmentDetailDTO
    {
        public int ReassessmentStudentSectionId { get; set; }
        public int StudentSectionId { get; set; }
        public bool IsSelected { get; set; }
        public int FeeId { get; set; }
        public string FeeType { get; set; }
        public int? AccountId { get; set; }
        public string AccountCode { get; set; }
        public int? SubaccountId { get; set; }
        public string SubaccountCode { get; set; }
        public string FeeDescription { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal NewAmount { get; set; }
        public decimal OriginalDiscountAmount { get; set; }
        public decimal NewDiscountAmount { get; set; }
        public decimal OriginalAmountDiff
        {
            get
            {
                return OriginalAmount - NewAmount;
            }
        }
        public decimal DiscountAmountDiff
        {
            get
            {
                return OriginalDiscountAmount - NewDiscountAmount;
            }
        }

    }
    public class AssessmentDTO
    {
        public int PeriodID { get; set; }
        public int SectionID { get; set; }
        public int StudentID { get; set; }
        public string StudentName { get; set; }
        public string StudentNo { get; set; }
        public short GradeYear { get; set; }
        public int oaf { get; set; }
        public string PaymentMode { get; set; }
        public string Program { get; set; }
        public double Units { get; set; }
        public double Hours { get; set; }
        public string Processing { get; set; }
        public DateTime AssessmentDate { get; set; }
        public decimal Tuition { get; set; }
        public decimal Misc { get; set; }
        public decimal Lab { get; set; }
        public decimal Various { get; set; }
        public decimal OtherPayment { get; set; }
        public decimal TotalAssesment { get; set; }
        public decimal Credit { get; set; }
        public decimal Discount { get; set; }
        public decimal NetAss { get; set; }
        public DateTime? ValidationDate { get; set; }
        public decimal Down { get; set; }
        public decimal Due { get; set; }
        public List<GetStudentSchedules_Result> StudentSchedule { get; set; }
        public List<PaymentSchedule> PaymentSchedules { get; set; }

    }
    public class PaymentSchedule
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }
}