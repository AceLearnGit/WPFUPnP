using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

namespace TestWPFUPNP
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
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
        /// 组播地址
        /// </summary>
        IPEndPoint multicastAddress = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);

        bool listenSSDP = true;

        private UdpClient udpClient = new UdpClient(AddressFamily.InterNetwork);

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

        bool Flag = true;
        /// <summary>
        /// 远程主机地址
        /// </summary>
        IPEndPoint remoteIPEndPoint = null;
        /// <summary>
        /// 接收UDP消息（此处主要为接受ssdp:discover消息的回复）
        /// </summary>
        private void ListenUdpMsg()
        {
            byte[] data = null;
            try
            {
                data = udpClient.Receive(ref remoteIPEndPoint);
            }
            catch (Exception ex)
            {

            }

            string responseStr = Encoding.UTF8.GetString(data);
            Debug.WriteLine(remoteIPEndPoint + ":" + responseStr);
            udpClient.Close();
            udpClient.Dispose();
            string find = "LOCATION:";
            int start = responseStr.IndexOf(find);
            if (start > -1)
            {
                int end = responseStr.IndexOf("\r\n", start);
                string locationValue = responseStr.Substring(start + find.Length, end - (start + find.Length)).Trim();
                Uri descUri = new Uri(locationValue);
                remoteIPEndPoint = new IPEndPoint(IPAddress.Parse(descUri.Host), descUri.Port);
                URLBase = "http://" + remoteIPEndPoint;
            }
            //    //TODO:xml解析，拿到我们要用的AVTransport服务节点

            //    //以小米盒子4为例
            //    //string str = @"
            //    //                <service>
            //    //                    <serviceType>urn:schemas-upnp-org:service:AVTransport:1</serviceType>
            //    //                    <serviceId>urn:upnp-org:serviceId:AVTransport</serviceId>
            //    //                    <SCPDURL>/dlna/Render/AVTransport_scpd.xml</SCPDURL>
            //    //                    <controlURL>_urn:schemas-upnp-org:service:AVTransport_control</controlURL>
            //    //                    <eventSubURL>_urn:schemas-upnp-org:service:AVTransport_event</eventSubURL>
            //    //                </service>";

            //    //其中 SCPDURL节点对应的值为服务描述文件地址，我们应该继续继续该描述文件，并利用解析到的action控制设备

        }

        /// <summary>
        /// 小米盒子4回应ssdp:discover消息时描述文件基地址
        /// </summary>
        private static string URLBase = "http://172.20.10.2:49153";
        /// <summary>
        /// 投屏使用的服务类型
        /// </summary>
        private static string Casting_serviceType = "urn:schemas-upnp-org:service:AVTransport:1";
        private static string Casting_serviceId = "urn:upnp-org:serviceId:AVTransport";

        /// <summary>
        /// 投屏所使用的服务描述文件地址
        /// </summary>
        private static string Casting_SCPDURL = "/dlna/Render/AVTransport_scpd.xml";

        /// <summary>
        /// 向服务发出控制消息的URL
        /// </summary>
        private static string Casting_controlURL = "/upnp/service/AVTransport/Control";//"_urn:schemas-upnp-org:service:ConnectionManager_control";
        private static string Casting_eventSubURL = "/upnp/service/AVTransport/Event";// "_urn:schemas-upnp-org:service:ConnectionManager_event";

        private string GetServiceDescUrl(string descXml, string serviceType)
        {
            //假设descXml为小米盒子4回应ssdp:discover消息中附带的设备描述信息
            //以向小米盒子4投屏为例，我们使用的serviceType为 urn:schemas-upnp-org:service:ConnectionManager:1
            //TODO:进行xml解析，拿到serviceType为urn:schemas-upnp-org:service:ConnectionManager:1的节点信息
            return Casting_SCPDURL;

        }

        /// <summary>
        /// 获取AVTransportService的描述文件
        /// </summary>
        /// <param name="serviceDescUrl"></param>
        private string GetAVTransportServiceDes(string serviceDescUrl)
        {
            //假设serviceDescUrl为小米盒子4投屏服务的描述文件地址（实际为基地址和SCPDURL的拼接，注意/）
            //这里我的地址为 http://172.20.10.3:49152/dlna/Render/AVTransport_scpd.xml
            string str = "GET " + URLBase.Remove(URLBase.Length - 1) + Casting_SCPDURL + " HTTP/1.1\r\n" +
                             "HOST:" + remoteIPEndPoint.Address + ":" + remoteIPEndPoint.Port + "\r\n" +
                             "ACCEPT-LANGUAGE: \r\n\r\n",
                   result = "";
            byte[] data = Encoding.UTF8.GetBytes(str);
            TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
            NetworkStream networkStream = null;
            try
            {
                tcpClient.Connect(remoteIPEndPoint);
                networkStream = tcpClient.GetStream();
                networkStream.Write(data, 0, data.Length);
                //Thread.Sleep(100);
                int ReadSize = 2048;
                byte[] buff = new byte[ReadSize], readBuff;
                str = "";
                while (ReadSize == 2048)
                {
                    ReadSize = networkStream.Read(buff, 0, buff.Length);
                    readBuff = new byte[ReadSize];
                    Array.Copy(buff, 0, readBuff, 0, ReadSize);
                    str += Encoding.UTF8.GetString(readBuff);
                }
                result = str.Substring(str.IndexOf("\r\n\r\n") + 4).Trim();
                while (result.Substring(result.Length - 2) == "\r\n" || result.Substring(result.Length - 2) == Encoding.Default.GetString(new byte[2] { 0, 0 }))
                {
                    result = result.Substring(0, result.Length - 2);
                }
            }
            catch { }
            finally
            {
                if (networkStream != null)
                {
                    networkStream.Close();
                    networkStream.Dispose();
                }
                if (tcpClient != null)
                {
                    tcpClient.Close();
                }
            }
            //TODO:随后我们应进行xml解析，拿到我们投屏要使用的action信息，这里我们需要使用SetAVTransportURI
            return result;//AVTransport_scpd.xml文件中的内容
        }

        /// <summary>
        /// 被投屏的文件Url
        /// </summary>
        private string fileUrl = "http://192.168.43.60:9000/file_video/1.jpg";
        //"https://ss0.bdstatic.com/5aV1bjqh_Q23odCf/static/superlanding/img/logo_top.png";
        // "http://172.20.10.7:9000/api/media/image";
        // // "http://172.20.10.12:9000/file_videp/1.jpg";
        // //http://172.20.10.12:9000/file_video/ladybug.wmv

        /// <summary>
        /// 动作名称，投屏本质本质为调用该方法
        /// </summary>
        private static string SetAVTransportURIStr = "SetAVTransportURI";
        /// <summary>
        /// 调用SetAVTransportURI action进行投屏
        /// </summary>
        private void SetAVTransportURI()
        {
            //remoteIPEndPoint = new IPEndPoint(IPAddress.Parse("172.20.10.2"), 49153);
            string metaData = "";
            string metaDataHeader = "<DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" " +
                                        "xmlns:sec=\"http://www.sec.co.kr/\"" +
                                        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\"" +
                                        "xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\"" +
                                        "xmlns:dlna=\"urn:schemas-dlna-org:metadata-1-0/\">";
            string item = "<item id=\"filePath\" restricted=\"0\" parentID=\"0\">";
            string title = "<dc:title>IMAG1466</dc:title>";  
            string middle = "<dc:creator>Unknown Artist</dc:creator>" +
                           "<upnp:artist>Unknown Artist</upnp:artist>" +
                           "<upnp:albumArtURI>http://IP:PORT/filePath</upnp:albumArtURI>" +
                           "<upnp:album>Unknown Album</upnp:album>";
            string resolution1 = "<res protocolInfo=\"http-get:*:image/jpeg:DLNA.ORG_PN=JPEG_LRG;DLNA.ORG_OP=01;DLNA.ORG_FLAGS=01700000000000000000000000000000\">" + "http://IP:PORT/filePath" + "</res>";
            string resolution2 = "<res protocolInfo=\"http-get:*:image/jpeg:DLNA.ORG_PN=JPEG_TN;DLNA.ORG_OP=01;DLNA.ORG_CI=1;DLNA.ORG_FLAGS=00f00000000000000000000000000000\">" + fileUrl + "</res>";
            string _class = "<upnp:class>object.item.imageItem</upnp:class>";
            string meataDataFooter = "</item></DIDL-Lite>";
            //metaData = metaDataHeader + item + title + middle + resolution1 + _class + meataDataFooter;
            //metaData= "<DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dlna=\"urn:schemas-dlna-org:metadata-1-0/\" xmlns:sec=\"http://www.sec.co.kr/\"><item id=\"filePath\" parentID=\"0\" restricted=\"1\"><upnp:class>object.item.imageItem</upnp:class><dc:title>IMAG1466</dc:title><dc:creator>Unknown Artist</dc:creator><upnp:artist>Unknown Artist</upnp:artist><upnp:albumArtURI>http://IP:PORT/filePath</upnp:albumArtURI><upnp:album>Unknown Album</upnp:album><res protocolInfo=\"http-get:*:image/jpg:DLNA.ORG_PN=JPG_LRG;DLNA.ORG_OP=01;DLNA.ORG_FLAGS=01700000000000000000000000000000\">http://IP:PORT/filePath</res></item></DIDL-Lite>";
            string soapData = $"<u:{SetAVTransportURIStr} xmlns:u=\"{Casting_serviceType}\">" +
                                    "<InstanceID>0</InstanceID>" +
                                    "<CurrentURI>" + fileUrl + "</CurrentURI>" +
                                    "<CurrentURIMetaData>" + metaData + "</CurrentURIMetaData>" +
                              $"</u:{SetAVTransportURIStr}>";



            string soap = "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                            "<s:Body>" + soapData + "</s:Body>" +
                          "</s:Envelope>";

            //URLBase + Casting_controlURL=http://172.20.10.3:49152/_urn:schemas-upnp-org:service:AVTransport_control
            string strData = "POST " + URLBase + Casting_controlURL + " HTTP/1.1\r\n" +
                             "HOST: " + remoteIPEndPoint + "\r\n" +
                             "Content-Length: " + Encoding.UTF8.GetBytes(soap).Length.ToString() + "\r\n" +
                             "CONTENT-TYPE: text/xml; charset=\"utf-8\"\r\n" +
                             "SOAPACTION: \"" + Casting_serviceType + "#" + SetAVTransportURIStr + "\"\r\n\r\n" + soap;

            byte[] data = Encoding.Default.GetBytes(strData);
            TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
            NetworkStream networkStream = null;
            try
            {
                tcpClient.Connect(remoteIPEndPoint);
                networkStream = tcpClient.GetStream();
                networkStream.Write(data, 0, data.Length);
                byte[] buffer = new byte[4096], readBuff;
                int ReadSize = networkStream.Read(buffer, 0, buffer.Length);
                readBuff = new byte[ReadSize];
                Array.Copy(buffer, 0, readBuff, 0, ReadSize);
                strData = Encoding.Default.GetString(readBuff);//调用结果，这里就不分析了
                //PlayPic();
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (networkStream != null)
                {
                    networkStream.Close();
                }
                tcpClient.Close();
            }
        }

        private void PlayPic()
        {
            string soapDataPlayPic = $"<u:Play xmlns:u=\"{Casting_serviceType}\">" +
                                     "<InstanceID>0</InstanceID>" +
                                     "<Speed>1</Speed>" +
                                     $"</u:Play>";
            string soapPlayPic = "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                                 "<s:Body>" + soapDataPlayPic + "</s:Body>" +
                                 "</s:Envelope>";
            string strDataPlayPic = "POST " + URLBase + Casting_controlURL + " HTTP/1.1\r\n" +
                                    "HOST: " + remoteIPEndPoint + "\r\n" +
                                    "Content-Length: " + Encoding.UTF8.GetBytes(soapPlayPic).Length.ToString() + "\r\n" +
                                    "CONTENT-TYPE: text/xml; charset=\"utf-8\"\r\n" +
                                    "SOAPACTION: \"" + Casting_serviceType + "#" + "Play" + "\"\r\n\r\n" + soapPlayPic;

            byte[] data = Encoding.Default.GetBytes(strDataPlayPic);
            TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
            NetworkStream networkStream = null;
            try
            {
                tcpClient.Connect(remoteIPEndPoint);
                networkStream = tcpClient.GetStream();
                networkStream.Write(data, 0, data.Length);
                byte[] buffer = new byte[4096], readBuff;
                int ReadSize = networkStream.Read(buffer, 0, buffer.Length);
                readBuff = new byte[ReadSize];
                Array.Copy(buffer, 0, readBuff, 0, ReadSize);
                strDataPlayPic = Encoding.Default.GetString(readBuff);//调用结果，这里就不分析了
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (networkStream != null)
                {
                    networkStream.Close();
                }
                tcpClient.Close();
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SetAVTransportURI();
        }
    }
}
