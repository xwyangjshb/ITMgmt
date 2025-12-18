namespace ITDeviceManager.Core.Models
{
    /// <summary>
    /// Represents a MAC address prefix to vendor name mapping
    /// </summary>
    public class MacVendorMapping
    {
        /// <summary>
        /// MAC address prefix (e.g., "00:00:0C")
        /// </summary>
        public string MacPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Vendor/manufacturer name
        /// </summary>
        public string VendorName { get; set; } = string.Empty;

        public MacVendorMapping()
        {
        }

        public MacVendorMapping(string macPrefix, string vendorName)
        {
            MacPrefix = macPrefix;
            VendorName = vendorName;
        }
    }
}
