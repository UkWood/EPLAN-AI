using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Audeu.EplanAi.AddIn.ReadModel
{
    public sealed class ProjectSnapshot
    {
        public uint DatabaseIdentifier { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public int PageCount { get; set; }
        public bool IsReadOnly { get; set; }
    }

    public sealed class PageSnapshot
    {
        public int Ordinal { get; set; }
        public string Name { get; set; }
        public string PageType { get; set; }
    }

    public sealed class ObjectSnapshot
    {
        public uint DatabaseIdentifier { get; set; }
        public uint TypeIdentifier { get; set; }
        public long ObjectIdentifier { get; set; }
        public string StringIdentifier { get; set; }
        public bool IsValid { get; set; }
        public bool IsFunction { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string Page { get; set; }
        public string FunctionText { get; set; }
        public string DeviceTag { get; set; }
    }

    [DataContract]
    internal sealed class ActiveProjectResult
    {
        [DataMember(Name = "projectSessionId", Order = 1)] public Guid ProjectSessionId { get; set; }
        [DataMember(Name = "databaseIdentifier", Order = 2)] public uint DatabaseIdentifier { get; set; }
        [DataMember(Name = "name", Order = 3)] public string Name { get; set; }
        [DataMember(Name = "fullName", Order = 4)] public string FullName { get; set; }
        [DataMember(Name = "pageCount", Order = 5)] public int PageCount { get; set; }
        [DataMember(Name = "isReadOnly", Order = 6)] public bool IsReadOnly { get; set; }
    }

    [DataContract]
    internal sealed class PageListResult
    {
        [DataMember(Name = "projectSessionId", Order = 1)] public Guid ProjectSessionId { get; set; }
        [DataMember(Name = "items", Order = 2)] public List<PageItemResult> Items { get; set; }
    }

    [DataContract]
    internal sealed class PageItemResult
    {
        [DataMember(Name = "ordinal", Order = 1)] public int Ordinal { get; set; }
        [DataMember(Name = "name", Order = 2)] public string Name { get; set; }
        [DataMember(Name = "pageType", Order = 3)] public string PageType { get; set; }
    }

    [DataContract]
    internal sealed class SelectionResult
    {
        [DataMember(Name = "projectSessionId", Order = 1)] public Guid ProjectSessionId { get; set; }
        [DataMember(Name = "items", Order = 2)] public List<SelectionItemResult> Items { get; set; }
    }

    [DataContract]
    internal sealed class SelectionItemResult
    {
        [DataMember(Name = "kind", Order = 1)] public string Kind { get; set; }
        [DataMember(Name = "reference", Order = 2)] public ObjectReferenceResult Reference { get; set; }
        [DataMember(Name = "name", Order = 3)] public string Name { get; set; }
        [DataMember(Name = "page", Order = 4)] public string Page { get; set; }
    }

    [DataContract]
    internal sealed class FunctionResult
    {
        [DataMember(Name = "kind", Order = 1)] public string Kind { get; set; }
        [DataMember(Name = "reference", Order = 2)] public ObjectReferenceResult Reference { get; set; }
        [DataMember(Name = "name", Order = 3)] public string Name { get; set; }
        [DataMember(Name = "page", Order = 4)] public string Page { get; set; }
        [DataMember(Name = "functionText", Order = 5)] public string FunctionText { get; set; }
        [DataMember(Name = "deviceTag", Order = 6)] public string DeviceTag { get; set; }
    }

    [DataContract]
    internal sealed class ObjectReferenceResult
    {
        [DataMember(Name = "projectSessionId", Order = 1)] public Guid ProjectSessionId { get; set; }
        [DataMember(Name = "databaseIdentifier", Order = 2)] public uint DatabaseIdentifier { get; set; }
        [DataMember(Name = "typeIdentifier", Order = 3)] public uint TypeIdentifier { get; set; }
        [DataMember(Name = "objectIdentifier", Order = 4)] public long ObjectIdentifier { get; set; }
        [DataMember(Name = "stringIdentifier", Order = 5)] public string StringIdentifier { get; set; }
    }
}
