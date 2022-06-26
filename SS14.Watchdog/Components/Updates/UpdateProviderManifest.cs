﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using SS14.Watchdog.Configuration.Updates;
using SS14.Watchdog.Utility;

namespace SS14.Watchdog.Components.Updates
{
    public sealed class UpdateProviderManifest : UpdateProvider
    {
        private const int DownloadTimeoutSeconds = 60;
        
        private readonly HttpClient _httpClient = new();

        private readonly string _manifestUrl;
        private readonly ILogger<UpdateProviderManifest> _logger;

        public UpdateProviderManifest(
            UpdateProviderManifestConfiguration configuration,
            ILogger<UpdateProviderManifest> logger)
        {
            _logger = logger;
            _manifestUrl = configuration.ManifestUrl;
        }

        public override async Task<bool> CheckForUpdateAsync(
            string? currentVersion,
            CancellationToken cancel = default)
        {
            ManifestInfo? manifest;
            try
            {
                manifest = await _httpClient.GetFromJsonAsync<ManifestInfo>(_manifestUrl, cancel);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to fetch build manifest!");
                return false;
            }

            if (manifest == null)
            {
                _logger.LogError("Failed to fetch build manifest: JSON response was null!");
                return false;
            }

            return SelectMaxVersion(manifest) != currentVersion;
        }

        public override async Task<string?> RunUpdateAsync(
            string? currentVersion,
            string binPath,
            CancellationToken cancel = default)
        {
            ManifestInfo? manifest;
            try
            {
                manifest = await _httpClient.GetFromJsonAsync<ManifestInfo>(_manifestUrl, cancel);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to fetch build manifest!");
                return null;
            }

            if (manifest == null)
            {
                _logger.LogError("Failed to fetch build manifest: JSON response was null!");
                return null;
            }

            var maxVersion = SelectMaxVersion(manifest);
            if (maxVersion == null)
            {
                _logger.LogWarning("There are no versions, not updating");
                return null;
            }
            
            if (maxVersion == currentVersion)
            {
                _logger.LogDebug("Update not necessary!");
                return null;
            }

            var versionInfo = manifest.Builds[maxVersion];
            
            _logger.LogTrace("New version is {NewVersion} from {OldVersion}", maxVersion, currentVersion ?? "<none>");

            var rid = RidUtility.FindBestRid(versionInfo.Server.Keys);

            if (rid == null)
            {
                _logger.LogError("Unable to find compatible build for our platform!");
                return null;
            }

            var build = versionInfo.Server[rid];
            var downloadUrl = build.Url;
            var downloadHash = Convert.FromHexString(build.Sha256);
            
            // Create temporary file to download binary into (not doing this in memory).
            await using var tempFile = TempFile.CreateTempFile();

            _logger.LogTrace("Downloading server binary from {Download} to {TempFile}", downloadUrl, tempFile.Name);

            // Download to file...
            var timeout = Task.Delay(TimeSpan.FromSeconds(DownloadTimeoutSeconds), cancel);
            var downloadTask = Task.Run(async () =>
            {
                var resp = await _httpClient.GetAsync(downloadUrl, cancel);
                await resp.Content.CopyToAsync(tempFile, cancel);
            }, cancel);

            if (await Task.WhenAny(downloadTask, timeout) == timeout)
            {
                await timeout; // Throws cancellation if cancellation requested.
                _logger.LogError("Timeout while downloading: {Timeout} seconds", DownloadTimeoutSeconds);
                return null;
            }

            await downloadTask;

            // Verify hash because why not?
            using var hash = SHA256.Create();
            tempFile.Seek(0, SeekOrigin.Begin);
            var hashOutput = await hash.ComputeHashAsync(tempFile, cancel);

            if (!downloadHash.AsSpan().SequenceEqual(hashOutput))
            {
                _logger.LogError("Hash verification failed while updating!");
                return null;
            }
            
            _logger.LogTrace("Deleting old bin directory ({BinPath})", binPath);
            if (Directory.Exists(binPath))
            {
                Directory.Delete(binPath, true);
            }

            Directory.CreateDirectory(binPath);

            _logger.LogTrace("Extracting zip file");
            
            tempFile.Seek(0, SeekOrigin.Begin);
            DoBuildExtract(tempFile, binPath);

            return maxVersion;
        }

        private static string? SelectMaxVersion(ManifestInfo manifest)
        {
            if (manifest.Builds.Count == 0)
                return null;

            return manifest.Builds.Aggregate((a, b) => a.Value.Time > b.Value.Time ? a : b).Key;
        }

        private sealed record ManifestInfo
        {
            [UsedImplicitly]
            public ManifestInfo(Dictionary<string, VersionInfo> builds)
            {
                Builds = builds;
            }

            [JsonPropertyName("builds")] public Dictionary<string, VersionInfo> Builds { get; }
        }

        private sealed record VersionInfo
        {
            [JsonPropertyName("time")] public DateTimeOffset Time { get; }
            [JsonPropertyName("client")] public DownloadInfo Client { get; }
            [JsonPropertyName("server")] public Dictionary<string, DownloadInfo> Server { get; }

            [UsedImplicitly]
            public VersionInfo(DateTimeOffset time, DownloadInfo client, Dictionary<string, DownloadInfo> server)
            {
                Client = client;
                Server = server;
                Time = time;
            }
        }

        private sealed record DownloadInfo
        {
            [JsonPropertyName("url")] public string Url { get; }
            [JsonPropertyName("sha256")] public string Sha256 { get; }

            [UsedImplicitly]
            public DownloadInfo(string url, string sha256)
            {
                Url = url;
                Sha256 = sha256;
            }
        }
    }
}