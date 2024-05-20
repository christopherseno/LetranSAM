using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public partial class Paycode
    {
        public bool IsSelected { get; set; }
    }
    [MetadataType(typeof(PaymentMetaData))]
    public partial class Payment
    {
        public string StudentNo { get; set; }
        public string StudentEmail { get; set; }
        public string MoneyInWord()
        {
            decimal doubleNumber = 0;
            foreach(var item in this.PaymentDetails)
            {
                doubleNumber += (decimal)item.Amount.Value;
            }
            var beforeFloatingPoint = (int)Math.Floor(doubleNumber);
            var beforeFloatingPointWord = $"{NumberToWords(beforeFloatingPoint)} Pesos ";
            var afterFloatingPointWord =
                $"{SmallNumberToWord((int)((doubleNumber - beforeFloatingPoint) * 100), ((int)((doubleNumber - beforeFloatingPoint) * 100) == 0 ? "Zero" : ""))} Centavos";
            return $"{beforeFloatingPointWord} and {afterFloatingPointWord}";
        }
        private string NumberToWords(int number)
        {
            if (number == 0)
                return "zero";

            if (number < 0)
                return "minus " + NumberToWords(Math.Abs(number));

            var words = "";

            if (number / 1000000000 > 0)
            {
                words += NumberToWords(number / 1000000000) + " Billion ";
                number %= 1000000000;
            }

            if (number / 1000000 > 0)
            {
                words += NumberToWords(number / 1000000) + " Million ";
                number %= 1000000;
            }

            if (number / 1000 > 0)
            {
                words += NumberToWords(number / 1000) + " Thousand ";
                number %= 1000;
            }

            if (number / 100 > 0)
            {
                words += NumberToWords(number / 100) + " Hundred ";
                number %= 100;
            }

            words = SmallNumberToWord(number, words);

            return words;
        }

        private string SmallNumberToWord(int number, string words)
        {
            if (number <= 0) return words;
            if (words != "")
                words += " ";

            var unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            var tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            if (number < 20)
                words += unitsMap[number];
            else
            {
                words += tensMap[number / 10];
                if ((number % 10) > 0)
                    words += "-" + unitsMap[number % 10];
            }
            return words;

        }
    }
    public class PaymentMetaData
    {
        [DataType(DataType.Date)]
        public DateTime DateReceived { get; set; }
        [DataType(DataType.Date)]
        public DateTime BankDate { get; set; }

        [DisplayFormat(DataFormatString = "{0:#,##0.00}")]
        public Decimal CheckAmount { get; set; }
    }

    public class BankPaymentWrapper
    {
        public string TransactionNo { get; set; }
        public DateTime? PostingDate { get; set; }
        public DateTime? BankDate { get; set; }
        public string StudentNo { get; set; }
        public decimal? Amount { get; set; }
        public string Remark { get; set; }
        public int? PeriodId { get; set; }
        public string EmployeeNo { get; set; }
        public int? BankId { get; set; }
        public long PaymentId { get; set; }
        public List<PaymentDetailWrapper> paymentDetails { get; set; }
        public string Email { get; set; }
        public string MobileNo { get; set; }

    }
    public class PaymentDetailWrapper
    {
        public int? PaycodeId { get; set; }
        public decimal? Amount { get; set; }
    }
}