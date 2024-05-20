using ARManila.Models;
using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ARManila.Controllers
{
    [Authorize(Roles = "Finance, IT")]
    public class UtilityController : Controller
    {
        public ActionResult SetPeriod(string fromcontroller, string fromaction)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();            
            ViewBag.EducationalLevelId = new SelectList(db.EducationalLevel, "EducLevelId", "EducLevelName");
            ViewBag.SchoolYearId = new SelectList(db.SchoolYear.OrderByDescending(m => m.SchoolYearName), "SYID", "SchoolYearName");
            ViewBag.Period = db.Period.Select(m => new { PeriodId = m.PeriodID, PeriodName = m.Period1, SchoolYearId = m.SchoolYearID, EducationalLevelId = m.EducLevelID }).ToList();
            ViewBag.controller = fromcontroller;
            ViewBag.action = fromaction;
            return View();
        }
        [HttpPost]
        public ActionResult SetPeriod(int Period, string fromcontroller, string fromaction)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var period = db.Period.Find(Period);
            var periodwrapper = new PeriodWrapper();
            periodwrapper.Department = period.EducationalLevel1.EducLevelName;
            periodwrapper.PeriodId = period.PeriodID;
            periodwrapper.PeriodName = period.SchoolYear.SchoolYearName + ", " + period.Period1;
            HttpCookie cookie = new HttpCookie("PeriodId", period.PeriodID.ToString());           
            cookie.Secure = true;
            HttpContext.Response.Cookies.Add(cookie);
            //Session["PeriodId"] = periodwrapper;
            return RedirectToAction(fromaction, fromcontroller);
        }
        
    }
}