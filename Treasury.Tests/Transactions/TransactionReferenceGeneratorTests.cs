using Treasury.Shared.Common;

namespace Treasury.Tests.Transactions;

public class TransactionReferenceGeneratorTests
{
    [Fact]
    public void Generate_ReturnsTransactionPrefix()
    {
        var reference =
            TransactionReferenceGenerator.Generate();

        Assert.StartsWith(
            "TRX-",
            reference);
    }

    [Fact]
    public void Generate_CreatesUniqueReferences()
    {
        var references =
            Enumerable.Range(1, 100)
                .Select(_ =>
                    TransactionReferenceGenerator
                        .Generate())
                .ToList();

        Assert.Equal(
            references.Count,
            references.Distinct().Count());
    }
}