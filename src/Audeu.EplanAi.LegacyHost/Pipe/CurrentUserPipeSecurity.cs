using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Audeu.EplanAi.LegacyHost.Pipe
{
    public static class CurrentUserPipeSecurity
    {
        public static SecurityIdentifier GetCurrentUserSid()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                if (identity == null || identity.User == null)
                {
                    throw new InvalidOperationException("The current Windows user SID is unavailable.");
                }

                return identity.User;
            }
        }

        public static PipeSecurity Create(SecurityIdentifier currentUserSid)
        {
            if (currentUserSid == null)
            {
                throw new ArgumentNullException(nameof(currentUserSid));
            }

            var security = new PipeSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.SetOwner(currentUserSid);
            security.AddAccessRule(new PipeAccessRule(
                currentUserSid,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
            return security;
        }
    }
}
