# Changelog

## [1.10.0] - 2026-10-05

### Changed
- Windows DeepLink 改由 `DeepLinkLauncher.exe` 處理 Scheme，不再透過 wscript 執行 VBS（容易被防毒誤判，且 VBScript 已被 Windows 列為淘汰功能）。
  - Build Windows 時會自動把 `DeepLinkLauncher.exe` 複製到 exe 旁邊（`DeepLinkLauncherPostprocessBuild`），不需額外設定；整個 build 資料夾壓縮發布即可使用。
  - 啟動時若 exe 旁有 `DeepLinkLauncher.exe`，HKCU 改註冊為 `"<exe 資料夾>\DeepLinkLauncher.exe" "<scheme>" "<exe 檔名>" "%1"`，並刪除先前產生的 `<persistentDataPath>\<productName>.vbs`。
  - 冷啟動、熱觸發行為不變：啟動器只會啟動同資料夾內的 exe，並檢查 Scheme 與 URI 長度。
  - VBS 方式只保留給 Unity Editor（Unity.exe 旁不會有啟動器）。正式 build 找不到啟動器時不註冊 Scheme 並記錄錯誤（寫出腳本並註冊為網址處理程式容易被防毒誤判為惡意程式），先前註冊的 VBS 會一併移除。
- `EWova.DeepLink.Win.Core.dll` 更新為 1.2.0.0：
  - 新增 `WindowsDeepLinkingCore.LauncherFileName`。
  - `Initialize` 新增 `allowAgentFallback` 參數（原 4 參數版本維持允許退回 VBS）。
  - 新增 `Registration` 屬性回報註冊方式（`Launcher` / `Agent` / `None`）。
- `EWova.DeepLink.Win.Core.dll` 與 `DeepLinkLauncher.exe` 的原始碼在 [EWova/DeepLinkWin-dll](https://github.com/EWova/DeepLinkWin-dll)。
