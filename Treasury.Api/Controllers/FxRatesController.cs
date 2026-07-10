using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Fx;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/fx-rates")]
[ApiController]
[Authorize(Roles = FxRoles)]
public class FxRatesController
    : ControllerBase
{
    private const string FxRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IFxRateService _fxRateService;

    public FxRatesController(
        IFxRateService fxRateService)
    {
        _fxRateService =
            fxRateService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateFxRateDto dto)
    {
        var result =
            await _fxRateService.Create(dto);

        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateFxRateDto dto)
    {
        var result =
            await _fxRateService.Update(
                id,
                dto);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        Guid id)
    {
        var result =
            await _fxRateService.GetById(id);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetRates(
        [FromQuery] string? fromCurrency,
        [FromQuery] string? toCurrency,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc)
    {
        var result =
            await _fxRateService.GetRates(
                fromCurrency,
                toCurrency,
                fromUtc,
                toUtc);

        return Ok(result);
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatestRate(
        [FromQuery] string fromCurrency,
        [FromQuery] string toCurrency,
        [FromQuery] DateTime? asOfUtc)
    {
        var result =
            await _fxRateService.GetLatestRate(
                fromCurrency,
                toCurrency,
                asOfUtc);

        return Ok(result);
    }

    [HttpGet("convert")]
    public async Task<IActionResult> ConvertAmount(
        [FromQuery] decimal amount,
        [FromQuery] string fromCurrency,
        [FromQuery] string toCurrency,
        [FromQuery] DateTime? asOfUtc)
    {
        var result =
            await _fxRateService.ConvertAmount(
                amount,
                fromCurrency,
                toCurrency,
                asOfUtc);

        return Ok(result);
    }

    [HttpGet("cash-position")]
    public async Task<IActionResult> GetConsolidatedCashPosition(
        [FromQuery] string baseCurrency,
        [FromQuery] DateTime? asOfUtc)
    {
        var result =
            await _fxRateService
                .GetConsolidatedCashPosition(
                    baseCurrency,
                    asOfUtc);

        return Ok(result);
    }

    [HttpGet("currency-exposure")]
    public async Task<IActionResult> GetCurrencyExposureReport(
        [FromQuery] string baseCurrency,
        [FromQuery] DateTime? asOfUtc)
    {
        var result =
            await _fxRateService
                .GetCurrencyExposureReport(
                    baseCurrency,
                    asOfUtc);

        return Ok(result);
    }
}