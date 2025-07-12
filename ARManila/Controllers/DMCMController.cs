using ARManila.Models;
using ARManila.Models.ReportsDTO;
using ARManila.Reports;
using CrystalDecisions.CrystalReports.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace ARManila.Controllers
{
    [Authorize]
    public class DMCMController : BaseController
    {
        LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();

        public ActionResult Discount()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var dmcmdiscounts = db.DMCM.Where(m => m.AccountNumber.StartsWith("E") && m.PeriodID == periodid && m.ChargeToStudentAr==false);
            return View(dmcmdiscounts);
        }
        public ActionResult DiscountDetail(int id)
        {
            var discountdetail = db.DmcmDiscountDetail.Where(m => m.DmcmId == id);
            return View();
        }
        public ActionResult DeleteDMCM(int id)
        {
            var start = id - 3;
            var last = id + 3;
            var dmcms = db.DMCM.Where(m => m.DocNum == id).ToList();
            foreach (var item in dmcms)
            {
                db.InsertDmcmTransactionLog(User.Identity.Name, "Delete DMCM - " + item.DC + " " + item.AccountNumber + " - " + item.Amount + " - " + item.Remarks, item.Student.StudentNo);
            }
            db.DMCM.RemoveRange(db.DMCM.Where(m => m.DocNum == id));
            db.SaveChanges();
            return RedirectToAction("ListDMCM", new { start = start, last = last });
        }
        public ActionResult BatchDMCM()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.sections = db.Section.Where(m => m.PeriodID == periodid).Select(m => new { SectionId = m.SectionID, SectionName = m.SectionName, GradeLevel = m.GradeYear, ProgramId = m.Curriculum.ProgamCurriculum.FirstOrDefault().ProgramID }).OrderBy(m => m.SectionName);
            ViewBag.gradeyears = new SelectList(db.GradeLevel.Where(m => m.EducationalLevelId == period.EducLevelID).OrderBy(m => m.SectionGradeYear), "SectionGradeYear", "GradeLevelName");
            //ViewBag.sectionsubjects = db.Schedule.Where(m => m.Section.PeriodID == periodid).Select(m => new { SubjectCode = m.Subject.SubjectCode, SectionId = m.SectionID, Description = m.Subject.Description, SubjectId= m.SubjectID });
            ViewBag.subjects = db.Schedule.Where(m => m.Section.PeriodID == periodid).Select(m => new { ScheduleId = m.ScheduleID, SubjectCode = m.Subject.SubjectCode + "-" + m.Section.SectionName, Description = m.Subject.Description, SubjectId = m.SubjectID, ProgramId = m.Section.Curriculum.ProgamCurriculum.FirstOrDefault().ProgramID, GradeLevel = m.Section.GradeYear, SectionId = m.SectionID }).OrderBy(m => m.SubjectCode);
            var curriculumids = db.Section.Where(m => m.PeriodID == periodid).Select(m => m.CurriculumID).Distinct().ToList();
            var programids = db.ProgamCurriculum.Where(m => curriculumids.Contains(m.CurriculumID)).Select(m => m.ProgramID).ToList();
            ViewBag.programs = new SelectList(db.Progam.Where(m => programids.Contains(m.ProgramID)).OrderBy(m => m.ProgramCode), "ProgramID", "ProgramCode");
            ViewBag.accounts = new SelectList(db.ChartOfAccounts.OrderBy(m => m.AcctName), "AcctID", "FullName");
            ViewBag.subaccounts = db.SubChartOfAccounts.Select(m => new { AcctID = m.AcctID, SubAcctID = m.SubAcctID, SubbAcctName = m.SubbAcctName }).OrderBy(m => m.SubbAcctName);
            return View();
        }
        [HttpPost]
        public ActionResult BatchDMCM(int? sectionid, int? scheduleid, int? programid, int? gradeyear)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.sections = db.Section.Where(m => m.PeriodID == periodid).Select(m => new { SectionId = m.SectionID, SectionName = m.SectionName, GradeLevel = m.GradeYear, ProgramId = m.Curriculum.ProgamCurriculum.FirstOrDefault().ProgramID }).OrderBy(m => m.SectionName);
            ViewBag.gradeyears = new SelectList(db.GradeLevel.Where(m => m.EducationalLevelId == period.EducLevelID).OrderBy(m => m.SectionGradeYear), "SectionGradeYear", "GradeLevelName");
            ViewBag.subjects = db.Schedule.Where(m => m.Section.PeriodID == periodid).Select(m => new { ScheduleId = m.ScheduleID, SubjectCode = m.Subject.SubjectCode + "-" + m.Section.SectionName, Description = m.Subject.Description, SubjectId = m.SubjectID, ProgramId = m.Section.Curriculum.ProgamCurriculum.FirstOrDefault().ProgramID, GradeLevel = m.Section.GradeYear, SectionId = m.SectionID }).OrderBy(m => m.SubjectCode);
            var curriculumids = db.Section.Where(m => m.PeriodID == periodid).Select(m => m.CurriculumID).Distinct().ToList();
            var programids = db.ProgamCurriculum.Where(m => curriculumids.Contains(m.CurriculumID)).Select(m => m.ProgramID).ToList();
            ViewBag.programs = new SelectList(db.Progam.Where(m => programids.Contains(m.ProgramID)).OrderBy(m => m.ProgramCode), "ProgramID", "ProgramCode");

            ViewBag.accounts = new SelectList(db.ChartOfAccounts.OrderBy(m => m.AcctName), "AcctID", "FullName");
            ViewBag.subaccounts = new SelectList(db.SubChartOfAccounts.OrderBy(m => m.SubbAcctName), "SubAcctID", "SubbAcctName");
             
            BatchDmcm batch = new BatchDmcm();
            if (scheduleid.HasValue)
            {
                var schedule = db.Schedule.Find(scheduleid.Value);
                var students = db.Student_Section.Where(m => m.ValidationDate.HasValue && m.StudentSchedule.Any(x => x.ScheduleID == scheduleid)).ToList();
                batch.Particular = schedule.Subject.SubjectCode;
                batch.Students = students.OrderBy(m => m.Student.FullName).ToList();
            }
            else if (sectionid.HasValue)
            {
                var students = db.Student_Section.Where(m => m.ValidationDate.HasValue && m.SectionID == sectionid).ToList();
                batch.Students = students.OrderBy(m => m.Student.FullName).ToList(); ;
            }
            else
            {
                if (programid.HasValue && gradeyear.HasValue)
                {
                    var students = db.Student_Section.Where(m => m.Section.Curriculum.ProgamCurriculum.Any(x => x.ProgramID == programid) && m.Section.GradeYear == gradeyear && m.ValidationDate.HasValue && m.Section.PeriodID == periodid).ToList();
                    batch.Students = students.OrderBy(m => m.Student.FullName).ToList();
                }
                else if (programid.HasValue)
                {
                    var students = db.Student_Section.Where(m => m.Section.Curriculum.ProgamCurriculum.Any(x => x.ProgramID == programid) && m.ValidationDate.HasValue && m.Section.PeriodID == periodid).ToList();
                    batch.Students = students.OrderBy(m => m.Student.FullName).ToList();
                }
                else if (gradeyear.HasValue)
                {
                    var students = db.Student_Section.Where(m => m.Section.GradeYear == gradeyear && m.ValidationDate.HasValue && m.Section.PeriodID == periodid).ToList();
                    batch.Students = students.OrderBy(m => m.Student.FullName).ToList();
                }
                else
                {
                    var students = db.Student_Section.Where(m => m.Section.PeriodID == periodid && m.ValidationDate.HasValue).ToList();
                    batch.Students = students.OrderBy(m => m.Student.FullName).ToList();
                }
            }
            return View(batch);
        }

        public ActionResult ListDMCM(int start, int last)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var dmcms = db.DMCM.Where(m => m.DocNum >= start && m.DocNum <= last && m.ChargeToStudentAr == false);
            return View("Index", dmcms);
        }

        public ActionResult TransactionLog()
        {
            var transactions = db.DmcmTransactionLog.OrderByDescending(m => m.TransactionDate);
            return View(transactions);
        }

        [HttpPost]
        public ActionResult PostBatchDMCM(BatchDmcm model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var account = model.AccountId == 0 ? null : db.ChartOfAccounts.Find(model.AccountId);
            var subaccount = model.SubaccountId == 0 ? null : db.SubChartOfAccounts.Find(model.SubaccountId);
            int syaccountid = period.SchoolYear.ChartOfAccountId ?? 0;
            if (syaccountid == 0)
            {
                var schoolyearCOA = db.ChartOfAccounts.Where(m => m.SYID == period.SchoolYearID).FirstOrDefault();
                if (schoolyearCOA == null)
                {
                    throw new Exception("School Year has no chart of account.");
                }
                else
                {
                    syaccountid = schoolyearCOA.AcctID;
                }
            }
            var araccount = syaccountid == 0 ? db.ChartOfAccounts.Find(syaccountid) : db.ChartOfAccounts.Find(syaccountid);
            var docnumlast = db.DMCM.OrderByDescending(m => m.DocNum).FirstOrDefault().DocNum.Value;
            var start = docnumlast;

            if (model.IsDebit)
            {
                foreach (var i in model.Students)
                {
                    if (i.IsSelected)
                    {
                        //docnumlast = PostDebitMemo(model, periodid, account, subaccount, araccount, docnumlast, i);
                        docnumlast = DmcmTransaction.PostDebitMemo(User.Identity.Name, docnumlast, (double)model.Amount, model.Particular, model.PostingDate, periodid, i.StudentID, i.Section.Curriculum.AcaDeptID.Value, account, subaccount, araccount);
                        EMailPostedDMCM(docnumlast);
                    }
                }
                return RedirectToAction("ListDMCM", new { start = start + 1, last = docnumlast });
            }
            else
            {
                foreach (var i in model.Students)
                {
                    if (i.IsSelected)
                    {
                        //docnumlast = PostCreditMemo(model, periodid, account, subaccount, araccount, docnumlast, i);
                        docnumlast = DmcmTransaction.PostCreditMemo(User.Identity.Name, docnumlast, (double)model.Amount, model.Particular, model.PostingDate, periodid, i.StudentID, i.Section.Curriculum.AcaDeptID.Value, account, subaccount, araccount);
                        EMailPostedDMCM(docnumlast);
                    }
                }
                return RedirectToAction("ListDMCM", new { start = start + 1, last = docnumlast });
            }

        }


        public ActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Index(string? studentno, DateTime? startdate, DateTime? enddate)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            var period = db.Period.Find(periodid);
            ViewBag.acctno = period.SchoolYear.ChartOfAccountId.HasValue ? period.SchoolYear.ChartOfAccounts.FirstOrDefault().AcctNo : "N/A";
            ViewBag.acctname = period.SchoolYear.ChartOfAccountId.HasValue ? period.SchoolYear.ChartOfAccounts.FirstOrDefault().AcctName : "N/A";
            if (studentno != null && studentno.Length > 0)
            {
                IQueryable<DMCM> dmcms = GetStudentDMCM(studentno);
                return View(dmcms);
            }
            else
            {
                DateTime sdate = startdate.HasValue ? startdate.Value : DateTime.Today;
                IQueryable<DMCM> dmcms = GetDMCMList(sdate, enddate);
                return View(dmcms);
            }
        }

        private IQueryable<DMCM> GetStudentDMCM(string studentno)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var student = db.Student.Where(m => m.StudentNo == studentno).FirstOrDefault();
            if (student == null) throw new Exception("Invalid student number.");
            var dmcms = db.DMCM.Where(m => m.StudentID == student.StudentID && m.ChargeToStudentAr == false && m.PeriodID == period.PeriodID);
            return dmcms;
        }
        private IQueryable<DMCM> GetDMCMList(DateTime startdate, DateTime? enddate)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value.ToString());
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            DateTime edate = enddate.HasValue ? enddate.Value : startdate.AddDays(1);
            var dmcms = db.DMCM.Where(m => m.ChargeToStudentAr == false && m.PeriodID == period.PeriodID && m.TransactionDate < edate && m.TransactionDate >= startdate);
            return dmcms;
        }

        public ActionResult ViewReport(int id)
        {
            var userWithClaims = (System.Security.Claims.ClaimsPrincipal)User;
            var fullname = userWithClaims.Claims.First(c => c.Type == "Fullname");
            using (ReportDocument document = new DMCMReport())
            {
                List<DMCMReportDTO> reportdata = GenerateDMCMReport(id, fullname);
                document.SetDataSource(reportdata);
                return ExportType(1, "DMCM_" + id, document);
            }
        }

        private List<DMCMReportDTO> GenerateDMCMReport(int id, System.Security.Claims.Claim fullname)
        {
            List<DMCMReportDTO> reportdata = new List<DMCMReportDTO>();
            var dmcms = db.GetDMCMByNo(id);
            foreach (var i in dmcms)
            {
                reportdata.Add(new DMCMReportDTO
                {
                    AccountName = i.AcctName,
                    Amount = i.Amount ?? 0,
                    Credit = (decimal)(i.Credit ?? 0),
                    Debit = (decimal)(i.Debit ?? 0),
                    Curriculum = i.Curriculum,
                    DocumentNumber = i.DocNum ?? 0,
                    EducationalLevelName = i.EducLevelName,
                    Message = i.Message,
                    PeriodName = i.Period + ", " + i.SchoolYearName,
                    PreparedBy = fullname.Value,
                    Remarks = i.Remarks,
                    StudentName = i.StudentName,
                    StudentNumber = i.StudentNo,
                    TransactionDate = i.TransactionDate.Value,
                    Type = i.Type.FirstOrDefault()
                });
            }

            return reportdata;
        }

        public int EMailPostedDMCM(int id)
        {
            try
            {
                using (ReportDocument document = new DMCMReport())
                {
                    var user = db.AspNetUsers.Where(m => m.UserName == User.Identity.Name).FirstOrDefault();
                    var userWithClaims = (System.Security.Claims.ClaimsPrincipal)User;
                    var fullname = userWithClaims.Claims.First(c => c.Type == "Fullname");
                    var dmcm = db.DMCM.Where(m => m.DocNum == id).FirstOrDefault();
                    var student = db.Student.Where(m => m.StudentID == dmcm.StudentID).FirstOrDefault();
                    var studentemail = db.AspNetUsers.Where(m => m.UserName == student.StudentNo).FirstOrDefault();
                    MailMessage mail = new MailMessage();
                    SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
                    var fromAddress = new MailAddress("admin@letran.edu.ph", "System Admin");
                    const string fromPassword = "Boo18!<3";
                    mail.From = fromAddress;
                    mail.CC.Add(new MailAddress(user.Email));
                    if (db.Database.Connection.ConnectionString.Contains("172.20.0.10"))
                    {
                        mail.To.Add("christopher.seno@letran.edu.ph");
                    }
                    else
                    {
                        mail.To.Add(studentemail.Email);
                    }
                    mail.CC.Add("arletran@letran.edu.ph");
                    //mail.To.Add("christopher.seno@letran.edu.ph");
                    mail.Body = "Please see the attached debit or credit memo.";
                    mail.Subject = "Debit/Credit Memo";
                    mail.IsBodyHtml = true;

                    List<DMCMReportDTO> reportdata = GenerateDMCMReport(id, fullname);
                    document.SetDataSource(reportdata);
                    mail.Attachments.Add(new Attachment(document.ExportToStream(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat), "Dmcm.pdf"));

                    mail.IsBodyHtml = true;
                    SmtpServer.Port = 587;
                    //SmtpServer.Credentials = new System.Net.NetworkCredential("admin@letran.edu.ph", fromPassword);
                    SmtpServer.Credentials = new System.Net.NetworkCredential("admin@letran.edu.ph", "dfws xjjr tmng ekkp");
                    SmtpServer.EnableSsl = true;
                    SmtpServer.Send(mail);
                    IQueryable<DMCM> dmcms = GetStudentDMCM(dmcm.Student.StudentNo);
                    return id;
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}