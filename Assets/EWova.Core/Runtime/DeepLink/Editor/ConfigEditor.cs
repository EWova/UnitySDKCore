using System.Text.RegularExpressions;

using UnityEditor;

using UnityEngine;

namespace EWova.DeepLink.Editor
{
    [CustomEditor(typeof(DeepLinkConfig))]
    public class ConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (DeepLinkConfig)target;

            // 使用 SerializedObject 處理序列化屬性，能完美支援 Unity 的 Undo (Ctrl+Z)
            serializedObject.Update();
            SerializedProperty schemeProp = serializedObject.FindProperty("MyAppScheme");

            EditorGUILayout.Space();

            // 1. 顯示設定指南 HelpBox
            EditorGUILayout.HelpBox(@"應用程式 Deep Link Scheme 設定指南

請輸入用於啟動應用程式的 Scheme（例如：輸入 'myapp' 對應 'myapp://'）。

【格式與限制規則】
1. 字元限制：僅允許小寫英文字母 (a-z)、數字 (0-9) 與點號 (.)。
2. 開頭限制：必須以小寫英文字母開頭（不可為數字或點號）。
3. 結尾限制：必須以小寫英文字母或數字結尾（不可為點號）。
4. 符號限制：禁止使用大寫字母、空白及任何特殊符號（如 _、-、/ 等）。
5. 點號限制：點號 (.) 不可連續出現（例如禁止 'my..app'）。

【正確範例】
• myapp
• com.company.app
• my.app.v1",
                MessageType.Info
            );

            EditorGUILayout.Space();

            // 2. 欄位輸入與變更檢查
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(schemeProp, new GUIContent("My App Scheme"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            // 如果欄位不是空的，才進行標準驗證（若為空則適用預設值，不擋它）
            bool isValid = config.VerifyFormat(out string errorMsg);
            string currentScheme = schemeProp.stringValue;

            // 4. 根據驗證結果顯示對應的 UI
            if (!isValid)
            {
                // 顯示錯誤紅框
                EditorGUILayout.HelpBox(errorMsg, MessageType.Error);

                // 顯示黃色「自動修正」按鈕
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("嘗試自動修正格式", GUILayout.Height(24)))
                {
                    // 自動修正字串
                    string fixedStr = currentScheme.ToLower();
                    fixedStr = Regex.Replace(fixedStr, "[^a-z0-9.]", ""); // 移除非法字元
                    fixedStr = Regex.Replace(fixedStr, "^[^a-z]+", "");   // 確保字母開頭
                    while (fixedStr.Contains("..")) fixedStr = fixedStr.Replace("..", "."); // 處理連續點
                    fixedStr = fixedStr.TrimEnd('.'); // 移除結尾點

                    // 將修正後的數值寫回 Property
                    schemeProp.stringValue = fixedStr;
                    serializedObject.ApplyModifiedProperties();
                    GUI.FocusControl(null); // 移除欄位聚焦以即時重繪 UI
                }
                GUI.backgroundColor = Color.white; // 還原 UI 顏色
            }
            else if (!string.IsNullOrEmpty(currentScheme))
            {
                // 驗證成功且有輸入值，顯示綠色勾勾或提示
                EditorGUILayout.HelpBox($"格式正確\n預覽: {currentScheme}://", MessageType.None);
            }
            else
            {
                string defaultScheme = DeepLinkConfig.GetDefaultScheme();
                schemeProp.stringValue = defaultScheme;
                serializedObject.ApplyModifiedProperties();
                GUI.FocusControl(null); // 移除欄位聚焦以即時重繪 UI
            }

            // 5. 確保物件有被標記 Dirty 並儲存
            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
    }
}