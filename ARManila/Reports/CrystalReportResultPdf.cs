using CrystalDecisions.CrystalReports.Engine; // Crystal Report Class
using CrystalDecisions.Shared; // Crystal Report ExportFormatType
using System;
using System.Collections.Generic;
using System.IO; // Path & Stream IO Class 
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ARManila.Reports
{
    public class CrystalReportPdfResult : ActionResult
    {
        private readonly byte[] _contentBytes;
        private string title;

        //public CrystalReportPdfResult(string reportPath, string reportitle, object dataSet)
        //{
        //    ReportDocument reportDocument = new ReportDocument();

        //    reportDocument.Load(reportPath);
        //    reportDocument.SetDataSource(dataSet);
        //    reportDocument.SummaryInfo.ReportTitle = "Report";
        //    reportDocument.SetDatabaseLogon("mark", "letran1620");
        //    reportDocument.SummaryInfo.ReportTitle = reportitle;
        //    _contentBytes = StreamToBytes(reportDocument.ExportToStream(ExportFormatType.PortableDocFormat));

        //    reportDocument.Close();
        //    reportDocument.Dispose();
        //    GC.Collect();

        //    title = reportitle;

        //}

        public CrystalReportPdfResult(string reportTitle, ReportDocument reportDocument)
        {
            reportDocument.SummaryInfo.ReportTitle = reportTitle;
            _contentBytes = StreamToBytes(reportDocument.ExportToStream(ExportFormatType.PortableDocFormat));

            reportDocument.Close();
            reportDocument.Dispose();
            GC.Collect();
            this.title = reportTitle;

        }


        public override void ExecuteResult(ControllerContext context)
        {

            var response = context.HttpContext.ApplicationInstance.Response;
            response.Clear();
            response.Buffer = false;
            response.ClearContent();
            response.ClearHeaders();
            response.Cache.SetCacheability(HttpCacheability.Public);
            response.ContentType = "application/pdf";
            using (var stream = new MemoryStream(_contentBytes))
            {
                stream.WriteTo(response.OutputStream);
                stream.Flush();
            }
        }

        private static byte[] StreamToBytes(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}