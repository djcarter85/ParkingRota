﻿using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ParkingRota.Pages
{
    using System.Collections.Generic;
    using System.Linq;
    using Business;
    using Business.Model;
    using Microsoft.AspNetCore.Identity;
    using NodaTime;
    using ParkingRota.Calendar;

    public class EditReservationsModel : PageModel
    {
        private readonly IDateCalculator dateCalculator;
        private readonly ISystemParameterListRepository systemParameterListRepository;
        private readonly IReservationRepository reservationRepository;
        private readonly UserManager<ApplicationUser> userManager;

        public EditReservationsModel(
            IDateCalculator dateCalculator,
            ISystemParameterListRepository systemParameterListRepository,
            IReservationRepository reservationRepository,
            UserManager<ApplicationUser> userManager)
        {
            this.dateCalculator = dateCalculator;
            this.systemParameterListRepository = systemParameterListRepository;
            this.reservationRepository = reservationRepository;
            this.userManager = userManager;
        }

        public void OnGet()
        {
            var activeDates = this.dateCalculator.GetActiveDates();
            var systemParameterList = this.systemParameterListRepository.GetSystemParameterList();
            var reservations = this.reservationRepository.GetReservations(activeDates.First(), activeDates.Last());
            var users = this.userManager.Users.OrderBy(u => u.LastName).ToArray();

            var calendarData = new Dictionary<LocalDate, DayReservations>();

            foreach (var activeDate in activeDates)
            {
                var spaceReservations = new List<SpaceReservation>();

                for (var order = 0; order < systemParameterList.ReservableSpaces; order++)
                {
                    var spaceReservationOptions = new List<SpaceReservationOption>();

                    var selectedUserId = reservations
                        .SingleOrDefault(r => r.Date == activeDate && r.Order == order)
                        ?.ApplicationUser.Id;

                    spaceReservationOptions.Add(
                        new SpaceReservationOption(null, activeDate, order, false));
                    spaceReservationOptions.AddRange(
                        users.Select(u => new SpaceReservationOption(u, activeDate, order, u.Id == selectedUserId)));

                    spaceReservations.Add(new SpaceReservation(spaceReservationOptions));
                }

                calendarData.Add(activeDate, new DayReservations(spaceReservations));
            }

            this.Calendar = Calendar<DayReservations>.Create(calendarData);
        }

        public Calendar<DayReservations> Calendar { get; private set; }

        public class DayReservations
        {
            public IReadOnlyList<SpaceReservation> SpaceReservations { get; }

            public DayReservations(IReadOnlyList<SpaceReservation> spaceReservations) =>
                this.SpaceReservations = spaceReservations;
        }

        public class SpaceReservation
        {
            public IReadOnlyList<SpaceReservationOption> Options { get; }

            public SpaceReservation(IReadOnlyList<SpaceReservationOption> spaceReservationOptions) =>
                this.Options = spaceReservationOptions;
        }


        public class SpaceReservationOption
        {
            private readonly ApplicationUser user;
            private readonly LocalDate date;
            private readonly int order;

            public SpaceReservationOption(ApplicationUser user, LocalDate date, int order, bool isSelected)
            {
                this.user = user;
                this.date = date;
                this.order = order;

                this.IsSelected = isSelected;
            }

            public string Key => $"{this.date.ForRoundTrip()}|{this.order}|{this.user?.Id}";

            public string DisplayValue => this.user?.FullName ?? $"Space {this.order + 1}";

            public bool IsSelected { get; }
        }

    }
}