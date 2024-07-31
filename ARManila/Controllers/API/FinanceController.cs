using ARManila.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace ARManila.Controllers
{
    public class FinanceController : ApiController
    {

        [HttpGet, Route("ARQueryByStudent/{id}/{periodid}")]
        [Authorize(Roles = "Student, IT")]
        public IHttpActionResult GetARQueryByStudent(string id, int periodid = 0)
        {
            try
            {
                if (User.Identity.Name != id)
                {
                    throw new Exception("Credential Problem.");
                }
                var arwrapper = GetStuddentAR(id, periodid);
                return Ok(arwrapper);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("ARQueryByFinance/{id}/{periodid}")]
        [Authorize(Roles = "Finance, IT")]
        public IHttpActionResult GetARQueryByFinance(string id, int periodid = 0)
        {
            try
            {
                var arwrapper = GetStuddentAR(id, periodid);
                return Ok(arwrapper);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [NonAction]
        public ARWrapper GetStuddentAR(string id, int periodid = 0)
        {
            try
            {
                using (LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities())
                {
                    var student = db.Student.FirstOrDefault(m => m.StudentNo == id);
                    if (student == null) throw new Exception("Student not found.");
                    if (periodid == 0)
                    {
                        var latestenrollment = db.Student_Section.Where(m => m.StudentID == student.StudentID).OrderByDescending(m => m.Student_SectionID).FirstOrDefault();
                        if (latestenrollment != null)
                        {
                            periodid = latestenrollment.Section.PeriodID;
                        }
                    }
                    ARWrapper arwrapper = new ARWrapper();
                    List<ARItem> aritems = new List<ARItem>();
                    double totalcredit = 0;
                    double totaldebit = 0;

                    var enrollment = db.Student_Section.Where(m => m.StudentID == student.StudentID && m.Section.PeriodID == periodid).FirstOrDefault();

                    if (enrollment == null) throw new Exception("No enrollment details found.");

                    arwrapper.Student = SetEnrolledStudent(enrollment, db);
                    GetDMCMs(periodid, db, student, aritems, ref totalcredit, ref totaldebit);

                    if (enrollment.AssessmentDate == null)
                    {
                        GetBackaccount(periodid, db, student, aritems, ref totalcredit, ref totaldebit);

                        totalcredit = GetPayments(periodid, db, student, aritems, totalcredit);
                        arwrapper.TotalCredit = totalcredit;
                        arwrapper.TotalDebit = totaldebit;
                        arwrapper.TotalBalance = totaldebit - totalcredit;
                    }
                    else
                    {
                        GetBackaccount(periodid, db, student, aritems, ref totalcredit, ref totaldebit);

                        totaldebit = GetAssessment(aritems, totaldebit, enrollment);

                        var discount = db.Discount.Where(m => m.StudentID == student.StudentID && m.PeriodID == periodid).FirstOrDefault();
                        if (discount != null)
                        {
                            double discountotal = 0;
                            GetVoucherAndTuitionDiscount(db, aritems, ref totalcredit, enrollment, discount, ref discountotal);

                            double miscelleneousdiscounttotal = GetMiscellaneousDiscount(db, aritems, enrollment, ref discountotal);

                            double laboratorydiscounttotal = GetLaboratoryDiscount(db, aritems, enrollment, ref discountotal);

                            double supplementarydiscounttotal = GetSupplementaryDiscount(db, aritems, enrollment, ref discountotal);

                            double otherfeesdiscounttotal = GetOtherFeesDiscount(db, aritems, enrollment, ref discountotal);
                            totalcredit += miscelleneousdiscounttotal + laboratorydiscounttotal + supplementarydiscounttotal + otherfeesdiscounttotal;
                        }

                        totalcredit = GetPayments(periodid, db, student, aritems, totalcredit);

                        totaldebit = GetAdjustments(db, aritems, totaldebit, enrollment);

                        double totalroundedoff = Math.Round(totalcredit + 0.0000001, 2);
                        double netdueroundedoff = Math.Round(totaldebit + 0.0000001, 2);
                        double balanceroundedoff = Math.Round((netdueroundedoff - totalroundedoff), 2);
                        arwrapper.TotalCredit = totalcredit;
                        arwrapper.TotalDebit = totaldebit;
                        arwrapper.TotalBalance = totaldebit - totalcredit;
                        GetARDueDates(periodid, db, arwrapper, enrollment);

                    }
                    arwrapper.ARItems = aritems.OrderBy(m => m.DocumentDate).ToList();
                    var armessage = db.ARMessage.Where(m => m.StudentID == student.StudentID).FirstOrDefault();
                    arwrapper.ARRemark = armessage == null ? "" : armessage.Message;
                    return arwrapper;
                }
            }
            catch (Exception exception)
            {
                throw exception;
            }
        }

        #region Refactored Methods
        private static void GetARDueDates(int periodid, LetranIntegratedSystemEntities db, ARWrapper arwrapper, Student_Section enrollment)
        {
            var balances = db.GetTotalBalance(enrollment.StudentID, periodid).FirstOrDefault();
            if (balances != null)
            {
                List<ARDueDate> arduedates = new List<ARDueDate>();
                if (balances.B1 > 0)
                {
                    ARDueDate duedate = new ARDueDate();
                    duedate.Payment = "Downpayment";
                    duedate.Amount = balances.B1.Value;
                    var ddate = db.PaySchedule.Where(m => m.PeriodID == periodid && m.PaycodeID == 1).FirstOrDefault();
                    if (ddate != null)
                    {
                        duedate.DueDate = ddate.DueDate.Value.ToShortDateString();
                    }
                    arduedates.Add(duedate);
                }
                if (enrollment.PaymodeID > 1)
                {
                    if (balances.B2 > 0)
                    {
                        ARDueDate duedate = new ARDueDate();
                        duedate.Payment = "2nd Payment";
                        duedate.Amount = balances.B2.Value - balances.B1.Value;
                        var ddate = db.PaySchedule.Where(m => m.PeriodID == periodid && m.PaymodeID == enrollment.PaymodeID && m.PaycodeID == 2).FirstOrDefault();
                        if (ddate != null)
                        {
                            duedate.DueDate = ddate.DueDate.Value.ToShortDateString();
                        }
                        arduedates.Add(duedate);
                    }

                    if (balances.B3 > 0)
                    {
                        ARDueDate duedate = new ARDueDate();
                        duedate.Payment = "3rd Payment";
                        duedate.Amount = balances.B3.Value - balances.B2.Value;
                        var ddate = db.PaySchedule.Where(m => m.PeriodID == periodid && m.PaymodeID == enrollment.PaymodeID && m.PaycodeID == 3).FirstOrDefault();
                        if (ddate != null)
                        {
                            duedate.DueDate = ddate.DueDate.Value.ToShortDateString();
                        }
                        arduedates.Add(duedate);
                    }
                    if (balances.B4 > 0)
                    {
                        ARDueDate duedate = new ARDueDate();
                        duedate.Payment = "4th Payment";
                        duedate.Amount = balances.B4.Value - balances.B3.Value;
                        var ddate = db.PaySchedule.Where(m => m.PeriodID == periodid && m.PaymodeID == enrollment.PaymodeID && m.PaycodeID == 4).FirstOrDefault();
                        if (ddate != null)
                        {
                            duedate.DueDate = ddate.DueDate.Value.ToShortDateString();
                        }
                        arduedates.Add(duedate);
                    }
                    if (balances.B5 > 0)
                    {
                        ARDueDate duedate = new ARDueDate();
                        duedate.Payment = "5th Payment";
                        duedate.Amount = balances.B5.Value - balances.B4.Value;
                        var ddate = db.PaySchedule.Where(m => m.PeriodID == periodid && m.PaymodeID == enrollment.PaymodeID && m.PaycodeID == 5).FirstOrDefault();
                        if (ddate != null)
                        {
                            duedate.DueDate = ddate.DueDate.Value.ToShortDateString();
                        }
                        arduedates.Add(duedate);
                    }
                    if (balances.B6 > 0 && enrollment.Curriculum.EducationalLevel <= 3)
                    {
                        ARDueDate duedate = new ARDueDate();
                        duedate.Payment = "6th Payment";
                        duedate.Amount = balances.B6.Value - balances.B5.Value;
                        var ddate = db.PaySchedule.Where(m => m.PeriodID == periodid && m.PaymodeID == enrollment.PaymodeID && m.PaycodeID == 6).FirstOrDefault();
                        if (ddate != null)
                        {
                            duedate.DueDate = ddate.DueDate.Value.ToShortDateString();
                        }
                        arduedates.Add(duedate);
                    }
                    if (balances.B7 > 0 && enrollment.Curriculum.EducationalLevel <= 3)
                    {
                        ARDueDate duedate = new ARDueDate();
                        duedate.Payment = "7th Payment";
                        duedate.Amount = balances.B7.Value - balances.B6.Value;
                        var ddate = db.PaySchedule.Where(m => m.PeriodID == periodid && m.PaymodeID == enrollment.PaymodeID && m.PaycodeID == 7).FirstOrDefault();
                        if (ddate != null)
                        {
                            duedate.DueDate = ddate.DueDate.Value.ToShortDateString();
                        }
                        arduedates.Add(duedate);
                    }
                    if (balances.B8 > 0 && enrollment.Curriculum.EducationalLevel <= 3)
                    {
                        ARDueDate duedate = new ARDueDate();
                        duedate.Payment = "8th Payment";
                        duedate.Amount = balances.B8.Value - balances.B7.Value;
                        var ddate = db.PaySchedule.Where(m => m.PeriodID == periodid && m.PaymodeID == enrollment.PaymodeID && m.PaycodeID == 8).FirstOrDefault();
                        if (ddate != null)
                        {
                            duedate.DueDate = ddate.DueDate.Value.ToShortDateString();
                        }
                        arduedates.Add(duedate);
                    }
                    if (balances.B9 > 0 && enrollment.Curriculum.EducationalLevel <= 3)
                    {
                        ARDueDate duedate = new ARDueDate();
                        duedate.Payment = "9th Payment";
                        duedate.Amount = balances.B9.Value - balances.B8.Value;
                        var ddate = db.PaySchedule.Where(m => m.PeriodID == periodid && m.PaymodeID == enrollment.PaymodeID && m.PaycodeID == 9).FirstOrDefault();
                        if (ddate != null)
                        {
                            duedate.DueDate = ddate.DueDate.Value.ToShortDateString();
                        }
                        arduedates.Add(duedate);
                    }
                }
                arwrapper.ARDueDates = arduedates;
            }
        }

        private static double GetAdjustments(LetranIntegratedSystemEntities db, List<ARItem> aritems, double netdue, Student_Section enrollment)
        {
            var adjustments = db.Adjustment.Where(m => m.StudentSectionID == enrollment.Student_SectionID).ToList();
            foreach (var i in adjustments)
            {
                double adjustmentamount = 0;
                var adjdet = db.AdjustmentDetails.Where(m => m.AdjustmentID == i.AdjustmentID).ToList();
                foreach (var ii in adjdet)
                {
                    double adjustmenttuition = ii.AdjTotalT.HasValue ? ii.AdjTotalT.Value : 0;
                    double adjustmentaircon = ii.AdjTotalA.HasValue ? ii.AdjTotalA.Value : 0;
                    double adjustmentlaboratory = ii.AdjTotalL.HasValue ? ii.AdjTotalL.Value : 0;
                    double adjustmentotherfee = ii.AdjTotalO.HasValue ? ii.AdjTotalO.Value : 0;
                    adjustmentamount += (adjustmenttuition + adjustmentaircon + adjustmentlaboratory + adjustmentotherfee);
                }
                ARItem adjustmentitem = new ARItem();
                adjustmentitem.Particular = "Adjustment";
                adjustmentitem.Credit = 0;
                adjustmentitem.Debit = adjustmentamount;
                adjustmentitem.DocumentNo = "AdjNo. " + i.AdjustmentID.ToString();
                adjustmentitem.DocumentDate = i.AdjustmentDate;
                netdue += adjustmentamount;
                aritems.Add(adjustmentitem);
            }

            return netdue;
        }

        private static double GetOtherFeesDiscount(LetranIntegratedSystemEntities db, List<ARItem> aritems, Student_Section enrollment, ref double discountotal)
        {
            double otherfeesdiscounttotal = 0;
            var otherfeesdiscount = db.Assessment.Where(m => m.Student_SectionID == enrollment.Student_SectionID && m.DiscountAmount > 0 && m.FeeType == "O").ToList();
            foreach (var i in otherfeesdiscount)
            {
                otherfeesdiscounttotal += i.DiscountAmount.Value;
            }
            if (otherfeesdiscount.Count > 0)
            {
                ARItem discountitem = new ARItem();
                discountitem.Particular = "Discount Other";
                discountitem.Credit = Math.Round(otherfeesdiscounttotal, 2);
                discountitem.Debit = 0;
                discountotal += otherfeesdiscounttotal;
                discountitem.DocumentNo = "OAFNo. " + enrollment.Student_SectionID;
                aritems.Add(discountitem);
            }

            return otherfeesdiscounttotal;
        }

        private static double GetSupplementaryDiscount(LetranIntegratedSystemEntities db, List<ARItem> aritems, Student_Section enrollment, ref double discountotal)
        {
            double supplementarydiscounttotal = 0;
            var supplementarydiscounts = db.Assessment.Where(m => m.Student_SectionID == enrollment.Student_SectionID && m.DiscountAmount > 0 && (m.FeeType == "S" || m.FeeType == "V")).ToList();

            foreach (var i in supplementarydiscounts)
            {
                supplementarydiscounttotal += i.DiscountAmount.Value;
            }
            if (supplementarydiscounts.Count > 0)
            {
                ARItem discountitem = new ARItem();
                discountitem.Particular = "Discount Supp";
                discountitem.Credit = Math.Round(supplementarydiscounttotal, 2);
                discountitem.Debit = 0;
                discountotal += supplementarydiscounttotal;
                discountitem.DocumentNo = "OAFNo. " + enrollment.Student_SectionID;
                aritems.Add(discountitem);
            }

            return supplementarydiscounttotal;
        }

        private static double GetLaboratoryDiscount(LetranIntegratedSystemEntities db, List<ARItem> aritems, Student_Section enrollment, ref double discountotal)
        {
            double laboratorydiscounttotal = 0;
            var laboratorydiscount = db.Assessment.Where(m => m.Student_SectionID == enrollment.Student_SectionID && m.DiscountAmount > 0 && (m.FeeType == "L" || m.FeeType == "A")).ToList();

            foreach (var i in laboratorydiscount)
            {
                laboratorydiscounttotal += i.DiscountAmount.Value;
            }
            if (laboratorydiscount.Count > 0)
            {
                ARItem discountitem = new ARItem();
                discountitem.Particular = "Discount Lab";
                discountitem.Credit = Math.Round(laboratorydiscounttotal, 2);
                discountitem.Debit = 0;
                discountotal += laboratorydiscounttotal;
                discountitem.DocumentNo = "OAFNo. " + enrollment.Student_SectionID;
                aritems.Add(discountitem);
            }

            return laboratorydiscounttotal;
        }

        private static double GetMiscellaneousDiscount(LetranIntegratedSystemEntities db, List<ARItem> aritems, Student_Section enrollment, ref double discountotal)
        {
            var miscellaneousdiscounts = db.Assessment.Where(m => m.Student_SectionID == enrollment.Student_SectionID && m.DiscountAmount > 0 && m.FeeType == "M").ToList();
            double miscd = 0;
            foreach (var i in miscellaneousdiscounts)
            {
                miscd += i.DiscountAmount.Value;
            }
            if (miscellaneousdiscounts.Count > 0)
            {
                ARItem discountitem = new ARItem();
                discountitem.Particular = "Discount Misc";
                discountitem.Credit = miscd;
                discountitem.Debit = 0;
                discountotal += miscd;
                discountitem.DocumentNo = "OAFNo. " + enrollment.Student_SectionID;
                aritems.Add(discountitem);
            }

            return miscd;
        }

        private static void GetVoucherAndTuitionDiscount(LetranIntegratedSystemEntities db, List<ARItem> aritems, ref double total, Student_Section enrollment, Discount discount, ref double discountotal)
        {
            if (discount.DiscountType.VoucherValue > 0)
            {
                ARItem voucher = new ARItem();
                voucher.Particular = "Voucher";
                voucher.Credit = discount.DiscountType.VoucherValue.Value;
                total += discount.DiscountType.VoucherValue.Value;
                discountotal += discount.DiscountType.VoucherValue.Value;
                voucher.DocumentNo = "OAFNo. " + enrollment.Student_SectionID;
                voucher.Remark = "CV/LRN: " + discount.CVLRN;
                aritems.Add(voucher);
            }
            if (discount.DiscountType.PercentForTotal > 0)
            {
                ARItem discountitem = new ARItem();
                discountitem.Particular = "Discount Tuition(" + discount.DiscountType.PercentForTotal * 100 + "%)";
                var tuitiondiscount = db.Assessment.Where(m => m.Student_SectionID == enrollment.Student_SectionID && m.DiscountAmount > 0 && m.FeeType == "T").FirstOrDefault();
                discountitem.Credit = tuitiondiscount == null ? 0 : Math.Round(tuitiondiscount.DiscountAmount.Value, 2);
                total += (tuitiondiscount == null ? 0 : tuitiondiscount.DiscountAmount.Value);
                discountotal += (tuitiondiscount == null ? 0 : tuitiondiscount.DiscountAmount.Value);
                discountitem.DocumentNo = "OAFNo. " + enrollment.Student_SectionID;
                aritems.Add(discountitem);
            }
            if (discount.DiscountType.PercentForTuition > 0)
            {

                ARItem discountitem = new ARItem();
                discountitem.Particular = "Discount Tuition(" + discount.DiscountType.PercentForTuition * 100 + "%)";
                var tuitiondiscount = db.Assessment.Where(m => m.Student_SectionID == enrollment.Student_SectionID && m.DiscountAmount > 0 && m.FeeType == "T").FirstOrDefault();
                discountitem.Credit = (tuitiondiscount == null ? 0 : Math.Round(tuitiondiscount.DiscountAmount.Value, 2));
                total += (tuitiondiscount == null ? 0 : tuitiondiscount.DiscountAmount.Value);
                discountotal += (tuitiondiscount == null ? 0 : tuitiondiscount.DiscountAmount.Value);
                discountitem.DocumentNo = "OAFNo. " + enrollment.Student_SectionID;
                aritems.Add(discountitem);
            }
        }

        private static double GetAssessment(List<ARItem> aritems, double netdue, Student_Section enrollment)
        {
            ARItem assessmentitem = new ARItem();
            assessmentitem.Particular = "Total Assessment";
            if (enrollment.AssessmentDate != null && enrollment.TuitionFee == null) throw new Exception("Re-assessment is needed.");

            assessmentitem.Debit = enrollment.AssessmentDate == null ? 0 : (enrollment.TuitionFee.HasValue ? enrollment.TuitionFee.Value : 0) + enrollment.MiscFee.Value + enrollment.LabFee.Value + enrollment.SuppFee.Value;
            netdue += assessmentitem.Debit;
            assessmentitem.DocumentNo = "OAFNo. " + enrollment.Student_SectionID;
            assessmentitem.DocumentDate = enrollment.AssessmentDate == null ? (DateTime?)null : enrollment.AssessmentDate.Value;
            aritems.Add(assessmentitem);
            return netdue;
        }

        private static double GetPayments(int periodid, LetranIntegratedSystemEntities db, Student student, List<ARItem> aritems, double total)
        {
            var payments = db.Payment.Where(m => m.SemID == periodid && m.StudentID == student.StudentID).ToList();
            foreach (var i in payments)
            {
                var paymentdetails = db.PaymentDetails.Where(m => m.PaymentID == i.PaymentID && (m.PaycodeID <= 11 || m.PaycodeID == 802)).OrderBy(m => m.PaycodeID).ToList();
                foreach (var ii in paymentdetails)
                {
                    if (i.CheckNo != null)
                    {
                        if (!i.CheckNo.Trim().Equals("CANCELLED"))
                        {
                            ARItem data = new ARItem();
                            data.Particular = ii.Paycode.Description;
                            data.Credit = ii.Amount.Value;
                            data.DocumentNo = "ORNo. " + i.ORNo;
                            data.DocumentDate = i.DateReceived;
                            data.Remark = i.CheckNo.Length > 12 ? "*CC-" + i.Bank.ToUpper() + "-" + i.CheckNo : (i.CheckNo.Length > 1 ? i.Bank.ToString().ToUpper() + "-" + i.CheckNo + "-" + i.Branch.ToString() : "");
                            aritems.Add(data);
                            total += ii.Amount.Value;
                        }
                    }
                    else
                    {
                        ARItem data = new ARItem();
                        data.Particular = ii.Paycode.Description;
                        data.Credit = ii.Amount.Value;
                        data.DocumentDate = i.DateReceived;
                        data.DocumentNo = "ORNo. " + i.ORNo;
                        aritems.Add(data);
                        total += ii.Amount.Value;
                    }
                }
            }

            return total;
        }

        private static void GetBackaccount(int periodid, LetranIntegratedSystemEntities db, Student student, List<ARItem> aritems, ref double totalcredit, ref double totaldebit)
        {
            var currentenrollment = db.Student_Section.Where(m => m.StudentID == student.StudentID && m.Section.PeriodID == periodid).FirstOrDefault();
            if (currentenrollment != null)
            {
                var backaccount = db.BackAccount.Where(m => m.Student_SectionID == currentenrollment.Student_SectionID).FirstOrDefault();
                if (backaccount != null)
                {
                    var actualbackaccount = backaccount.Balance;
                    double totalpayments = 0;
                    foreach (var item in backaccount.BackAccountPayment)
                    {
                        if (item.Payment.SemID != currentenrollment.Section.PeriodID)
                        {
                            foreach (var detail in item.Payment.PaymentDetails)
                            {
                                if (detail.PaycodeID == 11)
                                {
                                    totalpayments += detail.Amount ?? 0;
                                }
                            }
                        }
                    }
                    double totaldmcm = 0;
                    foreach (var item in backaccount.BackaccountDMCM)
                    {
                        if (item.DMCM.PeriodID != currentenrollment.Section.PeriodID && item.DMCM.ChargeToStudentAr == true)
                        {
                            if (item.DMCM.DC == "D")
                            {
                                totaldmcm += item.DMCM.Amount ?? 0;
                            }
                            else
                            {
                                totaldmcm -= item.DMCM.Amount ?? 0;
                            }
                        }
                    }
                    ARItem data2 = new ARItem();
                    data2.Particular = "Back Account";
                    double tempvalue = backaccount.Balance ?? 0 - totalpayments + totaldmcm;
                    if (tempvalue < 0)
                    {
                        data2.Debit = 0;
                        data2.Credit = 0 - tempvalue;
                        totalcredit += data2.Credit;
                    }
                    else
                    {

                        data2.Debit = tempvalue;
                        data2.Credit = 0;
                        totaldebit += tempvalue;
                    }

                    data2.Remark = backaccount.Period.SchoolYear.SchoolYearName + " " + backaccount.Period.Period1;
                    aritems.Add(data2);
                }
            }
            else
            {
                var backaccount = db.BackAccount.Where(m => m.StudentID == student.StudentID && m.Student_SectionID == null && m.SemID != periodid).OrderByDescending(m => m.BankAccountID).FirstOrDefault();
                if (backaccount != null)
                {
                    ARItem data2 = new ARItem();
                    data2.Particular = "Back Account";
                    if (backaccount.Balance < 0)
                    {
                        data2.Debit = backaccount.Balance.Value;
                        data2.Credit = 0;
                        totaldebit += backaccount.Balance.Value;
                    }
                    else
                    {
                        data2.Credit = 0 - backaccount.Balance.Value;
                        data2.Debit = 0;
                        totalcredit += (0 - backaccount.Balance.Value);
                    }
                    data2.Remark = backaccount.Period.SchoolYear.SchoolYearName + " " + backaccount.Period.Period1;
                    aritems.Add(data2);
                }
            }
        }

        private static void GetDMCMs(int periodid, LetranIntegratedSystemEntities db, Student student, List<ARItem> aritems, ref double total, ref double netdue)
        {
            var dmcms = db.DMCM.Where(m => m.StudentID == student.StudentID && m.PeriodID == periodid && m.ChargeToStudentAr == true).ToList();
            foreach (var i in dmcms)
            {
                ARItem data = new ARItem();
                data.Particular = "DMCM";
                if (i.DC.Equals("C"))
                {
                    data.Credit = i.Amount.Value;
                    total += i.Amount.Value;
                }
                if (i.DC.Equals("D"))
                {
                    data.Debit = i.Amount.Value;
                    netdue += i.Amount.Value;
                }
                data.DocumentNo = "Doc No." + i.DocNum.ToString();
                data.DocumentDate = i.TransactionDate.Value;
                data.Remark = i.Remarks;
                aritems.Add(data);
            }
        }

        private EnrolledStudent SetEnrolledStudent(Student_Section enrollment, LetranIntegratedSystemEntities db)
        {
            EnrolledStudent enrolledStudent = new EnrolledStudent
            {
                AssessmentId = enrollment.Student_SectionID,
                AssessmentDate = enrollment.AssessmentDate,
                Curriculum = enrollment.Section.Curriculum.Curriculum1,
                CurriculumId = enrollment.SectionID,
                EnlistmentDate = enrollment.EnlistmentDate,
                Level = enrollment.Section.GradeYear.ToString(),
                EducationalLevel = enrollment.Section.Period.EducationalLevel1.EducLevelName,
                EducationalLevelId = enrollment.Section.PeriodID,
                PaymentMode = enrollment.Paymode.Description,
                PaymentModeId = enrollment.PaymodeID,
                Period = enrollment.Section.Period.SchoolYear.SchoolYearName + ", " + enrollment.Section.Period.Period1,
                PeriodId = enrollment.Section.PeriodID,
                Section = enrollment.Section.SectionName,
                SectionId = enrollment.SectionID,
                StudentId = enrollment.StudentID,
                StudentName = enrollment.Student.FullName,
                StudentNumber = enrollment.Student.StudentNo,
                ValidationDate = enrollment.ValidationDate,
                StatusId = enrollment.StudentStatus,
                Status = enrollment.StudentStatus1.StudentStatusDescription
            };
            var curriculumprogam = db.ProgamCurriculum.Where(m => m.CurriculumID == enrollment.Section.CurriculumID).FirstOrDefault();
            enrolledStudent.Program = curriculumprogam.Progam.ProgramCode;
            enrolledStudent.ProgramId = curriculumprogam.ProgramID;
            return enrolledStudent;
        }
        #endregion
    }
}
