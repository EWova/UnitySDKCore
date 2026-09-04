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
                PlayerPrefs.SetInt(AutoFillEnableKey, 1);
            else
                PlayerPrefs.SetInt(AutoFillEnableKey, 0);

            PlayerPrefs.Save();
        }
        /// <summary>
        /// 判斷 AutoFill 是否啟用
        /// </summary>
        public static bool IsAutoFillActive()
        {
            if (!PlayerPrefs.HasKey(AutoFillEnableKey))
                return false;

            return PlayerPrefs.GetInt(AutoFillEnableKey, 0) == 1;
        }
        /// <summary>
        /// 清除 AutoFill 啟用狀態
        /// </summary>
        public static void ClearAutoFillActive()
        {
            PlayerPrefs.DeleteKey(AutoFillEnableKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 儲存 AutoFill 資料到 PlayerPrefs，如果 AutoFill 的屬性為 null，則不會覆蓋原本的值
        /// </summary>
        /// <param name="autoFill"></param>
        public static void SaveAutoFill(AutoFill autoFill)
        {
            if (autoFill.Method != null)
                PlayerPrefs.SetString(AutoFillMethodKey, autoFill.Method);
            if (autoFill.Email != null)
                PlayerPrefs.SetString(AutoFillEmailKey, autoFill.Email);
            if (autoFill.QuickOrg != null)
                PlayerPrefs.SetString(AutoFillQuickOrgKey, autoFill.QuickOrg);
            if (autoFill.QuickCode != null)
                PlayerPrefs.SetString(AutoFillQuickCodeKey, autoFill.QuickCode);
            PlayerPrefs.Save();
        }
        /// <summary>
        /// 從 PlayerPrefs 讀取 AutoFill 資料，如果沒有對應的值，則會回傳 null
        /// </summary>
        public static AutoFill LoadAutoFill()
        {
            var method
                = PlayerPrefs.GetString(AutoFillMethodKey, null);
            var email
                = PlayerPrefs.GetString(AutoFillEmailKey, null);
            var quickOrg
                = PlayerPrefs.GetString(AutoFillQuickOrgKey, null);
            var quickCode
                = PlayerPrefs.GetString(AutoFillQuickCodeKey, null);
            return new AutoFill(method, email, quickOrg, quickCode);
        }
        /// <summary>
        /// 清除 AutoFill 資料
        /// </summary>
        public static void ClearAutoFill()
        {
            PlayerPrefs.DeleteKey(AutoFillMethodKey);
            PlayerPrefs.DeleteKey(AutoFillEmailKey);
            PlayerPrefs.DeleteKey(AutoFillQuickOrgKey);
            PlayerPrefs.DeleteKey(AutoFillQuickCodeKey);
            PlayerPrefs.Save();
        }
    }
}
