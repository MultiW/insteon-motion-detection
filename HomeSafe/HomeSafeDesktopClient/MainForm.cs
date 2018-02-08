using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HomeSafeAgentCore;
using System.Threading;
using System.Net;

namespace HomeSafeDesktopClient
{
    public partial class MainForm : Form, ICameraDataReceiver
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private string commandUrl = "http://{0}/decoder_control.cgi?user={1}&pwd={2}&command={3}";
        
        bool cameraThreadCreated = false;
        
        private void button1_Click(object sender, EventArgs e)
        {
            Camera camera = new Camera("192.168.1.102:25106", "xinwang", "mengmeng");

            camera.CameraDataReceiver = this;

            if (!cameraThreadCreated)
            {
                camera.StartCameraThread();
                cameraThreadCreated = true;
            }
            else
            {
                camera.StopCameraThread();
                cameraThreadCreated = false;
            }

        }

        public void OnCameraDataReady(Image image)
        {

            pictureBox1.Image = image;
        }

        private void buttonLeft_Click(object sender, EventArgs e)
        {
            SendCommand(commandUrl, "6");
            Thread.Sleep(250);
            SendCommand(commandUrl, "1");
        }

        private void buttonRight_Click(object sender, EventArgs e)
        {
            SendCommand(commandUrl, "4");
            Thread.Sleep(250);
            SendCommand(commandUrl, "1");
        }

        private void SendCommand(string commandUrl, string command, string optionalValue = "")
        {
            string url = "";
            if (!optionalValue.Equals("")) url = String.Format(commandUrl, "192.168.1.102:25106", "xinwang", "mengmeng", command, optionalValue);
            else url = String.Format(commandUrl, "192.168.1.102:25106", "xinwang", "mengmeng", command);
            WebClient wc = new WebClient();
            wc.DownloadString(url);
        }
    }
}
