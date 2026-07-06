using Treasury.Application.DTOs.ApprovalPolicies;

namespace Treasury.Application.Interfaces;

public interface IApprovalPolicyService
{
    Task<decimal> GetThreshold(
        string operationType,
        string currency);

    Task<List<ApprovalPolicyDto>> GetAll();Task<ApprovalRequirementsDto>
        GetRequirements(
            string operationType,
            string currency);



    Task<ApprovalPolicyDto> SavePolicy(
        UpdateApprovalPolicyDto dto);
}