using ARManila.Models.OtherDTO;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public partial class LetranIntegratedSystemEntities  : DbContext
    {
        public List<ARTrailWrapper> GetArTrailBySchoolYear(int schoolyearid, int educlevelid, string asofdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                var result = context.Database.SqlQuery<ARTrailWrapper>(
                    "EXEC [AR].[ArTrailBySchoolYear] @schoolyearid ,@educlevelid, @asofdate",
                    new SqlParameter("@schoolyearid", schoolyearid),
                    new SqlParameter("@educlevelid", educlevelid),
                    new SqlParameter("@asofdate", asofdate)
                ).ToList();

                return result;
            }
        }
        public List<ARTrailWrapper> GetArTrailBySchoolYearWithDept(int schoolyearid, int educlevelid, string asofdate)
        {
            using (var context = new LetranIntegratedSystemEntities())
            {
                var result = context.Database.SqlQuery<ARTrailWrapper>(
                    "EXEC [AR].[ArTrailBySchoolYear] @schoolyearid ,@educlevelid, @asofdate",
                    new SqlParameter("@schoolyearid", schoolyearid),
                    new SqlParameter("@educlevelid", educlevelid),
                    new SqlParameter("@asofdate", asofdate)
                ).ToList();

                return result;
            }
        }

    }
}