using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ARManila.Models;
using ARManila.Models.OtherDTO;

namespace ARManila.Controllers
{
    // Adjustment discounts (discount portion of subject-load adjustments), summarized per academic
    // department and fee from [AR].[ArTrailAdjDiscountDetail2024], with the same
    // Generate -> Draft -> Post (Finalize) workflow and month N-of-M deferral as the other reports.
    //
    // Amounts recognized per month = Amount / NoOfMonths * NthMonth, minus what earlier finalized
    // months already posted (cumulative), keyed by Department + Fee.
    // Persisted via raw SQL to AR.AdjDiscountPosting / AR.AdjDiscountPostingDetail
    // (run Database/AdjDiscountPosting.Tables.sql once). Display DTOs are shared with the other
    // posting features; this report has a single "Adjustment Discount" section.
    public class AdjDiscountDetailController : BaseController
    {
        private LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        private Employee employee;
        protected Period Period { get; private set; }

        private static readonly Tuple<string, string>[] AdjDiscTypeOrder =
        {
            Tuple.Create("AdjDiscount", "Adjustment Discount")
        };

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            db.Database.CommandTimeout = 300;
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            Period = db.Period.Find(periodid);
            if (Period == null)
            {
                throw new Exception("Invalid period id.");
            }
            employee = db.Employee.FirstOrDefault(m => m.EmployeeNo == User.Identity.Name);
        }

        private string PeriodDisplayName()
        {
            return Period.EducationalLevel1.EducLevelName + " - " + Period.FullName;
        }

        // ---- List of saved posting batches for the current period ----

        public ActionResult Index()
        {
            if (TempData["Message"] != null) { ViewBag.Message = TempData["Message"]; }
            if (TempData["Error"] != null) { ViewBag.Error = TempData["Error"]; }

            var batches = db.GetAdjDiscountPostingBatches(Period.PeriodID);
            var genIds = batches.Select(b => b.GeneratedBy).Distinct().ToList();
            var names = db.Employee.Where(e => genIds.Contains(e.EmployeeID)).ToList()
                          .ToDictionary(e => e.EmployeeID, e => e.FullName);

            var model = new MemoPostingListDTO { PeriodName = PeriodDisplayName() };
            foreach (var b in batches)
            {
                string gen;
                names.TryGetValue(b.GeneratedBy, out gen);
                model.Batches.Add(new MemoPostingBatchDTO
                {
                    PostingDate = b.PostingDate,
                    DateGenerated = b.DateGenerated,
                    GeneratedBy = gen,
                    IsFinal = b.IsFinal,
                    NthMonth = b.NthMonth,
                    NoOfMonths = b.NoOfMonths,
                    TotalPosted = b.TotalPosted ?? 0m
                });
            }
            return View(model);
        }

        // ---- Generate / preview from the AR trail ----

        public ActionResult Generate()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Generate(DateTime asofdate, int nthmonth, int noofmonths)
        {
            string error = ValidateMonths(nthmonth, noofmonths);
            if (error != null)
            {
                ViewBag.Error = error;
                return View();
            }
            MemoAdjustmentQueryDTO model = BuildFromSp(asofdate, nthmonth, noofmonths);
            return View(model);
        }

        [HttpPost]
        public ActionResult Save(DateTime asofdate, int nthmonth, int noofmonths)
        {
            string error = ValidateMonths(nthmonth, noofmonths);
            if (error != null)
            {
                ViewBag.Error = error;
                return View("Generate");
            }
            if (employee == null)
            {
                ViewBag.Error = "Employee record not found for the current user.";
                return View("Generate");
            }

            DateTime postingdate = asofdate.Date;

            // Flat per-month posting: months are independent snapshots, so a zero result is a valid
            // posting (records that this month recognized nothing) and is saved like any other.
            Dictionary<int, MemoColumnDTO> depts;
            List<MemoPostingDetailInput> entries = ComputeEntriesFromSp(asofdate, nthmonth, noofmonths, out depts);

            int result = db.SaveAdjDiscountPosting(Period.PeriodID, postingdate, employee.EmployeeID, nthmonth, noofmonths, entries);
            if (result == 0)
            {
                TempData["Error"] = "This posting date is already final and was not overridden.";
                return RedirectToAction("Details", new { date = postingdate.ToString("yyyy-MM-dd") });
            }

            TempData["Message"] = "Saved draft for " + postingdate.ToShortDateString() +
                " (Month " + nthmonth + " of " + noofmonths + ").";
            return RedirectToAction("Details", new { date = postingdate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        public ActionResult Finalize(string date)
        {
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                TempData["Error"] = "Invalid date.";
                return RedirectToAction("Index");
            }
            postingdate = postingdate.Date;
            int affected = db.FinalizeAdjDiscountPosting(Period.PeriodID, postingdate);
            if (affected == 0)
            {
                TempData["Error"] = "No saved (non-final) records found for this date.";
            }
            else
            {
                TempData["Message"] = "Posting for " + postingdate.ToShortDateString() + " marked as final.";
            }
            return RedirectToAction("Details", new { date = postingdate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        public ActionResult Delete(string date)
        {
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                TempData["Error"] = "Invalid date.";
                return RedirectToAction("Index");
            }
            postingdate = postingdate.Date;
            int result = db.DeleteAdjDiscountPosting(Period.PeriodID, postingdate);
            if (result == -1)
            {
                TempData["Error"] = "Cannot delete: this posting date is already final.";
            }
            else if (result == 0)
            {
                TempData["Error"] = "No records found for this date.";
            }
            else
            {
                TempData["Message"] = "Deleted adjustment-discount posting for " + postingdate.ToShortDateString() + ".";
            }
            return RedirectToAction("Index");
        }

        // ---- Details / GL / Print of a saved batch ----

        public ActionResult Details(string date)
        {
            if (TempData["Message"] != null) { ViewBag.Message = TempData["Message"]; }
            if (TempData["Error"] != null) { ViewBag.Error = TempData["Error"]; }

            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                ViewBag.Error = "Invalid date.";
                return View((MemoAdjustmentQueryDTO)null);
            }
            MemoAdjustmentQueryDTO model = BuildFromSaved(postingdate);
            if (model == null)
            {
                ViewBag.Error = "No saved adjustment-discount posting found for " + postingdate.ToShortDateString() + ".";
                return View((MemoAdjustmentQueryDTO)null);
            }
            return View(model);
        }

        public ActionResult DetailsGL(string date)
        {
            if (TempData["Message"] != null) { ViewBag.Message = TempData["Message"]; }
            if (TempData["Error"] != null) { ViewBag.Error = TempData["Error"]; }

            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                ViewBag.Error = "Invalid date.";
                return View((MemoGLDTO)null);
            }
            MemoGLDTO model = BuildGL(postingdate);
            if (model == null)
            {
                ViewBag.Error = "No saved adjustment-discount posting found for " + postingdate.ToShortDateString() + ".";
                return View((MemoGLDTO)null);
            }
            return View(model);
        }

        // ---- Period-wide summary (all posted months) ----

        public ActionResult Summary()
        {
            return View("Summary", BuildSummary());
        }

        public ActionResult SummaryPdf()
        {
            SummaryReportDTO model = BuildSummary();
            ViewBag.PrintedOn = DateTime.Now;
            ViewBag.LogoDataUri = ImageDataUri("~/Images/letranseal.jpg");
            int numericCols = 2 * model.MonthCount + 1 + (model.ShowAdjustments ? 2 : 0);
            int widthMm = Math.Max(330, 120 + numericCols * 20);
            return new Rotativa.ViewAsPdf("SummaryPrint", model)
            {
                FileName = "AdjustmentDiscounts_Summary_" + DateTime.Now.ToString("yyyyMMdd") + ".pdf",
                PageMargins = new Rotativa.Options.Margins(8, 6, 10, 6),
                CustomSwitches = "--page-width " + widthMm + "mm --page-height 216mm " +
                    "--footer-center \"Page [page] of [topage]\" --footer-font-size 7 --footer-spacing 2"
            };
        }

        public ActionResult SummaryExcel()
        {
            byte[] bytes = ReportExcel.Summary(BuildSummary());
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "AdjustmentDiscounts_Summary_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx");
        }

        private SummaryReportDTO BuildSummary()
        {
            var details = db.GetAllSavedAdjDiscountDetails(Period.PeriodID);
            return SummaryBuilder.FromDetails("Adjustment Discounts", PeriodDisplayName(), Period.FullName, details, AdjDiscTypeOrder);
        }

        public ActionResult Excel(string date)
        {
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate)) { return Content("Invalid date."); }
            MemoAdjustmentQueryDTO model = BuildFromSaved(postingdate);
            if (model == null) { return Content("No saved adjustment-discount posting found for " + postingdate.ToShortDateString() + "."); }

            byte[] bytes = ReportExcel.MemoMatrix(model, "Adjustment Discounts");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "AdjustmentDiscounts_" + postingdate.ToString("dd-MMMM-yyyy") + ".xlsx");
        }

        public ActionResult Print(string date)
        {
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                return Content("Invalid date.");
            }
            MemoAdjustmentQueryDTO model = BuildFromSaved(postingdate);
            if (model == null)
            {
                return Content("No saved adjustment-discount posting found for " + postingdate.ToShortDateString() + ".");
            }

            ViewBag.PreparedBy = employee == null ? "" : employee.FullName;
            ViewBag.PrintedOn = DateTime.Now;
            ViewBag.LogoDataUri = ImageDataUri("~/Images/letranseal.jpg");

            return new Rotativa.ViewAsPdf("Print", model)
            {
                FileName = "AdjustmentDiscounts_" + postingdate.ToString("dd-MMMM-yyyy") + ".pdf",
                PageOrientation = Rotativa.Options.Orientation.Landscape,
                PageSize = Rotativa.Options.Size.A4,
                PageMargins = new Rotativa.Options.Margins(10, 8, 14, 8),
                CustomSwitches =
                    "--footer-center \"Page [page] of [topage]\" --footer-font-size 8 --footer-spacing 3"
            };
        }

        // ---- Builders ----

        private List<MemoPostingDetailInput> ComputeEntriesFromSp(DateTime asof, int nth, int noof, out Dictionary<int, MemoColumnDTO> depts)
        {
            List<ArTrailMemoDetailRow> rows = db.GetArTrailAdjDiscountDetail(Period.PeriodID, asof.ToString("yyyy-MM-dd"));

            depts = new Dictionary<int, MemoColumnDTO>();
            var entries = new List<MemoPostingDetailInput>();

            // Zero records from the SP: return an empty entry list (callers handle the empty state).
            if (rows == null || rows.Count == 0)
            {
                return entries;
            }

            foreach (var g in rows.Where(r => r.AcaDeptID.HasValue)
                                  .GroupBy(r => new { r.MemoType, Dept = r.AcaDeptID.Value, r.FeeID }))
            {
                var first = g.First();
                if (!depts.ContainsKey(g.Key.Dept))
                {
                    depts[g.Key.Dept] = new MemoColumnDTO
                    {
                        AcademicDepartmentId = g.Key.Dept,
                        Header = first.AcaAcronym,
                        DepartmentName = first.AcaDepartmentName
                    };
                }

                decimal amount = (decimal)g.Sum(x => x.Amount ?? 0d);
                // Flat per-month: each month recognizes its own 1/NoOfMonths of the amount that
                // exists as of THIS posting date. No cumulative catch-up, so a later/backdated
                // amount is never back-attributed to an earlier (already-posted) month.
                decimal postedThisMonth = Math.Round(amount / noof, 2);

                entries.Add(new MemoPostingDetailInput
                {
                    MemoType = string.IsNullOrEmpty(g.Key.MemoType) ? "AdjDiscount" : g.Key.MemoType,
                    AcaDeptId = g.Key.Dept,
                    FeeId = g.Key.FeeID,
                    Particular = first.Particular,
                    ChartOfAccount = g.Select(x => x.ChartOfAccount).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                    QNECode = g.Select(x => x.QNECode).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                    Amount = amount,
                    PostedAmount = postedThisMonth
                });
            }

            return entries;
        }

        private MemoAdjustmentQueryDTO BuildFromSp(DateTime asof, int nth, int noof)
        {
            Dictionary<int, MemoColumnDTO> depts;
            List<MemoPostingDetailInput> entries = ComputeEntriesFromSp(asof, nth, noof, out depts);

            MemoAdjustmentQueryDTO model = new MemoAdjustmentQueryDTO
            {
                PeriodId = Period.PeriodID,
                PeriodName = PeriodDisplayName(),
                AsOfDate = asof,
                NthMonth = nth,
                NoOfMonths = noof
            };
            BuildMatrix(model, entries, depts.Values.ToList());
            return model;
        }

        private MemoAdjustmentQueryDTO BuildFromSaved(DateTime postingdate)
        {
            var saved = db.GetSavedAdjDiscountDetails(Period.PeriodID, postingdate);
            if (saved == null || saved.Count == 0)
            {
                // No detail rows -- could be a saved zero batch; fall back to the header so the
                // (zero) posting is still viewable, printable and finalizable.
                var hdr = db.GetAdjDiscountPostingHeader(Period.PeriodID, postingdate);
                if (hdr == null) { return null; }
                var zero = new MemoAdjustmentQueryDTO
                {
                    PeriodId = Period.PeriodID,
                    PeriodName = PeriodDisplayName(),
                    AsOfDate = postingdate.Date,
                    NthMonth = hdr.NthMonth,
                    NoOfMonths = hdr.NoOfMonths,
                    HasExistingRecord = true,
                    IsFinal = hdr.IsFinal
                };
                BuildMatrix(zero, new List<MemoPostingDetailInput>(), new List<MemoColumnDTO>());
                return zero;
            }

            var deptIds = saved.Select(s => s.AcaDeptId).Distinct().ToList();
            var deptMeta = db.AcademicDepartment.Where(d => deptIds.Contains(d.AcaDeptID)).ToList();
            var columns = deptIds.Select(id =>
            {
                var d = deptMeta.FirstOrDefault(x => x.AcaDeptID == id);
                return new MemoColumnDTO
                {
                    AcademicDepartmentId = id,
                    Header = d != null && !string.IsNullOrWhiteSpace(d.AcaAcronym) ? d.AcaAcronym
                             : (d != null ? d.AcaDepartmentName : "Dept " + id),
                    DepartmentName = d != null ? d.AcaDepartmentName : "Dept " + id
                };
            }).ToList();

            var entries = saved.Select(s => new MemoPostingDetailInput
            {
                MemoType = s.MemoType,
                AcaDeptId = s.AcaDeptId,
                FeeId = s.FeeId,
                Particular = s.Particular,
                ChartOfAccount = s.ChartOfAccount,
                QNECode = s.QNECode,
                Amount = s.Amount,
                PostedAmount = s.PostedAmount
            }).ToList();

            var head = saved.First();
            MemoAdjustmentQueryDTO model = new MemoAdjustmentQueryDTO
            {
                PeriodId = Period.PeriodID,
                PeriodName = PeriodDisplayName(),
                AsOfDate = postingdate.Date,
                NthMonth = head.NthMonth,
                NoOfMonths = head.NoOfMonths,
                HasExistingRecord = true,
                IsFinal = head.IsFinal
            };
            BuildMatrix(model, entries, columns);
            return model;
        }

        private MemoGLDTO BuildGL(DateTime postingdate)
        {
            var saved = db.GetSavedAdjDiscountDetails(Period.PeriodID, postingdate);
            if (saved == null || saved.Count == 0)
            {
                // Saved zero batch (header, no details) -> a GL view with no account lines.
                var hdr = db.GetAdjDiscountPostingHeader(Period.PeriodID, postingdate);
                if (hdr == null) { return null; }
                return new MemoGLDTO
                {
                    PeriodName = PeriodDisplayName(),
                    AsOfDate = postingdate.Date,
                    NthMonth = hdr.NthMonth,
                    NoOfMonths = hdr.NoOfMonths,
                    IsFinal = hdr.IsFinal
                };
            }

            var head = saved.First();
            MemoGLDTO gl = new MemoGLDTO
            {
                PeriodName = PeriodDisplayName(),
                AsOfDate = postingdate.Date,
                NthMonth = head.NthMonth,
                NoOfMonths = head.NoOfMonths,
                IsFinal = head.IsFinal
            };

            foreach (var disc in AdjDiscTypeOrder)
            {
                var typeRows = saved.Where(s => string.Equals(s.MemoType, disc.Item1, StringComparison.OrdinalIgnoreCase)).ToList();
                if (typeRows.Count == 0)
                {
                    continue;
                }

                MemoGLSectionDTO section = new MemoGLSectionDTO { MemoType = disc.Item1, Title = disc.Item2 };

                foreach (var g in typeRows.GroupBy(s => new
                                          {
                                              Coa = string.IsNullOrWhiteSpace(s.ChartOfAccount) ? null : s.ChartOfAccount,
                                              Qne = string.IsNullOrWhiteSpace(s.QNECode) ? null : s.QNECode
                                          })
                                          .OrderBy(g => g.Key.Coa))
                {
                    var particulars = g.Select(x => x.Particular)
                                       .Where(p => !string.IsNullOrWhiteSpace(p))
                                       .Distinct()
                                       .ToList();
                    section.Rows.Add(new MemoGLRowDTO
                    {
                        ChartOfAccount = g.Key.Coa,
                        QNECode = g.Key.Qne,
                        Particular = string.Join(", ", particulars),
                        Amount = g.Sum(x => x.Amount),
                        PostedAmount = g.Sum(x => x.PostedAmount)
                    });
                }

                gl.Sections.Add(section);
            }

            return gl;
        }

        private void BuildMatrix(MemoAdjustmentQueryDTO model, List<MemoPostingDetailInput> entries, List<MemoColumnDTO> depts)
        {
            model.Columns = depts.OrderBy(c => c.Header).ToList();

            MemoRowDTO grand = new MemoRowDTO { Particular = "Grand Total" };

            foreach (var disc in AdjDiscTypeOrder)
            {
                var typeEntries = entries.Where(e => string.Equals(e.MemoType, disc.Item1, StringComparison.OrdinalIgnoreCase)).ToList();
                if (typeEntries.Count == 0)
                {
                    continue;
                }

                MemoSectionDTO section = new MemoSectionDTO { MemoType = disc.Item1, Title = disc.Item2 };
                MemoRowDTO subtotal = new MemoRowDTO { Particular = "Subtotal" };

                foreach (var feeGroup in typeEntries.GroupBy(e => e.FeeId).OrderBy(g => g.First().Particular))
                {
                    var first = feeGroup.First();
                    MemoRowDTO row = new MemoRowDTO
                    {
                        FeeId = first.FeeId,
                        Particular = first.Particular,
                        ChartOfAccount = feeGroup.Select(x => x.ChartOfAccount).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                        QNECode = feeGroup.Select(x => x.QNECode).FirstOrDefault(x => !string.IsNullOrEmpty(x))
                    };
                    foreach (var e in feeGroup)
                    {
                        row.Add(e.AcaDeptId, e.Amount, e.PostedAmount);
                        subtotal.Add(e.AcaDeptId, e.Amount, e.PostedAmount);
                        grand.Add(e.AcaDeptId, e.Amount, e.PostedAmount);
                    }
                    section.Rows.Add(row);
                }

                section.Subtotal = subtotal;
                model.Sections.Add(section);
            }

            model.GrandTotal = grand;
        }

        private static string PostingKey(string type, int deptId, int? feeId)
        {
            return (type ?? "") + "_" + deptId + "_" + (feeId.HasValue ? feeId.Value.ToString() : "null");
        }

        private string ValidateMonths(int nthmonth, int noofmonths)
        {
            if (noofmonths < 1) { return "No. of months must be at least 1."; }
            if (nthmonth < 1 || nthmonth > noofmonths) { return "Nth month must be between 1 and the no. of months."; }
            return null;
        }

        private string ImageDataUri(string virtualPath)
        {
            try
            {
                string path = Server.MapPath(virtualPath);
                if (!System.IO.File.Exists(path)) { return null; }
                string ext = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                string mime = ext == "png" ? "image/png"
                            : (ext == "jpg" || ext == "jpeg") ? "image/jpeg"
                            : (ext == "gif" ? "image/gif" : "application/octet-stream");
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                return "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
            }
            catch { return null; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
