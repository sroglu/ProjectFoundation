using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Cysharp.Text;
using PFound.GameDataStore.Entries;
using PFound.Utilities.DataType;
using PFound.Utilities.StringTools;
using PFound.Utilities.Pooling;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace PFound.GameDataStore.Storage
{
    // public enum ObsoleteOption
    // {
    // 	ObsoleteWarningOnRead = 0,
    // 	ObsoleteErrorOnRead = 1,
    // }

    public enum ReadOnlyWriteOption
    {
        FailIfReadOnly = 0,
        AllowWritingIfReadOnly = 1,
    }

    public enum ReadOnlyDeleteOption
    {
        FailIfReadOnly = 0,
        AllowDeletionIfReadOnly = 1,
    }

    [InlineProperty]
    public class LocalDataStore : IDisposable
    {
        #region Initialization / Deinitialization

        public static LocalDataStore Create(bool addConstants)
        {
            var LocalDataStore = new LocalDataStore();
            if (addConstants)
            {
                LocalDataStore.InitializeConstants();
            }

            return LocalDataStore;
        }

        private LocalDataStore()
        {
            InitializeInternalData();
            InitializeInstanceID();
        }

        public LocalDataStore Clone()
        {
            var clone = Create(false);
            CloneInternalDataInto(clone);
            return clone;
        }

        public void CopyAllVariablesInto(LocalDataStore other)
        {
            CloneOrUpdateInternalDataInto(other);
        }

        public void ClearData()
        {
            ClearInternalData();
        }

        public void Dispose()
        {
            DisposeInternalData();
        }

        #endregion

        #region Data - Constants

        // Used for adding LocalCharacterId as constant value in every LocalDataStore.
        public static Entry __LocalCharacterId;

        public static readonly HashSet<string> SuspiciousKeys =
            new()
            {
                "true",
                "false",
                "zero",
                "one",
                "half",

                "intminvalue",
                "intmaxvalue",
                "uintmaxvalue",
                "minvalue",
                "maxvalue",
                "intmin",
                "intmax",
                "uintmax",

                "nan",
                "floatnan",
                "doublenan",

                /*"character/localcharacter/id",
                "localcharacterid",
                "localcharacter",
                "characterid",
                "id",*/
            };

        private void InitializeConstants()
        {
            const bool markAsReadOnly = false;

            // Don't forget to apply any changes to all constant methods below and above here. See 11456924.
			// @formatter:off
			   Set("True"            , new Entry(true         ), markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
			   Set("False"           , new Entry(false        ), markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
			   Set("Zero"            , new Entry((int)0       ), markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
			   Set("One"             , new Entry((int)1       ), markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
			// Set("Half"            , new Entry((float)0.5f  ), markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
			// Set("IntMinValue"     , new Entry(int.MinValue ), markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
			// Set("IntMaxValue"     , new Entry(int.MaxValue ), markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
			// Set("UIntMaxValue"    , new Entry(uint.MaxValue), markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
			// Set("FloatNaN"        , new Entry(float.NaN    ), markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
			// Set("DoubleNaN"       , new Entry(double.NaN   ), markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
			// Set("LocalCharacterId", __LocalCharacterId      , markAsReadOnly, ReadOnlyWriteOption.FailIfReadOnly, null);
            // @formatter:on
        }

        public static bool IsConstant(ProcessedKey key)
        {
            // Don't forget to apply any changes to all constant methods below and above here. See 11456924.
			// @formatter:off
			   if (key.Key == "True"            ) return true;
			   if (key.Key == "False"           ) return true;
			   if (key.Key == "Zero"            ) return true;
			   if (key.Key == "One"             ) return true;
			// if (key.Key == "Half"            ) return true;
			// if (key.Key == "IntMinValue"     ) return true;
			// if (key.Key == "IntMaxValue"     ) return true;
			// if (key.Key == "UIntMaxValue"    ) return true;
			// if (key.Key == "FloatNaN"        ) return true;
			// if (key.Key == "DoubleNaN"       ) return true;
			// if (key.Key == "LocalCharacterId") return true;
            // @formatter:on

            return false;
        }

        public static EntryType GetConstantType(ProcessedKey key)
        {
            // Don't forget to apply any changes to all constant methods below and above here. See 11456924.
			// @formatter:off
			   if (key.Key == "True"            ) return EntryType.Bool;
			   if (key.Key == "False"           ) return EntryType.Bool;
			   if (key.Key == "Zero"            ) return EntryType.Int;
			   if (key.Key == "One"             ) return EntryType.Int;
			// if (key.Key == "Half"            ) return EntryType.Float;
			// if (key.Key == "IntMinValue"     ) return EntryType.Int;
			// if (key.Key == "IntMaxValue"     ) return EntryType.Int;
			// if (key.Key == "UIntMaxValue"    ) return EntryType.;
			// if (key.Key == "FloatNaN"        ) return EntryType.;
			// if (key.Key == "DoubleNaN"       ) return EntryType.;
			// if (key.Key == "LocalCharacterId") return EntryType.CharacterId;
            // @formatter:on

            return EntryType.Invalid;
        }

        #endregion

        #region Data

        [ShowInInspector]
        [DictionaryDrawerSettings(IsReadOnly = true, DisplayMode = DictionaryDisplayOptions.CollapsedFoldout)]
        [InlineProperty]
        private Dictionary<ProcessedKey, Entry> _LocalEntries;

        private HashSet<ProcessedKey> _ReadOnlyMarkedEntries;

        private void CloneInternalDataInto(LocalDataStore clone)
        {
            foreach (var entry in _LocalEntries)
            {
                clone._LocalEntries.Add(entry.Key, entry.Value);
            }

            foreach (var entry in _ReadOnlyMarkedEntries)
            {
                clone._ReadOnlyMarkedEntries.Add(entry);
            }
        }

        private void CloneOrUpdateInternalDataInto(LocalDataStore clone)
        {
            foreach (var entry in _LocalEntries)
            {
                clone._LocalEntries[entry.Key] = entry.Value;
            }

            foreach (var entry in _ReadOnlyMarkedEntries)
            {
                clone._ReadOnlyMarkedEntries.Add(entry);
            }
        }

        private void ClearInternalData()
        {
            if (_LocalEntries != null)
            {
                _LocalEntries.Clear();
            }

            if (_ReadOnlyMarkedEntries != null)
            {
                _ReadOnlyMarkedEntries.Clear();
            }
        }

        private void InitializeInternalData()
        {
            _LocalEntries = DictionaryPool<ProcessedKey, Entry>.Get();
            _ReadOnlyMarkedEntries = HashSetPool<ProcessedKey>.Get();

#if UNITY_EDITOR
            __EntryWriteStacks = DictionaryPool<ProcessedKey, CircularArray<UnityEngine.Object>>.Get();
#endif
        }

        private void DisposeInternalData()
        {
            if (_LocalEntries != null)
            {
                DictionaryPool<ProcessedKey, Entry>.Return(ref _LocalEntries);
            }

            if (_ReadOnlyMarkedEntries != null)
            {
                HashSetPool<ProcessedKey>.Release(ref _ReadOnlyMarkedEntries);
            }
#if UNITY_EDITOR
            if (__EntryWriteStacks != null)
            {
                DictionaryPool<ProcessedKey, CircularArray<UnityEngine.Object>>.Return(ref __EntryWriteStacks);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetEntry(ProcessedKey processedKey, out Entry entry)
        {
            return _LocalEntries.TryGetValue(processedKey, out entry);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Exists(ProcessedKey processedKey)
        {
            var dictionary = _LocalEntries;

            // A simple check such as this won't cut it. Because the entry might be added and removed, but the system
            // won't actually remove it. So we should also check if the entry's value was reset. See 11645124.
            // return dictionary.ContainsKey(processedKey);

            return dictionary.TryGetValue(processedKey, out var value) &&
                   value.Type != EntryType.Invalid;
        }

        public Entry GetEntryEnsured(ProcessedKey processedKey)
        {
            if (processedKey.IsNull)
            {
                throw new ArgumentNullException(
                    $"Tried to get entry in {nameof(LocalDataStore)} with an invalid key '{processedKey}'.");
            }

            if (_LocalEntries.TryGetValue(processedKey, out var entry))
            {
                return entry;
            }

            throw new KeyNotFoundException(
                $"Tried to get entry in {nameof(LocalDataStore)} but it does not have an entry with the key '{processedKey}'.");
        }

        public void Set(ProcessedKey processedKey, Entry entry, bool markAsReadOnly,
            ReadOnlyWriteOption readOnlyWriteOption, UnityEngine.Object source)
        {
            if (readOnlyWriteOption == ReadOnlyWriteOption.FailIfReadOnly)
            {
                // See if the existing value was marked as read-only.
                if (_ReadOnlyMarkedEntries.Contains(processedKey))
                {
                    // Get existing value for detailed logging.
                    if (_LocalEntries.TryGetValue(processedKey, out var existingValue))
                    {
                        throw new IOException(
                            $"Tried to write over a read-only key '{processedKey}' with value '{entry.ToStringWithType()}' where existing value is '{existingValue.ToStringWithType()}'.");
                    }
                    else
                    {
                        // If _ReadOnlyMarkedEntries contains the key, _LocalEntries should also contains the key.
                        throw new Exception(
                            $"Tried to write over a read-only key '{processedKey}' with value '{entry.ToStringWithType()}' where existing value is missing.");
                    }
                }
            }

            // Set the value.
            _LocalEntries[processedKey] = entry;

            // Mark the key as read-only. It's alright if it was already marked.
            if (markAsReadOnly)
            {
                _ReadOnlyMarkedEntries.Add(processedKey);
            }

#if UNITY_EDITOR
            PushWriteSourceData(processedKey, source);
#endif
        }

        private void _InternalDelete(ProcessedKey processedKey, ReadOnlyDeleteOption readOnlyDeleteOption)
        {
            // See if the data was marked as read-only.
            if (_ReadOnlyMarkedEntries.Contains(processedKey))
            {
                if (readOnlyDeleteOption == ReadOnlyDeleteOption.AllowDeletionIfReadOnly)
                {
                    _ReadOnlyMarkedEntries.Remove(processedKey);
                }
                else
                {
                    // Get existing value for detailed logging.
                    if (_LocalEntries.TryGetValue(processedKey, out var existingValue))
                    {
                        throw new IOException(
                            $"Tried to delete a read-only key '{processedKey}' with existing value '{existingValue.ToStringWithType()}'.");
                    }
                    else
                    {
                        // If _ReadOnlyMarkedEntries contains the key, _LocalEntries should also contains the key.
                        throw new Exception(
                            $"Tried to delete a read-only key '{processedKey}' where existing value is missing.");
                    }
                }
            }

            // Delete the value.
            _LocalEntries.Remove(processedKey);
        }

        public void DeleteIfExists(ProcessedKey processedKey, ReadOnlyDeleteOption readOnlyDeleteOption)
        {
            _InternalDelete(processedKey, readOnlyDeleteOption);
        }

        public void DeleteAllStartsWith(ProcessedKey processedKey, ReadOnlyDeleteOption readOnlyDeleteOption)
        {
            using var keysToDeleteScope = PooledList<ProcessedKey>.New();
            var keysToDelete = keysToDeleteScope.Value;
            foreach (var localEntriesKey in _LocalEntries.Keys)
            {
                if (localEntriesKey.Key.StartsWith(processedKey.Key, StringComparison.Ordinal))
                {
                    keysToDelete.Add(localEntriesKey);
                }
            }

            foreach (var key in keysToDelete)
            {
                _InternalDelete(key, readOnlyDeleteOption);
            }
        }

        #endregion

        #region Instance ID

        [ShowInInspector, ReadOnly] private int InstanceID;
        private static int LastGivenInstanceID = 100;

        private void InitializeInstanceID()
        {
            InstanceID = ++LastGivenInstanceID;
        }

        #endregion

        #region Log

        public void ToStringForDetailedLog(ref Utf16ValueStringBuilder stringBuilder)
        {
            stringBuilder.Append("[LocalDataStore-");
            stringBuilder.Append(InstanceID);
            stringBuilder.Append(", Data:{\n");
            if (_LocalEntries != null && _LocalEntries.Count > 0)
            {
                foreach (var entry in _LocalEntries)
                {
                    stringBuilder.Append(entry.Key.Key);
                    stringBuilder.Append("\t=");
                    entry.Value.ToStringWithType(ref stringBuilder);
                    if (_ReadOnlyMarkedEntries.Contains(entry.Key))
                    {
                        stringBuilder.Append(" (ReadOnly)");
                    }

                    stringBuilder.Append(",\n");
                }

                stringBuilder.Advance(-2); // Remove the ending ", " separator.
            }

            stringBuilder.Append("\n} ]");
        }

        #endregion

#if UNITY_EDITOR

        #region Inspector Style

        private static GUIStyle _localEntryDesign;

        private void InitializeGUIStylesIfRequired()
        {
            if (_localEntryDesign == null)
            {
                _localEntryDesign = new GUIStyle("AvatarMappingBox");
                _localEntryDesign.normal.textColor = Color.white;
                _localEntryDesign.alignment = TextAnchor.MiddleLeft;
            }
        }

        private Vector2 _sizeOfKeyText;
        private Vector2 _sizeOfValueText;
        private Vector2 _sizeOfValueTypeText;
        private int _LocalEntriesCount;

        private void CalculateGUILabelSizesIfRequired()
        {
            if (_LocalEntriesCount == _LocalEntries.Count)
                return;
            var longestKey = "";
            var longestValue = "";
            var longestValueType = "";

            foreach (var entry in _LocalEntries)
            {
                if (entry.Key.Key.Length > longestKey.Length)
                    longestKey = entry.Key.Key;
                if (entry.Value.ToString().Length > longestValue.Length)
                    longestValue = entry.Value.ToString();
                if (entry.Value.Type.ToString().Length > longestValueType.Length)
                    longestValueType = entry.Value.Type.ToString();
            }

            _sizeOfKeyText = _localEntryDesign.CalcSize(new GUIContent(longestKey));
            _sizeOfValueText = _localEntryDesign.CalcSize(new GUIContent(longestValue));
            _sizeOfValueTypeText = _localEntryDesign.CalcSize(new GUIContent(longestValueType));
            _sizeOfValueText.x = _sizeOfValueText.x > 500 ? _sizeOfValueText.x = 500 : _sizeOfValueText.x;
            _LocalEntriesCount = _LocalEntries.Count;
        }

        #endregion

        #region Inspector

        private static HashSet<ProcessedKey> __DetailVisibilities = new();


        public void _DrawInspector()
        {
            if (!Application.isPlaying)
                return;

            GUI.color = Color.white;

            InitializeGUIStylesIfRequired();
            CalculateGUILabelSizesIfRequired();
            GUILayout.BeginVertical();

            GUILayout.Space(16);

            GUILayout.Label($"    LocalDataStore entries ({_LocalEntries.Count})");
            GUILayout.Space(12);

            foreach (var entry in _LocalEntries)
            {
                var isReadOnly = _ReadOnlyMarkedEntries.Contains(entry.Key);
                var detailsVisible = __DetailVisibilities.Contains(entry.Key);

                if (detailsVisible)
                {
                    GUILayout.BeginVertical(Sirenix.Utilities.Editor.SirenixGUIStyles.MessageBox);
                }
                else
                {
                    GUILayout.BeginVertical();
                }

                GUILayout.BeginHorizontal();

                GUILayout.Label(entry.Value.Type.ToString(), _localEntryDesign,
                    GUILayout.Width(_sizeOfValueTypeText.x + 10));
                GUILayout.Space(5);
                GUILayout.TextArea(entry.Key.Key, _localEntryDesign, GUILayout.Width(_sizeOfKeyText.x + 10));
                GUILayout.Space(5);

                if (isReadOnly)
                {
                    GUILayout.TextArea(entry.Value.ToString(), _localEntryDesign, GUILayout.ExpandWidth(true),
                        GUILayout.MinWidth(_sizeOfValueText.x - 65));
                    GUILayout.Space(5);
                    GUILayout.Label("Read-Only", _localEntryDesign, GUILayout.Width(75));
                }
                else
                {
                    GUILayout.TextArea(entry.Value.ToString(), _localEntryDesign, GUILayout.ExpandWidth(true),
                        GUILayout.MinWidth(_sizeOfValueText.x + 10));
                }

                if (GUILayout.Button(
                        new GUIContent(detailsVisible ? "Hide History" : "Show History", "Show Details"),
                        GUILayout.Width(100)))
                {
                    if (__DetailVisibilities.Contains(entry.Key))
                        __DetailVisibilities.Remove(entry.Key);
                    else
                        __DetailVisibilities.Add(entry.Key);
                }

                GUILayout.EndHorizontal();

                if (detailsVisible)
                {
                    _DrawInspectorDetails(entry);
                }

                GUILayout.EndVertical();

                if (detailsVisible)
                {
                    GUILayout.Space(12);
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(12);

            // Reset color to white.
            GUI.contentColor = Color.white;
        }

        private void _DrawInspectorDetails(KeyValuePair<ProcessedKey, Entry> entry)
        {
            GUILayout.BeginVertical();

            if (__EntryWriteStacks.ContainsKey(entry.Key))
            {
                var stack = __EntryWriteStacks[entry.Key];

                GUILayout.Label($"Edit History ({stack.Count}):");

                foreach (var element in stack)
                {
                    if (element == null)
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        GUILayout.Button("NULL");
                        EditorGUI.EndDisabledGroup();
                    }
                    else if (GUILayout.Button(element.name))
                    {
                        // TODO: Go to context
                        UnityEditor.EditorGUIUtility.PingObject(element);
                    }
                }
            }
            else
            {
                GUILayout.Label("Edit History (0):");
            }

            GUILayout.EndVertical();
        }

        private Dictionary<ProcessedKey, CircularArray<UnityEngine.Object>> __EntryWriteStacks;

        public void PushWriteSourceData(ProcessedKey key, UnityEngine.Object source)
        {
            if (!__EntryWriteStacks.ContainsKey(key))
            {
                __EntryWriteStacks.Add(key, new(20));
            }

            __EntryWriteStacks[key].Append(source);
        }

        #endregion

#endif

        #region Text Format Using Multiple Output Results

        private const char KeyStartBrace = '{';
        private const char KeyEndBrace = '}';

        private static TagReplaceProcessor TagReplacer = new TagReplaceProcessor();

        public void FormatTextWithOutputResults(string format, ref Utf16ValueStringBuilder outputToStringBuilder)
        {
            var result = format.ProcessTags(KeyStartBrace, KeyEndBrace, TagReplacer.With(this),
                ref outputToStringBuilder);
            TagReplacer.Reset();
            switch (result)
            {
                case StringTools.TagProcessResult.Succeeded:
                case StringTools.TagProcessResult.SucceededButFoundNoTags:
                case StringTools.TagProcessResult.SucceededButEmptyInputText:
                {
                    return;
                }
                case StringTools.TagProcessResult.FailedDueToMismatchingTagBraces:
                case StringTools.TagProcessResult.FailedDueToNestedTagBraces:
                case StringTools.TagProcessResult.FailedDueToEmptyTagBraces:
                {
                    outputToStringBuilder.Clear();
                    throw new FormatException($"Mismatching '{KeyStartBrace}' and '{KeyEndBrace}' braces.");
                }
                default:
                {
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        private class TagReplaceProcessor : StringTools.ITagProcessor
        {
            private LocalDataStore _LocalDataStore;

            public void Reset()
            {
                _LocalDataStore = null;
            }

            public TagReplaceProcessor With(LocalDataStore LocalDataStore)
            {
                _LocalDataStore = LocalDataStore;
                return this;
            }

            public void AppendText(ref Utf16ValueStringBuilder stringBuilder, ReadOnlySpan<char> partOfText)
            {
                stringBuilder.Append(partOfText);
            }

            public void AppendTag(ref Utf16ValueStringBuilder stringBuilder, ReadOnlySpan<char> tag)
            {
                var tagAsString = tag.ToString();

                _LocalDataStore.GetEntryEnsured(tagAsString).ToString(ref stringBuilder);
            }
        }

        #endregion
    }

    public static class LocalDataStoreTools
    {
        public static string ToStringForDetailedLogSafe(this LocalDataStore LocalDataStore)
        {
            if (LocalDataStore == null)
                return "[NA]";

            var stringBuilder = ZString.CreateStringBuilder(true);
            LocalDataStore.ToStringForDetailedLog(ref stringBuilder);
            var result = stringBuilder.ToString();
            stringBuilder.Dispose();
            return result;
        }
    }
}