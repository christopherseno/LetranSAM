using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
using CrystalDecisions.CrystalReports.Engine;

namespace ARManila.Controllers
{
    [Authorize(Roles = "Finance, IT")]
    [Period]
    [HandleError]
    public class BaseController : Controller
    {
        protected override void Initialize(System.Web.Routing.RequestContext requestContext)
        {
            base.Initialize(requestContext);
        }

        public FileStreamResult ExportType(int type, string fileName, ReportDocument report)
        {
            Stream stream;
            switch (type)
            {
                case 1:
                    Response.Buffer = false;
                    Response.ClearContent();
                    Response.ClearHeaders();
                    stream = report.ExportToStream(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat);
                    stream.Seek(0, SeekOrigin.Begin);
                    report.Close();
                    report.Dispose();
                    return File(stream, "application/pdf", string.Concat(fileName.Trim(), ".pdf"));
                case 2:
                    Response.Buffer = false;
                    Response.ClearContent();
                    Response.ClearHeaders();
                    stream = report.ExportToStream(CrystalDecisions.Shared.ExportFormatType.Excel);
                    stream.Seek(0, SeekOrigin.Begin);
                    report.Close();
                    report.Dispose();
                    return File(stream, "application/ms-excel", string.Concat(fileName.Trim(), ".xls"));
                default:
                    Response.Buffer = false;
                    Response.ClearContent();
                    Response.ClearHeaders();
                    stream = report.ExportToStream(CrystalDecisions.Shared.ExportFormatType.Excel);
                    stream.Seek(0, SeekOrigin.Begin);
                    report.Close();
                    report.Dispose();
                    return File(stream, "application/ms-excel", string.Concat(fileName, ".xls"));
            }
        }
    }
}