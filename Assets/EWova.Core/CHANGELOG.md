# Changelog

## [1.11.0] - 2026-11-20

### Added
- Windows DeepLink 不再需要 .NET Framework，.NET Standard 2.1 也可使用：Windows 編輯器與 Windows build 在 Api Compatibility Level 為 .NET Standard 或 .NET Framework 時都能使用，也不再受 Editor Assemblies Compatibility Level 影響。

### Changed
- `EWova.DeepLink.Win.Core.dll` 更新為 1.3.0.0，改為 netstandard2.0（原為 .NET Framework 4.7.2），登錄檔操作改以 advapi32 P/Invoke 實作；公開 API 不變。
- Windows DeepLink 的 Launcher 與 DLL 改為已數位簽章（發行者：國立高雄大學）。
- 收到 DeepLink 時寫的 log 會把 `launch_ticket`、`code` 的值換成 `***`（新增 `EWovaApp.MaskSensitiveQuery(url)`）。

### Deprecated
- `WindowsDeepLinking.IsEditorSupported` 標為 `[Obsolete]`，不再需要設定：Windows 編輯器固定為 true，其他編輯器固定為 false。

### Fixed
- 修正 1.10.1 在 Unity 2021.3 / 2022.3 編譯失敗、找不到 `WinActivatedDeepLink` 的問題（不論 Build Target 與 Api Compatibility Level）。
  - `EWova.DeepLink.Win.Core.dll.meta` 原為 Unity 6 格式（PluginImporter serializedVersion 3），舊版 Unity 讀不到平台設定而不引用此 DLL；改回 serializedVersion 2，新舊版本都能讀取。
  - DLL 原為 .NET Framework 4.7.2，Unity 在 .NET Standard profile 下對其只有有限支援；改為 netstandard2.0，兩種 Api Compatibility Level 都完整支援。
- 修正 Mac 編輯器的 Build Target 為 Windows 時，Windows DeepLink 被視為支援，呼叫 `Initialize` 會因找不到 advapi32 丟出 `DllNotFoundException`；現在只在 Windows 編輯器與 Windows build 啟用。

## [1.10.1] - 2026-10-27

### Changed
- Windows 編輯器的 DeepLink 改由 Editor Assemblies Compatibility Level 決定是否支援，不再受目前 Build Target 的 Api Compatibility Level 影響。
  - 例如 Android 使用 .NET Standard 打包時，只要 Editor Assemblies Compatibility Level 為 .NET Framework（或 Default），在 Windows 編輯器仍可跳轉登入。
  - Windows 正式 build 仍需 Standalone 的 Api Compatibility Level 為 .NET Framework。

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
