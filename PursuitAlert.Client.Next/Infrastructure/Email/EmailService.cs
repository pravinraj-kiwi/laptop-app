using PursuitAlert.Client.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.Email
{
    public class EmailService : IEmailService
    {
        #region Properties

        private string Version
        {
            get => $"v{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}";
        }

        #endregion Properties

        #region Methods

        public void SendEmailTodaysLogs()
        {
            // https://stackoverflow.com/a/25586282
            var message = new MailMessage
            {
                From = new MailAddress("logs@pursuitalert.com"),
                Subject = "Pursuit Alert Desktop App Logs",
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(Settings.Default.LogRecipient));

            var logPathsFromToday = _getLogFilePathsFromToday();

            // We have to read the file in manually to a new stream. If we don't read it into a new
            // stream, the new Attachment constructor will throw an error that the file is in use,
            // so we have to read the file manually in a less invasive way and the pass the stream
            // to the new Attachment constructor.

            foreach (var attachmentFilePath in logPathsFromToday)
            {
                // Each memory stream is disposed in the message.Save extension method
                var memoryStream = new MemoryStream();

                // https://stackoverflow.com/a/64462894
                using (var fileStream = new FileStream(attachmentFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                var fileName = Path.GetFileName(attachmentFilePath);
                message.Attachments.Add(new Attachment(memoryStream, fileName));
            }

            var bodyContent = new StringBuilder();
            bodyContent.Append("<p>Please provide a description of the issue here.</p>");
            bodyContent.Append($"<p>{new string('-', 48)} Do not write below this line {new string('-', 48)}</p>");
            bodyContent.Append("<br>");
            bodyContent.Append($"<p><strong>Device Serial Number:</strong> {Settings.Default.DeviceSerialNumber}</p>");
            bodyContent.Append($"<p><strong>Application Version:</strong> {Version}</p>");
            message.Body = bodyContent.ToString();

            message.Save(Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath));

            var emailPath = _getEmailPath();

            // Remove the from and sender information from the eml file so Outlook opens with the
            // option to send from the default from address
            var emailFileContent = File.ReadAllText(emailPath);
            emailFileContent = Regex.Replace(emailFileContent, @"X-Sender: .+\n", "X-Sender: \n");
            emailFileContent = Regex.Replace(emailFileContent, @"From: .+\n", "From: \n");

            // Add the X-Unsent header so it's opened as a new message https://stackoverflow.com/a/25586282
            emailFileContent = $"X-Unsent: 1\n{emailFileContent}";
            File.WriteAllText(emailPath, emailFileContent);

            Process.Start(emailPath);
        }

        private string _getEmailPath()
        {
            var directory = new DirectoryInfo(Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath));
            return directory.GetFiles("*.eml")
                .OrderByDescending(f => f.LastWriteTime)
                .First()
                .FullName;
        }

        private IEnumerable<string> _getLogFilePathsFromToday()
        {
            var directory = new DirectoryInfo(Environment.ExpandEnvironmentVariables(Settings.Default.LogDirectoryPath));

            return directory.GetFiles("*.log")
                .Where(f => f.LastWriteTime.Date == DateTime.Now.Date)
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => f.FullName);
        }

        #endregion Methods
    }
}