using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace qBittorrentBlockXunlei
{
    internal class Program
    {
        // 時間相關常數
        static readonly int iPauseBeforeExitMs = 2000;
        static double dLoopIntervalSeconds = 10;

        // 進度檢查單位 (bytes)
        static readonly long lProgressCheckBoundarySize = 30 * 1024 * 1024;

        static readonly Encoding eOutput = Console.OutputEncoding;

        static readonly HttpClient client = new HttpClient();

        static readonly string sAuth_login = "/api/v2/auth/login";
        static readonly string sApp_webapiVersion = "/api/v2/app/webapiVersion";
        static readonly string sApp_setPreferences = "/api/v2/app/setPreferences";
        static readonly string sSync_maindata = "/api/v2/sync/maindata";
        static readonly string sSync_torrentPeers = "/api/v2/sync/torrentPeers?hash=";
        static readonly string sTransfer_banpeers = "/api/v2/transfer/banPeers";
        static readonly string sTorrentsTrackers = "/api/v2/torrents/trackers?hash=";
        static readonly string sTorrentsProperties = "/api/v2/torrents/properties?hash=";

        static readonly string sFullUpdateText = "\"full_update\":";
        static readonly string sTorrentsObjectText = "\"torrents\":{";
        static readonly string sUpspeedFieldText = "\"upspeed\":";

        static readonly string sTorrentPeersStartText = "{\"full_update\":";
        static readonly string sPeersObjectText = "\"peers\":{";
        static readonly string sClientFieldText = "\"client\":\"";
        static readonly string sCountryCodeFieldText = "\"country_code\":\"";
        static readonly string sDownloadedFieldText = "\"downloaded\":";
        static readonly string sFlagsFieldText = "\"flags\":\"";
        static readonly string sPortFieldText = "\"port\":";
        static readonly string sProgressFieldText = "\"progress\":";
        static readonly string sUploadedFieldText = "\"uploaded\":";
        static readonly string sTotalSizeFieldText = "\"total_size\":";
        static readonly string sPieceSizeFieldText = "\"piece_size\":";

        static readonly List<string> lsLeechClients = new List<string>() { "-XL", "Xunlei", "XunLei", "7.", "aria2", "Xfplay", "dandanplay", "FDM", "go.torrent", "Mozilla", "github.com/anacrolix/torrent (devel) (anacrolix/torrent unknown)", "dt/torrent/", "Taipei-Torrent dev", "trafficConsume", "hp/torrent/", "BitComet 1.92", "BitComet 1.98", "xm/torrent/", "flashget", "FlashGet", "Unknown", "GT", "StellarPlayer", "Gopeed dev", "BM", "DT", "HP", "MediaGet", "MS", "WW" };
        static readonly List<string> lsAncientClients = new List<string>() { "TorrentStorm", "Azureus 1.", "Azureus 2.", "Azureus 3.", "Deluge 0.", "Deluge 1.0", "Deluge 1.1", "qBittorrent 0.", "qBittorrent 1.", "qBittorrent 2.", "Transmission 0.", "Transmission 1.", "BitComet 0." };

        static void CCEHandler(object sender, ConsoleCancelEventArgs args)
        {
            if ((sender != null) || (args != null))
                Console.WriteLine(Environment.NewLine + "Ctrl-C is pressed!");
            Console.OutputEncoding = eOutput;
            Thread.Sleep(iPauseBeforeExitMs);
            Environment.Exit(0);
        }

        static async Task Main(string[] args)
        {
            Console.Title = "qBittorrentBlockXunlei v240830";

            Console.OutputEncoding = Encoding.UTF8;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(CCEHandler);

            bool bRemoteServer = false;

            string sTargetServer = "http://localhost:";
            string sTargetPort = "";
            string sTargetUsername = "";
            string sTargetPassword = "";

            // 讀取參數
            if (args.Length > 0)
            {
                int iArgumentIndex = 0;
                int iColonIndex = args[0].IndexOf(':');

                if (iColonIndex != -1)
                {
                    sTargetPort = args[0].Substring(iColonIndex + 1);
                    if ((args.Length >= 3) && int.TryParse(sTargetPort, out int j) && (j > 0) && (j <= 65535))
                    {
                        if (!args[0].StartsWith("http://"))
                            sTargetServer = "http://" + args[0];
                        else
                            sTargetServer = args[0];
                        sTargetUsername = args[1];
                        sTargetPassword = args[2];
                        bRemoteServer = true;

                        iArgumentIndex += 3;
                    }
                    else
                    {
                        Console.WriteLine("illegal server address: " + args[0]);
                        CCEHandler(null, null);
                    }
                }
                else if (int.TryParse(args[0], out int k) && (k > 0) && (k <= 65535))
                {
                    sTargetPort = args[0];
                    sTargetServer += sTargetPort;

                    iArgumentIndex += 1;
                }

                for (int i = iArgumentIndex; i < args.Length; ++i)
                {
                    if (args[i].Equals("/i", StringComparison.OrdinalIgnoreCase) || args[i].Equals("-i", StringComparison.OrdinalIgnoreCase))
                    {
                        if (((i + 1) < args.Length) && double.TryParse(args[i + 1], out double d) && (d >= 0))
                        {
                            dLoopIntervalSeconds = d;

                            i += 1;
                            Console.WriteLine("loop interval: " + dLoopIntervalSeconds + " sec.");
                        }
                        else
                        {
                            Console.WriteLine("illegal loop interval argument!");
                        }
                    }
                }
            }

            // 取得 port number
            while (sTargetPort == "")
            {
                Console.Write("Port Number = ");
                args = Console.ReadLine().Split(' ');
                if ((args.Length > 0) && int.TryParse(args[0], out int i) && (i > 0) && (i <= 65535))
                {
                    sTargetPort = args[0];
                    sTargetServer += sTargetPort;
                }
            }

            Console.Title += "          " + sTargetServer;
            Console.WriteLine("server address:\t\t" + sTargetServer);

            if (bRemoteServer)
            {
                try
                {
                    (await client.PostAsync(sTargetServer + sAuth_login, new FormUrlEncodedContent(new Dictionary<string, string>() { { "username", sTargetUsername }, { "password", sTargetPassword } }))).EnsureSuccessStatusCode();
                }
                catch
                {
                    Console.WriteLine("Can't login to remote server " + sTargetServer + ", please check related settings again!");
                    CCEHandler(null, null);
                }
            }

            double dLoopIntervalMs = dLoopIntervalSeconds * 1000;
            int iLoopIntervalMs = (int)Math.Round(dLoopIntervalMs);

            string responseBody;
            string peersBody;

            int iResponseStartIndex;
            int iResponseEndIndex;
            int iPeersStartIndex;
            int iPeersEndIndex;

            StringBuilder sbBanPeers = new StringBuilder();

            // 取得 WebAPI 版本
            responseBody = "";
            while (responseBody == "")
            {
                try
                {
                    responseBody = await client.GetStringAsync(sTargetServer + sApp_webapiVersion);
                }
                catch
                {
                    Console.WriteLine("Can't connect to qBittorrent WebUI, wait " + dLoopIntervalSeconds + " sec. to reconnect!");
                    Thread.Sleep(iLoopIntervalMs);
                }
            }

            // WebAPI 版本需 >= 2.3
            {
                string[] saVersion = responseBody.Split('.');
                if ((saVersion.Length < 2) || !int.TryParse(saVersion[0], out int iMajorVersion) || (iMajorVersion < 2) || !int.TryParse(saVersion[1], out int iMinorVersion) || ((iMajorVersion == 2) && (iMinorVersion < 3)))
                {
                    Console.WriteLine("Please upgrade your qBittorrent first!");
                    CCEHandler(null, null);
                }
            }

            DateTime dtLastResetTime = DateTime.Now;
            Console.WriteLine(dtLastResetTime + ", Reset banned IPs!");
            var response = await client.PostAsync(sTargetServer + sApp_setPreferences, new FormUrlEncodedContent(new Dictionary<string, string>() { { "json", "{\"banned_IPs\":\"\"}" } }));

            string[] saStatusToken = new string[] { ",\"status\":" };
            Dictionary<string, bool> dPublicTorrents = new Dictionary<string, bool>();
            Dictionary<string, long> dTorrentSizes = new Dictionary<string, long>();
            Dictionary<string, long> dTorrentPieceSizes = new Dictionary<string, long>();
            Dictionary<string, Dictionary<string, decimal>> dTorrentPeerProgresses = new Dictionary<string, Dictionary<string, decimal>>();
            HashSet<string> hsActiveTorrents = new HashSet<string>();

            string sDecimalFormat = "#0.###";

            while (true)
            {
                try
                {
                    DateTime dtResetBase = DateTime.Now;

                    TimeSpan ts = dtResetBase - dtLastResetTime;
                    if (ts.Days >= 1)
                    {
                        dtLastResetTime = dtResetBase;
                        Console.WriteLine(dtLastResetTime + ", Reset banned IPs, reset interval: " + ts.TotalDays + " days");
                        response = await client.PostAsync(sTargetServer + sApp_setPreferences, new FormUrlEncodedContent(new Dictionary<string, string>() { { "json", "{\"banned_IPs\":\"\"}" } }));
                    }

                    int iTorrentCount = 0;
                    int iPublicTorrentCount = 0;

                    // 取得 torrent hash
                    responseBody = "";
                    while (responseBody == "")
                    {
                        try
                        {
                            if (bRemoteServer)
                                (await client.PostAsync(sTargetServer + sAuth_login, new FormUrlEncodedContent(new Dictionary<string, string>() { { "username", sTargetUsername }, { "password", sTargetPassword } }))).EnsureSuccessStatusCode();
                            responseBody = await client.GetStringAsync(sTargetServer + sSync_maindata);
                        }
                        catch
                        {
                            Console.WriteLine("Can't connect to qBittorrent WebUI, wait " + dLoopIntervalSeconds + " sec. to reconnect!");
                            Thread.Sleep(iLoopIntervalMs);
                        }
                    }

                    iResponseStartIndex = responseBody.IndexOf(sTorrentsObjectText);
                    if ((iResponseStartIndex == -1) || !responseBody.Substring(0, iResponseStartIndex).Contains(sFullUpdateText))
                    {
                        Console.WriteLine("Can't get torrents info!");
                        throw new Exception("Can't get torrents info!");
                    }

                    iResponseStartIndex += sTorrentsObjectText.Length;

                    while (iResponseStartIndex != -1)
                    {
                        ++iResponseStartIndex;
                        iResponseEndIndex = responseBody.IndexOf('"', iResponseStartIndex);
                        string sTorrentHash = responseBody.Substring(iResponseStartIndex, iResponseEndIndex - iResponseStartIndex);
                        ++iTorrentCount;

                        if (!dPublicTorrents.ContainsKey(sTorrentHash))
                        {
                            string trackersBody = await client.GetStringAsync(sTargetServer + sTorrentsTrackers + sTorrentHash);
                            string[] sa = trackersBody.Split(saStatusToken, StringSplitOptions.None);

                            // Private Tracker: DHT is disabled & Trackers <= 3
                            if ((sa[1][0] == '0') && (sa.Length >= 5) && (sa.Length <= 7))
                            {
                                dPublicTorrents[sTorrentHash] = false;
                            }
                            else
                            {
                                dPublicTorrents[sTorrentHash] = true;

                                iResponseStartIndex = responseBody.IndexOf(sTotalSizeFieldText, iResponseEndIndex) + sTotalSizeFieldText.Length;
                                iResponseEndIndex = responseBody.IndexOf(',', iResponseStartIndex);
                                dTorrentSizes[sTorrentHash] = long.Parse(responseBody.Substring(iResponseStartIndex, iResponseEndIndex - iResponseStartIndex));

                                string propertiesBody = await client.GetStringAsync(sTargetServer + sTorrentsProperties + sTorrentHash);
                                int iStartIndex = propertiesBody.IndexOf(sPieceSizeFieldText) + sPieceSizeFieldText.Length;
                                dTorrentPieceSizes[sTorrentHash] = long.Parse(propertiesBody.Substring(iStartIndex, propertiesBody.IndexOf(',', iStartIndex) - iStartIndex));
                            }
                        }

                        // 取得 peers (僅限於非 PT 的種子)
                        if (dPublicTorrents[sTorrentHash])
                        {
                            ++iPublicTorrentCount;

                            hsActiveTorrents.Add(sTorrentHash);
                            if (!dTorrentPeerProgresses.ContainsKey(sTorrentHash))
                                dTorrentPeerProgresses[sTorrentHash] = new Dictionary<string, decimal>();
                            Dictionary<string, decimal> dPeerProgresses = dTorrentPeerProgresses[sTorrentHash];

                            Dictionary<string, HashSet<string>> dPeerNetworks = new Dictionary<string, HashSet<string>>();
                            Dictionary<string, string> dPeerToClients = new Dictionary<string, string>();

                            peersBody = await client.GetStringAsync(sTargetServer + sSync_torrentPeers + sTorrentHash);

                            iPeersStartIndex = peersBody.IndexOf(sPeersObjectText);
                            if ((iPeersStartIndex == -1) || !peersBody.StartsWith(sTorrentPeersStartText))
                            {
                                Console.WriteLine("Can't get peers list!");
                                break;
                            }

                            iPeersStartIndex += sPeersObjectText.Length;

                            while (iPeersStartIndex != -1)
                            {
                                ++iPeersStartIndex;
                                iPeersEndIndex = peersBody.IndexOf('"', iPeersStartIndex);

                                // 無 peer
                                if (iPeersEndIndex - iPeersStartIndex == 1)
                                    break;

                                string sPeer = peersBody.Substring(iPeersStartIndex, iPeersEndIndex - iPeersStartIndex);

                                iPeersStartIndex = peersBody.IndexOf(sClientFieldText, iPeersEndIndex) + sClientFieldText.Length;
                                iPeersEndIndex = peersBody.IndexOf('"', iPeersStartIndex);
                                string sClient = peersBody.Substring(iPeersStartIndex, iPeersEndIndex - iPeersStartIndex);

                                iPeersStartIndex = peersBody.IndexOf(sCountryCodeFieldText, iPeersEndIndex) + sCountryCodeFieldText.Length;
                                iPeersEndIndex = peersBody.IndexOf('"', iPeersStartIndex);
                                string sCountryCode = peersBody.Substring(iPeersStartIndex, iPeersEndIndex - iPeersStartIndex);

                                iPeersStartIndex = peersBody.IndexOf(sDownloadedFieldText, iPeersEndIndex) + sDownloadedFieldText.Length;
                                iPeersEndIndex = peersBody.IndexOf(',', iPeersStartIndex);
                                long lDownloaded = long.Parse(peersBody.Substring(iPeersStartIndex, iPeersEndIndex - iPeersStartIndex));

                                iPeersStartIndex = peersBody.IndexOf(sFlagsFieldText, iPeersEndIndex) + sFlagsFieldText.Length;
                                iPeersEndIndex = peersBody.IndexOf('"', iPeersStartIndex);
                                string sFlags = peersBody.Substring(iPeersStartIndex, iPeersEndIndex - iPeersStartIndex);

                                iPeersStartIndex = peersBody.IndexOf(sPortFieldText, iPeersEndIndex) + sPortFieldText.Length;
                                iPeersEndIndex = peersBody.IndexOf(",", iPeersStartIndex);
                                int iPort = int.Parse(peersBody.Substring(iPeersStartIndex, iPeersEndIndex - iPeersStartIndex));

                                iPeersStartIndex = peersBody.IndexOf(sProgressFieldText, iPeersEndIndex) + sProgressFieldText.Length;
                                iPeersEndIndex = peersBody.IndexOf(",", iPeersStartIndex);
                                decimal dmProgress = decimal.Parse(peersBody.Substring(iPeersStartIndex, iPeersEndIndex - iPeersStartIndex), System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowExponent);

                                iPeersStartIndex = peersBody.IndexOf(sUploadedFieldText, iPeersEndIndex) + sUploadedFieldText.Length;
                                iPeersEndIndex = peersBody.IndexOf('}', iPeersStartIndex);
                                long lUploaded = long.Parse(peersBody.Substring(iPeersStartIndex, iPeersEndIndex - iPeersStartIndex));

                                // 找下一個 peer：先找每個 peer 最後一欄 uploaded，再看結尾字串是 "}," 或 "}}"
                                iPeersStartIndex = iPeersEndIndex + 1;
                                if (peersBody[iPeersStartIndex] == ',')
                                    ++iPeersStartIndex;
                                else
                                    iPeersStartIndex = -1;

                                // 判斷是否要 ban 該 peer
                                bool bBanPeer = false;

                                // 判斷 peer 所屬的 network
                                {
                                    char cIpGroupSeparator = '.';
                                    string sNetwork = sPeer;
                                    if (sPeer.StartsWith("[::ffff:"))
                                        sNetwork = sNetwork.Substring("[::ffff:".Length);
                                    else if (sPeer[0] == '[')
                                        cIpGroupSeparator = ':';
                                    int Iindex = 0;
                                    for (int i = 0; i < 3; ++i)
                                        Iindex = sNetwork.IndexOf(cIpGroupSeparator, Iindex) + 1;
                                    sNetwork = sNetwork.Substring(0, Iindex);
                                    if (!dPeerNetworks.ContainsKey(sNetwork))
                                        dPeerNetworks[sNetwork] = new HashSet<string>();
                                    dPeerNetworks[sNetwork].Add(sPeer);
                                    dPeerToClients[sPeer] = sClient;
                                }

                                // 對方回報的進度是 0 或 對方未曾上傳過
                                if ((dmProgress == 0) || ((lDownloaded == 0) && (dmProgress != 1)))
                                {
                                    if (sClient != "")
                                    {
                                        if (sClient.Length < 4)
                                        {
                                            Console.WriteLine("Banned - ??? Client:     " + sClient + ", " + sPeer);
                                            bBanPeer = true;
                                        }
                                        else
                                        {
                                            foreach (string sLeechClient in lsLeechClients)
                                            {
                                                if (sClient.StartsWith(sLeechClient))
                                                {
                                                    Console.WriteLine("Banned - Leech Client:   " + sClient + ", " + sPeer);
                                                    bBanPeer = true;
                                                    break;
                                                }
                                            }

                                            if (!bBanPeer)
                                            {
                                                foreach (string sAncientClient in lsAncientClients)
                                                {
                                                    if (sClient.StartsWith(sAncientClient))
                                                    {
                                                        Console.WriteLine("Banned - Ancient Client: " + sClient + ", " + sPeer);
                                                        bBanPeer = true;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    /*
                                    // 可疑的 port
                                    if ((iPort == 12345) || (iPort == 2011) || (iPort == 2013) || (iPort == 54321) || (iPort == 15000) || (iPort < 2100))
                                    {
                                        Console.WriteLine("Banned by Port: " + iPort + ", " + sPeer);
                                        sbBanPeers.Append(sPeer + "|");
                                    }
                                    */
                                }

                                if (!bBanPeer && (sFlags.IndexOf('U') != -1) && (dTorrentSizes[sTorrentHash] > 0))
                                {
                                    // 上傳量 > 種子實際大小
                                    if (lUploaded > (dTorrentSizes[sTorrentHash] + 2 * dTorrentPieceSizes[sTorrentHash]))
                                    {
                                        Console.WriteLine("Banned - Uploaded " + ((decimal)lUploaded / 1024 / 1024).ToString(sDecimalFormat) + " MB > Torrent size " + ((decimal)dTorrentSizes[sTorrentHash] / 1024 / 1024).ToString(sDecimalFormat) + " MB: " + sClient + ", " + sPeer);
                                        bBanPeer = true;
                                    }
                                    // 上傳了 10 MB 後，對方回報的進度仍為 0
                                    else if ((dmProgress == 0) && (lUploaded > 10 * 1024 * 1024) && (lUploaded > (2 * dTorrentPieceSizes[sTorrentHash])))
                                    {
                                        Console.WriteLine("Banned - Uploaded > 10 MB & Progress = 0%: " + sClient + ", " + sPeer);
                                        bBanPeer = true;
                                    }
                                    // 記錄各 peer 最初的進度
                                    else if (!dPeerProgresses.ContainsKey(sPeer))
                                    {
                                        decimal dmInitialProgress = dmProgress - (decimal)lUploaded / dTorrentSizes[sTorrentHash];
                                        if (dmInitialProgress < 0)
                                            dmInitialProgress = 0;
                                        dPeerProgresses[sPeer] = dmInitialProgress;
                                    }
                                    // 現在進度 < 最初進度
                                    else if (dmProgress < dPeerProgresses[sPeer])
                                    {
                                        Console.WriteLine("Banned - Progress = " + (dmProgress * 100).ToString(sDecimalFormat) + "% < initial = " + (dPeerProgresses[sPeer] * 100).ToString(sDecimalFormat) + "%: " + sClient + ", " + sPeer);
                                        bBanPeer = true;
                                    }
                                    // 預估進度 > 對方回報的進度，預估進度 = (上傳量 - 容許誤差) / 種子實際大小
                                    else if ((dTorrentSizes[sTorrentHash] > lProgressCheckBoundarySize) && ((lUploaded - lProgressCheckBoundarySize) > (dTorrentSizes[sTorrentHash] * (dmProgress - dPeerProgresses[sPeer]))))
                                    {
                                        Console.WriteLine("Banned - Uploaded = " + ((decimal)lUploaded * 100 / dTorrentSizes[sTorrentHash]).ToString(sDecimalFormat) + "% > Progress = " + ((dmProgress - dPeerProgresses[sPeer]) * 100).ToString(sDecimalFormat) + "%: " + sClient + ", " + sPeer);
                                        bBanPeer = true;
                                    }
                                }

                                if (bBanPeer)
                                    sbBanPeers.Append(sPeer + "|");
                            }

                            foreach (string sNetwork in new List<string>(dPeerNetworks.Keys))
                            {
                                // 同一 network 下有 >= 5 個不同 IP 連線
                                if (dPeerNetworks[sNetwork].Count >= 5)
                                {
                                    foreach (string sPeer in dPeerNetworks[sNetwork])
                                    {
                                        Console.WriteLine("Banned - Same Network Clients: " + dPeerToClients[sPeer] + ", " + sPeer);
                                        sbBanPeers.Append(sPeer + "|");
                                    }
                                }
                                dPeerNetworks[sNetwork].Clear();
                            }
                            dPeerNetworks.Clear();

                            // 某些情況下，有可能 "種子實際大小 = -1"，移除種子相關紀錄，待下一輪重新檢測
                            if (dTorrentSizes[sTorrentHash] <= 0)
                            {
                                dPublicTorrents.Remove(sTorrentHash);
                                dTorrentSizes.Remove(sTorrentHash);
                                dTorrentPieceSizes.Remove(sTorrentHash);
                                dPeerProgresses.Clear();
                                dTorrentPeerProgresses.Remove(sTorrentHash);
                            }
                        }

                        // 找下一個 torrent hash：先找每個 torrent 最後一欄 upspeed，再看結尾字串是 "}," 或 "}}"
                        iResponseStartIndex = responseBody.IndexOf(sUpspeedFieldText, iResponseEndIndex);
                        if (iResponseStartIndex != -1)
                        {
                            iResponseStartIndex = responseBody.IndexOf('}', iResponseStartIndex) + 1;
                            if (responseBody[iResponseStartIndex] == ',')
                                ++iResponseStartIndex;
                            else
                                iResponseStartIndex = -1;
                        }
                    }

                    if (sbBanPeers.Length > 0)
                    {
                        sbBanPeers.Remove(sbBanPeers.Length - 1, 1);

                        response = await client.PostAsync(sTargetServer + sTransfer_banpeers, new FormUrlEncodedContent(new Dictionary<string, string>() { { "peers", sbBanPeers.ToString() } }));
                        sbBanPeers.Clear();
                    }

                    // 移除非作用中的種子
                    foreach (string sTorrentHash in new List<string>(dTorrentSizes.Keys))
                    {
                        if (!hsActiveTorrents.Contains(sTorrentHash))
                        {
                            dPublicTorrents.Remove(sTorrentHash);
                            dTorrentSizes.Remove(sTorrentHash);
                            dTorrentPieceSizes.Remove(sTorrentHash);
                            dTorrentPeerProgresses[sTorrentHash].Clear();
                            dTorrentPeerProgresses.Remove(sTorrentHash);
                        }
                    }
                    hsActiveTorrents.Clear();

                    DateTime dtNow = DateTime.Now;
                    TimeSpan tsLoopCost = dtNow - dtResetBase;
                    Console.WriteLine(dtNow + ", all/pt/bt: " + iTorrentCount + "/" + (iTorrentCount - iPublicTorrentCount) + "/" + iPublicTorrentCount + ", interval: " + dLoopIntervalSeconds + " sec., cost: " + tsLoopCost.TotalSeconds + " sec.");

                    int iSleepMs = (int)Math.Round(dLoopIntervalMs - tsLoopCost.TotalMilliseconds);
                    if (iSleepMs > 0)
                        Thread.Sleep(iSleepMs);

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + "\t" + DateTime.Now.ToString());
                    Thread.Sleep(iPauseBeforeExitMs);
                }
            }
        }
    }
}
