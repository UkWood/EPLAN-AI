using System;
using System.Collections.Generic;

namespace Audeu.EplanAi.Contracts
{
    public sealed class ChangePlan
    {
        public ChangePlan(
            Guid changePlanId,
            DateTime createdUtc,
            Guid projectSessionId,
            IEnumerable<ChangeOperation> operations)
        {
            ChangePlanId = changePlanId;
            CreatedUtc = createdUtc;
            ProjectSessionId = projectSessionId;
            Operations = new List<ChangeOperation>(operations).AsReadOnly();
            Status = ChangePlanStatuses.Previewed;
        }

        public Guid ChangePlanId { get; }

        public DateTime CreatedUtc { get; }

        public Guid ProjectSessionId { get; }

        public IReadOnlyList<ChangeOperation> Operations { get; }

        public string Status { get; private set; }

        public void MarkApplied()
        {
            EnsurePreviewed();
            Status = ChangePlanStatuses.Applied;
        }

        public void MarkFailed()
        {
            EnsurePreviewed();
            Status = ChangePlanStatuses.Failed;
        }

        private void EnsurePreviewed()
        {
            if (!string.Equals(Status, ChangePlanStatuses.Previewed, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Only a previewed change plan can transition state.");
            }
        }
    }
}
