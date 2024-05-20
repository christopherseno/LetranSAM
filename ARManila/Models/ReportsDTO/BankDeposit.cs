using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ARManila.Models.ReportsDTO
{
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