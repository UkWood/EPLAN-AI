using System;
using Audeu.EplanAi.AddIn.Dispatch;
using Audeu.EplanAi.AddIn.ReadModel;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.HEServices;
using Xunit;

namespace Audeu.EplanAi.AddIn.IntegrationTests
{
    public sealed class ReadAdapterContractTests : IDisposable
    {
        public ReadAdapterContractTests()
        {
            SelectionSet.StubCurrentProject = null;
            SelectionSet.StubSelection = new StorableObject[0];
            StorableObject.StubResolver = null;
        }

        [Fact]
        public void Adapter_MapsActiveProjectAndPages()
        {
            var project = new Project
            {
                DatabaseIdentifier = 7,
                ProjectName = "Demo",
                ProjectFullName = "C:\\Projects\\Demo",
                IsReadOnly = true,
                IsValid = true,
                Pages = new[]
                {
                    new Page { DatabaseIdentifier = 7, Name = "=A/1", PageTypeName = "Schematic", IsValid = true },
                    new Page { DatabaseIdentifier = 7, Name = "=A/2", PageTypeName = "Schematic", IsValid = true }
                }
            };
            SelectionSet.StubCurrentProject = project;

            var adapter = new EplanReadAdapter();
            var active = adapter.GetActiveProject();
            var pages = adapter.GetPages(7);

            Assert.NotNull(active);
            Assert.Equal((uint)7, active.DatabaseIdentifier);
            Assert.Equal("Demo", active.Name);
            Assert.Equal("C:\\Projects\\Demo", active.FullName);
            Assert.Equal(2, active.PageCount);
            Assert.True(active.IsReadOnly);
            Assert.Equal(2, pages.Count);
            Assert.Equal(1, pages[0].Ordinal);
            Assert.Equal("=A/1", pages[0].Name);
            Assert.Equal("Schematic", pages[0].PageType);
        }

        [Fact]
        public void Adapter_MapsSelectionAndFunctionWhitelist()
        {
            var page = new Page { DatabaseIdentifier = 7, Name = "=A/1", IsValid = true };
            var function = new Function
            {
                DatabaseIdentifier = 7,
                TypeIdentifier = 27,
                ObjectIdentifier = 1482,
                StubStringIdentifier = "Function:1482",
                IsValid = true,
                Name = "-K1",
                Page = page
            };
            function.Properties.FUNC_TEXT = new PropertyValue("Pump contactor");
            function.Properties.FUNC_IDENTDEVICETAG = new PropertyValue("-K1");
            SelectionSet.StubCurrentProject = new Project { DatabaseIdentifier = 7, IsValid = true };
            SelectionSet.StubSelection = new StorableObject[] { function };

            var item = new EplanReadAdapter().GetSelection(7)[0];

            Assert.True(item.IsFunction);
            Assert.Equal("Function", item.Kind);
            Assert.Equal((uint)7, item.DatabaseIdentifier);
            Assert.Equal((uint)27, item.TypeIdentifier);
            Assert.Equal((long)1482, item.ObjectIdentifier);
            Assert.Equal("Function:1482", item.StringIdentifier);
            Assert.Equal("-K1", item.Name);
            Assert.Equal("=A/1", item.Page);
            Assert.Equal("Pump contactor", item.FunctionText);
            Assert.Equal("-K1", item.DeviceTag);
        }

        [Fact]
        public void Adapter_UsesDisplayedFunctionTextForMultiLangString()
        {
            var project = new Project { DatabaseIdentifier = 7, IsValid = true };
            var function = new Function
            {
                DatabaseIdentifier = 7,
                TypeIdentifier = 27,
                ObjectIdentifier = 1482,
                StubStringIdentifier = "Function:1482",
                IsValid = true,
                Name = "-A1",
                Project = project
            };
            function.Properties.FUNC_TEXT = new PropertyValue("??_??@Контроллер УСПД БКД-ПК-RF.2;");
            SelectionSet.StubCurrentProject = project;
            SelectionSet.StubSelection = new StorableObject[] { function };

            var item = new EplanReadAdapter().GetSelection(7)[0];

            Assert.Equal("Контроллер УСПД БКД-ПК-RF.2", item.FunctionText);
        }

        [Fact]
        public void Adapter_ResolvesRuntimeStringIdentifier()
        {
            var function = new Function
            {
                DatabaseIdentifier = 7,
                TypeIdentifier = 27,
                ObjectIdentifier = 1482,
                StubStringIdentifier = "Function:1482",
                IsValid = true,
                Name = "-K1"
            };
            SelectionSet.StubCurrentProject = new Project { DatabaseIdentifier = 7, IsValid = true };
            StorableObject.StubResolver = value => value == "Function:1482" ? function : null;

            var resolved = new EplanReadAdapter().ResolveObject(7, "Function:1482");

            Assert.NotNull(resolved);
            Assert.True(resolved.IsFunction);
            Assert.Equal((long)1482, resolved.ObjectIdentifier);
        }

        [Fact]
        public void Registry_ExposesOnlyFixedTask5ReadOperationsPlusStatus()
        {
            var registry = new OperationRegistry();
            Func<Audeu.EplanAi.Contracts.BridgeRequest, Audeu.EplanAi.Contracts.BridgeResponse> handler;

            Assert.True(registry.TryResolve("system.status", out handler));
            Assert.True(registry.TryResolve("project.getActive", out handler));
            Assert.True(registry.TryResolve("project.listPages", out handler));
            Assert.True(registry.TryResolve("selection.get", out handler));
            Assert.True(registry.TryResolve("object.get", out handler));
            Assert.False(registry.TryResolve("eplan.setProperty", out handler));
        }

        public void Dispose()
        {
            SelectionSet.StubCurrentProject = null;
            SelectionSet.StubSelection = new StorableObject[0];
            StorableObject.StubResolver = null;
        }
    }
}
