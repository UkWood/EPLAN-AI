using System.Collections.Generic;

namespace Audeu.EplanAi.AddIn.ReadModel
{
    public interface IEplanReadAdapter
    {
        ProjectSnapshot GetActiveProject();
        IList<PageSnapshot> GetPages(uint databaseIdentifier);
        IList<ObjectSnapshot> GetSelection(uint databaseIdentifier);
        ObjectSnapshot ResolveObject(uint databaseIdentifier, string stringIdentifier);
    }
}
