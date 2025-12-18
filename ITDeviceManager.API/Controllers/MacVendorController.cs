using ITDeviceManager.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ITDeviceManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MacVendorController : ControllerBase
{
    private readonly MacVendorService _macVendorService;
    private readonly ILogger<MacVendorController> _logger;

    public MacVendorController(MacVendorService macVendorService, ILogger<MacVendorController> logger)
    {
        _macVendorService = macVendorService;
        _logger = logger;
    }

    /// <summary>
    /// Get vendor information by MAC address
    /// </summary>
    /// <param name="macAddress">MAC address (e.g., 00:00:0C:12:34:56 or 00-00-0C-12-34-56)</param>
    /// <returns>Vendor name</returns>
    [HttpGet("lookup/{macAddress}")]
    public ActionResult<object> LookupVendor(string macAddress)
    {
        try
        {
            var vendor = _macVendorService.GetVendorByMacPrefix(macAddress);
            return Ok(new
            {
                macAddress = macAddress,
                vendor = vendor,
                isLoaded = _macVendorService.IsLoaded,
                totalMappings = _macVendorService.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up vendor for MAC: {MacAddress}", macAddress);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get service statistics
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<object> GetStats()
    {
        return Ok(new
        {
            isLoaded = _macVendorService.IsLoaded,
            totalMappings = _macVendorService.Count
        });
    }

    /// <summary>
    /// Search vendors by name
    /// </summary>
    /// <param name="query">Vendor name search query</param>
    /// <returns>List of matching vendors</returns>
    [HttpGet("search")]
    public ActionResult<object> SearchVendors([FromQuery] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { error = "Query parameter is required" });
            }

            var results = _macVendorService.SearchByVendorName(query);
            return Ok(new
            {
                query = query,
                count = results.Count,
                results = results.Take(50) // Limit to 50 results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching vendors with query: {Query}", query);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint to verify common vendor lookups
    /// </summary>
    [HttpGet("test")]
    public ActionResult<object> Test()
    {
        var testMacs = new[]
        {
            "00:00:0C:12:34:56", // Cisco
            "00:50:56:AB:CD:EF", // VMware
            "08:00:27:12:34:56", // Oracle VirtualBox
            "00:1A:A0:12:34:56", // Dell
            "00:0D:93:12:34:56", // Apple
            "B8:27:EB:12:34:56", // Raspberry Pi Foundation
            "AA:BB:CC:DD:EE:FF"  // Unknown
        };

        var results = testMacs.Select(mac => new
        {
            mac = mac,
            vendor = _macVendorService.GetVendorByMacPrefix(mac)
        }).ToList();

        return Ok(new
        {
            testCount = results.Count,
            isLoaded = _macVendorService.IsLoaded,
            totalMappings = _macVendorService.Count,
            results = results
        });
    }
}
