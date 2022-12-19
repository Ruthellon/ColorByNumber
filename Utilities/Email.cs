using MailKit.Net.Smtp;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ColorByNumber.Utilities
{
    public class Email
    {
        public string Subject { get; set; }
        public string Body { get; set; }
        public MimePart Attachment { get; set; }
        // email displayName, address
        public KeyValuePair<string, string> Sender { get; set; } = new KeyValuePair<string, string>();
        public KeyValuePair<string, string> ReplyTo { get; set; } = new KeyValuePair<string, string>();
        public List<KeyValuePair<string, string>> To { get; set; } = new List<KeyValuePair<string, string>>();
        // email displayName, address
        public List<KeyValuePair<string, string>> Cc { get; set; } = new List<KeyValuePair<string, string>>();
        // email displayName, address
        public List<KeyValuePair<string, string>> Bcc { get; set; } = new List<KeyValuePair<string, string>>();


        public async Task<int> Send()
        {
            if ((Sender.Key == "") ||
                ((To.Count == 0) &&
                 (Cc.Count == 0) &&
                 (Bcc.Count == 0)) ||
                (Subject == "") ||
                (Body == ""))
            {
                return -1;
            }

            MimeMessage message = new MimeMessage();

            message.From.Add(new MailboxAddress(Sender.Key, Sender.Value));
            if (ReplyTo.Key != null && ReplyTo.Key != "")
            {
                message.ReplyTo.Add(new MailboxAddress(ReplyTo.Key, ReplyTo.Value));
            }
            foreach (KeyValuePair<string, string> to in To)
            {
                message.To.Add(new MailboxAddress(to.Key, to.Value));
            }
            foreach (KeyValuePair<string, string> cc in Cc)
            {
                message.Cc.Add(new MailboxAddress(cc.Key, cc.Value));
            }
            foreach (KeyValuePair<string, string> bcc in Bcc)
            {
                message.Bcc.Add(new MailboxAddress(bcc.Key, bcc.Value));
            }
            message.Subject = Subject;
            message.Body = new Multipart("mixed")
            {
                new TextPart("html")
                {
                    Text = Body
                },
                Attachment
            };
            
            using (var smtp = new SmtpClient())
            {
                try
                {
                    await smtp.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                    await smtp.AuthenticateAsync("angryelfstudios@gmail.com", "7bM!19dSkFQ%");
                    await smtp.SendAsync(message);
                }
                catch (Exception e)
                {
                }
            }

            return 0;
        }
    }
}
