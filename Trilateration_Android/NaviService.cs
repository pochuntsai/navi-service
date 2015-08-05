#define G25_PIC32
using System;
using Android.App;
using Android.OS;
using Android.Content;
using Android.Util;
using Android.Graphics;
using Java.Interop;
using Idv.Android.Hellouart;
using System.Threading;
using System.Collections.Generic;
using Trilateration;
using System.IO;


namespace Trilateration_Android
{
    [Service]
    [IntentFilter(new String[] { "com.XYZRobot.NaviService" })]
    public class NaviMessengerService : Service
    {
        Messenger naviMessenger, clientMessager;
        private int i;
        private int pic32_open = 0, tag_open = 0;
        private int target_total, target_now;
        private struct_PointF[] target = new struct_PointF[100];
        private int tagbufflen;
        private byte[] tagbuff = new byte[30];
        private string Tag = "Brian";
        System.Timers.Timer timer1, Pic32DataRecvTimer, TagDataRecvTimer, SendCoordinateTimer, AutoModeTimer, ManModeTimer;
        private beacon myTag = new beacon();
        private List<class_Anchors> anchor = new List<class_Anchors>();
        private string tagdata = "";
        private class_flag myFlag = new class_flag();
        private Single[] myTag_Old_X = new Single[5];
        private Single[] myTag_Old_Y = new Single[5];
        private System.Drawing.Point mapsize;
        private class_EKFL5 myEKF;
        HighPerformanceCounter hpcounter4 = new HighPerformanceCounter();
        private class_Vehicle myVehicle = new class_Vehicle();
        public static System.IO.StreamWriter wr;
        private int MainWidth, MainHeight;
        private Single screen2grid_x, screen2grid_y;
        private Single screen2cm_x, screen2cm_y;
        private Single pad2cm_x, pad2cm_y;
        Thread ThreadAlgorithm;
        private struct_config cfg = new struct_config();
        private Map myMap = new Map();
        private bool bBeaconFind;

        private short ManualCount;
        private string ManualCommand = "";
        DateTime dtScheduleTime;
        int iScheduleTime;
        Point PadTarget = new Point();
        //private struct_PointF[] AutoTarget = new struct_PointF[100];
        int NaviMode = 0, TargetNotWalkable = 0;
        int AutoTargetAmount = 0, AutoTargetNow = 0;
        struct_AutoTarget[] AutoTarget = new struct_AutoTarget[10];
        string path = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
        
        public NaviMessengerService()
        {
            naviMessenger = new Messenger(new NaviHandler(this));
            InitEnvParameters();
            ConnectPIC32();

            //Create navigation algorithms thread 
            ThreadAlgorithm = new Thread(new ThreadStart(algorithms));
            ThreadAlgorithm.IsBackground = true;
            ThreadAlgorithm.Priority = System.Threading.ThreadPriority.AboveNormal;

            //Init timer1
            timer1 = new System.Timers.Timer();
            timer1.Interval = 200;
            timer1.Elapsed += new System.Timers.ElapsedEventHandler(timer1_Tick);
            timer1.Stop();
            //Init manual mode timer
            ManModeTimer = new System.Timers.Timer();
            ManModeTimer.Interval = 100;
            ManModeTimer.Elapsed += new System.Timers.ElapsedEventHandler(ManModeTimerHandler);
            ManModeTimer.Stop();
            //Read config from default.set and process map
            ProcessConfigMap();
            //Connect beacon and start sendcoordinatetimer and TagDataRecvTimer
            ConnectBeacon();
        }

        private void ProcessConfigMap()
        {
            bool success;
            string file1 = path + @"/default.set";
            string file2 = path + @"/Raw";

            success = ReadConfig(file1);
            if (!success)
            {
                Log.Debug(Tag, file1 + " doesn't exist");
            }
            else
            {
                Log.Debug(Tag, "Reading " + file1);
                myMap.Grid_W = cfg.GridWidth;
                myMap.Grid_H = cfg.GridHeight;
                myMap.East = cfg.MapEast;
                myVehicle.East = cfg.MapEast;

                Log.Debug(Tag, "Loading image file " + file2 + ".bmp");
                success = myMap.LoadFile(file2);
                if (!success)
                {
                    Log.Debug(Tag, file2 + " doesn't exist");
                }
                else
                {
                    Log.Debug(Tag, "Processing bmp data");
                    success = myMap.Preprocess();
                    if (!success)
                    {
                        Log.Debug(Tag, "Initial fail");
                    }
                    else
                    {
                        Log.Debug(Tag, "Initial successfully");
                        //Bitmap bitmap = BitmapFactory.DecodeFile(file2 + ".bmp");
                        //Drawable drawable = new BitmapDrawable(bitmap);
                        //linearContent.SetBackgroundDrawable(drawable);

                        //labelTable.Visibility = ViewStates.Visible;
                        //btnConnect.Enabled = true;

                    }

                }
            }
        }

        private bool ReadConfig(string ff)
        {
            if (File.Exists(ff))
            {
                StreamReader sr = new StreamReader(ff);
                int tmpInt, count1 = 0;
                string line;
                string[] linesplit;
                //var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem);
                //adapter.Add("Favorite");
                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    linesplit = line.Split('=');

                    if (linesplit[0] == "mapwidth") cfg.MapWidth = Convert.ToUInt16(linesplit[1]);
                    else if (linesplit[0] == "mapheight") cfg.MapHeight = Convert.ToUInt16(linesplit[1]);
                    else if (linesplit[0] == "mapeast") cfg.MapEast = Convert.ToInt16(linesplit[1]);
                    else if (linesplit[0] == "gridwidth") cfg.GridWidth = Convert.ToUInt16(linesplit[1]);
                    else if (linesplit[0] == "gridheight") cfg.GridHeight = Convert.ToUInt16(linesplit[1]);
                    else if (linesplit[0] == "agent_max_speed") cfg.MaxSpeed = Convert.ToUInt16(linesplit[1]);
                    else if (linesplit[0] == "anchor")
                    {
                        class_Anchors tmpanchor = new class_Anchors(Convert.ToInt32(linesplit[1]), Convert.ToSingle(linesplit[2]), Convert.ToSingle(linesplit[3]), Convert.ToSingle(linesplit[4]));
                        anchor.Add(tmpanchor);
                    }
                    else if (linesplit[0] == "favorite")
                    {
                        //favorite[count1].Note = linesplit[1];
                        //favorite[count1].X = Convert.ToInt16(linesplit[2]);
                        //favorite[count1].Y = Convert.ToInt16(linesplit[3]);
                        //adapter.Add(favorite[count1].Note);
                        count1++;
                    }

                }
                sr.Close();
                //spinnerTarget.Adapter = adapter;
                Log.Debug("Brian", "anchor num=" + anchor.Count.ToString());
                return true;
            }
            else 
                return false;
        }
        public void ConnectBeacon()
        {
            //tag_open = Uart2C.OpenUart("ttymxc2");
            tag_open = Uart2C.OpenUart("ttymxc1"); //For G2.5
            if (tag_open > 0)
            {
                Uart2C.SetUart(1); //0: B9600, 1:B115200, 2:B19200                    
                Log.Debug(Tag, "Open ttymxc1 successfully, Baund Rate=115200, fd_num=" + tag_open);

                SetAppearance();

                TagDataRecvTimer = new System.Timers.Timer();
                TagDataRecvTimer.Interval = 100;
                TagDataRecvTimer.Elapsed += new System.Timers.ElapsedEventHandler(tag_DataReceived);
                TagDataRecvTimer.Start();

                hpcounter4.Start();
                
                SendCoordinateTimer = new System.Timers.Timer();
                SendCoordinateTimer.Interval = 1000;
                SendCoordinateTimer.Elapsed += new System.Timers.ElapsedEventHandler(SendCoordinateHandler);
                SendCoordinateTimer.Stop();
                //SendCoordinateTimer.Start();
                
                Thread.Sleep(500);
                ThreadAlgorithm.Start();
                timer1.Start();
            }
            else
            {
                Log.Debug(Tag, "Open ttymxc1 fail!!\r\n");
            }

            //Brian+: TODO: check this function
            SetAppearance();   
        }

        public void ConnectPIC32()
        {
            //pic32_open = Uart2C.OpenUart("ttymxc3");
            pic32_open = Uart2C.OpenUart("ttymxc2"); //For G2.5
            
            if (pic32_open > 0)
            {
                Uart2C.SetUart(2); //0: B9600, 1:B115200, 2:B19200                    
                Log.Debug(Tag, "Open ttymxc2 successfully, Baund Rate=19200, fd_num=" + pic32_open);

                Pic32DataRecvTimer = new System.Timers.Timer();
                Pic32DataRecvTimer.Interval = 100;
                Pic32DataRecvTimer.Elapsed += new System.Timers.ElapsedEventHandler(driving_DataReceived);
                Pic32DataRecvTimer.Start();
                Log.Debug("Brian", "PIC32 opened");
                TowardTargets.Start();
                TowardTargets.ControlEvent += ControlOut;
            }
            else
            {
                Log.Debug(Tag, "Open ttymx2 fail!!\r\n");
            }
            
        }
        
        /// <summary>
        /// Give any 3 valid anchors then return the x, y position 
        /// </summary>
        private bool loc_trilateration(class_Anchors anchorA, class_Anchors anchorB, class_Anchors anchorC)
        {
            Single x, y, x1, y1, x2, y2, x3, y3;
            Single ca, cb, cc, cd, ce, cf, cg, ch, ci;
            bool v1, v2, v3;
            short count;

            // calculate trilateration
            ca = anchorB.X - anchorA.X;
            cb = anchorB.Y - anchorA.Y;
            cc = (Single)(Math.Pow(anchorA.Range, 2) - Math.Pow(anchorB.Range, 2) - Math.Pow(anchorA.X, 2) + Math.Pow(anchorB.X, 2) - Math.Pow(anchorA.Y, 2) + Math.Pow(anchorB.Y, 2));
            cd = anchorB.X - anchorC.X;
            ce = anchorB.Y - anchorC.Y;
            cf = (Single)(Math.Pow(anchorC.Range, 2) - Math.Pow(anchorB.Range, 2) - Math.Pow(anchorC.X, 2) + Math.Pow(anchorB.X, 2) - Math.Pow(anchorC.Y, 2) + Math.Pow(anchorB.Y, 2));
            cg = anchorA.X - anchorC.X;
            ch = anchorA.Y - anchorC.Y;
            ci = (Single)(Math.Pow(anchorC.Range, 2) - Math.Pow(anchorA.Range, 2) - Math.Pow(anchorC.X, 2) + Math.Pow(anchorA.X, 2) - Math.Pow(anchorC.Y, 2) + Math.Pow(anchorA.Y, 2));
            if (cd * cb == ca * ce)
            {
                x1 = 0;
                y1 = 0;
                v1 = false;
            }
            else
            {
                y1 = 0.5f * (cd * cc - ca * cf) / (cd * cb - ca * ce);
                if (ca == 0) x1 = (cf - 2 * ce * y1) / (2 * cd);
                else x1 = (cc - 2 * cb * y1) / (2 * ca);
                v1 = true;
            }
            if (cg * cb == ca * ch)
            {
                x2 = 0;
                y2 = 0;
                v2 = false;
            }
            else
            {
                y2 = 0.5f * (cg * cc - ca * ci) / (cg * cb - ca * ch);
                if (ca == 0) x2 = (ci - 2 * ch * y2) / (2 * cg);
                else x2 = (cc - 2 * cb * y2) / (2 * ca);
                v2 = true;
            }
            if (cg * ce == cd * ch)
            {
                x3 = 0;
                y3 = 0;
                v3 = false;
            }
            else
            {
                y3 = 0.5f * (cg * cf - cd * ci) / (cg * ce - cd * ch);
                if (cd == 0) x3 = (ci - 2 * ch * y3) / (2 * cg);
                else x3 = (cf - 2 * ce * y3) / (2 * cd);
                v3 = true;
            }

            x = 0;
            y = 0;
            count = 0;
            if (v1)
            {
                x = x + x1;
                y = y + y1;
                count++;
            }
            if (v2)
            {
                x = x + x2;
                y = y + y2;
                count++;
            }
            if (v3)
            {
                x = x + x3;
                y = y + y3;
                count++;
            }
            if (count > 0)
            {
                x = x / count;
                y = y / count;
            }
            else return false;

            myTag.X = x;
            myTag.Y = y;
            myTag.Avg.X = x;
            myTag.Avg.Y = y;
            for (count = 0; count < myTag_Old_X.Length; count++)
            {
                myTag_Old_X[count] = x;
                myTag_Old_Y[count] = y;
            }
            return true;
        }

        private bool loc_initial()
        {
            class_Anchors[] candidate3 = new class_Anchors[3];
            Single[] tmpSingleA = new Single[6];
            int anchor_count = 0;

            for (i = 0; i < 3; i++)
            {
                candidate3[i] = anchor[i];
            }
            for (i = 0; i < anchor.Count; i++)
            {
                if (anchor[i].Reliability >= candidate3[0].Reliability)
                {
                    candidate3[2] = candidate3[1];
                    candidate3[1] = candidate3[0];
                    candidate3[0] = anchor[i];
                    anchor_count++;
                    continue;
                }
                else if (anchor[i].Reliability >= candidate3[1].Reliability)
                {
                    candidate3[2] = candidate3[1];
                    candidate3[1] = anchor[i];
                    anchor_count++;
                    continue;
                }
                else if (anchor[i].Reliability >= candidate3[2].Reliability)
                {
                    candidate3[2] = anchor[i];
                    anchor_count++;
                    continue;
                }
            }

            //Log.Debug("Brian", "count= " + anchor_count.ToString());
            if (anchor_count < 3)
            {
                Log.Debug(Tag, "Searching for beacon...\r\n");
                bBeaconFind = false;
                return false;
            }
            else
            {
                if (!loc_trilateration(candidate3[0], candidate3[1], candidate3[2]))
                {
                    return false;
                }
                
                Log.Debug("Brian", "Beacon found!!");
                bBeaconFind = true;

                myEKF = new class_EKFL5(myTag.X, myTag.Y, 0f, anchor.Count);
                for (i = 0; i < anchor.Count; i++)
                {
                    myEKF.Anchor[i] = anchor[i];
                }

                return true;
            }
        }
        
        private void avg_stdev(out Single avg, out Single stdev, Single[] set)
        {
            short len, i;
            Single sum;

            avg = 0;
            stdev = 0;
            sum = 0;

            len = (short)set.Length;
            if (len > 0)
            {
                for (i = 0; i <= len - 1; i++)
                {
                    avg = avg + set[i];
                }
                avg = avg / len;
                for (i = 0; i <= len - 1; i++)
                {
                    sum = sum + (Single)Math.Pow(set[i] - avg, 2);
                }
                stdev = (Single)Math.Sqrt(sum / len);
            }

        }
        private void algorithms()
        {
            Single std_x, std_y;
            Single[] tmpSingleA = new Single[6];
            int anchor_num;
            while (!myFlag.loc_init_done)
            {
                Thread.Sleep(500);
                myFlag.loc_init_done = loc_initial();
            }
            while (true)
            {
                Thread.Sleep(90);
                anchor_num = 0;
                for (i = 0; i < anchor.Count; i++)
                {
                    anchor[i].Chosen = true;
                    anchor_num++;
                }
                if (anchor_num < 2)
                {
                    loc_initial();
                }
                else
                {
                    // Give all infomation EKF needed
                    hpcounter4.Stop();
                    if (anchor_num >= 3) myEKF.DRonly = false;
                    else myEKF.DRonly = true;
                    myEKF.dT = (Single)hpcounter4.Duration;
                    //Log.Debug("Brian", "dT= "+ myEKF.dT.ToString());
                    hpcounter4.Start();
                    myEKF.Velocity = myVehicle.V;
                    myEKF.Omega = myVehicle.W;
                    myEKF.Compass = myVehicle.compass;
                    for (i = 0; i < anchor.Count; i++)
                    {
                        if (anchor[i].Chosen) myEKF.Anchor[i] = anchor[i];
                        else myEKF.Anchor[i].Range = 0;
                    }
                    // Perform calculation
                    myEKF.Calculation();
                    // Retrieve results
                    myTag.X = myEKF.tagX;
                    myTag.Y = myEKF.tagY;
                    //Log.Debug("Brian", "tag= " + myTag.X.ToString() + " " + myTag.Y.ToString());
                    //Log.Debug("Brian", "interval= " + myEKF.dT.ToString("f1"));
                    // Calculate avg & stdev
                    for (int j = myTag_Old_X.Length - 1; j >= 1; j--)
                    {
                        myTag_Old_X[j] = myTag_Old_X[j - 1];
                        myTag_Old_Y[j] = myTag_Old_Y[j - 1];
                    }
                    myTag_Old_X[0] = myTag.X;
                    myTag_Old_Y[0] = myTag.Y;
                    avg_stdev(out myTag.Avg.X, out std_x, myTag_Old_X);
                    avg_stdev(out myTag.Avg.Y, out std_y, myTag_Old_Y);

                    // Feed results to TowardTargets
                    TowardTargets.Pose.X = myTag.Avg.X;
                    TowardTargets.Pose.Y = myTag.Avg.Y;
                    TowardTargets.Pose.Theta = myVehicle.compass;
                    TowardTargets.Vehicle.Bumper = myVehicle.Bumper;
                    for (i = 0; i < 5; i++) TowardTargets.Vehicle.sonic[i] = myVehicle.sonic[i];

                    if (TowardTargets.Status == TowardTargets.e_status.None) myFlag.moving = false;
                    else myFlag.moving = true;
                }
            }
        }
                
        private void InitEnvParameters()
        {
            path = System.IO.Path.Combine(path, "Trilateration_Android");
            Log.Debug("Brian", "[InitEnvParameters] path=" + path);

            target_total = 0;
            target_now = 1;
            myFlag.screen_ready = false;
            myFlag.loc_init_done = false;

            // set initial value
            myTag.X = 200;
            myTag.Avg.X = 200;
            myTag.Y = 200;
            myTag.Avg.Y = 200;
            mapsize.X = 1000;
            mapsize.Y = 800;
            //offset = 10;

            for (int i = 0; i <= myTag_Old_X.Length - 1; i++)
            {
                myTag_Old_X[i] = 0;
                myTag_Old_Y[i] = 0;
            }

            //Create new default.set and raw.bmp anyway
            System.IO.Directory.CreateDirectory(path);
            Log.Debug(Tag, "[InitEnvParameter]222222222222");
            
            //Brian+: We can't use Assets.Open() in service 
            /*
            using (var stream = this.Assets.Open("default.set"))
            {
                Log.Debug(Tag, "[InitEnvParameter]2.11111111111111111");
                //using (FileStream fs = new FileStream(path + @"/default.set", FileMode.CreateNew))
                using (FileStream fs = new FileStream(path + @"/default.set", FileMode.Create))
                {
                    Log.Debug(Tag, "[InitEnvParameter]2.222222222222222222");
                    byte[] byt = new byte[1024];
                    while ((i = stream.Read(byt, 0, byt.Length)) != 0)
                    {
                        Log.Debug(Tag, "[InitEnvParameter]2.3333333333333333");
                        fs.Write(byt, 0, i);
                        Log.Debug(Tag, "[InitEnvParameter]2.4444444444444444");
                    }
                    fs.Flush();
                    Log.Debug(Tag, "[InitEnvParameter]2.55555555555555");
                }
            }
            Log.Debug(Tag, "[InitEnvParameter]3333333");
            //using (var stream = Assets.Open("Raw.bmp"))
            {
                //using (FileStream fs = new FileStream(path + @"/Raw.bmp", FileMode.CreateNew))
                using (FileStream fs = new FileStream(path + @"/Raw.bmp", FileMode.Create))
                {
                    byte[] byt = new byte[1024];
                    while ((i = stream.Read(byt, 0, byt.Length)) != 0)
                    {
                        fs.Write(byt, 0, i);
                    }
                    fs.Flush();
                }
            }
            Log.Debug(Tag, "[InitEnvParameter]4444444444");
            */

            // create log file
            string logName = path + @"/" + string.Format("log{0:s}", DateTime.Now) + ".txt";       // Set the file name
            wr = new System.IO.StreamWriter(logName, true);
            Log.Debug("Patrick", "Log File Name:" + logName);
            wr.Write("==== " + DateTime.Now.ToString("yyyyMMdd,HH:mm:ss ") + " ====\r\n");

        }

        private void ControlOut(object sender, EventArgs e)
        {
            string tmpString;
            byte[] outbyte = new byte[10];
            int outbytelen = 0;
            short speed = TowardTargets.OutSpeed;
            short turn = TowardTargets.OutTurn;

            tmpString = "cc " + TowardTargets.OutStr;
            //Log.Debug("Brian", tmpString);
            if (speed == 0 && turn != 0)
            {
                outbyte[0] = 0x53;
                outbyte[1] = 0x12;
                outbyte[2] = (byte)((turn >> 8) & 0xFF);
                outbyte[3] = (byte)(turn & 0xFF);
                outbyte[4] = 0x00;
                outbyte[5] = 0x45;
                Uart2C.SendMsgUart(pic32_open, outbyte);
            }
            else
            {
                if (speed > 100) speed = 100;
                else if (speed < -100) speed = -100;
                if (turn > 100) turn = 100;
                else if (turn < -100) turn = -100;

                outbyte[0] = 0x53;
                outbyte[1] = 0x13;
                outbyte[2] = (byte)(speed & 0xFF);
                outbyte[3] = (byte)(turn & 0xFF);
                outbyte[4] = 0x00;
                outbyte[5] = 0x45;
                Uart2C.SendMsgUart(pic32_open, outbyte);
            }
        }

        private void ManModeTimerHandler(object sender, EventArgs e)
        {
            byte[] outbyte = new byte[6];

            if (ManualCount > 0)
            {
                ManualCount--;
                outbyte[0] = 0x53;
                outbyte[1] = 0x11;
                outbyte[3] = 0x00;
                outbyte[4] = 0x00;
                outbyte[5] = 0x45;
                if (String.Compare(ManualCommand, "forward", true) == 0)
                    outbyte[2] = 0x01;
                else if (String.Compare(ManualCommand, "backward", true) == 0)
                    outbyte[2] = 0x02;
                else if (String.Compare(ManualCommand, "left", true) == 0)
                    outbyte[2] = 0x03;
                else if (String.Compare(ManualCommand, "right", true) == 0)
                    outbyte[2] = 0x04;
                else if (String.Compare(ManualCommand, "forRig", true) == 0)
                    outbyte[2] = 0x07;
                else if (String.Compare(ManualCommand, "bacRig", true) == 0)
                    outbyte[2] = 0x08;
                else if (String.Compare(ManualCommand, "forLeft", true) == 0)
                    outbyte[2] = 0x05;
                else if (String.Compare(ManualCommand, "bacLeft", true) == 0)
                    outbyte[2] = 0x06;

                Log.Debug("Brian", "outbyte[]=" + ToHexString(outbyte));
                Uart2C.SendMsgUart(pic32_open, outbyte);
            }
        }

        private void SendCoordinateHandler(object sender, EventArgs e)
        {
            int tag_x = 0, tag_y = 0, compass = 0;

            //Brian+: Maybe need to check whether becaon is found or not
            if (bBeaconFind)
            {
                tag_x = Convert.ToInt32(myTag.Avg.X / pad2cm_x);
                tag_y = Convert.ToInt32(myTag.Avg.Y / pad2cm_y);
                compass = Convert.ToInt32(myVehicle.compass);
                //Log.Debug("Brian", "[NaviService]tag_x=" + tag_x + ", tag_y=" + tag_y + ", compass=" + compass);
                if (clientMessager != null)
                {
                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("X", tag_x.ToString());
                    b.PutString("Y", tag_y.ToString());
                    b.PutString("Compass", compass.ToString());
                    message.What = 0;
                    message.Data = b;
                    clientMessager.Send(message);
                }
                else
                    Log.Debug("Brian", "[NaviService]clientMessager == NULL!!!!");
            }
       }

        private void timer1_Tick(object sender, EventArgs e)
        {
            string tmpString = "";

            for (i = 0; i < anchor.Count; i++)
            {
                if (anchor[i].Reliability >= 0) tmpString = tmpString + anchor[i].Range.ToString("f1") + " cm | ";
                else tmpString = tmpString + "Offline |";
            }
                        
            if (TowardTargets.Status != TowardTargets.e_status.None)
            {
                wr.Write("{0:mm:ss} ", DateTime.Now);
                wr.Write(TowardTargets.Status.ToString() + ",");
                wr.Write(TowardTargets.OutSpeed.ToString() + ",");
                wr.Write(TowardTargets.OutTurn.ToString() + ",");
                //if (anchor1.RateSum > 0) wr.Write(anchor1.Range.ToString("f1") + ",");
                //else wr.Write("0,");
                //if (anchor2.RateSum > 0) wr.Write(anchor2.Range.ToString("f1") + ",");
                //else wr.Write("0,");
                //if (anchor3.RateSum > 0) wr.Write(anchor3.Range.ToString("f1") + ",");
                //else wr.Write("0,");
                //if (anchor4.RateSum > 0) wr.Write(anchor4.Range.ToString("f1") + ",");
                //else wr.Write("0,");
                //if (anchor5.RateSum > 0) wr.Write(anchor5.Range.ToString("f1") + ",");
                //else wr.Write("0,");
                //if (anchor6.RateSum > 0) wr.Write(anchor6.Range.ToString("f1") + ",");
                //else wr.Write("0,");
                wr.Write(myVehicle.compass.ToString("f1") + ",");
                wr.Write(myTag.Avg.X.ToString("f1") + "," + myTag.Avg.Y.ToString("f1"));
                wr.Write("," + myVehicle.V.ToString() + "," + myVehicle.W.ToString());
                wr.Write("," + myEKF.Outval);
                wr.Write("\r\n");
                wr.Flush();
            }
                        
            if (TowardTargets.Status == TowardTargets.e_status.Finish)
            {
                target_total = 0;
                target_now = 1;
                TowardTargets.Status = TowardTargets.e_status.None;

                //Brian+ 2015/05/07: Send reach target XMPP message to pad
                //string strReachMsg = "";

                if (NaviMode == 2)
                {
                    if (clientMessager != null)
                    {
                        Message message = Message.Obtain();
                        Bundle bun = new Bundle();
                        bun.PutString("End", "1");
                        message.What = 24;
                        message.Data = bun;
                        clientMessager.Send(message);
                    }
                    //strReachMsg = "semiauto end";
                    //xmppSendMsg(strReachMsg);
                }
                else if (NaviMode == 3)
                {

                    if (AutoTargetNow < AutoTargetAmount - 1)
                    {
                        //DateTime dtReachTime = DateTime.Now;
                        iScheduleTime = DateTime.Now.Hour * 60 + DateTime.Now.Minute + AutoTarget[AutoTargetNow].StopTime;
                        if (iScheduleTime >= 1440) //24 hour x 60 min
                            iScheduleTime = iScheduleTime - 1440;

                        AutoTargetNow++;
                        //Log.Debug("Brian", "Reach number " + AutoTargetNow + "target, prepare to next one!!");
                        wr.WriteLine("[AutoMode]Reach target number " + AutoTargetNow + ", prepare to next target at " + iScheduleTime / 60 + ":" +
                                 iScheduleTime % 60);
                        wr.Flush();
                    }
                    else
                    {
                        AutoTargetNow = 0;
                        AutoTargetAmount = 0;
                        AutoModeTimer.Close();
                        //strReachMsg = "auto end";
                        //xmppSendMsg(strReachMsg);
                        //Log.Debug("Brian", "Reach number " + AutoTargetNow + "target, prepare to next one!!");
                        if (clientMessager != null)
                        {
                            Message message = Message.Obtain();
                            Bundle bun = new Bundle();
                            bun.PutString("End", "1");
                            message.What = 33;
                            message.Data = bun;
                            clientMessager.Send(message);
                        }
                        wr.WriteLine("Reach the last target, Stop auto navigation!!");
                        wr.Flush();
                        //wr.Close();
                    }

                }

            }

            if (TowardTargets.Status == TowardTargets.e_status.None) myFlag.moving = false;
            else myFlag.moving = true;
                        
        }

        private void SetAppearance()
        {
            //MainWidth = this.linearContent.Width;
            //MainHeight = this.linearContent.Height;
            //Brian+: Need to know original values of linearContent.Width and linearContent.Height
            MainWidth = 666;
            MainHeight = 382;

            screen2cm_x = (Single)cfg.MapWidth / (Single)MainWidth;
            screen2cm_y = (Single)cfg.MapHeight / (Single)MainHeight;
            screen2grid_x = (Single)cfg.GridWidth / (Single)MainWidth;
            screen2grid_y = (Single)cfg.GridHeight / (Single)MainHeight;
            pad2cm_x = (Single)cfg.MapWidth / 640;
            pad2cm_y = (Single)cfg.MapHeight / 359;

            Log.Debug("Brian", "ScreenWidth=" + MainWidth.ToString());
            Log.Debug("Brian", "ScreenHeight=" + MainHeight.ToString());

            //labelVehicle.SetX(offset);
            //labelVehicle.SetY(MainHeight - 50);
        }
        
        public void AutoCheckWalkable(int TargetX, int TargetY, int Mode)
        {
            int walkable;

            walkable = myMap.CheckWalk((int)(TargetX * screen2grid_x), (int)(TargetY * screen2grid_y));
            if (walkable != 0)
            {
                //Can not walk, send message to pad
                if (clientMessager != null)
                {
                    Message message = Message.Obtain();
                    Bundle bun = new Bundle();
                    bun.PutString("Walkable", "0");
                    message.What = 31;
                    message.Data = bun;
                    clientMessager.Send(message);
                }
                /*
                if (Mode == 2)
                    strSendMsg = "semiauto walkable 0";
                else if (Mode == 3)
                    strSendMsg = "auto walkable 0";

                xmppSendMsg(strSendMsg);
                 */
                TargetNotWalkable = 1;
            }
            else
            {
                if (clientMessager != null)
                {
                    Message message = Message.Obtain();
                    Bundle bun = new Bundle();
                    bun.PutString("Walkable", "1");
                    message.What = 31;
                    message.Data = bun;
                    clientMessager.Send(message);
                }
                /*
                if (Mode == 2)
                    strSendMsg = "semiauto walkable 1";
                else if (Mode == 3)
                    strSendMsg = "auto walkable 1";

                xmppSendMsg(strSendMsg);
                */
                TargetNotWalkable = 0;
            }

        }
        private void driving_DataReceived(object sender, EventArgs e)
        {
            int readbuff;
            int startindex;
            short tmpShort;
            byte[] Pic32RecvData = new byte[50];
#if G25_PIC32
            const int Pic32DataLen = 19; //Brian+: for new message format, length = 20
#else
            const int Pic32DataLen = 16;
#endif    
            Pic32RecvData = Uart2C.ReceiveMsgUartByte(pic32_open);

            if (Pic32RecvData == null) return;
            //if (Pic32RecvData.Length < 16) return;
            if (Pic32RecvData.Length < Pic32DataLen) return;
            //Log.Debug("Brian", "Pic32RecvData.Length=" + Pic32RecvData.Length);
            //Log.Debug("Brian", "Pic32RecvData(Convert)=" + ToHexString(Pic32RecvData));

            startindex = -1;
            //for (int j = 0; j < Pic32RecvData.Length - 16; j++)
            for (int j = 0; j < Pic32RecvData.Length - Pic32DataLen; j++)
            {
                //if (Pic32RecvData[j] == 0x53 && Pic32RecvData[j + 1] == 0x20 && Pic32RecvData[j + 16] == 0x45)
                if (Pic32RecvData[j] == 0x53 && Pic32RecvData[j + 1] == 0x20 && Pic32RecvData[j + Pic32DataLen] == 0x45)
                {
                    startindex = j + 1;
                    break;
                }
            }
            //Log.Debug("Brian", "startindex=" + startindex);

            if (startindex > 0)
            {
                //if ((startindex < Pic32RecvData.Length - 15) && (Pic32RecvData[startindex + 15] == 0x45))
                if ((startindex < Pic32RecvData.Length - (Pic32DataLen - 1)) && (Pic32RecvData[startindex + (Pic32DataLen - 1)] == 0x45))
                {
                    //byte[] tmpArray = new byte[15];
                    byte[] tmpArray = new byte[Pic32DataLen - 1];
                    //Array.Copy(Pic32RecvData, startindex, tmpArray, 0, 15);
                    Array.Copy(Pic32RecvData, startindex, tmpArray, 0, (Pic32DataLen - 1));
                    //Log.Debug("Brian", "Prepare to updateALL!!");
                    myVehicle.UpdatedAll(tmpArray);
                    //Log.Debug("Brian", "baterry=xxxx");
                    
                }
            }
            //Log.Debug("Brian", "baterry=" + myVehicle.BatteryLevel.ToString());
            
            
            TowardTargets.Vehicle.Bumper = myVehicle.Bumper;
            for (int i = 0; i < 5; i++) TowardTargets.Vehicle.sonic[i] = myVehicle.sonic[i];
            //tmpString = test_count.ToString() +" D=" + myVehicle.compass.ToString() + ", Sonic=" + myVehicle.sonic[0].ToString() + "," + myVehicle.sonic[1].ToString() + "," + myVehicle.sonic[2].ToString() + "," + myVehicle.sonic[3].ToString() + "," + myVehicle.sonic[4].ToString() + "," + myVehicle.sonic[5].ToString() + "," + myVehicle.sonic[6].ToString() + ", B=" + myVehicle.Bumper.ToString();

            //RunOnUiThread(() => labelTable.Text = tmpString);
        }
        
        //Brian+ for converting byte to hex string:
        public static string ToHexString(byte[] bytes) // 0xae00cf => "AE00CF "
        {
            string hexString = string.Empty;
            if (bytes != null)
            {
                System.Text.StringBuilder strB = new System.Text.StringBuilder();

                for (int i = 0; i < bytes.Length; i++)
                {
                    strB.Append(bytes[i].ToString("X2"));
                }
                hexString = strB.ToString();
            }
            return hexString;
        }

        public void GenCornerCoodinate(int TargetX, int TargetY, int Mode)
        {
            short walkable;
            Single diffX;
            Single diffY;
            Single tmpSingle1, tmpSingle2, tmpSingle3;

            Point a = new Point();
            Point b = new Point();

            //Brian+ mark for test
            walkable = myMap.CheckWalk((int)(TargetX * screen2grid_x), (int)(TargetY * screen2grid_y));

            //Ignore distance < 20cm(400/20) between 2 points
            tmpSingle1 = target[target_total].X - TargetX * screen2cm_x;
            tmpSingle2 = target[target_total].Y - TargetY * screen2cm_y;
            tmpSingle3 = tmpSingle1 * tmpSingle1 + tmpSingle2 * tmpSingle2;
            //if (tmpSingle3 < 400) return; //Brian+: temporally marked for testing 

            target_total = 0;
            target_now = 1;
            
            if (walkable != 0)
            {
                //Can not walk, send message to pad
                Log.Debug("Brian", "[NavService]Can not walk!!");
                if ((Mode == 2) && (clientMessager != null))
                {
                    Message message = Message.Obtain();
                    Bundle bun = new Bundle();
                    bun.PutString("Walkable", "0");
                    message.What = 21;
                    message.Data = bun;
                    clientMessager.Send(message);
                    Log.Debug("Brian", "[NavService][Semi]Send can't walk message to client!!");
                }
                else if((Mode == 3) && (clientMessager != null))
                {
                    Message message = Message.Obtain();
                    Bundle bun = new Bundle();
                    bun.PutString("Walkable", "0");
                    message.What = 31;
                    message.Data = bun;
                    clientMessager.Send(message);
                    Log.Debug("Brian", "[NavService][Auto]Send can't walk message to client!!");
                }
                else if ((Mode == 5) && (clientMessager != null))
                {
                    Message message = Message.Obtain();
                    Bundle bun = new Bundle();
                    TargetNotWalkable = 1;
                    bun.PutString("Walkable", "0");
                    message.What = 55;
                    message.Data = bun;
                    clientMessager.Send(message);
                    Log.Debug("Brian", "[NavService][CallCenter]Send can't walk message to client!!");
                }    
           }
           else
           {
               target[0].X = myTag.Avg.X;
               target[0].Y = myTag.Avg.Y;

               if (!myFlag.moving && target_total < 100)
               {
                   a.X = (int)(target[target_total].X / screen2cm_x * screen2grid_x);
                   a.Y = (int)(target[target_total].Y / screen2cm_y * screen2grid_y);
                   b.X = (int)(TargetX * screen2grid_x);
                   b.Y = (int)(TargetY * screen2grid_y);
                   myMap.initial_position(a, b);
                   if (myMap.Autoflag == true) 
                       myMap.action();
                   Log.Debug(Tag, "Add " + myMap.path_Result.Count.ToString() + " targets");

                   //Brian+ TODO: Send message to UI if no path generation?
                   if (myMap.path_Result.Count <= 0) 
                       return;
                   
                   foreach (Point p in myMap.path_Result)
                   {
                       target_total++;
                       target[target_total].X = p.X / screen2grid_x * screen2cm_x;
                       target[target_total].Y = p.Y / screen2grid_y * screen2cm_y;
                       diffX = target[target_total].X - target[target_total - 1].X;
                       diffY = target[target_total].Y - target[target_total - 1].Y;
                       target[target_total].Theta = (Single)(Math.Atan2(diffY, diffX) * 180f / 3.14f);
                       if (target[target_total].Theta < -180) target[target_total].Theta = target[target_total].Theta + 360;
                       else if (target[target_total].Theta > 180) target[target_total].Theta = target[target_total].Theta - 360;
                       Log.Debug(Tag, target[target_total].X.ToString() + ", " + target[target_total].Y.ToString() + ", " + target[target_total].Theta.ToString("f2"));
                   }
                   //Brian+: Send corner coordinate message to UI
                   if ((Mode == 2) && (clientMessager != null))
                   {
                       Message message = Message.Obtain();
                       Bundle bun = new Bundle();
                       bun.PutString("Walkable", "1");
                       bun.PutString("CornerNum", (target_total - 1).ToString());
                       for (int i = 1; i < target_total; i++)
                       {
                           bun.PutString("Corner" + i + "_X", Convert.ToString((int)(target[i].X / pad2cm_x)));
                           bun.PutString("Corner" + i + "_Y", Convert.ToString((int)(target[i].Y / pad2cm_y)));
                       }    
                       message.What = 22;
                       message.Data = bun;
                       clientMessager.Send(message);
                   }
                   else if ((Mode == 5) && (clientMessager != null))
                   {
                       Message message = Message.Obtain();
                       Bundle bun = new Bundle();
                       TargetNotWalkable = 0;
                       bun.PutString("Walkable", "1");
                       message.What = 55;
                       message.Data = bun;
                       clientMessager.Send(message);
                       Log.Debug("Brian", "[NavService][CallCenter]Send can walk message to client!!");
                   }
               }
            }
        }

        private void tag_DataReceived(object sender, EventArgs e)
        {
            int readbuff;
            int tmpInt1, tmpInt2, tmpInt3, tmpId;
            String TagRecvData = null;
            //Single tmpSingle1;
            //bool boolmsg;

            TagRecvData = Uart2C.ReceiveMsgUart(tag_open);

            byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(TagRecvData);

            for (int i = 0; i < byteArray.Length; i++)
            {
                readbuff = byteArray[i];

                if (readbuff == 0x23)
                {
                    Array.Clear(tagbuff, 0, 30);
                    tagbufflen = 0;
                }
                else if (tagbufflen >= 17)
                {
                    tagbufflen = 0;
                    myTag.Rate = (short)(myTag.Rate + 1);

                    tmpInt1 = (tagbuff[1] - 48) * 100 + (tagbuff[2] - 48) * 10 + (tagbuff[3] - 48);
                    tmpInt2 = (tagbuff[5] - 48) * 10 + (tagbuff[6] - 48);
                    tmpInt3 = tmpInt1 * 100 + tmpInt2;
                    if (tmpInt3 == 0) return;
                    if (anchor.Count == 0) return;

                    if (tagbuff[14] >= 49 && tagbuff[14] <= 54)
                    {
                        tmpId = tagbuff[14] - 49;
                        anchor[tmpId].Measurement = tmpInt3;
                        //Log.Debug("Brian", "meas=" + tmpId.ToString() + ", " + anchor[tmpId].Range.ToString());
                    }
                }
                else
                {
                    tagbuff[tagbufflen] = (byte)readbuff;
                    tagbufflen++;
                }
            }

        }

        private void AutoModeTimerHandler(object sender, EventArgs e)
        {
            int iCurrentTime = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            //Log.Debug("Brian", "[XMPP]ScheduleTime=" + sScheduleTime + ", CurrentTime=" + sCurrentTime);
            if (iCurrentTime == iScheduleTime)
            {
                //AutoGenCornerCoodinate(AutoTarget[AutoTargetNow].X, AutoTarget[AutoTargetNow].Y, NaviMode);
                //Brian: move to one function
                GenCornerCoodinate(AutoTarget[AutoTargetNow].X, AutoTarget[AutoTargetNow].Y, NaviMode);
                
                if (!myFlag.moving)
                {
                    myFlag.moving = true;

                    if ((AutoTargetNow == 0) && (clientMessager != null))
                    {    
                        //xmppSendMsg("auto start");
                         Message message = Message.Obtain();
                         Bundle bun = new Bundle();
                         bun.PutString("Start", "1");
                         message.What = 32;
                         message.Data = bun;
                         clientMessager.Send(message);
                    }
                    TowardTargets.NewTask(target, target_total);
                    wr.WriteLine("[AutoMode]Navigation start at" + DateTime.Now.ToString() +
                             " to (" + AutoTarget[AutoTargetNow].X + ", " + AutoTarget[AutoTargetNow].Y + ")");
                    Log.Debug("Brian", "[AutoMode]Navigation start at" + DateTime.Now.ToString() +
                             " to (" + AutoTarget[AutoTargetNow].X + ", " + AutoTarget[AutoTargetNow].Y + ")");
                }
            }
        }

        public override IBinder OnBind(Intent intent)
        {
            Log.Debug("Brian", "client bound to service");

            return naviMessenger.Binder;
        }

        public override Boolean OnUnbind(Intent intent)
        {
            Log.Debug("Brian", "client un-bound to service");

            return base.OnUnbind(intent);
        }


        class NaviHandler : Handler
        {
            NaviMessengerService parent;
            
            public NaviHandler(NaviMessengerService parent)
            {
                this.parent = parent;
            }

            public override void HandleMessage(Message msg)
            {
                parent.clientMessager = msg.ReplyTo;
                Boolean IsNullMsgr = (parent.clientMessager == null) ? true : false;
                Log.Debug(parent.Tag, "[NaviService]Message(what) from client: " + msg.What.ToString()+" Is Msgr Null:"+IsNullMsgr.ToString());
                switch(msg.What)
                {
                    case 0:
                        //Update coordinate info
                        parent.SendCoordinateTimer.Start();
                        Log.Debug(parent.Tag, "[NaviService] Update coordinate timer start!!!");
                        break;
                    case 10:
                        //handle manual-mode message
                        parent.NaviMode = 1;
                        if ((parent.pic32_open > 0) && (!parent.myFlag.moving))
                        {
                            parent.ManualCommand = msg.Data.GetString("Direction");
                            parent.ManualCount = 3;
                            parent.ManModeTimer.Start();
                            Log.Debug(parent.Tag, "[NaviService]ManualCommand from client: " + parent.ManualCommand);

                            //Kill auto mode timer if auto mode has been set
                            if (parent.AutoModeTimer != null)
                                    parent.AutoModeTimer.Stop();
                        }
                        else
                        {
                            parent.ManModeTimer.Stop();
                            Log.Debug("Brian", "[NavService]pic32 open fail or robot is moving!!");
                        }
                        break;
                    case 20:
                        //handle semi-auto set coordinate message
                        string X = msg.Data.GetString("X");
                        string Y = msg.Data.GetString("Y");
                        Log.Debug("Brian", "[NavService]Recv semi-auto X=" + X + ", Y=" + Y);
                        parent.NaviMode = 2;
                        parent.ManModeTimer.Stop();
                        if ((X != null) && (Y != null))
                        {
                            parent.PadTarget.X = (int)(Convert.ToInt16(X) * parent.pad2cm_x / parent.screen2cm_x);
                            parent.PadTarget.Y = (int)(Convert.ToInt16(Y) * parent.pad2cm_y / parent.screen2cm_y);
                            parent.GenCornerCoodinate(parent.PadTarget.X, parent.PadTarget.Y, parent.NaviMode);
                        }
                        break;
                    case 23:    
                        //handle semi-auto go messge
                        //Kill auto mode timer if auto mode has been set
                        if (parent.AutoModeTimer != null)
                            parent.AutoModeTimer.Stop();
                        
                        if (!parent.myFlag.moving)
                        {
                            parent.myFlag.moving = true;
                            Log.Debug("Brian", "[NavService][23] Start semi-auto navigation!!!");
                            wr.Write(DateTime.Now.ToString("HH:mm:ss ") + "New Route");
                            wr.Write("\r\n");
                            TowardTargets.NewTask(parent.target, parent.target_total);
                        }
                        else
                            Log.Debug("Brian", "[NavService][23] myFlag.moving==1, stop semi-auto navigation!!!");
                            
                        break;
                    case 25:
                        //handle semi-auto stop function
                        TowardTargets.Abort();
                        parent.myFlag.moving = false;
                        parent.target_total = 0;
                        parent.target_now = 1;
                        break;
                    case 30:
                        //handle auto mode message
                        parent.NaviMode = 3;
                        parent.ManModeTimer.Stop();
                        parent.AutoTargetAmount = 0;
                        parent.AutoTargetNow = 0;
                        parent.dtScheduleTime = Convert.ToDateTime(msg.Data.GetString("ScheduleTime"));
                        parent.iScheduleTime = parent.dtScheduleTime.Hour * 60 + parent.dtScheduleTime.Minute;
                        Log.Debug("Brian", "[AutoMode]Recevied schedule time=" + msg.Data.GetString("ScheduleTime"));
                        wr.WriteLine("[AutoMode]Receive auto scheduledTime=" + msg.Data.GetString("ScheduleTime"));

                        parent.AutoTargetAmount = Convert.ToInt16(msg.Data.GetString("TargetNum"));
                        for (int i = 0; i < parent.AutoTargetAmount; i++)
                        {
                            string AutoTarget_X = msg.Data.GetString("Target" + (i + 1) + "_X");
                            string AutoTarget_Y = msg.Data.GetString("Target" + (i + 1) + "_Y");
                            string StopTime = msg.Data.GetString("Target" + (i + 1) + "StopTime");
                            
                            parent.AutoTarget[i].X = Convert.ToInt16(AutoTarget_X);
                            parent.AutoTarget[i].Y = Convert.ToInt16(AutoTarget_Y);
                            parent.AutoTarget[i].StopTime = Convert.ToInt16(StopTime);

                            parent.AutoCheckWalkable(parent.AutoTarget[i].X, parent.AutoTarget[i].Y, parent.NaviMode);
                            Log.Debug("Brian", "[AutoMode]Receive Target" + (i + 1) + "_X="
                                        + AutoTarget_X + " ,Target" + (i + 1) + "_Y=" + AutoTarget_Y);
                            wr.WriteLine("[AutoMode]Receive Target" + (i + 1) + "_X="
                                        + AutoTarget_X + " ,Target" + (i + 1) + "_Y=" + AutoTarget_Y);
                        }

                        //Auto mode setup completely
                        if (parent.TargetNotWalkable == 1)
                        {
                            //Brian+ 2015/05/13: clear path and timer if any target is not walkable
                            Array.Clear(parent.AutoTarget, 0, 10);

                            if (parent.AutoModeTimer != null)
                                parent.AutoModeTimer.Stop();

                            parent.TargetNotWalkable = 0;
                            wr.WriteLine("[AutoMode]One target is not walkable, clear path and timer!!!");
                            Log.Debug("Brian", "[AutoMode]One target is not walkable, clear path and timer!!!");

                        }
                        else
                        {
                            //Start auto mode timer
                            parent.AutoModeTimer = new System.Timers.Timer();
                            parent.AutoModeTimer.Interval = 1000 * 60;
                            parent.AutoModeTimer.Elapsed += new System.Timers.ElapsedEventHandler(parent.AutoModeTimerHandler);
                            parent.AutoModeTimer.Start();

                            //xmppSendMsg("auto setUpDone");
                            Log.Debug("Brian", "[AutoMode]auto setup done");
                            wr.WriteLine("[AutoMode]auto setup done!!");
                            wr.Flush();
                        }
                        break;
                    case 34:
                        //Handle auto stop function
                        TowardTargets.Abort();
                        parent.myFlag.moving = false;
                        parent.target_total = 0;
                        parent.target_now = 1;
                        break;
#if G25_PIC32 
                    case 40:
                        //Handle LED display function
                        if (parent.clientMessager != null)
                        {
                            Message message = Message.Obtain();
                            Bundle b = new Bundle();
                            int Battery = Convert.ToInt16(parent.myVehicle.BatteryLevel); 

                            //Log.Debug("Brian", "[40]BatteryLevel=" + parent.myVehicle.BatteryLevel.ToString("X2"));
                            Log.Debug("Brian", "[40]BatteryLevel=" + Battery.ToString());
                            //b.PutString("Battery", parent.myVehicle.BatteryLevel.ToString("X2"));
                            b.PutString("Battery", Battery.ToString());
                            
                            if (parent.myVehicle.Status != 0x02)
                                b.PutString("Charging", "0");
                            else
                                b.PutString("Charging", "1");

                            b.PutByte("State", (sbyte)parent.myVehicle.Error); //TODO: need check 傳佳 
                            message.What = 41;
                            message.Data = b;
                            parent.clientMessager.Send(message);
                        }
                        break;
                    case 50:
                        //Handle call center ask robot status 
                        if (parent.clientMessager != null)
                        {
                            Message message = Message.Obtain();
                            Bundle b = new Bundle();
                            int Battery = Convert.ToInt16(parent.myVehicle.BatteryLevel);

                            b.PutString("Battery", Battery.ToString());
                            if (parent.myVehicle.Status == 0x04)
                                b.PutString("Mode", "3"); //calibration 
                            else if (parent.myVehicle.Status == 0x02)
                                b.PutString("Mode", "2"); //charging
                            else if (parent.NaviMode == 2)
                                b.PutString("Mode", "1"); //semi-auto mode
                            else if (parent.NaviMode == 1)
                                b.PutString("Mode", "0"); //manual mode
                            
                            //b.PutString("Report", parent.myVehicle.Error.ToString());
                            b.PutByte("Report", (sbyte)parent.myVehicle.Error); 
                            b.PutString("Busy", parent.myFlag.moving.ToString());
                            message.What = 51;
                            message.Data = b;
                            parent.clientMessager.Send(message);
                        }
                        break;
                    case 52:
                        //Handle call center ask robot's coordinate and compass
                        if (parent.clientMessager != null)
                        {
                            Message message = Message.Obtain();
                            Bundle b = new Bundle();
                            
                            b.PutString("X", Convert.ToString((int)(parent.myTag.Avg.X / parent.pad2cm_x)));
                            b.PutString("Y", Convert.ToString((int)(parent.myTag.Avg.Y / parent.pad2cm_y)));
                            b.PutString("Rotation", Convert.ToString((int)(parent.myTag.Avg.X / parent.pad2cm_x)));
                            message.What = 53;
                            message.Data = b;
                            parent.clientMessager.Send(message);
                        }
                        break;
                    case 54:    
                        //handle call center set target coordinate message
                        string target_X = msg.Data.GetString("X");
                        string target_Y = msg.Data.GetString("Y");
                        Log.Debug("Brian", "[NavService]Recv semi-auto X=" + target_X + ", Y=" + target_Y);
                        parent.NaviMode = 5; //Call center mode
                        parent.ManModeTimer.Stop();
                       
                        if ((target_X != null) && (target_Y != null))
                        {
                            parent.PadTarget.X = (int)(Convert.ToInt16(target_X) * parent.pad2cm_x / parent.screen2cm_x);
                            parent.PadTarget.Y = (int)(Convert.ToInt16(target_Y) * parent.pad2cm_y / parent.screen2cm_y);
                            parent.GenCornerCoodinate(parent.PadTarget.X, parent.PadTarget.Y, parent.NaviMode);
                        
                            //Start navigation
                            if (parent.AutoModeTimer != null)
                                parent.AutoModeTimer.Stop();

                            if ((!parent.myFlag.moving) && (parent.TargetNotWalkable==0))
                            {
                                parent.myFlag.moving = true;
                                Log.Debug("Brian", "[NavService][54] Start semi-auto navigation!!!");
                                wr.WriteLine(DateTime.Now.ToString("HH:mm:ss ") + "New Route");
                                TowardTargets.NewTask(parent.target, parent.target_total);
                            }
                            else
                                Log.Debug("Brian", "[NavService][54] myFlag.moving==1, stop semi-auto navigation!!!");
                            
                        }
                        break;
                    case 56:
                        //handle callcenter manual-mode message
                        parent.NaviMode = 1;
                        if ((parent.pic32_open > 0) && (!parent.myFlag.moving))
                        {
                            parent.ManualCommand = msg.Data.GetString("Direction");
                            parent.ManualCount = 3;
                            parent.ManModeTimer.Start();
                            Log.Debug(parent.Tag, "[NaviService][56]ManualCommand from call-center: " + parent.ManualCommand);

                            //Kill auto mode timer if auto mode has been set
                            if (parent.AutoModeTimer != null)
                                parent.AutoModeTimer.Stop();
                        }
                        else
                        {
                            parent.ManModeTimer.Stop();
                            Log.Debug("Brian", "[NavService]pic32 open fail or robot is moving!!");
                        }
                        break;


#endif
                    default:
                        break;
                }

                

            }
        }
    }
}