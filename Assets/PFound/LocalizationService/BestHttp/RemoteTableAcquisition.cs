#if PFOUND_BESTHTTP
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BestHTTP;

namespace PFound.LocalizationService.Unity
{
    /// <summary>
    /// Acquires a remote (CDN-hosted) localization table set over the project's standard HTTP transport
    /// (BestHTTP), landing the files in the writable persistent Localizables folder so
    /// <see cref="TableFileLoader.LoadFrom"/> can then read them. Lives in its own asmdef gated behind the
    /// <c>PFOUND_BESTHTTP</c> define so the module compiles without the library present.
    ///
    /// The update trigger stays a local-vs-remote hash comparison (<see cref="TableFileLoader.NeedsRefresh"/>):
    /// when the deployed hashes already match the remote manifest nothing is downloaded. On a refresh the
    /// writable directory is cleared first (so a new hash never coexists with the old set), then the content
    /// and definitions files are pulled by their hash-addressed remote paths. Path/hash construction lives in
    /// <see cref="TableFileLoader"/>; this type only owns the transfer.
    /// </summary>
    public sealed class RemoteTableAcquisition
    {
        private readonly string _baseUrl;

        /// <param name="baseUrl">CDN base URL that the hash-addressed remote paths are composed against.</param>
        public RemoteTableAcquisition(string baseUrl)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        }

        /// <summary>
        /// Ensures the writable table set matches <paramref name="remote"/>. Returns false (no transfer) when
        /// the local set is already current; true after downloading a fresh set. The remote hash pair is
        /// typically read from a manifest whose file names yield hashes via <see cref="TableFileLoader.GetHashFromUrl"/>.
        /// </summary>
        public async Task<bool> RefreshAsync(LocalizationHashData remote, CancellationToken cancellationToken = default)
        {
            var local = TableFileLoader.GetLocalFileHashes(TableFileLoader.PersistentTablesDir);
            if (!TableFileLoader.NeedsRefresh(local, remote))
                return false;

            TableFileLoader.ClearPersistentTablesDir();

            await DownloadAsync(
                TableFileLoader.RemoteContentPath(remote.ContentHash),
                TableFileLoader.PersistentContentFilePath(remote.ContentHash),
                cancellationToken);
            await DownloadAsync(
                TableFileLoader.RemoteDefinitionsPath(remote.DefinitionsHash),
                TableFileLoader.PersistentDefinitionsFilePath(remote.DefinitionsHash),
                cancellationToken);

            return true;
        }

        private async Task DownloadAsync(string relativePath, string destination, CancellationToken cancellationToken)
        {
            var request = new HTTPRequest(new Uri(CombineUrl(_baseUrl, relativePath)));
            var response = await request.GetHTTPResponseAsync(cancellationToken);

            if (!response.IsSuccess)
                throw new IOException(
                    "Failed to download localization table '" + relativePath + "': HTTP " + response.StatusCode + " " + response.Message + ".");

            File.WriteAllBytes(destination, response.Data);
        }

        private static string CombineUrl(string baseUrl, string relativePath)
            => baseUrl.TrimEnd('/') + "/" + relativePath.TrimStart('/');
    }
}
#endif
