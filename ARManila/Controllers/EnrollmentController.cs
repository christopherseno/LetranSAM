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
            return View();
        }
        public ActionResult DeleteValidationDate(int id)
        {
            var studentsection = db.Student_Section.Find(id);
            db.Student_Section.Remove(studentsection);
            db.SaveChanges();
            return RedirectToAction("OfficiallyEnrolled");
        }
    }
}