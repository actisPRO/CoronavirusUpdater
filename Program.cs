using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using Microsoft.VisualBasic.FileIO;

namespace CoronavirusUpdater
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            if (args[0] == null)
            {
                Console.WriteLine("Country is not specified.");
                Environment.Exit(1);
            }

            var country = args[0];

            //we'd better update at the beginning of a new day
            var today = DateTime.Today.AddDays(-1);
            var yesterday = DateTime.Today.AddDays(-2);

            //our data source uses format 00-00-0000
            var formattedTodayDate = $"{FormatNumber(today.Month)}-{FormatNumber(today.Day)}-{today.Year}";
            var formattedYesterdayDate =
                $"{FormatNumber(yesterday.Month)}-{FormatNumber(yesterday.Day)}-{yesterday.Year}";

            //data downloading
            using (var client = new WebClient())
            {
                client.DownloadFile(
                    $"https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/csse_covid_19_daily_reports/{formattedTodayDate}.csv",
                    "today.csv");
                client.DownloadFile(
                    $"https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/csse_covid_19_daily_reports/{formattedYesterdayDate}.csv",
                    "yesterday.csv");
            }

            int[] todayData = new int[4],
                yesterdayData = new int[4]; //0 - confirmed, 1 - deaths, 2 - recovered, 3 - active
            using (var parser = new TextFieldParser("today.csv"))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    var row = parser.ReadFields();
                    if (row[3] == country)
                        for (var i = 0; i < 4; ++i)
                            todayData[i] = Convert.ToInt32(row[i + 7]); //required fields are 7, 8, 9, 10
                }
            }

            using (var parser = new TextFieldParser("yesterday.csv"))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    var row = parser.ReadFields();
                    if (row[3] == country)
                        for (var i = 0; i < 4; ++i)
                            yesterdayData[i] = Convert.ToInt32(row[i + 7]);
                }
            }

            var changes = new string[4]; //we use pretty-looking format for output
            for (var i = 0; i < 4; ++i)
            {
                var change = todayData[i] - yesterdayData[i];
                if (change != 0) changes[i] = $" (+{change})";
                else if (change == 0) changes[i] = "";
            }

            //don't need this
            File.Delete("today.csv");
            File.Delete("yesterday.csv");

            var output =
                $"Confirmed: {todayData[0]}{changes[0]}\n" +
                $"Deaths: {todayData[1]}{changes[1]}\n" +
                $"Recovered: {todayData[2]}{changes[2]}\n" +
                $"Active: {todayData[3]}{changes[3]}\n";

            //parse settings for login data
            string login = null, password = null, host = null;
            int port = 0;
            bool ssl = false;
            
            using (var sr = new StreamReader("settings.txt"))
            {
                login = sr.ReadLine();
                password = sr.ReadLine();
                host = sr.ReadLine();
                port = Convert.ToInt32(sr.ReadLine());
                ssl = Convert.ToBoolean(sr.ReadLine());
            }
            
            //creation of the message
            MailMessage message = new MailMessage();
            SmtpClient smtp = new SmtpClient();
            message.From = new MailAddress(login); //TODO settings
            message.Subject = "Your daily coronavirus data for " + country;
            message.Body = output;

            //string emails = "";
            using (var sr = new StreamReader($"{country}-emails.txt"))
            {
                string email = sr.ReadLine();
                while (email != null)
                {
                    message.To.Add(email);
                    email = sr.ReadLine();
                }
            }
            
            //obviously, sending message
            smtp.Port = port;
            smtp.Host = host;
            smtp.EnableSsl = true;
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(login, password);
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtp.Send(message);
        }

        private static string FormatNumber(int number) //i have no idea if there are other, less weird ways of formatting date
        {
            if (number >= 10) return number.ToString();
            return "0" + number;
        }
    }
}