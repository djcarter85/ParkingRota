﻿namespace ParkingRota.Business.Emails
{
    using NodaTime;

    public class ReservationsReminder : IEmail
    {
        private readonly LocalDate localDate;

        public ReservationsReminder(string to, LocalDate localDate)
        {
            this.localDate = localDate;
            this.To = to;
        }

        public string To { get; }

        public string Subject => $"No reservations entered for {this.localDate.ForDisplay()}";

        public string HtmlBody => $"<p>{this.PlainTextBody}</p>";

        public string PlainTextBody =>
            $"No reservations have yet been entered for {this.localDate.ForDisplay()}." +
            " If no spaces need reserving for this date then you can ignore this message." +
            " Otherwise, you should enter reservations by 11am to ensure spaces are allocated accordingly.";
    }
}