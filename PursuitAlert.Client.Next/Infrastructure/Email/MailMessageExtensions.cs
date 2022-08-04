using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.Email
{
    public static class MailMessageExtensions
    {
        #region Methods

        //Extension method for MailMessage to save to a file on disk
        // https://stackoverflow.com/a/25586282
        public static void Save(this MailMessage message, string directory, bool addUnsentHeader = true)
        {
            var client = new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = directory
            };
            client.Send(message);

            // Dispose the attachment streams
            message.Attachments.ToList().ForEach(a => a.ContentStream.Dispose());
        }

        #endregion Methods
    }
}