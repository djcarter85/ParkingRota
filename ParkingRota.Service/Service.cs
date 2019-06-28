namespace ParkingRota.Service
{
    using System;
    using System.ServiceProcess;
    using System.Threading;
    using System.Timers;
    using AutoMapper;
    using Business;
    using Business.Model;
    using Business.ScheduledTasks;
    using Data;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NodaTime;
    using Timer = System.Timers.Timer;

    public class Service : ServiceBase
    {
        private ServiceProvider serviceProvider;

        private Timer timer;

        protected override void OnStart(string[] args)
        {
            this.serviceProvider = BuildServiceProvider();

            this.timer = new Timer(Duration.FromSeconds(20).TotalMilliseconds);
            this.timer.Elapsed += this.Timer_Elapsed;
            this.timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (Monitor.TryEnter(this))
                {
                    try
                    {
                        this.RunTasks();
                    }
                    finally
                    {
                        Monitor.Exit(this);
                    }
                }
            }
            catch (Exception exception)
            {
                // Exceptions get swallowed by the caller of this, so we need to make sure we don't ignore them.
                ThreadPool.QueueUserWorkItem(
                    callback => throw new InvalidOperationException("Timer process exception", exception));
            }
        }

        protected override void OnStop()
        {
            this.timer.Stop();

            lock (this)
            {
                // Ensure any current run has finished
            }

            base.OnStop();
            this.Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            this.serviceProvider?.Dispose();
            this.timer?.Dispose();
        }

        public async void RunTasks()
        {
            using (var scope = this.serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var allocationCreator = scope.ServiceProvider.GetRequiredService<AllocationCreator>();
                var newAllocations = allocationCreator.Create();

                var allocationNotifier = scope.ServiceProvider.GetRequiredService<AllocationNotifier>();
                allocationNotifier.Notify(newAllocations);

                var scheduledTaskRunner = scope.ServiceProvider.GetRequiredService<ScheduledTaskRunner>();
                await scheduledTaskRunner.Run();

                var emailProcessor = scope.ServiceProvider.GetRequiredService<EmailProcessor>();
                await emailProcessor.SendPending();
            }
        }

        private static ServiceProvider BuildServiceProvider()
        {
            Console.WriteLine("Building service provider");

            var services = new ServiceCollection();

            services.AddLogging(configure => configure.AddConsole());

            var connectionString =
                Environment.GetEnvironmentVariable("ParkingRotaConnectionString") ??
                GetConfiguration().GetConnectionString("DefaultConnection");

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddSingleton<IClock>(SystemClock.Instance);

            services.AddScoped<AllocationCreator>();
            services.AddScoped<AllocationNotifier>();
            services.AddScoped<IAllocationRepository, AllocationRepository>();
            services.AddScoped<IApplicationDbContext, ApplicationDbContext>();
            services.AddHttpClient<IBankHolidayFetcher, BankHolidayFetcher>();
            services.AddScoped<IBankHolidayRepository, BankHolidayRepository>();
            services.AddScoped<IDateCalculator, DateCalculator>();
            services.AddScoped<EmailProcessor>();
            services.AddScoped<IEmailRepository, EmailRepository>();
            services.AddScoped<IEmailSender, SendGridEmailSender>();
            services.AddScoped<IEmailSender, AwsSesEmailSender>();
            services.AddHttpClient<IPasswordBreachChecker, PasswordBreachChecker>();
            services.AddScoped<IRegistrationTokenRepository, RegistrationTokenRepository>();
            services.AddScoped<IRegistrationTokenValidator, RegistrationTokenValidator>();
            services.AddScoped<IRequestRepository, RequestRepository>();
            services.AddScoped<IRequestSorter, RequestSorter>();
            services.AddScoped<IReservationRepository, ReservationRepository>();
            services.AddScoped<ISingleDayAllocationCreator, SingleDayAllocationCreator>();
            services.AddScoped<IScheduledTaskRepository, ScheduledTaskRepository>();
            services.AddScoped<ScheduledTaskRunner>();
            services.AddScoped<ISystemParameterListRepository, SystemParameterListRepository>();

            services.AddScoped<IScheduledTask, BankHolidayUpdater>();
            services.AddScoped<IScheduledTask, DailySummary>();
            services.AddScoped<IScheduledTask, RequestReminder>();
            services.AddScoped<IScheduledTask, ReservationReminder>();
            services.AddScoped<IScheduledTask, WeeklySummary>();

            var mapperConfiguration = new MapperConfiguration(
                c =>
                {
                    c.CreateMap<Data.Allocation, Business.Model.Allocation>();
                    c.CreateMap<Data.BankHoliday, Business.Model.BankHoliday>();
                    c.CreateMap<Data.EmailQueueItem, Business.Model.EmailQueueItem>();
                    c.CreateMap<Data.RegistrationToken, Business.Model.RegistrationToken>();
                    c.CreateMap<Data.Request, Business.Model.Request>();
                    c.CreateMap<Data.Reservation, Business.Model.Reservation>();
                    c.CreateMap<Data.ScheduledTask, Business.Model.ScheduledTask>();
                    c.CreateMap<Data.SystemParameterList, Business.Model.SystemParameterList>();
                });

            services.AddSingleton<IMapper>(new Mapper(mapperConfiguration));

            return services.BuildServiceProvider();
        }

        private static IConfiguration GetConfiguration() =>
            new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("AppSettings.json").Build();
    }
}