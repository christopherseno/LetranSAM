using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;

namespace ARManila.Controllers
{
    public class EnrollmentController : BaseController
    {
        LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult OfficiallyEnrolled()
        {
            var periodid = HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            var period = db.Period.Find(Convert.ToInt32(periodid));
            var enrollments = db.Student_Section.Where(m => m.Section.PeriodID == period.PeriodID && m.ValidationDate != null);
            return View(enrollments);
        }
        public ActionResult UnvalidatedAssessment()
        {
            var periodid = HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            var period = db.Period.Find(Convert.ToInt32(periodid));
            var unvalidatedenrollments = db.Student_Section.Where(m => m.Section.PeriodID == period.PeriodID && m.ValidationDate == null && m.AssessmentDate != null);
            return View(unvalidatedenrollments);            
        }
        [HttpPost]
        public ActionResult UnvalidatedAssessment(DateTime validationdate, int studentsectionid)
        {
            var studentsection = db.Student_Section.Find(studentsectionid);
            var studentno = studentsection.Student.StudentNo;
            var remarks = "Manual Validation of studentsection " + studentsection.Student_SectionID + " set to " + validationdate.ToShortDateString() + ".";
            studentsection.ValidationDate = validationdate;
            db.SaveChanges();
            db.InsertEnrollmentTransactionLog(User.Identity.Name, remarks, studentno);
            return RedirectToAction("OfficiallyEnrolled");
        }
        public ActionResult Enlisted()
        {
            var periodid = HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            var period = db.Period.Find(Convert.ToInt32(periodid));
            var enlisted = db.Student_Section.Where(m => m.Section.PeriodID == period.PeriodID && m.ValidationDate == null && m.AssessmentDate == null && m.EnlistmentDate != null);
            return View(enlisted);
        }
        public ActionResult DeleteValidationDate(int id)
        {
            var studentsection = db.Student_Section.Find(id);
            var studentno = studentsection.Student.StudentNo;
            var remarks = "Deleted validation date (" + studentsection.ValidationDate.Value.ToShortDateString() + ") from studentsectionid of" + studentsection.Student_SectionID + ".";
            studentsection.ValidationDate = null;
            db.SaveChanges();
            db.InsertEnrollmentTransactionLog(User.Identity.Name, remarks, studentno);
            return RedirectToAction("UnvalidatedAssessment");
        }
        public ActionResult TransactionLog()
        {
            var transactions = db.EnrollmentTransactionLog.OrderByDescending(m => m.TransactionDate);
            return View(transactions);
        }
    }
}