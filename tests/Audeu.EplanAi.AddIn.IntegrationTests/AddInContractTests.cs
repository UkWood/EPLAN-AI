using System.Linq;
using Audeu.EplanAi.AddIn;
using Audeu.EplanAi.AddIn.Actions;
using Eplan.EplApi.ApplicationFramework;
using Xunit;

namespace Audeu.EplanAi.AddIn.IntegrationTests
{
    public sealed class AddInContractTests
    {
        [Fact]
        public void Assembly_ContainsExactlyOneAddInImplementation()
        {
            var implementations = typeof(EplanAddIn).Assembly
                .GetTypes()
                .Where(type => typeof(IEplAddIn).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                .ToArray();

            Assert.Single(implementations);
            Assert.Equal(typeof(EplanAddIn), implementations[0]);
        }

        [Fact]
        public void Assembly_ContainsExactlyOneBridgeActionImplementation()
        {
            var implementations = typeof(EplanAddIn).Assembly
                .GetTypes()
                .Where(type => typeof(IEplAction).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                .ToArray();

            Assert.Single(implementations);
            Assert.Equal(typeof(AudeuAiDispatchAction), implementations[0]);
        }

        [Fact]
        public void AddIn_RegistersForStartupAndLifecycleSucceeds()
        {
            var addIn = new EplanAddIn();
            var loadOnStart = false;

            Assert.True(addIn.OnRegister(ref loadOnStart));
            Assert.True(loadOnStart);
            Assert.True(addIn.OnInit());
            Assert.True(addIn.OnInitGui());
            Assert.True(addIn.OnExit());
            Assert.True(addIn.OnUnregister());
        }

        [Fact]
        public void DispatchAction_RegistersFixedNameAndOrdinal()
        {
            var action = new AudeuAiDispatchAction();
            string name = null;
            var ordinal = 0;

            Assert.True(action.OnRegister(ref name, ref ordinal));
            Assert.Equal("AudeuAiDispatch", name);
            Assert.Equal(20, ordinal);
        }

        [Fact]
        public void DispatchAction_GetActionProperties_DoesNotRequireDescriptionMember()
        {
            var action = new AudeuAiDispatchAction();
            var properties = new ActionProperties();
            var original = properties;

            action.GetActionProperties(ref properties);

            Assert.Same(original, properties);
        }
    }
}
