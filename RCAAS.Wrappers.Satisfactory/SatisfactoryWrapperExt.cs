using Newtonsoft.Json;
using RCAAS.Core.Helpers;
using RCAAS.Core.Interfaces;
using RCAAS.Core.Wrappers.Steam;
using System.Text;


namespace RCAAS.Wrappers.Satisfactory;

/// <summary>
/// Wrapper for Satisfactory dedicated server with Steam integration.
/// </summary>
public class SatisfactoryWrapperExt : SteamWrapper
{
    private const int MaxRecursionDepth = 10;

    /// <summary>
    /// Gets the path to the Satisfactory save game directory.
    /// </summary>
    public string PathToSaveGame => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "FactoryGame", 
        "Saved", 
        "SaveGames");

    private DateTime LastChange { get; set; }

    /// <summary>
    /// Gets or sets the time provider for testability. Defaults to system time provider.
    /// </summary>
    protected TimeProvider TimeProvider { get; set; } = TimeProvider.System;


    /// <summary>
    /// Initializes a new instance of the <see cref="SatisfactoryWrapperExt"/> class.
    /// </summary>
    public SatisfactoryWrapperExt()
    {
        AnonymousLogin = true;
        LastChange = DateTime.MinValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SatisfactoryWrapperExt"/> class with a custom time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider to use for time-based operations.</param>
    public SatisfactoryWrapperExt(TimeProvider timeProvider) : this()
    {
        TimeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Gets or sets the Satisfactory-specific command line arguments.
    /// </summary>
    public SatisfactoryArgs SatisfactorySettings
    {
        get
        {
            if (Config?.CmdArgs is null)
            {
                return new SatisfactoryArgs();
            }

            return JsonConvert.DeserializeObject<SatisfactoryArgs>(Config.CmdArgs) ?? new SatisfactoryArgs();
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Config is not null)
            {
                Config.CmdArgs = value.ToString();
            }
        }
    }


    /// <summary>
    /// Creates the process arguments for Steam update with Satisfactory-specific beta channel.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The process arguments string.</returns>
    protected override async Task<string> CreateProcessArgsSteamUpdateAsync(CancellationToken cancellationToken = default)
    {
        var args = await base.CreateProcessArgsSteamUpdateAsync(cancellationToken).ConfigureAwait(false);
        return args.Replace(" validate ", " -beta public validate ", StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates the process arguments for launching the Satisfactory dedicated server.
    /// </summary>
    /// <returns>The process arguments string.</returns>
    protected override string CreateProcessArgs()
    {
        var settings = SatisfactorySettings;
        var savePath = PathToSaveGame;
        var serverName = Config?.Name ?? "Satisfactory Server";
        var port = Config?.Port ?? 15002;

        // Validate and sanitize the save path
        if (!IsValidPath(savePath))
        {
            throw new InvalidOperationException($"Invalid save game path: {savePath}");
        }

        var args = new StringBuilder(256);
        args.Append($" -ServerQueryPort={settings.ServerQueryPort}")
            .Append($" -BeaconPort={settings.BeaconPort}")
            .Append($" -Port={port}")
            .Append(" -NoSound")
            .Append(" -log")
            .Append(" -unattended")
            .Append($" -SaveDir=\"{savePath}\"")
            .Append($" -ServerName={serverName}");

        return args.ToString();
    }

    /// <summary>
    /// Validates that a path is safe and properly formatted.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <returns>True if the path is valid, false otherwise.</returns>
    private static bool IsValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            return !string.IsNullOrWhiteSpace(fullPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Stops the Satisfactory server. There is no graceful shutdown mechanism, so the process is terminated.
    /// </summary>
    /// <param name="forcestop">Indicates whether to force stop the server.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task StopAsync(bool forcestop = false)
    {
        if (App is null)
        {
            return;
        }

        var appId = App.Id;

        try
        {
            App.Kill(entireProcessTree: true);
            App.WaitForExit(WaitTime);
        }
        catch (InvalidOperationException ex)
        {
            MyLog.Error(ex, $"Process {appId} was already exited.");
        }

        if (!IsRunning)
        {
            await RegisterProcessStopAsync(appId, forcestop).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Installs the Satisfactory server and applies updates.
    /// </summary>
    /// <param name="item">The configuration for the server to install.</param>
    /// <returns>The updated configuration.</returns>
    public override async Task<IAppWrapperConfig> InstallItemAsync(IAppWrapperConfig item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item = await base.InstallItemAsync(item).ConfigureAwait(false);
        await base.ApplyUpdateAsync().ConfigureAwait(false);
        return await DBHelper.UpdateCmdAppsAsync(item).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs a backup of the Satisfactory save files if changes are detected.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task BackupAsync(CancellationToken cancellationToken = default)
    {
        var saveDir = Path.Combine(PathToSaveGame, "Server");

        if (!Directory.Exists(saveDir))
        {
            MyLog.Debug($"Save directory does not exist: {saveDir}");
            return;
        }

        try
        {
            var saveFiles = Directory.GetFiles(saveDir, "*.sav", SearchOption.TopDirectoryOnly);
            var now = TimeProvider.GetUtcNow().UtcDateTime;

            foreach (var file in saveFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(file);

                if (fileInfo.LastWriteTimeUtc > LastChange)
                {
                    LastChange = fileInfo.LastWriteTimeUtc;
                    HasChanged = true;
                    MyLog.Trace($"Detected change in last write to save file: {file}");
                }
                else if (fileInfo.CreationTimeUtc > LastChange)
                {
                    LastChange = fileInfo.CreationTimeUtc;
                    HasChanged = true;
                    MyLog.Trace($"Detected change in creation to save file: {file}");
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            MyLog.Error(ex, $"Access denied to save directory: {saveDir}");
            return;
        }
        catch (DirectoryNotFoundException ex)
        {
            MyLog.Error(ex, $"Save directory not found: {saveDir}");
            return;
        }

        await base.BackupAsync(cancellationToken).ConfigureAwait(false);
        HasChanged = false;
    }


    /// <summary>
    /// Backs up save files to a temporary folder.
    /// </summary>
    /// <param name="folder">The destination folder for the backup.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task BackupFilesToTempFolderAsync(string folder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        try
        {
            var sourceDir = PathToSaveGame;

            if (!Directory.Exists(sourceDir))
            {
                MyLog.Debug($"Source directory does not exist: {sourceDir}");
                return;
            }

            await Task.Run(() => CopyDirectoryRecursively(sourceDir, folder, "*.*", 0, cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            MyLog.Debug($"Backed up all files from '{sourceDir}' to '{folder}'.");
        }
        catch (OperationCanceledException)
        {
            MyLog.Debug("Backup operation was cancelled.");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            MyLog.Error(ex, "Access denied during backup of Satisfactory server.");
            throw;
        }
        catch (IOException ex)
        {
            MyLog.Error(ex, "I/O error during backup of Satisfactory server.");
            throw;
        }
        catch (Exception ex)
        {
            MyLog.Error(ex, "Failed to copy files for backup of Satisfactory server.");
            throw;
        }
    }


    /// <summary>
    /// Recursively copies a directory and its contents with depth protection.
    /// </summary>
    /// <param name="sourceDir">The source directory to copy from.</param>
    /// <param name="destDir">The destination directory to copy to.</param>
    /// <param name="searchPattern">The file search pattern to match.</param>
    /// <param name="currentDepth">The current recursion depth (for safety).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown when maximum recursion depth is exceeded.</exception>
    private void CopyDirectoryRecursively(
        string sourceDir, 
        string destDir, 
        string searchPattern, 
        int currentDepth = 0,
        CancellationToken cancellationToken = default)
    {
        if (currentDepth > MaxRecursionDepth)
        {
            throw new InvalidOperationException(
                $"Maximum recursion depth ({MaxRecursionDepth}) exceeded while copying directory: {sourceDir}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Ensure destination directory exists
        Directory.CreateDirectory(destDir);

        // Copy files matching the pattern
        var files = Directory.GetFiles(sourceDir, searchPattern, SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);

            File.Copy(file, destFile, overwrite: true);
            MyLog.Trace($"Backing up save file: {file}");
        }

        // Recursively copy subdirectories
        var subDirectories = Directory.GetDirectories(sourceDir);
        foreach (var subDir in subDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subDirName = Path.GetFileName(subDir);
            var destSubDir = Path.Combine(destDir, subDirName);

            CopyDirectoryRecursively(subDir, destSubDir, searchPattern, currentDepth + 1, cancellationToken);
        }
    }
}
