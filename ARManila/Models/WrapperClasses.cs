using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ARManila.Models
{
    public partial class Student_Section
    {
        public decimal EndTermBalance
        {
            get
            {
                LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
                var artrail = db.ArTrailByStudent(this.Section.PeriodID, DateTime.Today, this.StudentID).FirstOrDefault();
                if(artrail!= null)
                {
                    return (decimal)(artrail.Assessment + artrail.Balance + artrail.DNForm + artrail.CMForm + artrail.DebitMemo - artrail.CreditMemo - artrail.Discount - artrail.AdjDiscount - artrail.Voucher - artrail.Processing) - artrail.Payment;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
    public partial class ArTrail2024_Result
    {
        public decimal ArBalance
        {
            get
            {
                return (decimal)(this.Assessment + this.Balance + this.DNForm + this.CMForm + this.DebitMemo - this.CreditMemo - this.Discount - this.AdjDiscount - this.Voucher - this.Processing) - this.Payment;
            }
        }
    }
    public partial class SchoolYear
    {
        public int? ChartOfAccountId { get; set; }
    }
    public partial class Miscellaneous
    {
        public int? GlAccount { get; set; }
        public int? SubAccount { get; set; }
        public string QneGlAccount { get; set; }
        [Required(ErrorMessage = "Description is required")]
        public int? FeeNameId { get; set; }
    }

    public partial class Supplemental
    {
        public int? GlAccount { get; set; }
        public int? SubAccount { get; set; }
        public string QneGlAccount { get; set; }
        [Required(ErrorMessage = "Description is required")]
        public int? FeeNameId { get; set; }
    }
    public partial class ChartOfAccounts
    {
        public string FullName
        {
            get
            {
                return this.AcctNo + " - " + this.AcctName;
            }
        }
    }
    public partial class SubChartOfAccounts
    {
        public string FullName
        {
            get
            {
                return this.SubAcctNo + " - " + this.SubbAcctName;
            }
        }
    }
    public partial class QNEGLAccount
    {
        public string FullName
        {
            get
            {
                return this.AccountCode + " - " + this.Description;
            }
        }
    }
    public partial class Period
    {
        public string FullName
        {
            get
            {
                return this.EducLevelID < 4 ? "SY " + this.SchoolYear.SchoolYearName : this.Period1 + ", SY " + this.SchoolYear.SchoolYearName;
            }
        }
    }
    public partial class Faculty
    {
        public string FacultyName { get; set; }

    }
    public partial class Section
    {
        public string SectionNameCurriculum
        {
            get
            {
                return this.SectionName + "(" + this.Curriculum.Curriculum1 + ")";
            }
        }
    }


    public partial class Student
    {
        public bool IsSelected { get; set; }
        public string FullName
        {
            get
            {
                if (this.LastName256 == null || this.LastName256.Length < 0)
                {
                    return Utility.Decrypt(StudentNo, 2) + ", " + Utility.Decrypt(StudentNo, 1);
                }
                else
                {
                    return Utility.Decrypt256(this.LastName256) + ", " + Utility.Decrypt256(this.FirstName256);
                }
            }
        }
        public string DLastName
        {
            get
            {
                return Utility.Decrypt(StudentNo, 2);
            }
        }
        public string DFirstName
        {
            get
            {
                return Utility.Decrypt(StudentNo, 1);
            }
        }
    }


    public partial class Employee
    {
        public string FullName
        {
            get
            {
                return Utility.DecryptEmployee(EmployeeNo, 2) + ", " + Utility.DecryptEmployee(EmployeeNo, 1);
            }
        }
    }

    public class StudentScheduleWrapper
    {
        public string Subject { get; set; }
        public string Room { get; set; }
        public string Time { get; set; }
        public string Day { get; set; }
        public string Faculty { get; set; }
        public string GClassroomCode { get; set; }
    }
    public class EnrollmentWrapper
    {
        [Key]
        public int StudentSectionId { get; set; }
        public int SectionId { get; set; }
        public DateTime ValidationDate { get; set; }
        public string Section { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNo { get; set; }
        public string Curriculum { get; set; }
        public short GradeLevel { get; set; }
    }
    public class PeriodWrapper
    {
        public int PeriodId { get; set; }
        public string Department { get; set; }
        public string PeriodName { get; set; }
    }
    public class StudentWrapper
    {
        public int StudentId { get; set; }
        public string StudentNo { get; set; }
        public string Fullname { get; set; }
        public string MobileNo { get; set; }
        public string Email { get; set; }
        public bool SMSSent { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string Program { get; set; }
        public DateTime Birthday { get; set; }
    }
    public class GetSummaryOfFees_ResultMetadata
    {
        [DataType(DataType.Currency)] // Example: Change data type to EmailAddress
        public decimal Total { get; set; }
    }

    [MetadataType(typeof(GetSummaryOfFees_ResultMetadata))]
    public partial class GetSummaryOfFees_Result
    {

    }
    public class GetSummaryOfFeesCollege_ResultMetadata
    {
        [DataType(DataType.Currency)] // Example: Change data type to EmailAddress
        public decimal Total { get; set; }
    }

    [MetadataType(typeof(GetSummaryOfFeesCollege_ResultMetadata))]
    public partial class GetSummaryOfFeesCollege_Result
    {

    }
}