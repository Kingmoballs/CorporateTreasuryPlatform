using Moq;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;
using Treasury.Application.Common.Exceptions;

namespace Treasury.Tests.Transfers;

public class TransferServiceTests
{
    [Fact]
    public async Task ApproveTransfer_WhenRequesterIsReviewer_RejectsApproval()
    {
        // Arrange
        var requesterId =
            Guid.NewGuid();

        var request = new TransferRequest
        {
            Id = Guid.NewGuid(),
            FromAccountId = Guid.NewGuid(),
            ToAccountId = Guid.NewGuid(),
            Amount = 25_000_000m,
            Description = "Investment allocation",
            Status = ApprovalStatus.Pending,
            RequestedByUserId = requesterId,
            ConcurrencyToken = Guid.NewGuid()
        };

        var accountRepository =
            new Mock<IAccountRepository>();

        var ledgerRepository =
            new Mock<ILedgerRepository>();

        var transferRequestRepository =
            new Mock<ITransferRequestRepository>();

        var currentUserService =
            new Mock<ICurrentUserService>();

        var transactionRepository =
            new Mock<ITreasuryTransactionRepository>();
        
        var approvalPolicyService =
            new Mock<IApprovalPolicyService>();

        approvalPolicyService
            .Setup(service =>
                service.GetThreshold(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .ReturnsAsync(10_000_000m);

        accountRepository
            .Setup(repository =>
                repository.BeginTransaction())
            .Returns(Task.CompletedTask);

        accountRepository
            .Setup(repository =>
                repository.RollbackTransaction())
            .Returns(Task.CompletedTask);

        transferRequestRepository
            .Setup(repository =>
                repository.GetById(request.Id))
            .ReturnsAsync(request);

        currentUserService
            .SetupGet(service =>
                service.UserId)
            .Returns(requesterId);

        var service = new TransferService(
            accountRepository.Object,
            ledgerRepository.Object,
            transferRequestRepository.Object,
            currentUserService.Object,
            transactionRepository.Object,
            approvalPolicyService.Object);

        // Act
        var exception =
            await Assert.ThrowsAsync<
                ForbiddenOperationException>(
                    () =>
                        service.ApproveTransfer(
                            request.Id));

        // Assert
        Assert.Contains(
            "own transfer request",
            exception.Message);

        accountRepository.Verify(
            repository =>
                repository.RollbackTransaction(),
            Times.Once);

        ledgerRepository.Verify(
            repository =>
                repository.Add(
                    It.IsAny<LedgerEntry>()),
            Times.Never);
    }
}