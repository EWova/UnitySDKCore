using System.Text.RegularExpressions;

using UnityEngine;

namespace EWova.DeepLink
{
    public class DeepLinkConfig : ScriptableObject
    {
        public const string ResourceName = "DeeplinkConfig";

        public static DeepLinkConfig LoadOrDefault(bool createNew = true)
        {
            DeepLinkConfig config = Resources.Load<DeepLinkConfig>(ResourceName);

            if (config == null && createNew)
            {
#if UNITY_EDITOR
                config = EditorLoadOrCreateResource(out bool isNewlyCreated);
                if (isNewlyCreated)
                {
                    var appScheme = config.MyAppScheme;
                    Debug.LogWarning($"[EWova]DeepLink 找不到 Resource/{ResourceName}，這會導致跳轉登入無法正常工作。已自動建立預設設定，AppScheme 為 {appScheme}。你可以修改 Asset 的 MyAppScheme 欄位為您專案的 Scheme 。");
                }
                return config;
#else
                Debug.LogError($"[EWova]DeepLink 找不到 Resource/{ResourceName}，這會導致跳轉登入無法正常工作。請確保已建立此 ScriptableObject 資源並設定正確的 AppScheme。");
                return null;
#endif
            }
            else
            {
                return config;
            }
        }

        /// <summary>
        /// 此應用程式的 DeepLink Scheme 
        /// </summary>
        public string MyAppScheme = "example";

        public bool VerifyFormat(out string errorMsg)
        {
            var isValid = true;
            var currentScheme = MyAppScheme;
            if (string.IsNullOrEmpty(currentScheme))
            {
                errorMsg = "不可為空：Scheme 不能為空字串，請至少設定一個小寫英文字母。";
                isValid = false;
            }
            else
            {
                if (!Regex.IsMatch(currentScheme, "^[a-z]"))
                {
                    errorMsg = "開頭錯誤：必須以小寫英文字母 (a-z) 開頭。";
                    isValid = false;
                }
                else if (!Regex.IsMatch(currentScheme, "^[a-z0-9.]+$"))
                {
                    errorMsg = "字元錯誤：僅允許小寫英文字母、數字與點號 (.)，不可包含大寫、空白或特殊符號。";
                    isValid = false;
                }
                else if (currentScheme.Contains(".."))
                {
                    errorMsg = "符號錯誤：點號 (.) 不可連續出現。";
                    isValid = false;
                }
                else if (currentScheme.EndsWith("."))
                {
                    errorMsg = "結尾錯誤：不可以點號 (.) 作為結尾。";
                    isValid = false;
                }
                else
                {
                    errorMsg = null;
                }
            }
            return isValid;
        }

#if UNITY_EDITOR
        internal static bool IsResourceExist(out DeepLinkConfig config)
        {
            string assetPath = $"Assets/Resources/{ResourceName}.asset";
            if (!System.IO.File.Exists(assetPath))
            {
                config = null;
                return false;
            }

            config = UnityEditor.AssetDatabase.LoadAssetAtPath<DeepLinkConfig>(assetPath);
            return config != null;
        }
        internal static DeepLinkConfig EditorLoadOrCreateResource(out bool isNewlyCreated)
        {
            if (IsResourceExist(out var asset))
                isNewlyCreated = false;
            else
            {
                isNewlyCreated = true;
                asset = ScriptableObject.CreateInstance<DeepLinkConfig>();
                asset.MyAppScheme = GetDefaultScheme(); // 預設使用 Bundle Identifier 作為 Scheme，並轉為小寫
                if (!System.IO.Directory.Exists("Assets/Resources"))
                    System.IO.Directory.CreateDirectory("Assets/Resources");
                UnityEditor.AssetDatabase.CreateAsset(asset, $"Assets/Resources/{ResourceName}.asset");
                UnityEditor.AssetDatabase.SaveAssets();
            }
            return asset;
        }

        internal static string GetDefaultScheme()
        {
            // 1. 強制抓取 Android 平台欄位設定的 Package Name
#pragma warning disable CS0618 // 類型或成員已經過時
            string androidId = UnityEditor.PlayerSettings.GetApplicationIdentifier(UnityEditor.BuildTargetGroup.Android);
#pragma warning restore CS0618 // 類型或成員已經過時

            // 如果 Android 有設定，且不是 Unity 預設的產物，就直接拿來用
            if (!string.IsNullOrEmpty(androidId) && androidId != "com.Company.ProductName")
            {
                return androidId.ToLower();
            }

            // 2. 如果 Android 欄位未設定，則利用 Company Name 與 Product Name 自動拼裝
            // 移除非法字元（只留下英文字母與數字）並轉為小寫
            string company = System.Text.RegularExpressions.Regex.Replace(UnityEditor.PlayerSettings.companyName.ToLower(), "[^a-z0-9]", "");
            string product = System.Text.RegularExpressions.Regex.Replace(UnityEditor.PlayerSettings.productName.ToLower(), "[^a-z0-9]", "");

            // 防呆：避免開發者完全沒有設定 Project Settings 的公司與產品名稱
            if (string.IsNullOrEmpty(company) || company == "defaultcompany")
            {
                company = "company";
            }
            if (string.IsNullOrEmpty(product))
            {
                product = "game";
            }

            // 回傳標準的反向網域格式 (全部小寫，符合 Deep Link 規範)
            return $"com.{company}.{product}";
        }
#endif
    }
}