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
                Data = D.ToByteArr();
                MsgLength = (UInt16)(3 + Data.Length);
            }
            public TalkPacket(byte T)
            {
                Type = T;
                MsgLength = 3;
            }
            public TalkPacket(byte[] D)
            {
                Type = D[0];
                MsgLength = BitConverter.ToUInt16(D, 1);
                Data = new byte[MsgLength-3];
                Buffer.BlockCopy(D, 3, Data, 0, MsgLength - 3);
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
                    ConnectChat(ListMessages);
                }
                else
                    MessageBox.Show("Введите имя для вашего аккаунта!");
            }
            else
            {
                LeaveChat(ListMessages);
            }
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {
            if (Connected)
            {
                TalkPacket msg = new TalkPacket(3, textBoxMsg.Text);
                ListMessages.Items.Add(" Вы: " + textBoxMsg.Text);
                textBoxMsg.Text = "";

                foreach (Sub sub in Subs)
                {
                    IPEndPoint iep = sub.endPoint;
                    TcpClient client = new TcpClient(iep);
                    NetworkStream stream = client.GetStream();
                    stream.Write(msg.getBytes(), 0, msg.MsgLength);
                    stream.Close();
                    client.Close();
                }
            }
            else
                MessageBox.Show("Вы не подключены к чату.");
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Connected)
                LeaveChat(ListMessages);
        }
        private void ConnectChat(ListBox lb)
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
                receiveUDP = new Task(() => ReceiveConnection(lb));
                receiveTCP = new Task(() => ReceiveMessage(lb));
                receiveUDP.Start();
                receiveTCP.Start();
                lb.Items.Add("     Вы присоединились к разговору.");
            }
            catch (Exception ex)
            {
                client.Close();
                MessageBox.Show(ex.Message);
            }
        }
        private void LeaveChat(ListBox lb)
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
                cancelTokenSource.Cancel();
                lb.Items.Add("     Вы покинули нас.");
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
        private void ReceiveConnection(ListBox lb)
        {
            UdpClient client = new UdpClient(8005);
            client.JoinMulticastGroup(IPAddress.Parse("230.230.230.230"), 50);
            IPEndPoint remoteIp = null;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    byte[] data = client.Receive(ref remoteIp);
                    TalkPacket msg = new TalkPacket(data);
                    Sub sub = new Sub(msg.Data.ToString(), remoteIp);
                    if (msg.Type == 1)
                    {
                        if (!Subs.Contains(sub))
                            Subs.Add(sub); 
                        lb.Items.Add("     " + sub.name + " присоединился к разговору.");
                        TcpClient sender = new TcpClient(remoteIp);
                        NetworkStream stream = sender.GetStream();
                        msg = new TalkPacket(1, UserName);
                        stream.Write(msg.getBytes(), 0, msg.MsgLength);
                        stream.Close();
                        sender.Close();
                    }
                    else if (msg.Type == 0)
                    {
                        if (Subs.Contains(sub))
                        {
                            Subs.Remove(sub);
                            lb.Items.Add("     " + sub.name + " покинул нас.");
                        }
                    }
                    else
                        MessageBox.Show("An error occured!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            client.Close();
        }
        private void ReceiveMessage(ListBox lb)
        {
            TcpListener listener = new TcpListener(IPAddress.Parse(GetLocalIPAddress()), 50);
            listener.Start();
            byte[] data = new byte[65536];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    NetworkStream stream = client.GetStream();
                    stream.Read(data, 0, data.Length);
                    TalkPacket msg = new TalkPacket(data);
                    IPEndPoint iep = (IPEndPoint)client.Client.RemoteEndPoint;
                    if (msg.Type == 1)
                    {
                        Sub sub = new Sub(msg.Data.ToString(), iep);
                        if (!Subs.Contains(sub))
                        {
                            Subs.Add(sub);
                            lb.Items.Add("     " + sub.name + " присоединился к разговору.");
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
                        lb.Items.Add(name + ": " + Encoding.ASCII.GetString(msg.Data));
                    }
                    else
                        MessageBox.Show("An error occured!");
                    client.Close();
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            listener.Stop();
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
    public static class StringExtension
    {
        public static byte[] ToByteArr(this string str)
        {
            byte[] data = Encoding.ASCII.GetBytes(str);
            return data;
        }
    }
}