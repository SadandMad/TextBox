using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TalkBox
{
    public partial class Form1 : Form
    {
        internal String UserName;
        internal bool Connected = false;
        internal bool Requested = false;
        internal List<Sub> Subs = new List<Sub>();
        internal Task receiveUDP;
        internal Task receiveTCP;
        internal CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
        internal CancellationToken token;
        internal class Sub
        {
            public string name;
            public IPEndPoint endPoint;
            public Sub(string str, IPEndPoint iep)
            {
                name = str;
                endPoint = iep;
            }
        }

        internal class TalkPacket
        {
            public byte Type;
            public UInt16 MsgLength;
            public byte[] Data;

            public TalkPacket(byte T, string D)
            {
                Type = T;
                Data = Encoding.Unicode.GetBytes(D);
                MsgLength = (UInt16)(3 + Data.Length);
            }
            public TalkPacket(byte[] D)
            {
                Type = D[0];
                MsgLength = BitConverter.ToUInt16(D, 1);
                Data = new byte[MsgLength-3];    
                Buffer.BlockCopy(D, 3, Data, 0, MsgLength - 3);
            }
            public TalkPacket(byte T)
            {
                Type = T;
                MsgLength = 3;
            }
            public byte[] getBytes()
            {
                byte[] data = new byte[MsgLength];
                Buffer.BlockCopy(BitConverter.GetBytes(Type), 0, data, 0, 1);
                Buffer.BlockCopy(BitConverter.GetBytes(MsgLength), 0, data, 1, 2);
                if (MsgLength > 3)
                    Buffer.BlockCopy(Data, 0, data, 3, MsgLength-3);
                return data;
            }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (buttonConnect.Text == "Подключиться")
            {
                UserName = textBoxName.Text.Trim();
                if (UserName.Length > 3 && UserName.Length < 13)
                {
                    ConnectChat();
                }
                else
                    MessageBox.Show("Введите имя для вашего аккаунта!");
            }
            else
            {
                LeaveChat();
            }
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {
            if (Connected && textBoxMsg.Text.Length > 0)
            {
                TalkPacket msg = new TalkPacket(2, textBoxMsg.Text);
                ListMessages.Items.Add("     " + DateTime.Now.ToLongTimeString() + " Вы: " + textBoxMsg.Text);
                textBoxMsg.Text = "";
                Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                foreach (Sub sub in Subs)
                {
                    client.Connect(sub.endPoint);
                    client.Send(msg.getBytes(), SocketFlags.None);
                    client.Close();
                }
            }
            else
                MessageBox.Show("Вы не подключены к чату.");
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Connected)
                LeaveChat();
        }
        private void ConnectChat()
        {
            textBoxName.Text = UserName;
            UdpClient client = new UdpClient();
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("230.230.230.230"), 8005);
            try
            {
                TalkPacket msg = new TalkPacket(1, UserName);
                client.Send(msg.getBytes(), msg.MsgLength, endPoint);
                client.Close();
                textBoxName.Enabled = false;
                buttonConnect.Text = "Отключиться";
                Connected = true;
                token = cancelTokenSource.Token;
                receiveUDP = new Task(() => ReceiveConnection());
                receiveTCP = new Task(() => ReceiveMessage());
                receiveUDP.Start();
                receiveTCP.Start();
                this.Invoke(new MethodInvoker(() =>
                {
                    ListMessages.Items.Add("     " + DateTime.Now.ToLongTimeString() + " Вы присоединились к разговору.");
                }));
            }
            catch (Exception ex)
            {
                client.Close();
                MessageBox.Show(ex.Message);
            }
        }
        private void LeaveChat()
        {
            UdpClient client = new UdpClient();
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("230.230.230.230"), 8005);
            try
            {
                TalkPacket msg = new TalkPacket(0);
                client.Send(msg.getBytes(), msg.MsgLength, endPoint);
                textBoxName.Enabled = true;
                buttonConnect.Text = "Подключиться";
                Connected = false;
                Requested = false;
                cancelTokenSource.Cancel();
                this.Invoke(new MethodInvoker(() =>
                {
                    ListMessages.Items.Add("     " + DateTime.Now.ToLongTimeString() + " Вы покинули нас.");
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                client.Close();
            }
        }
        private void ReceiveConnection()
        {
            
            try
            {
                while (!token.IsCancellationRequested)
                {
                    UdpClient client = new UdpClient(8005);
                    client.JoinMulticastGroup(IPAddress.Parse("230.230.230.230"), 100);
                    IPEndPoint remoteIp = null;
                    byte[] data = client.Receive(ref remoteIp);
                    TalkPacket msg = new TalkPacket(data);
                    Sub sub = new Sub(Encoding.Unicode.GetString(msg.Data), new IPEndPoint(remoteIp.Address, 8006));
                    client.Close();
                    if (msg.Type == 1)
                    {
                        if (!Subs.Contains(sub))
                            Subs.Add(sub);
                        this.Invoke(new MethodInvoker(() =>
                        {
                            ListMessages.Items.Add("     " + DateTime.Now.ToLongTimeString() + " " + sub.name + "(" + sub.endPoint.ToString() + ") " + " присоединился к разговору.");
                        }));
                        msg = new TalkPacket(1, UserName);
                        Socket sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        sender.Connect(sub.endPoint.Address, 8006);
                        sender.Send(msg.getBytes(), SocketFlags.None);
                        foreach (string str in ListMessages.Items)
                        {
                            msg = new TalkPacket(3, str);
                            sender.Send(msg.getBytes(), SocketFlags.None);
                        }
                        sender.Close();
                    }
                    else if (msg.Type == 0)
                    {
                        if (Subs.Contains(sub))
                        {
                            Subs.Remove(sub);
                        }
                        this.Invoke(new MethodInvoker(() =>
                        {
                            ListMessages.Items.Add("     " + DateTime.Now.ToLongTimeString() + " " + sub.name + "(" + sub.endPoint.ToString() + "): " + " покинул нас.");
                        }));
                    }
                    else
                        MessageBox.Show("An error occured!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void ReceiveMessage()
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    TcpListener listener = new TcpListener(IPAddress.Parse(GetLocalIPAddress()), 8006);
                    listener.Start();
                    TcpClient client = listener.AcceptTcpClient();
                    NetworkStream stream = client.GetStream();
                    byte[] data = new byte[65536];
                    stream.Read(data, 0, data.Length);
                    TalkPacket msg = new TalkPacket(data);
                    IPEndPoint iep = new IPEndPoint (IPAddress.Parse(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()), 8006);
                    if (msg.Type == 1)
                    {
                        Sub sub = new Sub(Encoding.Unicode.GetString(msg.Data), iep);
                        if (!Subs.Contains(sub))
                        {
                            Subs.Add(sub);
                            this.Invoke(new MethodInvoker(() =>
                            {
                                ListMessages.Items.Add("     " + DateTime.Now.ToLongTimeString() + " " + sub.name + "(" + sub.endPoint.ToString() + ") " + " присоединился к разговору.");
                            })); 
                        }
                    }
                    else if (msg.Type == 2)
                    {
                        string name = "Аноним";
                        foreach (Sub sub in Subs)
                        {
                            if (sub.endPoint.Equals(iep))
                            {
                                name = sub.name;
                                break;
                            }
                        }
                        this.Invoke(new MethodInvoker(() =>
                        {
                            ListMessages.Items.Add(DateTime.Now.ToLongTimeString() + " " + name + "(" + iep.ToString() + "): " + Encoding.Unicode.GetString(msg.Data));
                        }));
                    }
                    else if (msg.Type == 3)
                    {
                        this.Invoke(new MethodInvoker(() =>
                        {
                            ListMessages.Items.Add(Encoding.Unicode.GetString(msg.Data));
                        }));
                    }
                    else
                        MessageBox.Show("An error occured!");
                    client.Close();
                    stream.Close();
                    listener.Stop();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}