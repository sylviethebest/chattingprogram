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

namespace chattingClient
{
    public partial class Form1 : Form
    {
        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate textAppender;
        Socket mainSock;

        public Form1()
        {
            InitializeComponent();
            // 소켓 연결하기
            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            textAppender = new AppendTextDelegate(AppendText);
        }

        void AppendText(Control ctrl, string s)
        {
            if (ctrl.InvokeRequired) ctrl.Invoke(textAppender, ctrl, s);
            else
            {
                string source = ctrl.Text;
                ctrl.Text = source + Environment.NewLine + s;
            }
        }
        void OnFormLoaded(object sender, EventArgs e)
        {
            IPHostEntry he = Dns.GetHostEntry(Dns.GetHostName());

            // 처음으로 발견되는 주소 사용
            IPAddress defaultHostAddress = null;
            foreach (IPAddress addr in he.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    defaultHostAddress = addr;
                    break;
                }
            }

            // 주소가 없다면 로컬호스트 주소 사용
            if (defaultHostAddress == null)
            {
                defaultHostAddress = IPAddress.Loopback;

            }
            txtIp.Text = defaultHostAddress.ToString();
        }

        void OnConnectToServer(object sender, EventArgs e)
        {
            if (mainSock.Connected)
            {
                MessageBox.Show("이미 연결되어 있습니다", "확인", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int port;
            if (!int.TryParse(txtPort.Text, out port))
            {
                MessageBox.Show("포트 번호가 잘못 입력되었거나 입력되지 않았습니다.", "확인", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtPort.Focus();
                txtPort.SelectAll();
                return;
            }

            try
            {
                mainSock.Connect(txtIp.Text, port);
            }
            catch
            {
                MessageBox.Show("연결에 실패했습니다", "확인", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AppendText(txtHistory, "서버와 연결되었습니다.");

            // 연결 완료, 서버에서 데이터가 올 수 있으므로 수신 대기한다.
            AsyncObject obj = new AsyncObject(4096);
            obj.WorkingSocket = mainSock;
            mainSock.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0, DataReceived, obj);
        }

        // 데이터 수신
        void DataReceived(IAsyncResult ar)
        {
            AsyncObject obj = (AsyncObject)ar.AsyncState;

            int received = obj.WorkingSocket.EndReceive(ar);

            if (received <= 0)
            {
                obj.WorkingSocket.Close();
                return;
            }

            // 텍스트를 UTF8로 인코딩
            string text = Encoding.UTF8.GetString(obj.Buffer);

            string[] receivedMsg = text.Split('\x01');
            string ip = receivedMsg[0];
            string msg = receivedMsg[1];

            AppendText(txtHistory, string.Format("{0}: {1}", ip, msg));

            // 데이터를 받은 후 다시 버퍼를 비워기
            obj.ClearBuffer();

            // 수신 대기
            obj.WorkingSocket.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
        }

        void OnSendData(object sender, EventArgs e)
        {
            // 서버가 대기중인지 확인
            if (!mainSock.IsBound)
            {
                MessageBox.Show("서버가 실행되고 있지 않습니다", "확인", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 보낼 텍스트
            string tts = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(tts))
            {
                MessageBox.Show("텍스트를 입력해주세요", "확인", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtMessage.Focus();
                return;
            }

            IPEndPoint ip = (IPEndPoint)mainSock.LocalEndPoint;
            string addr = ip.Address.ToString();

            byte[] bDts = Encoding.UTF8.GetBytes(addr + '\x01' + tts);

            mainSock.Send(bDts);

            AppendText(txtHistory, string.Format("{0}: {1}", addr, tts));
            txtMessage.Clear();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }

    public class AsyncObject
    {
        // 비동기 작업에서 사용하는 소켓과 해당 작업에 대한 데이터 버퍼를 저장하는 클래스
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
