﻿namespace ParkingRota.UnitTests.Data
{
    using System.Linq;
    using AutoMapper;
    using NodaTime.Testing.Extensions;
    using ParkingRota.Business.EmailTemplates;
    using ParkingRota.Data;
    using Xunit;
    using DataQueueItem = ParkingRota.Data.EmailQueueItem;
    using ModelQueueItem = ParkingRota.Business.Model.EmailQueueItem;

    public class EmailRepositoryTests : DatabaseTests
    {
        private static readonly DataQueueItem UnsentEmail = new DataQueueItem
        {
            To = "a@b.c",
            Subject = "Unsent email subject",
            HtmlBody = "<p>Unsent email body</p>",
            PlainTextBody = "Unsent email body",
            AddedTime = 1.January(2019).At(10, 30, 56).Utc()
        };

        private static readonly DataQueueItem EarlierUnsentEmail = new DataQueueItem
        {
            To = "x@y.z",
            Subject = "Earlier unsent email subject",
            HtmlBody = "<p>Earlier unsent email body</p>",
            PlainTextBody = "Earlier unsent email body",
            AddedTime = 1.January(2019).At(10, 29, 02).Utc()
        };

        private static readonly DataQueueItem SentEmail = new DataQueueItem
        {
            To = "d@e.f",
            Subject = "Sent email subject",
            HtmlBody = "<p>Sent email body</p>",
            PlainTextBody = "Sent email body",
            AddedTime = 1.January(2019).At(10, 27, 50).Utc(),
            SentTime = 1.January(2019).At(10, 28, 03).Utc()
        };

        private static readonly DataQueueItem EarlierSentEmail = new DataQueueItem
        {
            To = "x@y.z",
            Subject = "Earlier sent email subject",
            HtmlBody = "<p>Earlier sent email body</p>",
            PlainTextBody = "Earlier sent email body",
            AddedTime = 1.January(2019).At(9, 59, 50).Utc(),
            SentTime = 1.January(2019).At(10, 00, 03).Utc()
        };

        [Fact]
        public void Test_AddToQueue()
        {
            var instant = 18.June(2019).At(23, 0, 0).Utc();

            var email = new RequestReminder("a@b.com", 1.July(2019), 5.July(2019));

            // Act
            using (var context = this.CreateContext())
            {
                new EmailRepositoryBuilder()
                    .WithCurrentInstant(instant)
                    .Build(context)
                    .AddToQueue(email);
            }

            using (var context = this.CreateContext())
            {
                var emails = context.EmailQueueItems.ToArray();

                Assert.Single(emails);

                var actual = emails[0];

                Assert.Equal(email.To, actual.To);
                Assert.Equal(email.Subject, actual.Subject);
                Assert.Equal(email.PlainTextBody, actual.PlainTextBody);
                Assert.Equal(email.HtmlBody, actual.HtmlBody);
                Assert.Equal(instant, actual.AddedTime);
            }
        }

        [Fact]
        public void Test_GetRecent()
        {
            // Arrange
            this.SeedDatabase(UnsentEmail, SentEmail, EarlierSentEmail);

            var instant = 1.January(2019).At(10, 30, 00).Utc();

            using (var context = this.CreateContext())
            {
                // Act
                var result = new EmailRepositoryBuilder()
                    .WithCurrentInstant(instant)
                    .Build(context)
                    .GetRecent();

                // Assert
                var recentEmails = new[] { UnsentEmail, SentEmail };

                Assert.Equal(recentEmails.Length, result.Count);

                Assert.True(result.First().AddedTime < result.Last().AddedTime);

                foreach (var expected in recentEmails)
                {
                    Assert.Single(
                        result,
                        actual =>
                            actual.To == expected.To &&
                            actual.Subject == expected.Subject &&
                            actual.HtmlBody == expected.HtmlBody &&
                            actual.PlainTextBody == expected.PlainTextBody &&
                            actual.AddedTime == expected.AddedTime &&
                            actual.SentTime == expected.SentTime);
                }
            }
        }

        [Fact]
        public void Test_GetUnsent()
        {
            // Arrange
            this.SeedDatabase(UnsentEmail, EarlierUnsentEmail, SentEmail);

            using (var context = this.CreateContext())
            {
                // Act
                var result = new EmailRepositoryBuilder()
                    .Build(context)
                    .GetUnsent();

                // Assert
                var unsentEmails = new[] { UnsentEmail, EarlierUnsentEmail };

                Assert.Equal(unsentEmails.Length, result.Count);

                Assert.True(result.First().AddedTime < result.Last().AddedTime);

                foreach (var expected in unsentEmails)
                {
                    Assert.Single(
                        result,
                        actual =>
                            actual.To == expected.To &&
                            actual.Subject == expected.Subject &&
                            actual.HtmlBody == expected.HtmlBody &&
                            actual.PlainTextBody == expected.PlainTextBody &&
                            actual.AddedTime == expected.AddedTime &&
                            actual.SentTime == null);
                }
            }
        }

        [Fact]
        public void Test_MarkAsSent()
        {
            const string Subject = "Earlier unsent email subject";

            // Arrange
            this.SeedDatabase(UnsentEmail, EarlierUnsentEmail, SentEmail);

            IMapper mapper = MapperBuilder.Build();

            var instant = 1.January(2019).At(11, 07, 23).Utc();

            // Act
            using (var context = this.CreateContext())
            {
                var dataUnsentEmail = context.EmailQueueItems.Single(e => e.Subject == Subject);
                var modelUnsentEmail = mapper.Map<ModelQueueItem>(dataUnsentEmail);

                new EmailRepositoryBuilder()
                    .WithCurrentInstant(instant)
                    .Build(context)
                    .MarkAsSent(modelUnsentEmail);
            }

            // Assert
            using (var context = this.CreateContext())
            {
                Assert.Single(context.EmailQueueItems.Where(e => e.DbSentTime == null));

                var previouslyUnsentEmail = context.EmailQueueItems.Single(e => e.Subject == Subject);

                Assert.Equal(instant, previouslyUnsentEmail.SentTime);
            }
        }

        private void SeedDatabase(params DataQueueItem[] emailQueueItems)
        {
            using (var context = this.CreateContext())
            {
                context.EmailQueueItems.AddRange(emailQueueItems);
                context.SaveChanges();
            }
        }
    }
}