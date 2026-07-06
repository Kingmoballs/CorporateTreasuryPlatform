using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IApprovalPolicyRepository
{
    Task<ApprovalPolicy?> GetActive(
        string operationType,
        string currency);

    Task<ApprovalPolicy?> GetByKey(
        string operationType,
        string currency);

    Task<List<ApprovalPolicy>> GetAll();

    Task Add(ApprovalPolicy policy);

    Task SaveChanges();
}