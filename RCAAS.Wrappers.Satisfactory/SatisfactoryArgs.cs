using RCAAS.Core.Wrappers;
using System.ComponentModel.DataAnnotations;

namespace RCAAS.Wrappers.Satisfactory;

/// <summary>
/// Represents Satisfactory-specific server command line arguments.
/// </summary>
public class SatisfactoryArgs : BaseArgs
{
    /// <summary>
    /// Gets or sets the server query port used for server browser connections.
    /// Default is 15777 (UDP).
    /// </summary>
    [Range(1024, 65535, ErrorMessage = "ServerQueryPort must be between 1024 and 65535.")]
    public int ServerQueryPort { get; set; }

    /// <summary>
    /// Gets or sets the beacon port for server discovery.
    /// Default is 15000 (UDP). The server will increment this port if it's already in use.
    /// </summary>
    [Range(1024, 65535, ErrorMessage = "BeaconPort must be between 1024 and 65535.")]
    public int BeaconPort { get; set; }
}
