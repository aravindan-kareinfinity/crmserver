namespace CRM.Server.DTOs
{
    /// <summary>Request envelope (same shape as crm-server <c>ActionReq&lt;T&gt;</c>).</summary>
    public class ActionReq<T>
    {
        public T item { get; set; } = default!;
    }

    /// <summary>Response envelope (same shape as crm-server <c>ActionRes&lt;T&gt;</c>).</summary>
    public class ActionRes<T>
    {
        public T item { get; set; } = default!;
    }
}
