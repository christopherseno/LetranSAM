using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public partial class Student_Section
    {
        public bool IsSelected { get; set; }
    }

    public class BatchDmcm
    {
        public int AccountId { get; set; }
        public int SubaccountId { get; set; }
        public DateTime PostingDate { get; set; }
        public string Particular { get; set; }
        public bool IsDebit { get; set; }
        public decimal Amount { get; set; }
        public List<Student_Section> Students { get; set; }

    }
    public class StudentForDmcm
    {
        public int StudentId { get; set; }
        public string StudentNo { get; set; }
        public string StudentName { get; set; }
        public string ValidationDate { get; set; }
        public string PaymentMode { get; set; }
        public string Section { get; set; }
        public int StudentSectionId { get; set; }
    }
}