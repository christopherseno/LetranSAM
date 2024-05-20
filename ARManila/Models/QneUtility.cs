using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ARManila.Models.QneDb;
namespace ARManila.Models
{
    public static class QneUtility
    {
        public static string GetNextDocNo(string arscode)
        {
            QNEDBEntities db = new QNEDBEntities();
            var suggesteddocno = arscode+"-0000001";
            var lastarrecord = db.Journals.Where(m => m.JournalCode.Contains(arscode)).OrderByDescending(m => m.JournalCode).FirstOrDefault();
            if(lastarrecord != null)
            {
                var lastnumber = Convert.ToInt32(lastarrecord.JournalCode.Right(5)) + 1;
                suggesteddocno = arscode + "-" + lastnumber.ToString("0000000");
            }
            return suggesteddocno;
        }

        public static string Right(this string sValue, int iMaxLength)
        {            
            if (string.IsNullOrEmpty(sValue))
            {                
                sValue = string.Empty;
            }
            else if (sValue.Length > iMaxLength)
            {                
                sValue = sValue.Substring(sValue.Length - iMaxLength, iMaxLength);
            }
            return sValue;
        }
    }
}