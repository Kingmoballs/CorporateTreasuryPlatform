using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.ApprovalPolicies;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/admin/approval-policies")]
[ApiController]
[Authorize(Roles = Roles.Admin)]
public class ApprovalPoliciesController
    : ControllerBase
{
    private readonly IApprovalPolicyService
        _policyService;

    public ApprovalPoliciesController(
        IApprovalPolicyService policyService)
    {
        _policyService = policyService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(
            await _policyService.GetAll());
    }

    [HttpPut]
    public async Task<IActionResult> SavePolicy(
        UpdateApprovalPolicyDto dto)
    {
        return Ok(
            await _policyService
                .SavePolicy(dto));
    }
}