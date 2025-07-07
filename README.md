一個幫 qBittorrent 阻擋迅雷 跟其它吸血 bt 客戶端的小工具

此軟體會在固定時間間隔 (預設 10 秒，可自行透過參數設定) 裡，透過 qBittorrent 的 WebUI 來獲取所有 torrent 的所有客戶端資訊

然後按照預定規則，找出迅雷及其它吸血客戶端，回報它們的 IP 給 qBittorrent 去阻擋


目前規則是

1. 該用戶進度為 0，或是從該用戶下載到的量是 0，而且用戶客戶端為 -XL*, Xunlei, 7.*, aria2, Xfplay, dandanplay, FDM, go.torrent, Mozilla, dt/torrent/*, github.com/anacrolix/torrent (devel) (anacrolix/torrent unknown), Taipei-Torrent dev, trafficConsume, hp/torrent/*, BitComet 1.92 & 1.98, xm/torrent/*, FlashGet, Unknown *, StellarPlayer *, Gopeed *, MediaGet*, aD/*, ADM, coc_coc_browser, FileCroc, filecxx, Folx, seanime (devel) (anacrolix/torrent *, HitomiDownloader, gateway (devel) (anacrolix/torrent *, offline-download (devel) (anacrolix/torrent *, QQDownload

2. 上古用戶端: Azureus (3.* 及以前版本: 2008/7)、Deluge (1.1.* 及以前版本: 2009/1)、qBittorrent (2.* 及以前版本: 2012/7)、TorrentStorm (最後一版: 2005/3)、Transmission (1.* 及以前版本: 2010/5)、BitComet (0.*: 2008/2)、µTorrent (1.*：2009/10)

3. 用戶端名字 < 4 個字，或用戶端含有非 ASCII 可顯示字元(µ、μ 除外)，或用戶端名字類似 "GT 0.0.0.2"、"HP 0.0.0.1" 這種型式

4. 該用戶進度為 0，已上傳給該用戶 10M 以上的量

5. 已上傳給該用戶，超過種子內容大小的數據

6. 該用戶回報的進度與上傳量不成比例

7. 對於連線到同一種子的 "同一網段的所有客戶端" (如：223.241.234.* 或 [240e:660:150c:*])，如果客戶端的總數 >= 5 時，則同時封鎖這些客戶端，並封鎖之後新連入的同網段客戶端

8. 若同一網段下陸陸續續已有 5 個客戶端被封鎖，直接封鎖全網段

9. 在程式啟動時，以及每隔 1 天會清空所有被擋的用戶 IP，避免永久封鎖可能會造成的誤鎖




程式截圖
![image](https://github.com/tonyhsie/qBittorrentBlockXunlei/assets/52758827/1697a7db-f2f9-4547-883c-790d7913f4dc)




[使用需知]

1. 需先設定 qBittorrent 的 WebUI，從 qBittorrent 的「工具」->「選項」裡選擇「WebUI」
　然後按照下圖設定

![image](https://github.com/tonyhsie/qBittorrentBlockXunlei/assets/52758827/abf6fed3-01a1-4b74-8484-09d11d360145)


2. 執行 qBittorrentBlockXunlei.exe 即可

3. 若要停止執行，可直接關掉視窗，或按熱鍵 ctrl-c

4. 可自行設定掃描的時間間隔，語法：qBittorrentBlockXunlei [埠號] [/i 間隔秒數]

5. 可由本機遙控遠端的 qBittorrent，語法：qBittorrentBlockXunlei [遠端位址:埠號 "帳號" "密碼"] [/i 間隔秒數]

6. 如何確認此程式真的有作用？

　可使用瀏覽器打開 http://127.0.0.1:54937/api/v2/app/preferences (54937 請自行代換成你先前設定的埠號)

　這是你的 qBittorrent 設置，可在裡面搜尋 qBittorrentBlockXunlei 擋掉的任意 ip，如果有找到，表示此程式正常運作中


如果有任何想法或建議，歡迎提出來討論
