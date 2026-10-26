using System;
using System.Collections.Generic;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.HEServices;

namespace Audeu.EplanAi.AddIn.ReadModel
{
    public sealed class EplanReadAdapter : IEplanReadAdapter
    {
        public ProjectSnapshot GetActiveProject()
        {
            var project = GetCurrentProject();
            if (project == null)
            {
                return null;
            }

            var pages = project.Pages ?? new Page[0];
            return new ProjectSnapshot
            {
                DatabaseIdentifier = project.DatabaseIdentifier,
                Name = project.ProjectName,
                FullName = project.ProjectFullName,
                PageCount = pages.Length,
                IsReadOnly = project.IsReadOnly
            };
        }

        public IList<PageSnapshot> GetPages(uint databaseIdentifier)
        {
            var project = GetCurrentProject();
            if (project == null || project.DatabaseIdentifier != databaseIdentifier)
            {
                return new List<PageSnapshot>();
            }

            var pages = project.Pages ?? new Page[0];
            var result = new List<PageSnapshot>(pages.Length);
            for (var index = 0; index < pages.Length; index++)
            {
                var page = pages[index];
                if (page == null || !page.IsValid || page.DatabaseIdentifier != databaseIdentifier)
                {
                    continue;
                }

                result.Add(new PageSnapshot
                {
                    Ordinal = index + 1,
                    Name = page.Name,
                    PageType = page.PageTypeName
                });
            }

            return result;
        }

        public IList<ObjectSnapshot> GetSelection(uint databaseIdentifier)
        {
            var project = GetCurrentProject();
            if (project == null || project.DatabaseIdentifier != databaseIdentifier)
            {
                return new List<ObjectSnapshot>();
            }

            var selection = new SelectionSet().Selection ?? new StorableObject[0];
            var result = new List<ObjectSnapshot>(selection.Length);
            foreach (var item in selection)
            {
                if (item == null || !item.IsValid || item.DatabaseIdentifier != databaseIdentifier)
                {
                    continue;
                }

                result.Add(CreateSnapshot(item));
            }

            return result;
        }

        public ObjectSnapshot ResolveObject(uint databaseIdentifier, string stringIdentifier)
        {
            var project = GetCurrentProject();
            if (project == null || project.DatabaseIdentifier != databaseIdentifier || string.IsNullOrWhiteSpace(stringIdentifier))
            {
                return null;
            }

            try
            {
                var item = StorableObject.FromStringIdentifier(stringIdentifier);
                if (item == null || !item.IsValid || item.DatabaseIdentifier != databaseIdentifier)
                {
                    return null;
                }

                return CreateSnapshot(item);
            }
            catch (Exception)
            {
                // FromStringIdentifier is runtime/session scoped and throws for stale or malformed identifiers.
                return null;
            }
        }

        private static Project GetCurrentProject()
        {
            var project = new SelectionSet().GetCurrentProject(false);
            return project != null && project.IsValid ? project : null;
        }

        private static ObjectSnapshot CreateSnapshot(StorableObject item)
        {
            var function = item as Function;
            return new ObjectSnapshot
            {
                DatabaseIdentifier = item.DatabaseIdentifier,
                TypeIdentifier = item.TypeIdentifier,
                ObjectIdentifier = item.ObjectIdentifier,
                StringIdentifier = item.ToStringIdentifier(),
                IsValid = item.IsValid,
                IsFunction = function != null,
                Kind = function == null ? item.GetTypeName() : "Function",
                Name = function == null ? null : function.Name,
                Page = function == null || function.Page == null ? null : function.Page.Name,
                FunctionText = function == null ? null : ReadFunctionText(function),
                DeviceTag = function == null ? null : ReadDeviceTag(function)
            };
        }

        private static string ReadFunctionText(Function function)
        {
            try
            {
                var value = function.Properties.FUNC_TEXT;
                return value == null ? null : value.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ReadDeviceTag(Function function)
        {
            try
            {
                var value = function.Properties.FUNC_IDENTDEVICETAG;
                return value == null ? null : value.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
