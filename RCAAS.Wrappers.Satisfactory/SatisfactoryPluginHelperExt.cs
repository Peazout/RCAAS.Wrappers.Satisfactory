using RCAAS.Core.Helpers;
using RCAAS.Core.Interfaces;


namespace RCAAS.Wrappers.Satisfactory
{
    public class SatisfactoryPluginHelperExt : BasePluginHelper
    {

        public override BaseArgs GetDefaultArgs()
        {
            var args = new SatisfactoryArgs();
            args.ServerQueryPort = 15777; // UDP
            args.BeaconPort = 15000;

            return args;

        }

        public async override Task<IAppWrapperConfig> GetDefaultCmdAppItemAsync()
        {
            var item = await base.GetDefaultCmdAppItemAsync();

            item.Name = "RCAAS Satisfactory server anno " + DateTime.Now.ToString("yyyy");
            item.WrapperName = "Satisfactory";
            item.Filename = "FactoryServer.exe";
            item.ExternalId = 1690800;
            item.Port = 15002;
            item.Port = await EthernetHelper.FindNextFreePortAsync(item.Port);

            return item;    

        }

    }

}
