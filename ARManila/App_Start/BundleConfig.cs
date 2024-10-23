using System.Web;
using System.Web.Optimization;

namespace ARManila
{
    public class BundleConfig
    {
        // For more information on bundling, visit https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            //Script
            bundles.Add(new Bundle("~/Codebase/mainjs").Include(
                 "~/assets/js/lib/jquery.min.js",
                 "~/assets/js/codebase.app.min.js",
                "~/assets/js/plugins/select2/js/select2.full.min.js",
                "~/assets/js/plugins/sweetalert2/sweetalert2.min.js",
                "~/assets/js/plugins/datatables/jquery.dataTables.min.js",
                "~/assets/js/plugins/datatables-bs5/js/dataTables.bootstrap5.min.js",
                "~/assets/js/plugins/datatables-responsive/js/dataTables.responsive.min.js",
                "~/assets/js/plugins/datatables-responsive-bs5/js/responsive.bootstrap5.min.js",
                "~/assets/js/plugins/datatables-buttons/dataTables.buttons.min.js",
                "~/assets/js/plugins/datatables-buttons-bs5/js/buttons.bootstrap5.min.js",
                "~/assets/js/plugins/datatables-buttons-jszip/jszip.min.js",
                "~/assets/js/plugins/datatables-buttons-pdfmake/pdfmake.min.js",
                "~/assets/js/plugins/datatables-buttons-pdfmake/vfs_fonts.js",
                "~/assets/js/plugins/datatables-buttons/buttons.print.min.js",
                "~/assets/js/plugins/datatables-buttons/buttons.html5.min.js",
                "~/assets/js/pages/be_tables_datatables.min.js",
                "~/assets/js/plugins/chart.js/chart.min.js",
                "~/assets/js/plugins/bootstrap-datepicker/js/bootstrap-datepicker.min.js"
                ));
            bundles.Add(new Bundle("~/Codebase/js").Include(
                    "~/assets/js/codebase.app.min.js",
                    "~/assets/js/lib/jquery.min.js",
                    "~/assets/js/plugins/jquery-validation/jquery.validate.min.js",
                    "~/assets/js/pages/op_auth_signin.min.js",
                    "~/assets/js/plugins/chart.js/chart.min.js"
            ));
            //Link
            bundles.Add(new StyleBundle("~/Codebase/maincss").Include(
                "~/Content/Site.css",
               "~/assets/js/plugins/select2/css/select2.min.css",
               "~/assets/js/plugins/sweetalert2/sweetalert2.min.css",
               "~/assets/js/plugins/datatables-bs5/css/dataTables.bootstrap5.min.css",
               "~/assets/js/plugins/datatables-buttons-bs5/css/buttons.bootstrap5.min.css",
               "~/assets/js/plugins/datatables-responsive-bs5/css/responsive.bootstrap5.min.css",
               "~/assets/js/plugins/bootstrap-datepicker/css/bootstrap-datepicker3.min.css"
               ));
            bundles.Add(new StyleBundle("~/Codebase/css").Include(
                "~/assets/css/codebase.css"
                ));

        }
    }
}
