using ARManila.Models;
using ARManila.Models.OtherDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models.OtherDTO
{
    public class ARTrailDTO
    {
        public DateTime AsOfDate { get; set; }
        public int ViewAs { get; set; }
        public List<ARTrailWrapper> ARTrailWrappers { get; set; } = new List<ARTrailWrapper>();
    }
    public partial class ARTrailWrapper
    {
        public string StudentNo { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string StudentName { get; set; }
        public decimal Assessment { get; set; }
        public decimal BegBalance { get; set; }
        public decimal DNForm { get; set; }
        public decimal CMForm { get; set; }
        public decimal DebitMemo { get; set; }
        public decimal CreditMemo { get; set; }
        public decimal Discount { get; set; }
        public decimal Voucher { get; set; }
        public decimal AdjDiscount { get; set; }
        public decimal Payment { get; set; }
        public decimal Processing { get; set; }
        public DateTime ValidationDate { get; set; }
        public string AcaAcronym { get; set; }
        public short GradeYear { get; set; }
        public string Paymode { get; set; }
        public string EducLevelName { get; set; }
        public string Period { get; set; }
        public string SchoolYearName { get; set; }
        public string BegBalanceSource { get; set; } = "";
        public decimal Balance1st { get; set; }
        public decimal Balance2nd { get; set; }
        public decimal Balance3rd { get; set; }
        public decimal EndBalance
        {
            get
            {
                return (decimal)(this.Assessment + this.BegBalance + this.DNForm + this.CMForm + this.DebitMemo - this.CreditMemo - this.Discount - this.AdjDiscount -this.Voucher - this.Processing) - this.Payment;
            }
        }
    }
}
namespace System
{
    public static class MethodExtensions
    {
        public static ARTrailWrapper ToDto(this ArTrail2024_Result value)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();            
            ARTrailWrapper wrapper = new ARTrailWrapper();
            wrapper.StudentNo = value.StudentNo;
            var student = db.Student.FirstOrDefault(m=>m.StudentNo.Equals(value.StudentNo));
            wrapper.StudentName = student != null ? student.FullName256 : "";
            wrapper.AcaAcronym = value.AcaAcronym;
            wrapper.AdjDiscount = (decimal)value.AdjDiscount;
            wrapper.Assessment = (decimal)value.Assessment;
            wrapper.BegBalance = (decimal)value.Balance;
            wrapper.CMForm = (decimal)value.CMForm;
            wrapper.CreditMemo = (decimal)value.CreditMemo;
            wrapper.DebitMemo = (decimal)value.DebitMemo;
            wrapper.Discount = (decimal)value.Discount;
            wrapper.DNForm = (decimal)value.DNForm;
            wrapper.EducLevelName = value.EducLevelName;
            wrapper.GradeYear = value.GradeYear;
            wrapper.Payment = value.Payment;
            wrapper.Paymode = value.Paymode;
            wrapper.Period = value.Period;
            wrapper.Processing = (decimal)value.Processing;
            wrapper.SchoolYearName = value.SchoolYearName;
            wrapper.ValidationDate = value.ValidationDate.Value;            
            return wrapper;
        }
        public static string ToFormattedString(this double value)
        {            
            return value.ToString("#,##0.00");
        }
        public static string ToFormattedString(this decimal value)
        {
            return value.ToString("#,##0.00");
        }
    }
}