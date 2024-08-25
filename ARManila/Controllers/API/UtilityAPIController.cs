using ARManila.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Cors;

namespace ARManila.Controllers
{
    [Authorize(Roles = "Finance, IT")]
    [RoutePrefix("")]
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class UtilityAPIController : ApiController
    {
        
        [HttpGet, Route("StudentSchedule/{studentsectionid:int}")]
        public HttpResponseMessage StudentSchedule(int studentsectionid)
        {

            var db = new LetranIntegratedSystemEntities();
            var enrollment = db.Student_Section.Find(studentsectionid);
            if (enrollment == null)
            {
                var errorresponse = Request.CreateErrorResponse(HttpStatusCode.NotFound, new Exception("Illegal Operation. Enrollment Not Found"));
                return errorresponse;
            }

            var studentschedules = db.StudentSchedule.Where(m => m.StudentSectionID == enrollment.Student_SectionID);
            List<StudentScheduleWrapper> schedules = new List<StudentScheduleWrapper>();
            foreach (var item in studentschedules)
            {
                schedules.Add(new StudentScheduleWrapper
                {
                    Day = item.Schedule.Days == null || item.Schedule.Days.Length == 0 ? "" : item.Schedule.Days,
                    Faculty = item.Schedule.FacultyID == null ? "" : item.Schedule.Faculty.Employee.FullName,
                    Room = item.Schedule.RoomID == null ? "" : item.Schedule.Room.RoomName,
                    Subject = item.Schedule.Subject.SubjectCode,
                    Time = (item.Schedule.StartTime == null ? "" : item.Schedule.StartTime.Value.ToString()) + (item.Schedule.EndTime == null ? "" : item.Schedule.EndTime.Value.ToString())
                });
            }
            var response = Request.CreateResponse<List<StudentScheduleWrapper>>(HttpStatusCode.OK, schedules);
            return response;
        }

        [HttpGet, Route("SearchStudent/{searchtext}")]
        public IHttpActionResult SearchStudent(string searchtext)
        {
            try
            {
                List<StudentWrapper> students = new List<StudentWrapper>();
                using (var db = new LetranIntegratedSystemEntities())
                {
                    SqlConnection con = new SqlConnection(db.Database.Connection.ConnectionString);
                    SqlCommand cmd = new SqlCommand("select studentid, studentno, [dbo].[DecryptText](StudentNo+'1T3@mWoRk0', LastName) as lastname, [dbo].[DecryptText](StudentNo+'1T3@mWoRk0', FirstName) as firstname, mobileno, [dbo].[DecryptText](StudentNo+'1T3@mWoRk0',EmailAddress) as Email from student where studentno like '" + searchtext + "%' or [dbo].[DecryptText](StudentNo+'1T3@mWoRk0', LastName) like '%" + searchtext + "%' or [dbo].[DecryptText](StudentNo+'1T3@mWoRk0', FirstName) like '%" + searchtext + "%'", con);
                    con.Open();
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        int sid = reader.GetInt32(0);
                        StudentWrapper student = new StudentWrapper();
                        student.LastName = reader.GetString(2);
                        student.FirstName = reader.GetString(3);
                        student.StudentNo = reader.GetString(1);
                        student.MobileNo = reader[4] == DBNull.Value ? "" : reader.GetString(4);
                        student.Email = reader[5] == DBNull.Value ? "" : reader.GetString(5);
                        var studentcurriculum = db.Student_Curriculum.Where(m => m.StudentID == sid && m.Status == 1).FirstOrDefault();
                        if (studentcurriculum != null)
                        {
                            var program = db.ProgamCurriculum.Where(m => m.CurriculumID == studentcurriculum.CurriculumID).FirstOrDefault();
                            student.Program = program == null ? studentcurriculum.Curriculum.Curriculum1 : program.Progam.ProgramCode;
                        }
                        else
                        {
                            student.Program = "";
                        }
                        students.Add(student);
                    }
                }
                return Ok(students);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
        [HttpGet, Route("SearchOrNo/{searchtext}")]
        public IHttpActionResult SearchOrNo(string searchtext)
        {
            try
            {
                List<StudentORWrapper> studentors = new List<StudentORWrapper>();
                using (var db = new LetranIntegratedSystemEntities())
                {
                    SqlConnection con = new SqlConnection(db.Database.Connection.ConnectionString);
                    SqlCommand cmd = new SqlCommand("select a.studentid, a.studentno, [dbo].[DecryptText](a.StudentNo+'1T3@mWoRk0', a.LastName) as lastname, "
                        + "[dbo].[DecryptText](a.StudentNo + '1T3@mWoRk0', a.FirstName) as firstname,b.ORNo, b.DateReceived, d.Description,"
                        + "c.Amount from student a join Payment b on a.StudentID = b.StudentID join PaymentDetails c on " +
                        "c.PaymentID = b.PaymentID join Paycode d on d.PaycodeID = c.PaycodeID where a.studentno like '" + searchtext
                        + "%' or[dbo].[DecryptText](a.StudentNo + '1T3@mWoRk0', a.LastName) like '%" + searchtext
                        + "%' or[dbo].[DecryptText](a.StudentNo + '1T3@mWoRk0', a.FirstName) like '%" + searchtext + "%'", con);
                    con.Open();
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        //Type fieldType0 = reader.GetFieldType(0);
                        //Type fieldType7 = reader.GetFieldType(7);                        
                        StudentORWrapper studentor = new StudentORWrapper();
                        studentor.Fullname = reader.GetString(2) + ", " + reader.GetString(3);
                        studentor.StudentNo = reader.GetString(1);
                        studentor.Amount = Convert.ToDecimal(reader.GetDouble(7));
                        studentor.Date = reader.GetDateTime(5).ToString("yyyy-MM-dd");
                        studentor.Description = reader.GetString(6);
                        studentor.OrNumber = reader.GetString(4);
                        //var studentcurriculum = db.Student_Curriculum.Where(m => m.StudentID == sid && m.Status == 1).FirstOrDefault();

                        studentors.Add(studentor);
                    }
                }
                return Ok(studentors);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("SearchAlphaList/{searchtext}")]
        public IHttpActionResult SearchStudentAlpha(string searchtext)
        {
            try
            {
                using (var db = new LetranIntegratedSystemEntities())
                {
                    List<AlphaStudent> students = new List<AlphaStudent>();
                    var alphalist = db.Alpha4.Where(m => m.LastName.Contains(searchtext) || m.FirstName.Contains(searchtext));
                    foreach (var item in alphalist)
                    {
                        students.Add(new AlphaStudent
                        {
                            BADate = item.BADate,
                            FullName = item.FullName,
                            Id = item.Id,
                            Level = item.EducationalLevel.EducLevelName,
                            StudentNo = item.StudentId.HasValue ? item.Student.StudentNo : "N/A"
                        });
                    }
                    return Ok(students);
                }

            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
        [AllowAnonymous]
        [HttpGet, Route("enrollmentstat/{id:int}/{enddate:datetime}")]
        public HttpResponseMessage Get(int id, DateTime enddate)
        {
            try
            {
                LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
                db.Database.CommandTimeout = 600;
                var enrollmentstat = db.GetEnrollmentStat2(id, enddate).ToList();
                var response = Request.CreateResponse<List<GetEnrollmentStat2_Result>>(HttpStatusCode.OK, enrollmentstat);
                return response;
            }
            catch (Exception exception)
            {
                var response = Request.CreateErrorResponse(HttpStatusCode.BadRequest, exception);
                return response;
            }
        }
    }
}
