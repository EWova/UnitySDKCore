using UnityEngine;

namespace EWova.Auth
{
    public static class LocalStorage
    {
        public const string AutoFillEnableKey = "ewova_autofill_enable";
        public const string AutoFillMethodKey = "ewova_autofill_method";
        public const string AutoFillEmailKey = "ewova_autofill_email";
        public const string AutoFillQuickOrgKey = "ewova_autofill_quickOrg";
        public const string AutoFillQuickCodeKey = "ewova_autofill_quickCode";

        /// <summary>
        /// 設定 AutoFill 是否啟用
        /// </summary>
        public static void SetAutoFillActive(bool enable)
        {
            if (enable)
                PrefsProvider.SetInt(AutoFillEnableKey, 1);
            else
                PrefsProvider.SetInt(AutoFillEnableKey, 0);

            PrefsProvider.Save();
        }
        /// <summary>
        /// 判斷 AutoFill 是否啟用
        /// </summary>
        public static bool IsAutoFillActive()
        {
            if (!PrefsProvider.HasKey(AutoFillEnableKey))
                return false;

            return PrefsProvider.GetInt(AutoFillEnableKey, 0) == 1;
        }
        /// <summary>
        /// 清除 AutoFill 啟用狀態
        /// </summary>
        public static void ClearAutoFillActive()
        {
            PrefsProvider.DeleteKey(AutoFillEnableKey);
            PrefsProvider.Save();
        }

        /// <summary>
        /// 儲存 AutoFill 資料到 PlayerPrefs，如果 AutoFill 的屬性為 null，則不會覆蓋原本的值
        /// </summary>
        /// <param name="autoFill"></param>
        public static void SaveAutoFill(AutoFill autoFill)
        {
            if (autoFill.Method != null)
                PrefsProvider.SetString(AutoFillMethodKey, autoFill.Method);
            if (autoFill.Email != null)
                PrefsProvider.SetString(AutoFillEmailKey, autoFill.Email);
            if (autoFill.QuickOrg != null)
                PrefsProvider.SetString(AutoFillQuickOrgKey, autoFill.QuickOrg);
            if (autoFill.QuickCode != null)
                PrefsProvider.SetString(AutoFillQuickCodeKey, autoFill.QuickCode);
            PrefsProvider.Save();
        }
        /// <summary>
        /// 從 PlayerPrefs 讀取 AutoFill 資料，如果沒有對應的值，則會回傳 null
        /// </summary>
        public static AutoFill LoadAutoFill()
        {
            var method
                = PrefsProvider.GetString(AutoFillMethodKey, null);
            var email
                = PrefsProvider.GetString(AutoFillEmailKey, null);
            var quickOrg
                = PrefsProvider.GetString(AutoFillQuickOrgKey, null);
            var quickCode
                = PrefsProvider.GetString(AutoFillQuickCodeKey, null);
            return new AutoFill(method, email, quickOrg, quickCode);
        }
        /// <summary>
        /// 清除 AutoFill 資料
        /// </summary>
        public static void ClearAutoFill()
        {
            PrefsProvider.DeleteKey(AutoFillMethodKey);
            PrefsProvider.DeleteKey(AutoFillEmailKey);
            PrefsProvider.DeleteKey(AutoFillQuickOrgKey);
            PrefsProvider.DeleteKey(AutoFillQuickCodeKey);
            PrefsProvider.Save();
        }
    }
}
