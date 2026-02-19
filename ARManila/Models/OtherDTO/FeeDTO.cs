using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models.OtherDTO
{
    public class FeeDTO
    {
        public Period Period { get; set; }
        public IEnumerable<Fee> Fees { get; set; }
        public IEnumerable<FeeName> FeeNames { get; set; }
        public IEnumerable<Tuition> TuitionFees { get; set; }
        public IEnumerable<Miscellaneous> MiscFees { get; set; }
        public IEnumerable<Supplemental> SupplementalFees { get; set; }
        public IEnumerable<Various> VariousFees { get; set; }
        public IEnumerable<Others> OtherFees { get; set; }
        public IEnumerable<Lab> LabFees { get; set; }
        public IEnumerable<Aircon> AirconFees { get; set; }
    }
}