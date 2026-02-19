using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;

namespace ARManila.Controllers
{
    public class FeeNameController : BaseController
    {
        // GET: FeeName
        public ActionResult Index()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var feenames = db.FeeName.Where(m => m.EducLevelID == period.EducLevelID);
            return View(feenames);
        }

        public ActionResult Create()
        {
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
        [ValidateAntiForgeryToken]
        public ActionResult Create(FeeName model)
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

        public ActionResult Edit(int id)
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
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FeeName model)
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

        public ActionResult Delete(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");

            var feename = db.FeeName.Find(id);
            if (feename != null)
            {
                db.FeeName.Remove(feename);
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}