using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TalkBox
{
    public partial class Form1 : Form
    {
        String UserName;
        internal bool Connected = false;
        


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
                if (MsgLength > 3)
                {
                    Data = new byte[MsgLength-3];
                    Buffer.BlockCopy(D, 3, Data, 0, MsgLength - 3);
                }
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
            if (Connected)
            {
                UdpClient client = new UdpClient();
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("230.230.230.230"), 8005);
                TalkPacket msg = new TalkPacket(3, textBoxMsg.Text);
                textBoxMsg.Text = "";
                client.Send(msg.getBytes(), msg.MsgLength,endPoint);
                client.Close();
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
                Task receiveUDP = new Task(ReceiveConnection);
                Task receiveTCP = new Task(ReceiveMessage);
                receiveUDP.Start();
                receiveTCP.Start();
                MessageBox.Show("Вы подключились к чату");
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
                MessageBox.Show("Вы отключились от чата");
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
            UdpClient client = new UdpClient(8005);
            client.JoinMulticastGroup(IPAddress.Parse("230.230.230.230"), 50);
            IPEndPoint remoteIp = null;
            try
            {
                while (Connected)
                {
                    byte[] data = client.Receive(ref remoteIp);
                    TalkPacket msg = new TalkPacket(data);
                    if (msg.Type == 1)
                    {
                        TcpClient sender = new TcpClient(remoteIp);
                        NetworkStream stream = sender.GetStream();
                        msg = new TalkPacket(1, UserName);
                        stream.Write(msg.getBytes(), 0, msg.MsgLength);
                        stream.Close();
                        sender.Close();
                    }
                    else if (msg.Type == 0)
                    { 
                    
                    }
                    else
                        MessageBox.Show("An error occured!");
                }
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
        private void ReceiveMessage()
        {
            TcpListener listener = new TcpListener(IPAddress.Parse("230.230.230.230"), 50);
            listener.Start();
            byte[] data = new byte[65536];
            try
            {
                while (Connected)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    NetworkStream stream = client.GetStream();

                    stream.Read(data, 0, data.Length);
                    TalkPacket msg = new TalkPacket(data);
                    // Отображение полученного сообщения
                    // ListMessages.Items.Add(Encoding.ASCII.GetString(msg.Data));
                    client.Close();
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                listener.Stop();
            }
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