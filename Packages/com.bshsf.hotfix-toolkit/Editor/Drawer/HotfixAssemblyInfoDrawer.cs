﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace zFramework.Hotfix.Toolkit
{
    using static AssemblyHotfixManager;
    [CustomPropertyDrawer(typeof(HotfixAssemblyInfo))]
    public class HotfixAssemblyInfoDrawer : PropertyDrawer
    {
        static Dictionary<string, DrawerState> drawerState = new Dictionary<string, DrawerState>();
        internal class DrawerState
        {
            public GUIStyle style;
            public float height;
            public string title;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            property.serializedObject.Update();
            position.height = EditorGUIUtility.singleLineHeight + 2;
            var indent = EditorGUI.indentLevel;
            var labelWidth = EditorGUIUtility.labelWidth;
            TextAsset bts = default;
            EditorGUI.indentLevel = 0;
            string message = string.Empty, message2 = string.Empty;
            var assets = new AssemblyDefinitionAsset[0];

            #region Initial Drawer State
            var path = property.FindPropertyRelative("assembly").propertyPath;
            var data = drawerState[path];
            #endregion

            EditorGUI.BeginProperty(position, label, property);

            #region Draw title
            var asm = property.FindPropertyRelative("assembly").objectReferenceValue as AssemblyDefinitionAsset;
            var color = EditorStyles.foldout.normal.textColor;
            data.style.normal.textColor = !asm || asm.name != data.title ? Color.red : color;
            if (!EditorGUIUtility.hierarchyMode)
            {
                EditorGUI.indentLevel--;
            }
            EditorGUIUtility.labelWidth = position.width;
            title_Content.text = data.title;
            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, title_Content, true, data.style);
            EditorGUIUtility.labelWidth = labelWidth;
            if (!EditorGUIUtility.hierarchyMode)
            {
                EditorGUI.indentLevel++;
            }
            #endregion
            var orign = position;
            if (property.isExpanded)
            {

                #region Assembly 有效性校验
                asm = property.FindPropertyRelative("assembly").objectReferenceValue as AssemblyDefinitionAsset;
                if (asm)
                {
                    if (IsEditorAssembly(asm)) // 查是否为编辑器脚本
                    {
                        message = "编辑器程序集不可热更！ ";
                    }
                    else if (IsUsedByAssemblyCSharp(asm)) // 查是否被Unity基础程序集引用
                    {
                        message = "不能被 Assembly-CSharp 相关程序集引用！ ";
                    }
                    else if (GetIndexFromPropertyPath(path) != FirstIndexOf(asm) && IsAssemblyDuplicated(asm))// 查重
                    {
                        message = "程序集已存在！ ";
                    }
                    else //查有被谁引用着，这些个程序集也需要热更，或者，你修正引用关系
                    {
                        assets = GetAssembliesRefed(asm);
                        if (assets.Length > 0)
                        {
                            message = "被以下程序集引用：";
                        }
                    }
                }
                else
                {
                    message = "程序集未指定！ ";
                }
                #endregion

                #region 转存的 bytes 文件校验
                asm = property.FindPropertyRelative("assembly").objectReferenceValue as AssemblyDefinitionAsset;
                bts = property.FindPropertyRelative("bytesAsset").objectReferenceValue as TextAsset;

                //有效性验证：assembly 不为空且名称与 转存文件名称匹配
                if (bts && (!asm || !bts.name.Equals(GetAssemblyName(asm))))
                {
                    Undo.RecordObject(property.serializedObject.targetObject, "RemoveTypeMissmatchedBytesFile");
                    property.FindPropertyRelative("bytesAsset").objectReferenceValue = null;
                    property.serializedObject.ApplyModifiedProperties();
                }
                if (!bts && asm)
                {
                    if (TryGetAssemblyBytesAsset(asm, out bts))
                    {
                        Undo.RecordObject(property.serializedObject.targetObject, "LoadTypeMatchedBytesFile");
                        property.FindPropertyRelative("bytesAsset").objectReferenceValue = bts;
                        property.serializedObject.ApplyModifiedProperties();
                    }
                    else
                    {
                        message2 = "热更二进制文件不存在，请通过 Force Build Assembly 构建、转存并挂载！";
                    }
                }
                #endregion

                #region 绘制 程序集 定义文件字段
                position.y += position.height + 6;
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUI.PropertyField(position, property.FindPropertyRelative("assembly"));
                    if (check.changed)
                    {
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }
                #endregion

                #region Draw Message
                data.title = !string.IsNullOrEmpty(message) || !asm ? "程序集配置异常" : asm.name;
                if (!string.IsNullOrEmpty(message))
                {
                    position.y += position.height + 4;
                    EditorGUI.HelpBox(position, message, MessageType.Error);
                }
                #endregion
                #region 绘制引用了此程序集的所有程序集（只处理直接引用，间接引用不处理）
                if (assets.Length > 0)
                {
                    for (int i = 0; i < assets.Length; i++)
                    {
                        position.y += position.height + 4;
                        var obj_rect = new Rect(position);
                        obj_rect.width -= 64;
                        var enabled = GUI.enabled;
                        GUI.enabled = false;
                        EditorGUI.ObjectField(obj_rect, assets[i], typeof(AssemblyDefinitionAsset), false);
                        GUI.enabled = true;

                        var bt_rect = new Rect(position);
                        bt_rect.x = bt_rect.width - (!EditorGUIUtility.hierarchyMode ? 30 : 15);
                        bt_rect.width = 62;
                        if (GUI.Button(bt_rect, fixButton))
                        {
                            AddAssemblyData(assets[i]);
                        }
                    }
                }
                #endregion

                #region 绘制 转存 dll 字段
                var enable = GUI.enabled;
                GUI.enabled = false;
                position.y += position.height + 4;
                EditorGUI.PropertyField(position, property.FindPropertyRelative("bytesAsset"));
                GUI.enabled = enable;
                #endregion

                #region 绘制转存 dll 相关消息
                if (!string.IsNullOrEmpty(message2)&&string.IsNullOrEmpty(message))
                {
                    position.y += position.height + 4;
                    var tip_rect = new Rect(position);
                    tip_rect.height *= 2;
                    EditorGUI.HelpBox(tip_rect, message2, MessageType.Warning);
                    position.y += position.height;
                }
                #endregion
            }
            data.height = position.y - orign.y + EditorGUIUtility.singleLineHeight + 6;
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var assemblis = property.FindPropertyRelative("assembly");
            var path = assemblis.propertyPath;
            if (!drawerState.TryGetValue(path, out var data))
            {
                data = new DrawerState();
                data.style = new GUIStyle(EditorStyles.foldout);
                data.height = base.GetPropertyHeight(property, label);
                data.title = property.FindPropertyRelative("assembly").objectReferenceValue?.name;
                drawerState[path] = data;
            }
            return data.height;
        }

        private int FirstIndexOf(AssemblyDefinitionAsset asset)
        {
            return AssemblyHotfixManager.Instance.assemblies.FindIndex(v => v.assembly == asset);
        }
        private int GetIndexFromPropertyPath(string path)
        {
            var arr = path.Split(new string[] { "Array.data[", "]" }, StringSplitOptions.RemoveEmptyEntries);
            return Convert.ToInt32(arr[1]);
        }

        GUIContent fixButton = new GUIContent("fix", "点击以载入,否则请断开引用关系！");
        GUIContent title_Content = new GUIContent();

    }
}
