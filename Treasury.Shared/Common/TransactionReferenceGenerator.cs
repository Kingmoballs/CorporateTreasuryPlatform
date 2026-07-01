namespace Treasury.Shared.Common;

public static class TransactionReferenceGenerator
{
    public static string Generate()
    {
        return
            $"TRX-{DateTime.UtcNow:yyyyMMdd}-" +
            $"{Guid.NewGuid():N}"
                .ToUpperInvariant();
    }
}