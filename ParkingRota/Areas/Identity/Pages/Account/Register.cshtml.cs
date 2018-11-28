﻿namespace ParkingRota.Areas.Identity.Pages.Account
{
    using System.ComponentModel.DataAnnotations;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;
    using Business;
    using Business.Model;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.Extensions.Logging;

    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IRegistrationTokenValidator registrationTokenValidator;
        private readonly IPasswordBreachChecker passwordBreachChecker;
        private readonly ILogger<RegisterModel> logger;
        private readonly IEmailSender emailSender;

        public RegisterModel(
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager,
            IRegistrationTokenValidator registrationTokenValidator,
            IPasswordBreachChecker passwordBreachChecker,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.userManager = userManager;
            this.registrationTokenValidator = registrationTokenValidator;
            this.passwordBreachChecker = passwordBreachChecker;
            this.signInManager = signInManager;
            this.logger = logger;
            this.emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(50)]
            [Display(Name = "First name")]
            public string FirstName { get; set; }

            [Required]
            [StringLength(50)]
            [Display(Name = "Last name")]
            public string LastName { get; set; }

            [Required]
            [StringLength(10)]
            [Display(Name = "Car registration number")]
            public string CarRegistrationNumber { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            [Display(Name = "Registration token")]
            public string RegistrationToken { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (this.ModelState.IsValid)
            {
                var registrationTokenIsValid =
                    this.registrationTokenValidator.TokenIsValid(this.Input.RegistrationToken);

                if (!registrationTokenIsValid)
                {
                    this.ModelState.AddModelError(
                        $"{nameof(this.Input)}.{nameof(this.Input.RegistrationToken)}",
                        "Registration token not valid.");
                }

                var passwordIsBreached = await this.passwordBreachChecker.PasswordIsBreached(this.Input.Password);

                if (passwordIsBreached)
                {
                    this.ModelState.AddModelError(
                        $"{nameof(this.Input)}.{nameof(this.Input.Password)}",
                        "Password is known to have been compromised in a data breach.");
                }

                if (registrationTokenIsValid && !passwordIsBreached)
                {
                    var user = CreateApplicationUser(this.Input);

                    var result = await this.userManager.CreateAsync(user, this.Input.Password);

                    if (result.Succeeded)
                    {
                        this.logger.LogInformation($"Created user with Id {user.Id}.");

                        var emailConfirmationToken = await this.userManager.GenerateEmailConfirmationTokenAsync(user);
                        var callbackUrl = this.Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new { userId = user.Id, code = emailConfirmationToken },
                            protocol: this.Request.Scheme);

                        var encodedCallbackUrl = HtmlEncoder.Default.Encode(callbackUrl);
                        var emailBody =
                            "<p>Someone - hopefully you - registered this email address on the Parking Rota website.<p>" +
                            $"<p>If this was you, please confirm your account by <a href='{encodedCallbackUrl}'>clicking here</a>. If not, you can disregard this email.</p>" +
                            $"<p>The request originated from IP address {this.httpContextAccessor.GetOriginatingIpAddress()}</p>";

                        await this.emailSender.SendEmailAsync(this.Input.Email, "[Parking Rota] Confirm your email", emailBody);

                        return this.RedirectToPage("/RegisterSuccess");
                    }

                    foreach (var error in result.Errors)
                    {
                        this.ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            // If we got this far, something failed, redisplay form
            return this.Page();
        }

        private static ApplicationUser CreateApplicationUser(InputModel input)
        {
            const decimal DefaultCommuteDistance = 9.99m;

            return new ApplicationUser
            {
                FirstName = input.FirstName,
                LastName = input.LastName,
                CarRegistrationNumber = input.CarRegistrationNumber,
                CommuteDistance = DefaultCommuteDistance,
                UserName = input.Email,
                Email = input.Email
            };
        }
    }
}
