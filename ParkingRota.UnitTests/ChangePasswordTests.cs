﻿namespace ParkingRota.UnitTests
{
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Areas.Identity.Pages.Account.Manage;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.Extensions.Logging;
    using Moq;
    using ParkingRota.Business;
    using Xunit;

    public class ChangePasswordTests
    {
        [Theory]
        [InlineData("An old password", "An unbreached new password")]
        [InlineData("Another old password", "Another unbreached new password")]
        public async Task Test_ChangePassword_Succeeds(string oldPassword, string newPassword)
        {
            // Arrange
            var user = new ClaimsPrincipal();
            var identityUser = new IdentityUser();

            // Set up user manager
            var mockUserManager = new Mock<UserManager<IdentityUser>>(
                Mock.Of<IUserStore<IdentityUser>>(), null, null, null, null, null, null, null, null);

            mockUserManager
                .Setup(u => u.GetUserAsync(user))
                .Returns(Task.FromResult(identityUser));
            mockUserManager
                .Setup(u => u.ChangePasswordAsync(identityUser, oldPassword, newPassword))
                .Returns(Task.FromResult(IdentityResult.Success));

            // Set up sign in manager
            var httpContextAccessor = Mock.Of<IHttpContextAccessor>();
            var userClaimsPrincipalFactory = Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>();

            var mockSigninManager = new Mock<SignInManager<IdentityUser>>(
                mockUserManager.Object, httpContextAccessor, userClaimsPrincipalFactory, null, null, null);

            // Set up password breach checker
            var mockPasswordBreachChecker = new Mock<IPasswordBreachChecker>(MockBehavior.Strict);
            mockPasswordBreachChecker
                .Setup(c => c.PasswordIsBreached(newPassword))
                .Returns(Task.FromResult(false));

            // Set up model
            var model = new ChangePasswordModel(
                mockUserManager.Object,
                mockSigninManager.Object,
                mockPasswordBreachChecker.Object,
                Mock.Of<ILogger<ChangePasswordModel>>())
            {
                PageContext = { HttpContext = new DefaultHttpContext { User = user } },
                Input = new ChangePasswordModel.InputModel { OldPassword = oldPassword, NewPassword = newPassword }
            };

            // Act
            var result = await model.OnPostAsync();

            // Assert
            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("Your password has been changed.", model.StatusMessage);
        }

        [Theory]
        [InlineData("A password")]
        [InlineData("Another password")]
        public async Task Test_ChangePassword_BreachedPassword(string password)
        {
            // Arrange
            var user = new ClaimsPrincipal();
            var identityUser = new IdentityUser();

            // Set up user manager
            var mockUserManager = new Mock<UserManager<IdentityUser>>(
                Mock.Of<IUserStore<IdentityUser>>(), null, null, null, null, null, null, null, null);

            mockUserManager
                .Setup(u => u.GetUserAsync(user))
                .Returns(Task.FromResult(identityUser));

            // Set up sign in manager
            var httpContextAccessor = Mock.Of<IHttpContextAccessor>();
            var userClaimsPrincipalFactory = Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>();

            var mockSigninManager = new Mock<SignInManager<IdentityUser>>(
                mockUserManager.Object, httpContextAccessor, userClaimsPrincipalFactory, null, null, null);

            // Set up password breach checker
            var mockPasswordBreachChecker = new Mock<IPasswordBreachChecker>(MockBehavior.Strict);
            mockPasswordBreachChecker
                .Setup(c => c.PasswordIsBreached(password))
                .Returns(Task.FromResult(true));

            // Set up model
            var model = new ChangePasswordModel(
                mockUserManager.Object,
                mockSigninManager.Object,
                mockPasswordBreachChecker.Object,
                Mock.Of<ILogger<ChangePasswordModel>>())
            {
                PageContext = { HttpContext = new DefaultHttpContext { User = user } },
                Input = new ChangePasswordModel.InputModel { NewPassword = password }
            };

            // Act
            var result = await model.OnPostAsync();

            // Assert
            Assert.IsType<PageResult>(result);
        }
    }
}