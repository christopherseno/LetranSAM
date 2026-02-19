using System;
using System.Collections.Generic;
using System.Data.Entity;
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
        private readonly LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();

        #region FeeName CRUD

        public ActionResult FeeName()
        {
            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var feenames = db.FeeName.Where(m => m.EducLevelID == period.EducLevelID).OrderBy(m => m.FeeName1).ToList();
            return View(feenames);
        }

        public ActionResult CreateFeeName()
        {
            ViewBag.feetypes = FeeTypeItems();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateFeeName(FeeName model)
        {
            if (ModelState.IsValid)
            {
                var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value);
                var period = db.Period.Find(periodid);
                if (period == null) throw new Exception("Invalid period id.");
                db.FeeName.Add(new FeeName
                {
                    Amount = model.Amount,
                    FeeCategory = model.FeeCategory,
                    FeeName1 = model.FeeName1,
                    EducLevelID = period.EducLevelID
                });
                db.SaveChanges();
                return RedirectToAction("FeeName");
            }
            ViewBag.feetypes = FeeTypeItems();
            return View(model);
        }

        public ActionResult EditFeeName(int id)
        {
            var feename = db.FeeName.Find(id);
            if (feename == null) return HttpNotFound();
            ViewBag.feetypes = FeeTypeItems();
            return View(feename);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditFeeName(FeeName model)
        {
            if (ModelState.IsValid)
            {
                var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value);
                var period = db.Period.Find(periodid);
                if (period == null) throw new Exception("Invalid period id.");
                var feename = db.FeeName.Find(model.FeeNameID);
                if (feename == null) return HttpNotFound();
                feename.FeeName1 = model.FeeName1;
                feename.Amount = model.Amount;
                feename.FeeCategory = model.FeeCategory;
                feename.EducLevelID = period.EducLevelID;
                db.SaveChanges();
                return RedirectToAction("FeeName");
            }
            ViewBag.feetypes = FeeTypeItems();
            return View(model);
        }

        [HttpPost]
        public ActionResult DeleteFeeName(int id)
        {
            var feename = db.FeeName.Find(id);
            if (feename == null) return HttpNotFound();
            try
            {
                db.FeeName.Remove(feename);
                db.SaveChanges();
                return Json(new { success = true, message = "Fee name deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting fee name: " + ex.Message });
            }
        }

        private List<SelectListItem> FeeTypeItems() => new List<SelectListItem>
        {
            new SelectListItem { Text = "Miscellaneous",  Value = "M" },
            new SelectListItem { Text = "Supplemental",  Value = "S" },
            new SelectListItem { Text = "Laboratory",    Value = "L" },
            new SelectListItem { Text = "Various",       Value = "V" },
            new SelectListItem { Text = "Others",        Value = "O" },
            new SelectListItem { Text = "Aircon",        Value = "A" },
        };

        #endregion

        #region Index and Display

        public ActionResult Index()
        {
            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            var period = db.Period.Find(periodid);

            var miscFees = db.Miscellaneous
                .Where(m => m.Fee.PeriodID == periodid)
                .Include(m => m.Fee.FeeName)
                .Include(m => m.Fee.ChartOfAccounts)
                .Include(m => m.Fee.SubChartOfAccounts)
                .ToList();

            var supplementalFees = db.Supplemental
                .Where(m => m.Fee.PeriodID == periodid)
                .Include(m => m.Fee.FeeName)
                .Include(m => m.Fee.ChartOfAccounts)
                .Include(m => m.Fee.SubChartOfAccounts)
                .ToList();

            var variousFees = db.Various
                .Where(m => m.Fee.PeriodID == periodid)
                .Include(m => m.Fee.FeeName)
                .Include(m => m.Fee.ChartOfAccounts)
                .Include(m => m.Fee.SubChartOfAccounts)
                .Include(m => m.Progam)
                .ToList();

            var otherFees = db.Others
                .Where(m => m.Fee.PeriodID == periodid)
                .Include(m => m.Fee.FeeName)
                .Include(m => m.Fee.ChartOfAccounts)
                .Include(m => m.Fee.SubChartOfAccounts)
                .Include(m => m.Subject)
                .Include(m => m.Section)
                .ToList();

            var labFees = db.Lab
                .Where(m => m.Fee.PeriodID == periodid)
                .Include(m => m.Fee.FeeName)
                .Include(m => m.Fee.ChartOfAccounts)
                .Include(m => m.Fee.SubChartOfAccounts)
                .Include(m => m.Subject)
                .ToList();

            var viewModel = new FeeDTO
            {
                Period = period,
                Fees = db.Fee.Where(m => m.PeriodID == periodid).ToList(),
                TuitionFees = db.Tuition.Where(m => m.Fee.PeriodID == periodid).ToList(),
                MiscFees = miscFees,
                SupplementalFees = supplementalFees,
                VariousFees = variousFees,
                OtherFees = otherFees,
                LabFees = labFees,
                AirconFees = db.Aircon.Where(m => m.Fee.PeriodID == periodid).ToList(),
                FeeNames = db.FeeName.Where(m => m.EducLevelID == period.EducLevelID).OrderBy(m => m.FeeName1).ToList()
            };

            return View(viewModel);
        }

        #endregion

        #region Tuition CRUD

        public ActionResult CreateTuition()
        {
            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

            ViewBag.PaymodeID = new SelectList(db.Paymode, "PaymodeID", "Description");
            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode");
            ViewBag.CurriculumID = new SelectList(db.Curriculum, "CurriculumID", "Curriculum1");
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctNo");
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctNo");
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateTuition(Tuition tuition, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

                    // Create Fee record
                    var fee = new Fee
                    {
                        PeriodID = periodid,
                        FeeCategory = "Tuition",
                        AcctID = AcctID,
                        SubAcctID = SubAcctID,
                        FeeNameID = FeeNameID
                    };

                    db.Fee.Add(fee);
                    db.SaveChanges();

                    // Assign the generated FeeID to Tuition
                    tuition.FeeID = fee.FeeID;
                    db.Tuition.Add(tuition);
                    db.SaveChanges();

                    TempData["Success"] = "Tuition fee created successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error creating tuition fee: " + ex.Message);
                }
            }

            ViewBag.PaymodeID = new SelectList(db.Paymode, "PaymodeID", "Description", tuition.PaymodeID);
            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode", tuition.ProgramID);
            ViewBag.CurriculumID = new SelectList(db.Curriculum, "CurriculumID", "Curriculum1", tuition.CurriculumID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctNo", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctNo", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(tuition);
        }

        public ActionResult EditTuition(int id)
        {
            var tuition = db.Tuition.Include(t => t.Fee).FirstOrDefault(t => t.FeeID == id);
            if (tuition == null)
                return HttpNotFound();

            ViewBag.PaymodeID = new SelectList(db.Paymode, "PaymodeID", "Description", tuition.PaymodeID);
            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode", tuition.ProgramID);
            ViewBag.CurriculumID = new SelectList(db.Curriculum, "CurriculumID", "CurriculumCode", tuition.CurriculumID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", tuition.Fee.AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", tuition.Fee.SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", tuition.Fee.FeeNameID);

            return View(tuition);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTuition(Tuition tuition, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingTuition = db.Tuition.Include(t => t.Fee).FirstOrDefault(t => t.FeeID == tuition.FeeID);
                    if (existingTuition == null)
                        return HttpNotFound();

                    // Update Tuition properties
                    existingTuition.YearLevel = tuition.YearLevel;
                    existingTuition.Amount = tuition.Amount;
                    existingTuition.PaymodeID = tuition.PaymodeID;
                    existingTuition.CurriculumID = tuition.CurriculumID;
                    existingTuition.ProgramID = tuition.ProgramID;
                    existingTuition.Downpayment = tuition.Downpayment;

                    // Update Fee properties
                    existingTuition.Fee.AcctID = AcctID;
                    existingTuition.Fee.SubAcctID = SubAcctID;
                    existingTuition.Fee.FeeNameID = FeeNameID;

                    db.Entry(existingTuition).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Tuition fee updated successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating tuition fee: " + ex.Message);
                }
            }

            ViewBag.PaymodeID = new SelectList(db.Paymode, "PaymodeID", "Description", tuition.PaymodeID);
            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode", tuition.ProgramID);
            ViewBag.CurriculumID = new SelectList(db.Curriculum, "CurriculumID", "CurriculumCode", tuition.CurriculumID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(tuition);
        }

        [HttpPost]
        public ActionResult DeleteTuition(int id)
        {
            var tuition = db.Tuition.Include(t => t.Fee).FirstOrDefault(t => t.FeeID == id);
            if (tuition == null)
                return HttpNotFound();

            try
            {
                db.Tuition.Remove(tuition);
                db.Fee.Remove(tuition.Fee);
                db.SaveChanges();

                return Json(new { success = true, message = "Tuition fee deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting tuition fee: " + ex.Message });
            }
        }

        #endregion

        #region Miscellaneous CRUD

        public ActionResult CreateMiscellaneous()
        {
            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode");
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode");
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateMiscellaneous(Miscellaneous misc, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

                    var fee = new Fee
                    {
                        PeriodID = periodid,
                        FeeCategory = "Miscellaneous",
                        AcctID = AcctID,
                        SubAcctID = SubAcctID,
                        FeeNameID = FeeNameID
                    };

                    db.Fee.Add(fee);
                    db.SaveChanges();

                    misc.FeeID = fee.FeeID;
                    db.Miscellaneous.Add(misc);
                    db.SaveChanges();

                    TempData["Success"] = "Miscellaneous fee created successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error creating miscellaneous fee: " + ex.Message);
                }
            }

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(misc);
        }

        public ActionResult EditMiscellaneous(int id)
        {
            var misc = db.Miscellaneous.Include(m => m.Fee).FirstOrDefault(m => m.FeeID == id);
            if (misc == null)
                return HttpNotFound();

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", misc.Fee.AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", misc.Fee.SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", misc.Fee.FeeNameID);

            return View(misc);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditMiscellaneous(Miscellaneous misc, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingMisc = db.Miscellaneous.Include(m => m.Fee).FirstOrDefault(m => m.FeeID == misc.FeeID);
                    if (existingMisc == null)
                        return HttpNotFound();

                    existingMisc.Description = misc.Description;
                    existingMisc.Amount = misc.Amount;
                    existingMisc.NewStatus = misc.NewStatus;
                    existingMisc.DiscountCharge = misc.DiscountCharge;
                    existingMisc.EducationalLevel = misc.EducationalLevel;
                    existingMisc.YearLevel = misc.YearLevel;
                    existingMisc.ExceptCurrID = misc.ExceptCurrID;
                    existingMisc.OnlyCurrID = misc.OnlyCurrID;

                    existingMisc.Fee.AcctID = AcctID;
                    existingMisc.Fee.SubAcctID = SubAcctID;
                    existingMisc.Fee.FeeNameID = FeeNameID;

                    db.Entry(existingMisc).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Miscellaneous fee updated successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating miscellaneous fee: " + ex.Message);
                }
            }

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(misc);
        }

        [HttpPost]
        public ActionResult DeleteMiscellaneous(int id)
        {
            var misc = db.Miscellaneous.Include(m => m.Fee).FirstOrDefault(m => m.FeeID == id);
            if (misc == null)
                return HttpNotFound();

            try
            {
                db.Miscellaneous.Remove(misc);
                db.Fee.Remove(misc.Fee);
                db.SaveChanges();

                return Json(new { success = true, message = "Miscellaneous fee deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting miscellaneous fee: " + ex.Message });
            }
        }

        #endregion

        #region Supplemental CRUD

        public ActionResult CreateSupplemental()
        {
            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode");
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode");
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateSupplemental(Supplemental supp, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

                    var fee = new Fee
                    {
                        PeriodID = periodid,
                        FeeCategory = "Supplemental",
                        AcctID = AcctID,
                        SubAcctID = SubAcctID,
                        FeeNameID = FeeNameID
                    };

                    db.Fee.Add(fee);
                    db.SaveChanges();

                    supp.FeeID = fee.FeeID;
                    db.Supplemental.Add(supp);
                    db.SaveChanges();

                    TempData["Success"] = "Supplemental fee created successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error creating supplemental fee: " + ex.Message);
                }
            }

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(supp);
        }

        public ActionResult EditSupplemental(int id)
        {
            var supp = db.Supplemental.Include(s => s.Fee).FirstOrDefault(s => s.FeeID == id);
            if (supp == null)
                return HttpNotFound();

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", supp.Fee.AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", supp.Fee.SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", supp.Fee.FeeNameID);

            return View(supp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditSupplemental(Supplemental supp, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingSupp = db.Supplemental.Include(s => s.Fee).FirstOrDefault(s => s.FeeID == supp.FeeID);
                    if (existingSupp == null)
                        return HttpNotFound();

                    existingSupp.Description = supp.Description;
                    existingSupp.Amount = supp.Amount;
                    existingSupp.NewStatus = supp.NewStatus;
                    existingSupp.DiscountCharge = supp.DiscountCharge;
                    existingSupp.EducationalLevel = supp.EducationalLevel;
                    existingSupp.YearLevel = supp.YearLevel;
                    existingSupp.ExceptCurrID = supp.ExceptCurrID;
                    existingSupp.OnlyCurrID = supp.OnlyCurrID;

                    existingSupp.Fee.AcctID = AcctID;
                    existingSupp.Fee.SubAcctID = SubAcctID;
                    existingSupp.Fee.FeeNameID = FeeNameID;

                    db.Entry(existingSupp).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Supplemental fee updated successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating supplemental fee: " + ex.Message);
                }
            }

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(supp);
        }

        [HttpPost]
        public ActionResult DeleteSupplemental(int id)
        {
            var supp = db.Supplemental.Include(s => s.Fee).FirstOrDefault(s => s.FeeID == id);
            if (supp == null)
                return HttpNotFound();

            try
            {
                db.Supplemental.Remove(supp);
                db.Fee.Remove(supp.Fee);
                db.SaveChanges();

                return Json(new { success = true, message = "Supplemental fee deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting supplemental fee: " + ex.Message });
            }
        }

        #endregion

        #region Various CRUD

        public ActionResult CreateVarious()
        {
            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode");
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode");
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode");
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateVarious(Various various, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

                    var fee = new Fee
                    {
                        PeriodID = periodid,
                        FeeCategory = "Various",
                        AcctID = AcctID,
                        SubAcctID = SubAcctID,
                        FeeNameID = FeeNameID
                    };

                    db.Fee.Add(fee);
                    db.SaveChanges();

                    various.FeeID = fee.FeeID;
                    db.Various.Add(various);
                    db.SaveChanges();

                    TempData["Success"] = "Various fee created successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error creating various fee: " + ex.Message);
                }
            }

            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode", various.ProgramID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(various);
        }

        public ActionResult EditVarious(int id)
        {
            var various = db.Various.Include(v => v.Fee).FirstOrDefault(v => v.FeeID == id);
            if (various == null)
                return HttpNotFound();

            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode", various.ProgramID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", various.Fee.AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", various.Fee.SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", various.Fee.FeeNameID);

            return View(various);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditVarious(Various various, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingVarious = db.Various.Include(v => v.Fee).FirstOrDefault(v => v.FeeID == various.FeeID);
                    if (existingVarious == null)
                        return HttpNotFound();

                    existingVarious.Description = various.Description;
                    existingVarious.Amount = various.Amount;
                    existingVarious.CurriculumID = various.CurriculumID;
                    existingVarious.YearLevel = various.YearLevel;
                    existingVarious.ProgramID = various.ProgramID;

                    existingVarious.Fee.AcctID = AcctID;
                    existingVarious.Fee.SubAcctID = SubAcctID;
                    existingVarious.Fee.FeeNameID = FeeNameID;

                    db.Entry(existingVarious).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Various fee updated successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating various fee: " + ex.Message);
                }
            }

            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode", various.ProgramID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(various);
        }

        [HttpPost]
        public ActionResult DeleteVarious(int id)
        {
            var various = db.Various.Include(v => v.Fee).FirstOrDefault(v => v.FeeID == id);
            if (various == null)
                return HttpNotFound();

            try
            {
                db.Various.Remove(various);
                db.Fee.Remove(various.Fee);
                db.SaveChanges();

                return Json(new { success = true, message = "Various fee deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting various fee: " + ex.Message });
            }
        }

        #endregion

        #region Others CRUD

        public ActionResult CreateOthers()
        {
            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode");
            ViewBag.SectionID = new SelectList(db.Section, "SectionID", "SectionName");
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode");
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode");
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateOthers(Others others, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

                    var fee = new Fee
                    {
                        PeriodID = periodid,
                        FeeCategory = "Others",
                        AcctID = AcctID,
                        SubAcctID = SubAcctID,
                        FeeNameID = FeeNameID
                    };

                    db.Fee.Add(fee);
                    db.SaveChanges();

                    others.FeeID = fee.FeeID;
                    db.Others.Add(others);
                    db.SaveChanges();

                    TempData["Success"] = "Others fee created successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error creating others fee: " + ex.Message);
                }
            }

            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", others.SubjectID);
            ViewBag.SectionID = new SelectList(db.Section, "SectionID", "SectionName", others.SectionID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(others);
        }

        public ActionResult EditOthers(int id)
        {
            var others = db.Others.Include(o => o.Fee).FirstOrDefault(o => o.FeeID == id);
            if (others == null)
                return HttpNotFound();

            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", others.SubjectID);
            ViewBag.SectionID = new SelectList(db.Section, "SectionID", "SectionName", others.SectionID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", others.Fee.AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", others.Fee.SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", others.Fee.FeeNameID);

            return View(others);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditOthers(Others others, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingOthers = db.Others.Include(o => o.Fee).FirstOrDefault(o => o.FeeID == others.FeeID);
                    if (existingOthers == null)
                        return HttpNotFound();

                    existingOthers.Description = others.Description;
                    existingOthers.Amount = others.Amount;
                    existingOthers.SubjectID = others.SubjectID;
                    existingOthers.SectionID = others.SectionID;

                    existingOthers.Fee.AcctID = AcctID;
                    existingOthers.Fee.SubAcctID = SubAcctID;
                    existingOthers.Fee.FeeNameID = FeeNameID;

                    db.Entry(existingOthers).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Others fee updated successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating others fee: " + ex.Message);
                }
            }

            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", others.SubjectID);
            ViewBag.SectionID = new SelectList(db.Section, "SectionID", "SectionName", others.SectionID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(others);
        }

        [HttpPost]
        public ActionResult DeleteOthers(int id)
        {
            var others = db.Others.Include(o => o.Fee).FirstOrDefault(o => o.FeeID == id);
            if (others == null)
                return HttpNotFound();

            try
            {
                db.Others.Remove(others);
                db.Fee.Remove(others.Fee);
                db.SaveChanges();

                return Json(new { success = true, message = "Others fee deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting others fee: " + ex.Message });
            }
        }

        #endregion

        #region Lab CRUD

        public ActionResult CreateLab()
        {
            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode");
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode");
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode");
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateLab(Lab lab, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

                    var fee = new Fee
                    {
                        PeriodID = periodid,
                        FeeCategory = "Lab",
                        AcctID = AcctID,
                        SubAcctID = SubAcctID,
                        FeeNameID = FeeNameID
                    };

                    db.Fee.Add(fee);
                    db.SaveChanges();

                    lab.FeeID = fee.FeeID;
                    db.Lab.Add(lab);
                    db.SaveChanges();

                    TempData["Success"] = "Lab fee created successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error creating lab fee: " + ex.Message);
                }
            }

            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", lab.SubjectID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(lab);
        }

        public ActionResult EditLab(int id)
        {
            var lab = db.Lab.Include(l => l.Fee).FirstOrDefault(l => l.FeeID == id);
            if (lab == null)
                return HttpNotFound();

            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", lab.SubjectID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", lab.Fee.AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", lab.Fee.SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", lab.Fee.FeeNameID);

            return View(lab);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditLab(Lab lab, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingLab = db.Lab.Include(l => l.Fee).FirstOrDefault(l => l.FeeID == lab.FeeID);
                    if (existingLab == null)
                        return HttpNotFound();

                    existingLab.Description = lab.Description;
                    existingLab.Amount = lab.Amount;
                    existingLab.SubjectID = lab.SubjectID;
                    existingLab.YearLevel = lab.YearLevel;

                    existingLab.Fee.AcctID = AcctID;
                    existingLab.Fee.SubAcctID = SubAcctID;
                    existingLab.Fee.FeeNameID = FeeNameID;

                    db.Entry(existingLab).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Lab fee updated successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating lab fee: " + ex.Message);
                }
            }

            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", lab.SubjectID);
            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(lab);
        }

        [HttpPost]
        public ActionResult DeleteLab(int id)
        {
            var lab = db.Lab.Include(l => l.Fee).FirstOrDefault(l => l.FeeID == id);
            if (lab == null)
                return HttpNotFound();

            try
            {
                db.Lab.Remove(lab);
                db.Fee.Remove(lab.Fee);
                db.SaveChanges();

                return Json(new { success = true, message = "Lab fee deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting lab fee: " + ex.Message });
            }
        }

        #endregion

        #region Aircon CRUD

        public ActionResult CreateAircon()
        {
            var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode");
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode");
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateAircon(Aircon aircon, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var periodid = int.Parse(HttpContext.Request.Cookies["PeriodId"].Value.ToString());

                    var fee = new Fee
                    {
                        PeriodID = periodid,
                        FeeCategory = "Aircon",
                        AcctID = AcctID,
                        SubAcctID = SubAcctID,
                        FeeNameID = FeeNameID
                    };

                    db.Fee.Add(fee);
                    db.SaveChanges();

                    aircon.FeeID = fee.FeeID;
                    db.Aircon.Add(aircon);
                    db.SaveChanges();

                    TempData["Success"] = "Aircon fee created successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error creating aircon fee: " + ex.Message);
                }
            }

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(aircon);
        }

        public ActionResult EditAircon(int id)
        {
            var aircon = db.Aircon.Include(a => a.Fee).FirstOrDefault(a => a.FeeID == id);
            if (aircon == null)
                return HttpNotFound();

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", aircon.Fee.AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", aircon.Fee.SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", aircon.Fee.FeeNameID);

            return View(aircon);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditAircon(Aircon aircon, int? AcctID, int? SubAcctID, int? FeeNameID)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingAircon = db.Aircon.Include(a => a.Fee).FirstOrDefault(a => a.FeeID == aircon.FeeID);
                    if (existingAircon == null)
                        return HttpNotFound();

                    existingAircon.Amount = aircon.Amount;
                    existingAircon.YearLevel = aircon.YearLevel;

                    existingAircon.Fee.AcctID = AcctID;
                    existingAircon.Fee.SubAcctID = SubAcctID;
                    existingAircon.Fee.FeeNameID = FeeNameID;

                    db.Entry(existingAircon).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Aircon fee updated successfully.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating aircon fee: " + ex.Message);
                }
            }

            ViewBag.AcctID = new SelectList(db.ChartOfAccounts, "AcctID", "AcctCode", AcctID);
            ViewBag.SubAcctID = new SelectList(db.SubChartOfAccounts, "SubAcctID", "SubAcctCode", SubAcctID);
            ViewBag.FeeNameID = new SelectList(db.FeeName, "FeeNameID", "FeeName1", FeeNameID);

            return View(aircon);
        }

        [HttpPost]
        public ActionResult DeleteAircon(int id)
        {
            var aircon = db.Aircon.Include(a => a.Fee).FirstOrDefault(a => a.FeeID == id);
            if (aircon == null)
                return HttpNotFound();

            try
            {
                db.Aircon.Remove(aircon);
                db.Fee.Remove(aircon.Fee);
                db.SaveChanges();

                return Json(new { success = true, message = "Aircon fee deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting aircon fee: " + ex.Message });
            }
        }

        #endregion

        #region MiscFeeSetup (list + dedicated create/edit)

        public ActionResult MiscFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var miscfees = db.Miscellaneous
                .Where(m => m.Fee.PeriodID == periodid)
                .Include(m => m.Fee.FeeName)
                .Include(m => m.Fee.ChartOfAccounts)
                .Include(m => m.Fee.SubChartOfAccounts)
                .ToList();
            return View(miscfees);
        }

        public ActionResult CreateMiscFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "M");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateMiscFeeSetup(Miscellaneous model)
        {
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
                fee.QneAccountCode = model.QneGlAccount;
                fee.PeriodID = periodid;
                db.Fee.Add(fee);
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Miscellaneous miscellaneous = new Miscellaneous();
                miscellaneous.Amount = model.Amount;
                miscellaneous.Description = feename != null ? feename.FeeName1 : model.Description;
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
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "M");
            var miscellaneous = db.Miscellaneous.Find(id);
            if (miscellaneous == null) return HttpNotFound();
            miscellaneous.FeeNameId = miscellaneous.Fee.FeeNameID;
            miscellaneous.GlAccount = miscellaneous.Fee.AcctID;
            miscellaneous.SubAccount = miscellaneous.Fee.SubAcctID;
            miscellaneous.QneGlAccount = miscellaneous.Fee.QneAccountCode;
            return View(miscellaneous);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditMiscFeeSetup(Miscellaneous model)
        {
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
                fee.QneAccountCode = model.QneGlAccount;
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Miscellaneous miscellaneous = db.Miscellaneous.Find(model.FeeID);
                miscellaneous.Amount = model.Amount;
                miscellaneous.Description = feename != null ? feename.FeeName1 : model.Description;
                miscellaneous.NewStatus = model.NewStatus;
                miscellaneous.YearLevel = model.YearLevel;
                db.SaveChanges();
                return RedirectToAction("MiscFeeSetup");
            }
            return View(model);
        }

        public ActionResult DeleteMiscFeeSetup(int id)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            Miscellaneous miscellaneous = db.Miscellaneous.Find(id);
            if (miscellaneous != null) { db.Miscellaneous.Remove(miscellaneous); db.SaveChanges(); }
            Fee fee = db.Fee.Find(id);
            if (fee != null) { db.Fee.Remove(fee); db.SaveChanges(); }
            return RedirectToAction("MiscFeeSetup");
        }

        #endregion

        #region SupplementalFeeSetup (list + dedicated create/edit)

        public ActionResult SupplementalFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var fees = db.Supplemental
                .Where(m => m.Fee.PeriodID == periodid)
                .Include(m => m.Fee.FeeName)
                .Include(m => m.Fee.ChartOfAccounts)
                .Include(m => m.Fee.SubChartOfAccounts)
                .ToList();
            return View(fees);
        }

        public ActionResult CreateSupplementalFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "S");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateSupplementalFeeSetup(Supplemental model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "S");
            if (ModelState.IsValid)
            {
                Fee fee = new Fee();
                fee.FeeNameID = model.FeeNameId;
                fee.FeeCategory = "S";
                fee.AcctID = model.GlAccount;
                fee.SubAcctID = model.SubAccount;
                fee.QneAccountCode = model.QneGlAccount;
                fee.PeriodID = periodid;
                db.Fee.Add(fee);
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Supplemental supplemental = new Supplemental();
                supplemental.Amount = model.Amount;
                supplemental.Description = feename != null ? feename.FeeName1 : model.Description;
                supplemental.EducationalLevel = period.EducLevelID;
                supplemental.FeeID = fee.FeeID;
                supplemental.NewStatus = model.NewStatus;
                supplemental.YearLevel = model.YearLevel;
                db.Supplemental.Add(supplemental);
                db.SaveChanges();
                return RedirectToAction("SupplementalFeeSetup");
            }
            return View(model);
        }

        public ActionResult EditSupplementalFeeSetup(int id)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "S");
            var supplemental = db.Supplemental.Find(id);
            if (supplemental == null) return HttpNotFound();
            supplemental.FeeNameId = supplemental.Fee.FeeNameID;
            supplemental.GlAccount = supplemental.Fee.AcctID;
            supplemental.SubAccount = supplemental.Fee.SubAcctID;
            supplemental.QneGlAccount = supplemental.Fee.QneAccountCode;
            return View(supplemental);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditSupplementalFeeSetup(Supplemental model)
        {
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
                fee.QneAccountCode = model.QneGlAccount;
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Supplemental supplemental = db.Supplemental.Find(model.FeeID);
                supplemental.Amount = model.Amount;
                supplemental.Description = feename != null ? feename.FeeName1 : model.Description;
                supplemental.NewStatus = model.NewStatus;
                supplemental.YearLevel = model.YearLevel;
                db.SaveChanges();
                return RedirectToAction("SupplementalFeeSetup");
            }
            return View(model);
        }

        public ActionResult DeleteSupplementalFeeSetup(int id)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            Supplemental supplemental = db.Supplemental.Find(id);
            if (supplemental != null) { db.Supplemental.Remove(supplemental); db.SaveChanges(); }
            Fee fee = db.Fee.Find(id);
            if (fee != null) { db.Fee.Remove(fee); db.SaveChanges(); }
            return RedirectToAction("SupplementalFeeSetup");
        }

        #endregion

        #region VariousFeeSetup (list + dedicated create/edit)

        public ActionResult VariousFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var fees = db.Various
                .Where(m => m.Fee.PeriodID == periodid)
                .Include(m => m.Fee.FeeName)
                .Include(m => m.Fee.ChartOfAccounts)
                .Include(m => m.Fee.SubChartOfAccounts)
                .Include(m => m.Progam)
                .ToList();
            return View(fees);
        }

        public ActionResult CreateVariousFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "V");
            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateVariousFeeSetup(Various model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "V");
            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode", model.ProgramID);
            if (ModelState.IsValid)
            {
                Fee fee = new Fee();
                fee.FeeNameID = model.FeeNameId;
                fee.FeeCategory = "V";
                fee.AcctID = model.GlAccount;
                fee.SubAcctID = model.SubAccount;
                fee.QneAccountCode = model.QneGlAccount;
                fee.PeriodID = periodid;
                db.Fee.Add(fee);
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Various various = new Various();
                various.Amount = model.Amount;
                various.Description = feename != null ? feename.FeeName1 : model.Description;
                various.FeeID = fee.FeeID;
                various.YearLevel = model.YearLevel;
                various.ProgramID = model.ProgramID;
                various.CurriculumID = model.CurriculumID;
                db.Various.Add(various);
                db.SaveChanges();
                return RedirectToAction("VariousFeeSetup");
            }
            return View(model);
        }

        public ActionResult EditVariousFeeSetup(int id)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "V");
            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode");
            var various = db.Various.Find(id);
            if (various == null) return HttpNotFound();
            various.FeeNameId = various.Fee.FeeNameID;
            various.GlAccount = various.Fee.AcctID;
            various.SubAccount = various.Fee.SubAcctID;
            various.QneGlAccount = various.Fee.QneAccountCode;
            return View(various);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditVariousFeeSetup(Various model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "V");
            ViewBag.ProgramID = new SelectList(db.Progam, "ProgramID", "ProgramCode", model.ProgramID);
            if (ModelState.IsValid)
            {
                Fee fee = db.Fee.Find(model.FeeID);
                fee.FeeNameID = model.FeeNameId;
                fee.AcctID = model.GlAccount == 0 ? (int?)null : model.GlAccount;
                fee.SubAcctID = model.SubAccount == 0 ? (int?)null : model.SubAccount;
                fee.QneAccountCode = model.QneGlAccount;
                db.SaveChanges();

                var feename = db.FeeName.Find(model.FeeNameId);
                Various various = db.Various.Find(model.FeeID);
                various.Amount = model.Amount;
                various.Description = feename != null ? feename.FeeName1 : model.Description;
                various.YearLevel = model.YearLevel;
                various.ProgramID = model.ProgramID;
                various.CurriculumID = model.CurriculumID;
                db.SaveChanges();
                return RedirectToAction("VariousFeeSetup");
            }
            return View(model);
        }

        public ActionResult DeleteVariousFeeSetup(int id)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            Various various = db.Various.Find(id);
            if (various != null) { db.Various.Remove(various); db.SaveChanges(); }
            Fee fee = db.Fee.Find(id);
            if (fee != null) { db.Fee.Remove(fee); db.SaveChanges(); }
            return RedirectToAction("VariousFeeSetup");
        }

        #endregion

        #region OtherFeeSetup (list)

        public ActionResult OtherFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var fees = db.Others
                .Where(m => m.Fee.PeriodID == periodid)
                .Include(m => m.Fee.FeeName)
                .Include(m => m.Fee.ChartOfAccounts)
                .Include(m => m.Fee.SubChartOfAccounts)
                .Include(m => m.Subject)
                .Include(m => m.Section)
                .ToList();
            return View(fees);
        }

        public ActionResult CreateOtherFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "O");
            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode");
            ViewBag.SectionID = new SelectList(db.Section.Where(m => m.PeriodID == periodid), "SectionID", "SectionName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateOtherFeeSetup(Others model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "O");
            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", model.SubjectID);
            ViewBag.SectionID = new SelectList(db.Section.Where(m => m.PeriodID == periodid), "SectionID", "SectionName", model.SectionID);
            if (ModelState.IsValid)
            {
                Fee fee = new Fee { FeeNameID = model.FeeNameId, FeeCategory = "O", AcctID = model.GlAccount, SubAcctID = model.SubAccount, QneAccountCode = model.QneGlAccount, PeriodID = periodid };
                db.Fee.Add(fee); db.SaveChanges();
                var feename = db.FeeName.Find(model.FeeNameId);
                Others others = new Others { Amount = model.Amount, Description = feename != null ? feename.FeeName1 : model.Description, FeeID = fee.FeeID, SubjectID = model.SubjectID, SectionID = model.SectionID };
                db.Others.Add(others); db.SaveChanges();
                return RedirectToAction("OtherFeeSetup");
            }
            return View(model);
        }

        public ActionResult EditOtherFeeSetup(int id)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "O");
            var others = db.Others.Find(id);
            if (others == null) return HttpNotFound();
            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", others.SubjectID);
            ViewBag.SectionID = new SelectList(db.Section.Where(m => m.PeriodID == periodid), "SectionID", "SectionName", others.SectionID);
            others.FeeNameId = others.Fee.FeeNameID;
            others.GlAccount = others.Fee.AcctID;
            others.SubAccount = others.Fee.SubAcctID;
            others.QneGlAccount = others.Fee.QneAccountCode;
            return View(others);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditOtherFeeSetup(Others model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "O");
            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", model.SubjectID);
            ViewBag.SectionID = new SelectList(db.Section.Where(m => m.PeriodID == periodid), "SectionID", "SectionName", model.SectionID);
            if (ModelState.IsValid)
            {
                Fee fee = db.Fee.Find(model.FeeID);
                fee.FeeNameID = model.FeeNameId;
                fee.AcctID = model.GlAccount == 0 ? (int?)null : model.GlAccount;
                fee.SubAcctID = model.SubAccount == 0 ? (int?)null : model.SubAccount;
                fee.QneAccountCode = model.QneGlAccount;
                db.SaveChanges();
                var feename = db.FeeName.Find(model.FeeNameId);
                Others others = db.Others.Find(model.FeeID);
                others.Amount = model.Amount;
                others.Description = feename != null ? feename.FeeName1 : model.Description;
                others.SubjectID = model.SubjectID;
                others.SectionID = model.SectionID;
                db.SaveChanges();
                return RedirectToAction("OtherFeeSetup");
            }
            return View(model);
        }

        public ActionResult DeleteOtherFeeSetup(int id)
        {
            Others others = db.Others.Find(id);
            if (others != null) { db.Others.Remove(others); db.SaveChanges(); }
            Fee fee = db.Fee.Find(id);
            if (fee != null) { db.Fee.Remove(fee); db.SaveChanges(); }
            return RedirectToAction("OtherFeeSetup");
        }

        #endregion

        #region LabFeeSetup (list)

        public ActionResult LabFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var fees = db.Lab
                .Where(m => m.Fee.PeriodID == periodid)
                .Include(m => m.Fee.FeeName)
                .Include(m => m.Fee.ChartOfAccounts)
                .Include(m => m.Fee.SubChartOfAccounts)
                .Include(m => m.Subject)
                .ToList();
            return View(fees);
        }

        public ActionResult CreateLabFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "L");
            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateLabFeeSetup(Lab model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "L");
            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", model.SubjectID);
            if (ModelState.IsValid)
            {
                Fee fee = new Fee { FeeNameID = model.FeeNameId, FeeCategory = "L", AcctID = model.GlAccount, SubAcctID = model.SubAccount, QneAccountCode = model.QneGlAccount, PeriodID = periodid };
                db.Fee.Add(fee); db.SaveChanges();
                var feename = db.FeeName.Find(model.FeeNameId);
                Lab lab = new Lab { Amount = model.Amount, Description = feename != null ? feename.FeeName1 : model.Description, FeeID = fee.FeeID, SubjectID = model.SubjectID, YearLevel = model.YearLevel };
                db.Lab.Add(lab); db.SaveChanges();
                return RedirectToAction("LabFeeSetup");
            }
            return View(model);
        }

        public ActionResult EditLabFeeSetup(int id)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "L");
            var lab = db.Lab.Find(id);
            if (lab == null) return HttpNotFound();
            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", lab.SubjectID);
            lab.FeeNameId = lab.Fee.FeeNameID;
            lab.GlAccount = lab.Fee.AcctID;
            lab.SubAccount = lab.Fee.SubAcctID;
            lab.QneGlAccount = lab.Fee.QneAccountCode;
            return View(lab);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditLabFeeSetup(Lab model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "L");
            ViewBag.SubjectID = new SelectList(db.Subject, "SubjectID", "SubjectCode", model.SubjectID);
            if (ModelState.IsValid)
            {
                Fee fee = db.Fee.Find(model.FeeID);
                fee.FeeNameID = model.FeeNameId;
                fee.AcctID = model.GlAccount == 0 ? (int?)null : model.GlAccount;
                fee.SubAcctID = model.SubAccount == 0 ? (int?)null : model.SubAccount;
                fee.QneAccountCode = model.QneGlAccount;
                db.SaveChanges();
                var feename = db.FeeName.Find(model.FeeNameId);
                Lab lab = db.Lab.Find(model.FeeID);
                lab.Amount = model.Amount;
                lab.Description = feename != null ? feename.FeeName1 : model.Description;
                lab.SubjectID = model.SubjectID;
                lab.YearLevel = model.YearLevel;
                db.SaveChanges();
                return RedirectToAction("LabFeeSetup");
            }
            return View(model);
        }

        public ActionResult DeleteLabFeeSetup(int id)
        {
            Lab lab = db.Lab.Find(id);
            if (lab != null) { db.Lab.Remove(lab); db.SaveChanges(); }
            Fee fee = db.Fee.Find(id);
            if (fee != null) { db.Fee.Remove(fee); db.SaveChanges(); }
            return RedirectToAction("LabFeeSetup");
        }

        #endregion

        #region AirconFeeSetup (list)

        public ActionResult AirconFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var fees = db.Aircon.Where(m => m.Fee.PeriodID == periodid).Include(m => m.Fee.FeeName).ToList();
            return View(fees);
        }

        public ActionResult CreateAirconFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "A");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateAirconFeeSetup(Aircon model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "A");
            if (ModelState.IsValid)
            {
                Fee fee = new Fee { FeeNameID = model.FeeNameId, FeeCategory = "A", AcctID = model.GlAccount, SubAcctID = model.SubAccount, QneAccountCode = model.QneGlAccount, PeriodID = periodid };
                db.Fee.Add(fee); db.SaveChanges();
                Aircon aircon = new Aircon { Amount = model.Amount, FeeID = fee.FeeID, YearLevel = model.YearLevel };
                db.Aircon.Add(aircon); db.SaveChanges();
                return RedirectToAction("AirconFeeSetup");
            }
            return View(model);
        }

        public ActionResult EditAirconFeeSetup(int id)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "A");
            var aircon = db.Aircon.Find(id);
            if (aircon == null) return HttpNotFound();
            aircon.FeeNameId = aircon.Fee.FeeNameID;
            aircon.GlAccount = aircon.Fee.AcctID;
            aircon.SubAccount = aircon.Fee.SubAcctID;
            aircon.QneGlAccount = aircon.Fee.QneAccountCode;
            return View(aircon);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditAirconFeeSetup(Aircon model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            FeeSetupViewBags(db, period, "A");
            if (ModelState.IsValid)
            {
                Fee fee = db.Fee.Find(model.FeeID);
                fee.FeeNameID = model.FeeNameId;
                fee.AcctID = model.GlAccount == 0 ? (int?)null : model.GlAccount;
                fee.SubAcctID = model.SubAccount == 0 ? (int?)null : model.SubAccount;
                fee.QneAccountCode = model.QneGlAccount;
                db.SaveChanges();
                Aircon aircon = db.Aircon.Find(model.FeeID);
                aircon.Amount = model.Amount;
                aircon.YearLevel = model.YearLevel;
                db.SaveChanges();
                return RedirectToAction("AirconFeeSetup");
            }
            return View(model);
        }

        public ActionResult DeleteAirconFeeSetup(int id)
        {
            Aircon aircon = db.Aircon.Find(id);
            if (aircon != null) { db.Aircon.Remove(aircon); db.SaveChanges(); }
            Fee fee = db.Fee.Find(id);
            if (fee != null) { db.Fee.Remove(fee); db.SaveChanges(); }
            return RedirectToAction("AirconFeeSetup");
        }

        #endregion

        #region TuitionFeeSetup (list)

        public ActionResult TuitionFeeSetup()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var fees = db.Tuition.Where(m => m.Fee.PeriodID == periodid).Include(m => m.Paymode).Include(m => m.Progam).ToList();
            return View(fees);
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