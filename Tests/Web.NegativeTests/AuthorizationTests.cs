using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using Web.NegativeTests.TestInfrastructure;

namespace Web.NegativeTests
{
    /// <summary>
    /// End-to-end HTTP tests through the real ASP.NET Core pipeline: unauthenticated access to
    /// protected routes, a malformed route value, and an empty form submission. These are
    /// deliberately *not* unit tests against the controller directly, because the behaviour being
    /// verified here (redirect to login, 400 on bad model binding, antiforgery + validation on a
    /// Razor Page form) is enforced by the framework pipeline itself, not by any code in the
    /// controllers/pages.
    /// </summary>
    public class AuthorizationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AuthorizationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _factory.EnsureDatabaseCreated();
        }

        private HttpClient ClientWithoutFollowingRedirects() =>
            _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        [Fact]
        public async Task AnonymousUser_AccessingTransactionsList_IsRedirectedToLogin()
        {
            var client = ClientWithoutFollowingRedirects();

            var response = await client.GetAsync($"/Dashboard/Transactions/List?accountId={Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task AnonymousUser_AccessingAdminConsole_IsRedirectedToLogin()
        {
            var client = ClientWithoutFollowingRedirects();

            var response = await client.GetAsync("/Dashboard/Admin");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task AnonymousUser_MalformedGuidInRoute_IsRedirectedToLogin_NeverReachesModelBinding()
        {
            // [Authorize] runs before model binding, so an anonymous request with a malformed accountId
            // never even gets to the point of validating the guid - it's stopped at the login redirect first.
            var client = ClientWithoutFollowingRedirects();

            var response = await client.GetAsync("/Dashboard/Transactions/List?accountId=not-a-valid-guid");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Fact]
        public async Task AuthenticatedUser_MalformedGuidInRoute_DoesNotCrash_ReturnsNotFound()
        {
            // TransactionsController is a plain MVC Controller (not [ApiController]), so ASP.NET Core
            // does not auto-reject an unparseable Guid with 400 the way it would on an [ApiController]
            // action (see BankController for that behaviour instead). A malformed accountId silently
            // model-binds to Guid.Empty, and the controller's own "does this account exist and belong
            // to me" check takes it from there - so the actually-observed, safe outcome is 404, not a
            // 500 or an unhandled FormatException. This test pins that down so a future change to the
            // binding/validation doesn't silently start leaking a stack trace instead.
            var client = _factory.CreateAuthenticatedClient(userId: "some-authenticated-user-id");

            var response = await client.GetAsync("/Dashboard/Transactions/List?accountId=not-a-valid-guid");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task AuthenticatedUser_MalformedGuidInApiRoute_ReturnsBadRequest()
        {
            // Contrast with the MVC case above: BankController is an [ApiController], which does
            // automatically reject a route value that fails model binding with 400, before the action
            // body runs at all.
            var client = _factory.CreateAuthenticatedClient(userId: "some-authenticated-user-id");

            var response = await client.PostAsync("/api/bank/transactions/not-a-valid-guid/sync", content: null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task EmptyRegistrationForm_ReturnsValidationErrors_DoesNotCreateAccount()
        {
            var client = _factory.CreateClient();

            var getResponse = await client.GetAsync("/Identity/Account/Register");
            getResponse.EnsureSuccessStatusCode();
            var html = await getResponse.Content.ReadAsStringAsync();

            var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
            var antiforgeryCookie = getResponse.Headers.GetValues("Set-Cookie")
                .First(c => c.StartsWith(".AspNetCore.Antiforgery"));

            using var request = new HttpRequestMessage(HttpMethod.Post, "/Identity/Account/Register")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Input.Email"] = "",
                    ["Input.Password"] = "",
                    ["Input.ConfirmPassword"] = "",
                    ["__RequestVerificationToken"] = token
                })
            };
            request.Headers.Add("Cookie", antiforgeryCookie.Split(';')[0]);

            var postResponse = await client.SendAsync(request);

            // Model validation fails before any Identity/db call is made - the page re-renders (200)
            // with validation errors instead of redirecting to a "registration successful" page.
            Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Data.Services.AppDbContext>();
            Assert.Empty(db.Users);
        }
    }
}
