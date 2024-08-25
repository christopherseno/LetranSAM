using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ARManila.Models;
namespace ARManila.Controllers.API
{
    public class DmcmApiController : ApiController
    {
        //[Route("LoadStudents/{periodid:int}")]
        //[Route("LoadStudents/{periodid:int}/{programid:int}/{gradeyear:int}")]
        //[Route("LoadStudents/{periodid:int}/{programid:int}")]
        //[Route("LoadStudents/{periodid:int}/{gradeyear:int}")]
        //[Route("LoadStudents/{scheduleid:int}")]
        //[Route("LoadStudents/{sectionid:int}")]
        [HttpGet]
        public IHttpActionResult LoadStudents(int? periodid, int? sectionid, int? scheduleid, int? programid, int? gradeyear)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            BatchDmcm batch = new BatchDmcm();
            if (scheduleid.HasValue)
            {
                var students = db.Student_Section.Where(m => m.ValidationDate.HasValue && m.StudentSchedule.Any(x => x.ScheduleID == scheduleid)).ToList();
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
            List<StudentForDmcm> batchstudents = new List<StudentForDmcm>();
            foreach (var item in batch.Students)
            {
                batchstudents.Add(new StudentForDmcm
                {
                    PaymentMode = item.Paymode.Description,
                    Section = item.Section.SectionName,
                    StudentId = item.StudentID,
                    StudentName = item.Student.FullName,
                    StudentNo = item.Student.StudentNo,
                    StudentSectionId = item.Student_SectionID,
                    ValidationDate = item.ValidationDate.Value.ToShortDateString()
                });
            }
            return Ok(batchstudents);
        }
    }
}
