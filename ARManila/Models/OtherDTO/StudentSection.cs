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
}