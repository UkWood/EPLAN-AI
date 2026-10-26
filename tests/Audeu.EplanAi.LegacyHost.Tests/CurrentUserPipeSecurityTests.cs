using System.Security.AccessControl;
using System.Security.Principal;
using Audeu.EplanAi.LegacyHost.Pipe;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class CurrentUserPipeSecurityTests
    {
        [Fact]
        public void Create_OwnsAndAllowsOnlyRequestedSid()
        {
            var sid = new SecurityIdentifier("S-1-5-21-1000-1001-1002-1003");

            var security = CurrentUserPipeSecurity.Create(sid);

            Assert.Equal(sid, security.GetOwner(typeof(SecurityIdentifier)));
            Assert.True(security.AreAccessRulesProtected);

            var rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: false,
                targetType: typeof(SecurityIdentifier));

            Assert.Single(rules);
            var rule = Assert.IsType<System.IO.Pipes.PipeAccessRule>(rules[0]);
            Assert.Equal(sid, rule.IdentityReference);
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
            Assert.Equal(System.IO.Pipes.PipeAccessRights.FullControl, rule.PipeAccessRights);
        }

        [Fact]
        public void GetCurrentUserSid_ReturnsWindowsIdentitySid()
        {
            var expected = WindowsIdentity.GetCurrent().User;
            Assert.NotNull(expected);

            var actual = CurrentUserPipeSecurity.GetCurrentUserSid();

            Assert.Equal(expected, actual);
        }
    }
}
