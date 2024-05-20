using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ARManila.Models;
namespace ARManila.Controllers.API
{
    public class PeriodSelectorController : ApiController
    {
        LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        [Route("api/EducationalLevels")]
        [HttpGet]
        public IHttpActionResult EducationLevel()
        {
            try
            {
                var data = new List<EducationalLevelWrapper>();
                var educationals = db.EducationalLevel.ToList();
                foreach (var i in educationals)
                {
                    data.Add(new EducationalLevelWrapper
                    {
                        EducationalLevelId = i.EducLevelID,
                        EducationalLevelName = i.EducLevelName
                    });
                }
                return Ok(data);
            }
            catch
            {
                return InternalServerError();
            }
        }
        [Route("api/SchoolYears")]
        [HttpGet]
        public IHttpActionResult SchoolYear()
        {
            try
            {
                var data = new List<SchoolYearWrapper>();
                var schoolyears = db.SchoolYear.ToList();
                foreach (var i in schoolyears)
                {
                    data.Add(new SchoolYearWrapper
                    {
                        SchoolYearId = i.SYID,
                        SchoolYearName = i.SchoolYearName
                    });
                }
                return Ok(data);
            }
            catch
            {
                return InternalServerError();
            }
        }
        [Route("api/Periods")]
        [HttpGet]
        public IHttpActionResult Period()
        {
            try
            {
                var data = new List<PeriodWrapper>();
                var periods = db.Period.ToList();
                foreach (var i in periods)
                {
                    data.Add(new PeriodWrapper
                    {
                        EducationalLevelId = i.EducLevelID.HasValue  ? i.EducLevelID.Value : 0,
                        FullName = i.SchoolYear.SchoolYearName + (i.EducLevelID.HasValue ? ", " + (i.EducLevelID.Value >= 4 ? i.Period1 : i.EducationalLevel1.EducLevelName) : ""),
                        PeriodId = i.PeriodID,
                        PeriodName = i.Period1,
                        SchoolYearId = i.SchoolYearID
                    }); ;
                }
                return Ok(data);
            }
            catch(Exception ex)
            {
                return InternalServerError(ex);
            }
        }
        [Route("api/Schedule/{id:int}")]
        [HttpGet]
        public IHttpActionResult Schedules(int id)
        {
            try
            {
                var data = new List<ScheduleWrapper>();
                var schedules = db.Schedule.Where(m => m.Section.PeriodID == id);
                foreach (var item in schedules)
                {
                    ScheduleWrapper schedule = new ScheduleWrapper();
                    schedule.Day = item.Days ?? "";
                    schedule.Description = item.Subject.Description;
                    schedule.Faculty = item.FacultyID.HasValue ? item.Faculty.FacultyName : "";
                    schedule.GoogleClassroomCode = item.EnrollmentCode ?? "";
                    schedule.Room = item.RoomID.HasValue ? item.Room.RoomName : "";
                    schedule.ScheduleId = item.ScheduleID;
                    schedule.Section = item.Section.SectionName;
                    schedule.Status = item.ScheduleStatusID.HasValue ? item.ScheduleStatus.StatusName : "";
                    schedule.Subject = item.Subject.SubjectCode;
                    schedule.Time = (item.StartTime.HasValue ? item.StartTime.Value.ToString() : "") + "-" +
                        (item.EndTime.HasValue ? item.EndTime.Value.ToString() : "");
                    schedule.Units = item.Subject.Units.ToString("##0.00");
                    data.Add(schedule);
                }
                return Ok(data);
            }
            catch
            {
                return InternalServerError();
            }
        }
    }

    public class ScheduleWrapper
    {
        public int ScheduleId { get; set; }
        public string Section { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public string Faculty { get; set; }
        public string Day { get; set; }
        public string Time { get; set; }
        public string Room { get; set; }
        public string GoogleClassroomCode { get; set; }
        public string Units { get; set; }
        public string Status { get; set; }
    }

    public class EducationalLevelWrapper
    {
        public int EducationalLevelId { get; set; }
        public string EducationalLevelName { get; set; }
    }
    public class SchoolYearWrapper
    {
        public int SchoolYearId { get; set; }
        public string SchoolYearName { get; set; }
    }
    public class PeriodWrapper
    {
        public int PeriodId { get; set; }
        public int SchoolYearId { get; set; }
        public int EducationalLevelId { get; set; }
        public string PeriodName { get; set; }
        public string FullName { get; set; }
    }

}
