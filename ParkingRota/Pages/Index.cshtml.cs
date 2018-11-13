﻿namespace ParkingRota.Pages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Business;
    using Business.Model;
    using Calendar;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using NodaTime;

    public class IndexModel : PageModel
    {
        private readonly IDateCalculator dateCalculator;
        private readonly IRequestRepository requestRepository;
        private readonly UserManager<ApplicationUser> userManager;

        public IndexModel(
            IDateCalculator dateCalculator,
            IRequestRepository requestRepository,
            UserManager<ApplicationUser> userManager)
        {
            this.dateCalculator = dateCalculator;
            this.requestRepository = requestRepository;
            this.userManager = userManager;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var activeDates = this.dateCalculator.GetActiveDates();

            var requests = this.requestRepository.GetRequests(activeDates.First(), activeDates.Last());

            var currentUser = await this.userManager.GetUserAsync(this.User);

            var calendarData = new Dictionary<LocalDate, IReadOnlyList<DisplayRequest>>();

            foreach (var activeDate in activeDates)
            {
                var displayRequests = requests
                    .Where(r => r.Date == activeDate)
                    .OrderBy(r => r.ApplicationUser.LastName)
                    .Select(r => new DisplayRequest(r.ApplicationUser.FullName, r.ApplicationUser.Id == currentUser.Id))
                    .ToArray();

                calendarData.Add(activeDate, displayRequests);
            }

            this.Calendar = Calendar<IReadOnlyList<DisplayRequest>>.Create(calendarData);

            return this.Page();
        }

        public Calendar<IReadOnlyList<DisplayRequest>> Calendar { get; private set; }

        public class DisplayRequest
        {
            public DisplayRequest(string fullName, bool isCurrentUser)
            {
                this.FullName = fullName;
                this.IsCurrentUser = isCurrentUser;
            }

            public string FullName { get; }

            public bool IsCurrentUser { get; }
        }
    }
}
