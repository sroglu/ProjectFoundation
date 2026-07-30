using System;
using UnityEditor;
using UnityEngine;
using System.Reflection;

/*
namespace PFound.GameDataStore.Entries.Editor
{
    [CustomPropertyDrawer(typeof(Entry))]
    public class EntryPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Draw the EntryType enum popup
            SerializedProperty typeProp = property.FindPropertyRelative("Type");
            if (typeProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Type field not found");
                return;
            }

            Rect typeRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(typeRect, typeProp);

            EntryType entryType = (EntryType)typeProp.enumValueFlag;

            // Draw the value field based on EntryType
            Rect valueRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width,
                EditorGUIUtility.singleLineHeight);

            // Use reflection to get the target object
            object entryObj = fieldInfo.GetValue(property.serializedObject.targetObject);

            if (entryObj == null)
                return;

            Entry e = (Entry)entryObj;
            e.Type = entryType;
            EditorGUI.BeginProperty(valueRect, label, property);
            switch (entryType)
            {
                case EntryType.Int:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Int2:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Int3:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Int64:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.UInt:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.UInt2:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.UInt3:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.UInt64:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Short:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.UShort:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Byte:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Sbyte:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Float:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Float2:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Float3:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Double:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Char:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.String:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.Bool:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.EntityType:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.EntityLevel:
                    DrawInternalField(e, valueRect, property);
                    break;
                case EntryType.WeekDay:
                    DrawInternalField(e, valueRect, property);
                    break;
                // Add more cases for other types as needed
                default:
                    EditorGUI.LabelField(valueRect, "Unsupported EntryType");
                    break;
            }
            EditorGUI.EndProperty();
        }

        private void DrawInternalField(Entry e, Rect rect, SerializedProperty property)
        {
            /*FieldInfo field = entryObj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                EditorGUI.LabelField(rect, $"Field {fieldName} not found");
                return;
            }#1#

            EditorGUI.BeginChangeCheck();
            object newValue = null;

            if (e.Type == EntryType.Int)
            {
                newValue = EditorGUI.IntField(rect, "Value", e.ValueInt != null ? (int)e.ValueInt : 0);
            }
            else if (e.Type == EntryType.Int2)
            {
                var tuple = Tuple.Create(e.ValueInt2.x, e.ValueInt2.y);
                int item1 = EditorGUI.IntField(new Rect(rect.x, rect.y, rect.width / 2 - 2, rect.height), "X",
                    tuple.Item1);
                int item2 = EditorGUI.IntField(
                    new Rect(rect.x + rect.width / 2 + 2, rect.y, rect.width / 2 - 2, rect.height), "Y", tuple.Item2);
                newValue = Tuple.Create(item1, item2);
            }
            else if (e.Type == EntryType.Int3)
            {
                var tuple = Tuple.Create(e.ValueInt3.x, e.ValueInt3.y, e.ValueInt3.z);
                int item1 = EditorGUI.IntField(new Rect(rect.x, rect.y, rect.width / 3 - 4, rect.height), "X",
                    tuple.Item1);
                int item2 = EditorGUI.IntField(
                    new Rect(rect.x + rect.width / 3 + 4, rect.y, rect.width / 3 - 4, rect.height), "Y", tuple.Item2);
                int item3 = EditorGUI.IntField(
                    new Rect(rect.x + 2 * (rect.width / 3) + 8, rect.y, rect.width / 3 - 4, rect.height), "Z",
                    tuple.Item3);
                newValue = Tuple.Create(item1, item2, item3);
            }
            else if (e.Type == EntryType.Int64)
            {
                newValue = EditorGUI.LongField(rect, "Value", e.ValueInt64 != null ? (long)e.ValueInt64 : 0L);
            }
            else if (e.Type == EntryType.UInt)
            {
                newValue = (uint)EditorGUI.LongField(rect, "Value", e.ValueUInt != null ? (long)(uint)e.ValueUInt : 0L);
            }
            else if (e.Type == EntryType.UInt2)
            {
                var tuple = Tuple.Create(e.ValueUInt2.x, e.ValueUInt2.y);
                uint item1 = (uint)EditorGUI.LongField(new Rect(rect.x, rect.y, rect.width / 2 - 2, rect.height), "X",
                    tuple.Item1);
                uint item2 = (uint)EditorGUI.LongField(
                    new Rect(rect.x + rect.width / 2 + 2, rect.y, rect.width / 2 - 2, rect.height), "Y", tuple.Item2);
                newValue = Tuple.Create(item1, item2);
            }
            else if (e.Type == EntryType.UInt3)
            {
                var tuple = Tuple.Create(e.ValueUInt3.x, e.ValueUInt3.y, e.ValueUInt3.z);
                uint item1 = (uint)EditorGUI.LongField(new Rect(rect.x, rect.y, rect.width / 3 - 4, rect.height), "X",
                    tuple.Item1);
                uint item2 = (uint)EditorGUI.LongField(
                    new Rect(rect.x + rect.width / 3 + 4, rect.y, rect.width / 3 - 4, rect.height), "Y", tuple.Item2);
                uint item3 = (uint)EditorGUI.LongField(
                    new Rect(rect.x + 2 * (rect.width / 3) + 8, rect.y, rect.width / 3 - 4, rect.height), "Z",
                    tuple.Item3);
                newValue = Tuple.Create(item1, item2, item3);
            }
            else if (e.Type == EntryType.UInt64)
            {
                newValue = (ulong)EditorGUI.LongField(rect, "Value",
                    e.ValueUInt64 != null ? (long)(ulong)e.ValueUInt64 : 0L);
            }
            else if (e.Type == EntryType.Short)
            {
                newValue = (short)EditorGUI.IntField(rect, "Value",
                    e.ValueShort != null ? (short)e.ValueShort : (short)0);
            }
            else if (e.Type == EntryType.UShort)
            {
                newValue = (ushort)EditorGUI.IntField(rect, "Value",
                    e.ValueUShort != null ? (ushort)e.ValueUShort : (ushort)0);
            }
            else if (e.Type == EntryType.Byte)
            {
                newValue = (byte)EditorGUI.IntField(rect, "Value", e.ValueByte != null ? (byte)e.ValueByte : (byte)0);
            }
            else if (e.Type == EntryType.Sbyte)
            {
                newValue = (sbyte)EditorGUI.IntField(rect, "Value",
                    e.ValueSByte != null ? (sbyte)e.ValueSByte : (sbyte)0);
            }
            else if (e.Type == EntryType.Float)
            {
                newValue = EditorGUI.FloatField(rect, "Value", e.ValueFloat != null ? (float)e.ValueFloat : 0f);
            }
            else if (e.Type == EntryType.Float2)
            {
                var tuple = Tuple.Create(e.ValueFloat2.x, e.ValueFloat2.y);
                float item1 = EditorGUI.FloatField(new Rect(rect.x, rect.y, rect.width / 2 - 2, rect.height), "X",
                    tuple.Item1);
                float item2 =
                    EditorGUI.FloatField(new Rect(rect.x + rect.width / 2 + 2, rect.y, rect.width / 2 - 2, rect.height),
                        "Y", tuple.Item2);
                newValue = Tuple.Create(item1, item2);
            }
            else if (e.Type == EntryType.Float3)
            {
                var tuple = Tuple.Create(e.ValueFloat3.x, e.ValueFloat3.y, e.ValueFloat3.z);
                float item1 = EditorGUI.FloatField(new Rect(rect.x, rect.y, rect.width / 3 - 4, rect.height), "X",
                    tuple.Item1);
                float item2 =
                    EditorGUI.FloatField(new Rect(rect.x + rect.width / 3 + 4, rect.y, rect.width / 3 - 4, rect.height),
                        "Y", tuple.Item2);
                float item3 =
                    EditorGUI.FloatField(
                        new Rect(rect.x + 2 * (rect.width / 3) + 8, rect.y, rect.width / 3 - 4, rect.height), "Z",
                        tuple.Item3);
                newValue = Tuple.Create(item1, item2, item3);
            }
            else if (e.Type == EntryType.Double)
            {
                newValue = EditorGUI.DoubleField(rect, "Value", e.ValueDouble);
            }
            else if (e.Type == EntryType.Char)
            {
                string charStr = e.ValueChar.ToString();
                string newCharStr = EditorGUI.TextField(rect, "Value", charStr);
                if (!string.IsNullOrEmpty(newCharStr))
                    newValue = newCharStr[0];
                else
                    newValue = '\0';
            }
            else if (e.Type == EntryType.String)
            {
                newValue = EditorGUI.TextField(rect, "Value", e.ValueString);
            }
            else if (e.Type == EntryType.Bool)
            {
                newValue = EditorGUI.Toggle(rect, "Value", e.ValueBool);
            }
            /*else if (fieldType == typeof(EntityType))
                newValue = (EntityType)EditorGUI.EnumPopup(rect, "Value", value != null ? (EntityType)value : default);
            else if (fieldType == typeof(EntityLevel))
                newValue = (EntityLevel)EditorGUI.EnumPopup(rect, "Value", value != null ? (EntityLevel)value : default);#1#
            else if (e.Type == EntryType.WeekDay)
            {
                newValue = (WeekDay)EditorGUI.EnumPopup(rect, "Value", e.ValueWeekDay);
            }
            else
                EditorGUI.LabelField(rect, $"Unsupported field type: {e.Type}");
            // Add more types as needed

            if (EditorGUI.EndChangeCheck())
            {
                if (e.Type == EntryType.Int)
                {
                    e.ValueInt = (int)newValue;
                }
                else if (e.Type == EntryType.Int2)
                {
                    if (newValue is Tuple<int, int> tuple)
                        e.ValueInt2 = (tuple.Item1, tuple.Item2);
                }
                else if (e.Type == EntryType.Int3)
                {
                    if (newValue is Tuple<int, int, int> tuple)
                        e.ValueInt3 = (tuple.Item1, tuple.Item2, tuple.Item3);
                }
                else if (e.Type == EntryType.Int64)
                {
                    e.ValueInt64 = (long)newValue;
                }
                else if (e.Type == EntryType.UInt)
                {
                    e.ValueUInt = (uint)newValue;
                }
                else if (e.Type == EntryType.UInt2)
                {
                    if (newValue is Tuple<uint, uint> tuple)
                    {
                        e.ValueUInt2 = (tuple.Item1, tuple.Item2);
                    }
                }
                else if (e.Type == EntryType.UInt3)
                {
                    if (newValue is Tuple<uint, uint, uint> tuple)
                    {
                        e.ValueUInt3 = (tuple.Item1, tuple.Item2, tuple.Item3);
                    }
                }
                else if (e.Type == EntryType.UInt64)
                {
                    e.ValueUInt64 = (ulong)newValue;
                }
                else if (e.Type == EntryType.Short)
                {
                    e.ValueShort = (short)newValue;
                }
                else if (e.Type == EntryType.UShort)
                {
                    e.ValueUShort = (ushort)newValue;
                }
                else if (e.Type == EntryType.Byte)
                {
                    e.ValueByte = (byte)newValue;
                }
                else if (e.Type == EntryType.Sbyte)
                {
                    e.ValueSByte = (sbyte)newValue;
                }
                else if (e.Type == EntryType.Float)
                {
                    e.ValueFloat = (float)newValue;
                }
                else if (e.Type == EntryType.Float2)
                {
                    if (newValue is Tuple<float, float> tuple)
                    {
                        e.ValueFloat2 = (tuple.Item1, tuple.Item2);
                    }
                }
                else if (e.Type == EntryType.Float3)
                {
                    if (newValue is Tuple<float, float, float> tuple)
                    {
                        e.ValueFloat3 = (tuple.Item1, tuple.Item2, tuple.Item3);
                    }
                }
                else if (e.Type == EntryType.Double)
                {
                    e.ValueDouble = (double)newValue;
                }
                else if (e.Type == EntryType.Char)
                {
                    e.ValueChar = (char)newValue;
                }
                else if (e.Type == EntryType.String)
                {
                    e.ValueString = (string)newValue;
                }
                else if (e.Type == EntryType.Bool)
                {
                    e.ValueBool = (bool)newValue;
                }
                else if (e.Type == EntryType.EntityType)
                {
                    e.ValueEntityType = (EntityType)newValue;
                }
                else if (e.Type == EntryType.EntityLevel)
                {
                    e.ValueEntityLevel = (EntityLevel)newValue;
                }
                else if (e.Type == EntryType.WeekDay)
                {
                    //e.ValueWeekDay = (WeekDay)newValue;
                    /*typeof(Entry).GetField("InternalWeekDay", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.SetValue(e, newValue);#1#
                    property.FindPropertyRelative("ValueWeekDay").enumValueIndex = (int)(WeekDay)newValue;
                }

                property.serializedObject.ApplyModifiedProperties();
                //EditorUtility.SetDirty(property.serializedObject.targetObject);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Two lines: one for type, one for value
            return EditorGUIUtility.singleLineHeight * 2 + 2;
        }
    }
}
*/

