using log4net;
using log4net.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.SessionState;
using System.Windows.Forms;
using ZedGraph;

namespace _JMLED
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }


        private CommPort comm1 = new CommPort();

        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        // 配置 log4net


        private DateTime currentTime= System.DateTime.Now;
   

        private StringBuilder builder = new StringBuilder();//文字队列。
        private long received_count = 0;//接收计数

        //指示灯是否打开
        bool seriaport1_open = false;

        int recCount = 20;

        //是否要画曲线
        bool isDrawLine = true;

        //待发送的命令
        byte[] cmd = new byte[7];

        //绘图相关
        public struct CurveStruct
        {
            public PointPairList pointList;//曲线数据
            public Color lineColor;//曲线颜色
            public SymbolType lineStyle;//线型
            public string curveName;//名称
            public LineItem curve;    //曲线
            public float scaleFactor;//比例系数
            public byte lineWidth; //线宽
        }

        //private Hashtable speedHashTable = new Hashtable();//用来保存速度曲线        .
        //private Hashtable distanceHashTable = new Hashtable();//用来保存距离曲线
        private GraphPane mGraphPane;
        private long tickStart = 0;
 
        //double previousTime = 0;

        /**
        //增加4个温度曲线
        CurveStruct mBtemp = new CurveStruct();
        CurveStruct mCtemp = new CurveStruct();
        CurveStruct sBtemp = new CurveStruct();
        CurveStruct sCtemp = new CurveStruct();

        //增加2个功率曲线
        CurveStruct mWpower = new CurveStruct();
        CurveStruct sWpower = new CurveStruct();
        **/

        //增加2个曲线
        CurveStruct speedLine = new CurveStruct();
        CurveStruct distanceLine = new CurveStruct();
        //      CurveStruct acceleratedLine = new CurveStruct();//加速度曲线


        private double m_dis = 2.4;//两线之间距离
        private double m_maxvalue = 10;//最远距离
        private double xmin=0;
        private double xmax=0;
        private double ymin= 0;
        private double ymax = 0;


        private bool SerialPort_Open(CommPort comm, String port, String msg = "")
        {

            //根据当前串口对象，来判断操作
            if (comm.IsOpen)
            {
                comm.Closing = true;
                while (comm.Listenling) Application.DoEvents();
                //打开时点击，则关闭串口
                comm.Close();
                return false;
            }
            else
            {
                //初始化SerialPort对象
                comm.NewLine = "\r\n";
                comm.RtsEnable = true;
                comm.PortName = port;
                comm.BaudRate = 115200;
                comm.Message = msg;//串口欢迎词

                comm.DataReceived += comm_DataReceived;//添加串口DataReceived事件注册
                try
                {
                    comm.Open();
                    tickStart = Environment.TickCount;
                    return true;
                }
                catch (Exception ex)
                {
                    //捕获到异常信息，创建一个新的comm对象，之前的不能用了
                    comm = new CommPort();
                    //现实异常信息给客户(一般为串口被占用了)
                    MessageBox.Show(ex.Message);
                    Trace.TraceError(ex.Message);
                    Trace.Flush();
                    return false;
                }
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (comboBox1.Text == string.Empty)
            {
                MessageBox.Show("请选择串口号");
            }
            else
            {
                if (seriaport1_open == false)
                {
                    //打开串口操作
                    if (SerialPort_Open(comm1, comboBox1.Text, "feiji")) //如果串口打开成功
                    {
                        seriaport1_open = true;
                        pictureBox1.BackColor = Color.Green;
                        button1.Text = "关闭串口";
                    }

                }
                else
                {
                    //关闭串口操作
                    SerialPort_Close(comm1);//关闭串口函数
                    seriaport1_open = false;
                    pictureBox1.BackColor = Color.White;
                    button1.Text = "打开串口";
                }
            }
        }
        private void SerialPort_Close(CommPort comm) //关闭串口函数
        {
            comm.Close();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            //初始化下拉串口名称列表框
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            comboBox1.Items.AddRange(ports);

            Thread thread = new Thread(comm_DataSendThread);
            thread.IsBackground = true;
            thread.Start();

            //曲线图
            CreateGraph(zedGraphControl1);


            chart1.ChartAreas[0].AxisY.Minimum = 0;
            chart1.ChartAreas[0].AxisY.Maximum = 20;
            chart1.ChartAreas[0].AxisY.Interval = 1;
            chart1.ChartAreas[0].AxisX.Minimum = -10;
            chart1.ChartAreas[0].AxisX.Maximum = 10;
            chart1.ChartAreas[0].AxisX.Interval = 1;

            for (int i = -10; i <= 10; i++)
            {

                chart1.Series[0].Points.AddXY(i, Math.Abs(i));
            }

            btn_sure_Click(null, null);

            SetSize();
            // 配置 log4net
            XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));
        }
        private void CreateGraph(ZedGraphControl zgc)
        {

            //得到GraphPane的引用

            mGraphPane = zgc.GraphPane;

            mGraphPane.Title.Text = "速度曲线";
            mGraphPane.XAxis.Title.Text = "时间";
            mGraphPane.YAxis.Title.Text = "速度";
            mGraphPane.Y2Axis.Title.Text = "距离";

            zgc.ScrollGrace = 0;
    

            mGraphPane.XAxis.Type = AxisType.Date;
            mGraphPane.XAxis.Scale.Format = "HH:mm:ss";
            mGraphPane.XAxis.Scale.MinAuto = true;
            mGraphPane.XAxis.Scale.MaxAuto = true;
            mGraphPane.XAxis.Scale.MajorStepAuto = true;
            mGraphPane.XAxis.Scale.MinorStepAuto = true;
            mGraphPane.XAxis.Scale.MinorUnit = DateUnit.Millisecond;




            mGraphPane.Y2Axis.IsVisible = true;
            mGraphPane.Y2Axis.Scale.Min = 0;
            mGraphPane.Y2Axis.Scale.MaxAuto = true;
            mGraphPane.Y2Axis.Scale.MajorStepAuto = true;
            mGraphPane.Y2Axis.Scale.MinorStepAuto = true;









            // 是否允许横向缩放
            this.zedGraphControl1.IsEnableHZoom = true;
            //是否允许纵向缩放
            this.zedGraphControl1.IsEnableVZoom = false;
            //是否允许缩放
            this.zedGraphControl1.IsEnableZoom = false;
            //是否显示右键菜单
            this.zedGraphControl1.IsShowContextMenu = true;
            //复制图像时是否显示提示信息
            this.zedGraphControl1.IsShowCopyMessage = true;
            //鼠标在图表上移动时是否显示鼠标所在点对应的坐标 默认为false
            this.zedGraphControl1.IsShowCursorValues = false;
            //是否显示横向滚动条
            this.zedGraphControl1.IsShowHScrollBar = true;
            //是否显示纵向滚动条
            this.zedGraphControl1.IsShowVScrollBar = true;
            //鼠标经过图表上的点时是否显示该点所对应的值 默认为false
            this.zedGraphControl1.IsShowPointValues = true;
            //使用滚轮时以鼠标所在点为中心进行缩放还是以图形中心进行缩放
            this.zedGraphControl1.IsZoomOnMouseCenter = false;

            this.zedGraphControl1.IsEnableVPan = true;//禁止鼠标竖向拖动
            this.zedGraphControl1.IsEnableHPan = true;
            


            //清除原有曲线
            if (mGraphPane.CurveList.Count != 0)
            {
                mGraphPane.CurveList.Clear();
                mGraphPane.GraphObjList.Clear();
                //speedHashTable.Clear();
                //distanceHashTable.Clear();
            }




          

      

            speedLine.curveName = "速度";
            speedLine.pointList = new PointPairList();
            speedLine.lineColor = Color.Green;
            speedLine.lineStyle = SymbolType.Circle;
            speedLine.lineWidth = 2;
            speedLine.scaleFactor = 1;
            speedLine.curve = mGraphPane.AddCurve(
                speedLine.curveName,
                speedLine.pointList,
                speedLine.lineColor,
                speedLine.lineStyle);
            speedLine.curve.Line.Width = speedLine.lineWidth;
            speedLine.curve.YAxisIndex = 0;
      

//            speedHashTable.Add("speedLine", speedLine);







            distanceLine.curveName = "距离";
            distanceLine.pointList = new PointPairList();
            distanceLine.lineColor = Color.Red;
            distanceLine.lineStyle = SymbolType.Circle;
            distanceLine.lineWidth = 2;
            distanceLine.scaleFactor = 1;
            distanceLine.curve = mGraphPane.AddCurve(
                distanceLine.curveName,
                distanceLine.pointList,
                distanceLine.lineColor,
                distanceLine.lineStyle);
            distanceLine.curve.Line.Width = distanceLine.lineWidth;
            distanceLine.curve.IsY2Axis = true;

            //            distanceHashTable.Add("distanceLine", distanceLine);


           

            if (isDrawLine)
            {
                //更新X和Y轴的范围
                zedGraphControl1.AxisChange();
                //更新图表
                zedGraphControl1.Invalidate();
            }


        }



        List<byte> buffer = new List<byte>(4096);
        void comm_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            Console.WriteLine($"接收到数据: {indata}");
            this.richTextBox2.AppendText(DateTime.Now.ToString() + " 收到数据:" +indata + "\r");
            this.richTextBox2.ScrollToCaret();
            // 处理接收到的数据
            string filteredData = FilterData(indata);
            if (!string.IsNullOrEmpty(filteredData))
            {
               // Console.WriteLine($"接收到符合条件的数据: {filteredData}");
         



                TimeSpan ts = System.DateTime.Now - currentTime;
                if (ts.TotalMinutes > 5)
                {
                    log.Info(" ");

                    using (Bitmap bitmap = mGraphPane.GetImage())
                    {
                        String filename = "D:/image/" + currentTime.ToString("yyMMddHHmm") + ".jpg";
                        bitmap.Save(filename, ImageFormat.Jpeg);
                    }
                    speedLine.pointList.Clear();
                    distanceLine.pointList.Clear();
                }
                log.Info(DateTime.Now.ToString() + " " + filteredData.ToString());

                currentTime = System.DateTime.Now;

                codeAnalysis(filteredData.ToString());//解析数据
            }
         
        }

        private string FilterData(string data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in data)
            {
                if (c == 'w')
                {
                    sb.Clear();
                    sb.Append(c);
                }
                else if ((c == 'i' || c == 'o' || c=='u'  )&& sb.Length > 0)
                {
                    sb.Append(c);
                    return sb.ToString();
                }
                else if (sb.Length > 0)
                {
                    sb.Append(c);
                }
            }
            return null;
            throw new NotImplementedException();
        }

      

        public void codeAnalysis(String code)
        {
            //   currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();//double类型的毫秒级别的时间戳

            XDate xDate = new XDate(DateTime.Now);


            //  currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            String time = System.DateTime.Now.ToString("HH:mm:ss.ff");
            this.LTime.Text = time;
            // 使用正则表达式提取速度和距离信息
            //string pattern = @"W (\d+(\.\d+)?) Km/h (\d+) mm [iOu]$";
            //无距离信息的正则表达式
            //string pattern = @"W (\d+(\.\d+)?) Km/h [iOu]$";
            string pattern = @"^w (\d+\.\d+) km/h(?: (\d{4}) mm)? ([iou])";


            MatchCollection matches = Regex.Matches(code, pattern);
              foreach (Match match in matches)
              {
                // 提取速度信息
                double speed = double.Parse(match.Groups[1].Value);

                // 提取距离信息
                double distance = match.Groups[2].Success ? double.Parse(match.Groups[2].Value) : 0;

                // 提取方向信息
                string direction = match.Value.Substring(match.Index + match.Length - 1, 1);

                  this.LSpeed.Text = speed + " km/h";
                  this.LDistance.Text = distance + " mm";
                this.LDirection.Text = direction;

                string endChar = match.Groups[3].Value;

                speedLine.pointList.Add(xDate, speed);
                distanceLine.pointList.Add(xDate, distance);

                  //Scale xScale = zedGraphControl1.GraphPane.XAxis.Scale;
                  
                  //if (currentTime.ToOADate() > xScale.Max - xScale.MajorStep)
                  //{
                  //    xScale.Max = currentTime.ToOADate() + xScale.MajorStep;
                  //    xScale.Min = xScale.Max - 3;
                  //}
                  zedGraphControl1.AxisChange();//更新X和Y轴范围
                  zedGraphControl1.Invalidate();//更新图表
              }
            

        }

     


        public void comm_DataSendThread()
        {
            while (true)
            {
                if (comm1.IsOpen)
                {
                   // comm1.Write(cmd, 0, cmd.Length);
                }
                Thread.Sleep(1000);
            }
        }

        void comm_DataSend(string s) //串口发送数据函数
        {
            string hex = strToToHexByte(s)+" 0d 0a";

            cmd=HexStrTobyte(hex);
            comm1.Write(cmd, 0, cmd.Length);
            
        }


        private static string strToToHexByte(string hexString)
        {
           // hexString = hexString.Replace(" ","");
            byte[] buffer = Encoding.ASCII.GetBytes(hexString);
            string result = string.Empty;
            for (int i=0;i<buffer.Length;i++)
            {
                result += Convert.ToString(buffer[i],16);
            }
            return result;
        }

        private byte[] HexStrTobyte(string hexString)
        {
            hexString = hexString.Replace(" ", string.Empty);
            if ((hexString.Length % 2) != 0)
                hexString += " ";
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2).Trim(), 16);
            return returnBytes;
        }


        private void button8_Click(object sender, EventArgs e)
        {
            this.richTextBox2.Clear();
        }



  


        private void Form1_Resize(object sender, EventArgs e)
        {
            SetSize();
        }
        private void SetSize()

        {

            //  zedGraphControl1.Location = new Point(10, 10);

            //保留一个小的页面空白在控件的周围

            zedGraphControl1.Size = new Size(ClientRectangle.Width - 50, ClientRectangle.Height - 500);

        }


        private void button9_Click(object sender, EventArgs e)
        {
            if (isDrawLine)
            {
                isDrawLine = false;
                this.button9.Text = "继续绘制";
            }
            else
            {
                isDrawLine = true;
                this.button9.Text = "停止绘制";
            }
        }
      

        private void button3_Click(object sender, EventArgs e)
        {
            createLeftLD();
        }

        //设置左雷达命令
        private async void createLeftLD()
        {
            await comm_DataSendWithDelay("sensorStop");
            await comm_DataSendWithDelay("flushCfg");
            await comm_DataSendWithDelay("dfeDataOutputMode 1");
            await comm_DataSendWithDelay("channelCfg 15 1 0");
            await comm_DataSendWithDelay("adcCfg 2 1");
            await comm_DataSendWithDelay("adcbufCfg -1 0 1 1 1");
            await comm_DataSendWithDelay("profileCfg  0 60 17 7 13.12 0 0 50 1 64 12499 0 0 158");
            await comm_DataSendWithDelay("chirpCfg 0 0 0 0 0 0 0 1");
            await comm_DataSendWithDelay("frameCfg 0 0 128 0 33.333 1 0");
            await comm_DataSendWithDelay("lowPower 0 0");
            await comm_DataSendWithDelay("guiMonitor -1 1 0 0 0 0 " + this.textBox1.Text);
            await comm_DataSendWithDelay("multiObjBeamForming -1 1 0.5");
            await comm_DataSendWithDelay("clutterRemoval -1 1");
            await comm_DataSendWithDelay("calibDcRangeSig -1 0 -5 8 256");
            await comm_DataSendWithDelay("extendedMaxVelocity -1 1");
            await comm_DataSendWithDelay("bpmCfg -1 0 0 1");
            await comm_DataSendWithDelay("lvdsStreamCfg -1 0 0 0");
            await comm_DataSendWithDelay("compRangeBiasAndRxChanPhase  0.0 1 0 1 0 1 0 1 0 1 0 1 0 1 0 1 0 1 0 1 0 1 0 1 0");
            await comm_DataSendWithDelay("measureRangeBiasAndRxChanPhase 0 1.5 0.2");
            await comm_DataSendWithDelay("CQRxSatMonitor 0 3 4 63 0");
            await comm_DataSendWithDelay("CQSigImgMonitor 0 127 4");
            await comm_DataSendWithDelay("analogMonitor 0 0");
            await comm_DataSendWithDelay("cfarFovCfg -1 0 0 59.99");
            await comm_DataSendWithDelay("cfarFovCfg -1 1 -50 50.00");
            await comm_DataSendWithDelay("cfarCfg -1 0 2 8 4 3 0 15 0");
            await comm_DataSendWithDelay("cfarCfg -1 1 0 4 2 3 1 15 0");
            /**
           await comm_DataSendWithDelay("aoaFovCfg -1 -90 -1 0 45");
           await comm_DataSendWithDelay("boundaryBox -5 -1 3 25 0 3 ");
             **/
            if (this.radioButton1.Checked)//左雷达
            {
                await comm_DataSendWithDelay("aoaFovCfg -1 -90 -1 0 45");
                await comm_DataSendWithDelay("boundaryBox " + xmin + " " + xmax + " " + ymin + " " + ymax + " 0 3");
            }
            else
            {
                await comm_DataSendWithDelay("aoaFovCfg -1 1 90 0 45");
                await comm_DataSendWithDelay("boundaryBox " + xmax + " " + xmin + " " + ymin + " " + ymax + " 0 3");
            }
        

            await comm_DataSendWithDelay("presenceBoundaryBox 0.5 5 2 15 0 3");
            await comm_DataSendWithDelay("staticBoundaryBox 0 0 0 0 0 0");
            await comm_DataSendWithDelay("sensorPosition 1 0 0");
            await comm_DataSendWithDelay("allocationParam 80 60 0.01 9 9 3");
            await comm_DataSendWithDelay("gatingParam 16 5 25 5 50");
            await comm_DataSendWithDelay("stateParam 1 5 20 30 10 6000");
            await comm_DataSendWithDelay("maxAcceleration 1 1 1");
            await comm_DataSendWithDelay("trackingCfg 1 2 50 1 500 640 33.333");
            await comm_DataSendWithDelay("sensorStart");

            MessageBox.Show("完成雷达参数设置");
        }
        private async Task comm_DataSendWithDelay(string command)
        {
            comm_DataSend(command);
            await Task.Delay(100); // 延迟1秒
        }

        private void btn_sure_Click(object sender, EventArgs e)
        {
            double value1;
            if (!double.TryParse(textBox_value.Text.Trim(), out value1))
            {
                MessageBox.Show("距离设置错误！", "错误");
                return;
            }
            else if (value1 > m_maxvalue || value1 < 0)
            {
                MessageBox.Show($"距离范围:{0}到{m_maxvalue}", "错误");
                return;
            }
            chart1.Series[1].Points.Clear();
            chart1.Series[2].Points.Clear();
            chart1.Series[3].Points.Clear();
            double value2;
            if (radioButton2.Checked)
            {
                value2 = value1 + m_dis;
            }
            else
            {
                value1 *= -1;
                value2 = value1 - m_dis;
            }
            for (int i = 0; i <= 20; i++)
            {
                chart1.Series[1].Points.AddXY(value1, i);

                chart1.Series[2].Points.AddXY(value2, i);
            }
            double pos_x, pos_y;
            //点1
            pos_x = value1;
            pos_y = chart1.ChartAreas[0].AxisY.Maximum;
            chart1.Series[3].Points.AddXY(pos_x, pos_y);
            chart1.Series[3].Points[0].Label = $"P1({pos_x},{pos_y})";
            chart1.Series[3].Points[0].LabelForeColor = Color.Purple;
            this.xmax = pos_x ;
            this.ymax = pos_y ;


            //点2
            pos_x = value1;
            pos_y = Math.Abs(value1);
            chart1.Series[3].Points.AddXY(pos_x, pos_y);
            chart1.Series[3].Points[1].Label = $"P2({pos_x},{pos_y})";
            chart1.Series[3].Points[1].LabelForeColor = Color.Purple;
            //点3
            pos_x = value2;
            pos_y = chart1.ChartAreas[0].AxisY.Maximum;
            chart1.Series[3].Points.AddXY(pos_x, pos_y);
            chart1.Series[3].Points[2].Label = $"P3({pos_x},{pos_y})";
            chart1.Series[3].Points[2].LabelForeColor = Color.Purple;
            //点4
            pos_x = value2;
            pos_y = Math.Abs(value2);
            chart1.Series[3].Points.AddXY(pos_x, pos_y);
            chart1.Series[3].Points[3].Label = $"P4({pos_x},{pos_y})";
            chart1.Series[3].Points[3].LabelForeColor = Color.Purple;
            this.xmin = pos_x ; 
            this.ymin = pos_y ;
        }
    }
}
