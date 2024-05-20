using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public class QneJournalEntryDTO
    {
        public string AcctName { get; set; }
        public string AcctNo { get; set; }
        public double Credit { get; set; }
        public double Debit { get; set; }
        public string GLCode { get; set; }
        public string AcaAcronym { get; set; }
        public string JournalCode { get; set; }
        public string Description { get; set; }
    }
}