using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ARManila.Models.ReportsDTO
{
    public class BankDepositJournalEntry
    {
        [Key]
        public int Id { get; set; }
        public string Description { get; set; }
        public string DepartmentAcronym { get; set; }
        public string Bank { get; set; }
        public string AccountName { get; set; }
        public string AccountNumber { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}