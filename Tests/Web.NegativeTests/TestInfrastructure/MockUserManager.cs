using Data.Model;
using Microsoft.AspNetCore.Identity;
using Moq;
using System.Security.Claims;

namespace Web.NegativeTests.TestInfrastructure
{
    /// <summary>
    /// UserManager&lt;TUser&gt; has no interface and most of its members aren't virtual, so it can't be
    /// mocked directly with Moq - the standard workaround is to mock the IUserStore it wraps and
    /// construct a real UserManager on top of that fake store. GetUserId(ClaimsPrincipal) itself
    /// never touches the store (it just reads the NameIdentifier claim), so the controllers under
    /// test work correctly as long as the ClaimsPrincipal on the request carries that claim.
    /// </summary>
    public static class MockUserManager
    {
        public static UserManager<AppUser> Create()
        {
            var store = new Mock<IUserStore<AppUser>>();
            return new UserManager<AppUser>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        public static ClaimsPrincipal PrincipalFor(string userId) =>
            new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
                authenticationType: "TestAuth"));
    }
}
