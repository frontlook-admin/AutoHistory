using System;

namespace Microsoft.EntityFrameworkCore
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    public class ExcludeFromHistoryAttribute : Attribute { }
}
