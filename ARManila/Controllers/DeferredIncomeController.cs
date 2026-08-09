using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using ARManila.Models;
using ARManila.Models.OtherDTO;
using CrystalDecisions.CrystalReports.Engine;

namespace ARManila.Controllers
{
    public class DeferredIncomeController : BaseController
    {
        private LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        private Employee employee;
        protected Period Period { get; private set; }

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

        // ---- List of saved deferred income for the current period ----

        public ActionResult Index()
        {
            CarryTempMessages();
            int periodid = Period.PeriodID;
            var records = db.DeferredIncome.Where(m => m.PeriodId == periodid).ToList();

            var postedByDate = (from f in db.DeferredIncomeFee
                                join di in db.DeferredIncome on f.DeferredIncomeId equals di.Id
                                where di.PeriodId == periodid
                                group f by di.PostingDate into g
                                select new { Date = g.Key, Posted = g.Sum(x => x.PostedAmount) })
                               .ToList()
                               .ToDictionary(x => x.Date, x => x.Posted);

            // Decrypt only the employees who generated a batch (FullName runs a decrypt per call).
            var genByIds = records.Select(m => m.GeneratedBy).Distinct().ToList();
            var employees = db.Employee.Where(e => genByIds.Contains(e.EmployeeID)).ToList()
                              .ToDictionary(e => e.EmployeeID, e => e.FullName);

            DeferredIncomeListDTO model = new DeferredIncomeListDTO
            {
                PeriodName = Period.EducationalLevel1.EducLevelName + " - " + Period.FullName
            };

            foreach (var group in records.GroupBy(m => m.PostingDate).OrderByDescending(g => g.Key))
            {
                var first = group.OrderByDescending(m => m.DateGenerated).First();
                decimal posted;
                postedByDate.TryGetValue(group.Key, out posted);
                string genby;
                employees.TryGetValue(first.GeneratedBy, out genby);
                model.Batches.Add(new DeferredIncomeBatchDTO
                {
                    PostingDate = group.Key,
                    DepartmentCount = group.Count(),
                    FinalCount = group.Count(m => m.IsFinal),
                    DateGenerated = first.DateGenerated,
                    GeneratedBy = genby,
                    NthMonth = first.NthMonth,
                    NoOfMonths = first.NoOfMonths,
                    TotalPosted = posted
                });
            }
            return View(model);
        }

        // ---- Generate / preview from current assessments ----

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
            DeferredIncomeQueryDTO model = BuildSummary(asofdate, nthmonth, noofmonths);
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

            int periodid = Period.PeriodID;
            DeferredIncomeQueryDTO model = BuildSummary(asofdate, nthmonth, noofmonths);
            DateTime postingdate = asofdate.Date;
            List<string> saved = new List<string>();
            List<string> skipped = new List<string>();

            foreach (var dept in model.Departments)
            {
                // Flat per-month posting: each date is an independent snapshot, so there is no
                // ordering dependency between months. A finalized department is still immutable below.
                var existing = db.DeferredIncome.FirstOrDefault(m => m.PostingDate == postingdate && m.PeriodId == periodid && m.AcademicDepartmentId == dept.AcademicDepartmentId);
                DeferredIncome target;
                if (existing != null)
                {
                    if (existing.IsFinal)
                    {
                        skipped.Add(dept.DepartmentName);
                        continue;
                    }
                    var oldfees = db.DeferredIncomeFee.Where(m => m.DeferredIncomeId == existing.Id);
                    db.DeferredIncomeFee.RemoveRange(oldfees);
                    existing.DateGenerated = DateTime.Now;
                    existing.GeneratedBy = employee.EmployeeID;
                    existing.NthMonth = nthmonth;
                    existing.NoOfMonths = noofmonths;
                    target = existing;
                }
                else
                {
                    target = new DeferredIncome
                    {
                        PeriodId = periodid,
                        AcademicDepartmentId = dept.AcademicDepartmentId,
                        DateGenerated = DateTime.Now,
                        PostingDate = postingdate,
                        GeneratedBy = employee.EmployeeID,
                        IsFinal = false,
                        NthMonth = nthmonth,
                        NoOfMonths = noofmonths
                    };
                    db.DeferredIncome.Add(target);
                }
                db.SaveChanges();

                foreach (var fee in dept.Fees)
                {
                    db.DeferredIncomeFee.Add(new DeferredIncomeFee
                    {
                        DeferredIncomeId = target.Id,
                        FeeId = fee.FeeId,
                        Amount = fee.Amount,
                        PostedAmount = fee.PostedAmount
                    });
                }
                db.SaveChanges();
                saved.Add(dept.DepartmentName);
            }

            string message = saved.Count == 0 ? "No records were saved." : "Saved: " + string.Join(", ", saved) + ".";
            if (skipped.Count > 0)
            {
                message += " Skipped (already final): " + string.Join(", ", skipped) + ".";
            }
            TempData["Message"] = message;

            return RedirectToAction("Details", new { date = postingdate.ToString("yyyy-MM-dd") });
        }

        // ---- Details of a saved posting date (rebuilt from DeferredIncome/DeferredIncomeFee) ----

        public ActionResult Details(string date)
        {
            CarryTempMessages();
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                ViewBag.Error = "Invalid date.";
                return View((DeferredIncomeQueryDTO)null);
            }
            DeferredIncomeQueryDTO model = BuildFromSaved(postingdate);
            if (model == null)
            {
                ViewBag.Error = "No deferred income records found for " + postingdate.ToShortDateString() + ".";
                return View((DeferredIncomeQueryDTO)null);
            }
            return View(model);
        }

        // Alternate Details view: shows each fee's mapped GL Chart of Account and QNE account code.
        public ActionResult DetailsGL(string date)
        {
            CarryTempMessages();
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                ViewBag.Error = "Invalid date.";
                return View((DeferredIncomeGLDTO)null);
            }
            DeferredIncomeGLDTO model = BuildGL(postingdate);
            if (model == null)
            {
                ViewBag.Error = "No deferred income records found for " + postingdate.ToShortDateString() + ".";
                return View((DeferredIncomeGLDTO)null);
            }
            return View(model);
        }

        // Separate process to lock the saved postings for a date. Once final they are posted to the
        // accounting system and are immutable -- never overridden on save nor deletable. Corrections
        // are made through a separate adjustment.
        [HttpPost]
        public ActionResult Finalize(string date)
        {
            int periodid = Period.PeriodID;
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                TempData["Error"] = "Invalid date.";
                return RedirectToAction("Index");
            }
            postingdate = postingdate.Date;
            var records = db.DeferredIncome
                .Where(m => m.PostingDate == postingdate && m.PeriodId == periodid && !m.IsFinal)
                .ToList();

            if (records.Count == 0)
            {
                TempData["Error"] = "No saved (non-final) records found for this date.";
            }
            else
            {
                foreach (var record in records)
                {
                    record.IsFinal = true;
                }
                db.SaveChanges();
                TempData["Message"] = records.Count + " department record(s) marked as final for " + postingdate.ToShortDateString() + ".";
            }
            return RedirectToAction("Details", new { date = postingdate.ToString("yyyy-MM-dd") });
        }

        // Deletes a whole posting-date batch (all departments) and its fee lines, but only while
        // none of the records are final.
        [HttpPost]
        public ActionResult Delete(string date)
        {
            int periodid = Period.PeriodID;
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                TempData["Error"] = "Invalid date.";
                return RedirectToAction("Index");
            }
            postingdate = postingdate.Date;
            var records = db.DeferredIncome
                .Where(m => m.PostingDate == postingdate && m.PeriodId == periodid)
                .ToList();

            if (records.Count == 0)
            {
                TempData["Error"] = "No records found for this date.";
            }
            else if (records.Any(m => m.IsFinal))
            {
                TempData["Error"] = "Cannot delete: some records for this date are already final.";
            }
            else
            {
                var recordids = records.Select(m => m.Id).ToList();
                var fees = db.DeferredIncomeFee.Where(f => recordids.Contains(f.DeferredIncomeId));
                db.DeferredIncomeFee.RemoveRange(fees);
                db.DeferredIncome.RemoveRange(records);
                db.SaveChanges();
                TempData["Message"] = "Deleted deferred income for " + postingdate.ToShortDateString() + ".";
            }
            return RedirectToAction("Index");
        }

        // GL revenue account that recognized deferred income is credited to (one per department,
        // same code across departments; the department GL/project code distinguishes them).
        // TODO: set these to the institution's actual revenue account code and name.
        private const string RecognitionIncomeAcctNo = "I-REV";
        private const string RecognitionIncomeAcctName = "Tuition & Other Fees Income";
        // QNE equivalent of the revenue account; blank shows as "N/A" when the QNE coding is selected.
        private const string RecognitionIncomeQneCode = "";

        // ---- Revenue-recognition journal entry (PDF, per department) ----

        // code = "qne" uses each account's QNEAccountCode ("N/A" when none); anything else (default)
        // uses the internal AcctNo (+ ".SubAcctNo" when the fee has a sub-account).
        public ActionResult JournalEntry(string date, string code)
        {
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                return Content("Invalid date.");
            }
            bool useQne = string.Equals(code, "qne", StringComparison.OrdinalIgnoreCase);
            JournalEntryReportDTO model = BuildJournalEntry(postingdate, useQne);
            if (model == null)
            {
                return Content("No deferred income records found for " + postingdate.ToShortDateString() + ".");
            }

            ViewBag.PreparedBy = employee == null ? "" : employee.FullName;
            ViewBag.PrintedOn = DateTime.Now;

            return new Rotativa.ViewAsPdf("JournalEntry", model)
            {
                FileName = "DeferredIncome_JE_" + (useQne ? "QNE_" : "") + postingdate.ToString("dd-MMMM-yyyy") + ".pdf",
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageSize = Rotativa.Options.Size.A4,
                PageMargins = new Rotativa.Options.Margins(12, 12, 14, 12),
                CustomSwitches =
                    "--footer-center \"Page [page] of [topage]\" --footer-font-size 8 --footer-spacing 3"
            };
        }

        // Builds the recognition JE from the saved posting: per department, debit each deferred-income
        // account (fee's chart of account + sub-account) by the month's posted amount, and credit a
        // single revenue account for the department's total. Debits equal credits per department.
        // useQne: show each account's QNEAccountCode ("N/A" when none) instead of the internal AcctNo.
        private JournalEntryReportDTO BuildJournalEntry(DateTime postingdate, bool useQne)
        {
            int periodid = Period.PeriodID;
            DateTime pdate = postingdate.Date;

            var records = db.DeferredIncome.Where(m => m.PostingDate == pdate && m.PeriodId == periodid).ToList();
            if (records.Count == 0)
            {
                return null;
            }

            var recIds = records.Select(r => r.Id).ToList();
            var fees = db.DeferredIncomeFee.Where(f => recIds.Contains(f.DeferredIncomeId)).ToList();

            // Resolve each fee's chart of account + sub-account (with QNE codes) once.
            var feeIds = fees.Select(f => f.FeeId).Distinct().ToList();
            var feeAcct = db.Fee.Where(f => feeIds.Contains(f.FeeID))
                            .Select(f => new { f.FeeID, f.AcctID, f.SubAcctID }).ToList();

            var acctIds = feeAcct.Where(f => f.AcctID.HasValue).Select(f => f.AcctID.Value).Distinct().ToList();
            var accts = db.ChartOfAccounts.Where(c => acctIds.Contains(c.AcctID)).ToList()
                          .ToDictionary(c => c.AcctID, c => new { c.AcctNo, c.AcctName, c.QNEAccountCode });

            var subIds = feeAcct.Where(f => f.SubAcctID.HasValue).Select(f => f.SubAcctID.Value).Distinct().ToList();
            var subs = db.SubChartOfAccounts.Where(s => subIds.Contains(s.SubAcctID)).ToList()
                          .ToDictionary(s => s.SubAcctID, s => new { s.SubAcctNo, s.QNEAccountCode });

            var acctByFee = feeAcct.ToDictionary(f => f.FeeID, f => new { f.AcctID, f.SubAcctID });

            var deptIds = records.Select(r => r.AcademicDepartmentId).Distinct().ToList();
            var departments = db.AcademicDepartment.Where(d => deptIds.Contains(d.AcaDeptID)).ToList();
            var first = records.OrderByDescending(r => r.DateGenerated).First();

            var model = new JournalEntryReportDTO
            {
                PeriodName = Period.FullName,
                EducLevel = Period.EducationalLevel1 != null ? Period.EducationalLevel1.EducLevelName : "",
                AsOfDate = pdate,
                NthMonth = first.NthMonth,
                NoOfMonths = first.NoOfMonths,
                IsFinal = records.All(r => r.IsFinal),
                CodeSourceLabel = useQne ? "QNE account codes" : "Internal account codes",
                Description = BuildRecognitionDescription(pdate, first.NthMonth, first.NoOfMonths)
            };

            foreach (var department in departments.OrderBy(d => d.AcaDepartmentName))
            {
                var deptRecIds = records.Where(r => r.AcademicDepartmentId == department.AcaDeptID).Select(r => r.Id).ToList();

                // Group this department's posted amounts by deferred-income account + sub-account.
                var byAccount = fees.Where(f => deptRecIds.Contains(f.DeferredIncomeId))
                    .Select(f =>
                    {
                        int? acctId = null, subId = null;
                        if (acctByFee.ContainsKey(f.FeeId))
                        {
                            acctId = acctByFee[f.FeeId].AcctID;
                            subId = acctByFee[f.FeeId].SubAcctID;
                        }
                        string acctNo = null, acctName = null, acctQne = null, subNo = null, subQne = null;
                        if (acctId.HasValue && accts.ContainsKey(acctId.Value))
                        {
                            acctNo = accts[acctId.Value].AcctNo;
                            acctName = accts[acctId.Value].AcctName;
                            acctQne = accts[acctId.Value].QNEAccountCode;
                        }
                        if (subId.HasValue && subs.ContainsKey(subId.Value))
                        {
                            subNo = subs[subId.Value].SubAcctNo;
                            subQne = subs[subId.Value].QNEAccountCode;
                        }
                        return new { acctNo, acctName, acctQne, subNo, subQne, f.PostedAmount };
                    })
                    .GroupBy(x => new { x.acctNo, x.acctName, x.acctQne, x.subNo, x.subQne })
                    .Select(g => new { g.Key.acctNo, g.Key.acctName, g.Key.acctQne, g.Key.subNo, g.Key.subQne, Posted = g.Sum(x => x.PostedAmount) })
                    .Where(x => x.Posted != 0m)
                    .OrderBy(x => x.acctNo)
                    .ToList();

                if (byAccount.Count == 0)
                {
                    continue;
                }

                var jeDept = new JeDeptDTO
                {
                    Acronym = string.IsNullOrWhiteSpace(department.AcaAcronym) ? department.AcaDepartmentName : department.AcaAcronym,
                    DepartmentName = department.AcaDepartmentName,
                    GLCode = department.GLCode
                };

                decimal totalDebit = 0m;
                foreach (var a in byAccount)
                {
                    bool hasSub = !string.IsNullOrWhiteSpace(a.subNo);
                    string code;
                    if (useQne)
                    {
                        string mainQ = string.IsNullOrWhiteSpace(a.acctQne) ? "N/A" : a.acctQne;
                        code = hasSub ? mainQ + "." + (string.IsNullOrWhiteSpace(a.subQne) ? "N/A" : a.subQne) : mainQ;
                    }
                    else
                    {
                        code = string.IsNullOrWhiteSpace(a.acctNo) ? "(unmapped)" : a.acctNo;
                        if (hasSub) { code += "." + a.subNo; }
                    }

                    jeDept.Lines.Add(new JeLineDTO
                    {
                        AcctNo = code,
                        GLCode = department.GLCode,
                        AccountName = string.IsNullOrWhiteSpace(a.acctName) ? "(unmapped account)" : a.acctName,
                        Debit = a.Posted,
                        Credit = 0m
                    });
                    totalDebit += a.Posted;
                }

                // Single revenue credit line for the department's total.
                jeDept.Lines.Add(new JeLineDTO
                {
                    AcctNo = useQne ? (string.IsNullOrWhiteSpace(RecognitionIncomeQneCode) ? "N/A" : RecognitionIncomeQneCode) : RecognitionIncomeAcctNo,
                    GLCode = department.GLCode,
                    AccountName = RecognitionIncomeAcctName,
                    Debit = 0m,
                    Credit = totalDebit
                });

                jeDept.TotalDebit = totalDebit;
                jeDept.TotalCredit = totalDebit;
                model.Departments.Add(jeDept);
            }

            return model.Departments.Count == 0 ? null : model;
        }

        private string BuildRecognitionDescription(DateTime pdate, int nth, int noof)
        {
            DateTime start = new DateTime(pdate.Year, pdate.Month, 1);
            string range = start.ToString("MMMM") + " " + start.Day + "-" + pdate.Day + ", " + pdate.Year;
            return "To record revenue recognition for the period covered " + range +
                   " (" + nth + "/" + noof + "), " + Period.FullName + ".";
        }

        // ---- PDF (rendered from the same HTML as the Details screen, via Rotativa/wkhtmltopdf) ----

        public ActionResult Print(string date)
        {
            DateTime postingdate;
            if (!DateTime.TryParse(date, out postingdate))
            {
                return Content("Invalid date.");
            }
            DeferredIncomeQueryDTO model = BuildFromSaved(postingdate);
            if (model == null)
            {
                return Content("No deferred income records found for " + postingdate.ToShortDateString() + ".");
            }

            ViewBag.PreparedBy = employee == null ? "" : employee.FullName;
            ViewBag.PrintedOn = DateTime.Now;
            // Embed the seal as a data URI; wkhtmltopdf can't resolve root-relative image URLs.
            ViewBag.LogoDataUri = ImageDataUri("~/Images/letranseal.jpg");

            // Renders Views/DeferredIncome/Print.cshtml, which is built from the same model as the
            // Details screen (same columns, same Actual-then-Deferred grouping), so the two stay in sync.
            return new Rotativa.ViewAsPdf("Print", model)
            {
                FileName = "DeferredIncome_" + postingdate.ToString("dd-MMMM-yyyy") + ".pdf",
                PageOrientation = Rotativa.Options.Orientation.Landscape,
                PageSize = Rotativa.Options.Size.A4,
                PageMargins = new Rotativa.Options.Margins(10, 8, 14, 8),
                CustomSwitches =
                    "--footer-center \"Page [page] of [topage]\" --footer-font-size 8 --footer-spacing 3"
            };
        }

        // Reads an app image and returns it as a base64 data URI (or null if missing) so it can be
        // inlined into a Rotativa/wkhtmltopdf view, which cannot fetch root-relative image URLs.
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

        private static void TrySetParameter(ReportDocument report, string name, object value)
        {
            try { report.SetParameterValue(name, value); }
            catch { /* template may not define this parameter */ }
        }

        // Binds the report rows. Some Crystal runtime installs fail to replace a
        // dataset/object connection through ReportDocument.SetDataSource ("Failed to
        // load database information"), so this falls back to rebinding through the
        // RAS SDK, which replaces the connection the same way it was created.
        private static void BindReportRows<T>(ReportDocument report, List<T> rows)
        {
            try
            {
                report.SetDataSource(rows);
                return;
            }
            catch (Exception)
            {
                // fall through to the RAS route below
            }
            System.Data.DataTable table = new System.Data.DataTable(typeof(T).Name);
            System.Reflection.PropertyInfo[] props = typeof(T).GetProperties();
            foreach (System.Reflection.PropertyInfo prop in props)
            {
                table.Columns.Add(prop.Name, prop.PropertyType);
            }
            foreach (T row in rows)
            {
                System.Data.DataRow r = table.NewRow();
                foreach (System.Reflection.PropertyInfo prop in props)
                {
                    r[prop.Name] = prop.GetValue(row, null);
                }
                table.Rows.Add(r);
            }
            System.Data.DataSet ds = new System.Data.DataSet();
            ds.Tables.Add(table);
            report.ReportClientDocument.DatabaseController.SetDataSource(
                CrystalDecisions.ReportAppServer.DataSetConversion.DataSetConverter.Convert(ds),
                typeof(T).Name, typeof(T).Name);
        }

        // ---- Report row builders ----

        private List<DeferredIncomeCollegeReportRow> BuildCollegeReportRows(DeferredIncomeQueryDTO model)
        {
            List<DeferredIncomeCollegeReportRow> rows = new List<DeferredIncomeCollegeReportRow>();
            foreach (var category in model.Categories)
            {
                if (category.IsSectionHeader)
                {
                    rows.Add(new DeferredIncomeCollegeReportRow { RowType = "SectionHeader", Section = category.Name });
                }
                foreach (var row in category.Rows)
                {
                    rows.Add(CollegeRow(model, "Item", category.Name, row));
                }
                if (category.ShowSubtotal && category.Subtotal != null)
                {
                    rows.Add(CollegeRow(model, "Subtotal", category.Name, category.Subtotal));
                }
            }
            if (model.TotalAssessment != null)
            {
                rows.Add(CollegeRow(model, "GrandTotal", "", model.TotalAssessment));
            }
            return rows;
        }

        private DeferredIncomeCollegeReportRow CollegeRow(DeferredIncomeQueryDTO model, string rowtype, string section, DeferredIncomeRowDTO row)
        {
            Func<int, int> dept = i => i < model.Columns.Count ? model.Columns[i].AcademicDepartmentId : -1;
            return new DeferredIncomeCollegeReportRow
            {
                RowType = rowtype,
                Section = section,
                Description = rowtype == "Subtotal" ? "Subtotal" : row.Description,
                Amount1 = row.AmountFor(dept(0)),
                Amount2 = row.AmountFor(dept(1)),
                Amount3 = row.AmountFor(dept(2)),
                Amount4 = row.AmountFor(dept(3)),
                AmountTotal = row.TotalAmount,
                Posted1 = row.PostedFor(dept(0)),
                Posted2 = row.PostedFor(dept(1)),
                Posted3 = row.PostedFor(dept(2)),
                Posted4 = row.PostedFor(dept(3)),
                PostedTotal = row.TotalPosted
            };
        }

        private List<DeferredIncomeReportRow> BuildGenericReportRows(DeferredIncomeQueryDTO model)
        {
            List<DeferredIncomeReportRow> rows = new List<DeferredIncomeReportRow>();
            foreach (var dept in model.Departments)
            {
                // Consolidate a department's fees the same way the college matrix does, so a
                // department with many tuition fee IDs (per year level/curriculum) shows a
                // single "Tuition Fee" line instead of one identical "Tuition" row each, and
                // other fees that share a description are merged into one line.
                foreach (var category in dept.Fees.GroupBy(f => CategoryOf(f.FeeType)).OrderBy(g => CategoryRank(g.Key)))
                {
                    if (category.Key == "Tuition Fee")
                    {
                        rows.Add(new DeferredIncomeReportRow
                        {
                            Department = dept.DepartmentName,
                            Section = category.Key,
                            Description = "Tuition Fee",
                            Amount = category.Sum(f => f.Amount),
                            PostedAmount = category.Sum(f => f.PostedAmount)
                        });
                    }
                    else
                    {
                        foreach (var group in category.GroupBy(f => f.Description).OrderBy(g => g.Key))
                        {
                            rows.Add(new DeferredIncomeReportRow
                            {
                                Department = dept.DepartmentName,
                                Section = category.Key,
                                Description = group.Key,
                                Amount = group.Sum(f => f.Amount),
                                PostedAmount = group.Sum(f => f.PostedAmount)
                            });
                        }
                    }
                }
            }
            return rows;
        }

        // Fixed category order matching the Summary of Fees report and BuildMatrix.
        private static int CategoryRank(string category)
        {
            switch (category)
            {
                case "Tuition Fee": return 1;
                case "Miscellaneous Fees": return 2;
                case "Supplemental Fees": return 3;
                case "Various/Other Fees": return 4;
                case "Laboratory Fees": return 5;
                default: return 6;
            }
        }

        // ---- Shared helpers ----

        private void CarryTempMessages()
        {
            if (TempData["Message"] != null) { ViewBag.Message = TempData["Message"]; }
            if (TempData["Error"] != null) { ViewBag.Error = TempData["Error"]; }
        }

        private string ValidateMonths(int nthmonth, int noofmonths)
        {
            if (noofmonths < 1)
            {
                return "No. of months must be at least 1.";
            }
            if (nthmonth < 1 || nthmonth > noofmonths)
            {
                return "Nth month must be between 1 and the no. of months.";
            }
            return null;
        }

        // Same population as FinanceReports/FeesSummary (validated enrollees as of the given
        // date, student status < 6), but grouped by academic department and fee so the
        // figures can be persisted to DeferredIncome/DeferredIncomeFee.
        private DeferredIncomeQueryDTO BuildSummary(DateTime asofdate, int nthmonth, int noofmonths)
        {
            int periodid = Period.PeriodID;
            DateTime asofd = asofdate.Date;
            var raw = db.Assessment
                .Where(a => a.Student_Section.Section.PeriodID == periodid
                    && a.Student_Section.ValidationDate != null
                    && DbFunctions.TruncateTime(a.Student_Section.ValidationDate) <= asofd
                    && a.Student_Section.StudentStatus != null
                    && a.Student_Section.StudentStatus < 6
                    && a.FeeID != null
                    && a.Student_Section.Curriculum.AcaDeptID != null)
                .GroupBy(a => new { AcaDeptId = a.Student_Section.Curriculum.AcaDeptID.Value, FeeId = a.FeeID.Value })
                .Select(g => new
                {
                    g.Key.AcaDeptId,
                    g.Key.FeeId,
                    Description = g.Max(x => x.Description),
                    FeeType = g.Max(x => x.FeeType),
                    Amount = g.Sum(x => x.Amount)
                })
                .ToList();

            DeferredIncomeQueryDTO model = new DeferredIncomeQueryDTO
            {
                PeriodId = periodid,
                PeriodName = Period.EducationalLevel1.EducLevelName + " - " + Period.FullName,
                AsOfDate = asofdate,
                NthMonth = nthmonth,
                NoOfMonths = noofmonths
            };

            DateTime postingdate = asofdate.Date;
            var deptids = raw.Select(m => m.AcaDeptId).Distinct().ToList();
            var departments = db.AcademicDepartment.Where(m => deptids.Contains(m.AcaDeptID)).ToList();
            var existingrecords = db.DeferredIncome.Where(m => m.PostingDate == postingdate && m.PeriodId == periodid).ToList();

            foreach (var department in departments.OrderBy(m => m.AcaDepartmentName))
            {
                var existing = existingrecords.FirstOrDefault(m => m.AcademicDepartmentId == department.AcaDeptID);
                DeferredIncomeDepartmentDTO dept = new DeferredIncomeDepartmentDTO
                {
                    AcademicDepartmentId = department.AcaDeptID,
                    DepartmentName = department.AcaDepartmentName,
                    Acronym = string.IsNullOrWhiteSpace(department.AcaAcronym) ? department.AcaDepartmentName : department.AcaAcronym,
                    HasExistingRecord = existing != null,
                    IsFinal = existing != null && existing.IsFinal
                };
                foreach (var item in raw.Where(m => m.AcaDeptId == department.AcaDeptID).OrderBy(m => m.FeeType).ThenBy(m => m.Description))
                {
                    decimal amount = item.Amount.HasValue ? (decimal)item.Amount.Value : 0;
                    // Flat per-month: recognize this month's own 1/NoOfMonths of the amount as of this
                    // date. No cumulative catch-up, so a later/backdated amount is never back-attributed
                    // to an earlier (already-posted) month.
                    decimal postedThisMonth = Math.Round(amount / noofmonths, 2);
                    dept.Fees.Add(new DeferredIncomeFeeDTO
                    {
                        FeeId = item.FeeId,
                        Description = item.Description,
                        FeeType = item.FeeType,
                        Amount = amount,
                        PostedAmount = postedThisMonth
                    });
                }
                model.Departments.Add(dept);
            }

            BuildMatrix(model);
            return model;
        }

        // Rebuilds the same view model from previously saved DeferredIncome/DeferredIncomeFee rows,
        // recovering each fee's category/name from the Fee master.
        private DeferredIncomeQueryDTO BuildFromSaved(DateTime postingdate)
        {
            int periodid = Period.PeriodID;
            DateTime pdate = postingdate.Date;
            var records = db.DeferredIncome
                .Where(m => m.PostingDate == pdate && m.PeriodId == periodid)
                .ToList();
            if (records.Count == 0)
            {
                return null;
            }

            var first = records.OrderByDescending(m => m.DateGenerated).First();
            DeferredIncomeQueryDTO model = new DeferredIncomeQueryDTO
            {
                PeriodId = periodid,
                PeriodName = Period.EducationalLevel1.EducLevelName + " - " + Period.FullName,
                AsOfDate = pdate,
                NthMonth = first.NthMonth,
                NoOfMonths = first.NoOfMonths
            };

            var deptids = records.Select(m => m.AcademicDepartmentId).Distinct().ToList();
            var departments = db.AcademicDepartment.Where(m => deptids.Contains(m.AcaDeptID)).ToList();
            var recordids = records.Select(m => m.Id).ToList();
            var fees = db.DeferredIncomeFee.Where(f => recordids.Contains(f.DeferredIncomeId)).ToList();

            // Recover each fee's name/type from the period assessments (same source Generate uses),
            // so saved rows show the real fee name rather than a "Fee <id>" placeholder.
            var feeids = fees.Select(f => f.FeeId).Distinct().ToList();
            var feeMeta = db.Assessment
                .Where(a => a.FeeID != null && feeids.Contains(a.FeeID.Value) && a.Student_Section.Section.PeriodID == periodid)
                .GroupBy(a => a.FeeID.Value)
                .Select(g => new { FeeId = g.Key, Description = g.Max(x => x.Description), FeeType = g.Max(x => x.FeeType) })
                .ToList();
            var descByFee = feeMeta.ToDictionary(x => x.FeeId, x => x.Description);
            var typeByFee = feeMeta.ToDictionary(x => x.FeeId, x => x.FeeType);

            foreach (var record in records)
            {
                var department = departments.FirstOrDefault(d => d.AcaDeptID == record.AcademicDepartmentId);
                DeferredIncomeDepartmentDTO dept = new DeferredIncomeDepartmentDTO
                {
                    AcademicDepartmentId = record.AcademicDepartmentId,
                    DepartmentName = department != null ? department.AcaDepartmentName : ("Dept " + record.AcademicDepartmentId),
                    Acronym = department != null && !string.IsNullOrWhiteSpace(department.AcaAcronym) ? department.AcaAcronym : (department != null ? department.AcaDepartmentName : ("Dept " + record.AcademicDepartmentId)),
                    HasExistingRecord = true,
                    IsFinal = record.IsFinal
                };
                foreach (var fee in fees.Where(f => f.DeferredIncomeId == record.Id))
                {
                    string desc;
                    string ftype;
                    descByFee.TryGetValue(fee.FeeId, out desc);
                    typeByFee.TryGetValue(fee.FeeId, out ftype);
                    if (string.IsNullOrWhiteSpace(desc))
                    {
                        desc = fee.Fee != null && fee.Fee.FeeName != null ? fee.Fee.FeeName.FeeName1 : ("Fee " + fee.FeeId);
                    }
                    if (string.IsNullOrWhiteSpace(ftype))
                    {
                        ftype = fee.Fee != null ? fee.Fee.FeeCategory : "O";
                    }
                    dept.Fees.Add(new DeferredIncomeFeeDTO
                    {
                        FeeId = fee.FeeId,
                        Description = desc,
                        FeeType = ftype,
                        Amount = fee.Amount,
                        PostedAmount = fee.PostedAmount
                    });
                }
                model.Departments.Add(dept);
            }

            model.Departments = model.Departments.OrderBy(d => d.DepartmentName).ToList();
            BuildMatrix(model);
            return model;
        }

        // GL account view built on top of the saved model, joining each fee to its Chart of Account
        // and QNE account code. Unmapped fees are left null so the view can flag them.
        private DeferredIncomeGLDTO BuildGL(DateTime postingdate)
        {
            DeferredIncomeQueryDTO model = BuildFromSaved(postingdate);
            if (model == null)
            {
                return null;
            }

            var feeids = model.Departments.SelectMany(d => d.Fees).Select(f => f.FeeId).Distinct().ToList();
            var glLookup = db.Fee
                .Where(f => feeids.Contains(f.FeeID))
                .Select(f => new
                {
                    f.FeeID,
                    Coa = f.ChartOfAccounts.AcctNo + "-" + f.ChartOfAccounts.AcctName,
                    Qne = f.QNEGLAccount.AccountCode,
                    QneRaw = f.QneAccountCode
                })
                .ToList()
                .ToDictionary(x => x.FeeID, x => x);

            DeferredIncomeGLDTO gl = new DeferredIncomeGLDTO
            {
                PeriodName = model.PeriodName,
                AsOfDate = model.AsOfDate,
                NthMonth = model.NthMonth,
                NoOfMonths = model.NoOfMonths,
                AllFinal = model.Columns.All(c => c.IsFinal)
            };

            foreach (var dept in model.Departments)
            {
                DeferredIncomeGLDeptDTO gdept = new DeferredIncomeGLDeptDTO
                {
                    DepartmentName = dept.DepartmentName,
                    Acronym = dept.Acronym,
                    IsFinal = dept.IsFinal
                };
                foreach (var fee in dept.Fees.OrderBy(f => f.Description))
                {
                    string coa = null;
                    string qne = null;
                    if (glLookup.ContainsKey(fee.FeeId))
                    {
                        var g = glLookup[fee.FeeId];
                        coa = string.IsNullOrWhiteSpace(g.Coa) || g.Coa == "-" ? null : g.Coa;
                        qne = !string.IsNullOrWhiteSpace(g.Qne) ? g.Qne : (string.IsNullOrWhiteSpace(g.QneRaw) ? null : g.QneRaw);
                    }
                    gdept.Fees.Add(new DeferredIncomeGLRowDTO
                    {
                        FeeName = fee.Description,
                        ChartOfAccount = coa,
                        QNEAccountCode = qne,
                        Actual = fee.Amount,
                        Deferred = fee.PostedAmount
                    });
                }
                gl.Departments.Add(gdept);
            }
            return gl;
        }

        // Fee-category grouping used by the Summary of Fees report.
        private static string CategoryOf(string feetype)
        {
            switch (feetype)
            {
                case "T": return "Tuition Fee";
                case "M": return "Miscellaneous Fees";
                case "S":
                case "V": return "Supplemental Fees";
                case "L":
                case "A": return "Laboratory Fees";
                default: return "Various/Other Fees";
            }
        }

        // Reshapes the per-department figures into the FeesSummary-style matrix:
        // departments as columns, fees consolidated by name as rows, grouped by category.
        private void BuildMatrix(DeferredIncomeQueryDTO model)
        {
            foreach (var dept in model.Departments)
            {
                model.Columns.Add(new DeferredIncomeColumnDTO
                {
                    AcademicDepartmentId = dept.AcademicDepartmentId,
                    Header = dept.Acronym,
                    HasExistingRecord = dept.HasExistingRecord,
                    IsFinal = dept.IsFinal
                });
            }

            // Fixed category order matching the Summary of Fees report.
            string[] categoryOrder = { "Tuition Fee", "Miscellaneous Fees", "Supplemental Fees", "Various/Other Fees", "Laboratory Fees" };
            var grandTotal = new DeferredIncomeRowDTO { Description = "Total Assessment Fees" };

            foreach (var categoryName in categoryOrder)
            {
                var categoryFees = model.Departments
                    .SelectMany(d => d.Fees.Select(f => new { d.AcademicDepartmentId, Fee = f }))
                    .Where(x => CategoryOf(x.Fee.FeeType) == categoryName)
                    .ToList();

                if (categoryFees.Count == 0)
                {
                    continue;
                }

                bool isTuition = categoryName == "Tuition Fee";
                DeferredIncomeCategoryDTO category = new DeferredIncomeCategoryDTO
                {
                    Name = categoryName,
                    // Tuition is a single consolidated line; the rest get a banner + subtotal.
                    IsSectionHeader = !isTuition,
                    ShowSubtotal = !isTuition
                };

                if (isTuition)
                {
                    // Like the Summary of Fees report, all tuition fees roll up into one
                    // "Tuition Fee" line regardless of their individual descriptions.
                    DeferredIncomeRowDTO row = new DeferredIncomeRowDTO { Description = "Tuition Fee" };
                    foreach (var entry in categoryFees)
                    {
                        row.Add(entry.AcademicDepartmentId, entry.Fee.Amount, entry.Fee.PostedAmount);
                        grandTotal.Add(entry.AcademicDepartmentId, entry.Fee.Amount, entry.Fee.PostedAmount);
                    }
                    category.Rows.Add(row);
                }
                else
                {
                    var subtotal = new DeferredIncomeRowDTO { Description = "Subtotal" };
                    foreach (var group in categoryFees.GroupBy(x => x.Fee.Description).OrderBy(g => g.Key))
                    {
                        DeferredIncomeRowDTO row = new DeferredIncomeRowDTO { Description = group.Key };
                        foreach (var entry in group)
                        {
                            row.Add(entry.AcademicDepartmentId, entry.Fee.Amount, entry.Fee.PostedAmount);
                            subtotal.Add(entry.AcademicDepartmentId, entry.Fee.Amount, entry.Fee.PostedAmount);
                            grandTotal.Add(entry.AcademicDepartmentId, entry.Fee.Amount, entry.Fee.PostedAmount);
                        }
                        category.Rows.Add(row);
                    }
                    category.Subtotal = subtotal;
                }
                model.Categories.Add(category);
            }

            model.TotalAssessment = grandTotal;
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
