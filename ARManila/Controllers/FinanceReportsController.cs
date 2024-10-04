using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
using ARManila.Models.ReportsDTO;
using ARManila.Reports;
using CrystalDecisions.CrystalReports.Engine;
using Newtonsoft.Json;

namespace ARManila.Controllers
{

    public class FinanceReportsController : BaseController
    {
        Uri baseAddress = new Uri("https://api.letran.edu.ph");
        HttpClient client;
        private LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        private Employee employee = new Employee();
        protected Period Period { get; private set; }
        public FinanceReportsController()
        {
            client = new HttpClient();
            client.BaseAddress = baseAddress;
        }
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            InitializeDatabaseContext();
        }

        private void InitializeDatabaseContext()
        {
            db.Database.CommandTimeout = 300;
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            Period = db.Period.Find(periodid);
            if (Period == null)
            {
                throw new Exception("Invalid period id.");
            }
            employee = db.Employee.FirstOrDefault(m => m.EmployeeNo == User.Identity.Name);
        }

        #region ARSetupSummary

        public ActionResult DiscountSummaryConsolidated()
        {
            return View();
        }
        [HttpPost]
        public ActionResult DiscountSummaryConsolidated(DateTime startdate, DateTime enddate, int viewas)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            Period = db.Period.Find(periodid);
            enddate = enddate.AddDays(1);
            var studentswithdiscount = db.GetConsolidatedDiscountDetails(startdate, enddate, periodid);
            List<DiscountSummaryDTO> summaries = new List<DiscountSummaryDTO>();
            foreach (var item in studentswithdiscount)
            {
                if (item.Total > 0)
                {
                    var student = db.Student.Find(item.StudentID);
                    var summary = new DiscountSummaryDTO();
                    summary.AcademicDepartment = item.AcaAcronym;
                    summary.AccountName = item.AcctName;
                    summary.AccountNo = item.AcctNo;
                    summary.Category = item.Category;
                    summary.EducationalLevel = item.EducLevelName;
                    summary.DiscountPercentNonTuition = (decimal)(item.PMisc ?? 0);
                    summary.DiscountPercentTotal = (decimal)(item.PTotal ?? 0);
                    summary.DiscountPercentTuition = (decimal)(item.PTuition ?? 0);
                    summary.StartDate = startdate.ToShortDateString();
                    summary.EndDate = enddate.ToShortDateString();
                    summary.GradeYear = item.GradeYear.ToString();
                    summary.PeriodFullName = Period.FullName;
                    summary.ProgramCode = item.ProgramCode;
                    summary.StudentName = student.FullName256.Length > 0 ? student.FullName256 : student.FullName;
                    summary.StudentNo = item.StudentNo;
                    summary.DiscountT = (decimal)(item.Discount ?? 0);
                    summary.DiscountA = (decimal)(item.discountA ?? 0);
                    summary.DiscountL = (decimal)(item.discountL ?? 0);
                    summary.DiscountM = (decimal)(item.discountM);
                    summary.DiscountS = (decimal)(item.discountS);
                    summary.DiscountV = (decimal)(item.discountV);
                    summary.DiscountO = (decimal)(item.discountO ?? 0);
                    summary.DiscountTotal = (decimal)(item.Total ?? 0);
                    summary.Source = item.WhenDiscounted;
                    summaries.Add(summary);
                }
            }
            return View(summaries);
        }
        public ActionResult ARSetupSummary()
        {
            return View();
        }
        public ActionResult ARDetailsWithBalance(List<Period> periodids, DateTime asofdate, Dictionary<int, List<Period>> consolidatedperiodids)
        {
            ARSetupSummary summary = new ARSetupSummary();
            GetARSummaryDataViaArTrail(periodids, asofdate, summary, consolidatedperiodids);
            return View(summary);
        }

        [HttpPost]
        public ActionResult ARSetupSummary(DateTime asofdate, int viewas, string isconsolidated, string isschoolyear)
        {
            ARSetupSummary summary = new ARSetupSummary();
            summary.Header = Period.EducationalLevel1.EducLevelName + " Fees Setup";
            summary.AsOfDate = asofdate.ToString("MMMM dd, yyyy");
            summary.PreparedBy = employee.FullName;
            summary.Subheader1 = Period.SchoolYear.SchoolYearName;
            var schoolyearid = Period.SchoolYearID;
            var periodids = new List<Period>();
            Dictionary<int, List<Period>> consolidatedperiodids = new Dictionary<int, List<Period>>();
            ViewBag.asofdate = asofdate.ToString("yyyy-MM-dd");
            if (isconsolidated != null && isconsolidated.Equals("on"))
            {
                summary.Subheader2 = "1st Term";
                summary.Subheader3 = "2nd Term";
                summary.Subheader4 = "3rd Term";
                var firstsetperiodids = db.Period.Where(m => m.EducLevelID < 4 && m.SchoolYearID == schoolyearid).ToList();
                var college1 = db.Period.Where(m => m.EducLevelID == 4 && m.SchoolYearID == schoolyearid).OrderBy(m => m.PeriodID).FirstOrDefault();
                var masteral1 = db.Period.Where(m => m.EducLevelID == 5 && m.SchoolYearID == schoolyearid).OrderBy(m => m.PeriodID).FirstOrDefault();
                var doctoral1 = db.Period.Where(m => m.EducLevelID == 6 && m.SchoolYearID == schoolyearid).OrderBy(m => m.PeriodID).FirstOrDefault();
                firstsetperiodids.Add(college1);
                firstsetperiodids.Add(masteral1);
                firstsetperiodids.Add(doctoral1);
                consolidatedperiodids.Add(0, firstsetperiodids);
                var college2 = db.Period.Where(m => m.EducLevelID == 4 && m.SchoolYearID == schoolyearid).OrderBy(m => m.PeriodID).Skip(1).FirstOrDefault();
                var masteral2 = db.Period.Where(m => m.EducLevelID == 5 && m.SchoolYearID == schoolyearid).OrderBy(m => m.PeriodID).Skip(1).FirstOrDefault();
                var doctoral2 = db.Period.Where(m => m.EducLevelID == 6 && m.SchoolYearID == schoolyearid).OrderBy(m => m.PeriodID).Skip(1).FirstOrDefault();
                List<Period> secondsetperiodids = new List<Period>();
                secondsetperiodids.Add(college2);
                secondsetperiodids.Add(masteral2);
                secondsetperiodids.Add(doctoral2);
                consolidatedperiodids.Add(1, secondsetperiodids);
                var college3 = db.Period.Where(m => m.EducLevelID == 4 && m.SchoolYearID == schoolyearid).OrderBy(m => m.PeriodID).Skip(2).FirstOrDefault();
                var masteral3 = db.Period.Where(m => m.EducLevelID == 5 && m.SchoolYearID == schoolyearid).OrderBy(m => m.PeriodID).Skip(2).FirstOrDefault();
                var doctoral3 = db.Period.Where(m => m.EducLevelID == 6 && m.SchoolYearID == schoolyearid).OrderBy(m => m.PeriodID).Skip(2).FirstOrDefault();
                List<Period> thirdsetperiodids = new List<Period>();
                thirdsetperiodids.Add(college3);
                thirdsetperiodids.Add(masteral3);
                thirdsetperiodids.Add(doctoral3);
                consolidatedperiodids.Add(2, thirdsetperiodids);
            }
            else if (isschoolyear != null && isschoolyear.Equals("on"))
            {
                periodids = db.Period.Where(m => m.EducLevelID == Period.EducLevelID && m.SchoolYearID == schoolyearid).ToList();
                switch (Period.EducLevelID)
                {
                    case 1:
                        summary.Subheader2 = "Elem";
                        summary.Subheader3 = "Elem-HSP";
                        break;
                    case 2:
                        summary.Subheader2 = "JHS";
                        summary.Subheader3 = "JHS-HSP";
                        break;
                    case 3:
                        summary.Subheader2 = "1st Semester";
                        summary.Subheader3 = Period.Period1;
                        break;
                    case 4:
                    case 5:
                    case 6:
                        for (int i = 0; i < periodids.Count; i++)
                        {
                            if (i == 0)
                                summary.Subheader2 = periodids[0].Period1;
                            if (i == 1)
                                summary.Subheader3 = periodids[1].Period1;
                            if (i == 2)
                                summary.Subheader4 = periodids[2].Period1;
                        }
                        break;

                }
            }
            else
            {
                switch (Period.EducLevelID)
                {
                    case 1:
                        summary.Subheader2 = "Elem";
                        break;
                    case 2:
                        summary.Subheader2 = "JHS";
                        break;
                    case 3:
                        summary.Subheader2 = "1st Semester";
                        break;
                    case 4:
                    case 5:
                    case 6:
                        summary.Subheader2 = Period.Period1;
                        break;

                }

                periodids.Add(Period);
            }

            //GetARSummaryData(periodids, asofdate, summary, consolidatedperiodids);            
            GetARSummaryDataViaArTrail(periodids, asofdate, summary, consolidatedperiodids);

            if (viewas == 1)
            {
                return View(summary);
            }
            else
            {
                ReportDocument reportDocument = new ReportDocument();
                if (isconsolidated != null && isconsolidated.Equals("on"))
                {
                    summary.Header = "Consolidated AR Fees Setup";
                    reportDocument = new ARSetupSummaryReportConsolidated();
                    List<ARSetupSummaryConsolidatedItem> consolidateitems = new List<ARSetupSummaryConsolidatedItem>();
                    foreach (var item in summary.ARSetupSummaryConsolidatedItems)
                    {
                        consolidateitems.Add(new ARSetupSummaryConsolidatedItem
                        {
                            Item = item.Value.Item,
                            ARBalance = item.Value.ARBalance,
                            ARFeesSetup = item.Value.ARFeesSetup,
                            Order = item.Value.Item.Contains("Basic") ? 1 : (item.Value.Item.Contains("SHS") ? 2 : (item.Value.Item.Contains("Coll") ? 3 : (item.Value.Item.Contains("gs") ? 4 : 5)))
                        });
                    }
                    reportDocument.Subreports["consolidated"].SetDataSource(consolidateitems);
                    reportDocument.Subreports["chart"].SetDataSource(consolidateitems);

                }
                else
                {
                    if (summary.Tuition.Amount2 == 0)
                    {
                        reportDocument = new ARSetupSummaryReportSHS();
                    }
                    else if (summary.Tuition.Amount3 == 0)
                    {
                        reportDocument = new ARSetupSummaryReportBasicEd();
                    }
                    else if (summary.Tuition.Amount4 == 0)
                    {
                        reportDocument = new ARSetupSummaryReportCollege();
                    }
                }
                List<ARSetupSummaryItem> tuitions = new List<ARSetupSummaryItem>();
                tuitions.Add(summary.Tuition);
                reportDocument.Subreports["tuitionfees"].SetDataSource(tuitions);
                List<ARSetupSummaryItem> miscfees = new List<ARSetupSummaryItem>();
                miscfees.Add(summary.Miscellaneous);
                reportDocument.Subreports["miscfees"].SetDataSource(miscfees);
                List<ARSetupSummaryItem> labfees = new List<ARSetupSummaryItem>();
                labfees.Add(summary.Laboratory);
                reportDocument.Subreports["labfees"].SetDataSource(labfees);
                List<ARSetupSummaryItem> variousfees = new List<ARSetupSummaryItem>();
                variousfees.Add(summary.Various);
                reportDocument.Subreports["variousfees"].SetDataSource(variousfees);
                List<ARSetupSummaryItem> totalfees = new List<ARSetupSummaryItem>();
                totalfees.Add(summary.TotalFees);
                reportDocument.Subreports["totalfees"].SetDataSource(totalfees);
                List<ARSetupSummaryItem> totalstudents = new List<ARSetupSummaryItem>();
                totalstudents.Add(summary.TotalStudent);
                reportDocument.Subreports["totalstudents"].SetDataSource(totalstudents);
                List<ARSetupSummaryItem> beginning = new List<ARSetupSummaryItem>();
                beginning.Add(summary.BeginningBalance);
                reportDocument.Subreports["beginning"].SetDataSource(beginning);
                List<ARSetupSummaryItem> collections = new List<ARSetupSummaryItem>();
                collections.Add(summary.Collection);
                reportDocument.Subreports["collections"].SetDataSource(collections);
                List<ARSetupSummaryItem> adjustments = new List<ARSetupSummaryItem>();
                adjustments.Add(summary.Adjustment);
                reportDocument.Subreports["adjustments"].SetDataSource(adjustments);
                List<ARSetupSummaryItem> discounts = new List<ARSetupSummaryItem>();
                discounts.Add(summary.Discount);
                reportDocument.Subreports["discounts"].SetDataSource(discounts);
                List<ARSetupSummaryItem> vouchers = new List<ARSetupSummaryItem>();
                vouchers.Add(summary.Voucher);
                reportDocument.Subreports["vouchers"].SetDataSource(vouchers);
                if (!(vouchers.Count > 0 && vouchers.First().Amount1 > 0))
                    reportDocument.ReportDefinition.Sections[11].SectionFormat.EnableSuppress = true;
                List<ARSetupSummaryItem> arbalance = new List<ARSetupSummaryItem>();
                arbalance.Add(summary.ARBalance);
                reportDocument.Subreports["arbalance"].SetDataSource(arbalance);
                List<ARSetupSummaryItem> arbalancetotalstudents = new List<ARSetupSummaryItem>();
                arbalancetotalstudents.Add(summary.TotalStudentsWithBalance);
                reportDocument.Subreports["arbalancetotalstudents"].SetDataSource(arbalancetotalstudents);
                List<ARSetupSummaryItem> collectionpercent = new List<ARSetupSummaryItem>();
                collectionpercent.Add(summary.CollectionPercent2);
                reportDocument.Subreports["collectionpercent"].SetDataSource(collectionpercent);
                List<ARSetupSummaryItem> arbalancepercent = new List<ARSetupSummaryItem>();
                arbalancepercent.Add(summary.ARBalancePercent2);
                reportDocument.Subreports["arbalancepercent"].SetDataSource(arbalancepercent);
                List<ARSetupSummaryItem> collectionpercent2 = new List<ARSetupSummaryItem>();
                collectionpercent2.Add(summary.CollectionPercent1);
                reportDocument.Subreports["collectionpercent2"].SetDataSource(collectionpercent2);
                List<ARSetupSummaryItem> arbalancepercent2 = new List<ARSetupSummaryItem>();
                arbalancepercent2.Add(summary.ARBalancePercent1);
                reportDocument.Subreports["arbalancepercent2"].SetDataSource(arbalancepercent2);
                List<ARSetupSummary> feeSummaries = new List<ARSetupSummary>();


                feeSummaries.Add(summary);
                reportDocument.SetDataSource(feeSummaries);
                return ExportType(viewas - 1, "ARSummary_" + asofdate.Date.ToString("dd MMMM yyyy"), reportDocument);
            }
        }

        private void GetARSummaryDataViaArTrail(List<Period> periodids, DateTime asofdate, ARSetupSummary summary, Dictionary<int, List<Period>> consolidatedperiodids)
        {
            summary.Tuition.Item = "Tuition";
            summary.Miscellaneous.Item = "Miscellaneous";
            summary.Various.Item = "Various";
            summary.Laboratory.Item = "Laboratory";
            summary.TotalFees.Item = "Total fees set-up";
            summary.BeginningBalance.Item = "Beginning Balances";
            summary.Collection.Item = "Collection";
            summary.Adjustment.Item = "Adjustments";
            summary.Voucher.Item = "Vouchers";
            summary.Discount.Item = "Discounts";
            summary.ARBalance.Item = "A/R Balance";
            summary.TotalStudentsWithBalance.Item = "";
            summary.CollectionPercent1.Item = "Collections in %age";
            summary.ARBalancePercent1.Item = "A/R Balance in %age";
            var noofstudentswithbalancerunninglist = new List<StudentCount>();
            if (consolidatedperiodids.Count() > 0)
            {
                summary.ARSetupSummaryConsolidatedItems.Add(1, new ARSetupSummaryConsolidatedItem { Item = "Basic Ed", ARFeesSetup = 0, ARBalance = 0 });
                summary.ARSetupSummaryConsolidatedItems.Add(2, new ARSetupSummaryConsolidatedItem { Item = "SHS", ARFeesSetup = 0, ARBalance = 0 });
                summary.ARSetupSummaryConsolidatedItems.Add(3, new ARSetupSummaryConsolidatedItem { Item = "College", ARFeesSetup = 0, ARBalance = 0 });
                summary.ARSetupSummaryConsolidatedItems.Add(4, new ARSetupSummaryConsolidatedItem { Item = "GS", ARFeesSetup = 0, ARBalance = 0 });
                List<StudentCount> studentnos = new List<StudentCount>();
                List<StudentCount> studentnoswithbalance = new List<StudentCount>();
                foreach (var consolidatedperiod in consolidatedperiodids)
                {
                    switch (consolidatedperiod.Key)
                    {
                        case 0:
                            summary.Periods.Add("1st Term");
                            break;
                        case 1:
                            summary.Periods.Add("2nd Term");
                            break;
                        case 2:
                            summary.Periods.Add("3rd Term");
                            break;
                        case 3:
                            summary.Periods.Add("4th Term");
                            break;
                    }

                    foreach (var item in consolidatedperiod.Value)
                    {
                        var artrails = db.ArTrail2024(item.PeriodID, asofdate).ToList();
                        List<StudentCount> templist = artrails.Select(m => new StudentCount { EducLevelId = item.EducLevelID.Value, StudentNo = m.StudentNo }).ToList();
                        studentnos.AddRange(templist);
                        List<StudentCount> templistnobalance = artrails.Where(m => m.ArBalance <= 1).Select(m => new StudentCount { EducLevelId = item.EducLevelID.Value, StudentNo = m.StudentNo }).ToList();
                        List<StudentCount> templistbalance = artrails.Where(m => m.ArBalance > 1).Select(m => new StudentCount { EducLevelId = item.EducLevelID.Value, StudentNo = m.StudentNo }).ToList();
                        studentnoswithbalance.AddRange(templistbalance);
                        studentnoswithbalance = studentnoswithbalance.Where(m => !templistnobalance.Any(p => p.StudentNo == m.StudentNo)).ToList();
                        SumARSetupData(summary, consolidatedperiod.Key, artrails);

                        switch (item.EducLevelID.Value)
                        {
                            case 1:
                            case 2:
                                var consolidateditembasiced = summary.ARSetupSummaryConsolidatedItems.FirstOrDefault(m => m.Key == 1);
                                ConsolidatedSummary(artrails, consolidateditembasiced, false);
                                break;
                            case 3:
                                var consolidateditemshs = summary.ARSetupSummaryConsolidatedItems.FirstOrDefault(m => m.Key == 2);
                                ConsolidatedSummary(artrails, consolidateditemshs, false);
                                break;
                            case 4:
                                summary.BeginningBalance.IsBeginningBalance = true;
                                summary.ARBalance.IsARTotalUsingBeginningBalance = true;
                                var consolidateditemcollege = summary.ARSetupSummaryConsolidatedItems.FirstOrDefault(m => m.Key == 3);
                                consolidateditemcollege.Value.IsCollegeOrGs = true;
                                ConsolidatedSummary(artrails, consolidateditemcollege, consolidatedperiod.Key == 0 ? false : true);
                                break;
                            case 5:
                            case 6:
                                summary.BeginningBalance.IsBeginningBalance = true;
                                summary.ARBalance.IsARTotalUsingBeginningBalance = true;
                                var consolidateditemgs = summary.ARSetupSummaryConsolidatedItems.FirstOrDefault(m => m.Key == 4);
                                consolidateditemgs.Value.IsCollegeOrGs = true;
                                ConsolidatedSummary(artrails, consolidateditemgs, consolidatedperiod.Key == 0 ? false : true);
                                break;
                        }
                    }

                }
                var arsetuptotal = summary.ARSetupSummaryConsolidatedItems.Sum(m => m.Value.ARFeesSetup);
                var arbalancetotal = summary.ARSetupSummaryConsolidatedItems.Sum(m => m.Value.ARBalance);
                var enrolledstudents = studentnos.Select(student => student.StudentNo).Distinct();
                summary.TotalStudent.TotalRW = enrolledstudents.Count();
                summary.TotalStudentsWithBalance.TotalRW = studentnoswithbalance.Select(student => student.StudentNo).Distinct().Count();
                summary.ARBalancePercent1.TotalRW = summary.TotalStudent.TotalRW == 0 ? 0 : summary.TotalStudentsWithBalance.TotalRW / summary.TotalStudent.TotalRW;
                summary.CollectionPercent1.TotalRW = summary.TotalStudent.TotalRW == 0 ? 0 : 1 - summary.TotalStudentsWithBalance.TotalRW / summary.TotalStudent.TotalRW;
                summary.ARSetupSummaryConsolidatedItems.Add(5, new ARSetupSummaryConsolidatedItem { Item = "Total", ARFeesSetup = arsetuptotal, ARBalance = arbalancetotal });
                summary.CollectionPercent2.TotalRW = summary.TotalFees.Total == 0 ? 0 : (1 - summary.ARBalance.Total / summary.TotalFees.Total);
                summary.ARBalancePercent2.TotalRW = summary.TotalFees.Total == 0 ? 0 : (summary.ARBalance.Total / summary.TotalFees.Total);
            }
            else
            {
                int i = 0;
                List<StudentCount> studentnosnobalance = new List<StudentCount>();
                List<StudentCount> studentnos = new List<StudentCount>();
                List<StudentCount> studentnoswithbalance = new List<StudentCount>();
                foreach (var item in periodids)
                {
                    if (item.EducLevelID >= 4)
                    {
                        summary.BeginningBalance.IsBeginningBalance = true;
                        summary.ARBalance.IsARTotalUsingBeginningBalance = true;
                    }
                    var artrails = db.ArTrail2024(item.PeriodID, asofdate).ToList();
                    if (artrails.Count > 1)
                    {
                        List<StudentCount> templist = artrails.Select(m => new StudentCount { EducLevelId = item.EducLevelID.Value, StudentNo = m.StudentNo }).ToList();
                        studentnos.AddRange(templist);
                        List<StudentCount> templistnobalance = artrails.Where(m => m.ArBalance <= 1).Select(m => new StudentCount { EducLevelId = item.EducLevelID.Value, StudentNo = m.StudentNo }).ToList();
                        studentnosnobalance = templistnobalance;
                        List<StudentCount> templistbalance = artrails.Where(m => m.ArBalance >= 1).Select(m => new StudentCount { EducLevelId = item.EducLevelID.Value, StudentNo = m.StudentNo }).ToList();
                        studentnoswithbalance.AddRange(templistbalance);
                        summary.Periods.Add(item.Period1);
                        SumARSetupData(summary, i, artrails);
                        i++;
                    }
                }
                studentnoswithbalance = studentnoswithbalance.Where(m => !studentnosnobalance.Any(p => p.StudentNo == m.StudentNo)).ToList();
                summary.TotalStudent.TotalRW = studentnos.Select(student => student.StudentNo).Distinct().Count();
                summary.TotalStudentsWithBalance.TotalRW = studentnoswithbalance.Select(student => student.StudentNo).Distinct().Count();
                summary.ARBalancePercent1.TotalRW = summary.TotalStudent.TotalRW == 0 ? 0 : summary.TotalStudentsWithBalance.TotalRW / summary.TotalStudent.TotalRW;
                summary.CollectionPercent1.TotalRW = summary.TotalStudent.TotalRW == 0 ? 0 : 1 - summary.TotalStudentsWithBalance.TotalRW / summary.TotalStudent.TotalRW;
                summary.CollectionPercent2.TotalRW = summary.TotalFees.Total == 0 ? 0 : (1 - summary.ARBalance.Total / summary.TotalFees.Total);
                summary.ARBalancePercent2.TotalRW = summary.TotalFees.Total == 0 ? 0 : (summary.ARBalance.Total / summary.TotalFees.Total);
            }
        }

        private static void ConsolidatedSummary(List<ArTrail2024_Result> artrails, KeyValuePair<int, ARSetupSummaryConsolidatedItem> consolidateditem, bool dontincludebalance)
        {
            if (dontincludebalance)
            {
                consolidateditem.Value.ARBalance += (decimal)artrails.Sum(m => m.Assessment) + (decimal)artrails.Sum(m => m.DNForm) + (decimal)artrails.Sum(m => m.CMForm) + (decimal)artrails.Sum(m => m.DebitMemo) - (decimal)artrails.Sum(m => m.CreditMemo) - (decimal)artrails.Sum(m => m.Discount) - (decimal)artrails.Sum(m => m.AdjDiscount) - (decimal)artrails.Sum(m => m.Voucher) - (decimal)artrails.Sum(m => m.Payment) - (decimal)artrails.Sum(m => m.Processing);

            }
            else
            {
                consolidateditem.Value.ARBalance += (decimal)artrails.Sum(m => m.Assessment) + (decimal)artrails.Sum(m => m.Balance) + (decimal)artrails.Sum(m => m.DNForm) + (decimal)artrails.Sum(m => m.CMForm) + (decimal)artrails.Sum(m => m.DebitMemo) - (decimal)artrails.Sum(m => m.CreditMemo) - (decimal)artrails.Sum(m => m.Discount) - (decimal)artrails.Sum(m => m.AdjDiscount) - (decimal)artrails.Sum(m => m.Voucher) - (decimal)artrails.Sum(m => m.Payment) - (decimal)artrails.Sum(m => m.Processing);

            }
            consolidateditem.Value.ARFeesSetup += (decimal)artrails.Sum(m => m.Assessment);
        }

        private void GetARSummaryData(List<Period> periodids, DateTime asofdate, ARSetupSummary summary, Dictionary<int, List<Period>> consolidatedperiodids)
        {
            summary.Tuition.Item = "Tuition";
            summary.Miscellaneous.Item = "Miscellaneous";
            summary.Various.Item = "Various";
            summary.Laboratory.Item = "Laboratory";
            summary.TotalFees.Item = "Total fees set-up";
            summary.BeginningBalance.Item = "Beginning Balances";
            summary.Collection.Item = "Collection";
            summary.Adjustment.Item = "Adjustments";
            summary.Voucher.Item = "Vouchers";
            summary.Discount.Item = "Discounts";
            summary.ARBalance.Item = "A/R Balance";
            if (consolidatedperiodids.Count() > 0)
            {
                foreach (var consolidatedperiod in consolidatedperiodids)
                {
                    switch (consolidatedperiod.Key)
                    {
                        case 0:
                            summary.Periods.Add("1st Term");
                            break;
                        case 1:
                            summary.Periods.Add("2nd Term");
                            break;
                        case 2:
                            summary.Periods.Add("3rd Term");
                            break;
                        case 3:
                            summary.Periods.Add("4th Term");
                            break;
                    }

                    foreach (var item in consolidatedperiod.Value)
                    {
                        FeeSummaryElem elempergradelevel = new FeeSummaryElem();
                        var beginningbalance = db.GetSumOfBeginningBalance(item.PeriodID, asofdate).ToList();
                        var collectionsummary = db.GetCollectionSummary(item.PeriodID, asofdate).ToList();
                        var adjustmentsummary = db.GetSumOfAdjustment(item.PeriodID, asofdate).ToList();
                        var vouchersummary = db.GetSumOfVoucher(item.PeriodID, asofdate).ToList();
                        var discountsummary = db.GetSumOfDiscount(item.PeriodID, asofdate).ToList();
                        switch (item.EducLevelID)
                        {
                            case 1:
                                elempergradelevel = GetFeeSummaryElem(item.PeriodID, asofdate);
                                break;
                            case 2:
                                elempergradelevel = GetFeeSummaryJHS(item.PeriodID, asofdate);
                                break;
                            case 3:
                                elempergradelevel = GetFeeSummarySHS(item.PeriodID, asofdate);
                                break;
                            case 4:
                                elempergradelevel = GetFeeSummaryCollege(item.PeriodID, asofdate);
                                break;
                            case 5:
                            case 6:
                                elempergradelevel = GetFeeSummaryGS(item.PeriodID, asofdate);
                                break;
                        }
                        SumARSetupData(summary, consolidatedperiod.Key, elempergradelevel, beginningbalance, collectionsummary, adjustmentsummary, vouchersummary, discountsummary);
                    }

                }
            }
            else
            {
                int i = 0;
                foreach (var item in periodids)
                {
                    FeeSummaryElem elempergradelevel = new FeeSummaryElem();
                    var beginningbalance = db.GetSumOfBeginningBalance(item.PeriodID, asofdate).ToList();
                    var collectionsummary = db.GetCollectionSummary(item.PeriodID, asofdate).ToList();
                    var adjustmentsummary = db.GetSumOfAdjustment(item.PeriodID, asofdate).ToList();
                    var vouchersummary = db.GetSumOfVoucher(item.PeriodID, asofdate).ToList();
                    var discountsummary = db.GetSumOfDiscount(item.PeriodID, asofdate).ToList();
                    summary.Periods.Add(item.Period1);
                    switch (item.EducLevelID)
                    {
                        case 1:
                            elempergradelevel = GetFeeSummaryElem(item.PeriodID, asofdate);
                            break;
                        case 2:
                            elempergradelevel = GetFeeSummaryJHS(item.PeriodID, asofdate);
                            break;
                        case 3:
                            elempergradelevel = GetFeeSummarySHS(item.PeriodID, asofdate);
                            break;
                        case 4:
                            elempergradelevel = GetFeeSummaryCollege(item.PeriodID, asofdate);
                            break;
                        case 5:
                        case 6:
                            elempergradelevel = GetFeeSummaryGS(item.PeriodID, asofdate);
                            break;
                    }
                    SumARSetupData(summary, i, elempergradelevel, beginningbalance, collectionsummary, adjustmentsummary, vouchersummary, discountsummary);
                    i++;
                }
            }
        }

        private void SumARSetupData(ARSetupSummary summary, int switchkey, FeeSummaryElem elempergradelevel, List<GetSumOfBeginningBalance_Result> beginningbalance, List<double?> collectionsummary, List<double?> adjustmentsummary, List<double?> vouchersummary, List<double?> discountsummary)
        {
            switch (switchkey)
            {
                case 0:
                    summary.Tuition.Amount1 += elempergradelevel.TuitionFee.FirstOrDefault().Total;
                    summary.Miscellaneous.Amount1 += elempergradelevel.MiscellaneousTotal.Total;
                    summary.Various.Amount1 += elempergradelevel.OtherTotal.Total + elempergradelevel.SupplementalTotal.Total;
                    summary.Laboratory.Amount1 += elempergradelevel.LabTotal.Total;
                    summary.TotalFees.Amount1 += elempergradelevel.TotalAssessmentFee.Total;
                    summary.TotalStudent.Amount1 += elempergradelevel.NoOfEnrollees.FirstOrDefault().Total;
                    summary.BeginningBalance.Amount1 += beginningbalance.FirstOrDefault() != null ? (decimal)(beginningbalance.FirstOrDefault().Balance ?? 0) : 0;
                    summary.Collection.Amount1 += collectionsummary.FirstOrDefault() != null ? -(decimal)collectionsummary.FirstOrDefault().Value : 0;
                    summary.Adjustment.Amount1 += adjustmentsummary.FirstOrDefault() != null ? (decimal)adjustmentsummary.FirstOrDefault().Value : 0;
                    summary.Voucher.Amount1 += vouchersummary.FirstOrDefault() != null
                         ? -(decimal)vouchersummary.FirstOrDefault().Value : 0;
                    summary.Discount.Amount1 += discountsummary.FirstOrDefault() != null ? -(decimal)discountsummary.FirstOrDefault().Value : 0;
                    break;
                case 1:
                    summary.Tuition.Amount2 += elempergradelevel.TuitionFee.FirstOrDefault().Total;
                    summary.Miscellaneous.Amount2 += elempergradelevel.MiscellaneousTotal.Total;
                    summary.Various.Amount2 += elempergradelevel.OtherTotal.Total + elempergradelevel.SupplementalTotal.Total;
                    summary.Laboratory.Amount2 += elempergradelevel.LabTotal.Total;
                    summary.TotalFees.Amount2 += elempergradelevel.TotalAssessmentFee.Total;
                    summary.TotalStudent.Amount2 += elempergradelevel.NoOfEnrollees.FirstOrDefault().Total;
                    summary.BeginningBalance.Amount2 += beginningbalance.FirstOrDefault() != null ? (decimal)(beginningbalance.FirstOrDefault().Balance ?? 0) : 0;
                    summary.Collection.Amount2 += collectionsummary.FirstOrDefault() != null ? -(decimal)collectionsummary.FirstOrDefault().Value : 0;
                    summary.Adjustment.Amount2 += adjustmentsummary.FirstOrDefault() != null ? (decimal)adjustmentsummary.FirstOrDefault().Value : 0;
                    summary.Voucher.Amount2 += vouchersummary.FirstOrDefault() != null
                         ? -(decimal)vouchersummary.FirstOrDefault().Value : 0;
                    summary.Discount.Amount2 += discountsummary.FirstOrDefault() != null ? -(decimal)discountsummary.FirstOrDefault().Value : 0;
                    break;
                case 2:
                    summary.Tuition.Amount3 += elempergradelevel.TuitionFee.FirstOrDefault().Total;
                    summary.Miscellaneous.Amount3 += elempergradelevel.MiscellaneousTotal.Total;
                    summary.Various.Amount3 += elempergradelevel.OtherTotal.Total + elempergradelevel.SupplementalTotal.Total;
                    summary.Laboratory.Amount3 += elempergradelevel.LabTotal.Total;
                    summary.TotalFees.Amount3 += elempergradelevel.TotalAssessmentFee.Total;
                    summary.TotalStudent.Amount3 += elempergradelevel.NoOfEnrollees.FirstOrDefault().Total;
                    summary.BeginningBalance.Amount3 += beginningbalance.FirstOrDefault() != null ? (decimal)(beginningbalance.FirstOrDefault().Balance ?? 0) : 0;
                    summary.Collection.Amount3 += collectionsummary.FirstOrDefault() != null ? -(decimal)collectionsummary.FirstOrDefault().Value : 0;
                    summary.Adjustment.Amount3 += adjustmentsummary.FirstOrDefault() != null ? (decimal)adjustmentsummary.FirstOrDefault().Value : 0;
                    summary.Voucher.Amount3 += vouchersummary.FirstOrDefault() != null
                         ? -(decimal)vouchersummary.FirstOrDefault().Value : 0;
                    summary.Discount.Amount3 += discountsummary.FirstOrDefault() != null ? -(decimal)discountsummary.FirstOrDefault().Value : 0;
                    break;
                case 3:
                    summary.Tuition.Amount4 += elempergradelevel.TuitionFee.FirstOrDefault().Total;
                    summary.Miscellaneous.Amount4 += elempergradelevel.MiscellaneousTotal.Total;
                    summary.Various.Amount4 += elempergradelevel.OtherTotal.Total + elempergradelevel.SupplementalTotal.Total;
                    summary.Laboratory.Amount4 += elempergradelevel.LabTotal.Total;
                    summary.TotalFees.Amount4 += elempergradelevel.TotalAssessmentFee.Total;
                    summary.TotalStudent.Amount4 += elempergradelevel.NoOfEnrollees.FirstOrDefault().Total;
                    summary.BeginningBalance.Amount4 += beginningbalance.FirstOrDefault() != null ? (decimal)(beginningbalance.FirstOrDefault().Balance ?? 0) : 0;
                    summary.Collection.Amount4 += collectionsummary.FirstOrDefault() != null ? -(decimal)collectionsummary.FirstOrDefault().Value : 0;
                    summary.Adjustment.Amount4 += adjustmentsummary.FirstOrDefault() != null ? (decimal)adjustmentsummary.FirstOrDefault().Value : 0;
                    summary.Voucher.Amount4 += vouchersummary.FirstOrDefault() != null
                         ? -(decimal)vouchersummary.FirstOrDefault().Value : 0;
                    summary.Discount.Amount4 += discountsummary.FirstOrDefault() != null ? -(decimal)discountsummary.FirstOrDefault().Value : 0;
                    break;
            }
        }

        private void SumARSetupData(ARSetupSummary summary, int switchkey, List<ArTrail2024_Result> artrails)
        {

            switch (switchkey)
            {
                case 0:
                    summary.Tuition.Amount1 += (decimal)artrails.Sum(m => m.TuitionFee);
                    summary.Miscellaneous.Amount1 += (decimal)artrails.Sum(m => m.MiscFee);
                    summary.Various.Amount1 += (decimal)artrails.Sum(m => m.VariousFee);
                    summary.Laboratory.Amount1 += (decimal)artrails.Sum(m => m.LabFee);
                    summary.TotalFees.Amount1 += (decimal)artrails.Sum(m => m.Assessment);
                    summary.TotalStudent.Amount1 += (decimal)artrails.Count();
                    summary.BeginningBalance.Amount1 += (decimal)artrails.Sum(m => m.Balance);
                    summary.Collection.Amount1 += -((decimal)artrails.Sum(m => m.Processing) + (decimal)artrails.Sum(m => m.Payment));
                    summary.Adjustment.Amount1 += (decimal)artrails.Sum(m => m.DNForm) + (decimal)artrails.Sum(m => m.CMForm) + (decimal)artrails.Sum(m => m.DebitMemo) - (decimal)artrails.Sum(m => m.CreditMemo);
                    summary.Voucher.Amount1 += -(decimal)artrails.Sum(m => m.Voucher);
                    summary.Discount.Amount1 += -(decimal)artrails.Sum(m => m.Discount) - (decimal)artrails.Sum(m => m.AdjDiscount);
                    summary.ARBalance.Amount1 += (decimal)artrails.Sum(m => m.Assessment) + (decimal)artrails.Sum(m => m.Balance) + (decimal)artrails.Sum(m => m.DNForm) + (decimal)artrails.Sum(m => m.CMForm) + (decimal)artrails.Sum(m => m.DebitMemo) - (decimal)artrails.Sum(m => m.CreditMemo) - (decimal)artrails.Sum(m => m.Discount) - (decimal)artrails.Sum(m => m.AdjDiscount) - (decimal)artrails.Sum(m => m.Voucher) - (decimal)artrails.Sum(m => m.Payment) - (decimal)artrails.Sum(m => m.Processing);
                    summary.TotalStudentsWithBalance.Amount1 += artrails.Where(m => m.ArBalance >= 1).Count();
                    summary.ARBalancePercent1.Amount1 = summary.TotalStudent.Amount1 == 0 ? 0 : (summary.TotalStudentsWithBalance.Amount1 / summary.TotalStudent.Amount1);
                    summary.ARBalancePercent2.Amount1 = summary.TotalFees.Amount1 == 0 ? 0 : (summary.ARBalance.Amount1 / summary.TotalFees.Amount1);
                    summary.CollectionPercent1.Amount1 = (1 - summary.ARBalancePercent1.Amount1);
                    summary.CollectionPercent2.Amount1 = (1 - summary.ARBalancePercent2.Amount1);
                    break;
                case 1:
                    summary.Tuition.Amount2 += (decimal)artrails.Sum(m => m.TuitionFee);
                    summary.Miscellaneous.Amount2 += (decimal)artrails.Sum(m => m.MiscFee);
                    summary.Various.Amount2 += (decimal)artrails.Sum(m => m.VariousFee);
                    summary.Laboratory.Amount2 += (decimal)artrails.Sum(m => m.LabFee);
                    summary.TotalFees.Amount2 += (decimal)artrails.Sum(m => m.Assessment);
                    summary.TotalStudent.Amount2 += (decimal)artrails.Count();
                    summary.BeginningBalance.Amount2 += (decimal)artrails.Sum(m => m.Balance);
                    summary.Collection.Amount2 += -((decimal)artrails.Sum(m => m.Processing) + (decimal)artrails.Sum(m => m.Payment));
                    summary.Adjustment.Amount2 += (decimal)artrails.Sum(m => m.DNForm) + (decimal)artrails.Sum(m => m.CMForm) + (decimal)artrails.Sum(m => m.DebitMemo) - (decimal)artrails.Sum(m => m.CreditMemo);
                    summary.Voucher.Amount2 += -(decimal)artrails.Sum(m => m.Voucher);
                    summary.Discount.Amount2 += -(decimal)artrails.Sum(m => m.Discount) - (decimal)artrails.Sum(m => m.AdjDiscount);
                    summary.ARBalance.AmountB2 += (decimal)artrails.Sum(m => m.Balance);
                    summary.ARBalance.Amount2 += (decimal)artrails.Sum(m => m.Assessment) + (decimal)artrails.Sum(m => m.Balance) + (decimal)artrails.Sum(m => m.DNForm) + (decimal)artrails.Sum(m => m.CMForm) + (decimal)artrails.Sum(m => m.DebitMemo) - (decimal)artrails.Sum(m => m.CreditMemo) - (decimal)artrails.Sum(m => m.Discount) - (decimal)artrails.Sum(m => m.AdjDiscount) - (decimal)artrails.Sum(m => m.Voucher) - (decimal)artrails.Sum(m => m.Payment) - (decimal)artrails.Sum(m => m.Processing);
                    summary.TotalStudentsWithBalance.Amount2 += artrails.Where(m => m.ArBalance >= 1).Count();
                    summary.ARBalancePercent1.Amount2 = summary.TotalStudent.Amount2 == 0 ? 0 : (summary.TotalStudentsWithBalance.Amount2 / summary.TotalStudent.Amount2);
                    summary.ARBalancePercent2.Amount2 = summary.TotalFees.Amount2 == 0 ? 0 : (summary.ARBalance.Amount2 / summary.TotalFees.Amount2);
                    summary.CollectionPercent1.Amount2 = (1 - summary.ARBalancePercent1.Amount2);
                    summary.CollectionPercent2.Amount2 = (1 - summary.ARBalancePercent2.Amount2);
                    break;
                case 2:
                    summary.Tuition.Amount3 += (decimal)artrails.Sum(m => m.TuitionFee);
                    summary.Miscellaneous.Amount3 += (decimal)artrails.Sum(m => m.MiscFee);
                    summary.Various.Amount3 += (decimal)artrails.Sum(m => m.VariousFee);
                    summary.Laboratory.Amount3 += (decimal)artrails.Sum(m => m.LabFee);
                    summary.TotalFees.Amount3 += (decimal)artrails.Sum(m => m.Assessment);
                    summary.TotalStudent.Amount3 += (decimal)artrails.Count();
                    summary.BeginningBalance.Amount3 += (decimal)artrails.Sum(m => m.Balance);
                    summary.Collection.Amount3 += -((decimal)artrails.Sum(m => m.Processing) + (decimal)artrails.Sum(m => m.Payment));
                    summary.Adjustment.Amount3 += (decimal)artrails.Sum(m => m.DNForm) + (decimal)artrails.Sum(m => m.CMForm) + (decimal)artrails.Sum(m => m.DebitMemo) - (decimal)artrails.Sum(m => m.CreditMemo);
                    summary.Voucher.Amount3 += -(decimal)artrails.Sum(m => m.Voucher);
                    summary.Discount.Amount3 += -(decimal)artrails.Sum(m => m.Discount) - (decimal)artrails.Sum(m => m.AdjDiscount);
                    summary.ARBalance.AmountB3 += (decimal)artrails.Sum(m => m.Balance);
                    summary.ARBalance.Amount3 += (decimal)artrails.Sum(m => m.Assessment) + (decimal)artrails.Sum(m => m.Balance) + (decimal)artrails.Sum(m => m.DNForm) + (decimal)artrails.Sum(m => m.CMForm) + (decimal)artrails.Sum(m => m.DebitMemo) - (decimal)artrails.Sum(m => m.CreditMemo) - (decimal)artrails.Sum(m => m.Discount) - (decimal)artrails.Sum(m => m.AdjDiscount) - (decimal)artrails.Sum(m => m.Voucher) - (decimal)artrails.Sum(m => m.Payment) - (decimal)artrails.Sum(m => m.Processing);
                    summary.TotalStudentsWithBalance.Amount3 += artrails.Where(m => m.ArBalance >= 1).Count();
                    summary.ARBalancePercent1.Amount3 = summary.TotalStudent.Amount3 == 0 ? 0 : (summary.TotalStudentsWithBalance.Amount3 / summary.TotalStudent.Amount3);
                    summary.ARBalancePercent2.Amount3 = summary.TotalFees.Amount3 == 0 ? 0 : (summary.ARBalance.Amount3 / summary.TotalFees.Amount3);
                    summary.CollectionPercent1.Amount3 = (1 - summary.ARBalancePercent1.Amount3);
                    summary.CollectionPercent2.Amount3 = (1 - summary.ARBalancePercent2.Amount3);
                    break;
                case 3:
                    summary.Tuition.Amount4 += (decimal)artrails.Sum(m => m.TuitionFee);
                    summary.Miscellaneous.Amount4 += (decimal)artrails.Sum(m => m.MiscFee);
                    summary.Various.Amount4 += (decimal)artrails.Sum(m => m.VariousFee);
                    summary.Laboratory.Amount4 += (decimal)artrails.Sum(m => m.LabFee);
                    summary.TotalFees.Amount4 += (decimal)artrails.Sum(m => m.Assessment);
                    summary.TotalStudent.Amount4 += (decimal)artrails.Count();
                    summary.BeginningBalance.Amount4 += (decimal)artrails.Sum(m => m.Balance);
                    summary.Collection.Amount4 += -((decimal)artrails.Sum(m => m.Processing) + (decimal)artrails.Sum(m => m.Payment));
                    summary.Adjustment.Amount4 += (decimal)artrails.Sum(m => m.DNForm) + (decimal)artrails.Sum(m => m.CMForm) + (decimal)artrails.Sum(m => m.DebitMemo) - (decimal)artrails.Sum(m => m.CreditMemo);
                    summary.Voucher.Amount4 += -(decimal)artrails.Sum(m => m.Voucher);
                    summary.Discount.Amount4 += -(decimal)artrails.Sum(m => m.Discount) - (decimal)artrails.Sum(m => m.AdjDiscount);
                    summary.ARBalance.AmountB4 += (decimal)artrails.Sum(m => m.Balance);
                    summary.ARBalance.Amount4 += (decimal)artrails.Sum(m => m.Assessment) + (decimal)artrails.Sum(m => m.Balance) + (decimal)artrails.Sum(m => m.DNForm) + (decimal)artrails.Sum(m => m.CMForm) + (decimal)artrails.Sum(m => m.DebitMemo) - (decimal)artrails.Sum(m => m.CreditMemo) - (decimal)artrails.Sum(m => m.Discount) - (decimal)artrails.Sum(m => m.AdjDiscount) - (decimal)artrails.Sum(m => m.Voucher) - (decimal)artrails.Sum(m => m.Payment) - (decimal)artrails.Sum(m => m.Processing);
                    summary.TotalStudentsWithBalance.Amount4 += artrails.Where(m => m.ArBalance >= 1).Count();
                    summary.ARBalancePercent1.Amount4 = summary.TotalStudent.Amount4 == 0 ? 0 : (summary.TotalStudentsWithBalance.Amount4 / summary.TotalStudent.Amount4);
                    summary.ARBalancePercent2.Amount4 = summary.TotalFees.Amount4 == 0 ? 0 : (summary.ARBalance.Amount4 / summary.TotalFees.Amount4);
                    summary.CollectionPercent1.Amount4 = (1 - summary.ARBalancePercent1.Amount4);
                    summary.CollectionPercent2.Amount4 = (1 - summary.ARBalancePercent2.Amount4);
                    break;
            }

        }
        #endregion ARSetupSummary

        #region SummaryOfFees
        public ActionResult FeesSummary()
        {
            //return FeesSummaryViewAs(DateTime.Today, 1);
            return View();
        }

        [HttpPost]
        public ActionResult FeesSummary(DateTime asofdate, int viewas, string iscollegeformat)
        {
            return FeesSummaryViewAs(asofdate, viewas, iscollegeformat);
        }

        private ActionResult FeesSummaryViewAs(DateTime asofdate, int viewas, string iscollegeformat)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();

            ViewBag.asofdate = asofdate.ToString("yyyy-MM-dd");
            if (Period.EducLevelID == 1)
            {
                FeeSummaryElem fees = GetFeeSummaryElem(Period.PeriodID, asofdate);
                fees.AsOfDate = "As Of " + asofdate.ToString("MMMM dd, yyyy");
                fees.PreparedBy = employee.FullName;
                fees.Period = Period.FullName;
                fees.Header = "Basic Education Department";
                fees.Subheader = "(" + Period.EducationalLevel1.EducLevelName + ")";
                if (viewas == 1)
                {
                    return View(fees);
                }
                else
                {
                    ReportDocument reportDocument = new FeeSummaryReport();
                    reportDocument.Subreports["NoOfEnrollees"].SetDataSource(fees.NoOfEnrollees);
                    reportDocument.Subreports["TuitionFees"].SetDataSource(fees.TuitionFee);
                    reportDocument.Subreports["miscfees"].SetDataSource(fees.MiscellaneousFee);
                    reportDocument.Subreports["suppfees"].SetDataSource(fees.SupplementalFee);
                    reportDocument.Subreports["otherfees"].SetDataSource(fees.OtherFee);
                    reportDocument.Subreports["labfees"].SetDataSource(fees.LabFee);

                    List<FeeSummaryItemElem> total = new List<FeeSummaryItemElem>();
                    total.Add(fees.TotalAssessmentFee);
                    reportDocument.Subreports["totalfees"].SetDataSource(total);

                    List<FeeSummaryElem> feeSummaries = new List<FeeSummaryElem>();
                    feeSummaries.Add(fees);
                    reportDocument.SetDataSource(feeSummaries);
                    return ExportType(viewas - 1, "FeesSummary_" + Period.EducationalLevel1.EducLevelName + "_" + asofdate.Date.ToString("dd MMMM yyyy"), reportDocument);
                }
            }
            else if (Period.EducLevelID == 2)
            {
                FeeSummaryElem fees = GetFeeSummaryJHS(Period.PeriodID, asofdate);
                fees.AsOfDate = "As Of " + asofdate.ToString("MMMM dd, yyyy");
                fees.PreparedBy = employee.FullName;
                fees.Period = Period.FullName;
                fees.Header = "Basic Education Department";
                fees.Subheader = "(" + Period.EducationalLevel1.EducLevelName + ")";
                if (viewas == 1)
                {
                    return View("FeesSummaryJHS", fees);
                }
                else
                {
                    ReportDocument reportDocument = new FeeSummaryReportJHS();
                    reportDocument.Subreports["NoOfEnrollees"].SetDataSource(fees.NoOfEnrollees);
                    reportDocument.Subreports["TuitionFees"].SetDataSource(fees.TuitionFee);
                    reportDocument.Subreports["miscfees"].SetDataSource(fees.MiscellaneousFee);
                    reportDocument.Subreports["suppfees"].SetDataSource(fees.SupplementalFee);
                    reportDocument.Subreports["otherfees"].SetDataSource(fees.OtherFee);
                    reportDocument.Subreports["labfees"].SetDataSource(fees.LabFee);

                    List<FeeSummaryItemElem> total = new List<FeeSummaryItemElem>();
                    total.Add(fees.TotalAssessmentFee);
                    reportDocument.Subreports["totalfees"].SetDataSource(total);

                    List<FeeSummaryElem> feeSummaries = new List<FeeSummaryElem>();
                    feeSummaries.Add(fees);
                    reportDocument.SetDataSource(feeSummaries);
                    return ExportType(viewas - 1, "FeesSummary_" + Period.EducationalLevel1.EducLevelName + "_" + asofdate.Date.ToString("dd MMMM yyyy"), reportDocument);
                }

            }
            else if (Period.EducLevelID == 3)
            {
                FeeSummaryElem fees = GetFeeSummarySHS(Period.PeriodID, asofdate);
                fees.AsOfDate = "As Of " + asofdate.ToString("MMMM dd, yyyy");
                fees.PreparedBy = employee.FullName;
                fees.Period = Period.FullName;
                fees.Header = Period.EducationalLevel1.EducLevelName;
                fees.Subheader = "";
                if (viewas == 1)
                {
                    return View("FeesSummarySHS", fees);
                }
                else
                {
                    ReportDocument reportDocument = new FeeSummaryReportSHS();
                    reportDocument.Subreports["NoOfEnrollees"].SetDataSource(fees.NoOfEnrollees);
                    reportDocument.Subreports["TuitionFees"].SetDataSource(fees.TuitionFee);
                    reportDocument.Subreports["miscfees"].SetDataSource(fees.MiscellaneousFee);
                    reportDocument.Subreports["suppfees"].SetDataSource(fees.SupplementalFee);
                    reportDocument.Subreports["otherfees"].SetDataSource(fees.OtherFee);
                    reportDocument.Subreports["labfees"].SetDataSource(fees.LabFee);

                    List<FeeSummaryItemElem> total = new List<FeeSummaryItemElem>();
                    total.Add(fees.TotalAssessmentFee);
                    reportDocument.Subreports["totalfees"].SetDataSource(total);

                    List<FeeSummaryElem> feeSummaries = new List<FeeSummaryElem>();
                    feeSummaries.Add(fees);
                    reportDocument.SetDataSource(feeSummaries);
                    return ExportType(viewas - 1, "FeesSummary_" + Period.EducationalLevel1.EducLevelName + "_" + asofdate.Date.ToString("dd MMMM yyyy"), reportDocument);
                }


            }
            else if (Period.EducLevelID == 4)
            {
                FeeSummaryElem fees;
                if (iscollegeformat != null && iscollegeformat.Equals("on"))
                {
                    fees = GetFeeSummaryCollegeCollegeFormat(Period.PeriodID, asofdate);
                }
                else
                {
                    fees = GetFeeSummaryCollege(Period.PeriodID, asofdate);
                }
                fees.AsOfDate = "As Of " + asofdate.ToString("MMMM dd, yyyy");
                fees.PreparedBy = employee.FullName;
                fees.Period = Period.FullName;
                fees.Header = Period.EducationalLevel1.EducLevelName;
                fees.Subheader = "";
                if (viewas == 1)
                {
                    if (iscollegeformat != null && iscollegeformat.Equals("on"))
                        return View("FeesSummaryCollegeCollegeFormat", fees);
                    else
                        return View("FeesSummaryCollege", fees);
                }
                else
                {
                    ReportDocument reportDocument;
                    if (iscollegeformat != null && iscollegeformat.Equals("on"))
                        reportDocument = new FeeSummaryReportCollegeFormat();
                    else
                        reportDocument = new FeeSummaryReportCollege();
                    reportDocument.Subreports["NoOfEnrollees"].SetDataSource(fees.NoOfEnrollees);
                    reportDocument.Subreports["TuitionFees"].SetDataSource(fees.TuitionFee);
                    reportDocument.Subreports["miscfees"].SetDataSource(fees.MiscellaneousFee);
                    reportDocument.Subreports["suppfees"].SetDataSource(fees.SupplementalFee);
                    reportDocument.Subreports["otherfees"].SetDataSource(fees.OtherFee);
                    reportDocument.Subreports["labfees"].SetDataSource(fees.LabFee);

                    List<FeeSummaryItemElem> total = new List<FeeSummaryItemElem>();
                    total.Add(fees.TotalAssessmentFee);
                    reportDocument.Subreports["totalfees"].SetDataSource(total);

                    List<FeeSummaryElem> feeSummaries = new List<FeeSummaryElem>();
                    feeSummaries.Add(fees);
                    reportDocument.SetDataSource(feeSummaries);
                    return ExportType(viewas - 1, "FeesSummary_" + Period.EducationalLevel1.EducLevelName + "_" + asofdate.Date.ToString("dd MMMM yyyy"), reportDocument);
                }

            }
            else
            {
                FeeSummaryElem fees = GetFeeSummaryGS(Period.PeriodID, asofdate);
                fees.AsOfDate = "As Of " + asofdate.ToString("MMMM dd, yyyy");
                fees.PreparedBy = employee.FullName;
                fees.Period = Period.FullName;
                fees.Header = "Graduate School";
                fees.Subheader = "(" + Period.EducationalLevel1.EducLevelName + ")";
                if (viewas == 1)
                {
                    return View("FeesSummaryGS", fees);
                }
                else
                {
                    ReportDocument reportDocument = new FeeSummaryReportGS();
                    reportDocument.Subreports["NoOfEnrollees"].SetDataSource(fees.NoOfEnrollees);
                    reportDocument.Subreports["TuitionFees"].SetDataSource(fees.TuitionFee);
                    reportDocument.Subreports["miscfees"].SetDataSource(fees.MiscellaneousFee);
                    reportDocument.Subreports["suppfees"].SetDataSource(fees.SupplementalFee);
                    reportDocument.Subreports["otherfees"].SetDataSource(fees.OtherFee);
                    reportDocument.Subreports["labfees"].SetDataSource(fees.LabFee);

                    List<FeeSummaryItemElem> total = new List<FeeSummaryItemElem>();
                    total.Add(fees.TotalAssessmentFee);
                    reportDocument.Subreports["totalfees"].SetDataSource(total);

                    List<FeeSummaryElem> feeSummaries = new List<FeeSummaryElem>();
                    feeSummaries.Add(fees);
                    reportDocument.SetDataSource(feeSummaries);
                    return ExportType(viewas - 1, "FeesSummary_" + Period.EducationalLevel1.EducLevelName + "_" + asofdate.Date.ToString("dd MMMM yyyy"), reportDocument);
                }
            }
        }

        private FeeSummaryElem GetFeeSummaryCollege(int periodid, DateTime asofdate)
        {
            //var assessments = db.Student_Section.Where(m => m.Section.PeriodID == periodid && m.ValidationDate != null && m.ValidationDate <= asofdate && m.StudentStatus.HasValue && m.StudentStatus < 6);
            var summaryNoOfEnrollees = db.GetSummaryOfFees(0, asofdate, periodid).ToList();
            var summaryTuitionFees = db.GetSummaryOfFees(1, asofdate, periodid).ToList();
            var summaryMiscFees = db.GetSummaryOfFees(2, asofdate, periodid).ToList();
            var summarySuppFees = db.GetSummaryOfFees(3, asofdate, periodid).ToList();
            var summaryOtherFees = db.GetSummaryOfFees(4, asofdate, periodid).ToList();
            var summaryLabFees = db.GetSummaryOfFees(5, asofdate, periodid).ToList();
            FeeSummaryElem fees = new FeeSummaryElem();
            List<FeeSummaryItemElem> feeSummaryItemsTotalEnrolles = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsTuition = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsMisc = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsSupp = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsOther = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsLab = new List<FeeSummaryItemElem>();
            fees.TotalAssessmentFee.Item = "Total Assessment Fees";

            int grade1totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 1).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 1).FirstOrDefault().Total : 0;
            int grade2totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 2).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 2).FirstOrDefault().Total : 0;
            int grade3totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 3).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 3).FirstOrDefault().Total : 0;
            int grade4totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 4).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 4).FirstOrDefault().Total : 0;
            int grade5totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 5).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 5).FirstOrDefault().Total : 0;
            feeSummaryItemsTotalEnrolles.Add(new FeeSummaryItemElem
            {
                Item = "Number of Enrollees",
                Grade1 = grade1totalenrolles,
                Grade2 = grade2totalenrolles,
                Grade3 = grade3totalenrolles,
                Grade4 = grade4totalenrolles,
                Grade5 = grade5totalenrolles,
                Total = grade1totalenrolles + grade2totalenrolles + grade3totalenrolles + grade4totalenrolles + grade5totalenrolles
            });

            decimal grade1tuitiontotal = 0;
            decimal grade2tuitiontotal = 0;
            decimal grade3tuitiontotal = 0;
            decimal grade4tuitiontotal = 0;
            decimal grade5tuitiontotal = 0;
            foreach (var item in summaryTuitionFees)
            {
                switch (item.GradeYear)
                {
                    case 1:
                        grade1tuitiontotal += item.Total;
                        break;
                    case 2:
                        grade2tuitiontotal += item.Total;
                        break;
                    case 3:
                        grade3tuitiontotal += item.Total;
                        break;
                    case 4:
                        grade4tuitiontotal += item.Total;
                        break;
                    case 5:
                        grade5tuitiontotal += item.Total;
                        break;
                }
            }
            feeSummaryItemsTuition.Add(new FeeSummaryItemElem
            {
                Item = "Tuition Fee",
                Grade1 = grade1tuitiontotal,
                Grade2 = grade2tuitiontotal,
                Grade3 = grade3tuitiontotal,
                Grade4 = grade4tuitiontotal,
                Grade5 = grade5tuitiontotal,
                Total = grade1tuitiontotal + grade2tuitiontotal + grade3tuitiontotal + grade4tuitiontotal + grade5tuitiontotal
            });
            fees.TotalAssessmentFee.Grade1 += grade1tuitiontotal;
            fees.TotalAssessmentFee.Grade2 += grade2tuitiontotal;
            fees.TotalAssessmentFee.Grade3 += grade3tuitiontotal;
            fees.TotalAssessmentFee.Grade4 += grade4tuitiontotal;
            fees.TotalAssessmentFee.Grade5 += grade5tuitiontotal;
            fees.TotalAssessmentFee.Total += grade1tuitiontotal + grade2tuitiontotal + grade3tuitiontotal + grade4tuitiontotal + grade5tuitiontotal;

            fees.MiscellaneousTotal = new FeeSummaryItemElem();
            var miscdescriptions = summaryMiscFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in miscdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                decimal grade5miscitemtotal = 0;
                foreach (var miscitem in summaryMiscFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 1:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 2:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 3:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 4:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                        case 5:
                            grade5miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsMisc.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Grade5 = grade5miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal
                });
                fees.MiscellaneousTotal.Item = "Subotal";
                fees.MiscellaneousTotal.Grade1 += grade1miscitemtotal;
                fees.MiscellaneousTotal.Grade2 += grade2miscitemtotal;
                fees.MiscellaneousTotal.Grade3 += grade3miscitemtotal;
                fees.MiscellaneousTotal.Grade4 += grade4miscitemtotal;
                fees.MiscellaneousTotal.Grade5 += grade5miscitemtotal;
                fees.MiscellaneousTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade5 += grade5miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal;
            }

            fees.SupplementalTotal = new FeeSummaryItemElem();
            var suppdescriptions = summarySuppFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in suppdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                decimal grade5miscitemtotal = 0;
                foreach (var miscitem in summarySuppFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 1:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 2:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 3:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 4:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                        case 5:
                            grade5miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsSupp.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Grade5 = grade5miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal
                });
                fees.SupplementalTotal.Item = "Subtotal";
                fees.SupplementalTotal.Grade1 += grade1miscitemtotal;
                fees.SupplementalTotal.Grade2 += grade2miscitemtotal;
                fees.SupplementalTotal.Grade3 += grade3miscitemtotal;
                fees.SupplementalTotal.Grade4 += grade4miscitemtotal;
                fees.SupplementalTotal.Grade5 += grade5miscitemtotal;
                fees.SupplementalTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade5 += grade5miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal;
            }

            fees.OtherTotal = new FeeSummaryItemElem();
            var otherdescriptions = summaryOtherFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in otherdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                decimal grade5miscitemtotal = 0;
                foreach (var miscitem in summaryOtherFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 1:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 2:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 3:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 4:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                        case 5:
                            grade5miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsOther.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Grade5 = grade5miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal
                });
                fees.OtherTotal.Item = "Subtotal";
                fees.OtherTotal.Grade1 += grade1miscitemtotal;
                fees.OtherTotal.Grade2 += grade2miscitemtotal;
                fees.OtherTotal.Grade3 += grade3miscitemtotal;
                fees.OtherTotal.Grade4 += grade4miscitemtotal;
                fees.OtherTotal.Grade5 += grade5miscitemtotal;
                fees.OtherTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade5 += grade5miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal;
            }

            fees.LabTotal = new FeeSummaryItemElem();
            var labdescriptions = summaryLabFees.Select(o => o.Description).Distinct().ToList(); ;
            foreach (var miscdecription in labdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                decimal grade5miscitemtotal = 0;
                foreach (var miscitem in summaryLabFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 1:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 2:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 3:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 4:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                        case 5:
                            grade5miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsLab.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Grade5 = grade5miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal
                });
                fees.LabTotal.Item = "Subtotal";
                fees.LabTotal.Grade1 += grade1miscitemtotal;
                fees.LabTotal.Grade2 += grade2miscitemtotal;
                fees.LabTotal.Grade3 += grade3miscitemtotal;
                fees.LabTotal.Grade4 += grade4miscitemtotal;
                fees.LabTotal.Grade5 += grade5miscitemtotal;
                fees.LabTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade5 += grade5miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal;
            }

            fees.NoOfEnrollees = feeSummaryItemsTotalEnrolles;
            fees.TuitionFee = feeSummaryItemsTuition;
            fees.MiscellaneousFee = feeSummaryItemsMisc.OrderBy(m => m.Item).ToList();
            fees.SupplementalFee = feeSummaryItemsSupp.OrderBy(m => m.Item).ToList();
            fees.OtherFee = feeSummaryItemsOther.OrderBy(m => m.Item).ToList();
            fees.LabFee = feeSummaryItemsLab.OrderBy(m => m.Item).ToList();
            return fees;
        }

        private FeeSummaryElem GetFeeSummaryCollegeCollegeFormat(int periodid, DateTime asofdate)
        {
            var summaryNoOfEnrollees = db.GetSummaryOfFeesCollege(0, asofdate, periodid).ToList();
            var summaryTuitionFees = db.GetSummaryOfFeesCollege(1, asofdate, periodid).ToList();
            var summaryMiscFees = db.GetSummaryOfFeesCollege(2, asofdate, periodid).ToList();
            var summarySuppFees = db.GetSummaryOfFeesCollege(3, asofdate, periodid).ToList();
            var summaryOtherFees = db.GetSummaryOfFeesCollege(4, asofdate, periodid).ToList();
            var summaryLabFees = db.GetSummaryOfFeesCollege(5, asofdate, periodid).ToList();
            FeeSummaryElem fees = new FeeSummaryElem();
            List<FeeSummaryItemElem> feeSummaryItemsTotalEnrolles = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsTuition = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsMisc = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsSupp = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsOther = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsLab = new List<FeeSummaryItemElem>();
            fees.TotalAssessmentFee.Item = "Total Assessment Fees";

            int grade1totalenrolles = summaryNoOfEnrollees.Where(m => m.AcaAcronym.Equals("CBAA")).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.AcaAcronym.Equals("CBAA")).FirstOrDefault().Total : 0;
            int grade2totalenrolles = summaryNoOfEnrollees.Where(m => m.AcaAcronym.Equals("CEIT")).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.AcaAcronym.Equals("CEIT")).FirstOrDefault().Total : 0;
            int grade3totalenrolles = summaryNoOfEnrollees.Where(m => m.AcaAcronym.Equals("CLAS")).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.AcaAcronym.Equals("CLAS")).FirstOrDefault().Total : 0;
            int grade4totalenrolles = summaryNoOfEnrollees.Where(m => m.AcaAcronym.Equals("CoE")).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.AcaAcronym.Equals("CoE")).FirstOrDefault().Total : 0;
            feeSummaryItemsTotalEnrolles.Add(new FeeSummaryItemElem
            {
                Item = "Number of Enrollees",
                Grade1 = grade1totalenrolles,
                Grade2 = grade2totalenrolles,
                Grade3 = grade3totalenrolles,
                Grade4 = grade4totalenrolles,
                Total = grade1totalenrolles + grade2totalenrolles + grade3totalenrolles + grade4totalenrolles
            });

            decimal grade1tuitiontotal = 0;
            decimal grade2tuitiontotal = 0;
            decimal grade3tuitiontotal = 0;
            decimal grade4tuitiontotal = 0;
            foreach (var item in summaryTuitionFees)
            {
                switch (item.AcaAcronym)
                {
                    case "CBAA":
                        grade1tuitiontotal += item.Total;
                        break;
                    case "CEIT":
                        grade2tuitiontotal += item.Total;
                        break;
                    case "CLAS":
                        grade3tuitiontotal += item.Total;
                        break;
                    case "CoE":
                        grade4tuitiontotal += item.Total;
                        break;
                }
            }
            feeSummaryItemsTuition.Add(new FeeSummaryItemElem
            {
                Item = "Tuition Fee",
                Grade1 = grade1tuitiontotal,
                Grade2 = grade2tuitiontotal,
                Grade3 = grade3tuitiontotal,
                Grade4 = grade4tuitiontotal,
                Total = grade1tuitiontotal + grade2tuitiontotal + grade3tuitiontotal + grade4tuitiontotal
            });
            fees.TotalAssessmentFee.Grade1 += grade1tuitiontotal;
            fees.TotalAssessmentFee.Grade2 += grade2tuitiontotal;
            fees.TotalAssessmentFee.Grade3 += grade3tuitiontotal;
            fees.TotalAssessmentFee.Grade4 += grade4tuitiontotal;
            fees.TotalAssessmentFee.Total += grade1tuitiontotal + grade2tuitiontotal + grade3tuitiontotal + grade4tuitiontotal;

            fees.MiscellaneousTotal = new FeeSummaryItemElem();
            var miscdescriptions = summaryMiscFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in miscdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                foreach (var miscitem in summaryMiscFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.AcaAcronym)
                    {
                        case "CBAA":
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case "CEIT":
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case "CLAS":
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case "CoE":
                            grade4miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsMisc.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal
                });
                fees.MiscellaneousTotal.Item = "Subotal";
                fees.MiscellaneousTotal.Grade1 += grade1miscitemtotal;
                fees.MiscellaneousTotal.Grade2 += grade2miscitemtotal;
                fees.MiscellaneousTotal.Grade3 += grade3miscitemtotal;
                fees.MiscellaneousTotal.Grade4 += grade4miscitemtotal;
                fees.MiscellaneousTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
            }

            fees.SupplementalTotal = new FeeSummaryItemElem();
            var suppdescriptions = summarySuppFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in suppdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                foreach (var miscitem in summarySuppFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.AcaAcronym)
                    {
                        case "CBAA":
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case "CEIT":
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case "CLAS":
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case "CoE":
                            grade4miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsSupp.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal
                });
                fees.SupplementalTotal.Item = "Subtotal";
                fees.SupplementalTotal.Grade1 += grade1miscitemtotal;
                fees.SupplementalTotal.Grade2 += grade2miscitemtotal;
                fees.SupplementalTotal.Grade3 += grade3miscitemtotal;
                fees.SupplementalTotal.Grade4 += grade4miscitemtotal;
                fees.SupplementalTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
            }

            fees.OtherTotal = new FeeSummaryItemElem();
            var otherdescriptions = summaryOtherFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in otherdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                foreach (var miscitem in summaryOtherFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.AcaAcronym)
                    {
                        case "CBAA":
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case "CEIT":
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case "CLAS":
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case "CoE":
                            grade4miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsOther.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal
                });
                fees.OtherTotal.Item = "Subtotal";
                fees.OtherTotal.Grade1 += grade1miscitemtotal;
                fees.OtherTotal.Grade2 += grade2miscitemtotal;
                fees.OtherTotal.Grade3 += grade3miscitemtotal;
                fees.OtherTotal.Grade4 += grade4miscitemtotal;
                fees.OtherTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
            }

            fees.LabTotal = new FeeSummaryItemElem();
            var labdescriptions = summaryLabFees.Select(o => o.Description).Distinct().ToList(); ;
            foreach (var miscdecription in labdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                foreach (var miscitem in summaryLabFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.AcaAcronym)
                    {
                        case "CBAA":
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case "CEIT":
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case "CLAS":
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case "CoE":
                            grade4miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsLab.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal
                });
                fees.LabTotal.Item = "Subtotal";
                fees.LabTotal.Grade1 += grade1miscitemtotal;
                fees.LabTotal.Grade2 += grade2miscitemtotal;
                fees.LabTotal.Grade3 += grade3miscitemtotal;
                fees.LabTotal.Grade4 += grade4miscitemtotal;
                fees.LabTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
            }

            fees.NoOfEnrollees = feeSummaryItemsTotalEnrolles;
            fees.TuitionFee = feeSummaryItemsTuition;
            fees.MiscellaneousFee = feeSummaryItemsMisc.OrderBy(m => m.Item).ToList();
            fees.SupplementalFee = feeSummaryItemsSupp.OrderBy(m => m.Item).ToList();
            fees.OtherFee = feeSummaryItemsOther.OrderBy(m => m.Item).ToList();
            fees.LabFee = feeSummaryItemsLab.OrderBy(m => m.Item).ToList();
            return fees;
        }

        private FeeSummaryElem GetFeeSummaryElem(int periodid, DateTime asofdate)
        {
            //var assessments = db.Student_Section.Where(m => m.Section.PeriodID == periodid && m.ValidationDate != null && m.ValidationDate <= asofdate && m.StudentStatus.HasValue && m.StudentStatus < 6);
            var summaryNoOfEnrollees = db.GetSummaryOfFees(0, asofdate, periodid).ToList();
            var summaryTuitionFees = db.GetSummaryOfFees(1, asofdate, periodid).ToList();
            var summaryMiscFees = db.GetSummaryOfFees(2, asofdate, periodid).ToList();
            var summarySuppFees = db.GetSummaryOfFees(3, asofdate, periodid).ToList();
            var summaryOtherFees = db.GetSummaryOfFees(4, asofdate, periodid).ToList();
            var summaryLabFees = db.GetSummaryOfFees(5, asofdate, periodid).ToList();
            FeeSummaryElem fees = new FeeSummaryElem();
            List<FeeSummaryItemElem> feeSummaryItemsTotalEnrolles = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsTuition = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsMisc = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsSupp = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsOther = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsLab = new List<FeeSummaryItemElem>();
            fees.TotalAssessmentFee.Item = "Total Assessment Fees";

            int grade1totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 1).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 1).FirstOrDefault().Total : 0;
            int grade2totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 2).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 2).FirstOrDefault().Total : 0;
            int grade3totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 3).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 3).FirstOrDefault().Total : 0;
            int grade4totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 4).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 4).FirstOrDefault().Total : 0;
            int grade5totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 5).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 5).FirstOrDefault().Total : 0;
            int grade6totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 6).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 6).FirstOrDefault().Total : 0;
            int kindertotalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 0).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 0).FirstOrDefault().Total : 0;
            feeSummaryItemsTotalEnrolles.Add(new FeeSummaryItemElem
            {
                Item = "Number of Enrollees",
                Grade1 = grade1totalenrolles,
                Grade2 = grade2totalenrolles,
                Grade3 = grade3totalenrolles,
                Grade4 = grade4totalenrolles,
                Grade5 = grade5totalenrolles,
                Grade6 = grade6totalenrolles,
                Kinder = kindertotalenrolles,
                Total = grade1totalenrolles + grade2totalenrolles + grade3totalenrolles + grade4totalenrolles + grade5totalenrolles + grade6totalenrolles + kindertotalenrolles
            });

            decimal grade1tuitiontotal = 0;
            decimal grade2tuitiontotal = 0;
            decimal grade3tuitiontotal = 0;
            decimal grade4tuitiontotal = 0;
            decimal grade5tuitiontotal = 0;
            decimal grade6tuitiontotal = 0;
            decimal kindertuitiontotal = 0;
            foreach (var item in summaryTuitionFees)
            {
                switch (item.GradeYear)
                {
                    case 0:
                        kindertuitiontotal += item.Total;
                        break;
                    case 1:
                        grade1tuitiontotal += item.Total;
                        break;
                    case 2:
                        grade2tuitiontotal += item.Total;
                        break;
                    case 3:
                        grade3tuitiontotal += item.Total;
                        break;
                    case 4:
                        grade4tuitiontotal += item.Total;
                        break;
                    case 5:
                        grade5tuitiontotal += item.Total;
                        break;
                    case 6:
                        grade6tuitiontotal += item.Total;
                        break;
                }
            }
            feeSummaryItemsTuition.Add(new FeeSummaryItemElem
            {
                Item = "Tuition Fee",
                Grade1 = grade1tuitiontotal,
                Grade2 = grade2tuitiontotal,
                Grade3 = grade3tuitiontotal,
                Grade4 = grade4tuitiontotal,
                Grade5 = grade5tuitiontotal,
                Grade6 = grade6tuitiontotal,
                Kinder = kindertuitiontotal,
                Total = grade1tuitiontotal + grade2tuitiontotal + grade3tuitiontotal + grade4tuitiontotal + grade5tuitiontotal + grade6tuitiontotal + kindertuitiontotal
            });
            fees.TotalAssessmentFee.Grade1 += grade1tuitiontotal;
            fees.TotalAssessmentFee.Grade2 += grade2tuitiontotal;
            fees.TotalAssessmentFee.Grade3 += grade3tuitiontotal;
            fees.TotalAssessmentFee.Grade4 += grade4tuitiontotal;
            fees.TotalAssessmentFee.Grade5 += grade5tuitiontotal;
            fees.TotalAssessmentFee.Grade6 += grade6tuitiontotal;
            fees.TotalAssessmentFee.Total += grade1tuitiontotal + grade2tuitiontotal + grade3tuitiontotal + grade4tuitiontotal + grade5tuitiontotal + grade6tuitiontotal + kindertuitiontotal;

            fees.MiscellaneousTotal = new FeeSummaryItemElem();
            var miscdescriptions = summaryMiscFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in miscdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                decimal grade5miscitemtotal = 0;
                decimal grade6miscitemtotal = 0;
                decimal kindermiscitemtotal = 0;
                foreach (var miscitem in summaryMiscFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 0:
                            kindermiscitemtotal += miscitem.Total;
                            break;
                        case 1:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 2:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 3:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 4:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                        case 5:
                            grade5miscitemtotal += miscitem.Total;
                            break;
                        case 6:
                            grade6miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsMisc.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Grade5 = grade5miscitemtotal,
                    Grade6 = grade6miscitemtotal,
                    Kinder = kindermiscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal
                });
                fees.MiscellaneousTotal.Item = "Subotal";
                fees.MiscellaneousTotal.Grade1 += grade1miscitemtotal;
                fees.MiscellaneousTotal.Grade2 += grade2miscitemtotal;
                fees.MiscellaneousTotal.Grade3 += grade3miscitemtotal;
                fees.MiscellaneousTotal.Grade4 += grade4miscitemtotal;
                fees.MiscellaneousTotal.Grade5 += grade5miscitemtotal;
                fees.MiscellaneousTotal.Grade6 += grade6miscitemtotal;
                fees.MiscellaneousTotal.Kinder += kindermiscitemtotal;
                fees.MiscellaneousTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade5 += grade5miscitemtotal;
                fees.TotalAssessmentFee.Grade6 += grade6miscitemtotal;
                fees.TotalAssessmentFee.Kinder += kindermiscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal;
            }

            fees.SupplementalTotal = new FeeSummaryItemElem();
            var suppdescriptions = summarySuppFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in suppdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                decimal grade5miscitemtotal = 0;
                decimal grade6miscitemtotal = 0;
                decimal kindermiscitemtotal = 0;
                foreach (var miscitem in summarySuppFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 0:
                            kindermiscitemtotal += miscitem.Total;
                            break;
                        case 1:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 2:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 3:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 4:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                        case 5:
                            grade5miscitemtotal += miscitem.Total;
                            break;
                        case 6:
                            grade6miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsSupp.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Grade5 = grade5miscitemtotal,
                    Grade6 = grade6miscitemtotal,
                    Kinder = kindermiscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal
                });
                fees.SupplementalTotal.Item = "Subtotal";
                fees.SupplementalTotal.Grade1 += grade1miscitemtotal;
                fees.SupplementalTotal.Grade2 += grade2miscitemtotal;
                fees.SupplementalTotal.Grade3 += grade3miscitemtotal;
                fees.SupplementalTotal.Grade4 += grade4miscitemtotal;
                fees.SupplementalTotal.Grade5 += grade5miscitemtotal;
                fees.SupplementalTotal.Grade6 += grade6miscitemtotal;
                fees.SupplementalTotal.Kinder += kindermiscitemtotal;
                fees.SupplementalTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade5 += grade5miscitemtotal;
                fees.TotalAssessmentFee.Grade6 += grade6miscitemtotal;
                fees.TotalAssessmentFee.Kinder += kindermiscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal;
            }

            fees.OtherTotal = new FeeSummaryItemElem();
            var otherdescriptions = summaryOtherFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in otherdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                decimal grade5miscitemtotal = 0;
                decimal grade6miscitemtotal = 0;
                decimal kindermiscitemtotal = 0;
                foreach (var miscitem in summaryOtherFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 0:
                            kindermiscitemtotal += miscitem.Total;
                            break;
                        case 1:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 2:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 3:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 4:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                        case 5:
                            grade5miscitemtotal += miscitem.Total;
                            break;
                        case 6:
                            grade6miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsOther.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Grade5 = grade5miscitemtotal,
                    Grade6 = grade6miscitemtotal,
                    Kinder = kindermiscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal
                });
                fees.OtherTotal.Item = "Subtotal";
                fees.OtherTotal.Grade1 += grade1miscitemtotal;
                fees.OtherTotal.Grade2 += grade2miscitemtotal;
                fees.OtherTotal.Grade3 += grade3miscitemtotal;
                fees.OtherTotal.Grade4 += grade4miscitemtotal;
                fees.OtherTotal.Grade5 += grade5miscitemtotal;
                fees.OtherTotal.Grade6 += grade6miscitemtotal;
                fees.OtherTotal.Kinder += kindermiscitemtotal;
                fees.OtherTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade5 += grade5miscitemtotal;
                fees.TotalAssessmentFee.Grade6 += grade6miscitemtotal;
                fees.TotalAssessmentFee.Kinder += kindermiscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal;
            }

            fees.LabTotal = new FeeSummaryItemElem();
            var labdescriptions = summaryLabFees.Select(o => o.Description).Distinct().ToList(); ;
            foreach (var miscdecription in labdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                decimal grade5miscitemtotal = 0;
                decimal grade6miscitemtotal = 0;
                decimal kindermiscitemtotal = 0;
                foreach (var miscitem in summaryLabFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 0:
                            kindermiscitemtotal += miscitem.Total;
                            break;
                        case 1:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 2:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 3:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 4:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                        case 5:
                            grade5miscitemtotal += miscitem.Total;
                            break;
                        case 6:
                            kindermiscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsLab.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Grade5 = grade5miscitemtotal,
                    Grade6 = grade6miscitemtotal,
                    Kinder = kindermiscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal
                });
                fees.LabTotal.Item = "Subtotal";
                fees.LabTotal.Grade1 += grade1miscitemtotal;
                fees.LabTotal.Grade2 += grade2miscitemtotal;
                fees.LabTotal.Grade3 += grade3miscitemtotal;
                fees.LabTotal.Grade4 += grade4miscitemtotal;
                fees.LabTotal.Grade5 += grade5miscitemtotal;
                fees.LabTotal.Grade6 += grade6miscitemtotal;
                fees.LabTotal.Kinder += kindermiscitemtotal;
                fees.LabTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade5 += grade5miscitemtotal;
                fees.TotalAssessmentFee.Grade6 += grade6miscitemtotal;
                fees.TotalAssessmentFee.Kinder += kindermiscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal + grade5miscitemtotal + grade6miscitemtotal + kindermiscitemtotal;
            }

            fees.NoOfEnrollees = feeSummaryItemsTotalEnrolles;
            fees.TuitionFee = feeSummaryItemsTuition;
            fees.MiscellaneousFee = feeSummaryItemsMisc.OrderBy(m => m.Item).ToList();
            fees.SupplementalFee = feeSummaryItemsSupp.OrderBy(m => m.Item).ToList();
            fees.OtherFee = feeSummaryItemsOther.OrderBy(m => m.Item).ToList();
            fees.LabFee = feeSummaryItemsLab.OrderBy(m => m.Item).ToList();
            return fees;
        }

        private FeeSummaryElem GetFeeSummaryJHS(int periodid, DateTime asofdate)
        {
            //var assessments = db.Student_Section.Where(m => m.Section.PeriodID == periodid && m.ValidationDate != null && m.ValidationDate <= asofdate && m.StudentStatus.HasValue && m.StudentStatus < 6);
            var summaryNoOfEnrollees = db.GetSummaryOfFees(0, asofdate, periodid).ToList();
            var summaryTuitionFees = db.GetSummaryOfFees(1, asofdate, periodid).ToList();
            var summaryMiscFees = db.GetSummaryOfFees(2, asofdate, periodid).ToList();
            var summarySuppFees = db.GetSummaryOfFees(3, asofdate, periodid).ToList();
            var summaryOtherFees = db.GetSummaryOfFees(4, asofdate, periodid).ToList();
            var summaryLabFees = db.GetSummaryOfFees(5, asofdate, periodid).ToList();
            FeeSummaryElem fees = new FeeSummaryElem();
            List<FeeSummaryItemElem> feeSummaryItemsTotalEnrolles = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsTuition = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsMisc = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsSupp = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsOther = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsLab = new List<FeeSummaryItemElem>();
            fees.TotalAssessmentFee.Item = "Total Assessment Fees";

            int grade1totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 7).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 7).FirstOrDefault().Total : 0;
            int grade2totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 8).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 8).FirstOrDefault().Total : 0;
            int grade3totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 9).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 9).FirstOrDefault().Total : 0;
            int grade4totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 10).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 10).FirstOrDefault().Total : 0;
            feeSummaryItemsTotalEnrolles.Add(new FeeSummaryItemElem
            {
                Item = "Number of Enrollees",
                Grade1 = grade1totalenrolles,
                Grade2 = grade2totalenrolles,
                Grade3 = grade3totalenrolles,
                Grade4 = grade4totalenrolles,
                Total = grade1totalenrolles + grade2totalenrolles + grade3totalenrolles + grade4totalenrolles
            });

            decimal grade1tuitiontotal = 0;
            decimal grade2tuitiontotal = 0;
            decimal grade3tuitiontotal = 0;
            decimal grade4tuitiontotal = 0;
            foreach (var item in summaryTuitionFees)
            {
                switch (item.GradeYear)
                {
                    case 7:
                        grade1tuitiontotal += item.Total;
                        break;
                    case 8:
                        grade2tuitiontotal += item.Total;
                        break;
                    case 9:
                        grade3tuitiontotal += item.Total;
                        break;
                    case 10:
                        grade4tuitiontotal += item.Total;
                        break;
                }
            }
            feeSummaryItemsTuition.Add(new FeeSummaryItemElem
            {
                Item = "Tuition Fee",
                Grade1 = grade1tuitiontotal,
                Grade2 = grade2tuitiontotal,
                Grade3 = grade3tuitiontotal,
                Grade4 = grade4tuitiontotal,
                Total = grade1tuitiontotal + grade2tuitiontotal + grade3tuitiontotal + grade4tuitiontotal
            });
            fees.TotalAssessmentFee.Grade1 += grade1tuitiontotal;
            fees.TotalAssessmentFee.Grade2 += grade2tuitiontotal;
            fees.TotalAssessmentFee.Grade3 += grade3tuitiontotal;
            fees.TotalAssessmentFee.Grade4 += grade4tuitiontotal;
            fees.TotalAssessmentFee.Total += grade1tuitiontotal + grade2tuitiontotal + grade3tuitiontotal + grade4tuitiontotal;

            fees.MiscellaneousTotal = new FeeSummaryItemElem();
            var miscdescriptions = summaryMiscFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in miscdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                foreach (var miscitem in summaryMiscFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 7:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 8:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 9:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 10:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsMisc.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal
                });
                fees.MiscellaneousTotal.Item = "Subotal";
                fees.MiscellaneousTotal.Grade1 += grade1miscitemtotal;
                fees.MiscellaneousTotal.Grade2 += grade2miscitemtotal;
                fees.MiscellaneousTotal.Grade3 += grade3miscitemtotal;
                fees.MiscellaneousTotal.Grade4 += grade4miscitemtotal;
                fees.MiscellaneousTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
            }

            fees.SupplementalTotal = new FeeSummaryItemElem();
            var suppdescriptions = summarySuppFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in suppdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                foreach (var miscitem in summarySuppFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 7:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 8:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 9:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 10:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsSupp.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal
                });
                fees.SupplementalTotal.Item = "Subtotal";
                fees.SupplementalTotal.Grade1 += grade1miscitemtotal;
                fees.SupplementalTotal.Grade2 += grade2miscitemtotal;
                fees.SupplementalTotal.Grade3 += grade3miscitemtotal;
                fees.SupplementalTotal.Grade4 += grade4miscitemtotal;
                fees.SupplementalTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
            }

            fees.OtherTotal = new FeeSummaryItemElem();
            var otherdescriptions = summaryOtherFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in otherdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                foreach (var miscitem in summaryOtherFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 7:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 8:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 9:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 10:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsOther.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal
                });
                fees.OtherTotal.Item = "Subtotal";
                fees.OtherTotal.Grade1 += grade1miscitemtotal;
                fees.OtherTotal.Grade2 += grade2miscitemtotal;
                fees.OtherTotal.Grade3 += grade3miscitemtotal;
                fees.OtherTotal.Grade4 += grade4miscitemtotal;
                fees.OtherTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
            }

            fees.LabTotal = new FeeSummaryItemElem();
            var labdescriptions = summaryLabFees.Select(o => o.Description).Distinct().ToList(); ;
            foreach (var miscdecription in labdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                decimal grade3miscitemtotal = 0;
                decimal grade4miscitemtotal = 0;
                foreach (var miscitem in summaryLabFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 7:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 8:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                        case 9:
                            grade3miscitemtotal += miscitem.Total;
                            break;
                        case 10:
                            grade4miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsLab.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Grade3 = grade3miscitemtotal,
                    Grade4 = grade4miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal
                });
                fees.LabTotal.Item = "Subtotal";
                fees.LabTotal.Grade1 += grade1miscitemtotal;
                fees.LabTotal.Grade2 += grade2miscitemtotal;
                fees.LabTotal.Grade3 += grade3miscitemtotal;
                fees.LabTotal.Grade4 += grade4miscitemtotal;
                fees.LabTotal.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade3 += grade3miscitemtotal;
                fees.TotalAssessmentFee.Grade4 += grade4miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal + grade3miscitemtotal + grade4miscitemtotal;
            }

            fees.NoOfEnrollees = feeSummaryItemsTotalEnrolles;
            fees.TuitionFee = feeSummaryItemsTuition;
            fees.MiscellaneousFee = feeSummaryItemsMisc.OrderBy(m => m.Item).ToList();
            fees.SupplementalFee = feeSummaryItemsSupp.OrderBy(m => m.Item).ToList();
            fees.OtherFee = feeSummaryItemsOther.OrderBy(m => m.Item).ToList();
            fees.LabFee = feeSummaryItemsLab.OrderBy(m => m.Item).ToList();
            return fees;
        }

        private FeeSummaryElem GetFeeSummarySHS(int periodid, DateTime asofdate)
        {
            //var assessments = db.Student_Section.Where(m => m.Section.PeriodID == periodid && m.ValidationDate != null && m.ValidationDate <= asofdate && m.StudentStatus.HasValue && m.StudentStatus < 6);
            var summaryNoOfEnrollees = db.GetSummaryOfFees(0, asofdate, periodid).ToList();
            var summaryTuitionFees = db.GetSummaryOfFees(1, asofdate, periodid).ToList();
            var summaryMiscFees = db.GetSummaryOfFees(2, asofdate, periodid).ToList();
            var summarySuppFees = db.GetSummaryOfFees(3, asofdate, periodid).ToList();
            var summaryOtherFees = db.GetSummaryOfFees(4, asofdate, periodid).ToList();
            var summaryLabFees = db.GetSummaryOfFees(5, asofdate, periodid).ToList();
            db.GetSummaryOfFees(1, asofdate, periodid);
            FeeSummaryElem fees = new FeeSummaryElem();
            List<FeeSummaryItemElem> feeSummaryItemsTotalEnrolles = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsTuition = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsMisc = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsSupp = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsOther = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsLab = new List<FeeSummaryItemElem>();
            fees.TotalAssessmentFee.Item = "Total Assessment Fees";

            int grade1totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 11).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 11).FirstOrDefault().Total : 0;
            int grade2totalenrolles = summaryNoOfEnrollees.Where(m => m.GradeYear == 12).FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Where(m => m.GradeYear == 12).FirstOrDefault().Total : 0;
            feeSummaryItemsTotalEnrolles.Add(new FeeSummaryItemElem
            {
                Item = "Number of Enrollees",
                Grade1 = grade1totalenrolles,
                Grade2 = grade2totalenrolles,
                Total = grade1totalenrolles + grade2totalenrolles
            });

            decimal grade1tuitiontotal = 0;
            decimal grade2tuitiontotal = 0;
            foreach (var item in summaryTuitionFees)
            {
                switch (item.GradeYear)
                {
                    case 11:
                        grade1tuitiontotal += item.Total;
                        break;
                    case 12:
                        grade2tuitiontotal += item.Total;
                        break;
                }
            }
            feeSummaryItemsTuition.Add(new FeeSummaryItemElem
            {
                Item = "Tuition Fee",
                Grade1 = grade1tuitiontotal,
                Grade2 = grade2tuitiontotal,
                Total = grade1tuitiontotal + grade2tuitiontotal
            });
            fees.TotalAssessmentFee.Grade1 += grade1tuitiontotal;
            fees.TotalAssessmentFee.Grade2 += grade2tuitiontotal;
            fees.TotalAssessmentFee.Total += grade1tuitiontotal + grade2tuitiontotal;

            fees.MiscellaneousTotal = new FeeSummaryItemElem();
            var miscdescriptions = summaryMiscFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in miscdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                foreach (var miscitem in summaryMiscFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 11:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 12:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsMisc.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal
                });
                fees.MiscellaneousTotal.Item = "Subotal";
                fees.MiscellaneousTotal.Grade1 += grade1miscitemtotal;
                fees.MiscellaneousTotal.Grade2 += grade2miscitemtotal;
                fees.MiscellaneousTotal.Total += grade1miscitemtotal + grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal;
            }

            fees.SupplementalTotal = new FeeSummaryItemElem();
            var suppdescriptions = summarySuppFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in suppdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                foreach (var miscitem in summarySuppFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 11:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 12:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsSupp.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal
                });
                fees.SupplementalTotal.Item = "Subtotal";
                fees.SupplementalTotal.Grade1 += grade1miscitemtotal;
                fees.SupplementalTotal.Grade2 += grade2miscitemtotal;
                fees.SupplementalTotal.Total += grade1miscitemtotal + grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal;
            }

            fees.OtherTotal = new FeeSummaryItemElem();
            var otherdescriptions = summaryOtherFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in otherdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                foreach (var miscitem in summaryOtherFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 11:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 12:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsOther.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal
                });
                fees.OtherTotal.Item = "Subtotal";
                fees.OtherTotal.Grade1 += grade1miscitemtotal;
                fees.OtherTotal.Grade2 += grade2miscitemtotal;
                fees.OtherTotal.Total += grade1miscitemtotal + grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal;
            }

            fees.LabTotal = new FeeSummaryItemElem();
            var labdescriptions = summaryLabFees.Select(o => o.Description).Distinct().ToList(); ;
            foreach (var miscdecription in labdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                decimal grade2miscitemtotal = 0;
                foreach (var miscitem in summaryLabFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    switch (miscitem.GradeYear)
                    {
                        case 11:
                            grade1miscitemtotal += miscitem.Total;
                            break;
                        case 12:
                            grade2miscitemtotal += miscitem.Total;
                            break;
                    }

                }
                feeSummaryItemsLab.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Grade2 = grade2miscitemtotal,
                    Total = grade1miscitemtotal + grade2miscitemtotal
                });
                fees.LabTotal.Item = "Subtotal";
                fees.LabTotal.Grade1 += grade1miscitemtotal;
                fees.LabTotal.Grade2 += grade2miscitemtotal;
                fees.LabTotal.Total += grade1miscitemtotal + grade2miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade2 += grade2miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal + grade2miscitemtotal;
            }

            fees.NoOfEnrollees = feeSummaryItemsTotalEnrolles;
            fees.TuitionFee = feeSummaryItemsTuition;
            fees.MiscellaneousFee = feeSummaryItemsMisc.OrderBy(m => m.Item).ToList();
            fees.SupplementalFee = feeSummaryItemsSupp.OrderBy(m => m.Item).ToList();
            fees.OtherFee = feeSummaryItemsOther.OrderBy(m => m.Item).ToList();
            fees.LabFee = feeSummaryItemsLab.OrderBy(m => m.Item).ToList();
            return fees;
        }

        private FeeSummaryElem GetFeeSummaryGS(int periodid, DateTime asofdate)
        {
            //var assessments = db.Student_Section.Where(m => m.Section.PeriodID == periodid && m.ValidationDate != null && m.ValidationDate <= asofdate && m.StudentStatus.HasValue && m.StudentStatus < 6);
            var summaryNoOfEnrollees = db.GetSummaryOfFees(0, asofdate, periodid).ToList();
            var summaryTuitionFees = db.GetSummaryOfFees(1, asofdate, periodid).ToList();
            var summaryMiscFees = db.GetSummaryOfFees(2, asofdate, periodid).ToList();
            var summarySuppFees = db.GetSummaryOfFees(3, asofdate, periodid).ToList();
            var summaryOtherFees = db.GetSummaryOfFees(4, asofdate, periodid).ToList();
            var summaryLabFees = db.GetSummaryOfFees(5, asofdate, periodid).ToList();
            db.GetSummaryOfFees(1, asofdate, periodid);
            FeeSummaryElem fees = new FeeSummaryElem();
            List<FeeSummaryItemElem> feeSummaryItemsTotalEnrolles = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsTuition = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsMisc = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsSupp = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsOther = new List<FeeSummaryItemElem>();
            List<FeeSummaryItemElem> feeSummaryItemsLab = new List<FeeSummaryItemElem>();
            fees.TotalAssessmentFee.Item = "Total Assessment Fees";

            int grade1totalenrolles = summaryNoOfEnrollees.FirstOrDefault() != null ? (int)summaryNoOfEnrollees.Sum(m => m.Total) : 0;
            feeSummaryItemsTotalEnrolles.Add(new FeeSummaryItemElem
            {
                Item = "Number of Enrollees",
                Grade1 = grade1totalenrolles,
                Total = grade1totalenrolles
            });

            decimal grade1tuitiontotal = 0;
            foreach (var item in summaryTuitionFees)
            {
                grade1tuitiontotal += item.Total;
            }
            feeSummaryItemsTuition.Add(new FeeSummaryItemElem
            {
                Item = "Tuition Fee",
                Grade1 = grade1tuitiontotal,
                Total = grade1tuitiontotal
            });
            fees.TotalAssessmentFee.Grade1 += grade1tuitiontotal;
            fees.TotalAssessmentFee.Total += grade1tuitiontotal;

            fees.MiscellaneousTotal = new FeeSummaryItemElem();
            var miscdescriptions = summaryMiscFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in miscdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                foreach (var miscitem in summaryMiscFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    grade1miscitemtotal += miscitem.Total;
                }
                feeSummaryItemsMisc.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Total = grade1miscitemtotal
                });
                fees.MiscellaneousTotal.Item = "Subotal";
                fees.MiscellaneousTotal.Grade1 += grade1miscitemtotal;
                fees.MiscellaneousTotal.Total += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal;
            }

            fees.SupplementalTotal = new FeeSummaryItemElem();
            var suppdescriptions = summarySuppFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in suppdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                foreach (var miscitem in summarySuppFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    grade1miscitemtotal += miscitem.Total;
                }
                feeSummaryItemsSupp.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Total = grade1miscitemtotal
                });
                fees.SupplementalTotal.Item = "Subtotal";
                fees.SupplementalTotal.Grade1 += grade1miscitemtotal;
                fees.SupplementalTotal.Total += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal;
            }

            fees.OtherTotal = new FeeSummaryItemElem();
            var otherdescriptions = summaryOtherFees.Select(o => o.Description).Distinct().ToList();
            foreach (var miscdecription in otherdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                foreach (var miscitem in summaryOtherFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    grade1miscitemtotal += miscitem.Total;
                }
                feeSummaryItemsOther.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Total = grade1miscitemtotal
                });
                fees.OtherTotal.Item = "Subtotal";
                fees.OtherTotal.Grade1 += grade1miscitemtotal;
                fees.OtherTotal.Total += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal;
            }

            fees.LabTotal = new FeeSummaryItemElem();
            var labdescriptions = summaryLabFees.Select(o => o.Description).Distinct().ToList(); ;
            foreach (var miscdecription in labdescriptions)
            {
                decimal grade1miscitemtotal = 0;
                foreach (var miscitem in summaryLabFees.Where(m => m.Description.Equals(miscdecription)))
                {
                    grade1miscitemtotal += miscitem.Total;
                }
                feeSummaryItemsLab.Add(new FeeSummaryItemElem
                {
                    Item = miscdecription,
                    Grade1 = grade1miscitemtotal,
                    Total = grade1miscitemtotal
                });
                fees.LabTotal.Item = "Subtotal";
                fees.LabTotal.Grade1 += grade1miscitemtotal;
                fees.LabTotal.Total += grade1miscitemtotal;
                fees.TotalAssessmentFee.Grade1 += grade1miscitemtotal;
                fees.TotalAssessmentFee.Total += grade1miscitemtotal;
            }

            fees.NoOfEnrollees = feeSummaryItemsTotalEnrolles;
            fees.TuitionFee = feeSummaryItemsTuition;
            fees.MiscellaneousFee = feeSummaryItemsMisc.OrderBy(m => m.Item).ToList();
            fees.SupplementalFee = feeSummaryItemsSupp.OrderBy(m => m.Item).ToList();
            fees.OtherFee = feeSummaryItemsOther.OrderBy(m => m.Item).ToList();
            fees.LabFee = feeSummaryItemsLab.OrderBy(m => m.Item).ToList();
            return fees;
        }
        #endregion SummaryOfFees
        public ActionResult EnrollmentStat()
        {
            return View();
        }

        public ActionResult PrintEnrollmentStat(int pid, DateTime sdate)
        {
            try
            {
                SqlConnectionStringBuilder SConn = new SqlConnectionStringBuilder(ConfigurationManager.ConnectionStrings["DefaultConnection"].ToString());
                //document.ExportToStream(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat), "SOA.pdf"
                LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
                var userWithClaims = (System.Security.Claims.ClaimsPrincipal)User;
                var fullname = userWithClaims.Claims.First(c => c.Type == "Fullname");
                var sec = db.Period.Find(pid);
                string reportPath = Path.Combine(Server.MapPath("~/Reports"), "EnrollmentComparativeReport1.rpt");
                ReportDocument reportDocument = new ReportDocument();
                reportDocument.Load(reportPath);
                //reportDocument.SetDatabaseLogon("softrack", "softrack");
                reportDocument.SetDatabaseLogon(SConn.UserID, SConn.Password, "LISDB", SConn.InitialCatalog);
                reportDocument.SetParameterValue("@periodid", pid);
                reportDocument.SetParameterValue("@sdate", sdate);
                //CrystalDecisions.CrystalReports.Engine.TextObject txtperiod, txtdate, txtby;
                //txtperiod = reportDocument.ReportDefinition.ReportObjects["txtperiod"] as TextObject;
                //txtby = reportDocument.ReportDefinition.ReportObjects["txtby"] as TextObject;
                //txtby.Text = fullname.Value;
                //txtperiod.Text = sec.SchoolYear.SchoolYearName + " " + sec.Period1;
                //txtdate = reportDocument.ReportDefinition.ReportObjects["txtdate"] as TextObject;
                //txtdate.Text = "as of " + sdate.ToLongDateString();


                //Direct Print
                //reportDocument.PrintToPrinter(1, true, 0, 0);

                //return new CrystalReportPdfResult("Employee", reportDocument);
                return ExportType(1, "EnrollmentStatistics_" + sdate.Date.ToString("dd MMMM yyyy"), reportDocument);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //public ActionResult EnrollmentStat2()
        //{
        //    return View();
        //}

        public ActionResult EnrollmentStatPartial(int periodid, DateTime edate)
        {
            ViewBag.PeriodID = periodid;
            ViewBag.EDate = edate.Date;
            List<GetEnrollmentStat2_Result> stat = new List<GetEnrollmentStat2_Result>();
            HttpResponseMessage response = client.GetAsync(client.BaseAddress + "/enrollmentstat/" + periodid + "/" + edate.ToString("yyyy-MM-dd")).Result;

            if (response.IsSuccessStatusCode)
            {
                string data = response.Content.ReadAsStringAsync().Result;
                stat = JsonConvert.DeserializeObject<List<GetEnrollmentStat2_Result>>(data).ToList();
            }

            GetEnrollmentStat2_Result stattotal = new GetEnrollmentStat2_Result();

            stattotal.AssTodate = 0;
            stattotal.Projected = 0;
            stattotal.AssessmentToDateFCount = 0;
            stattotal.AssToday = 0;
            stattotal.AssessmentTodayFCount = 0;
            stattotal.EnrTodate = 0;
            stattotal.EnrollmentToDateFCount = 0;
            stattotal.EnrToday = 0;
            stattotal.EnrollmentTodayFCount = 0;
            stattotal.Cancelled = 0;
            stattotal.Cash = 0;
            stattotal.Installment = 0;
            stattotal.Monthky = 0;
            stattotal.Total = 0;
            foreach (var total in stat)
            {
                stattotal.AssTodate += total.AssTodate;
                stattotal.Projected += total.Projected;
                stattotal.AssessmentToDateFCount += total.AssessmentToDateFCount;
                stattotal.AssToday += total.AssToday;
                stattotal.AssessmentTodayFCount += total.AssessmentTodayFCount;
                stattotal.EnrTodate += total.EnrTodate;
                stattotal.EnrollmentToDateFCount += total.EnrollmentToDateFCount;
                stattotal.EnrToday += total.EnrToday;
                stattotal.EnrollmentTodayFCount += total.EnrollmentTodayFCount;
                stattotal.Cancelled += total.Cancelled;
                stattotal.Cash += total.Cash;
                stattotal.Installment += total.Installment;
                stattotal.Monthky += total.Monthky;
                stattotal.Total += total.Total;

            }
            ViewBag.StatTotal = stattotal;

            return PartialView("_EnrollmentStatPartial", stat);
        }

        public ActionResult Index()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var reports = new List<SelectListItem>();
            foreach (var item in db.ReportName)
            {
                reports.Add(new SelectListItem
                {
                    Text = item.ReportName1,
                    Value = item.ReportNameId.ToString()
                });
            }
            ViewBag.ReportName = reports;
            return View();
        }
        [HttpPost]
        public ActionResult ViewReports(DateTime startdate, DateTime enddate, int ReportNameId, int reporttype)
        {
            var username = User.Identity.Name;
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var employee = db.Employee.Where(m => m.EmployeeNo == username).FirstOrDefault();
            if (ReportNameId == 1)
                return RedirectToAction("BankDepositJournalEntry", new { startdate = startdate, enddate = enddate, reporttype = reporttype });
            else if (ReportNameId == 2)
                return RedirectToAction("BankDeposit", new { startdate = startdate, enddate = enddate, reporttype = reporttype });
            else
            {
                switch (ReportNameId)
                {
                    case 1:
                        List<BankDepositJournalEntry> reportdata = GetBankDepositJournalEntry(startdate, enddate, employee == null ? 0 : employee.EmployeeDepartmentID.Value);
                        return View("BankDepositJournalEntry", reportdata);
                    case 2:
                        return View();
                    default:
                        return View();
                }

            }
        }

        public ActionResult BankDepositJournalEntry(DateTime startdate, DateTime enddate, int reporttype)
        {
            List<BankDepositJournalEntry> reportdata = GetBankDepositJournalEntry(startdate, enddate, 1);
            using (ReportDocument document = new BankDepositReceiptJournalEntryReport())
            {
                document.SetDataSource(reportdata);
                if (reporttype == 1)
                    return ExportType(1, "BankDepositJournalEntry_" + DateTime.Today.ToShortDateString(), document);
                else if (reporttype == 2)
                    return ExportType(2, "BankDepositJournalEntry_" + DateTime.Today.ToShortDateString(), document);
                else
                    return View(reportdata);
            }
        }

        private static List<BankDepositJournalEntry> GetBankDepositJournalEntry(DateTime startdate, DateTime enddate, int departmentid)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            db.Database.CommandTimeout = 600;
            var journalentries = db.MakeBankDepositJournalEntry(startdate, enddate, departmentid);
            List<BankDepositJournalEntry> reportdata = new List<BankDepositJournalEntry>();
            foreach (var item in journalentries)
            {
                reportdata.Add(new BankDepositJournalEntry
                {
                    AccountName = item.AccountName,
                    AccountNumber = item.AccountNo,
                    Bank = item.Bank,
                    Credit = (decimal)(item.Credit ?? 0),
                    Debit = item.Debit,
                    DepartmentAcronym = item.AcaAcronym,
                    Description = item.Description,
                    EndDate = enddate,
                    StartDate = startdate
                });
            }

            return reportdata;
        }

        public ActionResult BankDeposit(DateTime startdate, DateTime enddate, int reporttype)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            db.Database.CommandTimeout = 600;
            var employee = db.Employee.Where(m => m.EmployeeNo == User.Identity.Name).FirstOrDefault();
            var deposits = db.GetBankDeposit(startdate, enddate, employee.EmployeeDepartmentID);
            List<BankDeposit> reportdata = new List<BankDeposit>();
            foreach (var item in deposits)
            {
                reportdata.Add(new BankDeposit
                {
                    Bank = item.Bank,
                    Description = item.Description,
                    EndDate = enddate,
                    StartDate = startdate,
                    Amount = (decimal)(item.Amount ?? 0),
                    BankDate = item.BankDate.HasValue ? item.BankDate.Value.ToShortDateString() : "",
                    Code = item.Code,
                    DateReceived = item.DateReceived.ToShortDateString(),
                    Department = item.Department,
                    Employee = item.Employee,
                    OrNo = item.ORNo,
                    PaycodeId = item.PaycodeID ?? 0,
                    PaymentId = item.PaymentID,
                    PaymentTotal = (decimal)item.PaymentTotal,
                    Remarks = item.Remarks,
                    StudentName = item.StudentName,
                    StudentNumber = item.StudentNo,
                    PaycodeType = item.PaycodeID < 10 ? "Tuition Fees" : (item.PaycodeID == 10 ? "Processing Fee" : (item.PaycodeID == 11 ? "Back Account" : "Other Fees"))
                });
            }
            using (ReportDocument document = new BankDepositReport())
            {
                document.Subreports["summarybybank"].SetDataSource(reportdata);
                document.Subreports["summarybydept"].SetDataSource(reportdata);
                document.Subreports["summarybypaycode"].SetDataSource(reportdata);
                document.Subreports["summary"].SetDataSource(reportdata);
                document.SetDataSource(reportdata);
                if (reporttype == 1)
                    return ExportType(1, "BankDeposit_" + DateTime.Today.ToShortDateString(), document);
                else if (reporttype == 2)
                    return ExportType(2, "BankDeposit_" + DateTime.Today.ToShortDateString(), document);
                else
                    return View(reportdata);
            }
        }

    }
}