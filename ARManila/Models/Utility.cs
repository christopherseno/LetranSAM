using DatabaseEncryption;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace ARManila.Models
{
    public static class DecimalExtensions
    {
        public static string ToCurrencyFormat(this decimal value)
        {
            // Check if the value is negative
            bool isNegative = value < 0;

            // Convert the absolute value to string with two decimal points and comma separator
            string formattedValue = Math.Abs(value).ToString("N2");

            // Add parenthesis for negative numbers
            if (isNegative)
            {
                formattedValue = "(" + formattedValue + ")";
            }

            return formattedValue;
        }
        public static string ToPercentFormat(this decimal value)
        {           
            return Math.Round(value * 100, 0).ToString() + "%";
        }
    }
    public class Utility
    {
        public static string Decrypt(string passphrase, int type)
        {
           
            string data = "";
            var connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;            
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            SqlCommand command = new SqlCommand("SELECT dbo.DecryptStudent(@passphrase, @plaintext, @type)", conn);
            command.CommandType = CommandType.Text;
            command.Parameters.Add(new SqlParameter("@passphrase", "1T3@mWoRk0"));
            command.Parameters.Add(new SqlParameter("@plaintext", passphrase));
            command.Parameters.Add(new SqlParameter("@type", type));
            data = command.ExecuteScalar().ToString();
            conn.Close();
            return data;
        }
        public static string Decrypt256(string encryptedstring)
        {
            string decryptedstring = "";
            var connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            string[] connectionStringParts = connectionString.Split(';');
            string dataSource = "";
            foreach (string part in connectionStringParts)
            {
                if (part.StartsWith("Data Source="))
                {
                    dataSource = part.Split('=')[1].Trim();
                    break;
                }
            }
            decryptedstring = Encryption.DecryptStringFromBytes_Aes(encryptedstring, "13061025", "-951Han5", dataSource);           
            return decryptedstring;
        }
        public static string DecryptEmployee(string passphrase, int type)
        {
            string data = "";
            var connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            SqlCommand command = new SqlCommand("SELECT dbo.DecryptEmployee(@passphrase, @plaintext, @type)", conn);
            command.CommandType = CommandType.Text;
            command.Parameters.Add(new SqlParameter("@passphrase", "1T3@mWoRk0"));
            command.Parameters.Add(new SqlParameter("@plaintext", passphrase));
            command.Parameters.Add(new SqlParameter("@type", type));
            data = command.ExecuteScalar().ToString();
            conn.Close();
            return data;
        }
        public static async System.Threading.Tasks.Task<bool> SendSMS(string mobileno, string message, string logdata, int lognumber)
        {
            try
            {
                using (LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities())
                {
                    if (mobileno.Length == 11)
                    {
                        HttpClient client = new HttpClient();
                        client.BaseAddress = new Uri("https://devapi.globelabs.com.ph/smsmessaging/v1/outbound/21589099/requests");
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        try
                        {
                            var student = new outboundSMSMessageRequest
                            {
                                message = message,
                                address = mobileno.TrimStart('0'),
                                passphrase = "A9undLLc2Q",
                                app_id = "6859HrGyoGhRdcAEy4TyMXhR98dRHyaG",
                                app_secret = "6347762b8166232702b40faa4b99f9222928f163c7cd9987b957cc88d9feffe9"
                            };
                            var response = await client.PostAsJsonAsync("", student);
                            SMSLog log = new SMSLog();
                            log.ApplicationNo = lognumber;
                            log.DateSent = DateTime.Now;
                            log.Message = message;
                            log.MobileNo = mobileno;
                            log.TypeofSystem = logdata;
                            db.SMSLog.Add(log);
                            db.SaveChanges();
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }

        }
        public static Task SendMail(string body, string from, List<string> to, string subject)
        {
            return Task.Run(() => {
                MailMessage email = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
                email.From = new MailAddress("itms@letranbataan.edu.ph", "ITSD");
                foreach (var i in to)
                {
                    email.To.Add(i);
                }
                email.Subject = subject;
                email.IsBodyHtml = true;
                email.Body = body;
                email.Bcc.Add("admin@letranbataan.edu.ph");
                SmtpServer.Port = 587;
                SmtpServer.UseDefaultCredentials = false;
                SmtpServer.Credentials = new System.Net.NetworkCredential("itms@letranbataan.edu.ph", "1T3@mWoRk0");
                //SmtpServer.Credentials = new System.Net.NetworkCredential("christopher.seno@letran.edu.ph", "-951Han5");
                SmtpServer.EnableSsl = true;
                SmtpServer.SendMailAsync(email);
            });
        }
        public static string DecryptStringFromBytes_Aes(string Text)
        {
            SHA256 sha2 = new SHA256CryptoServiceProvider();
            byte[] Key = sha2.ComputeHash(Encoding.ASCII.GetBytes("1T3@mWoRk053ñ0"));

            byte[] IV = sha2.ComputeHash(Encoding.ASCII.GetBytes("N3tWoRk!nG23"));
            Array.Resize(ref IV, 16);
            if (Text == null || Text.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");

            string plaintext = null;
            byte[] cipherText = Convert.FromBase64String(Text.Replace(' ', '+'));

            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;


                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }

            }
            return plaintext;
        }
    }
    public class outboundSMSMessageRequest
    {
        public string message { get; set; }
        public string address { get; set; }
        public string passphrase { get; set; }
        public string app_id { get; set; }
        public string app_secret { get; set; }

    }
    public class MyEmail
    {
        public string body { get; set; }
        public string from { get; set; }
        public List<string> to { get; set; }
        public string subject { get; set; }
        public string image { get; set; }
    }
}