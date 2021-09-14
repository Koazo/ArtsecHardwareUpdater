using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Threading;
using FluentFTP;
using System.Net.Sockets;

namespace ArtsecHardwareUpdater
{
    public partial class FormMain : Form
    {

        public FormMain()
        {
            InitializeComponent();
        }

        const string helpstring = "SomeUdp <mode> [file] [address]\n" +
           "\tmode:\n" +
           "\t\t-s - sender mode\n" +
           "\t\t-r - receiver mode\n" +
           "\tfile - file to send\n" +
           "\taddress - host to send\n";
        const int sendPort = 20600; //Номер порта, который будет открываться для отправки пакетов
        const int receivePort = 20601; //Номер порта, который будет получать пакеты
        const int packetSize = 8192; //Макс размер пакета

        static UdpClient udpSender; //Экземпляр класса, для работы с Udp для отправки пакетов
        static UdpClient udpReceiver;//Экземпляр класса, для работы с Udp для получения пакетов

        /// <summary>
        /// Функция, для отправки пакетов
        /// </summary>
        /// <param name="path">путь файла</param>
        /// <param name="address">хост-получатель</param>
        static void Sender(string path, string address)
        {
            Console.WriteLine("Sender mode");
            IPAddress ipAddr;
            try
            {
                IPHostEntry entry = Dns.GetHostEntry(address); // Резловим введенный пользователем адрес
                ipAddr = entry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork); //получаем введеный пользователем адрес и проверяем адрес IpV4 или IpV6
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            IPEndPoint sendEndPoint = new IPEndPoint(ipAddr, sendPort); //Конечная точка получателя
            IPEndPoint receiveEndPoint = null; //Конечная точка Устройства, который ответит на принятый пакет
            try
            {
                udpReceiver = new UdpClient(receivePort); //Экземпляр класса, для получение файла с заданного порта
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            using (FileStream fsSource = new FileStream(path, FileMode.Open, FileAccess.Read)) //Работа с файлом
            {
                int numBytesToRead = (int)fsSource.Length; //Количество байтов на чтение
                int numBytesReaded = 0;//Количество байтов прочитано получателем
                string name = Path.GetFileName(path); //Получение имени файла, чтобы получатель знал какой файл с каким названием создать
                byte[] packetSend; //пакеты для отправки
                byte[] packetRecieve;//пакеты для приема
                packetSend = Encoding.Unicode.GetBytes(name); //Подготавливаем к отрпавке пакет с названием файла
                udpReceiver.Client.ReceiveTimeout = 5000; //время ожидания ответа
                udpSender.Send(packetSend, packetSend.Length, sendEndPoint); // отправка название файла получателю
                packetRecieve = udpReceiver.Receive(ref receiveEndPoint); //ожидаем ответа и записываем полученный ответ в переменную

                int parts = (int)fsSource.Length / packetSize; //количество пакетов
                if ((int)fsSource.Length % packetSize != 0)
                {
                    parts++;
                }
                packetSend = BitConverter.GetBytes(parts);
                udpSender.Send(packetSend, packetSend.Length, sendEndPoint); //оповещаем получателя о количестве входящих пакетов файла
                packetRecieve = udpReceiver.Receive(ref receiveEndPoint);

                int n = 0;
                packetSend = new byte[packetSize];
                for (int i = 0; i < parts - 1; i++)
                {
                    n = fsSource.Read(packetSend, 0, packetSize);
                    if (n == 0)
                    {
                        Console.WriteLine("Нет пакетов!");
                        break;
                    }
                    numBytesReaded += n;
                    numBytesToRead -= n;
                    udpSender.Send(packetSend, packetSend.Length, sendEndPoint); //отправка файла
                    packetRecieve = udpReceiver.Receive(ref receiveEndPoint);
                }
                packetSend = new byte[numBytesToRead];
                n = fsSource.Read(packetSend, 0, numBytesToRead);
                udpSender.Send(packetSend, packetSend.Length, sendEndPoint); //отправка последнего пакета файла
                packetRecieve = udpReceiver.Receive(ref receiveEndPoint);
            }
            Console.WriteLine("file sent successfully");
        }

        static void Receiver()
        {
            Console.WriteLine("Receiver mode");
            IPEndPoint receiveEndPoint = null;
            try
            {
                udpReceiver = new UdpClient(sendPort);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            byte[] packetSend = new byte[1];
            byte[] packetReceive;
            packetSend[0] = 1;
            packetReceive = udpReceiver.Receive(ref receiveEndPoint);
            IPEndPoint sendEndPoint = new IPEndPoint(receiveEndPoint.Address, receivePort);
            string name = Encoding.Unicode.GetString(packetReceive);
            udpSender.Send(packetSend, packetSend.Length, sendEndPoint);
            udpReceiver.Client.ReceiveTimeout = 5000;
            packetReceive = udpReceiver.Receive(ref receiveEndPoint);
            int parts = BitConverter.ToInt32(packetReceive, 0);
            using (FileStream fsDest = new FileStream(name, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < parts; i++)
                {
                    packetReceive = udpReceiver.Receive(ref receiveEndPoint);
                    fsDest.Write(packetReceive, 0, packetReceive.Length);
                    udpSender.Send(packetSend, packetSend.Length, sendEndPoint);
                }
            }
            Console.WriteLine("File recieved");
        }

        async private void buttonStart_Click(object sender, EventArgs e)
        {
            string ip_address = textBoxIP.Text;
            int port = int.Parse(textBoxPort.Text);
            int delay = int.Parse(textBoxDelay.Text);
            string file_path = textBoxFilePath.Text;
            
            richTextBoxLog.Text += "Начата загрузка файла!";
            UdpClient udpSender = new UdpClient();

            IPAddress ipAddr;
            try
            {
                /*                ipAddr = IPAddress.Parse(ip_address);
                                IPEndPoint point = new IPEndPoint(ipAddr, port);

                                byte[] setBaudrate_cmd = { 0x46, 0x01, 0x02, 0xD2, 0xE5, 0xF1, 0xF2 };
                                udpSender.Send(setBaudrate_cmd, setBaudrate_cmd.Length, point);*/

                /*IPHostEntry entry = Dns.GetHostEntry(ip_address);
                ipAddr = entry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);*/
                ipAddr = IPAddress.Parse(ip_address);
                IPEndPoint sendEndPoint = new IPEndPoint(ipAddr, port);
                int n = 0;
                
/*                using (FileStream fs = new FileStream(file_path, FileMode.Open, FileAccess.Read))
                {*/
                    byte[] packetSend = { 0x00, 0x02, 0x73, 0x6c, 0x6f, 0x77, 0x5f, 0x62, 0x6c, 0x69, 0x6e, 0x6b, 0x2e, 0x62, 0x69, 0x6e, 0x00, 0x6f, 0x63, 0x74, 0x65, 0x74, 0x00 };
                    /*n = fs.Read(packetSend,0, packetSend.Length);*/
                    udpSender.Send(packetSend, packetSend.Length, sendEndPoint);


                /*}*/
            }
            catch (Exception ex)
            {
                richTextBoxLog.ForeColor = Color.Red;
                richTextBoxLog.Text += ex.Message;
            }
        }
        

        private void buttonChooseFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            /*openFileDialog.Filter = "Firmware File(*.bin, *.hex)|*.bin,*.hex";*/
            openFileDialog.Filter = "Firmware File(*.bin)|*.bin|Firmware File(*.hex)|*.hex";
            if(openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    textBoxFilePath.Text = openFileDialog.FileName;           
                }
                catch
                {
                    DialogResult result = MessageBox.Show("Невозможно выбрать указанный файл!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void buttonClearLogs_Click(object sender, EventArgs e)
        {
            richTextBoxLog.Clear();
        }
    }
}


// ПЕРВЫЙ НЕУДАЧНЫЙ СПОСОБ ПОДКЛЮЧЕНИЯ
//try
//{
//    using (var connection = new FtpClient())
//    {
//        connection.Host = ip_address;
//        richTextBoxLog.Text += "Устанавливается соединение...\n";
//        connection.ConnectTimeout = 1000;
//        await connection.ConnectAsync();
//        if (connection.IsConnected)
//        {
//            richTextBoxLog.ForeColor = Color.Green;
//            richTextBoxLog.Text += "Соединение успешно установлено!\n";
//            return;
//        }
//        else
//        {
//            richTextBoxLog.ForeColor = Color.Red;
//            richTextBoxLog.Text += "Ошибка подключения! Повторное соединение...\n";
//            await Task.Delay(delay);
//        }
//    }

//}
//catch (Exception ex)
//{
//    richTextBoxLog.Text += ex.Message;
//    richTextBoxLog.Text += "sdfsfdsf";
//}
// ПЕРВЫЙ НЕУДАЧНЫЙ СПОСОБ ПОДКЛЮЧЕНИЯ
//try
//{
//    using (var connection = new FtpClient())
//    {
//        connection.Host = ip_address;
//        richTextBoxLog.Text += "Устанавливается соединение...\n";
//        connection.ConnectTimeout = 1000;
//        await connection.ConnectAsync();
//        if (connection.IsConnected)
//        {
//            richTextBoxLog.ForeColor = Color.Green;
//            richTextBoxLog.Text += "Соединение успешно установлено!\n";
//            return;
//        }
//        else
//        {
//            richTextBoxLog.ForeColor = Color.Red;
//            richTextBoxLog.Text += "Ошибка подключения! Повторное соединение...\n";
//            await Task.Delay(delay);
//        }
//    }

//}
//catch (Exception ex)
//{
//    richTextBoxLog.Text += ex.Message;
//    richTextBoxLog.Text += "sdfsfdsf";
//}