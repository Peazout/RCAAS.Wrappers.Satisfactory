using Newtonsoft.Json;
using RCAAS.Core.Helpers;
using RCAAS.Core.Interfaces;
using RCAAS.Wrappers.Steam;
using System.Text;


namespace RCAAS.Wrappers.Satisfactory
{
    public class SatisfactoryWrapperExt : SteamWrapper
    {
        public string PathToSaveGame => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FactoryGame", "Saved", "SaveGames");
        private DateTime LastChange { get; set; }


        public SatisfactoryWrapperExt()
        {
            AnonymousLogin = true;
            LastChange = DateTime.MinValue;
        }

        public SatisfactoryArgs SatisfactorySettings
        {
            get
            {
                if ((Config == null) || (Config.CmdArgs == null)) return new SatisfactoryArgs();

                var args = JsonConvert.DeserializeObject<SatisfactoryArgs>(Config.CmdArgs);

                return args;
            }
            set
            {
                Config.CmdArgs = value.ToString();
            }

        }


        protected override string CreateProcessArgsSteamUpdate()
        {
            var args = base.CreateProcessArgsSteamUpdate();

            args = args.Replace(" validate ", " -beta public validate ");

            return args;

        }

        protected override string CreateProcessArgs()
        {

            var str = new StringBuilder();
            // str.Append($" -multihome=<ip address>"); // Bind the server process to a specific IP address rather than all available interfaces 
            str.Append($" -ServerQueryPort={SatisfactorySettings.ServerQueryPort} "); // Override the Query Port the server uses. This is the port specified in the Server Manager in the client UI to establish a server connection. 
            str.Append($" -BeaconPort={SatisfactorySettings.BeaconPort} "); // this port can be set freely. The default port is UDP/15000. If this port is already in use, the server will step up to the next port until an available one is found. 
            str.Append($" -Port={Config.Port} ");
            str.Append(" -NoSound "); // Disables sound output.
            str.Append(" -log "); // Forces the server to display logs in a window (on Windows) or in the active terminal (on Linux). This option is implicit by default when launching on Linux. 
            str.Append(" -unattended "); // Makes it such that the Dedicated Server will not present any dialogs which might otherwise interrupt the server from running if not attended to. This option is implicit by default when launching on Linux, 
            str.Append($" -SaveDir=\"{PathToSaveGame}\" "); // Specifies the directory where the server will save game data.
            str.Append($" -ServerName ={Config.Name} "); // Specifies the name of the server as it will appear in the server browser.
            // -NoSteamClient // if not using Steam.

            return str.ToString();

        }

        /// <summary>
        /// Stop the server, if not force the use the /stop command.
        /// </summary>
        /// <param name="forcestop"></param>
        /// <returns></returns>
        public override async Task StopAsync(bool forcestop = false)
        {
            if (App == null) return;
            var appid = App.Id;
            // There is no graceful way to stop the server other than killing the process.
            App.Kill(entireProcessTree: true);
            App.WaitForExit(WaitTime);

            if (!IsRunning) await RegisterProcessStopAsync(appid, forcestop);

        }

        public override async Task<IAppWrapperConfig> InstallItemAsync(IAppWrapperConfig item)
        {
            item = await base.InstallItemAsync(item);

            await base.ApplyUpdateAsync();

            return await DBHelper.UpdateCmdAppsAsync(item);

        }

        public override void Backup()
        {
            var savedir = Path.Combine(PathToSaveGame, "Server");
            var saves = Directory.GetFiles(savedir, "*.sav", SearchOption.TopDirectoryOnly);
            foreach (var file in saves)
            {
                var fi = new FileInfo(file);
                if (fi.LastWriteTime > LastChange)
                {
                    LastChange = fi.LastWriteTime;
                    HasChanged = true;
                    MyLog.Trace("Detected change in lastwrite to save file: " + file);
                }
                else if (fi.CreationTime > LastChange)
                {
                    LastChange = fi.CreationTime;
                    HasChanged = true;
                    MyLog.Trace("Detected change in creation to save file: " + file);
                }

            }

            base.Backup();
            HasChanged = false;

        }

        protected override void BackupFilesToTempFolder(string folder)
        {
            try
            {
                var sourceDir = PathToSaveGame;
                CopyDirectoryRecursively(sourceDir, folder, "*.*");
                MyLog.Debug($"Backed up all .sav files from '{sourceDir}' to '{folder}'.");
            }
            catch (Exception ex)
            {
                MyLog.Error(ex, "Failed to copy files for backup of Satisfactory server.");
            }

        }


        private void CopyDirectoryRecursively(string sourceDir, string destDir, string searchPattern)
        {
            // Ensure destination directory exists
            Directory.CreateDirectory(destDir);

            // Copy files matching the pattern
            foreach (var file in Directory.GetFiles(sourceDir, searchPattern, SearchOption.TopDirectoryOnly))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
                MyLog.Trace("Backing up save file: " + file);
            }

            // Recursively copy subdirectories
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var subDirName = Path.GetFileName(subDir);
                var destSubDir = Path.Combine(destDir, subDirName);
                CopyDirectoryRecursively(subDir, destSubDir, searchPattern);
            }

        }

    }

}
