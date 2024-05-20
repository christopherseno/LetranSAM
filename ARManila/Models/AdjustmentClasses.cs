using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public class AdjustmentWrapper
    {
        [Key]
        public int AdjustmentId { get; set; }
        public EnrolledStudent EnrolledStudent { get; set; }
        public List<AdjustmentSubject> AdjustmentSubjects { get; set; }
        public DateTime? AdjustmentDate { get; set; }
        [DataType(DataType.Currency)]
        public double Aircon { get; set; }
        [DataType(DataType.Currency)]
        public double Tuition { get; set; }
        [DataType(DataType.Currency)]
        public double Laboratory { get; set; }
        [DataType(DataType.Currency)]
        public double OtherFee { get; set; }
        [DataType(DataType.Currency)]
        public double Total
        {
            get
            {
                return Aircon + Tuition + Laboratory + OtherFee;
            }
        }
    }
    public class AdjustmentSubject
    {
        public int AdjustmentDetailId { get; set; }
        public string Action { get; set; }
        public string Subject { get; set; }
        public string Unit { get; set; }
    }
}