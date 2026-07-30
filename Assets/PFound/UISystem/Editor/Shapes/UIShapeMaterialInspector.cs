using PFound.UISystem.Shapes;
using UnityEditor;
using UnityEngine;

namespace PFound.UISystem.Editor.Shapes
{
    /// <summary>
    /// Custom <see cref="ShaderGUI"/> for the <c>UISystem/Shape</c> shader. Registered
    /// via the shader's <c>CustomEditor</c> directive, so it only affects materials that
    /// use this shader — does NOT override Unity's default Material Inspector for other shaders.
    /// </summary>
    /// <remarks>
    /// Each effect toggle ALWAYS sets both the <c>_&lt;Effect&gt;Enable</c> property AND the
    /// matching <c>EFFECT_&lt;EFFECT&gt;_ON</c> shader keyword (strip discipline).
    /// </remarks>
    public class UIShapeMaterialInspector : ShaderGUI
    {
        private const string PrefShapeGroup = "PFound.UISystem.Shapes.Inspector.ShapeOpen";
        private const string PrefFillGroup = "PFound.UISystem.Shapes.Inspector.FillOpen";
        private const string PrefGradientGroup = "PFound.UISystem.Shapes.Inspector.GradientOpen";
        private const string PrefOutlineGroup = "PFound.UISystem.Shapes.Inspector.OutlineOpen";
        private const string PrefBandingGroup = "PFound.UISystem.Shapes.Inspector.BandingOpen";
        private const string PrefNoiseGroup = "PFound.UISystem.Shapes.Inspector.NoiseOpen";
        private const string PrefDotsGroup = "PFound.UISystem.Shapes.Inspector.DotsOpen";
        private const string PrefShadowGroup = "PFound.UISystem.Shapes.Inspector.ShadowOpen";

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var material = materialEditor.target as Material;
            if (material == null)
            {
                materialEditor.PropertiesDefaultGUI(properties);
                return;
            }

            DrawShapeGroup(materialEditor, material);
            EditorGUILayout.Space(4);
            DrawFillGroup(materialEditor, material);
            EditorGUILayout.Space(8);

            DrawGradientGroup(materialEditor, material);
            DrawOutlineGroup(materialEditor, material);
            DrawBandingGroup(materialEditor, material);
            DrawNoiseGroup(materialEditor, material);
            DrawDotsGroup(materialEditor, material);
            DrawShadowGroup(materialEditor, material);
            EditorGUILayout.Space(8);

            DrawCompositionReadout(material);
            EditorGUILayout.Space(4);

            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
            materialEditor.DoubleSidedGIField();
        }

        // ─── Effect helpers ────────────────────────────────────────────

        private static EffectMask ReadEffectMask(Material material)
        {
            EffectMask mask = EffectMask.None;
            if (material.GetFloat(UIShapeMaterialProperties.GradientEnable) > 0.5f) mask |= EffectMask.Gradient;
            if (material.GetFloat(UIShapeMaterialProperties.OutlineEnable) > 0.5f) mask |= EffectMask.Outline;
            if (material.GetFloat(UIShapeMaterialProperties.BandingEnable) > 0.5f) mask |= EffectMask.Banding;
            if (material.GetFloat(UIShapeMaterialProperties.NoiseEnable) > 0.5f) mask |= EffectMask.Noise;
            if (material.GetFloat(UIShapeMaterialProperties.DotsEnable) > 0.5f) mask |= EffectMask.Dots;
            if (material.GetFloat(UIShapeMaterialProperties.ShadowEnable) > 0.5f) mask |= EffectMask.Shadow;
            return mask;
        }

        private static void DrawCompositionReadout(Material material)
        {
            var mask = ReadEffectMask(material);
            string composition = UIShapeEffectComposition.GetCompositionString(mask);
            EditorGUILayout.HelpBox("Composition: " + composition, MessageType.None);
        }

        private static bool DrawEffectFoldout(string prefKey, string title)
        {
            bool open = EditorPrefs.GetBool(prefKey, false);
            open = EditorGUILayout.BeginFoldoutHeaderGroup(open, title);
            EditorPrefs.SetBool(prefKey, open);
            return open;
        }

        private static bool DrawEnableToggle(MaterialEditor materialEditor, Material material, string enableProp, string keyword, string label)
        {
            bool current = material.GetFloat(enableProp) > 0.5f;
            EditorGUI.BeginChangeCheck();
            bool next = EditorGUILayout.Toggle(label, current);
            if (EditorGUI.EndChangeCheck() && next != current)
            {
                materialEditor.RegisterPropertyChangeUndo("UIShape " + label);
                material.SetFloat(enableProp, next ? 1f : 0f);
                if (next) material.EnableKeyword(keyword);
                else material.DisableKeyword(keyword);
                EditorUtility.SetDirty(material);
            }
            return next;
        }

        private void DrawShapeGroup(MaterialEditor materialEditor, Material material)
        {
            bool open = EditorPrefs.GetBool(PrefShapeGroup, true);
            open = EditorGUILayout.BeginFoldoutHeaderGroup(open, "Shape");
            EditorPrefs.SetBool(PrefShapeGroup, open);

            if (open)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawShapeTypeRow(materialEditor, material);
                    DrawCornerRadiiRow(materialEditor, material);
                    DrawRadiusOverflowWarning(material);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawShapeTypeRow(MaterialEditor materialEditor, Material material)
        {
            int current = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat(UIShapeMaterialProperties.ShapeType)), 0, 3);
            EditorGUI.BeginChangeCheck();
            int next = (int)(ShapeType)EditorGUILayout.EnumPopup("Shape Type", (ShapeType)current);
            if (EditorGUI.EndChangeCheck() && next != current)
            {
                materialEditor.RegisterPropertyChangeUndo("UIShape Shape Type");
                material.SetFloat(UIShapeMaterialProperties.ShapeType, next);
                UIShapeMaterialHelpers.SyncShapeTypeKeyword(material, (ShapeType)next);
                EditorUtility.SetDirty(material);
            }
        }

        private static void DrawCornerRadiiRow(MaterialEditor materialEditor, Material material)
        {
            Vector4 radii = material.GetVector(UIShapeMaterialProperties.CornerRadii);
            EditorGUI.BeginChangeCheck();
            Vector4 next = EditorGUILayout.Vector4Field("Corner Radii (TL,TR,BR,BL)", radii);
            if (EditorGUI.EndChangeCheck() && next != radii)
            {
                materialEditor.RegisterPropertyChangeUndo("UIShape Corner Radii");
                material.SetVector(UIShapeMaterialProperties.CornerRadii, next);
                EditorUtility.SetDirty(material);
            }
        }

        private static void DrawRadiusOverflowWarning(Material material)
        {
            Vector4 rectSize = material.GetVector(UIShapeMaterialProperties.RectSize);
            float halfMin = Mathf.Min(rectSize.x, rectSize.y) * 0.5f;
            if (halfMin <= 0f) return;

            Vector4 radii = material.GetVector(UIShapeMaterialProperties.CornerRadii);
            bool overflow = radii.x > halfMin || radii.y > halfMin || radii.z > halfMin || radii.w > halfMin;
            if (overflow)
            {
                EditorGUILayout.HelpBox(
                    "Corner radius exceeds half the shape's smaller dimension; clamped at render time.",
                    MessageType.Warning);
            }
        }

        private void DrawFillGroup(MaterialEditor materialEditor, Material material)
        {
            bool open = EditorPrefs.GetBool(PrefFillGroup, true);
            open = EditorGUILayout.BeginFoldoutHeaderGroup(open, "Fill");
            EditorPrefs.SetBool(PrefFillGroup, open);

            if (open)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    Color current = material.GetColor(UIShapeMaterialProperties.FillColor);
                    EditorGUI.BeginChangeCheck();
                    Color next = EditorGUILayout.ColorField("Fill Color", current);
                    if (EditorGUI.EndChangeCheck() && next != current)
                    {
                        materialEditor.RegisterPropertyChangeUndo("UIShape Fill Color");
                        material.SetColor(UIShapeMaterialProperties.FillColor, next);
                        EditorUtility.SetDirty(material);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ─── Effect group draws ────────────────────────────────────────

        private void DrawGradientGroup(MaterialEditor materialEditor, Material material)
        {
            bool open = DrawEffectFoldout(PrefGradientGroup, "Gradient");
            if (open)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    bool enabled = DrawEnableToggle(materialEditor, material,
                        UIShapeMaterialProperties.GradientEnable,
                        UIShapeShaderKeywords.EffectGradientOn, "Enable");
                    if (enabled)
                    {
                        // Fully qualify GradientMode — UnityEngine has its own GradientMode enum.
                        DrawModeDropdown<PFound.UISystem.Shapes.GradientMode>(materialEditor, material,
                            UIShapeMaterialProperties.GradientMode, "Mode",
                            UIShapeShaderKeywords.GradientModeRadial,
                            PFound.UISystem.Shapes.GradientMode.Radial);
                        DrawFloatRow(materialEditor, material, UIShapeMaterialProperties.GradientAngle, "Angle");
                        DrawFloatRow(materialEditor, material, UIShapeMaterialProperties.GradientFalloff, "Falloff");
                        DrawColorRow(materialEditor, material, UIShapeMaterialProperties.GradientColorA, "Color A");
                        DrawColorRow(materialEditor, material, UIShapeMaterialProperties.GradientColorB, "Color B");
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawOutlineGroup(MaterialEditor materialEditor, Material material)
        {
            bool open = DrawEffectFoldout(PrefOutlineGroup, "Outline");
            if (open)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    bool enabled = DrawEnableToggle(materialEditor, material,
                        UIShapeMaterialProperties.OutlineEnable,
                        UIShapeShaderKeywords.EffectOutlineOn, "Enable");
                    if (enabled)
                    {
                        DrawFloatRow(materialEditor, material, UIShapeMaterialProperties.OutlineThickness, "Thickness");
                        DrawColorRow(materialEditor, material, UIShapeMaterialProperties.OutlineColor, "Color");
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawBandingGroup(MaterialEditor materialEditor, Material material)
        {
            bool open = DrawEffectFoldout(PrefBandingGroup, "Banding");
            if (open)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    bool enabled = DrawEnableToggle(materialEditor, material,
                        UIShapeMaterialProperties.BandingEnable,
                        UIShapeShaderKeywords.EffectBandingOn, "Enable");
                    if (enabled)
                    {
                        DrawFloatRow(materialEditor, material, UIShapeMaterialProperties.BandingSpacing, "Spacing");
                        DrawColorRow(materialEditor, material, UIShapeMaterialProperties.BandingColorA, "Color A");
                        DrawColorRow(materialEditor, material, UIShapeMaterialProperties.BandingColorB, "Color B");
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawNoiseGroup(MaterialEditor materialEditor, Material material)
        {
            bool open = DrawEffectFoldout(PrefNoiseGroup, "Noise");
            if (open)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    bool enabled = DrawEnableToggle(materialEditor, material,
                        UIShapeMaterialProperties.NoiseEnable,
                        UIShapeShaderKeywords.EffectNoiseOn, "Enable");
                    if (enabled)
                    {
                        DrawModeDropdown<NoiseMode>(materialEditor, material,
                            UIShapeMaterialProperties.NoiseMode, "Mode",
                            UIShapeShaderKeywords.NoiseModeWorley, NoiseMode.Worley);
                        DrawFloatRow(materialEditor, material, UIShapeMaterialProperties.NoiseScale, "Scale");
                        DrawSliderRow(materialEditor, material, UIShapeMaterialProperties.NoiseAmplitude, "Amplitude", 0f, 1f);
                        DrawColorRow(materialEditor, material, UIShapeMaterialProperties.NoiseColor, "Color");
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDotsGroup(MaterialEditor materialEditor, Material material)
        {
            bool open = DrawEffectFoldout(PrefDotsGroup, "Dots");
            if (open)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    bool enabled = DrawEnableToggle(materialEditor, material,
                        UIShapeMaterialProperties.DotsEnable,
                        UIShapeShaderKeywords.EffectDotsOn, "Enable");
                    if (enabled)
                    {
                        DrawFloatRow(materialEditor, material, UIShapeMaterialProperties.DotsRadius, "Radius");
                        DrawFloatRow(materialEditor, material, UIShapeMaterialProperties.DotsSpacing, "Spacing");
                        DrawColorRow(materialEditor, material, UIShapeMaterialProperties.DotsColor, "Color");
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawShadowGroup(MaterialEditor materialEditor, Material material)
        {
            bool open = DrawEffectFoldout(PrefShadowGroup, "Shadow");
            if (open)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    bool enabled = DrawEnableToggle(materialEditor, material,
                        UIShapeMaterialProperties.ShadowEnable,
                        UIShapeShaderKeywords.EffectShadowOn, "Enable");
                    if (enabled)
                    {
                        DrawVector2Row(materialEditor, material, UIShapeMaterialProperties.ShadowOffset, "Offset (X,Y)");
                        DrawFloatRow(materialEditor, material, UIShapeMaterialProperties.ShadowBlur, "Blur");
                        DrawColorRow(materialEditor, material, UIShapeMaterialProperties.ShadowColor, "Color");
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ─── Common row helpers ────────────────────────────────────────

        private static void DrawFloatRow(MaterialEditor materialEditor, Material material, string prop, string label)
        {
            float current = material.GetFloat(prop);
            EditorGUI.BeginChangeCheck();
            float next = EditorGUILayout.FloatField(label, current);
            if (EditorGUI.EndChangeCheck() && next != current)
            {
                materialEditor.RegisterPropertyChangeUndo("UIShape " + label);
                material.SetFloat(prop, next);
                EditorUtility.SetDirty(material);
            }
        }

        private static void DrawSliderRow(MaterialEditor materialEditor, Material material, string prop, string label, float min, float max)
        {
            float current = material.GetFloat(prop);
            EditorGUI.BeginChangeCheck();
            float next = EditorGUILayout.Slider(label, current, min, max);
            if (EditorGUI.EndChangeCheck() && next != current)
            {
                materialEditor.RegisterPropertyChangeUndo("UIShape " + label);
                material.SetFloat(prop, next);
                EditorUtility.SetDirty(material);
            }
        }

        private static void DrawColorRow(MaterialEditor materialEditor, Material material, string prop, string label)
        {
            Color current = material.GetColor(prop);
            EditorGUI.BeginChangeCheck();
            Color next = EditorGUILayout.ColorField(label, current);
            if (EditorGUI.EndChangeCheck() && next != current)
            {
                materialEditor.RegisterPropertyChangeUndo("UIShape " + label);
                material.SetColor(prop, next);
                EditorUtility.SetDirty(material);
            }
        }

        private static void DrawVector2Row(MaterialEditor materialEditor, Material material, string prop, string label)
        {
            Vector4 current = material.GetVector(prop);
            EditorGUI.BeginChangeCheck();
            Vector2 next = EditorGUILayout.Vector2Field(label, new Vector2(current.x, current.y));
            if (EditorGUI.EndChangeCheck() && (next.x != current.x || next.y != current.y))
            {
                materialEditor.RegisterPropertyChangeUndo("UIShape " + label);
                material.SetVector(prop, new Vector4(next.x, next.y, 0f, 0f));
                EditorUtility.SetDirty(material);
            }
        }

        private static void DrawModeDropdown<TEnum>(MaterialEditor materialEditor, Material material, string prop, string label, string variantKeyword, TEnum variantValue)
            where TEnum : System.Enum
        {
            int current = Mathf.RoundToInt(material.GetFloat(prop));
            EditorGUI.BeginChangeCheck();
            TEnum currentEnum = (TEnum)System.Enum.ToObject(typeof(TEnum), current);
            TEnum nextEnum = (TEnum)EditorGUILayout.EnumPopup(label, currentEnum);
            int next = System.Convert.ToInt32(nextEnum);
            if (EditorGUI.EndChangeCheck() && next != current)
            {
                materialEditor.RegisterPropertyChangeUndo("UIShape " + label);
                material.SetFloat(prop, next);
                if (next == System.Convert.ToInt32(variantValue)) material.EnableKeyword(variantKeyword);
                else material.DisableKeyword(variantKeyword);
                EditorUtility.SetDirty(material);
            }
        }

        // Note: SetEffectEnabled / SyncShapeTypeKeyword runtime helpers live in
        // PFound.UISystem.Shapes.UIShapeMaterialHelpers so test/runtime callers can
        // reuse them without depending on the Editor assembly.
    }
}
