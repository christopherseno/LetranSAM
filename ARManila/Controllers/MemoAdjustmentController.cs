using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ARManila.Models;
using ARManila.Models.OtherDTO;

namespace ARManila.Controllers
{
    // Debit/Credit Memo and Adjustments (to student subject loads), summarized per academic
    // department and fee from [AR].[ArTrailMemoDetail], with the same Generate -> Draft -> Post
    // (Finalize) workflow and month N-of-M deferral as DeferredIncomeController.
    //
    // Amounts recognized per month = Amount / NoOfMonths * NthMonth, minus what earlier finalized
    // months already posted (cumulative), keyed by MemoType + Department + Fee.
    // Persisted via raw SQL to AR.MemoAdjustmentPosting / AR.MemoAdjustmentPostingDetail
    // (run Database/MemoAdjustmentPosting.Tables.sql once).
    public class MemoAdjustmentController : BaseController
    {
        private LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        private Employee employee;
        protected Period Period { get; private set; }

        // Memo types in display order, with friendly section titles.
        private static readonly Tuple<string, string>[] MemoTypeOrder =
        {
            Tuple.Create("DebitMemo",  "Debit Memo"),
            Tuple.Create("CreditMemo", "Credit Memo"),
            Tuple.Create("DNForm",     "Debit Adjustment"),
            Tuple.Create("CMForm",     "Credit Adjustment")
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

            var batches = db.GetMemoPostingBatches(Period.PeriodID);
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

            int result = db.SaveMemoPosting(Period.PeriodID, postingdate, employee.EmployeeID, nthmonth, noofmonths, entries);
            if (result == 0)
            {
                TempData["Error"] = "This posting date is already final and was not overridden.";
                return RedirectToAction("Details", new { date = postingdate.ToString("yyyy-MM-dd") });
            }

            TempData["Message"] = "Saved draft for " + postingdate.ToShortDateString() +
                " (Month " + nthmonth + " of " + noofmonths + ").";
            return RedirectToAction("Details", new { date = postingdate.ToString("yyyy-MM-dd") });
        }

        // Locks a posting date. Once final it is no longer overridden and its posted amounts are
        // deducted from later months.
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
            int affected = db.FinalizeMemoPosting(Period.PeriodID, postingdate);
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
            int result = db.DeleteMemoPosting(Period.PeriodID, postingdate);
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
                TempData["Message"] = "Deleted memo/adjustment posting for " + postingdate.ToShortDateString() + ".";
            }
            return RedirectToAction("Index");
        }

        // ---- Details / Print of a saved batch ----

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
                ViewBag.Error = "No saved memo/adjustment posting found for " + postingdate.ToShortDateString() + ".";
                return View((MemoAdjustmentQueryDTO)null);
            }
            return View(model);
        }

        // Alternate view: rolls the saved posting up by Chart of Account (within each memo type),
        // for posting journal entries to the GL.
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
                ViewBag.Error = "No saved memo/adjustment posting found for " + postingdate.ToShortDateString() + ".";
                return View((MemoGLDTO)null);
            }
            return View(model);
        }

        public ActionResult Excel(string date)
        {
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate)) { return Content("Invalid date."); }
            MemoAdjustmentQueryDTO model = BuildFromSaved(postingdate);
            if (model == null) { return Content("No saved memo/adjustment posting found for " + postingdate.ToShortDateString() + "."); }

            byte[] bytes = ReportExcel.MemoMatrix(model, "Debit / Credit Memo & Adjustments");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "MemoAdjustments_" + postingdate.ToString("dd-MMMM-yyyy") + ".xlsx");
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
                return Content("No saved memo/adjustment posting found for " + postingdate.ToShortDateString() + ".");
            }

            ViewBag.PreparedBy = employee == null ? "" : employee.FullName;
            ViewBag.PrintedOn = DateTime.Now;
            ViewBag.LogoDataUri = ImageDataUri("~/Images/letranseal.jpg");

            return new Rotativa.ViewAsPdf("Print", model)
            {
                FileName = "MemoAdjustments_" + postingdate.ToString("dd-MMMM-yyyy") + ".pdf",
                PageOrientation = Rotativa.Options.Orientation.Landscape,
                PageSize = Rotativa.Options.Size.A4,
                PageMargins = new Rotativa.Options.Margins(10, 8, 14, 8),
                CustomSwitches =
                    "--footer-center \"Page [page] of [topage]\" --footer-font-size 8 --footer-spacing 3"
            };
        }

        // ---- Builders ----

        // Runs the SP and turns each (MemoType, Dept, Fee) group into a posting entry, computing
        // the amount recognized this month (cumulative target minus prior finalized).
        private List<MemoPostingDetailInput> ComputeEntriesFromSp(DateTime asof, int nth, int noof, out Dictionary<int, MemoColumnDTO> depts)
        {
            List<ArTrailMemoDetailRow> rows = db.GetArTrailMemoDetail(Period.PeriodID, asof.ToString("yyyy-MM-dd"));

            depts = new Dictionary<int, MemoColumnDTO>();
            var entries = new List<MemoPostingDetailInput>();

            if (rows == null)
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
                    MemoType = g.Key.MemoType,
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
            var saved = db.GetSavedMemoDetails(Period.PeriodID, postingdate);
            if (saved == null || saved.Count == 0)
            {
                // No detail rows -- could be a saved zero batch; fall back to the header so the
                // (zero) posting is still viewable, printable and finalizable.
                var hdr = db.GetMemoPostingHeader(Period.PeriodID, postingdate);
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

        // Rolls the saved posting up by Chart of Account within each memo type.
        private MemoGLDTO BuildGL(DateTime postingdate)
        {
            var saved = db.GetSavedMemoDetails(Period.PeriodID, postingdate);
            if (saved == null || saved.Count == 0)
            {
                // Saved zero batch (header, no details) -> a GL view with no account lines.
                var hdr = db.GetMemoPostingHeader(Period.PeriodID, postingdate);
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

            foreach (var memo in MemoTypeOrder)
            {
                var typeRows = saved.Where(s => string.Equals(s.MemoType, memo.Item1, StringComparison.OrdinalIgnoreCase)).ToList();
                if (typeRows.Count == 0)
                {
                    continue;
                }

                MemoGLSectionDTO section = new MemoGLSectionDTO { MemoType = memo.Item1, Title = memo.Item2 };

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

        // Turns a flat list of posting entries into the grouped matrix (columns = departments,
        // sections = memo types, rows = fees, per-section subtotals + grand total).
        private void BuildMatrix(MemoAdjustmentQueryDTO model, List<MemoPostingDetailInput> entries, List<MemoColumnDTO> depts)
        {
            model.Columns = depts.OrderBy(c => c.Header).ToList();

            MemoRowDTO grand = new MemoRowDTO { Particular = "Grand Total" };

            foreach (var memo in MemoTypeOrder)
            {
                var typeEntries = entries.Where(e => string.Equals(e.MemoType, memo.Item1, StringComparison.OrdinalIgnoreCase)).ToList();
                if (typeEntries.Count == 0)
                {
                    continue;
                }

                MemoSectionDTO section = new MemoSectionDTO { MemoType = memo.Item1, Title = memo.Item2 };
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

        private static string PostingKey(string memoType, int deptId, int? feeId)
        {
            return (memoType ?? "") + "_" + deptId + "_" + (feeId.HasValue ? feeId.Value.ToString() : "null");
        }

        private string ValidateMonths(int nthmonth, int noofmonths)
        {
            if (noofmonths < 1) { return "No. of months must be at least 1."; }
            if (nthmonth < 1 || nthmonth > noofmonths) { return "Nth month must be between 1 and the no. of months."; }
            return null;
        }

        // Reads an app image as a base64 data URI (or null) so it can be inlined into the
        // Rotativa/wkhtmltopdf view, which cannot fetch root-relative image URLs.
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
