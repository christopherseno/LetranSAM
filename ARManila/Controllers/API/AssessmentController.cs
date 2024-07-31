using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ARManila.Models;
using System.Web.Http.Cors;

namespace ARManila.Controllers
{
    [RoutePrefix("Assessment")]
    [Authorize]
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class AssessmentController : ApiController
    {
        readonly LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();

        // GET: Assessment
        [HttpGet]
        [Route("{id:int}/{willsave:bool}")]
        [Authorize(Roles = "Student, IT")]
        public AssessmentDTO StudentSelfAssessment(int id, bool willsave)
        {
            var studentno = User.Identity.Name;
            var student = db.Student.Where(m => m.StudentNo == studentno).FirstOrDefault();
            var enlistment = db.Student_Section.Find(id);
            if (enlistment == null)
            {
                throw new Exception("Enlistment id doest not exist.");
            }
            if (enlistment.StudentID != student.StudentID)
            {
                throw new Exception("Credential problem. See ITSD!");
            }
            if (enlistment.ValidationDate != null)
            {
                throw new Exception("Current enrollment was already validated last !" + enlistment.ValidationDate.Value.ToLongDateString());
            }
            if (enlistment.AssessmentDate != null)
            {
                throw new Exception("Current enrollment was already assessed last !" + enlistment.AssessmentDate.Value.ToLongDateString());
            }
            return Assessment(id, willsave);
        }

        [HttpGet]
        [Route("dean/{id:int}")]
        [Authorize(Roles = "Administrator, IT")]
        public AssessmentDTO DeanApprovalAssessment(int id)
        {
            var enlistment = db.Student_Section.Find(id);
            if (enlistment.ValidationDate != null)
            {
                throw new Exception("Current enrollment was already validated last !" + enlistment.ValidationDate.Value.ToLongDateString());
            }
            if (enlistment.AssessmentDate != null)
            {
                throw new Exception("Current enrollment was already assessed last !" + enlistment.AssessmentDate.Value.ToLongDateString());
            }
            return Assessment(id, true);
        }

        [HttpGet]
        [Route("finance/{id:int}")]
        [Authorize(Roles = "Finance, IT")]
        public AssessmentDTO FADAssessment(int id)
        {
            //check if approved by the dean
            return Assessment(id, true);
        }

        [NonAction]
        private AssessmentDTO Assessment(int id, bool willsave)
        {
            var enlistment = db.Student_Section.Find(id);
            CheckExceptions(enlistment);

            int tuitionid = 0;
            decimal voucher = 0;
            decimal firstpayment = 0, secondpayment = 0;
            var student = db.Student.Find(enlistment.StudentID);
            var periodid = enlistment.Section.PeriodID;
            var computedassessment = db.GetAssessment(enlistment.Student_SectionID).ToList();
            var discount = db.Discount.Where(m => m.StudentID == enlistment.StudentID && m.PeriodID == periodid).FirstOrDefault();
            var fixeddownpayment = computedassessment.Where(m => m.FeeCategory == "D").FirstOrDefault();
            var istuition = computedassessment.Where(m => m.FeeCategory == "T").FirstOrDefault();

            List<Assessment> miscfeeswithdiscount = new List<Assessment>();
            List<Assessment> supplementalfeeswithdiscount = new List<Assessment>();
            List<Assessment> labfeeswithddiscount = new List<Assessment>();
            List<Assessment> otherfeeswithdiscount = new List<Assessment>();
            List<Assessment> variousfeeswithdiscount = new List<Assessment>();
            List<Assessment> labairconfeeswithdiscount = new List<Assessment>();

            decimal totalprocessingfee = ComputeProcessingFee(student, periodid);

            ComputeAssessmentDetails(miscfeeswithdiscount, out supplementalfeeswithdiscount, out otherfeeswithdiscount, out variousfeeswithdiscount, labairconfeeswithdiscount, computedassessment, discount);

            ComputeTotalAssessment(enlistment, ref tuitionid, out decimal tuitionfee, out decimal totalmiscfee, out decimal totallabfee, out decimal totalotherfee, out decimal totalvariousfee, out decimal totalairconfee, out decimal totalsupplemtalfee, out decimal totalfee, out decimal creditamount, out decimal totaldiscount, out decimal netassessmentamount, ref voucher, computedassessment, discount);

            var otherpayment = totalmiscfee + totallabfee + totalotherfee + totalvariousfee + totalsupplemtalfee + totalairconfee;

            ComputeFirstAndSecondPayment(enlistment, creditamount, netassessmentamount, ref firstpayment, ref secondpayment, fixeddownpayment, otherpayment);

            List<PaymentSchedule> paymentschedules = SetupPaymentSchedule(enlistment, firstpayment, secondpayment);
            AssessmentDTO assessment = SetupAssessmentDTO(enlistment, tuitionfee, totalmiscfee, totallabfee, totalotherfee, totalvariousfee, totalairconfee, totalsupplemtalfee, totalfee, creditamount, totaldiscount, netassessmentamount, firstpayment, student, periodid, totalprocessingfee, otherpayment, paymentschedules);

            SaveAssessment(willsave, enlistment, miscfeeswithdiscount, supplementalfeeswithdiscount, otherfeeswithdiscount, variousfeeswithdiscount, labairconfeeswithdiscount, tuitionid, tuitionfee, secondpayment, discount, assessment);

            return assessment;
        }

        private static void CheckExceptions(Student_Section enlistment)
        {
            if (enlistment == null)
            {
                throw new Exception("Enlistment id doest not exist.");
            }
            if (enlistment.PaymodeID == null)
            {
                throw new Exception("No Payment mode is set.");
            }
            if (enlistment.StudentSchedule.Count() == 0)
            {
                throw new Exception("Student has no enlisted subjects.");
            }
            if (enlistment.PaymodeID == 2 && enlistment.Section.Period.EducLevelID == 4)
            {
                throw new Exception("Invalid payment mode.");
            }
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

        private void SaveAssessment(bool willsave, Student_Section enlistment, List<Assessment> miscfeeswithdiscount, List<Assessment> supplementalfeeswithdiscount, List<Assessment> otherfeeswithdiscount, List<Assessment> variousfeeswithdiscount, List<Assessment> labairconfeeswithdiscount, int tuitionid, decimal tuitionfee, decimal secondpayment, Discount discount, AssessmentDTO assessment)
        {
            if (willsave)
            {

                var currentassessmentdetails = db.Assessment.Where(m => m.Student_SectionID == enlistment.Student_SectionID);
                db.Assessment.RemoveRange(currentassessmentdetails);
                db.SaveChanges();

                var tuitiondiscountrate = discount == null ? 0 : (discount.DiscountType.PercentForTotal ?? (discount.DiscountType.PercentForTuition ?? 0));
                var allotherfeesdiscountrate = discount == null ? 0 : (discount.DiscountType.PercentForTotal ?? (discount.DiscountType.PercentForMisc ?? 0));
                Assessment tuitionassessment = new Assessment();
                tuitionassessment.Amount = (double)tuitionfee;
                tuitionassessment.Discount = tuitiondiscountrate;
                tuitionassessment.Description = "Tuition";
                tuitionassessment.FeeType = "T";
                tuitionassessment.FeeID = tuitionid;
                tuitionassessment.Student_SectionID = enlistment.Student_SectionID;
                db.Assessment.Add(tuitionassessment);
                foreach (var i in miscfeeswithdiscount)
                {
                    Assessment miscassessment = new Assessment();
                    miscassessment.Amount = ((Assessment)i).Amount;
                    miscassessment.Description = ((Assessment)i).Description;
                    miscassessment.FeeType = ((Assessment)i).FeeType;
                    miscassessment.FeeID = ((Assessment)i).FeeID;
                    miscassessment.Discount = ((Assessment)i).Discount;
                    miscassessment.Student_SectionID = enlistment.Student_SectionID;
                    db.Assessment.Add(miscassessment);
                }
                foreach (var i in supplementalfeeswithdiscount)
                {
                    Assessment supplementalassessment = new Assessment();
                    supplementalassessment.Amount = ((Assessment)i).Amount;
                    supplementalassessment.Description = ((Assessment)i).Description;
                    supplementalassessment.FeeType = ((Assessment)i).FeeType;
                    supplementalassessment.FeeID = ((Assessment)i).FeeID;
                    supplementalassessment.Discount = ((Assessment)i).Discount;
                    supplementalassessment.Student_SectionID = enlistment.Student_SectionID;
                    db.Assessment.Add(supplementalassessment);
                }
                foreach (var i in labairconfeeswithdiscount)
                {
                    Assessment labairconassessment = new Assessment();
                    labairconassessment.Amount = ((Assessment)i).Amount;
                    labairconassessment.Description = ((Assessment)i).Description;
                    labairconassessment.FeeType = ((Assessment)i).FeeType;
                    labairconassessment.FeeID = ((Assessment)i).FeeID;
                    labairconassessment.Discount = ((Assessment)i).Discount;
                    labairconassessment.Student_SectionID = enlistment.Student_SectionID;
                    db.Assessment.Add(labairconassessment);
                }
                foreach (var i in variousfeeswithdiscount)
                {
                    Assessment variousfeeassessment = new Assessment();
                    variousfeeassessment.Discount = ((Assessment)i).Discount;
                    variousfeeassessment.Amount = ((Assessment)i).Amount;
                    variousfeeassessment.Description = ((Assessment)i).Description;
                    variousfeeassessment.FeeType = ((Assessment)i).FeeType;
                    variousfeeassessment.FeeID = ((Assessment)i).FeeID;
                    variousfeeassessment.Student_SectionID = enlistment.Student_SectionID;
                    db.Assessment.Add(variousfeeassessment);
                }
                foreach (var i in otherfeeswithdiscount)
                {
                    Assessment otherfeesassessment = new Assessment();
                    otherfeesassessment.Amount = ((Assessment)i).Amount;
                    otherfeesassessment.Discount = ((Assessment)i).Discount;
                    otherfeesassessment.Description = ((Assessment)i).Description;
                    otherfeesassessment.FeeType = ((Assessment)i).FeeType;
                    otherfeesassessment.FeeID = ((Assessment)i).FeeID;
                    otherfeesassessment.Student_SectionID = enlistment.Student_SectionID;
                    db.Assessment.Add(otherfeesassessment);
                }

                db.SaveChanges();

                var studentsection = db.Student_Section.Find(enlistment.Student_SectionID);
                studentsection.AssessmentDate = DateTime.Now;
                studentsection.Credit = (double)assessment.Credit;
                studentsection.Discount = (double)assessment.Discount;
                studentsection.DownPayment = (double)assessment.Down;
                studentsection.LabFee = (double)assessment.Lab;
                studentsection.MiscFee = (double)assessment.Misc;
                studentsection.SecondPayment = (double)secondpayment;
                studentsection.SuppFee = (double)assessment.Various;
                studentsection.TuitionFee = (double)assessment.Tuition;
                db.SaveChanges();
            }
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

        private static void ComputeFirstAndSecondPayment(Student_Section enlistment, decimal creditamount, decimal netassessmentamount, ref decimal firstpayment, ref decimal secondpayment, GetAssessment_Result fixeddownpayment, decimal otherpayment)
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

        private void ComputeTotalAssessment(Student_Section enlistment, ref int tuitionid, out decimal tuitionfee, out decimal totalmiscfee, out decimal totallabfee, out decimal totalotherfee, out decimal totalvariousfee, out decimal totalairconfee, out decimal totalsupplemtalfee, out decimal totalfee, out decimal creditamount, out decimal totaldiscount, out decimal netassessmentamount, ref decimal voucher, List<GetAssessment_Result> computedassessment, Discount discount)
        {
            var assessedtuitionfee = computedassessment.Where(m => m.FeeCategory == "T").FirstOrDefault();
            if (assessedtuitionfee == null)
            {
                tuitionfee = 0;
            }
            else
            {
                tuitionfee = (decimal)(assessedtuitionfee.Amount.Value);
                tuitionid = assessedtuitionfee.FeeID;
            }

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
                if (discount.DiscountType.VoucherValue != null)
                {
                    voucher = (decimal)discount.DiscountType.VoucherValue.Value;
                }
                if (discount.DiscountType.PercentForTuition != null)
                {
                    totaldiscount += (tuitionfee - voucher) * (decimal)discount.DiscountType.PercentForTuition.Value;
                    if (discount.DiscountType.PercentForMisc != null)
                    {
                        totaldiscount += (totalmiscfee + totallabfee + totalairconfee + totalotherfee + totalvariousfee + totalsupplemtalfee) * (decimal)discount.DiscountType.PercentForMisc.Value;
                    }
                }
                else if (discount.DiscountType.PercentForTotal != null)
                {
                    totaldiscount += totalfee * (decimal)discount.DiscountType.PercentForTotal.Value;
                }
            }
            netassessmentamount = totalfee - totaldiscount;
        }

        private static void ComputeAssessmentDetails(List<Assessment> miscfeeswithdiscount, out List<Assessment> supplementalfeeswithdiscount, out List<Assessment> otherfeeswithdiscount, out List<Assessment> variousfeeswithdiscount, List<Assessment> labairconfeeswithdiscount, List<GetAssessment_Result> computedassessment, Discount discount)
        {
            var miscfees = computedassessment.Where(m => m.FeeCategory == "M").ToList();
            foreach (var i in miscfees)
            {
                miscfeeswithdiscount.Add(new Assessment { Amount = i.Amount, Discount = discount == null ? 0 : ((discount.DiscountType.PercentForTotal == null || discount.DiscountType.PercentForTotal == 0) ? (discount.DiscountType.PercentForMisc == null ? 0 : discount.DiscountType.PercentForMisc.Value) : discount.DiscountType.PercentForTotal.Value), FeeID = i.FeeID, FeeType = i.FeeCategory, Student_SectionID = i.studentsectionid, Description = i.Description });
            }

            supplementalfeeswithdiscount = new List<Assessment>();
            var supplementalfees = computedassessment.Where(m => m.FeeCategory == "S").ToList();
            foreach (var i in supplementalfees)
            {
                supplementalfeeswithdiscount.Add(new Assessment { Amount = i.Amount, Discount = discount == null ? 0 : ((discount.DiscountType.PercentForTotal == null || discount.DiscountType.PercentForTotal == 0) ? 0 : discount.DiscountType.PercentForTotal.Value), FeeID = i.FeeID, FeeType = i.FeeCategory, Student_SectionID = i.studentsectionid, Description = i.Description });
            }

            variousfeeswithdiscount = new List<Assessment>();
            var variousfees = computedassessment.Where(m => m.FeeCategory == "V").ToList();
            foreach (var i in variousfees)
            {
                variousfeeswithdiscount.Add(new Assessment { Amount = i.Amount, Discount = discount == null ? 0 : ((discount.DiscountType.PercentForTotal == null || discount.DiscountType.PercentForTotal == 0) ? 0 : discount.DiscountType.PercentForTotal.Value), FeeID = i.FeeID, FeeType = i.FeeCategory, Student_SectionID = i.studentsectionid, Description = i.Description });
            }

            otherfeeswithdiscount = new List<Assessment>();
            var otherfees = computedassessment.Where(m => m.FeeCategory == "O").ToList();
            foreach (var i in otherfees)
            {
                otherfeeswithdiscount.Add(new Assessment { Amount = i.Amount, Discount = discount == null ? 0 : ((discount.DiscountType.PercentForTotal == null || discount.DiscountType.PercentForTotal == 0) ? 0 : discount.DiscountType.PercentForTotal.Value), FeeID = i.FeeID, FeeType = i.FeeCategory, Student_SectionID = i.studentsectionid, Description = i.Description });
            }

            List<GetAssessment_Result> labair = new List<GetAssessment_Result>();
            labair.AddRange(computedassessment.Where(m => m.FeeCategory == "A"));
            labair.AddRange(computedassessment.Where(m => m.FeeCategory == "L"));
            foreach (var i in labair)
            {
                labairconfeeswithdiscount.Add(new Assessment { Amount = i.Amount, Discount = discount == null ? 0 : ((discount.DiscountType.PercentForTotal == null || discount.DiscountType.PercentForTotal == 0) ? 0 : discount.DiscountType.PercentForTotal.Value), FeeID = i.FeeID, FeeType = i.FeeCategory, Student_SectionID = i.studentsectionid, Description = i.Description });
            }
        }
    }
}
