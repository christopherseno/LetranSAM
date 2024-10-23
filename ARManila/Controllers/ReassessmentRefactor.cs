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

        [NonAction]
        private int CreateReassessment(int id)
        {
            var enlistment = db.Student_Section.Find(id);
            if (enlistment.ValidationDate == null) throw new Exception("Reassessment module is only for officially enrolled students.");
            if (!db.OriginalStudentSchedule.Any(m => m.StudentSectionID == id)) throw new Exception("Original Schedule Setup is missing.");
            int tuitionid = 0;
            decimal voucher = 0;
            decimal firstpayment = 0, secondpayment = 0;
            var student = db.Student.Find(enlistment.StudentID);
            var periodid = enlistment.Section.PeriodID;
            var computedassessment = db.GetReAssessment(enlistment.Student_SectionID).ToList();
            var discount = db.Discount.Where(m => m.StudentID == enlistment.StudentID && m.PeriodID == periodid).FirstOrDefault();
            var computedfixeddownpayment = computedassessment.Where(m => m.FeeCategory == "D").FirstOrDefault();
            var computedtuition = computedassessment.Where(m => m.FeeCategory == "T").FirstOrDefault();

            List<Assessment> miscfeeswithdiscount = new List<Assessment>();
            List<Assessment> supplementalfeeswithdiscount = new List<Assessment>();
            List<Assessment> labfeeswithddiscount = new List<Assessment>();
            List<Assessment> otherfeeswithdiscount = new List<Assessment>();
            List<Assessment> variousfeeswithdiscount = new List<Assessment>();
            List<Assessment> labairconfeeswithdiscount = new List<Assessment>();

            decimal totalprocessingfee = ComputeProcessingFee(student, periodid);

            ComputeAssessmentDetails(miscfeeswithdiscount, supplementalfeeswithdiscount, otherfeeswithdiscount, variousfeeswithdiscount, labairconfeeswithdiscount, computedassessment, discount);

            ComputeTotalAssessment(enlistment, ref tuitionid, out decimal tuitionfee, out decimal totalmiscfee, out decimal totallabfee, out decimal totalotherfee, out decimal totalvariousfee, out decimal totalairconfee, out decimal totalsupplemtalfee, out decimal totalfee, out decimal creditamount, out decimal totaldiscount, out decimal netassessmentamount, out decimal tuitionfordiscount, ref voucher, computedassessment, discount);

            var otherpayment = totalmiscfee + totallabfee + totalotherfee + totalvariousfee + totalsupplemtalfee + totalairconfee;

            ComputeFirstAndSecondPayment(enlistment, creditamount, netassessmentamount, ref firstpayment, ref secondpayment, computedfixeddownpayment, otherpayment);

            List<PaymentSchedule> paymentschedules = SetupPaymentSchedule(enlistment, firstpayment, secondpayment);
            AssessmentDTO assessment = SetupAssessmentDTO(enlistment, tuitionfee, totalmiscfee, totallabfee, totalotherfee, totalvariousfee, totalairconfee, totalsupplemtalfee, totalfee, creditamount, totaldiscount, netassessmentamount, firstpayment, student, periodid, totalprocessingfee, otherpayment, paymentschedules);

            return SaveAssessment(enlistment, miscfeeswithdiscount, supplementalfeeswithdiscount, otherfeeswithdiscount, variousfeeswithdiscount, labairconfeeswithdiscount, tuitionid, tuitionfee, secondpayment, discount, assessment, tuitionfordiscount);

            //return assessment;
        }
        
        private decimal ComputeProcessingFee(Student student, int periodid)
        {
            var processingfees = db.PaymentDetails.Where(m => m.Payment.StudentID == student.StudentID && m.Payment.SemID == periodid && m.PaycodeID == 10).ToList();
            decimal totalprocessingfee = 0;
            foreach (var processingfee in processingfees)
            {
                totalprocessingfee += (decimal)processingfee.Amount.Value;
            }

            return totalprocessingfee;
        }

        private AssessmentDTO SetupAssessmentDTO(Student_Section enlistment, decimal tuitionfee, decimal totalmiscfee, decimal totallabfee, decimal totalotherfee, decimal totalvariousfee, decimal totalairconfee, decimal totalsupplemtalfee, decimal totalfee, decimal creditamount, decimal totaldiscount, decimal netassessmentamount, decimal firstpayment, Student student, int periodid, decimal totalprocessingfee, decimal otherpayment, List<PaymentSchedule> paymentschedules)
        {
            AssessmentDTO assessment = new AssessmentDTO();
            assessment.PeriodID = periodid;
            assessment.SectionID = enlistment.SectionID ?? 0;
            assessment.StudentID = student.StudentID;
            assessment.StudentName = student.FullName;
            assessment.StudentNo = student.StudentNo;
            assessment.GradeYear = enlistment.Section.GradeYear;
            assessment.oaf = enlistment.Student_SectionID;
            assessment.PaymentMode = enlistment.Paymode.Description;
            assessment.Program = db.ProgamCurriculum.Where(m => m.CurriculumID == enlistment.CurriculumID).First().Progam.ProgramCode;
            assessment.Units = (db.StudentSchedule.Where(m => m.StudentSectionID == enlistment.Student_SectionID).Where(m => m.Schedule.Subject.IsTuition == true).GroupBy(m => m.StudentSectionID).Select(g => new { TotalUnit = g.Sum(c => c.Schedule.Subject.Units) }).FirstOrDefault()).TotalUnit;
            assessment.Hours = (db.StudentSchedule.Where(m => m.StudentSectionID == enlistment.Student_SectionID).GroupBy(m => m.StudentSectionID).Select(g => new { TotalUnit = g.Sum(c => c.Schedule.Subject.NoOfHours) }).FirstOrDefault()).TotalUnit.Value;
            assessment.Processing = totalprocessingfee.ToString("###,##0.00");
            assessment.AssessmentDate = DateTime.Now;
            assessment.Tuition = tuitionfee;
            assessment.Misc = totalmiscfee;
            assessment.Lab = (totallabfee + totalairconfee);
            assessment.Various = (totalvariousfee + totalsupplemtalfee + totalotherfee);
            assessment.OtherPayment = otherpayment;
            assessment.TotalAssesment = totalfee;
            assessment.Credit = creditamount;
            assessment.Discount = totaldiscount;
            assessment.NetAss = netassessmentamount;
            assessment.Down = firstpayment;
            assessment.Due = firstpayment - totalprocessingfee;
            assessment.StudentSchedule = db.GetStudentSchedules(enlistment.StudentID, periodid).ToList();
            assessment.PaymentSchedules = paymentschedules;
            return assessment;
        }

        private int SaveAssessment(Student_Section enlistment, List<Assessment> miscfeeswithdiscount, List<Assessment> supplementalfeeswithdiscount, List<Assessment> otherfeeswithdiscount, List<Assessment> variousfeeswithdiscount, List<Assessment> labairconfeeswithdiscount, int tuitionid, decimal tuitionfee, decimal secondpayment, Discount discount, AssessmentDTO assessment, decimal tuitionfordiscount)
        {
            StudentSectionReAssessment studentsection = new StudentSectionReAssessment();
            studentsection.AssessedBy = User.Identity.Name;
            studentsection.AssessmentDate = DateTime.Now;
            //var studentsection = db.Student_Section.Find(enlistment.Student_SectionID);            
            studentsection.Credit = (double)assessment.Credit;
            studentsection.Discount = (double)assessment.Discount;
            studentsection.DownPayment = (double)assessment.Down;
            studentsection.LabFee = (double)assessment.Lab;
            studentsection.MiscFee = (double)assessment.Misc;
            studentsection.SecondPayment = (double)secondpayment;
            studentsection.SuppFee = (double)assessment.Various;
            studentsection.TuitionFee = (double)assessment.Tuition;
            studentsection.Student_SectionID = enlistment.Student_SectionID;
            studentsection.SectionID = enlistment.SectionID.Value;
            studentsection.CurriculumID = enlistment.CurriculumID;
            studentsection.NewStatus = enlistment.NewStatus.Value;
            studentsection.PaymodeID = enlistment.PaymodeID;
            studentsection.Status = enlistment.Status.Value;
            studentsection.StudentStatus = enlistment.StudentStatus;
            db.StudentSectionReAssessment.Add(studentsection);
            db.SaveChanges();

            var tuitiondiscountrate = discount == null ? 0 : (discount.DiscountType.PercentForTotal.HasValue && discount.DiscountType.PercentForTotal.Value > 0 ? discount.DiscountType.PercentForTotal.Value : (discount.DiscountType.PercentForTuition ?? 0));
            var allotherfeesdiscountrate = discount == null ? 0 : (discount.DiscountType.PercentForTotal.HasValue && discount.DiscountType.PercentForTotal.Value > 0 ? discount.DiscountType.PercentForTotal.Value : (discount.DiscountType.PercentForMisc ?? 0));
            ReAssessment tuitionassessment = new ReAssessment();
            tuitionassessment.Amount = (double)tuitionfee;
            tuitionassessment.Discount = tuitiondiscountrate;
            tuitionassessment.Description = "Tuition";
            tuitionassessment.FeeType = "T";
            tuitionassessment.FeeID = tuitionid;
            tuitionassessment.TuitionDiscountRate = (decimal)tuitiondiscountrate;
            tuitionassessment.MaxUnit = discount == null || discount.MaxUnit == null ? null : (decimal)discount.MaxUnit.Value;
            tuitionassessment.TuitionForDiscount = tuitionfordiscount == 0 ? null : tuitionfordiscount;
            tuitionassessment.StudentSectionReAssessmentID = studentsection.StudentSectionReAssessmentID;
            db.ReAssessment.Add(tuitionassessment);
            foreach (var i in miscfeeswithdiscount)
            {
                ReAssessment miscassessment = new ReAssessment();
                miscassessment.Amount = ((Assessment)i).Amount;
                miscassessment.Description = ((Assessment)i).Description;
                miscassessment.FeeType = ((Assessment)i).FeeType;
                miscassessment.FeeID = ((Assessment)i).FeeID;
                miscassessment.Discount = ((Assessment)i).Discount;
                miscassessment.StudentSectionReAssessmentID = studentsection.StudentSectionReAssessmentID;
                db.ReAssessment.Add(miscassessment);
            }
            foreach (var i in supplementalfeeswithdiscount)
            {
                ReAssessment supplementalassessment = new ReAssessment();
                supplementalassessment.Amount = ((Assessment)i).Amount;
                supplementalassessment.Description = ((Assessment)i).Description;
                supplementalassessment.FeeType = ((Assessment)i).FeeType;
                supplementalassessment.FeeID = ((Assessment)i).FeeID;
                supplementalassessment.Discount = ((Assessment)i).Discount;
                supplementalassessment.StudentSectionReAssessmentID = studentsection.StudentSectionReAssessmentID;
                db.ReAssessment.Add(supplementalassessment);
            }
            foreach (var i in labairconfeeswithdiscount)
            {
                ReAssessment labairconassessment = new ReAssessment();
                labairconassessment.Amount = ((Assessment)i).Amount;
                labairconassessment.Description = ((Assessment)i).Description;
                labairconassessment.FeeType = ((Assessment)i).FeeType;
                labairconassessment.FeeID = ((Assessment)i).FeeID;
                labairconassessment.Discount = ((Assessment)i).Discount;
                labairconassessment.StudentSectionReAssessmentID = studentsection.StudentSectionReAssessmentID;
                db.ReAssessment.Add(labairconassessment);
            }
            foreach (var i in variousfeeswithdiscount)
            {
                ReAssessment variousfeeassessment = new ReAssessment();
                variousfeeassessment.Discount = ((Assessment)i).Discount;
                variousfeeassessment.Amount = ((Assessment)i).Amount;
                variousfeeassessment.Description = ((Assessment)i).Description;
                variousfeeassessment.FeeType = ((Assessment)i).FeeType;
                variousfeeassessment.FeeID = ((Assessment)i).FeeID;
                variousfeeassessment.StudentSectionReAssessmentID = studentsection.StudentSectionReAssessmentID;
                db.ReAssessment.Add(variousfeeassessment);
            }
            foreach (var i in otherfeeswithdiscount)
            {
                ReAssessment otherfeesassessment = new ReAssessment();
                otherfeesassessment.Amount = ((Assessment)i).Amount;
                otherfeesassessment.Discount = ((Assessment)i).Discount;
                otherfeesassessment.Description = ((Assessment)i).Description;
                otherfeesassessment.FeeType = ((Assessment)i).FeeType;
                otherfeesassessment.FeeID = ((Assessment)i).FeeID;
                otherfeesassessment.StudentSectionReAssessmentID = studentsection.StudentSectionReAssessmentID;
                db.ReAssessment.Add(otherfeesassessment);
            }
            db.SaveChanges();
            return studentsection.StudentSectionReAssessmentID;
        }

        private List<PaymentSchedule> SetupPaymentSchedule(Student_Section enlistment, decimal firstpayment, decimal secondpayment)
        {
            List<PaymentSchedule> paymentschedules = new List<PaymentSchedule>();
            var paycode = db.Paycode.Where(m => m.TuitionRelated == true).ToList();
            PaymentSchedule paycode1 = new PaymentSchedule();
            paycode1.Description = paycode.Find(m => m.PaycodeID == 1).Description;
            PaymentSchedule paycode2 = new PaymentSchedule();
            paycode2.Description = paycode.Find(m => m.PaycodeID == 2).Description;
            PaymentSchedule paycode3 = new PaymentSchedule();
            paycode3.Description = paycode.Find(m => m.PaycodeID == 3).Description;
            PaymentSchedule paycode4 = new PaymentSchedule();
            paycode4.Description = paycode.Find(m => m.PaycodeID == 4).Description;
            PaymentSchedule paycode5 = new PaymentSchedule();
            paycode5.Description = paycode.Find(m => m.PaycodeID == 5).Description;
            PaymentSchedule paycode6 = new PaymentSchedule();
            paycode6.Description = paycode.Find(m => m.PaycodeID == 6).Description;
            PaymentSchedule paycode7 = new PaymentSchedule();
            paycode7.Description = paycode.Find(m => m.PaycodeID == 7).Description;
            PaymentSchedule paycode8 = new PaymentSchedule();
            paycode8.Description = paycode.Find(m => m.PaycodeID == 8).Description;
            PaymentSchedule paycode9 = new PaymentSchedule();
            paycode9.Description = paycode.Find(m => m.PaycodeID == 9).Description;
            paycode1.DueDate = db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 1).FirstOrDefault() != null ? (db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 1).FirstOrDefault().DueDate ?? DateTime.Now) : DateTime.Now;
            paycode2.DueDate = db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 2).FirstOrDefault() != null ? (db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 2).FirstOrDefault().DueDate ?? DateTime.Now) : DateTime.Now;
            paycode3.DueDate = db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 3).FirstOrDefault() != null ? (db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 3).FirstOrDefault().DueDate ?? DateTime.Now) : DateTime.Now;
            paycode4.DueDate = db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 4).FirstOrDefault() != null ? (db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 4).FirstOrDefault().DueDate ?? DateTime.Now) : DateTime.Now;
            paycode5.DueDate = db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 5).FirstOrDefault() != null ? (db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 5).FirstOrDefault().DueDate ?? DateTime.Now) : DateTime.Now;
            paycode6.DueDate = db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 6).FirstOrDefault() != null ? (db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 6).FirstOrDefault().DueDate ?? DateTime.Now) : DateTime.Now;
            paycode7.DueDate = db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 7).FirstOrDefault() != null ? (db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 7).FirstOrDefault().DueDate ?? DateTime.Now) : DateTime.Now;
            paycode8.DueDate = db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 8).FirstOrDefault() != null ? (db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 8).FirstOrDefault().DueDate ?? DateTime.Now) : DateTime.Now;
            paycode9.DueDate = db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 9).FirstOrDefault() != null ? (db.PaySchedule.Where(m => m.PeriodID == enlistment.Section.PeriodID && m.PaymodeID == enlistment.PaymodeID && m.PaycodeID == 9).FirstOrDefault().DueDate ?? DateTime.Now) : DateTime.Now;

            switch (enlistment.PaymodeID.Value)
            {
                case 1:
                    paycode1.Amount = firstpayment;
                    paymentschedules.Add(paycode1);
                    break;
                case 2:
                    paycode1.Amount = firstpayment;
                    paycode2.Amount = secondpayment;
                    paymentschedules.Add(paycode1);
                    paymentschedules.Add(paycode2);
                    if (enlistment.Section.Curriculum.EducationalLevel == 3)
                    {
                        paycode3.Amount = secondpayment;
                        paymentschedules.Add(paycode3);
                    }
                    break;
                case 3:
                    paycode1.Amount = firstpayment;
                    paycode2.Amount = secondpayment;
                    paymentschedules.Add(paycode1);
                    paymentschedules.Add(paycode2);
                    if (enlistment.Curriculum.EducationalLevel < 4)
                    {
                        paycode3.Amount = paycode2.Amount;
                        paycode4.Amount = paycode2.Amount;
                        paymentschedules.Add(paycode3);
                        paymentschedules.Add(paycode4);
                    }
                    if (enlistment.Section.Curriculum.EducationalLevel == 4)
                    {
                        paycode3.Amount = paycode2.Amount;
                        paymentschedules.Add(paycode3);
                    }
                    break;
                case 4:
                    paycode1.Amount = firstpayment;
                    paycode2.Amount = secondpayment;
                    paycode3.Amount = secondpayment;
                    paycode4.Amount = secondpayment;
                    paycode5.Amount = secondpayment;
                    paymentschedules.Add(paycode1);
                    paymentschedules.Add(paycode2);
                    paymentschedules.Add(paycode3);
                    paymentschedules.Add(paycode4);
                    paymentschedules.Add(paycode5);
                    if (enlistment.Section.Curriculum.EducationalLevel < 4)
                    {
                        paycode6.Amount = paycode2.Amount;
                        paycode7.Amount = paycode2.Amount;
                        paycode8.Amount = paycode2.Amount;
                        paycode9.Amount = paycode2.Amount;
                        paymentschedules.Add(paycode6);
                        paymentschedules.Add(paycode7);
                        paymentschedules.Add(paycode8);
                        paymentschedules.Add(paycode9);
                    }
                    break;
            }

            return paymentschedules;
        }

        private static void ComputeFirstAndSecondPayment(Student_Section enlistment, decimal creditamount, decimal netassessmentamount, ref decimal firstpayment, ref decimal secondpayment, GetReAssessment_Result fixeddownpayment, decimal otherpayment)
        {
            if (enlistment.PaymodeID == 1)
            {
                firstpayment = netassessmentamount > 0 ? netassessmentamount - creditamount : 0;
                secondpayment = 0;
            }
            //Payment mode =2  for shs jhs elem
            else if (enlistment.PaymodeID == 2 && enlistment.Section.Curriculum.EducationalLevel < 4)
            {
                var initialfirstpayment = (decimal)(fixeddownpayment.Amount ?? 0) - creditamount;
                firstpayment = netassessmentamount > 0 && initialfirstpayment > 0 ? initialfirstpayment : 0;
                if (enlistment.Section.Curriculum.EducationalLevel == 3)
                {
                    secondpayment = netassessmentamount > 0 && (netassessmentamount - initialfirstpayment) / 2 > 0 ? (netassessmentamount - initialfirstpayment) / 2 : 0;
                }
                else
                {
                    secondpayment = netassessmentamount > 0 && netassessmentamount - initialfirstpayment > 0 ? netassessmentamount - initialfirstpayment : 0;
                }
            }
            //payment mode =3 for shs jhs elem
            else if (enlistment.PaymodeID == 3 && enlistment.Curriculum.EducationalLevel < 4)
            {
                var initialfirstpayment = (decimal)(fixeddownpayment.Amount ?? 0) - creditamount;
                firstpayment = netassessmentamount > 0 && initialfirstpayment > 0 ? initialfirstpayment : 0;
                secondpayment = netassessmentamount > 0 && (netassessmentamount - initialfirstpayment) / 4 > 0 ? (netassessmentamount - initialfirstpayment) / 4 : 0;
            }
            //payment mode =3 for college
            else if (enlistment.PaymodeID == 3 && enlistment.Curriculum.EducationalLevel == 4)
            {
                var initialfirstpayment = (netassessmentamount - otherpayment) * 0.3m + otherpayment - creditamount;
                firstpayment = initialfirstpayment > 0 ? initialfirstpayment : 0;
                secondpayment = (netassessmentamount - creditamount - initialfirstpayment) / 2 > 0 ? (netassessmentamount - creditamount - initialfirstpayment) / 2 : 0;
            }
            //payment mode =4 for shs jhs elem
            else if (enlistment.PaymodeID == 4 && enlistment.Curriculum.EducationalLevel < 4)
            {
                var initialfirstpayment = (decimal)(fixeddownpayment.Amount ?? 0) - creditamount;
                firstpayment = netassessmentamount > 0 && initialfirstpayment > 0 ? initialfirstpayment : 0;
                secondpayment = netassessmentamount > 0 && (netassessmentamount - initialfirstpayment) / 8 > 0 ? (netassessmentamount - initialfirstpayment) / 8 : 0;
            }
            //payment mode =4 for college
            else if (enlistment.PaymodeID == 4 && enlistment.Curriculum.EducationalLevel == 4)
            {
                var initialfirstpayment = (netassessmentamount - otherpayment) * 0.1m + otherpayment - creditamount;
                firstpayment = initialfirstpayment > 0 ? initialfirstpayment : 0;
                secondpayment = (netassessmentamount - creditamount - initialfirstpayment) / 4 > 0 ? (netassessmentamount - creditamount - initialfirstpayment) / 4 : 0;
            }
        }

        private void ComputeTotalAssessment(Student_Section enlistment, ref int tuitionid, out decimal tuitionfee, out decimal totalmiscfee, out decimal totallabfee, out decimal totalotherfee, out decimal totalvariousfee, out decimal totalairconfee, out decimal totalsupplemtalfee, out decimal totalfee, out decimal creditamount, out decimal totaldiscount, out decimal netassessmentamount, out decimal tuitionfordiscount, ref decimal voucher, List<GetReAssessment_Result> computedassessment, Discount discount)
        {
            var assessedtuitionfee = computedassessment.Where(m => m.FeeCategory == "T").FirstOrDefault();
            var cashtuitionfee = (decimal)(computedassessment.Where(m => m.FeeCategory == "Z").FirstOrDefault() != null ?
                    computedassessment.Where(m => m.FeeCategory == "Z").FirstOrDefault().Amount : 0);
            var totalhours = (db.OriginalStudentSchedule.Where(m => m.StudentSectionID == enlistment.Student_SectionID && m.Schedule.Subject.IsTuition.HasValue && m.Schedule.Subject.IsTuition.Value ==true).GroupBy(m => m.StudentSectionID).Select(g => new { TotalUnit = g.Sum(c => c.Schedule.Subject.NoOfHours) }).FirstOrDefault()).TotalUnit.Value;
            if (assessedtuitionfee == null)
            {
                tuitionfee = 0;
            }
            else
            {
                tuitionfee = (decimal)(assessedtuitionfee.Amount.Value);
                tuitionid = assessedtuitionfee.FeeID;
            }
            tuitionfordiscount = 0;
            totalmiscfee = (decimal)(computedassessment.Where(m => m.FeeCategory == "M").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault() == null ? 0 : (computedassessment.Where(m => m.FeeCategory == "M").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault()).Total.Value);
            totallabfee = (decimal)(computedassessment.Where(m => m.FeeCategory == "L").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault() == null ? 0 : (computedassessment.Where(m => m.FeeCategory == "L").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault()).Total.Value);
            totalairconfee = (decimal)(computedassessment.Where(m => m.FeeCategory == "A").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault() == null ? 0 : (computedassessment.Where(m => m.FeeCategory == "A").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault()).Total.Value);
            totalotherfee = (decimal)(computedassessment.Where(m => m.FeeCategory == "O").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault() == null ? 0 : (computedassessment.Where(m => m.FeeCategory == "O").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault()).Total.Value);
            totalvariousfee = (decimal)(computedassessment.Where(m => m.FeeCategory == "V").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault() == null ? 0 : (computedassessment.Where(m => m.FeeCategory == "V").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault()).Total.Value);
            totalsupplemtalfee = (decimal)(computedassessment.Where(m => m.FeeCategory == "S").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault() == null ? 0 : (computedassessment.Where(m => m.FeeCategory == "S").GroupBy(m => m.FeeCategory).Select(g => new { Total = g.Sum(c => c.Amount) }).FirstOrDefault()).Total.Value);

            var currentbackaccount = db.CheckStudentBackAccount(enlistment.StudentID).Where(m => m.Student_SectionID == null || m.Student_SectionID == enlistment.Student_SectionID).FirstOrDefault();
            totalfee = tuitionfee + totalmiscfee + totallabfee + totalairconfee + totalotherfee + totalvariousfee + totalsupplemtalfee;

            creditamount = (decimal)(currentbackaccount != null ? 0 - currentbackaccount.Balance ?? 0 : 0);
            totaldiscount = 0;
            if (discount != null)
            {
                if (discount.Period.EducLevelID < 4)
                {
                    tuitionfordiscount = cashtuitionfee;
                    if (discount.DiscountType.VoucherValue != null)
                    {
                        voucher = (decimal)discount.DiscountType.VoucherValue.Value;
                    }
                    if (discount.DiscountType.PercentForTuition != null)
                    {
                        totaldiscount += (cashtuitionfee - voucher) * (decimal)discount.DiscountType.PercentForTuition.Value;
                        if (discount.DiscountType.PercentForMisc != null)
                        {
                            totaldiscount += (totalmiscfee + totallabfee + totalairconfee + totalotherfee + totalvariousfee + totalsupplemtalfee) * (decimal)discount.DiscountType.PercentForMisc.Value;
                        }
                    }
                    else if (discount.DiscountType.PercentForTotal != null)
                    {
                        totaldiscount += (cashtuitionfee + totalmiscfee + totallabfee + totalairconfee + totalotherfee + totalvariousfee + totalsupplemtalfee - voucher) * (decimal)discount.DiscountType.PercentForTotal.Value;
                    }
                }
                else
                {
                    if (discount.DiscountType.VoucherValue != null)
                    {
                        voucher = (decimal)discount.DiscountType.VoucherValue.Value;
                    }
                    if (discount.MaxUnit == null || discount.MaxUnit.Value >= totalhours)
                    {
                        tuitionfordiscount = cashtuitionfee * (decimal)totalhours;
                        if (discount.DiscountType.PercentForTuition != null)
                        {
                            totaldiscount += (tuitionfordiscount - voucher) * (decimal)discount.DiscountType.PercentForTuition.Value;
                            if (discount.DiscountType.PercentForMisc != null)
                            {
                                totaldiscount += (totalmiscfee + totallabfee + totalairconfee + totalotherfee + totalvariousfee + totalsupplemtalfee) * (decimal)discount.DiscountType.PercentForMisc.Value;
                            }
                        }
                        else if (discount.DiscountType.PercentForTotal != null)
                        {
                            totaldiscount += (tuitionfordiscount + totalmiscfee + totallabfee + totalairconfee + totalotherfee + totalvariousfee + totalsupplemtalfee - voucher) * (decimal)discount.DiscountType.PercentForTotal.Value;
                        }
                    }
                    else
                    {
                        tuitionfordiscount = cashtuitionfee * (decimal)discount.MaxUnit.Value;
                        if (discount.DiscountType.PercentForTuition != null)
                        {
                            totaldiscount += (tuitionfordiscount - voucher) * (decimal)discount.DiscountType.PercentForTuition.Value;
                            if (discount.DiscountType.PercentForMisc != null)
                            {
                                totaldiscount += (totalmiscfee + totallabfee + totalairconfee + totalotherfee + totalvariousfee + totalsupplemtalfee) * (decimal)discount.DiscountType.PercentForMisc.Value;
                            }
                        }
                        else if (discount.DiscountType.PercentForTotal != null)
                        {
                            totaldiscount += (tuitionfordiscount + totalmiscfee + totallabfee + totalairconfee + totalotherfee + totalvariousfee + totalsupplemtalfee - voucher) * (decimal)discount.DiscountType.PercentForTotal.Value;
                        }
                    }
                }
            }
            netassessmentamount = totalfee - totaldiscount;
        }

        private static void ComputeAssessmentDetails(List<Assessment> miscfeeswithdiscount, List<Assessment> supplementalfeeswithdiscount, List<Assessment> otherfeeswithdiscount, List<Assessment> variousfeeswithdiscount, List<Assessment> labairconfeeswithdiscount, List<GetReAssessment_Result> computedassessment, Discount discount)
        {
            var computedmiscfees = computedassessment.Where(m => m.FeeCategory == "M").ToList();
            foreach (var i in computedmiscfees)
            {
                miscfeeswithdiscount.Add(new Assessment { Amount = i.Amount, Discount =i.DiscountRate.HasValue
                    ? i.DiscountRate.Value : (discount == null ? 0 : ((discount.DiscountType.PercentForTotal == null || discount.DiscountType.PercentForTotal == 0) ? (discount.DiscountType.PercentForMisc == null ? 0 : discount.DiscountType.PercentForMisc.Value) : discount.DiscountType.PercentForTotal.Value)),
                    FeeID = i.FeeID, FeeType = i.FeeCategory, Student_SectionID = i.studentsectionid, Description = i.Description });
            }

            var computedsupplementalfees = computedassessment.Where(m => m.FeeCategory == "S").ToList();
            foreach (var i in computedsupplementalfees)
            {
                supplementalfeeswithdiscount.Add(new Assessment { Amount = i.Amount, Discount = i.DiscountRate.HasValue
                    ? i.DiscountRate.Value : (discount == null ? 0 : ((discount.DiscountType.PercentForTotal == null || discount.DiscountType.PercentForTotal == 0) ? 0 : discount.DiscountType.PercentForTotal.Value)), 
                    FeeID = i.FeeID, FeeType = i.FeeCategory, Student_SectionID = i.studentsectionid, Description = i.Description });
            }

            var variousfees = computedassessment.Where(m => m.FeeCategory == "V").ToList();
            foreach (var i in variousfees)
            {
                variousfeeswithdiscount.Add(new Assessment { Amount = i.Amount, Discount = i.DiscountRate.HasValue
                    ? i.DiscountRate.Value : (discount == null ? 0 : ((discount.DiscountType.PercentForTotal == null || discount.DiscountType.PercentForTotal == 0) ? 0 : discount.DiscountType.PercentForTotal.Value)), 
                    FeeID = i.FeeID, FeeType = i.FeeCategory, Student_SectionID = i.studentsectionid, Description = i.Description });
            }

            var otherfees = computedassessment.Where(m => m.FeeCategory == "O").ToList();
            foreach (var i in otherfees)
            {
                otherfeeswithdiscount.Add(new Assessment { Amount = i.Amount, Discount = i.DiscountRate.HasValue
                    ? i.DiscountRate.Value : (discount == null ? 0 : ((discount.DiscountType.PercentForTotal == null || discount.DiscountType.PercentForTotal == 0) ? 0 : discount.DiscountType.PercentForTotal.Value)), 
                    FeeID = i.FeeID, FeeType = i.FeeCategory, Student_SectionID = i.studentsectionid, Description = i.Description });
            }

            List<GetReAssessment_Result> labair = new List<GetReAssessment_Result>();
            labair.AddRange(computedassessment.Where(m => m.FeeCategory == "A"));
            labair.AddRange(computedassessment.Where(m => m.FeeCategory == "L"));
            foreach (var i in labair)
            {
                labairconfeeswithdiscount.Add(new Assessment { Amount = i.Amount, Discount = i.DiscountRate.HasValue
                    ? i.DiscountRate.Value : (discount == null ? 0 : ((discount.DiscountType.PercentForTotal == null || discount.DiscountType.PercentForTotal == 0) ? 0 : discount.DiscountType.PercentForTotal.Value)), 
                    FeeID = i.FeeID, FeeType = i.FeeCategory, Student_SectionID = i.studentsectionid, Description = i.Description });
            }
        }

    }
}