using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;

namespace chattingServer
{
    public partial class Form1 : Form
    {
        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate appendTextDel;
        Socket mainSock;
        IPAddress thisAddress;

        public Form1()
        {
            InitializeComponent();
            // 소켓 연결하기
            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            appendTextDel = new AppendTextDelegate(AppendText);
        }
        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        void AppendText(Control ctrl, string s)
        {
            if (ctrl.InvokeRequired)
            {
                ctrl.Invoke(appendTextDel, ctrl, s);
            }
            else
            {
                string source = ctrl.Text;
                ctrl.Text = source + Environment.NewLine + s;
            }
        }
        void OnFormLoaded(object sender, EventArgs e)
        {
            IPHostEntry he = Dns.GetHostEntry(Dns.GetHostName());

            IPAddress defaultHostAddress = null;
            foreach (IPAddress addr in he.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    thisAddress = addr;
                    break;
                }
            }

            // 주소가 없다면 로컬호스트 주소 사용
            if (defaultHostAddress == null)
            {
                defaultHostAddress = IPAddress.Loopback;

            }
            // 처음으로 발견되는 ip주소를 txtIp에 입력
            txtIp.Text = thisAddress.ToString();
        }

        // 서버 시작 및 접속 연결 대기(비동기적)
        void BeginStartServer(object sender, EventArgs e)
        {
            int port;
            if (!int.TryParse(txtPort.Text, out port))
            {
                MessageBox.Show("포트 번호가 잘못 입력되었거나 입력되지 않았습니다.", "확인", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtPort.Focus();
                txtPort.SelectAll();
                return;
            }

            IPEndPoint serverEP = new IPEndPoint(thisAddress, port);
            mainSock.Bind(serverEP);
            mainSock.Listen(10);

            mainSock.BeginAccept(AcceptCallback, null);
        }

        List<Socket> connectedClients = new List<Socket>();
        void AcceptCallback(IAsyncResult ar)
        {
            Socket client = mainSock.EndAccept(ar);

            mainSock.BeginAccept(AcceptCallback, null);

            AsyncObject obj = new AsyncObject(4096);
            obj.WorkingSocket = client;

            connectedClients.Add(client);

            AppendText(txtMessage, string.Format("클라이언트 ({0})가 연결되었습니다.", client.RemoteEndPoint));

            client.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
        }

        void DataReceived(IAsyncResult ar)
        {
            AsyncObject obj = (AsyncObject)ar.AsyncState;

            // 수신 종료
            int received = obj.WorkingSocket.EndReceive(ar);

            if (received <= 0)
            {
                obj.WorkingSocket.Close();
                return;
            }

            // 텍스트를 UTF8로 인코딩
            string text = Encoding.UTF8.GetString(obj.Buffer);

            string[] sendMsg = text.Split('\x01');
            string ip = sendMsg[0];
            string msg = sendMsg[1];

            AppendText(txtMessage, string.Format("{0}: {1}", ip, msg));

            // 역순으로 데이터 보내기
            for (int i = connectedClients.Count - 1; i >= 0; i--)
            {
                Socket socket = connectedClients[i];
                if (socket != obj.WorkingSocket)
                {
                    try
                    {
                        socket.Send(obj.Buffer);
                    }
                    catch
                    {
                        try
                        {
                            socket.Dispose();
                        }
                        catch { }
                        // 오류 발생 시 리스트에서 삭제
                        connectedClients.RemoveAt(i);
                    }
                }
            }

            obj.ClearBuffer();

            obj.WorkingSocket.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
        }

        void OnSendData(object sender, EventArgs e)
        {
            if (!mainSock.IsBound)
            {
                MessageBox.Show("서버가 실행되고 있지 않습니다", "확인", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string tts = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(tts))
            {
                MessageBox.Show("텍스트가 입력되지 않습니다", "확인", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtMessage.Focus();
                return;
            }

            byte[] bDts = Encoding.UTF8.GetBytes(thisAddress.ToString() + '\x01' + tts);

            // 연결된 모든 클라이언트에게 전송한다.
            for (int i = connectedClients.Count - 1; i >= 0; i--)
            {
                Socket socket = connectedClients[i];
                try
                {
                    socket.Send(bDts);
                }
                catch
                {
                    try
                    {
                        socket.Dispose();
                    }
                    catch { }
                    connectedClients.RemoveAt(i);
                }
            }

            AppendText(txtMessage, string.Format("{0}: {1}", thisAddress.ToString(), tts));
            txtMessage.Clear();
        }
    }

    public class AsyncObject
    {
        public byte[] Buffer;
        public Socket WorkingSocket;
        public readonly int BufferSize;
        public AsyncObject(int bufferSize)
        {
            BufferSize = bufferSize;
            Buffer = new byte[BufferSize];
        }

        public void ClearBuffer()
        {
            Array.Clear(Buffer, 0, BufferSize);
        }
    }
}
