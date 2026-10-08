# Desktop Life 0.10 安裝與驗收

正式版本：0.10.0-20261002-202540

安裝檔：%USERPROFILE%\AppData\Local\Programs\DesktopLife\0.10.0-20261002-202540\DesktopLife.exe

401 項測試、17 個分析器案例、WPF、611.57 秒三角色壓力測試與封裝執行檔檢查通過。沒有非預期導航失敗、卡住或恢復逾時；移動家具造成的一次恢復已完成。

實際存檔已核對：schema6，栗子與原次角色身分、名字、學習偏好、作品、170 次獎勵、4 次懲罰、9 件物件均保留。三角色保存、重啟與兩個捷徑通過。玩具可依物理移動，固定家具允許1像素內的邊界調整。

完整原生資料備份：%USERPROFILE%\AppData\Local\DesktopLife\upgrade-backups\before-0.10-20261002-202636

最近安裝復原入口：D:\APP\GPT專用\Desktop Life\artifacts\recovery\0.10-20261002-202636

0.9 歷史復原入口：artifacts/recovery/0.10-20261002-195550。使用保留的原生 schema5 歷史世代，詳見其中 RECOVERY-NOTE.txt。

Windows Store 封裝子程序原先讀到另一份舊快取；升級與復原已改用原生工作程序，與正式程式一致。兩份歷史均保留，沒有合併或覆蓋較新的原生歷史。

短測不能證明長期沒有記憶體洩漏。兩小時、混合 DPI、完整家具插畫與連續動畫仍待驗收。復原入口未在真實資料上演練降版；磁碟或權限故障的自動補償列入後續改善。

證據：artifacts/verification/0.10/FINAL_STANDARD.md、NATIVE_INSTALLATION.json、PERFORMANCE.md。
