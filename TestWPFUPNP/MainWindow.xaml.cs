using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;

namespace TestWPFUPNP
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 动作名称，投屏本质本质为调用该方法
        /// </summary>
        private const string SetAVTransportURIStr = "SetAVTransportURI";
        private const string PlayAction = "Play";
        private const string PauseAction = "Pause";
        private const string StopAction = "Stop";
        private const string NextAction = "Next";
        private const string PreviousAction = "Previous";

        /// <summary>
        /// 用于发送SOAP的基地址
        /// </summary>
        private static string URLBase = "http://172.20.10.2:49153";

        /// <summary>
        /// 投屏使用的服务类型
        /// </summary>
        private static string Casting_serviceType = "urn:schemas-upnp-org:service:AVTransport:1";
        //private static string Casting_serviceId = "urn:upnp-org:serviceId:AVTransport";

        /// <summary>
        /// 投屏所使用的服务描述文件地址
        /// </summary>
        //private static string Casting_SCPDURL = "/dlna/Render/AVTransport_scpd.xml";

        /// <summary>
        /// 向服务发出控制消息的URL
        /// </summary>
        private static string Casting_controlURL = "/upnp/service/AVTransport/Control";//"_urn:schemas-upnp-org:service:ConnectionManager_control";
        //private static string Casting_eventSubURL = "/upnp/service/AVTransport/Event";// "_urn:schemas-upnp-org:service:ConnectionManager_event";

        /// <summary>
        /// 远程主机地址
        /// </summary>
        IPEndPoint remoteIPEndPoint = new IPEndPoint(IPAddress.Parse("172.20.10.2"), 49153);

        /// <summary>
        /// 组播地址
        /// </summary>
        private static IPEndPoint multicastAddress = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);

        private List<SSDPDevice> Devices = new List<SSDPDevice>();

        private UdpClient udpClient = new UdpClient();

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DiscoverSSNPDevice();
        }


        /// <summary>
        /// 发送 发现ssdp设备 的消息
        /// </summary>
        private async void DiscoverSSNPDevice()
        {
            //urn:schemas-upnp-org:service:AVTransport:1 代表投屏
            string str = "M-SEARCH * HTTP/1.1\r\n" +
                         "HOST: 239.255.255.250:1900\r\n" +
                         "MAN:\"ssdp:discover\"\r\n" +
                         "MX:5\r\n" +
                         "ST:urn:schemas-upnp-org:service:AVTransport:1\r\n\r\n";
            byte[] data = Encoding.UTF8.GetBytes(str);
            //udpClient.send(data, data.Length, multicastAddress);
            await udpClient.SendAsync(data, data.Length, multicastAddress);
            ListenUdpMsg();
        }

        /// <summary>
        /// 接收UDP消息（此处主要为接受ssdp:discover消息的回复）
        /// </summary>
        private async void ListenUdpMsg()
        {
            byte[] data = null;
            var udpRet = await udpClient.ReceiveAsync();
            data = udpRet.Buffer;
            remoteIPEndPoint = udpRet.RemoteEndPoint;
            //data = udpClient.Receive(ref remoteIPEndPoint);
            string responseStr = Encoding.UTF8.GetString(data);
            Debug.WriteLine(remoteIPEndPoint + ":" + responseStr);

            string find = "LOCATION:";
            int start = responseStr.IndexOf(find);
            if (start > -1)
            {
                int end = responseStr.IndexOf("\r\n", start);
                string locationValue = responseStr.Substring(start + find.Length, end - (start + find.Length)).Trim();
                Uri descUri = new Uri(locationValue);
                remoteIPEndPoint = new IPEndPoint(IPAddress.Parse(descUri.Host), descUri.Port);
                string xml = await GetDescXml(locationValue);
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                XmlNamespaceManager msMgr = new XmlNamespaceManager(doc.NameTable);
                msMgr.AddNamespace("ns", "urn:schemas-upnp-org:device-1-0");
                var nameNode = (XmlElement)doc.SelectSingleNode("//ns:friendlyName", msMgr);
                var serviceNode = (XmlElement)doc.SelectSingleNode("//ns:service[ns:serviceType='urn:schemas-upnp-org:service:AVTransport:1']", msMgr);
                Devices.Add(new SSDPDevice
                {
                    CastingCtrlUrl = serviceNode.GetElementsByTagName("controlURL")[0].InnerText,
                    URLBase = "http://" + remoteIPEndPoint,
                    FriendlyName = nameNode.InnerText
                });

            }

            udpClient.Close();
            udpClient.Dispose();

            URLBase = Devices[0].URLBase;
            Casting_controlURL = Devices[0].CastingCtrlUrl;
        }

        /// <summary>
        /// 调用SetAVTransportURI action进行投屏
        /// </summary>
        private async void SetAVTransportURI(string fileUrl)
        {
            string metaData = GetMetaData(MediaType.Image, "jpeg");
            string soapData = $"<u:{SetAVTransportURIStr} xmlns:u=\"{Casting_serviceType}\">" +
                                    "<InstanceID>0</InstanceID>" +
                                    "<CurrentURI>" + fileUrl + "</CurrentURI>" +
                                    "<CurrentURIMetaData>" + metaData + "</CurrentURIMetaData>" +
                              $"</u:{SetAVTransportURIStr}>";

            string strData = CombineSOAPData(URLBase + Casting_controlURL, remoteIPEndPoint.ToString(), Casting_serviceType, SetAVTransportURIStr, soapData);
            byte[] data = Encoding.Default.GetBytes(strData);

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Host", remoteIPEndPoint.ToString());
                var httpContent = new ByteArrayContent(data);
                httpContent.Headers.Add("CONTENT-TYPE", "text/xml;charset=\"utf-8\"");
                httpContent.Headers.Add("SOAPACTION", "\"" + Casting_serviceType + "#" + SetAVTransportURIStr + "\"");
                var responseMessage = await client.PostAsync(new Uri(URLBase + Casting_controlURL), httpContent);
                StreamReader reader = new StreamReader(await responseMessage.Content.ReadAsStreamAsync());
                var ret = reader.ReadToEnd();
            }

            //手动调用播放
            //ControlAVAction(PlayAction);
        }

        /// <summary>
        /// 控制状态
        /// </summary>
        /// <param name="actionName">Play/Pause/Stop/Next/Previous</param>
        private async void ControlAVAction(string actionName)
        {
            string soapData = "";
            if (actionName == PlayAction)
            {
                soapData = $"<u:{actionName} xmlns:u=\"{Casting_serviceType}\">" +
                                 "<InstanceID>0</InstanceID>" +
                                 "<Speed>1</Speed>" +
                           $"</u:{actionName}>";
            }
            else
            {
                soapData = $"<u:{actionName} xmlns:u=\"{Casting_serviceType}\">" +
                                "<InstanceID>0</InstanceID>" +
                           $"</u:{actionName}>";
            }
            string strData = CombineSOAPData(URLBase + Casting_controlURL, remoteIPEndPoint.ToString(), Casting_serviceType, actionName, soapData);
            byte[] data = Encoding.Default.GetBytes(strData);
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Host", remoteIPEndPoint.ToString());
                var httpContent = new ByteArrayContent(data);
                httpContent.Headers.Add("CONTENT-TYPE", "text/xml;charset=\"utf-8\"");
                httpContent.Headers.Add("SOAPACTION", "\"" + Casting_serviceType + "#" + actionName + "\"");
                var responseMessage = await client.PostAsync(new Uri(URLBase + Casting_controlURL), httpContent);
                StreamReader reader = new StreamReader(await responseMessage.Content.ReadAsStreamAsync());
                var ret = reader.ReadToEnd();
            }
            #region TCP调用方式
            //TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
            //NetworkStream networkStream = null;
            //try
            //{
            //    tcpClient.Connect(remoteIPEndPoint);
            //    networkStream = tcpClient.GetStream();
            //    networkStream.Write(data, 0, data.Length);
            //    byte[] buffer = new byte[4096], readBuff;
            //    int ReadSize = networkStream.Read(buffer, 0, buffer.Length);
            //    readBuff = new byte[ReadSize];
            //    Array.Copy(buffer, 0, readBuff, 0, ReadSize);
            //    string res = Encoding.Default.GetString(readBuff);//调用结果，这里就不分析了
            //}
            //catch (Exception ex)
            //{

            //}
            //finally
            //{
            //    if (networkStream != null)
            //    {
            //        networkStream.Close();
            //    }
            //    tcpClient.Close();
            //}
            #endregion
        }

        /// <summary>
        /// 获取服务的描述文件
        /// </summary>
        /// <param name="descUrl"></param>
        private async Task<string> GetDescXml(string descUrl)
        {
            string descXml = "";
            using (HttpClient client = new HttpClient())
            {
                var ret = await client.GetStreamAsync(descUrl);
                StreamReader reader = new StreamReader(ret);
                descXml = reader.ReadToEnd();
            }
            return descXml;
            #region TCP 方式
            //Uri u = new Uri(descUrl);
            //string host = u.Host + ":" + u.Port;

            //string str = "GET " + descUrl + " HTTP/1.1\r\n" +
            //             "HOST:" + host + "\r\n" +
            //             "ACCEPT-LANGUAGE: \r\n\r\n",
            //       result = "";
            //byte[] data = Encoding.UTF8.GetBytes(str);
            //TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
            //NetworkStream networkStream = null;
            //try
            //{
            //    tcpClient.Connect(remoteIPEndPoint);
            //    networkStream = tcpClient.GetStream();
            //    networkStream.Write(data, 0, data.Length);
            //    Thread.Sleep(100);
            //    int ReadSize = 2048;
            //    byte[] buff = new byte[ReadSize], readBuff;
            //    str = "";
            //    while (ReadSize == 2048)
            //    {
            //        ReadSize = networkStream.Read(buff, 0, buff.Length);
            //        readBuff = new byte[ReadSize];
            //        Array.Copy(buff, 0, readBuff, 0, ReadSize);
            //        str += Encoding.UTF8.GetString(readBuff);
            //    }
            //    result = str.Substring(str.IndexOf("\r\n\r\n") + 4).Trim();
            //    while (result.Substring(result.Length - 2) == "\r\n" || result.Substring(result.Length - 2) == Encoding.Default.GetString(new byte[2] { 0, 0 }))
            //    {
            //        result = result.Substring(0, result.Length - 2);
            //    }
            //}
            //catch { }
            //finally
            //{
            //    if (networkStream != null)
            //    {
            //        networkStream.Close();
            //        networkStream.Dispose();
            //    }
            //    if (tcpClient != null)
            //    {
            //        tcpClient.Close();
            //    }
            //}
            //return result;
            #endregion
        }

        /// <summary>
        /// 被投屏的文件Url
        /// </summary>
        private string fileUrl = "http://172.20.10.12:9000/file_video/1.jpg";

        /// <summary>
        /// 组装Metadata字段
        /// </summary>
        /// <param name="mediaType"></param>
        /// <param name="fileType"></param>
        /// <returns></returns>
        private string GetMetaData(MediaType mediaType, string fileType)
        {
            string metaData = "";
            string metaDataHeader = "<DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" " +
                                    " xmlns:sec=\"http://www.sec.co.kr/\"" +
                                    " xmlns:dc=\"http://purl.org/dc/elements/1.1/\"" +
                                    " xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\"" +
                                    " xmlns:dlna=\"urn:schemas-dlna-org:metadata-1-0/\">";
            string item = "<item id=\"id\" restricted=\"0\" parentID=\"0\">";
            string title = "<dc:title>UnKnown</dc:title>";
            string middle = "<dc:creator>Unknown</dc:creator>";
            string protocolInfo = "";
            string _class = "";
            switch (mediaType)
            {
                case MediaType.Video:
                    {
                        protocolInfo = $"<res protocolInfo=\"http-get:*:video/*\">" + "http://IP:PORT/filePath" +
                                       "</res>";
                        _class = "<upnp:class>object.item.videoItem</upnp:class>";
                    }
                    break;
                case MediaType.Image:
                    {
                        protocolInfo = $"<res protocolInfo=\"http-get:*:image/*\">" + "http://IP:PORT/filePath" +
                                       "</res>";
                        _class = "<upnp:class>object.item.imageItem</upnp:class>";
                    }
                    break;
                case MediaType.Audio:
                    {
                        protocolInfo = $"<res protocolInfo=\"http-get:*:audio/*\">" + "http://IP:PORT/filePath" +
                                       "</res>";
                        _class = "<upnp:class>object.item.audioItem</upnp:class>";
                    }
                    break;
                default:
                    break;
            }
            string meataDataFooter = "</item></DIDL-Lite>";
            metaData = metaDataHeader + item + title + middle + protocolInfo + _class + meataDataFooter;
            return metaData;
        }

        /// <summary>
        /// 组装SOAP消息
        /// </summary>
        /// <param name="postUrl"></param>
        /// <param name="host"></param>
        /// <param name="serviceType"></param>
        /// <param name="action"></param>
        /// <param name="soapData"></param>
        /// <returns></returns>
        private string CombineSOAPData(string postUrl, string host, string serviceType, string action, string soapData)
        {
            string soap = "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                                "<s:Body>" + soapData + "</s:Body>" +
                          "</s:Envelope>";
            return soap;
            //TCP方式需带上这些
            //string str = "POST " + postUrl + " HTTP/1.1\r\n" +
            //             "HOST: " + host + "\r\n" +
            //             "Content-Length: " + Encoding.UTF8.GetBytes(soap).Length.ToString() + "\r\n" +
            //             "CONTENT-TYPE: text/xml; charset=\"utf-8\"\r\n" +
            //             "SOAPACTION: \"" + serviceType + "#" + action + "\"\r\n\r\n" + soap;
            //return str;

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SetAVTransportURI(fileUrl);
        }
    }
    public enum MediaType
    {
        Video,
        Image,
        Audio
    }
    public class SSDPDevice
    {
        public string FriendlyName { get; set; }
        public string URLBase { get; set; }
        public string CastingCtrlUrl { get; set; }
    }
}
