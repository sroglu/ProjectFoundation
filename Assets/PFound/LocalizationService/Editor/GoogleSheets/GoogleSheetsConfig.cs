#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using PFound.LocalizationService;
using PFound.LocalizationService.EditorTools;
using PFound.Utilities.EditorHelpers;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace PFound.LocalizationService.Editor
{
    public enum LocalizationSourceType
    {
        LocalCsv,
        GoogleSheets
    }

    /// <summary>
    /// Configuration + import driver for localization data. Inherits from
    /// <see cref="ImportableAsset"/> so the Inspector exposes a one-click Import
    /// button — no separate Editor window needed.
    ///
    /// The CSV → table conversion itself is delegated to the shared PFound
    /// pipeline (<see cref="LocalizationTableBuilder"/> +
    /// <see cref="CsvTableConverter"/>); this asset only chooses the source
    /// (a local CSV asset or a shared Google Sheet), fetches the text, and
    /// hands it off.
    ///
    /// Create via Assets &gt; Create &gt; Localization &gt; Source Config.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationSourceConfig", menuName = "Localization/Source Config")]
    public class GoogleSheetsConfig : ImportableAsset
    {
        [Header("Source")]
        [Tooltip("Select where to import localization data from.")]
        public LocalizationSourceType SourceType = LocalizationSourceType.LocalCsv;

        [Header("Local CSV")]
        [Tooltip("Translations CSV: Key, ParameterDefinition, en-US, tr-TR, ...")]
        public UnityEngine.Object TranslationsCsv;

        [Tooltip("Parameters CSV (optional): Key, ParameterDefinition — for dynamic keys and extra definitions.")]
        public UnityEngine.Object ParametersCsv;

        [Header("Google Sheets")]
        [Tooltip("Sheet URL — full edit link or just the base /spreadsheets/d/<ID> form both work.\nSheet must be shared: Share > Anyone with the link.\nTranslations are read from the FIRST tab in the sheet (place your consolidation tab there).")]
        [TextArea(2, 3)]
        public string GoogleSheetsUrl;

        [Tooltip("Name of the parameters sheet tab (optional). If set, dynamic key definitions are imported from this tab.")]
        public string ParametersSheetName;

        [Header("Output")]
        [Tooltip("Directory where the localization table files will be generated.")]
        public string OutputDirectory = "Assets/StreamingAssets/Localizables";

        public bool IsConfigured
        {
            get
            {
                return SourceType switch
                {
                    LocalizationSourceType.LocalCsv => TranslationsCsv != null,
                    LocalizationSourceType.GoogleSheets => !string.IsNullOrWhiteSpace(GoogleSheetsUrl),
                    _ => false
                };
            }
        }

        public override bool CanImport => IsConfigured;

        public override string CannotImportReason => SourceType == LocalizationSourceType.GoogleSheets
            ? "Set the Google Sheets URL above. Sheet must be shared: Share > Anyone with the link."
            : "Drag a CSV file into the Translations CSV field above.";

        public string GetAssetPath(UnityEngine.Object asset)
        {
            if (asset == null) return null;
            return AssetDatabase.GetAssetPath(asset);
        }

        // ------------------------------------------------------------------ //
        //  Import                                                              //
        // ------------------------------------------------------------------ //

        public override void Import()
        {
            if (SourceType == LocalizationSourceType.GoogleSheets)
                ImportFromGoogleSheets();
            else
                ImportFromLocalCsv();
        }

        void ImportFromLocalCsv()
        {
            var translationsPath = GetAssetPath(TranslationsCsv);
            if (!File.Exists(translationsPath))
            {
                Debug.LogError($"[Localization] Translations CSV not found: {translationsPath}");
                return;
            }

            try
            {
                var translationsCsv = File.ReadAllText(translationsPath, Encoding.UTF8);

                string parametersCsv = null;
                if (ParametersCsv != null)
                {
                    var paramsPath = GetAssetPath(ParametersCsv);
                    if (File.Exists(paramsPath))
                        parametersCsv = File.ReadAllText(paramsPath, Encoding.UTF8);
                }

                ConvertAndFinish(translationsCsv, parametersCsv);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void ImportFromGoogleSheets()
        {
            // No sheet name passed — Google Sheets returns the FIRST tab,
            // which by convention is the consolidation / translations tab.
            Debug.Log("[Localization] Downloading translations (first tab)...");

            var url = ConvertToCsvExportUrl(GoogleSheetsUrl);
            var req = UnityWebRequest.Get(url);
            var op = req.SendWebRequest();

            void Poll()
            {
                if (!op.isDone) return;
                EditorApplication.update -= Poll;

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Localization] Download failed: {req.error}\nEnsure the sheet is shared: Share > Anyone with the link.");
                    req.Dispose();
                    return;
                }

                var translationsCsv = req.downloadHandler.text;
                req.Dispose();

                if (!string.IsNullOrWhiteSpace(ParametersSheetName))
                    DownloadParametersThenConvert(translationsCsv);
                else
                    ConvertAndFinish(translationsCsv, null);
            }

            EditorApplication.update += Poll;
        }

        void DownloadParametersThenConvert(string translationsCsv)
        {
            Debug.Log("[Localization] Downloading parameters...");

            var url = ConvertToCsvExportUrl(GoogleSheetsUrl, ParametersSheetName);
            var req = UnityWebRequest.Get(url);
            var op = req.SendWebRequest();

            void Poll()
            {
                if (!op.isDone) return;
                EditorApplication.update -= Poll;

                string parametersCsv = null;
                if (req.result == UnityWebRequest.Result.Success)
                    parametersCsv = req.downloadHandler.text;
                else
                    Debug.LogWarning($"[Localization] Parameters sheet download failed: {req.error}. Continuing without it.");

                req.Dispose();
                ConvertAndFinish(translationsCsv, parametersCsv);
            }

            EditorApplication.update += Poll;
        }

        void ConvertAndFinish(string translationsCsv, string parametersCsv)
        {
            try
            {
                // Reuse the shared PFound pipeline: the RFC-4180 reader + table builder
                // parse and route the CSV, and CsvTableConverter writes the plain + LZMA
                // table pair. The optional parameters sheet contributes definition-only
                // rows (Key, ParameterDefinition, no language columns), so they are merged
                // into the same CSV the builder consumes.
                var combined = MergeParameters(translationsCsv, parametersCsv);
                var enums = LocalizableEnumScanner.Scan();
                var result = LocalizationTableBuilder.Build(combined, enums);
                CsvTableConverter.WriteFiles(OutputDirectory, result);
                Finish();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Appends the data rows of the optional parameters CSV onto the translations
        /// CSV so the shared builder registers their definitions. The parameters CSV
        /// carries only <c>Key, ParameterDefinition</c> columns (no language cells), so
        /// its header is skipped and its rows add definitions without content.
        /// </summary>
        static string MergeParameters(string translationsCsv, string parametersCsv)
        {
            if (string.IsNullOrWhiteSpace(parametersCsv)) return translationsCsv;

            int firstBreak = parametersCsv.IndexOf('\n');
            string paramRows = firstBreak >= 0 ? parametersCsv.Substring(firstBreak + 1) : string.Empty;
            if (paramRows.Trim().Length == 0) return translationsCsv;

            var sb = new StringBuilder(translationsCsv);
            if (translationsCsv.Length > 0 && translationsCsv[translationsCsv.Length - 1] != '\n')
                sb.Append('\n');
            sb.Append(paramRows);
            return sb.ToString();
        }

        void Finish()
        {
            AssetDatabase.Refresh();
            var contentPath = Path.Combine(
                OutputDirectory,
                LocalizationConstants.ContentFilePrefix + LocalizationConstants.PlainExtension);
            Debug.Log($"[Localization] Import successful → {contentPath}");

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(contentPath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }

        // ------------------------------------------------------------------ //
        //  URL helpers                                                         //
        // ------------------------------------------------------------------ //

        internal static string ConvertToCsvExportUrl(string url, string sheetName = null)
        {
            url = url.Trim();

            if (url.Contains("tqx=out:csv") || url.Contains("output=csv") || url.Contains("format=csv"))
                return url;

            const string marker = "/spreadsheets/d/";
            var idx = url.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var start = idx + marker.Length;
                var end = url.IndexOf('/', start);
                var sheetId = end > start ? url.Substring(start, end - start) : url.Substring(start);

                var qIdx = sheetId.IndexOf('?');
                if (qIdx >= 0) sheetId = sheetId.Substring(0, qIdx);

                var gidParam = "";
                var gidIdx = url.IndexOf("gid=", StringComparison.Ordinal);
                if (gidIdx >= 0)
                {
                    var gidStart = gidIdx + 4;
                    var gidEnd = url.IndexOfAny(new[] { '&', '#' }, gidStart);
                    var gid = gidEnd > gidStart ? url.Substring(gidStart, gidEnd - gidStart) : url.Substring(gidStart);
                    gidParam = $"&gid={gid}";
                }

                var sheetParam = !string.IsNullOrEmpty(sheetName) ? $"&sheet={UnityWebRequest.EscapeURL(sheetName)}" : "";
                return $"https://docs.google.com/spreadsheets/d/{sheetId}/gviz/tq?tqx=out:csv{gidParam}{sheetParam}";
            }

            return url;
        }
    }
}
#endif
