using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
namespace ARManila.Controllers
{
    public class AccountSetupController : BaseController
    {
        #region FeeName

        public ActionResult Index()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var feenames = db.FeeName.Where(m => m.EducLevelID == period.EducLevelID);
            return View(feenames);
        }
        public ActionResult CreateFeeName()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            List<SelectListItem> items = new List<SelectListItem>();
            items.Add(new SelectListItem { Text = "M", Value = "M" });
            items.Add(new SelectListItem { Text = "S", Value = "S" });
            items.Add(new SelectListItem { Text = "L", Value = "L" });
            items.Add(new SelectListItem { Text = "V", Value = "V" });
            items.Add(new SelectListItem { Text = "O", Value = "O" });
            items.Add(new SelectListItem { Text = "A", Value = "A" });
            ViewBag.feetypes = items;
            return View();
        }

        [HttpPost]
        public ActionResult CreateFeeName(FeeName model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");

            if (ModelState.IsValid)
            {
                db.FeeName.Add(new FeeName
                {
                    Amount = model.Amount,
                    FeeCategory = model.FeeCategory,
                    FeeName1 = model.FeeName1,
                    EducLevelID = period.EducLevelID
                });
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            List<SelectListItem> items = new List<SelectListItem>();
            items.Add(new SelectListItem { Text = "M", Value = "M" });
            items.Add(new SelectListItem { Text = "S", Value = "S" });
            items.Add(new SelectListItem { Text = "L", Value = "L" });
            items.Add(new SelectListItem { Text = "V", Value = "V" });
            items.Add(new SelectListItem { Text = "O", Value = "O" });
            items.Add(new SelectListItem { Text = "A", Value = "A" });
            ViewBag.feetypes = items;
            return View(model);
        }
        public ActionResult DeleteFeeName(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");

            var feename = db.FeeName.Find(id);
            db.FeeName.Remove(feename);
            db.SaveChanges();
            return RedirectToAction("Index");
        }
        public ActionResult EditFeeName(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            List<SelectListItem> items = new List<SelectListItem>();
            items.Add(new SelectListItem { Text = "M", Value = "M" });
            items.Add(new SelectListItem { Text = "S", Value = "S" });
            items.Add(new SelectListItem { Text = "L", Value = "L" });
            items.Add(new SelectListItem { Text = "V", Value = "V" });
            items.Add(new SelectListItem { Text = "O", Value = "O" });
            items.Add(new SelectListItem { Text = "A", Value = "A" });
            ViewBag.feetypes = items;
            var feename = db.FeeName.Find(id);
            return View(feename);
        }
        [HttpPost]
        public ActionResult EditFeeName(FeeName model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");

            if (ModelState.IsValid)
            {
                var feename = db.FeeName.Find(model.FeeNameID);
                feename.Amount = model.Amount;
                feename.FeeCategory = model.FeeCategory;
                feename.FeeName1 = model.FeeName1;
                feename.EducLevelID = period.EducLevelID;
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            List<SelectListItem> items = new List<SelectListItem>();
            items.Add(new SelectListItem { Text = "M", Value = "M" });
            items.Add(new SelectListItem { Text = "S", Value = "S" });
            items.Add(new SelectListItem { Text = "L", Value = "L" });
            items.Add(new SelectListItem { Text = "V", Value = "V" });
            items.Add(new SelectListItem { Text = "O", Value = "O" });
            items.Add(new SelectListItem { Text = "A", Value = "A" });
            ViewBag.feetypes = items;
            return View(model);
        }

        #endregion

        #region MiscFee
        public ActionResult MiscFeeSetup()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var miscfees = db.Miscellaneous.Where(m => m.Fee.PeriodID == periodid);
            return View(miscfees);
        }

        public ActionResult CreateMiscFeeSetup()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "M");
            return View();
        }

        [HttpPost]
        public ActionResult CreateMiscFeeSetup(Miscellaneous model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "M");
            if (ModelState.IsValid)
            {
                Fee fee = new Fee();
                fee.FeeNameID = model.FeeNameId;
                fee.FeeCategory = "M";
                fee.AcctID = model.GlAccount;
                fee.SubAcctID = model.SubAccount;                
                fee.PeriodID = periodid;
                db.Fee.Add(fee);
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Miscellaneous miscellaneous = new Miscellaneous();
                miscellaneous.Amount = model.Amount;
                miscellaneous.Description = feename.FeeName1;
                miscellaneous.EducationalLevel = period.EducLevelID;
                miscellaneous.FeeID = fee.FeeID;
                miscellaneous.NewStatus = model.NewStatus;
                miscellaneous.YearLevel = model.YearLevel;
                db.Miscellaneous.Add(miscellaneous);
                db.SaveChanges();
                return RedirectToAction("MiscFeeSetup");
            }
            return View(model);
        }

        public ActionResult EditMiscFeeSetup(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "M");
            var miscellaneous = db.Miscellaneous.Find(id);
            miscellaneous.FeeNameId = miscellaneous.Fee.FeeNameID;
            miscellaneous.GlAccount = miscellaneous.Fee.AcctID;
            miscellaneous.SubAccount = miscellaneous.Fee.SubAcctID;                 
            return View(miscellaneous);
        }

        [HttpPost]
        public ActionResult EditMiscFeeSetup(Miscellaneous model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "M");
            if (ModelState.IsValid)
            {
                Fee fee = db.Fee.Find(model.FeeID);
                fee.FeeNameID = model.FeeNameId;
                fee.AcctID = model.GlAccount == 0 ? (int?)null : model.GlAccount;
                fee.SubAcctID = model.SubAccount == 0 ? (int?)null : model.SubAccount;                
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Miscellaneous miscellaneous = db.Miscellaneous.Find(model.FeeID);
                miscellaneous.Amount = model.Amount;
                miscellaneous.Description = feename.FeeName1;
                miscellaneous.NewStatus = model.NewStatus;
                miscellaneous.YearLevel = model.YearLevel;
                db.SaveChanges();
                return RedirectToAction("MiscFeeSetup");
            }
            return View(model);
        }

        public ActionResult DeleteMiscFeeSetup(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            Miscellaneous miscellaneous = db.Miscellaneous.Find(id);
            db.Miscellaneous.Remove(miscellaneous);
            db.SaveChanges();
            Fee fee = db.Fee.Find(id);
            db.Fee.Remove(fee);
            db.SaveChanges();
            return RedirectToAction("MiscFeeSetup");
        }

        #endregion

        #region SupplementalFee
        public ActionResult SupplementalFeeSetup()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var supplementalfees = db.Supplemental.Where(m => m.Fee.PeriodID == periodid);
            return View(supplementalfees);
        }

        public ActionResult CreateSupplementalFeeSetup()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "S");
            return View();
        }

        [HttpPost]
        public ActionResult CreateSupplementalFeeSetup(Miscellaneous model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "S");
            if (ModelState.IsValid)
            {
                Fee fee = new Fee();
                fee.FeeNameID = model.FeeNameId;
                fee.FeeCategory = "M";
                fee.AcctID = model.GlAccount;
                fee.SubAcctID = model.SubAccount;                
                fee.PeriodID = periodid;
                db.Fee.Add(fee);
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Miscellaneous miscellaneous = new Miscellaneous();
                miscellaneous.Amount = model.Amount;
                miscellaneous.Description = feename.FeeName1;
                miscellaneous.EducationalLevel = period.EducLevelID;
                miscellaneous.FeeID = fee.FeeID;
                miscellaneous.NewStatus = model.NewStatus;
                miscellaneous.YearLevel = model.YearLevel;
                db.Miscellaneous.Add(miscellaneous);
                db.SaveChanges();
                return RedirectToAction("MiscFeeSetup");
            }
            return View(model);
        }

        public ActionResult EditSupplementalFeeSetup(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "S");
            var supplemental = db.Supplemental.Find(id);
            supplemental.FeeNameId = supplemental.Fee.FeeNameID ?? null;
            supplemental.GlAccount = supplemental.Fee.AcctID;
            supplemental.SubAccount = supplemental.Fee.SubAcctID;            
            return View(supplemental);
        }

        [HttpPost]
        public ActionResult EditSupplementalFeeSetup(Miscellaneous model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "S");
            if (ModelState.IsValid)
            {
                Fee fee = db.Fee.Find(model.FeeID);
                fee.FeeNameID = model.FeeNameId;
                fee.AcctID = model.GlAccount == 0 ? (int?)null : model.GlAccount;
                fee.SubAcctID = model.SubAccount == 0 ? (int?)null : model.SubAccount;                
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Miscellaneous miscellaneous = db.Miscellaneous.Find(model.FeeID);
                miscellaneous.Amount = model.Amount;
                miscellaneous.Description = feename.FeeName1;
                miscellaneous.NewStatus = model.NewStatus;
                miscellaneous.YearLevel = model.YearLevel;
                db.SaveChanges();
                return RedirectToAction("MiscFeeSetup");
            }
            return View(model);
        }

        public ActionResult DeleteSupplementalFeeSetup(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            Miscellaneous miscellaneous = db.Miscellaneous.Find(id);
            db.Miscellaneous.Remove(miscellaneous);
            db.SaveChanges();
            Fee fee = db.Fee.Find(id);
            db.Fee.Remove(fee);
            db.SaveChanges();
            return RedirectToAction("MiscFeeSetup");
        }

        #endregion

        #region variousfee

        public ActionResult VariousFeeSetup()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var supplementalfees = db.Supplemental.Where(m => m.Fee.PeriodID == periodid);
            return View(supplementalfees);
        }

        public ActionResult CreateVariousFeeSetup()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "S");
            return View();
        }

        [HttpPost]
        public ActionResult CreateVariousFeeSetup(Miscellaneous model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "S");
            if (ModelState.IsValid)
            {
                Fee fee = new Fee();
                fee.FeeNameID = model.FeeNameId;
                fee.FeeCategory = "M";
                fee.AcctID = model.GlAccount;
                fee.SubAcctID = model.SubAccount;                
                fee.PeriodID = periodid;
                db.Fee.Add(fee);
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Miscellaneous miscellaneous = new Miscellaneous();
                miscellaneous.Amount = model.Amount;
                miscellaneous.Description = feename.FeeName1;
                miscellaneous.EducationalLevel = period.EducLevelID;
                miscellaneous.FeeID = fee.FeeID;
                miscellaneous.NewStatus = model.NewStatus;
                miscellaneous.YearLevel = model.YearLevel;
                db.Miscellaneous.Add(miscellaneous);
                db.SaveChanges();
                return RedirectToAction("MiscFeeSetup");
            }
            return View(model);
        }

        #endregion

        private void FeeSetupViewBags(LetranIntegratedSystemEntities db, Period period, string category)
        {
            ViewBag.feename = new SelectList(db.FeeName.Where(m => m.EducLevelID == period.EducLevelID && m.FeeCategory == category).OrderBy(m => m.FeeName1), "FeeNameID", "FeeName1");
            ViewBag.feenames = db.FeeName.Where(m => m.EducLevelID == period.EducLevelID && m.FeeCategory == category).Select(m => new { FeeNameId = m.FeeNameID, Amount = m.Amount ?? 0 });
            ViewBag.coa = new SelectList(db.ChartOfAccounts.OrderBy(m => m.AcctName), "AcctID", "FullName");
            ViewBag.sl = new SelectList(db.SubChartOfAccounts.OrderBy(m => m.SubbAcctName), "SubAcctID", "FullName");
            ViewBag.qne = new SelectList(db.QNEGLAccount.OrderBy(m => m.Description), "AccountCode", "Description");
            ViewBag.gradelevel = new SelectList(db.GradeLevel.Where(m => m.EducationalLevelId == period.EducLevelID), "GradeLevelId", "GradeLevelName");
        }
    }
}