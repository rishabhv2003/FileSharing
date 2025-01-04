using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace FileSharing
{
    public partial class Form1 : Form
    {
        private const int Port = 8888; // Port for communication
        private TcpListener tcpListener;
        private TcpClient tcpClient;
        private Thread listenerThread;

        public Form1()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            // Open File Dialog to select a file to send
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = openFileDialog.FileName;
                }
            }
        }
        private void btnSend_Click(object sender, EventArgs e)
        {
            string filePath = txtFilePath.Text;
            string receiverIP = txtReceiverIP.Text;

            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(receiverIP))
            {
                MessageBox.Show("Please select a file and enter the receiver's IP address.");
                return;
            }

            try
            {
                // Create a TCP connection to the receiver
                tcpClient = new TcpClient(receiverIP, Port);
                using (NetworkStream networkStream = tcpClient.GetStream())
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    // Prepare file metadata
                    string fileName = Path.GetFileName(filePath);
                    byte[] fileNameBytes = System.Text.Encoding.UTF8.GetBytes(fileName);
                    byte[] fileNameLengthBytes = BitConverter.GetBytes(fileNameBytes.Length);
                    byte[] fileSizeBytes = BitConverter.GetBytes(fileStream.Length);

                    // Send file metadata: file name length, file name, and file size
                    networkStream.Write(fileNameLengthBytes, 0, fileNameLengthBytes.Length);
                    networkStream.Write(fileNameBytes, 0, fileNameBytes.Length);
                    networkStream.Write(fileSizeBytes, 0, fileSizeBytes.Length);

                    // Send the file data
                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        networkStream.Write(buffer, 0, bytesRead);
                    }
                }

                txtStatus.Text = "File sent successfully!";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Error: {ex.Message}";
            }
        }

        private void btnStartListening_Click(object sender, EventArgs e)
        {
            txtStatus.Text = "Listening for incoming files...";
            listenerThread = new Thread(ListenForConnections);
            listenerThread.Start();
        }

        private void ListenForConnections()
        {
            try
            {
                // Set up the TCP listener
                tcpListener = new TcpListener(IPAddress.Any, Port);
                tcpListener.Start();

                while (true)
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                txtStatus.Invoke((Action)(() => txtStatus.Text = $"Error: {ex.Message}"));
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            // You can add any initialization code here if needed
            txtStatus.Text = "Ready to send or receive files.";
        }
        private void HandleClient(TcpClient client)
        {
            try
            {
                using (NetworkStream networkStream = client.GetStream())
                {
                    // Step 1: Receive the file name length
                    byte[] fileNameLengthBytes = new byte[4]; // Int32 for file name length
                    networkStream.Read(fileNameLengthBytes, 0, fileNameLengthBytes.Length);
                    int fileNameLength = BitConverter.ToInt32(fileNameLengthBytes, 0);

                    // Step 2: Receive the file name
                    byte[] fileNameBytes = new byte[fileNameLength];
                    networkStream.Read(fileNameBytes, 0, fileNameBytes.Length);
                    string fileName = System.Text.Encoding.UTF8.GetString(fileNameBytes).TrimEnd('\0');

                    // Step 3: Validate the file name
                    fileName = Path.GetFileName(fileName); // Sanitize the file name
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        txtStatus.Invoke((Action)(() => txtStatus.Text = "Error: Invalid file name received."));
                        return;
                    }

                    // Step 4: Receive the file size
                    byte[] fileSizeBytes = new byte[8]; // Int64 for file size
                    networkStream.Read(fileSizeBytes, 0, fileSizeBytes.Length);
                    long fileSize = BitConverter.ToInt64(fileSizeBytes, 0);

                    // Step 5: Save the file
                    string savePath = Path.Combine(Application.StartupPath, fileName);
                    using (FileStream fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead;
                        long bytesReceived = 0;

                        while (bytesReceived < fileSize && (bytesRead = networkStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fileStream.Write(buffer, 0, bytesRead);
                            bytesReceived += bytesRead;
                        }
                    }
                }

                txtStatus.Invoke((Action)(() => txtStatus.Text = "File received successfully!"));
            }
            catch (Exception ex)
            {
                txtStatus.Invoke((Action)(() => txtStatus.Text = $"Error: {ex.Message}"));
            }
        }



    }
}
