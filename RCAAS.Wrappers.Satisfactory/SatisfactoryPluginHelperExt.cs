using RCAAS.Core.Helpers;
using RCAAS.Core.Interfaces;
using RCAAS.Core.Wrappers;

namespace RCAAS.Wrappers.Satisfactory;

/// <summary>
/// Plugin helper for Satisfactory dedicated server configuration.
/// </summary>
public class SatisfactoryPluginHelperExt : BasePluginHelper
{

    /// <summary>
    /// Gets the default Satisfactory server arguments with standard ports.
    /// </summary>
    /// <returns>The default Satisfactory arguments.</returns>
    public override BaseArgs GetDefaultArgs()
    {
        return new SatisfactoryArgs
        {
            ServerQueryPort = 15777, // UDP - Server browser query port
            BeaconPort = 15000       // UDP - Server discovery beacon port
        };
    }

    /// <summary>
    /// Gets the default application wrapper configuration for a Satisfactory server.
    /// </summary>
    /// <returns>The default configuration for the Satisfactory dedicated server.</returns>
    public override async Task<IAppWrapperConfig> GetDefaultCmdAppItemAsync()
    {
        var item = await base.GetDefaultCmdAppItemAsync().ConfigureAwait(false);
        var currentYear = TimeProvider.System.GetUtcNow().Year;

        item.Name = $"RCAAS Satisfactory server anno {currentYear}";
        item.WrapperName = "Satisfactory";
        item.Filename = "FactoryServer.exe";
        item.ExternalId = 1690800; // Steam App ID for Satisfactory Dedicated Server
        item.Port = await EthernetHelper.FindNextFreePortAsync(15002).ConfigureAwait(false);

        return item;
    }
}
