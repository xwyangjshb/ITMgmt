using ITDeviceManager.Core.Models;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;

namespace ITDeviceManager.Core.Services
{
    /// <summary>
    /// Service for managing MAC address vendor mappings in memory
    /// </summary>
    public class MacVendorService
    {
        private readonly ILogger<MacVendorService> _logger;
        private readonly List<MacVendorMapping> _vendorMappings;
        private readonly object _lock = new object();

        public MacVendorService(ILogger<MacVendorService> logger)
        {
            _logger = logger;
            _vendorMappings = new List<MacVendorMapping>();
        }

        /// <summary>
        /// Load vendor mappings from XML file
        /// </summary>
        /// <param name="xmlFilePath">Path to vendorMacs.xml file</param>
        /// <returns>Number of mappings loaded</returns>
        public int LoadFromXml(string xmlFilePath)
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(xmlFilePath))
                    {
                        _logger.LogError($"Vendor MAC XML file not found: {xmlFilePath}");
                        return 0;
                    }

                    _logger.LogInformation($"Loading MAC vendor mappings from {xmlFilePath}");

                    // Clear existing mappings
                    _vendorMappings.Clear();

                    // Parse XML using XDocument
                    XDocument doc = XDocument.Load(xmlFilePath);
                    XNamespace ns = "http://www.cisco.com/server/spt";

                    var mappings = doc.Descendants(ns + "VendorMapping")
                        .Select(element => new MacVendorMapping
                        {
                            MacPrefix = element.Attribute("mac_prefix")?.Value ?? string.Empty,
                            VendorName = element.Attribute("vendor_name")?.Value ?? string.Empty
                        })
                        .Where(m => !string.IsNullOrWhiteSpace(m.MacPrefix))
                        .ToList();

                    _vendorMappings.AddRange(mappings);

                    _logger.LogInformation($"Successfully loaded {_vendorMappings.Count} MAC vendor mappings");
                    return _vendorMappings.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error loading MAC vendor mappings from {xmlFilePath}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// Get vendor name by MAC address prefix
        /// </summary>
        /// <param name="macAddress">Full MAC address (e.g., "00:00:0C:12:34:56") or prefix (e.g., "00:00:0C")</param>
        /// <returns>Vendor name if found, otherwise "Unknown"</returns>
        public string GetVendorByMacPrefix(string macAddress)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return "Unknown";
            }

            // Normalize MAC address format (remove dashes, convert to uppercase)
            string normalizedMac = macAddress.Replace("-", ":").ToUpper().Trim();

            // Extract first 8 characters (e.g., "00:00:0C" from "00:00:0C:12:34:56")
            string prefix8Char = normalizedMac.Length >= 8 ? normalizedMac.Substring(0, 8) : normalizedMac;

            // Try to find exact match for 8-character prefix (e.g., "00:00:0C")
            var mapping = _vendorMappings.FirstOrDefault(m =>
                m.MacPrefix.Equals(prefix8Char, StringComparison.OrdinalIgnoreCase));

            if (mapping != null)
            {
                return mapping.VendorName;
            }

            // Try to find match with 6-character prefix (e.g., "00:00:0" for shorter MACs)
            if (normalizedMac.Length >= 7)
            {
                string prefix7Char = normalizedMac.Substring(0, 7);
                mapping = _vendorMappings.FirstOrDefault(m =>
                    m.MacPrefix.StartsWith(prefix7Char, StringComparison.OrdinalIgnoreCase));

                if (mapping != null)
                {
                    return mapping.VendorName;
                }
            }

            // Try to find match with first 5 characters (e.g., "00:00")
            if (normalizedMac.Length >= 5)
            {
                string prefix5Char = normalizedMac.Substring(0, 5);
                mapping = _vendorMappings.FirstOrDefault(m =>
                    m.MacPrefix.StartsWith(prefix5Char, StringComparison.OrdinalIgnoreCase));

                if (mapping != null)
                {
                    return mapping.VendorName;
                }
            }

            return "Unknown";
        }

        /// <summary>
        /// Get all vendor mappings
        /// </summary>
        /// <returns>Read-only collection of all mappings</returns>
        public IReadOnlyList<MacVendorMapping> GetAllMappings()
        {
            return _vendorMappings.AsReadOnly();
        }

        /// <summary>
        /// Get total count of loaded mappings
        /// </summary>
        public int Count => _vendorMappings.Count;

        /// <summary>
        /// Check if mappings are loaded
        /// </summary>
        public bool IsLoaded => _vendorMappings.Count > 0;

        /// <summary>
        /// Search vendors by name (case-insensitive partial match)
        /// </summary>
        /// <param name="vendorNamePart">Part of vendor name to search for</param>
        /// <returns>List of matching mappings</returns>
        public List<MacVendorMapping> SearchByVendorName(string vendorNamePart)
        {
            if (string.IsNullOrWhiteSpace(vendorNamePart))
            {
                return new List<MacVendorMapping>();
            }

            return _vendorMappings
                .Where(m => m.VendorName.Contains(vendorNamePart, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Get all unique vendor names
        /// </summary>
        /// <returns>Sorted list of unique vendor names</returns>
        public List<string> GetAllVendorNames()
        {
            return _vendorMappings
                .Select(m => m.VendorName)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
        }
    }
}
