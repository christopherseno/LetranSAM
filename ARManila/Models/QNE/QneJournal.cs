using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public class QneError
    {
        public string code { get; set; }
        public string message { get; set; }
    }
    public class QneJournalBase
    {
        public DateTime docDate { get; set; }
        public string docCode { get; set; }
        public string description { get; set; }
        public string currency { get; set; }
        public decimal currencyRate { get; set; }
        public bool isTaxInclusive { get; set; }
        public List<QneJournalDetail> details { get; set; } = new List<QneJournalDetail>();
    }
    public class QneJournal : QneJournalBase
    {
        [Required(ErrorMessage = "Start date is required")]
        public DateTime SDate { get; set; }
        [Required(ErrorMessage = "End date is required.")]
        public DateTime EDate { get; set; }
        [Required]
        public bool IsQne { get; set; }
        public List<QneJournalEntryDTO> Entries = new List<QneJournalEntryDTO>();
        [Required]
        public string Action { get; set; }
        [Required]
        public int JournalEntryTypeId { get; set; }
    }
    public class QneJournalDetail
    {
        public string AccountName { get; set; }
        public string account { get; set; }
        public string description { get; set; }
        public string referenceNo { get; set; }
        public decimal debit { get; set; }
        public decimal credit { get; set; }
        public string taxCode { get; set; }
        public string costCentre { get; set; }
        public string project { get; set; }
        public int IsDebit
        {
            get
            {
                return this.debit == 0 ? 2 : 1;
            }
        }
        [JsonIgnore]
        public string Department { get; set; }
        [JsonIgnore]
        public string AcctNo { get; set; }
        [JsonIgnore]
        public string GLCode { get; set; }
    }
    public class Dcr
    {
        public string receiptCode { get; set; }
        public DateTime receiptDate { get; set; }
        public string currency { get; set; }
        public string project { get; set; }
        public string receiveFrom { get; set; }
        public string description { get; set; }
        public string depositToAccount { get; set; }
        public List<DcrDetail> details { get; set; } = new List<DcrDetail>();
        [JsonIgnore]
        public bool CanBePosted { get; set; }
    }
    public class DcrDetail
    {
        public int pos { get; set; }
        public string account { get; set; }
        public string description { get; set; }
        public string project { get; set; }
        public decimal amount { get; set; }

    }
}