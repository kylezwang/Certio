namespace Certio.Web.Services
{
    public interface IClientContextAccessor
    {
        IClientContext? Current { get; }
        ClientContext? ClientContext { get; set; }
    }

    public sealed class ClientContextAccessor : IClientContextAccessor
    {
        public IClientContext? Current => ClientContext;
        public ClientContext? ClientContext { get; set; }
    }
}


