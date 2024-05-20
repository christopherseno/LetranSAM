using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
namespace ARManila.Controllers
{
    public class AdjustmentController : BaseController
    {
        private readonly LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();

        public ActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Index(int id)
        {
            var adjustment = db.Adjustment.Find(id);
            if (adjustment == null) throw new Exception("Adjustment Number not found");
            EnrolledStudent enrolledStudent = SetEnrolledStudent(adjustment);
            AdjustmentWrapper wrapper = new AdjustmentWrapper
            {
                AdjustmentDate = adjustment.AdjustmentDate,
                AdjustmentId = adjustment.AdjustmentID,
                EnrolledStudent = enrolledStudent
            };
            wrapper.AdjustmentSubjects = new List<AdjustmentSubject>();
            foreach (var item in adjustment.AdjustmentDetails)
            {
                wrapper.AdjustmentSubjects.Add(new AdjustmentSubject
                {
                    Action = item.Action.HasValue ? (item.Action.Value ? "Add" : "Drop") : "0 Effect",
                    AdjustmentDetailId = item.AdjustmentDetailsID,
                    Subject = item.Schedule.Subject.SubjectCode,
                    Unit = item.Schedule.Subject.Units.ToString()
                });
                wrapper.Aircon += item.AdjTotalA ?? 0;
                wrapper.Tuition += item.AdjTotalT ?? 0;
                wrapper.Laboratory += item.AdjTotalL ?? 0;
                wrapper.OtherFee += item.OtherFee ?? 0;
            }
            return View(wrapper);
        }

        public ActionResult RecomputeAdjustment(int id)
        {
            var adjustment = db.Adjustment.Find(id);
            if (adjustment == null) throw new Exception("Adjustment Number not found");            
            var adjustmentperiodid = adjustment.Student_Section.Section.PeriodID;
            var adjustmentComputations = db.GetAdjustmentAssessmentTotal(adjustment.AdjustmentID, adjustmentperiodid).ToList();
            if (adjustmentComputations != null)
            {
                foreach (var computation in adjustmentComputations)
                {
                    var adjustmentDetail = db.AdjustmentDetails.FirstOrDefault(ad => ad.AdjustmentDetailsID == computation.AdjustmentDetailsID);

                    adjustmentDetail.TuitionFee = computation.Tuition.HasValue ? computation.Tuition.Value : 0;
                    adjustmentDetail.LaboFee = computation.Lab.HasValue ? computation.Lab.Value : 0;
                    adjustmentDetail.OtherFee = computation.Others.HasValue ? computation.Others.Value : 0;
                    adjustmentDetail.AirconFee = computation.Aircon.HasValue ? computation.Aircon.Value : 0;
                    adjustmentDetail.RefundRate = 1; //refund rate for future discussion with concerned departments
                    db.SaveChanges();
                }
                var adjustmentDetailsFees = (from adjf in db.GetAdjustmentDetailsFees(adjustment.AdjustmentID, adjustmentperiodid)
                                             select new AdjustmentDetailFees()
                                             {
                                                 AdjustmentDetailsID = adjf.AdjustmentDetailsID,
                                                 FeeID = adjf.FeeID,
                                                 Amount = adjf.Amount
                                             }).ToList();

                db.AdjustmentDetailFees.AddRange(adjustmentDetailsFees);
                db.SaveChanges();
            }

            EnrolledStudent enrolledStudent = SetEnrolledStudent(adjustment);
            AdjustmentWrapper wrapper = new AdjustmentWrapper
            {
                AdjustmentDate = adjustment.AdjustmentDate,
                AdjustmentId = adjustment.AdjustmentID,
                EnrolledStudent = enrolledStudent
            };
            wrapper.AdjustmentSubjects = new List<AdjustmentSubject>();
            foreach (var item in adjustment.AdjustmentDetails)
            {
                wrapper.AdjustmentSubjects.Add(new AdjustmentSubject
                {
                    Action = item.Action.HasValue ? (item.Action.Value ? "Add" : "Drop") : "0 Effect",
                    AdjustmentDetailId = item.AdjustmentDetailsID,
                    Subject = item.Schedule.Subject.SubjectCode,
                    Unit = item.Schedule.Subject.Units.ToString()
                });
                wrapper.Aircon = item.AdjTotalA ?? 0;
                wrapper.Tuition = item.AdjTotalT ?? 0;
                wrapper.Laboratory = item.AdjTotalL ?? 0;
                wrapper.OtherFee = item.OtherFee ?? 0;
            }
            return View("Index", wrapper);
        }
        private EnrolledStudent SetEnrolledStudent(Adjustment adjustment)
        {
            EnrolledStudent enrolledStudent = new EnrolledStudent
            {
                AssessmentDate = adjustment.Student_Section.AssessmentDate,
                Curriculum = adjustment.Student_Section.Section.Curriculum.Curriculum1,
                CurriculumId = adjustment.Student_Section.SectionID,
                EnlistmentDate = adjustment.Student_Section.EnlistmentDate,
                Level = adjustment.Student_Section.Section.GradeYear.ToString(),
                EducationalLevel = adjustment.Student_Section.Section.Period.EducationalLevel1.EducLevelName,
                EducationalLevelId = adjustment.Student_Section.Section.PeriodID,
                PaymentMode = adjustment.Student_Section.Paymode.Description,
                PaymentModeId = adjustment.Student_Section.PaymodeID,
                Period = adjustment.Student_Section.Section.Period.SchoolYear.SchoolYearName + ", " + adjustment.Student_Section.Section.Period.Period1,
                PeriodId = adjustment.Student_Section.Section.PeriodID,
                Section = adjustment.Student_Section.Section.SectionName,
                SectionId = adjustment.Student_Section.SectionID,
                StudentId = adjustment.Student_Section.StudentID,
                StudentName = adjustment.Student_Section.Student.FullName,
                StudentNumber = adjustment.Student_Section.Student.StudentNo,
                ValidationDate = adjustment.Student_Section.ValidationDate,
                StatusId = adjustment.Student_Section.StudentStatus,
                Status = adjustment.Student_Section.StudentStatus1.StudentStatusDescription
            };
            var curriculumprogam = db.ProgamCurriculum.Where(m => m.CurriculumID == adjustment.Student_Section.Section.CurriculumID).FirstOrDefault();
            enrolledStudent.Program = curriculumprogam.Progam.ProgramCode;
            enrolledStudent.ProgramId = curriculumprogam.ProgramID;
            return enrolledStudent;
        }
    }
}