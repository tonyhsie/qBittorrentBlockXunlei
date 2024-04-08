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
        static bool bBanAncientClients = true;

        // 時間相關常數
        static readonly int iPauseBeforeExitMs = 2000;
        static double dLoopIntervalSeconds = 30;

        // 進度檢查單位 (bytes)
        static readonly long lProgressCheckBoundarySize = 50 * 1024 * 1024;

        static readonly Encoding eOutput = Console.OutputEncoding;

        static readonly HttpClient client = new HttpClient();

        static readonly string sAuth_login = "/api/v2/auth/login";
        static readonly string sApp_webapiVersion = "/api/v2/app/webapiVersion";
        static readonly string sApp_setPreferences = "/api/v2/app/setPreferences";
        static readonly string sSync_maindata = "/api/v2/sync/maindata";
        static readonly string sSync_torrentPeers = "/api/v2/sync/torrentPeers?hash=";
        static readonly string sTransfer_banpeers = "/api/v2/transfer/banPeers";
        static readonly string sTorrentsTrackers = "/api/v2/torrents/trackers?hash=";

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

        static readonly List<string> lsLeechClients = new List<string>() { "-XL", "Xunlei", "7.", "aria2", "Xfplay", "dandanplay", "FDM", "go.torrent", "Mozilla", "github.com/anacrolix/torrent (devel) (anacrolix/torrent unknown)", "dt/torrent/", "Taipei-Torrent dev", "trafficConsume" };
        static readonly List<string> lsAncientClients = new List<string>() { "TorrentStorm", "Azureus", "Deluge 0.", "Deluge 1.0", "Deluge 1.1", "qBittorrent 0.", "qBittorrent 1.", "qBittorrent 2.", "Transmission 0.", "Transmission 1." };

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
            Console.Title = "qBittorrentBlockXunlei v240408";

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
            Console.WriteLine(dtLastResetTime + ", Reset banned_IPs!");
            var response = await client.PostAsync(sTargetServer + sApp_setPreferences, new FormUrlEncodedContent(new Dictionary<string, string>() { { "json", "{\"banned_IPs\":\"\"}" } }));

            string[] saStatusToken = new string[] { ",\"status\":" };
            Dictionary<string, bool> dPublicTorrents = new Dictionary<string, bool>();

            Dictionary<string, HashSet<string>> dBannedClients = new Dictionary<string, HashSet<string>>();
            Dictionary<string, HashSet<string>> dNotBannedClients = new Dictionary<string, HashSet<string>>();

            while (true)
            {
                DateTime dtStart = DateTime.Now;

                TimeSpan ts = dtStart - dtLastResetTime;
                if (ts.Days >= 1)
                {
                    dtLastResetTime = dtStart;
                    Console.WriteLine(dtLastResetTime + ", Reset banned_IPs, reset interval: " + ts.TotalDays + " days");
                    response = await client.PostAsync(sTargetServer + sApp_setPreferences, new FormUrlEncodedContent(new Dictionary<string, string>() { { "json", "{\"banned_IPs\":\"\"}" } }));
                }

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
                    CCEHandler(null, null);
                }

                iResponseStartIndex += sTorrentsObjectText.Length;

                while (iResponseStartIndex != -1)
                {
                    ++iResponseStartIndex;
                    iResponseEndIndex = responseBody.IndexOf('"', iResponseStartIndex);
                    string sTorrentHash = responseBody.Substring(iResponseStartIndex, iResponseEndIndex - iResponseStartIndex);

                    iResponseStartIndex = responseBody.IndexOf(sTotalSizeFieldText, iResponseEndIndex) + sTotalSizeFieldText.Length;
                    iResponseEndIndex = responseBody.IndexOf(',', iResponseStartIndex);
                    long lTorrentSize = long.Parse(responseBody.Substring(iResponseStartIndex, iResponseEndIndex - iResponseStartIndex));

                    if (!dPublicTorrents.ContainsKey(sTorrentHash))
                    {
                        dPublicTorrents[sTorrentHash] = true;
                        string trackersBody = await client.GetStringAsync(sTargetServer + sTorrentsTrackers + sTorrentHash);
                        string[] sa = trackersBody.Split(saStatusToken, StringSplitOptions.None);

                        // Private Tracker: DHT is disabled & only one Tracker
                        if ((sa.Length == 5) && (sa[1][0] == '0'))
                            dPublicTorrents[sTorrentHash] = false;
                    }

                    // 取得 peers (僅限於非 PT 的種子)
                    if (dPublicTorrents[sTorrentHash])
                    {
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

                            // 對方回報的進度是 0 或 對方未曾上傳過
                            if ((dmProgress == 0) || (lDownloaded == 0))
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

                                if (!bBanPeer && bBanAncientClients)
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

                                /*
                                // 可疑的 port
                                if ((iPort == 12345) || (iPort == 2011) || (iPort == 2013) || (iPort == 54321) || (iPort == 15000) || (iPort < 2100))
                                {
                                    Console.WriteLine("Banned by Port: " + iPort + ", " + sPeer);
                                    sbBanPeers.Append(sPeer + "|");
                                }
                                */
                            }

                            if (!bBanPeer && (sFlags.IndexOf('U') != -1))
                            {
                                // 上傳量 > 種子實際大小
                                if (lUploaded > lTorrentSize)
                                {
                                    Console.WriteLine("Banned - Uploaded " + ((decimal)lUploaded / 1024 / 1024) + " MB > Torrent size " + ((decimal)lTorrentSize / 1024 / 1024) + " MB: " + sClient + ", " + sPeer);
                                    bBanPeer = true;
                                }
                                // 上傳了 10 MB 後，對方回報的進度仍為 0
                                else if ((dmProgress == 0) && (lUploaded >= 10 * 1024 * 1024))
                                {
                                    Console.WriteLine("Banned - Uploaded >= 10 MB & Progress = 0%: " + sClient + ", " + sPeer);
                                    bBanPeer = true;
                                }
                                // 預估進度 > 對方回報的進度，預估進度 = (上傳量 - 容許誤差) / 種子實際大小
                                else if ((lTorrentSize >= lProgressCheckBoundarySize) && ((lUploaded - lProgressCheckBoundarySize) > (lTorrentSize * dmProgress)))
                                {
                                    Console.WriteLine("Banned - Uploaded = " + ((decimal)lUploaded * 100 / lTorrentSize) + "% > Progress = " + (dmProgress * 100) + "%: " + sClient + ", " + sPeer);
                                    bBanPeer = true;
                                }
                            }

                            if (bBanPeer)
                                sbBanPeers.Append(sPeer + "|");
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

                DateTime dtNow = DateTime.Now;
                TimeSpan tsLoopCost = dtNow - dtStart;
                Console.WriteLine(dtNow + ", loop interval: " + dLoopIntervalSeconds + " sec., loop cost: " + tsLoopCost.TotalSeconds + " sec.");

                int iSleepMs = (int)Math.Round(dLoopIntervalMs - tsLoopCost.TotalMilliseconds);
                if (iSleepMs > 0)
                    Thread.Sleep(iSleepMs);
            }
        }
    }
}
