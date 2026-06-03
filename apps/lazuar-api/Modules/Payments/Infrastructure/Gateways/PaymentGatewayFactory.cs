using Modules.Payments.Application.Ports;

namespace Modules.Payments.Infrastructure.Gateways;

public class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly IEnumerable<IPaymentGatewayAdapter> _adapters;

    public PaymentGatewayFactory(IEnumerable<IPaymentGatewayAdapter> adapters)
    {
        _adapters = adapters;
    }

    public IPaymentGatewayAdapter GetAdapter(string gatewayType)
    {
        var normalizedType = gatewayType.ToUpperInvariant();
        var adapter = _adapters.FirstOrDefault(a => a.GatewayType == normalizedType);

        if (adapter == null)
        {
            throw new NotSupportedException($"Payment gateway type '{gatewayType}' is not supported.");
        }

        return adapter;
    }
}
