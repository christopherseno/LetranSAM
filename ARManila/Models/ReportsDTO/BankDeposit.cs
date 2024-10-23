using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using ARManila.Models;
namespace ARManila.Models.ReportsDTO
{
    public class BackaccountSummaryDTO
    {
        public DateTime AsOfDate { get; set; }
        public int ViewAs { get; set; }
        public List<GetBackaccountSchoolYear_Result> GetBackaccountSchoolYear_Result { get; set; } = new List<GetBackaccountSchoolYear_Result>();
        public List<BackaccountDto> BackaccountDtos { get; set; } = new List<BackaccountDto>();
    }
    public class BackaccountDto
    {
        public int BackaccountId { get; set; }
        public int PeriodId { get; set; }
        public string StudentNo { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public decimal Amount { get; set; }
        public int SchoolYearId { get; set; }
        public string SchoolYear { get; set; }
        public string PeriodName { get; set; }
        public string CompletePeriodName { get; set; }
        public int EducationalLevelId { get; set; }
        public string EducationalLevel { get; set; }
        public string Department { get; set; }
        public int DepartmentId { get; set; }
        public decimal Balance { get; set; }
        public List<BackaccountPaymentDto> Payments { get; set; } = new List<BackaccountPaymentDto>();
        public List<BackaccountDmcmDto> Dmcms { get; set; } = new List<BackaccountDmcmDto>();
        public string Payment {
            get 
            {
                return String.Join(",", this.Payments);
            }
        }
        public string DMCM
        {
            get
            {
                return String.Join(",", this.Dmcms);
            }
        }
        public override string ToString()
        {
            return String.Join(",", this.Payments) + " " + String.Join(",", this.Dmcms);
        }
    }

    public class BackaccountPaymentDto
    {
        public int PaymentId { get; set; }
        public string OrNo { get; set; }
        public string OrDate { get; set; }
        public decimal Amount { get; set; }
        public override string ToString()
        {
            return $"{this.OrNo} ({this.OrDate})";
        }
    }
    public class BackaccountDmcmDto
    {
        public int DmcmId { get; set; }
        public string DocNo { get; set; }
        public string DocDate { get; set; }
        public decimal Amount { get; set; }
        public override string ToString()
        {
            return $"{this.DocNo} ({this.DocDate})";
        }
    }

    public class BankDeposit
    {
        [Key]
        public int Id { get; set; }
        public string Department { get; set; }
        public string StudentNumber { get; set; }
        public string StudentName { get; set; }
        public string Code { get; set; }
        public string DateReceived { get; set; }        
        public decimal Amount { get; set; }
        public string Bank { get; set; }
        public string BankDate { get; set; }
        public string Remarks { get; set; }
        public int PaymentId { get; set; }
        public decimal PaymentTotal { get; set; }
        public string Employee { get; set; }
        public string OrNo { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Description { get; set; }
        public int PaycodeId { get; set; }
        public string PaycodeType { get; set; }
    }
}