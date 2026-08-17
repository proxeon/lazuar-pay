using FluentAssertions;
using Modules.Billing.Application;
using Modules.Billing.Infrastructure.Commands;
using Modules.Billing.Infrastructure.Repositories;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Commands;

[TestFixture]
public class SequenceTransactionTests
{
    [Test]
    public void LedgerRepository_ImplementsBillingTransactional()
    {
        typeof(LedgerRepository).Should().Implement<IBillingTransactional>();
        typeof(ILedgerRepository).Should().NotBeAssignableTo<IBillingTransactional>();
    }

    [Test]
    public void SequenceHandler_UsesBillingDbContext()
    {
        var ctor = typeof(GenerateNextSequenceNumberCommandHandler).GetConstructors()[0];
        ctor.GetParameters().Should().ContainSingle(p =>
            p.ParameterType == typeof(Modules.Billing.Infrastructure.BillingDbContext));
    }
}
