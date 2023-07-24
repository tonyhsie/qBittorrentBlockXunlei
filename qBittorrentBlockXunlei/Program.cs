﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace qBittorrentBlockXunlei
{
    internal class Program
    {
        static readonly bool bCollectClients = false;
        static readonly int iLoopInterval = 60;

        static readonly Encoding eOutput = Console.OutputEncoding;

        static readonly HttpClient client = new HttpClient();

        static readonly string sApp_webapiVersion = "/api/v2/app/webapiVersion";
        static readonly string sSync_maindata = "/api/v2/sync/maindata";
        static readonly string sSync_torrentPeers = "/api/v2/sync/torrentPeers?hash=";
        static readonly string sTransfer_banpeers = "/api/v2/transfer/banPeers";

        static readonly string sMaindataStartText = "{\"categories\":{";
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

        static readonly List<string> lsBannedClients = new List<string>() { "-XL", "Xunlei", "7.", "aria2", "Xfplay", "dandanplay", "FDM", "go.torrent", "Mozilla" };

        static void CCEHandler(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine(Environment.NewLine + "Ctrl-C is pressed!");
            Console.OutputEncoding = eOutput;
            Environment.Exit(0);
        }

        static async Task Main(string[] args)
        {
            Console.Title = "qBittorrentBlockXunlei v230724";

            Console.OutputEncoding = Encoding.UTF8;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(CCEHandler);

            // 取得 port number
            while ((args.Length == 0) || !int.TryParse(args[0], out int iPort) || (iPort < 0) || (iPort > 65535))
            {
                Console.Write("Port Number = ");
                args = Console.ReadLine().Split(' ');
            }

            string sBaseUrl = "http://127.0.0.1:" + args[0];
            string responseBody;
            string peersBody;

            int iResponseStartIndex;
            int iResponseEndIndex;
            int iPeersStartIndex;
            int iPeersEndIndex;

            StringBuilder sbBanPeers = new StringBuilder();

            // 取得 WebAPI 版本
            try
            {
                responseBody = await client.GetStringAsync(sBaseUrl + sApp_webapiVersion);
            }
            catch
            {
                Console.WriteLine("Can't connent to qBittorrent WebUI");
                Console.OutputEncoding = eOutput;
                return;
            }

            // WebAPI 版本需 >= 2.3
            {
                string[] saVersion = responseBody.Split('.');
                if ((saVersion.Length < 2) || !int.TryParse(saVersion[0], out int iMajorVersion) || (iMajorVersion < 2) || !int.TryParse(saVersion[1], out int iMinorVersion) || ((iMajorVersion == 2) && (iMinorVersion < 3)))
                {
                    Console.WriteLine("Please upgrade your qBittorrent first!");
                    Console.OutputEncoding = eOutput;
                    return;
                }
            }

            Dictionary<string, HashSet<string>> dBannedClients = new Dictionary<string, HashSet<string>>();
            Dictionary<string, HashSet<string>> dNotBannedClients = new Dictionary<string, HashSet<string>>();

            while (true)
            {
                DateTime dtStart = DateTime.UtcNow;

                // 取得 torrent hash
                responseBody = "";
                while (responseBody == "")
                {
                    try
                    {
                        responseBody = await client.GetStringAsync(sBaseUrl + sSync_maindata);
                    }
                    catch
                    {
                        Console.WriteLine("Can't connent to qBittorrent WebUI, wait " + iLoopInterval + " sec. to reconnect!");
                        Thread.Sleep(iLoopInterval * 1000);
                    }
                }

                iResponseStartIndex = responseBody.IndexOf(sTorrentsObjectText);
                if ((iResponseStartIndex == -1) || !responseBody.StartsWith(sMaindataStartText))
                {
                    Console.WriteLine("Can't get torrents info!");
                    break;
                }

                iResponseStartIndex += sTorrentsObjectText.Length;

                while (iResponseStartIndex != -1)
                {
                    ++iResponseStartIndex;
                    iResponseEndIndex = responseBody.IndexOf('"', iResponseStartIndex);
                    string sTorrentHash = responseBody.Substring(iResponseStartIndex, iResponseEndIndex - iResponseStartIndex);

                    // 取得 peers
                    {
                        peersBody = await client.GetStringAsync(sBaseUrl + sSync_torrentPeers + sTorrentHash);

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
                            ulong ulDownloaded = ulong.Parse(peersBody.Substring(iPeersStartIndex, iPeersEndIndex - iPeersStartIndex));

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
                            ulong ulUploaded = ulong.Parse(peersBody.Substring(iPeersStartIndex, iPeersEndIndex - iPeersStartIndex));

                            // 找下一個 peer：先找每個 peer 最後一欄 uploaded，再看結尾字串是 "}," 或 "}}"
                            iPeersStartIndex = iPeersEndIndex + 1;
                            if (peersBody[iPeersStartIndex] == ',')
                                ++iPeersStartIndex;
                            else
                                iPeersStartIndex = -1;

                            // 判斷是否要 ban 該 peer
                            bool bBanPeer = false;

                            // 吸血雷上報的進度永遠是0，且大部分不上傳
                            if ((dmProgress == 0) || (ulDownloaded == 0))
                            {
                                foreach (string sBannedClient in lsBannedClients)
                                {
                                    if (sClient.StartsWith(sBannedClient))
                                    {
                                        Console.WriteLine("Banned - Leech Client: " + sClient + ", " + sPeer);
                                        bBanPeer = true;
                                        break;
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

                            // 上傳了 1M 進度還是0
                            if (!bBanPeer && (dmProgress == 0) && (ulUploaded >= 1024 * 1024) && (sFlags.IndexOf("U") != -1))
                            {
                                Console.WriteLine("Banned - Uploaded >= 1M & Progress = 0%: " + sClient + ", " + sPeer);
                                bBanPeer = true;
                            }

                            if (bBanPeer)
                                sbBanPeers.Append(sPeer + "|");

                            // 收集 client 資訊
                            if (bCollectClients)
                            {
                                string sClientVersion = "";
                                iResponseStartIndex = sClient.IndexOf('/');
                                if (iResponseStartIndex == -1)
                                    iResponseStartIndex = sClient.LastIndexOf(' ');
                                if (iResponseStartIndex != -1)
                                {
                                    sClientVersion = sClient.Substring(iResponseStartIndex + 1);
                                    sClient = sClient.Substring(0, iResponseStartIndex);
                                }

                                if (bBanPeer)
                                {
                                    if (dBannedClients.ContainsKey(sClient))
                                        dBannedClients[sClient].Add(sClientVersion);
                                    else
                                        dBannedClients[sClient] = new HashSet<string>() { sClientVersion };

                                    if (dNotBannedClients.ContainsKey(sClient))
                                    {
                                        dNotBannedClients[sClient].Clear();
                                        dNotBannedClients.Remove(sClient);
                                    }
                                }
                                else
                                {
                                    if (!dBannedClients.ContainsKey(sClient))
                                    {
                                        if (dNotBannedClients.ContainsKey(sClient))
                                            dNotBannedClients[sClient].Add(sClientVersion);
                                        else
                                            dNotBannedClients[sClient] = new HashSet<string>() { sClientVersion };
                                    }
                                }
                            }
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
                    if (sbBanPeers[sbBanPeers.Length - 1] == '|')
                        sbBanPeers.Remove(sbBanPeers.Length - 1, 1);

                    var response = await client.PostAsync(sBaseUrl + sTransfer_banpeers, new FormUrlEncodedContent(new Dictionary<string, string>() { { "peers", sbBanPeers.ToString() } }));
                    sbBanPeers.Clear();
                }

                if (bCollectClients)
                {
                    using (StreamWriter sw = new StreamWriter("D:\\qbx.log"))
                    {
                        sw.WriteLine("Banned Clients:");
                        foreach (string sClient in new SortedSet<string>(dBannedClients.Keys))
                        {
                            sw.Write("\t" + sClient + ": ");
                            foreach (string sVersion in new SortedSet<string>(dBannedClients[sClient]))
                                sbBanPeers.Append("\"" + sVersion + "\", ");
                            sw.WriteLine(sbBanPeers.ToString(0, sbBanPeers.Length - 2));
                            sbBanPeers.Clear();
                        }

                        sw.WriteLine();

                        sw.WriteLine("Legal Clients:");
                        foreach (string sClient in new SortedSet<string>(dNotBannedClients.Keys))
                        {
                            sw.Write("\t" + sClient + ": ");
                            foreach (string sVersion in new SortedSet<string>(dNotBannedClients[sClient]))
                                sbBanPeers.Append("\"" + sVersion + "\", ");
                            sw.WriteLine(sbBanPeers.ToString(0, sbBanPeers.Length - 2));
                            sbBanPeers.Clear();
                        }
                    }
                }

                Console.WriteLine(DateTime.Now + ", loop interval: " + iLoopInterval + " sec., loop cost: " + (DateTime.UtcNow - dtStart).TotalSeconds + " sec.");

                Thread.Sleep(iLoopInterval * 1000);
            }

            Console.OutputEncoding = eOutput;
        }
    }
}