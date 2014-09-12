using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Lidgren.Network;

namespace LidgrenChatApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        const int MAX_LINES = 50000;
        NetPeerConfiguration config;
        NetClient client;
        INetEncryption algo;

        private void Form1_Load(object sender, EventArgs e)
        {
            // Set up client
            config = new NetPeerConfiguration("LidgrenChat");
            string serverHostName = "localhost";
            int serverPort = 7777;
            algo = new NetXtea("SecretKey0101");

            if (File.Exists("clientconfig.txt"))
            {
                using (StreamReader sr = new StreamReader("clientconfig.txt"))
                {
                    serverHostName = sr.ReadLine();
                    serverPort = Convert.ToInt32(sr.ReadLine());
                    algo = new NetXtea(sr.ReadLine());
                }
            }
            else
            {

            }
            //config.Port = 25568;
            client = new NetClient(config);
            client.Start();
            client.Connect(serverHostName, serverPort);
        }

        /// <summary>
        /// Obtain messages when tick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void updateTimer_Tick(object sender, EventArgs e)
        {
            NetIncomingMessage inMsg;
            while ((inMsg = client.ReadMessage()) != null)
            {
                bool msgDecrypted = inMsg.Decrypt(algo);
                switch (inMsg.MessageType)
                {
                    case NetIncomingMessageType.StatusChanged:
                        txtBoxInfo.AppendText("New status: " + client.Status + " (Reason: " + inMsg.ReadString() + ")" + Environment.NewLine);
                        break;

                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.WarningMessage:
                        txtBoxInfo.AppendText(inMsg.ReadString() + Environment.NewLine);
                        break;

                    case NetIncomingMessageType.Data:
                        txtBoxInfo.AppendText(inMsg.ReadString());
                        break;

                    default:
                        txtBoxInfo.AppendText("Unhandled type: " + inMsg.MessageType + 
                            "; Read string: " + inMsg.ReadString() + Environment.NewLine);
                        break;
                }
            }

            // Make sure the number of lines is within limit
            if (txtBoxInfo.Lines.Length > MAX_LINES)
            {
                txtBoxInfo.Select(0, txtBoxInfo.GetFirstCharIndexFromLine(1)); // select the first line
                txtBoxInfo.SelectedText = "";
            }
        }

        private void textBoxInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (!String.IsNullOrEmpty(txtBoxInput.Text))
                {
                    SendStringMessage(txtBoxInput.Text);
                    txtBoxInput.Text = String.Empty;
                }
            }
        }

        void SendStringMessage(string msgStr)
        {
            NetOutgoingMessage outMsg = client.CreateMessage();
            outMsg.Write(msgStr);
            outMsg.Encrypt(algo);
            client.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            client.Disconnect("disconnected.");
        }
    }
}
