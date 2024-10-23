using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Web;
using System.Web.Http.Results;
using System.Web.Mvc;
using ARManila.Models;
using ARManila.Models.ReportsDTO;
using ARManila.Reports;
using CrystalDecisions.CrystalReports.Engine;

namespace ARManila.Controllers
{
    public partial class ReassessmentController : BaseController
    {
        LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {
            return View();
        }
       
        [HttpPost]
        public ActionResult Index(string studentno, string usedefault)
        {
            var periodid = usedefault != null && usedefault.Equals("on") ? "0" : HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            var period = int.Parse(periodid);
            var student = db.Student.FirstOrDefault(m => m.StudentNo.Equals(studentno));
            if (student == null) throw new Exception("Invalid Student Number");
            var enrollment = db.Student_Section.FirstOrDefault(m => m.Section.PeriodID == period && m.ValidationDate != null && m.StudentID == student.StudentID);
            if (enrollment == null) throw new Exception("Student is not currently enrolled.");
            var originalschedule = db.OriginalStudentSchedule.Where(m => m.StudentSectionID == enrollment.Student_SectionID).ToList();
            var items = new List<StudentScheduleWrapper>();
            if (originalschedule == null || originalschedule.Count == 0)
            {
                List<StudentSchedule> studentscheduletobesaved = new List<StudentSchedule>();
                var hasadjustment = db.Adjustment.Any(m => m.StudentSectionID == enrollment.Student_SectionID);
                var initialschedules = db.StudentSchedule.Where(m => m.StudentSectionID == enrollment.Student_SectionID);
                foreach (var item in initialschedules)
                {
                    studentscheduletobesaved.Add(new StudentSchedule
                    {
                        ScheduleID = item.ScheduleID,
                        StudentSectionID = item.StudentSectionID
                    });
                }
                if (hasadjustment)
                {

                    var adjustments = db.Adjustment.Where(m => m.StudentSectionID == enrollment.Student_SectionID).OrderByDescending(m => m.AdjustmentDate);
                    foreach (var adjustment in adjustments)
                    {
                        var adjustmentdetails = db.AdjustmentDetails.Where(m => m.AdjustmentID == adjustment.AdjustmentID);
                        foreach (var detail in adjustmentdetails)
                        {
                            if (detail.Action == null)
                            {
                                var subject = db.Schedule.FirstOrDefault(m => m.ScheduleID == detail.ScheduleID);
                                if (subject == null) throw new Exception("Something is wrong with the saved adjustment.");
                                foreach (var item in studentscheduletobesaved)
                                {
                                    var schedule = db.Schedule.FirstOrDefault(m => m.SubjectID == subject.SubjectID && m.ScheduleID == item.ScheduleID);
                                    if (schedule != null)
                                    {
                                        item.ScheduleID = detail.ScheduleID.Value;
                                    }
                                }
                            }
                            else if (detail.Action == true)
                            {
                                var item = studentscheduletobesaved.FirstOrDefault(m => m.ScheduleID == detail.ScheduleID);
                                studentscheduletobesaved.Remove(item);
                            }
                            else
                            {
                                studentscheduletobesaved.Add(new StudentSchedule
                                {
                                    ScheduleID = detail.ScheduleID.Value,
                                    StudentSectionID = enrollment.Student_SectionID
                                });
                            }
                        }
                    }
                }

                foreach (var item in studentscheduletobesaved)
                {
                    db.OriginalStudentSchedule.Add(new OriginalStudentSchedule
                    {
                        ScheduleID = item.ScheduleID,
                        StudentSectionID = enrollment.Student_SectionID
                    });
                }
                db.SaveChanges();
                originalschedule = db.OriginalStudentSchedule.Where(m => m.StudentSectionID == enrollment.Student_SectionID).ToList();
            }
            ViewBag.StudentNo = enrollment.Student.StudentNo;
            ViewBag.FullName = enrollment.Student.FullName;
            ViewBag.ValidationDate = enrollment.ValidationDate.Value.ToShortDateString();
            ViewBag.Period = enrollment.Section.Period.FullName;
            ViewBag.SectionName = enrollment.Section.SectionName;
            ViewBag.Remarks = enrollment.Remarks;
            ViewBag.id = enrollment.Student_SectionID;
            ViewBag.reassessment = db.StudentSectionReAssessment.Where(m => m.Student_SectionID == enrollment.Student_SectionID);
            items = OriginalScheduleToDto(originalschedule);
            return View(items);
        }
        
        [NonAction]
        public List<StudentScheduleWrapper> OriginalScheduleToDto(List<OriginalStudentSchedule> list)
        {
            var items = new List<StudentScheduleWrapper>();
            foreach (var item in list)
            {
                var schedule = db.Schedule.Find(item.ScheduleID);
                items.Add(new StudentScheduleWrapper
                {
                    Day = schedule.Days,
                    Description = schedule.Subject.Description,
                    Faculty = schedule.FacultyID.HasValue ? schedule.Faculty.FacultyName : "",
                    Room = schedule.Room.RoomName,
                    Subject = schedule.Subject.SubjectCode,
                    TotalHours = schedule.Subject.NoOfHours,
                    Time = (schedule.StartTime.HasValue ? DateTime.Today.Add(schedule.StartTime.Value).ToString("hh:mm tt") : "") + "-" +
                            (schedule.EndTime.HasValue ? DateTime.Today.Add(schedule.EndTime.Value).ToString("hh:mm tt") : "")
                });
            }
            return items;
        }

        [HttpPost]
        public ActionResult PostDMCM(DmcmDto model)
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var docnumlast = db.DMCM.OrderByDescending(m => m.DocNum).FirstOrDefault().DocNum.Value;
            var student = db.Student.Find(model.StudentId);
            var studentsection = db.Student_Section.Where(m => m.StudentID == model.StudentId && m.Section.PeriodID == periodid).FirstOrDefault();
            docnumlast++;
            foreach (var i in model.DetailDtos)
            {
                DMCM memo = new DMCM();
                memo.DocNum = docnumlast;
                memo.Amount = (double)i.Amount;
                memo.ChargeToStudentAr = i.ChargeToStudentAr;
                memo.DC = i.IsDebit ? "D" : "C";
                memo.PeriodID = periodid;
                memo.Remarks = model.Remarks;
                memo.StudentID = model.StudentId;
                memo.AcaDeptID = i.DepartmentId;
                memo.TransactionDate = model.PostingDate;
                memo.AcctID = i.AccountId == 0 ? (int?)null : i.AccountId;
                memo.SubAcctID = i.SubaccountId == 0 ? (int?)null : i.SubaccountId;
                memo.AccountName = i.AccountName;
                memo.AccountNumber = i.AccountCode;
                db.DMCM.Add(memo);
                db.SaveChanges();
                db.InsertDmcmTransactionLog(User.Identity.Name, "DMCM - " + memo.AccountNumber + " - " + memo.Amount + " - " + memo.Remarks, student.StudentNo);
                if (!i.ChargeToStudentAr)
                {
                    foreach (var d in model.DiscountDetails)
                    {
                        db.DmcmDiscountDetail.Add(new DmcmDiscountDetail
                        {
                            DiscountId = d.DiscountId,
                            Amount = d.Amount,
                            FeeId = d.FeeId,
                            DmcmId = memo.DMCMID
                        });
                    }
                    db.SaveChanges();
                }
            }
            EMailPostedDMCM(docnumlast);
            return RedirectToAction("ListDMCM", "DMCM", new { start = docnumlast, last = docnumlast });
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
                    if (db.Database.Connection.ConnectionString.Contains("172.20.0.10"))
                    {
                        mail.To.Add("christopher.seno@letran.edu.ph");
                    }
                    else
                    {
                        mail.To.Add(studentemail.Email);
                    }
                    mail.CC.Add(new MailAddress(user.Email));

                    //mail.To.Add("christopher.seno@letran.edu.ph");
                    mail.Body = "Please see the attached debit or credit memo.";
                    mail.Subject = "Debit/Credit Memo";
                    mail.IsBodyHtml = true;

                    List<DMCMReportDTO> reportdata = GenerateDMCMReport(id, fullname);
                    document.SetDataSource(reportdata);
                    mail.Attachments.Add(new Attachment(document.ExportToStream(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat), "SOA.pdf"));

                    mail.IsBodyHtml = true;
                    SmtpServer.Port = 587;
                    SmtpServer.Credentials = new System.Net.NetworkCredential("admin@letran.edu.ph", fromPassword);
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
       
        [HttpPost]
        public ActionResult ViewReassessment(List<AssessmentDetailDTO> assessmentDetails, string btnDiscount, string btnAmount)
        {
            if (assessmentDetails.Count < 1) throw new Exception("No selected item.");
            var reassessment = db.StudentSectionReAssessment.Find(assessmentDetails.FirstOrDefault().ReassessmentStudentSectionId);
            DmcmDto dmcm = new DmcmDto();
            dmcm.StudentId = reassessment.Student_Section.StudentID;
            dmcm.StudentName = reassessment.Student_Section.StudentName;
            dmcm.StudentNo = reassessment.Student_Section.StudentNo;
            var syAccountCode = reassessment.Section.Period.SchoolYear.ChartOfAccountId == null ?
                db.ChartOfAccounts.Where(m=>m.SYID== reassessment.Section.Period.SchoolYearID).FirstOrDefault() : reassessment.Section.Period.SchoolYear.ChartOfAccounts.FirstOrDefault();
            if (syAccountCode == null)
                throw new Exception("No journal entry setup for this school year.");
            if (btnDiscount != null && btnDiscount.Length > 0)
            {
                var discount = db.Discount.Where(m => m.StudentID == dmcm.StudentId && m.PeriodID == reassessment.Section.PeriodID).FirstOrDefault();
                var discountCoa = discount.DiscountType.ChartOfAccounts1;
                var discountSubCoa = discount.DiscountType.SubChartOfAccounts;
                List<DmcmDiscountDetail> discountDetails = new List<DmcmDiscountDetail>();
                decimal totaldiscount = 0;
                foreach (var item in assessmentDetails)
                {
                    if (item.IsSelected)
                    {
                        totaldiscount += item.DiscountAmountDiff;
                        var discountdetail = new DmcmDiscountDetail();
                        var fee = db.Fee.Find(item.FeeId);
                        discountdetail.AccountId = fee.ChartOfAccounts.AcctID;
                        discountdetail.AccountNo = fee.ChartOfAccounts.AcctNo;
                        discountdetail.Amount = item.DiscountAmountDiff;
                        discountdetail.DiscountId = discount.DiscountID;
                        discountdetail.AccountName = fee.ChartOfAccounts.AcctName;
                        discountdetail.FeeId = item.FeeId;
                        discountDetails.Add(discountdetail);
                    }
                }
                dmcm.DiscountDetails = discountDetails;
                dmcm.DetailDtos.Add(new DmcmDetailDto
                {
                    AccountCode = syAccountCode != null ? syAccountCode.AcctNo : "n/a",
                    AccountId = syAccountCode != null ? syAccountCode.AcctID : 0,
                    AccountName = syAccountCode != null ? syAccountCode.AcctName : "no setup",
                    Amount = Math.Abs(totaldiscount),
                    IsDebit = totaldiscount > 0 ? true : false,
                    ChargeToStudentAr = true,
                    SubaccountId = 0,
                    DepartmentId = reassessment.Section.Curriculum.AcaDeptID ?? 0
                });
                dmcm.DetailDtos.Add(new DmcmDetailDto
                {
                    AccountCode = discountSubCoa != null ? discountSubCoa.SubAcctNo : (discountCoa != null ? discountCoa.AcctNo : "n/a"),
                    AccountId = discountCoa != null ? discountCoa.AcctID : 0,
                    AccountName = discountSubCoa != null ? discountSubCoa.SubbAcctName : (discountCoa != null ? discountCoa.AcctName : "no setup"),
                    Amount = Math.Abs(totaldiscount),
                    IsDebit = totaldiscount > 0 ? false : true,
                    ChargeToStudentAr = false,
                    SubaccountId = discountSubCoa != null ? discountSubCoa.SubAcctID : 0,
                    DepartmentId = discount.DiscountCategoryID.HasValue ? discount.DiscountCategory.AcctID ?? 0 : (reassessment.Section.Curriculum.AcaDeptID ?? 0)
                });
            }
            else
            {                
                decimal totalar = 0;
                foreach (var item in assessmentDetails)
                {
                    if (item.IsSelected)
                    {
                        totalar += item.OriginalAmountDiff;
                        var fee = db.Fee.Find(item.FeeId);
                        if (fee == null || (fee.AcctID == null && fee.SubAcctID == null))
                            throw new Exception("Error in fee setup for " + fee.FeeName);
                        var coa = db.ChartOfAccounts.Find(fee.AcctID);
                        var subcoa = db.SubChartOfAccounts.Find(fee.SubAcctID);
                        dmcm.DetailDtos.Add(new DmcmDetailDto
                        {
                            AccountCode = subcoa != null ? subcoa.SubAcctNo : (coa != null ? coa.AcctNo : "n/a"),
                            AccountId = coa != null ? coa.AcctID : (subcoa != null ? subcoa.AcctID.Value : 0),
                            AccountName = subcoa != null ? subcoa.SubbAcctName : (coa != null ? coa.AcctName : "no setup"),
                            Amount = Math.Abs(item.OriginalAmountDiff),
                            IsDebit = item.OriginalAmountDiff > 0 ? false : true,
                            ChargeToStudentAr = false,
                            SubaccountId = subcoa != null ? subcoa.SubAcctID : 0
                        });
                    }
                }
                dmcm.DetailDtos.Add(new DmcmDetailDto
                {
                    AccountCode = syAccountCode != null ? syAccountCode.AcctNo : "n/a",
                    AccountId = syAccountCode != null ? syAccountCode.AcctID : 0,
                    AccountName = syAccountCode != null ? syAccountCode.AcctName : "no setup",
                    Amount = Math.Abs(totalar),
                    IsDebit = totalar > 0 ? true : false,
                    ChargeToStudentAr = true,
                    SubaccountId = 0,
                    DepartmentId = reassessment.Section.Curriculum.AcaDeptID ?? 0
                });
            }
            return View(dmcm);
        }
        public ActionResult ViewReassessment(int id)
        {
            var newassessment = db.StudentSectionReAssessment.Find(id);
            var oldassessment = db.Student_Section.Find(newassessment.Student_SectionID);
            List<AssessmentDetailDTO> assessmentDetails = new List<AssessmentDetailDTO>();
            foreach (var item in oldassessment.Assessment)
            {
                assessmentDetails.Add(new AssessmentDetailDTO
                {
                    AccountCode = item.Fee.AcctID.HasValue ? item.Fee.ChartOfAccounts.AcctNo : "",
                    AccountId = item.Fee.AcctID.HasValue ? item.Fee.AcctID : null,
                    SubaccountId = item.Fee.SubAcctID.HasValue ? item.Fee.SubAcctID : null,
                    SubaccountCode = item.Fee.SubAcctID.HasValue ? item.Fee.SubChartOfAccounts.SubAcctNo : "",
                    FeeDescription = item.Description,
                    FeeId = item.FeeID.Value,
                    FeeType = item.FeeType,
                    OriginalAmount = item.Amount.HasValue ? (decimal)item.Amount.Value : 0,
                    OriginalDiscountAmount = item.DiscountAmount.HasValue ? (decimal)item.DiscountAmount.Value : 0,
                    NewAmount = 0,
                    NewDiscountAmount = 0,
                    StudentSectionId = oldassessment.Student_SectionID,
                    ReassessmentStudentSectionId = newassessment.StudentSectionReAssessmentID
                });
            }
            foreach (var item in newassessment.ReAssessment)
            {
                var existingitem = assessmentDetails.FirstOrDefault(m => m.FeeId == item.FeeID);
                if (existingitem != null)
                {
                    existingitem.NewAmount = item.Amount.HasValue ? (decimal)item.Amount.Value : 0;
                    existingitem.NewDiscountAmount = item.DiscountAmount.HasValue ? (decimal)item.DiscountAmount.Value : 0;
                }
                else
                {
                    assessmentDetails.Add(new AssessmentDetailDTO
                    {
                        AccountCode = item.Fee.AcctID.HasValue ? item.Fee.ChartOfAccounts.AcctNo : "",
                        AccountId = item.Fee.AcctID.HasValue ? item.Fee.AcctID : null,
                        SubaccountId = item.Fee.SubAcctID.HasValue ? item.Fee.SubAcctID : null,
                        SubaccountCode = item.Fee.SubAcctID.HasValue ? item.Fee.SubChartOfAccounts.SubAcctNo : "",
                        FeeDescription = item.Description,
                        FeeId = item.FeeID.Value,
                        FeeType = item.FeeType,
                        NewAmount = item.Amount.HasValue ? (decimal)item.Amount.Value : 0,
                        NewDiscountAmount = item.DiscountAmount.HasValue ? (decimal)item.DiscountAmount.Value : 0,
                        OriginalAmount = 0,
                        OriginalDiscountAmount = 0,
                        StudentSectionId = oldassessment.Student_SectionID,
                        ReassessmentStudentSectionId = newassessment.StudentSectionReAssessmentID
                    });
                }
            }
            return View("Reassess", assessmentDetails);
        }
        public ActionResult Reassess(int id)
        {
            int reassesmentid = CreateReassessment(id);
            return RedirectToAction("ViewReassessment", new { id = reassesmentid });
        }

    }
}