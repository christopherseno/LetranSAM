using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public class EnrolledStudent
    {
        [Key]
        public int AssessmentId { get; set; }
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string StudentName { get; set; }
        public string Section { get; set; }
        public int? SectionId { get; set; }
        public int? CurriculumId { get; set; }
        public string Curriculum { get; set; }
        public int? ProgramId { get; set; }
        public string Program { get; set; }
        public string EducationalLevel { get; set; }
        public int EducationalLevelId { get; set; }
        public string Level { get; set; }
        [DataType(DataType.Date)]
        public DateTime? EnlistmentDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? AssessmentDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ValidationDate { get; set; }
        public int PeriodId { get; set; }
        public string Period { get; set; }
        public int? StatusId { get; set; }
        public string Status { get; set; }
        public int? PaymentModeId { get; set; }
        public string PaymentMode { get; set; }
    }

}