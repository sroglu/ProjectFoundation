using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PFound.Render.UIShapes.Editor
{
    /// <summary>
    /// Bake EditorWindow at <c>Window → Render → UIShapes → Bake Tool</c>.
    /// Configure <c>UIShape</c> material parameters with a live preview, then save the
    /// result to <c>Assets/GameSpecific/Render/UIShapes/Baked/&lt;FileName&gt;.png</c>.
    /// </summary>
    /// <remarks>
    /// Bake settings persist across editor sessions via EditorPrefs. Material parameters
    /// reset each session — the developer typically tweaks for one bake at a time.
    /// </remarks>
    public sealed class UIShapeBakeWindow : EditorWindow
    {
        private const string MenuPath = "Window/Render/UIShapes/Bake Tool";
        private const string BakedRootFolder = "Assets/GameSpecific/Render/UIShapes/Baked";
        private const string PrefsKey = "PFound.Render.UIShapes.BakeSettings";
        private const string ShaderName = "Render/UI/Shape";
        private const int PreviewMaxSide = 384;
        private const int OneShotWarnThreshold = 4096;

        [SerializeField] private BakeSettings _settings = BakeSettings.Default;

        private Material _previewMaterial;
        private Texture2D _livePreview;
        private bool _statusVisible;
        private string _statusText;
        private double _statusUntilTime;
        private bool _oversizeWarningShown;
        private Vector2 _scroll;
        private bool _shapeOpen = true;
        private bool _fillOpen = true;
        private bool _bakeOpen = true;
        private bool _gradientOpen = false;
        private bool _outlineOpen = false;
        private bool _bandingOpen = false;
        private bool _noiseOpen = false;
        private bool _dotsOpen = false;
        private bool _shadowOpen = false;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var w = GetWindow<UIShapeBakeWindow>();
            w.titleContent = new GUIContent("UIShape Bake Tool");
            w.minSize = new Vector2(420f, 580f);
            w.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
            EnsurePreviewMaterial();
            RegeneratePreview();
        }

        private void OnDisable()
        {
            SaveSettings();
            DisposeLivePreview();
            if (_previewMaterial != null)
            {
                DestroyImmediate(_previewMaterial);
                _previewMaterial = null;
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawPreview();
            EditorGUILayout.Space(6);

            EditorGUI.BeginChangeCheck();

            DrawShapeGroup();
            EditorGUILayout.Space(4);
            DrawFillGroup();
            EditorGUILayout.Space(8);

            DrawEffectGroup("Gradient", ref _gradientOpen, UIShapeMaterialProperties.GradientEnable,
                UIShapeShaderKeywords.EffectGradientOn, DrawGradientFields);
            DrawEffectGroup("Outline", ref _outlineOpen, UIShapeMaterialProperties.OutlineEnable,
                UIShapeShaderKeywords.EffectOutlineOn, DrawOutlineFields);
            DrawEffectGroup("Banding", ref _bandingOpen, UIShapeMaterialProperties.BandingEnable,
                UIShapeShaderKeywords.EffectBandingOn, DrawBandingFields);
            DrawEffectGroup("Noise", ref _noiseOpen, UIShapeMaterialProperties.NoiseEnable,
                UIShapeShaderKeywords.EffectNoiseOn, DrawNoiseFields);
            DrawEffectGroup("Dots", ref _dotsOpen, UIShapeMaterialProperties.DotsEnable,
                UIShapeShaderKeywords.EffectDotsOn, DrawDotsFields);
            DrawEffectGroup("Shadow", ref _shadowOpen, UIShapeMaterialProperties.ShadowEnable,
                UIShapeShaderKeywords.EffectShadowOn, DrawShadowFields);
            EditorGUILayout.Space(8);

            DrawBakeSettingsGroup();
            EditorGUILayout.Space(6);

            if (EditorGUI.EndChangeCheck())
            {
                RegeneratePreview();
            }

            DrawButtons();
            DrawStatusFooter();

            EditorGUILayout.EndScrollView();
        }

        // ─── Preview ───────────────────────────────────────────────────

        private void DrawPreview()
        {
            float availableWidth = position.width - 32f;
            int side = (int)Mathf.Min(PreviewMaxSide, availableWidth);
            var rect = GUILayoutUtility.GetRect(side, side, GUILayout.ExpandWidth(false));
            rect.x = (position.width - side) * 0.5f;

            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f, 1f));
            if (_livePreview != null)
            {
                GUI.DrawTexture(rect, _livePreview, ScaleMode.ScaleToFit, alphaBlend: true);
            }
            EditorGUI.LabelField(new Rect(rect.x, rect.yMax + 2f, side, 14f),
                "Live preview (" + _settings.Width + "×" + _settings.Height + ")",
                EditorStyles.miniLabel);
            GUILayout.Space(18f);
        }

        // ─── Shape group ───────────────────────────────────────────────

        private void DrawShapeGroup()
        {
            _shapeOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_shapeOpen, "Shape");
            if (_shapeOpen && _previewMaterial != null)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var currentType = (ShapeType)Mathf.Clamp(Mathf.RoundToInt(_previewMaterial.GetFloat(UIShapeMaterialProperties.ShapeType)), 0, 3);
                    var nextType = (ShapeType)EditorGUILayout.EnumPopup("Shape Type", currentType);
                    if (nextType != currentType)
                    {
                        _previewMaterial.SetFloat(UIShapeMaterialProperties.ShapeType, (int)nextType);
                        UIShapeMaterialInspector.SyncShapeTypeKeyword(_previewMaterial, nextType);
                    }

                    Vector4 radii = _previewMaterial.GetVector(UIShapeMaterialProperties.CornerRadii);
                    Vector4 nextRadii = EditorGUILayout.Vector4Field("Corner Radii (TL,TR,BR,BL)", radii);
                    if (nextRadii != radii)
                    {
                        _previewMaterial.SetVector(UIShapeMaterialProperties.CornerRadii, nextRadii);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ─── Fill group ────────────────────────────────────────────────

        private void DrawFillGroup()
        {
            _fillOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_fillOpen, "Fill");
            if (_fillOpen && _previewMaterial != null)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    Color current = _previewMaterial.GetColor(UIShapeMaterialProperties.FillColor);
                    Color next = EditorGUILayout.ColorField("Fill Color", current);
                    if (next != current)
                    {
                        _previewMaterial.SetColor(UIShapeMaterialProperties.FillColor, next);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ─── Effect groups (live preview) ──────────────────────────────

        private void DrawEffectGroup(string title, ref bool openFlag, string enableProp, string keyword, System.Action drawFields)
        {
            openFlag = EditorGUILayout.BeginFoldoutHeaderGroup(openFlag, title);
            if (openFlag && _previewMaterial != null)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    bool current = _previewMaterial.GetFloat(enableProp) > 0.5f;
                    bool next = EditorGUILayout.Toggle("Enable", current);
                    if (next != current)
                    {
                        _previewMaterial.SetFloat(enableProp, next ? 1f : 0f);
                        if (next) _previewMaterial.EnableKeyword(keyword);
                        else _previewMaterial.DisableKeyword(keyword);
                    }
                    if (next) drawFields();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawGradientFields()
        {
            int modeCurr = Mathf.RoundToInt(_previewMaterial.GetFloat(UIShapeMaterialProperties.GradientMode));
            GradientMode mode = (GradientMode)EditorGUILayout.EnumPopup("Mode", (GradientMode)modeCurr);
            if ((int)mode != modeCurr)
            {
                _previewMaterial.SetFloat(UIShapeMaterialProperties.GradientMode, (int)mode);
                if (mode == GradientMode.Radial) _previewMaterial.EnableKeyword(UIShapeShaderKeywords.GradientModeRadial);
                else _previewMaterial.DisableKeyword(UIShapeShaderKeywords.GradientModeRadial);
            }
            FloatField(UIShapeMaterialProperties.GradientAngle, "Angle");
            FloatField(UIShapeMaterialProperties.GradientFalloff, "Falloff");
            ColorField(UIShapeMaterialProperties.GradientColorA, "Color A");
            ColorField(UIShapeMaterialProperties.GradientColorB, "Color B");
        }

        private void DrawOutlineFields()
        {
            FloatField(UIShapeMaterialProperties.OutlineThickness, "Thickness");
            ColorField(UIShapeMaterialProperties.OutlineColor, "Color");
        }

        private void DrawBandingFields()
        {
            FloatField(UIShapeMaterialProperties.BandingSpacing, "Spacing");
            ColorField(UIShapeMaterialProperties.BandingColorA, "Color A");
            ColorField(UIShapeMaterialProperties.BandingColorB, "Color B");
        }

        private void DrawNoiseFields()
        {
            int modeCurr = Mathf.RoundToInt(_previewMaterial.GetFloat(UIShapeMaterialProperties.NoiseMode));
            NoiseMode mode = (NoiseMode)EditorGUILayout.EnumPopup("Mode", (NoiseMode)modeCurr);
            if ((int)mode != modeCurr)
            {
                _previewMaterial.SetFloat(UIShapeMaterialProperties.NoiseMode, (int)mode);
                if (mode == NoiseMode.Worley) _previewMaterial.EnableKeyword(UIShapeShaderKeywords.NoiseModeWorley);
                else _previewMaterial.DisableKeyword(UIShapeShaderKeywords.NoiseModeWorley);
            }
            FloatField(UIShapeMaterialProperties.NoiseScale, "Scale");
            float amp = _previewMaterial.GetFloat(UIShapeMaterialProperties.NoiseAmplitude);
            float nextAmp = EditorGUILayout.Slider("Amplitude", amp, 0f, 1f);
            if (nextAmp != amp) _previewMaterial.SetFloat(UIShapeMaterialProperties.NoiseAmplitude, nextAmp);
            ColorField(UIShapeMaterialProperties.NoiseColor, "Color");
        }

        private void DrawDotsFields()
        {
            FloatField(UIShapeMaterialProperties.DotsRadius, "Radius");
            FloatField(UIShapeMaterialProperties.DotsSpacing, "Spacing");
            ColorField(UIShapeMaterialProperties.DotsColor, "Color");
        }

        private void DrawShadowFields()
        {
            Vector4 off = _previewMaterial.GetVector(UIShapeMaterialProperties.ShadowOffset);
            Vector2 nextOff = EditorGUILayout.Vector2Field("Offset (X,Y)", new Vector2(off.x, off.y));
            if (nextOff.x != off.x || nextOff.y != off.y)
            {
                _previewMaterial.SetVector(UIShapeMaterialProperties.ShadowOffset, new Vector4(nextOff.x, nextOff.y, 0f, 0f));
            }
            FloatField(UIShapeMaterialProperties.ShadowBlur, "Blur");
            ColorField(UIShapeMaterialProperties.ShadowColor, "Color");
        }

        private void FloatField(string prop, string label)
        {
            float current = _previewMaterial.GetFloat(prop);
            float next = EditorGUILayout.FloatField(label, current);
            if (next != current) _previewMaterial.SetFloat(prop, next);
        }

        private void ColorField(string prop, string label)
        {
            Color current = _previewMaterial.GetColor(prop);
            Color next = EditorGUILayout.ColorField(label, current);
            if (next != current) _previewMaterial.SetColor(prop, next);
        }

        // ─── Bake settings group ───────────────────────────────────────

        private void DrawBakeSettingsGroup()
        {
            _bakeOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_bakeOpen, "Bake Settings");
            if (_bakeOpen)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    if (_settings.Width > OneShotWarnThreshold || _settings.Height > OneShotWarnThreshold)
                    {
                        EditorGUILayout.HelpBox(
                            "Output resolution above " + OneShotWarnThreshold + "×" + OneShotWarnThreshold +
                            " is memory-intensive. Consider smaller bakes for most UI use cases.",
                            MessageType.Warning);
                        _oversizeWarningShown = true;
                    }

                    int w = Mathf.Clamp(EditorGUILayout.IntField("Width", _settings.Width), 16, 8192);
                    int h = Mathf.Clamp(EditorGUILayout.IntField("Height", _settings.Height), 16, 8192);
                    bool link = EditorGUILayout.Toggle("Link Aspect", _settings.LinkAspect);

                    if (link)
                    {
                        if (w != _settings.Width) h = w;
                        else if (h != _settings.Height) w = h;
                    }
                    _settings.Width = w;
                    _settings.Height = h;
                    _settings.LinkAspect = link;

                    _settings.ColorSpace = (BakeColorSpace)EditorGUILayout.EnumPopup("Color Space", _settings.ColorSpace);
                    _settings.FilterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter", _settings.FilterMode);
                    _settings.WrapMode = (TextureWrapMode)EditorGUILayout.EnumPopup("Wrap", _settings.WrapMode);
                    _settings.TargetType = (BakeTargetType)EditorGUILayout.EnumPopup("Target Type", _settings.TargetType);
                    using (new EditorGUI.DisabledScope(_settings.TargetType != BakeTargetType.Sprite))
                    {
                        _settings.GenerateSpriteMesh = EditorGUILayout.Toggle("Generate Sprite Mesh", _settings.GenerateSpriteMesh);
                    }

                    _settings.FileName = EditorGUILayout.TextField("File Name", _settings.FileName ?? string.Empty);
                    EditorGUILayout.LabelField("Path", BakedRootFolder + "/" + (_settings.FileName ?? string.Empty) + ".png", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ─── Buttons ───────────────────────────────────────────────────

        private void DrawButtons()
        {
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save", GUILayout.Height(28f)))
                {
                    OnSaveClicked();
                }
                if (GUILayout.Button("Reset", GUILayout.Height(28f), GUILayout.Width(96f)))
                {
                    OnResetClicked();
                }
            }
        }

        private void DrawStatusFooter()
        {
            if (!_statusVisible) return;
            if (EditorApplication.timeSinceStartup > _statusUntilTime)
            {
                _statusVisible = false;
                _statusText = string.Empty;
                return;
            }
            EditorGUILayout.HelpBox(_statusText, MessageType.Info);
        }

        // ─── Save flow ─────────────────────────────────────────────────

        private void OnSaveClicked()
        {
            if (!UIShapeFilenameValidator.TryValidate(_settings.FileName, out string sanitized, out string warning))
            {
                EditorUtility.DisplayDialog("UIShape Bake", "Filename required (non-empty, no leading '.').", "OK");
                return;
            }
            if (sanitized != _settings.FileName)
            {
                bool accept = EditorUtility.DisplayDialog(
                    "UIShape Bake",
                    "Filename was sanitized.\n\nOriginal: " + _settings.FileName + "\nSanitized: " + sanitized +
                    (string.IsNullOrEmpty(warning) ? string.Empty : "\n\n" + warning),
                    "Use Sanitized",
                    "Cancel");
                if (!accept) return;
                _settings.FileName = sanitized;
            }

            string assetPath = BakedRootFolder + "/" + _settings.FileName + ".png";
            if (File.Exists(assetPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "UIShape Bake",
                    "File already exists at:\n" + assetPath + "\n\nOverwrite?",
                    "Overwrite",
                    "Cancel");
                if (!overwrite) return;
            }

            try
            {
                Directory.CreateDirectory(BakedRootFolder);
                var baked = UIShapeBakeService.Bake(_previewMaterial, _settings);
                if (!UIShapeBakeService.Save(baked, assetPath, _settings))
                {
                    Debug.LogError("[UIShapes] Bake save failed for " + assetPath);
                    DestroyImmediate(baked);
                    return;
                }
                DestroyImmediate(baked);

                var importedSprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (importedSprite != null)
                {
                    EditorGUIUtility.PingObject(importedSprite);
                }
                ShowStatus("Saved " + assetPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[UIShapes] Bake save failed: " + ex.Message);
            }
        }

        private void OnResetClicked()
        {
            _settings = BakeSettings.Default;
            DisposeLivePreview();
            if (_previewMaterial != null) DestroyImmediate(_previewMaterial);
            _previewMaterial = null;
            EnsurePreviewMaterial();
            RegeneratePreview();
        }

        // ─── Live preview ──────────────────────────────────────────────

        private void EnsurePreviewMaterial()
        {
            if (_previewMaterial != null) return;
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[UIShapes] Shader '" + ShaderName + "' not found — Phase 12 setup incomplete.");
                return;
            }
            _previewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "UIShapeBakeWindow_Preview",
            };
            _previewMaterial.SetFloat(UIShapeMaterialProperties.ShapeType, (int)ShapeType.RoundedRect);
            _previewMaterial.SetVector(UIShapeMaterialProperties.CornerRadii, new Vector4(16f, 16f, 16f, 16f));
            _previewMaterial.SetColor(UIShapeMaterialProperties.FillColor, Color.white);
        }

        private void RegeneratePreview()
        {
            if (_previewMaterial == null) return;
            DisposeLivePreview();
            _livePreview = UIShapeBakeService.Bake(_previewMaterial, _settings);
            _livePreview.hideFlags = HideFlags.HideAndDontSave;
            Repaint();
        }

        private void DisposeLivePreview()
        {
            if (_livePreview != null)
            {
                DestroyImmediate(_livePreview);
                _livePreview = null;
            }
        }

        // ─── Settings persistence + status ─────────────────────────────

        private void LoadSettings()
        {
            string json = EditorPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { _settings = JsonUtility.FromJson<BakeSettings>(json); }
                catch { _settings = BakeSettings.Default; }
            }
            else
            {
                _settings = BakeSettings.Default;
            }
            if (_settings.Width <= 0) _settings = BakeSettings.Default;
        }

        private void SaveSettings()
        {
            try
            {
                string json = JsonUtility.ToJson(_settings);
                EditorPrefs.SetString(PrefsKey, json);
            }
            catch { /* best-effort persistence */ }
        }

        private void ShowStatus(string text)
        {
            _statusText = text;
            _statusVisible = true;
            _statusUntilTime = EditorApplication.timeSinceStartup + 3.0;
            Repaint();
        }
    }
}
