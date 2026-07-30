using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Editor.PageBuilder
{
    /// <summary>
    /// Creates M3-ready UXML page scaffolds and inserts M3 components into existing UXML files.
    /// Works with Unity's native UI Builder — modifies UXML on disk, UI Builder auto-refreshes.
    /// </summary>
    internal static class UxmlExporter
    {
        private static readonly XNamespace UiNs = "UnityEngine.UIElements";
        private static readonly XNamespace ComponentsNs = "PFound.UISystem.Components";

        // Mapping from M3 component type name to required USS file names
        private static readonly Dictionary<string, string[]> s_ComponentStyles = new()
        {
            { "M3Button",             new[] { "Components/button.uss", "state-layer.uss" } },
            { "M3Card",               new[] { "Components/card.uss", "state-layer.uss" } },
            { "M3Checkbox",           new[] { "Components/checkbox.uss", "state-layer.uss" } },
            { "M3Chip",               new[] { "Components/chip.uss", "state-layer.uss" } },
            { "M3Dialog",             new[] { "Components/dialog.uss" } },
            { "M3Divider",            new[] { "Components/divider.uss" } },
            { "M3FAB",                new[] { "Components/fab.uss", "state-layer.uss" } },
            { "M3List",               new[] { "Components/list.uss" } },
            { "M3ListItem",           new[] { "Components/list.uss", "state-layer.uss" } },
            { "M3TextField",          new[] { "Components/textfield.uss", "state-layer.uss" } },
            { "M3Toggle",             new[] { "Components/toggle.uss" } },
            { "M3Tabs",               new[] { "Components/tabs.uss" } },
            { "M3TabItem",            new[] { "Components/tabs.uss" } },
            { "M3RadioButton",        new[] { "Components/radio.uss", "state-layer.uss" } },
            { "M3Slider",             new[] { "Components/slider.uss" } },
            { "M3SegmentedButton",    new[] { "Components/segmented-button.uss", "state-layer.uss" } },
            { "M3SegmentedItem",      new[] { "Components/segmented-button.uss" } },
            { "M3Menu",               new[] { "Components/menu.uss" } },
            { "M3MenuItem",           new[] { "Components/menu.uss" } },
            { "M3Snackbar",           new[] { "Components/snackbar.uss" } },
            { "M3Badge",              new[] { "Components/badge.uss" } },
            { "M3ProgressIndicator",  new[] { "Components/progress-indicator.uss" } },
            { "M3NavigationBar",      new[] { "Components/navigation-bar.uss" } },
            { "M3NavigationItem",     new[] { "Components/navigation-bar.uss" } },
            { "M3NavigationDrawer",   new[] { "Components/navigation-drawer.uss" } },
            { "M3NavigationRail",     new[] { "Components/navigation-rail.uss" } },
            { "M3TopAppBar",          new[] { "Components/top-app-bar.uss" } },
            { "M3BottomAppBar",       new[] { "Components/bottom-app-bar.uss" } },
            { "M3BottomSheet",        new[] { "Components/bottom-sheet.uss" } },
            { "M3SearchBar",          new[] { "Components/search-bar.uss", "state-layer.uss" } },
            { "M3DatePicker",         new[] { "Components/date-picker.uss" } },
            { "M3TimePicker",         new[] { "Components/time-picker.uss" } },
            { "M3Tooltip",            new[] { "Components/tooltip.uss" } },
        };

        /// <summary>
        /// Creates a new UXML file with M3 style references — ready for UI Builder editing.
        /// </summary>
        internal static void CreatePageScaffold(string filePath)
        {
            string stylesBase = GetStylesRelativePath(filePath);

            var sb = new StringBuilder();
            sb.AppendLine("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"");
            sb.AppendLine("         xmlns:components=\"PFound.UISystem.Components\">");
            sb.AppendLine($"    <ui:Style src=\"{stylesBase}typography.uss\" />");
            sb.AppendLine($"    <ui:Style src=\"{stylesBase}state-layer.uss\" />");
            sb.AppendLine("    <ui:VisualElement name=\"page-root\" style=\"flex-grow: 1;\">");
            sb.AppendLine("    </ui:VisualElement>");
            sb.AppendLine("</ui:UXML>");

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"[PageBuilder] Created new page at {filePath}");
        }

        /// <summary>
        /// Adds an M3 component to an existing UXML file.
        /// Inserts the component element and any missing style references.
        /// </summary>
        internal static void AddComponentToUxml(string filePath, ComponentRegistry.ComponentInfo info)
        {
            string content = File.ReadAllText(filePath);

            // Parse UXML
            XDocument doc;
            try
            {
                doc = XDocument.Parse(content);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PageBuilder] Failed to parse UXML: {e.Message}");
                return;
            }

            var root = doc.Root;
            if (root == null) return;

            // Ensure components namespace is declared
            if (root.Attribute(XNamespace.Xmlns + "components") == null)
                root.SetAttributeValue(XNamespace.Xmlns + "components", ComponentsNs.NamespaceName);

            // Add missing style references
            string stylesBase = GetStylesRelativePath(filePath);
            AddMissingStyles(root, info, stylesBase);

            // Find page-root or add to the document root
            var pageRoot = FindElementByName(root, "page-root");
            var targetParent = pageRoot ?? root;

            // Count existing elements of same type for unique naming
            string baseName = info.ComponentType == typeof(VisualElement)
                ? "container"
                : ToKebabCase(info.ComponentType.Name);
            int index = CountElementsWithPrefix(targetParent, baseName) + 1;
            string elementName = $"{baseName}-{index}";

            // Build element — container or M3 component
            XElement newElement;
            if (info.ComponentType == typeof(VisualElement))
            {
                newElement = new XElement(UiNs + "VisualElement");
                newElement.SetAttributeValue("name", elementName);
                newElement.SetAttributeValue("style", "flex-grow: 1; padding: 8px;");
            }
            else
            {
                newElement = BuildComponentElement(info);
                newElement.SetAttributeValue("name", elementName);
            }

            targetParent.Add(newElement);

            // Write back with proper formatting
            var settings = new System.Xml.XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                OmitXmlDeclaration = true
            };

            using (var writer = System.Xml.XmlWriter.Create(filePath, settings))
            {
                doc.WriteTo(writer);
            }

            Debug.Log($"[PageBuilder] Added {info.DisplayName} to {Path.GetFileName(filePath)}");
        }

        private static XElement BuildComponentElement(ComponentRegistry.ComponentInfo info)
        {
            string typeName = info.ComponentType.Name;
            var element = new XElement(ComponentsNs + typeName);

            // Get non-default attributes from a factory-created instance
            var instance = info.Factory();
            var defaultInstance = (VisualElement)Activator.CreateInstance(info.ComponentType);

            var properties = info.ComponentType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.GetCustomAttribute<UxmlAttributeAttribute>() == null) continue;
                if (!prop.CanRead || !prop.CanWrite) continue;

                try
                {
                    var currentValue = prop.GetValue(instance);
                    var defaultValue = prop.GetValue(defaultInstance);

                    if (Equals(currentValue, defaultValue)) continue;
                    if (currentValue == null) continue;

                    string attrName = ToKebabCase(prop.Name);
                    string attrValue = SerializeValue(currentValue);
                    element.SetAttributeValue(attrName, attrValue);
                }
                catch
                {
                    // Skip properties that can't be read
                }
            }

            return element;
        }

        private static void AddMissingStyles(XElement root, ComponentRegistry.ComponentInfo info, string stylesBase)
        {
            string typeName = info.ComponentType.Name;
            if (!s_ComponentStyles.TryGetValue(typeName, out var requiredStyles)) return;

            // Collect existing style src values
            var existingStyles = new HashSet<string>();
            foreach (var styleEl in root.Elements())
            {
                if (styleEl.Name.LocalName != "Style") continue;
                var src = styleEl.Attribute("src")?.Value;
                if (src != null) existingStyles.Add(src);
            }

            // Find the last Style element to insert after
            XElement lastStyle = null;
            foreach (var el in root.Elements())
            {
                if (el.Name.LocalName == "Style")
                    lastStyle = el;
            }

            foreach (string style in requiredStyles)
            {
                string fullSrc = stylesBase + style;
                if (existingStyles.Contains(fullSrc)) continue;

                var styleElement = new XElement(UiNs + "Style");
                styleElement.SetAttributeValue("src", fullSrc);

                if (lastStyle != null)
                {
                    lastStyle.AddAfterSelf(styleElement);
                    lastStyle = styleElement;
                }
                else
                {
                    root.AddFirst(styleElement);
                    lastStyle = styleElement;
                }

                existingStyles.Add(fullSrc);
            }
        }

        /// <summary>
        /// Removes all children from page-root, keeping style references intact.
        /// </summary>
        internal static void ClearPage(string filePath)
        {
            string content = File.ReadAllText(filePath);

            XDocument doc;
            try
            {
                doc = XDocument.Parse(content);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PageBuilder] Failed to parse UXML: {e.Message}");
                return;
            }

            var root = doc.Root;
            if (root == null) return;

            var pageRoot = FindElementByName(root, "page-root");
            if (pageRoot != null)
            {
                pageRoot.RemoveNodes();
            }

            var settings = new System.Xml.XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                OmitXmlDeclaration = true
            };

            using (var writer = System.Xml.XmlWriter.Create(filePath, settings))
            {
                doc.WriteTo(writer);
            }

            Debug.Log($"[PageBuilder] Cleared all components from {Path.GetFileName(filePath)}");
        }

        private static int CountElementsWithPrefix(XElement parent, string prefix)
        {
            int count = 0;
            foreach (var child in parent.Elements())
            {
                var nameAttr = child.Attribute("name")?.Value;
                if (nameAttr != null && nameAttr.StartsWith(prefix))
                    count++;
            }
            return count;
        }

        private static XElement FindElementByName(XElement root, string name)
        {
            if (root.Attribute("name")?.Value == name) return root;

            foreach (var child in root.Elements())
            {
                var found = FindElementByName(child, name);
                if (found != null) return found;
            }

            return null;
        }

        private static string SerializeValue(object value)
        {
            if (value is Enum e) return e.ToString();
            if (value is bool b) return b ? "true" : "false";
            if (value is float f) return f.ToString("G");
            if (value is int i) return i.ToString();
            return value.ToString();
        }

        private static string ToKebabCase(string pascalCase)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < pascalCase.Length; i++)
            {
                char c = pascalCase[i];
                if (char.IsUpper(c) && i > 0)
                {
                    sb.Append('-');
                    sb.Append(char.ToLower(c));
                }
                else
                {
                    sb.Append(char.ToLower(c));
                }
            }
            return sb.ToString();
        }

        private static string GetStylesRelativePath(string outputFilePath)
        {
            string outputDir = Path.GetDirectoryName(Path.GetFullPath(outputFilePath)) ?? "";
            string stylesDir = Path.GetFullPath(GetUISystemRoot() + "/Styles/");

            try
            {
                var outputUri = new Uri(outputDir + "/");
                var stylesUri = new Uri(stylesDir);
                return outputUri.MakeRelativeUri(stylesUri).ToString();
            }
            catch
            {
                return "../../Styles/";
            }
        }

        /// <summary>
        /// Finds UISystem root by locating the asmdef via AssetDatabase.
        /// Works regardless of where the submodule is mounted.
        /// </summary>
        private static string GetUISystemRoot()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("PFound.UISystem t:AssemblyDefinitionAsset");
            if (guids.Length > 0)
            {
                var asmdefPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return Path.GetDirectoryName(asmdefPath)?.Replace('\\', '/');
            }
            // Fallback
            return "Assets/PFound/UISystem";
        }
    }
}
