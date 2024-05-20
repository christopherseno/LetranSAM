using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models.OtherDTO
{
    public class StudentWithBalancesDTO
    {
        public bool IsSelected { get; set; }
        public string StudentNo { get; set; }
        public string StudentName { get; set; }
        public string Course { get; set; }
        public string YearLevel { get; set; }
        public string Remark { get; set; }
        public string Amount { get; set; }
        public string Interest { get; set; }
        public string DueDate { get; set; }
        public string PayFor { get; set; }
        public string DateIssued { get; set; }
        public string PaymentMode { get; set; }
        public string StudentContactNo { get; set; }
        public string GuardianContactNo { get; set; }
        public int StudentID { get; set; }
        public string Email { get; set; }

    }
}