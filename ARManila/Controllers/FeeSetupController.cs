using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
using ARManila.Models.OtherDTO;
using ARManila.Models.ReportsDTO;
namespace ARManila.Controllers
{
    public class FeeSetupController : BaseController
    {
        LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {

            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            var period = db.Period.Find(periodid);
            var viewModel = new FeeDTO
            {
                Period = period,
                Fees = db.Fee.Where(m => m.PeriodID == periodid).ToList(),
                TuitionFees = db.Tuition.Where(m => m.Fee.PeriodID == periodid).ToList(),
                MiscFees = db.Miscellaneous.Where(m => m.Fee.PeriodID == periodid).ToList(),
                SupplementalFees = db.Supplemental.Where(m => m.Fee.PeriodID == periodid).ToList(),
                VariousFees = db.Various.Where(m => m.Fee.PeriodID == periodid).ToList(),
                OtherFees = db.Others.Where(m => m.Fee.PeriodID == periodid).ToList(),
                LabFees = db.Lab.Where(m => m.Fee.PeriodID == periodid).ToList(),
                AirconFees = db.Aircon.Where(m => m.Fee.PeriodID == periodid).ToList()
            };

            return View(viewModel);
        }
        public ActionResult FeeName()
        {
            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            var period = db.Period.Find(periodid);
            var feenames = db.FeeName.Where(m => m.EducLevelID == period.EducLevelID && (m.Deactivated == null || m.Deactivated.Value == false));
            return View(feenames);
        }
    }
}