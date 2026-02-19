using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
namespace ARManila.Controllers
{
    public class HomeController : BaseController
    {
        readonly LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {
            try
            {
                if (HttpContext.Request.Cookies["PeriodId"] != null)
                {
                    var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
                    var period = db.Period.Find(periodid);
                    if (period != null)
                    {
                        ViewBag.PeriodName = period.FullName;
                        ViewBag.EducLevelName = period.EducationalLevel1 != null ? period.EducationalLevel1.EducLevelName : "";
                        ViewBag.EnrolledCount = db.Student_Section.Count(m => m.Section.PeriodID == period.PeriodID && m.ValidationDate != null);
                        ViewBag.AssessedCount = db.Student_Section.Count(m => m.Section.PeriodID == period.PeriodID && m.ValidationDate == null && m.AssessmentDate != null);
                        ViewBag.EnlistedCount = db.Student_Section.Count(m => m.Section.PeriodID == period.PeriodID && m.ValidationDate == null && m.AssessmentDate == null && m.EnlistmentDate != null);
                        ViewBag.TodayPaymentsCount = db.Payment.Count(m => m.DateReceived >= DateTime.Today && m.ORNo.StartsWith("*") && m.StudentID != null);
                        ViewBag.FloatingPaymentsCount = db.GetFloatingPayment().Count();
                        ViewBag.FloatingDMCMCount = db.GetFloatingDMCM().Count();
                    }
                }
            }
            catch { }
            return View();
        }
    }
}