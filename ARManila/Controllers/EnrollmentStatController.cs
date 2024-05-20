using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;

namespace ARManila.Controllers
{
    [Period]
    public class EnrollmentStatController : BaseController
    {
        private readonly LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Index(DateTime id)
        {
            db.Database.CommandTimeout = 180;
            var periodwraper =(PeriodWrapper) Session["PeriodId"];
            var period = db.Period.Find(periodwraper.PeriodId);
            if (period == null) throw new Exception("Invalid period id.");
            switch(period.EducLevelID)
            {
                case 1: case 2:
                    var report = db.GetEnrollmentStat2(period.PeriodID, id).ToList();
                    return View("Index",report);              
                case 3:
                    var shsreport = db.GetEnrollmentComparativeSHS(period.PeriodID, id).ToList();
                    return View("IndexSHS", shsreport);                    
                default:
                    var collreport = db.GetEnrollmentStat2(period.PeriodID, id).ToList();
                    return View("IndexColl", collreport);
            }            
        }
    }
}