//#define OBSTACLE

using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Android.OS;
using Android.Graphics;
using System.Linq;
using System.IO;
using Trilateration;
using Android.Graphics.Drawables;
using System.Threading;
using Idv.Android.Hellouart;
using Android.Util;
using System.Collections.Generic;

using Matrix.Xmpp.Client;

namespace Trilateration_Android
{
    [Activity(Label = "Trilateration_Android", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        private int i;

        private Single pad2cm_x, pad2cm_y;
        private int ComPortDataReady;
        private int MainWidth, MainHeight;
        private int offset;
        private System.Drawing.Point mapsize;

        private Single screen2grid_x, screen2grid_y;
        private Single screen2cm_x, screen2cm_y;

        private int target_total;
        private int target_now;
        private Single[] err = new Single[5];
        private struct_PointF[] target = new struct_PointF[100];
        private struct_Location[] favorite = new struct_Location[50];
        private List<class_Anchors> anchor=new List<class_Anchors>();

        private beacon myTag = new beacon();
        private Single[] myTag_Old_X = new Single[5];
        private Single[] myTag_Old_Y = new Single[5];

        private int tagbufflen;
        private byte[] tagbuff = new byte[30];

        private string ManualCommand;
        private short ManualCount;

        private struct_config cfg = new struct_config();
        private move_command myCommand = new move_command();
        private class_Vehicle myVehicle = new class_Vehicle();
        private class_flag myFlag = new class_flag();
        private Map myMap = new Map();
        private class_EKFL5 myEKF;

        public static System.IO.StreamWriter wr;

        HighPerformanceCounter hpcounter4 = new HighPerformanceCounter();
		
        DrawView view;
        TextView textView;
        Button btnDelete, btnGo, btnConnect, btnCalCompass;
        FrameLayout linearContent;
        TextView labelXY, labelVehicle, labelTable;
        Spinner spinnerTarget;

        //Brian+: add UART related variables
        int pic32_open = 0;
        int tag_open = 0;
        System.Timers.Timer timer1, Pic32DataRecvTimer, TagDataRecvTimer, SendCoordinateTimer, AutoModeTimer, ManModeTimer;
        private static System.IO.StreamWriter fwr;

        //Brian+ 2015/04/7: Add xmpp variables
        XmppClient xmppClient = new XmppClient();
        string strTargetName = "rdc04@ea-xmppserver";
        string strSrcName = "rdc01@ea-xmppserver";
        string strSrcPass = "rdc01";
        string strSrcDomain = "ea-xmppserver";
        string strSrcHost = "ea-xmppserver.cloudapp.net";
        //string strSrcHost = "207.46.147.45";
        string strSendMsg, strRecvMsg;
        bool bXmppConnection = false;
        bool bBeaconFind = false;
        DateTime dtScheduleTime;
        int iScheduleTime;
        Point PadTarget = new Point();
        //private struct_PointF[] AutoTarget = new struct_PointF[100];
        int NaviMode = 0, TargetNotWalkable = 0;
        int AutoTargetAmount = 0, AutoTargetNow = 0;
        struct_AutoTarget[] AutoTarget = new struct_AutoTarget[10];

        //int iPadTargetCount = 0;

        protected override void OnCreate(Bundle bundle)
        {
            #region set essensial parameters
            string path = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
            path=System.IO.Path.Combine(path, "Trilateration_Android");
            ComPortDataReady = 0;
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
            offset = 10;
            
            for (i = 0; i <= myTag_Old_X.Length - 1; i++)
            {
                myTag_Old_X[i] = 0;
                myTag_Old_Y[i] = 0;
            }
            #endregion

            #region get UI ready
            base.OnCreate(bundle);
            this.RequestWindowFeature(WindowFeatures.NoTitle);
            SetContentView(Resource.Layout.Main);
            btnConnect = this.FindViewById<Button>(Resource.Id.btnConnect);
            btnDelete = this.FindViewById<Button>(Resource.Id.btnDelete);
            btnGo = this.FindViewById<Button>(Resource.Id.btnGo);
            btnCalCompass = this.FindViewById<Button>(Resource.Id.btnCalCompass);
            linearContent = this.FindViewById<FrameLayout>(Resource.Id.linearContent);
            spinnerTarget = this.FindViewById<Spinner>(Resource.Id.spinnerTarget);
            textView = this.FindViewById<TextView>(Resource.Id.textView);
            labelXY = this.FindViewById<TextView>(Resource.Id.labelTag);
            labelVehicle = this.FindViewById<TextView>(Resource.Id.labelVehicle);
            labelTable = this.FindViewById<TextView>(Resource.Id.labelTable);
            //Brian+ for debug
            Toast.MakeText(this, path, ToastLength.Long).Show();
            #endregion

            #region Xmpp settings
            //Brian+ 2015/06/16: Add Matrix license key here
            string lic = @"eJxkkNtSwjAQhl/F4ZbRFjlZZ8kIpUC1IlUR4S60oZPSNG2SmuLTWwTxdLOz
                           u98e/l3waEBSSc5KlqSyV8PRueQbpbEg18kB1RDMBA+LQLkhelJFSDkY3xnw
                           C5wqqnaoAcbJB7uQijMiEEwxI8h5w0mBFRdgfMZgc5bhdPcFKE/PjlLA+GLg
                           MEwTJHFC5M0PZRdhVXRgVfFp0TwLsSJOmVFBhpWHLs1Gx+w0OmD8Q+DKIWEc
                           KVFUs44B7O3v/rbZ3ff/AfBEoxSrQlSHZevFbRBvVHTn5xM1pyowvfpq4OmA
                           6yi9ij17On3n8kWt6l4L274eB/F2VurhQ381kebmcfmWLV1LR/HSmc0XbN11
                           hVk478kgbm+50CIfWOPgfjF9FZz5znMrX227Y95yrKxVDprZ6I4S16K7DaEj
                           FduJrfvh2sqbpWDlsH9ZCutFt11/3TB7YHzrBuP4bvQhgAA="; 
            
            Matrix.License.LicenseManager.SetLicense(lic);

            //Brian+ 2015/04/17: Add connect to xmpp server code here
            xmppClient.Username = strSrcName;
            xmppClient.Password = strSrcPass;
            xmppClient.XmppDomain = strSrcDomain;
            xmppClient.Hostname = strSrcHost;
            xmppClient.ResolveSrvRecords = false;

            xmppClient.OnClose += (sender, e) => { Log.Debug("Brian", "XMPP On Close!!"); };
            xmppClient.OnTls += (sender, e) => { Log.Debug("Brian", "XMPP On TLS!!"); };
            xmppClient.OnAuthError += (sender, e) => { Log.Debug("Brian", "XMPP Auth Error!!"); };
            xmppClient.OnBeforeSasl += (sender, e) => { Log.Debug("Brian", "XMPP Before Sasl!!"); };
            xmppClient.OnRosterStart += (sender, e) => { Log.Debug("Brian", "XMPP OnRosterStart!!"); };
            xmppClient.OnRosterEnd += (sender, e) => { Log.Debug("Brian", "XMPP OnRosterEnd!!"); };
            xmppClient.OnRosterItem += (sender, e) => { Log.Debug("Brian", "OnRosterItem => jid:" + e.RosterItem.Jid.Bare); };

            xmppClient.OnIq += (sender, e) => { Log.Debug("Brian", "OnIq => " + e.Iq.From); };
            xmppClient.OnPresence += (sender, e) =>
            {
                Log.Debug("Brian", "OnPresence => from:" + e.Presence.From + " | " + e.Presence.Show + " | " + e.Presence.Status);
                bXmppConnection = true;
            };

            xmppClient.OnError += (sender, e) =>
            {
                Log.Debug("Brian", "XMPP ERROR:" + e.Exception.Message);
            };

            xmppClient.OnReceiveXml += (sender, e) =>
            {
                Log.Debug("Brian", "RECV=" + e.Text);
            };

            xmppClient.OnSendXml += (sender, e) =>
            {
                Log.Debug("Brian", "SEND=" + e.Text);
            };

            //Brian+ 2015/04/17: Add handler to determine the xmpp connection state
            xmppClient.OnBind += (sender, e) =>
            {
                //textView.Append("XMPP Binding Success!!\r\n");
                Log.Debug("Brian", "XMPP Binding Success!!");
            };

            xmppClient.OnLogin += (sender, e) =>
            {
                //textView.Append(" XMPP Login Success!!\r\n");
                Log.Debug("Brian", "XMPP Login Success!!");
            };
            #endregion

            labelVehicle.Visibility = ViewStates.Visible;

            xmppClient.Open();

            #region establish communication with PIC32
            pic32_open = Uart2C.OpenUart("ttymxc3");
            if (pic32_open > 0)
            {
                Uart2C.SetUart(2); //0: B9600, 1:B115200, 2:B19200                    
                textView.Append("Open ttymxc3 successfully, Baund Rate=19200, fd_num=" + pic32_open + "\r\n");

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
                textView.Append("Open ttymx3 fail!!\r\n");
            }
            #endregion

            #region <Event> receive msg from xmpp
            xmppClient.OnMessage += (sender, e) =>
            {
                string sBody = "";

                sBody = e.Message.Body;
                Log.Debug("Brian", "[XMPP]OnMessage => from:" + e.Message.From);
                Log.Debug("Brian", "[XMPP]Body=" + e.Message.Body);

                string[] sArray = sBody.Split(' ');
                for (i = 0; i < sArray.Length; i++)
                {
                    Log.Debug("Brian", "[XMPP]Spilt str[" + i + "]=" + sArray[i]);
                }
                               
                if (String.Compare(sArray[0], "semiauto", true) == 0)
                {
                    //semiauto mode
                    NaviMode = 2;
                    ManModeTimer.Stop();
                    if (String.Compare(sArray[1], "coordinate", true) == 0)
                    {

                        PadTarget.X = (int)(Convert.ToInt16(sArray[2]) * pad2cm_x/screen2cm_x);
                        PadTarget.Y = (int)(Convert.ToInt16(sArray[3])*pad2cm_y/screen2cm_y);
                        //GenCornerCoodinateTest(PadTarget.X, PadTarget.Y);
                        GenCornerCoodinate(PadTarget.X, PadTarget.Y, NaviMode);
                        //AutoTarget = GenCornerCoodinate(PadTarget.X, PadTarget.Y);

                        //Add send corner coordinate to pad code here
                        string sCornerX, sCornerY;
                        if (target_total >= 1)
                        {
                            //Send prepare to send coordinate message 
                            xmppSendMsg("semiauto corner start");
                                                        //Brian+ 2015/04/29: Only send corner coordinate to pad
                            for (int i = 1; i < target_total; i++)
                            {
                                sCornerX = Convert.ToString((int)(target[i].X / pad2cm_x));
                                sCornerY = Convert.ToString((int)(target[i].Y / pad2cm_y));
                                strSendMsg = "semiauto corner " + sCornerX + " " + sCornerY;

                                xmppSendMsg(strSendMsg);
                                
                            }

                            //Send end to send coordinate message 
                            xmppSendMsg("semiauto corner end");
                            
                        }

                        //Kill auto mode timer if auto mode has been set
                        if (AutoModeTimer != null)
                            AutoModeTimer.Stop();
                    }
                    else if (String.Compare(sArray[1], "start", true) == 0)
                    {
                        //Log.Debug("Brian", "myFlag.moving=" + myFlag.moving);
                        if (!myFlag.moving)
                        {

                            myFlag.moving = true;
                            wr.Write(DateTime.Now.ToString("HH:mm:ss ") + "New Route");
                            wr.Write("\r\n");
                            TowardTargets.NewTask(target, target_total);
                        }
                    }
                    else if (String.Compare(sArray[1], "stop", true) == 0)
                    {
                        TowardTargets.Abort();
                        myFlag.moving = false;
                        target_total = 0;
                        target_now = 1;
                        btnDelete.Enabled = false;
                        btnGo.Enabled = false;
                        spinnerTarget.SetSelection(0);
                        RunOnUiThread(() => view.Invalidate());
                    }
                }
                else if (String.Compare(sArray[0], "direction", true) == 0)
                {

                    //Manual mode
                    if ((pic32_open > 0) && (!myFlag.moving))
                    //if (pic32_open > 0)
                    {
                        ManualCommand = sArray[1];
                        ManualCount = 3;
                        ManModeTimer.Start();

                        //SendManualCommand(pic32_open, sArray[1]);
                        //Kill auto mode timer if auto mode has been set
                        if (AutoModeTimer != null)
                            AutoModeTimer.Stop();
                    }
                    else
                    {
                        ManModeTimer.Stop();
                        Log.Debug("Brian", "pic32 open fail!!");
                    }
                }
                else if (String.Compare(sArray[0], "auto", true) == 0)
                {
                    //Auto mode
                    NaviMode = 3;
                    ManModeTimer.Stop();
                    if (String.Compare(sArray[1], "scheduledTime", true) == 0)
                    {
                        dtScheduleTime = Convert.ToDateTime(sArray[2]);
                        iScheduleTime = dtScheduleTime.Hour * 60 + dtScheduleTime.Minute;
                        //Log.Debug("Brian", "[XMPP]Recevied schedule time=" + sScheduleTime);
                        wr.Write("[Auto]Receive auto scheduledTime=" + sArray[2]);
                        wr.Write("\r\n");
                    }
                    else if (String.Compare(sArray[1], "coordinate", true) == 0)
                    {

                        if (String.Compare(sArray[2], "start", true) == 0)
                        {
                            //clear AutoModeTargetAmount and AutoTargetNow
                            AutoTargetAmount = 0;
                            AutoTargetNow = 0;
                            wr.Write("[Auto]Receive auto Start!");
                            wr.Write("\r\n");

                            //Log.Debug("Brian", "[Auto]Receive auto Start!!");
                    
                        }
                        else if (String.Compare(sArray[2], "end", true) == 0)
                        {
                            //Auto mode setup completely
                            if (TargetNotWalkable == 1)
                            {
                                //Brian+ 2015/05/13: clear path and timer if any target is not walkable
                                Array.Clear(AutoTarget, 0, 10);
                                
                                if (AutoModeTimer != null)
                                    AutoModeTimer.Close();
                                
                                TargetNotWalkable = 0;
                                //wr.WriteLine("[Auto]One target is not walkable, clear path and timer!!!");
                                wr.Write("[Auto]One target is not walkable, clear path and timer!!!");
                                wr.Write("\r\n");
                                //Log.Debug("Brian", "[Auto]One target is not walkable, clear path and timer!!!");
                            
                            }
                            else
                            {
                                AutoModeTimer = new System.Timers.Timer();
                                AutoModeTimer.Interval = 1000 * 60;
                                AutoModeTimer.Elapsed += new System.Timers.ElapsedEventHandler(AutoModeTimerHandler);
                                AutoModeTimer.Start();

                                xmppSendMsg("auto setUpDone");
                                //Log.Debug("Brian", "[Auto]auto setup done");
                                
                                wr.Write("[Auto]auto setup done!!");
                                wr.Write("\r\n");
                                wr.Flush();
                                
                            }
                            
                        }
                        else
                        {
                            AutoTarget[AutoTargetAmount].X = Convert.ToInt16(sArray[2]);
                            AutoTarget[AutoTargetAmount].Y = Convert.ToInt16(sArray[3]);
                            AutoTarget[AutoTargetAmount].StopTime = Convert.ToInt16(sArray[4]);

                            AutoCheckWalkable(AutoTarget[AutoTargetAmount].X, AutoTarget[AutoTargetAmount].Y, NaviMode);
                            AutoTargetAmount++;
                            //Log.Debug("Brian", "[Auto]auto setup done");
                            //Log.Debug("Brian", "[Auto]Receive Target" + AutoTargetAmount + " X="
                            //            + AutoTarget[AutoTargetAmount - 1].X + " ,Y=" + AutoTarget[AutoTargetAmount - 1].Y);
                              
                            wr.WriteLine("[Auto]Receive Target" + AutoTargetAmount + " X="
                                        + AutoTarget[AutoTargetAmount - 1].X + " ,Y=" + AutoTarget[AutoTargetAmount - 1].Y);
                                                   
                        }    
                    }
                }
            };
            #endregion

            Thread ThreadAlgorithm = new Thread(new ThreadStart(algorithms));
            ThreadAlgorithm.IsBackground = true;
            ThreadAlgorithm.Priority = System.Threading.ThreadPriority.AboveNormal;

            labelVehicle.Visibility = ViewStates.Visible;

            //Thread ThreadAnchor = new Thread(new ThreadStart(renew_anchor_rate));
            //ThreadAnchor.IsBackground = true;
            ////ThreadAnchor.Priority = System.Threading.ThreadPriority.BelowNormal;
            //ThreadAnchor.Start();

            timer1 = new System.Timers.Timer();
            timer1.Interval = 200;
            timer1.Elapsed += new System.Timers.ElapsedEventHandler(timer1_Tick);
            timer1.Stop();

            ManModeTimer = new System.Timers.Timer();
            ManModeTimer.Interval = 100;
            ManModeTimer.Elapsed += new System.Timers.ElapsedEventHandler(ManModeTimerHandler);
            ManModeTimer.Stop();

            //Brian+ for test: create new default.set and raw.bmp anyway
            System.IO.Directory.CreateDirectory(path);

            //i=0;
            
            using(var stream = Assets.Open("default.set"))
            {
                //using (FileStream fs = new FileStream(path + @"/default.set", FileMode.CreateNew))
                using (FileStream fs = new FileStream(path + @"/default.set", FileMode.Create))
                {
                    byte[] byt = new byte[1024];
                    while ((i = stream.Read(byt, 0, byt.Length)) != 0)
                    {
                        fs.Write(byt, 0, i);
                    }
                    fs.Flush();
                }
            }
            using (var stream = Assets.Open("Raw.bmp"))
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

            // create log file
            string logName = path + @"/" + string.Format("log{0:s}", DateTime.Now) + ".txt";       // Set the file name
            wr = new System.IO.StreamWriter(logName, true);
            Log.Debug("Patrick", "Log File Name:" + logName);
            wr.Write("==== " + DateTime.Now.ToString("yyyyMMdd,HH:mm:ss ") + " ====\r\n");

            view = new DrawView(this);

            view.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            linearContent.AddView(view);

            btnCalCompass.Enabled = true;
            btnCalCompass.Click += (sender, e) =>
            {
                byte[] outbyte = new byte[10];

                hpcounter4.Start();
                outbyte[0] = 0x53;
                outbyte[1] = 0x01;
                outbyte[2] = 0x04;
                outbyte[3] = 0x00;
                outbyte[4] = 0x00;
                outbyte[5] = 0x45;

                Uart2C.SendMsgUart(pic32_open, outbyte);
            };

            spinnerTarget.ItemSelected += (sender, e) =>
            {   
                Log.Debug("Patrick", "Item Index=" + e.Position);
                if (e.Position == 0)
                    return;
                int ArrayIdx = e.Position - 1;
                Single diffX;
                Single diffY;

                target[0].X = myTag.Avg.X;
                target[0].Y = myTag.Avg.Y;

                if (!myFlag.moving && target_total < 100)
                {
                    target_total++;
                    target[target_total].X = favorite[ArrayIdx].X;
                    target[target_total].Y = favorite[ArrayIdx].Y;
                    diffX = target[target_total].X - target[target_total - 1].X;
                    diffY = target[target_total].Y - target[target_total - 1].Y;
                    target[target_total].Theta = (Single)(Math.Atan2(diffY, diffX) * 180f / 3.14f);
                    if (target[target_total].Theta < 0) target[target_total].Theta = target[target_total].Theta + 360;
                    else if (target[target_total].Theta > 360) target[target_total].Theta = target[target_total].Theta - 360;

                    if (target_total >= 1)
                    {
                        btnDelete.Enabled = true;
                        btnGo.Enabled = true;
                    }
                }
                view.Invalidate();

            };

            view.Visibility = ViewStates.Gone;
            
            view.OnDrawing += (sender, e) =>
            {

                int i;
                Single tmpSingle1, tmpSingle2;

                // Set pens
                Paint pen_anchor_idle = new Paint() { Color = Color.LightPink, StrokeWidth = 3 };
                Paint pen_anchor_activate = new Paint() { Color = Color.Red, StrokeWidth = 3 };
                //pen_anchor.SetStyle(Paint.Style.Stroke); //Brian+ for set paint style=stroke
                Paint pen_tag = new Paint() { Color = Color.DodgerBlue, StrokeWidth = 8 }; 
                Paint pen_shadow = new Paint() { Color = Color.Indigo, StrokeWidth = 8 };
                Paint pen_target = new Paint() { Color = Color.Orange, StrokeWidth = 5 };

                //pen_indication.StartCap = LineCap.ArrowAnchor;
                //pen_indication.EndCap = LineCap.Round;

                // Graphics

                for (i = 0; i < anchor.Count; i++)
                {
                    if (anchor[i].Chosen) e.DrawCircle((int)(anchor[i].X / screen2cm_x), (int)(anchor[i].Y / screen2cm_y), 5, pen_anchor_activate);
                    else e.DrawCircle((int)(anchor[i].X / screen2cm_x), (int)(anchor[i].Y / screen2cm_y), 5, pen_anchor_idle);
                }
                
                if ((TowardTargets.Status & TowardTargets.e_status.Moving) > 0) target_now = TowardTargets.TargetNow;

                if (target_total >= 1 && target_now >= 1)
                {
                    for (i = target_now; i <= target_total; i++)
                    {
                        e.DrawCircle((int)(target[i].X / screen2cm_x) - 4, (int)(target[i].Y / screen2cm_y) - 4, 7, pen_target);
                        //Brian+ for debug
                        //Log.Debug("Brian", "[SCREEN]i=" + i + ", Target.X=" + (int)(target[i].X / screen2cm_x) + ", Target.Y=" + (int)(target[i].Y / screen2cm_y));
                        
                        e.DrawLine((int)(target[i - 1].X / screen2cm_x), (int)(target[i - 1].Y / screen2cm_y), (int)(target[i].X / screen2cm_x), (int)(target[i].Y / screen2cm_y), pen_target);
                    }
                }

                
                tmpSingle1 = (Single)(20 * Math.Cos(myVehicle.compass * 3.14 / 180f)) + myTag.Avg.X / screen2cm_x;
                tmpSingle2 = (Single)(20 * Math.Sin(myVehicle.compass * 3.14 / 180f)) + myTag.Avg.Y / screen2cm_y;
                e.DrawCircle((int)(3 + myTag_Old_X[0] / screen2cm_x), (int)(3 + myTag_Old_Y[0] / screen2cm_y), 8, pen_shadow);
                e.DrawLine((int)(3 + tmpSingle1), (int)(3 + tmpSingle2), (int)(3 + myTag.X / screen2cm_x), (int)(3 + myTag.Y / screen2cm_y), pen_shadow);
                for (i = 0; i <= myTag_Old_X.Length - 1; i++)
                {
                    e.DrawCircle((int)(myTag_Old_X[i] / screen2cm_x), (int)(myTag_Old_Y[i] / screen2cm_y), 8, pen_tag);
                }
                e.DrawLine((int)tmpSingle1, (int)tmpSingle2, (int)(myTag.X / screen2cm_x), (int)(myTag.Y / screen2cm_y), pen_tag);
            };

            btnDelete.Click += (sender, e) =>
            {
                TowardTargets.Abort();
                myFlag.moving = false;
                target_total = 0;
                target_now = 1;
                btnDelete.Enabled = false;
                btnGo.Enabled = false;
                spinnerTarget.SetSelection(0);
                RunOnUiThread(() => view.Invalidate());
            };
            
            btnGo.Click += (sender, e) =>
            {
                //Log.Debug("Brian", "myFlag.moving=" + myFlag.moving);
                if (!myFlag.moving)
                {

                    myFlag.moving = true;
                    wr.Write(DateTime.Now.ToString("HH:mm:ss ")+"New Route");
                    wr.Write("\r\n");

                    TowardTargets.NewTask(target, target_total);
                    //Thread dispatchloop = new Thread(new ThreadStart(Dispatch));
                    //dispatchloop.IsBackground = true;
                    //dispatchloop.Priority = System.Threading.ThreadPriority.BelowNormal;
                    //dispatchloop.Start();

                }
            };
            
            view.OnTouching += (sender, e) =>
             {
                 if (e.Action == MotionEventActions.Down)
                 {
                     if (!myFlag.screen_ready) return; //Toby's patch

                     
                     short walkable;
                     Single diffX;
                     Single diffY;
                     //Single tmpSingle;
                     Single tmpSingle1, tmpSingle2, tmpSingle3;
                     
                     Point a = new Point();
                     Point b = new Point();
                     //List<Point> corner = new List<Point>();
                     target[0].X = myTag.Avg.X;
                     target[0].Y = myTag.Avg.Y;

                     walkable = myMap.CheckWalk((int)(e.GetX() * screen2grid_x), (int)(e.GetY() * screen2grid_y));
                     
                     tmpSingle1 = target[target_total].X - e.GetX() *screen2cm_x;
                     tmpSingle2 = target[target_total].Y - e.GetY() *screen2cm_y;
                     tmpSingle3 = tmpSingle1 * tmpSingle1 + tmpSingle2 * tmpSingle2;
                     if (tmpSingle3 < 400) return;
                     
                     if (walkable != 0)
                     {
                         
                     }
                     else
                     {
                        if (!myFlag.moving && target_total < 100)
                        {
                            a.X = (int)(target[target_total].X / screen2cm_x * screen2grid_x);
                            a.Y = (int)(target[target_total].Y / screen2cm_y * screen2grid_y);
                            b.X = (int)(e.GetX() * screen2grid_x);
                            b.Y = (int)(e.GetY() * screen2grid_y);
                            myMap.initial_position(a, b);

                            if (myMap.Autoflag == true)
                            {
                                if (myMap.action() == false) { Log.Debug("Brian", "actionFalse"); } else { Log.Debug("Brian", "actionTrue"); }
                            }
                            //if (myMap.action() == false) return;
                                
                            RunOnUiThread(() => textView.Append("Add " + myMap.path_Result.Count.ToString() + " targets\r\n"));
                            //myMap.path_Result.Clear();
                            //myMap.path_Result.Add(b);
                            if (myMap.path_Result.Count <= 0) return;

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
                                RunOnUiThread(() => textView.Append(target[target_total].X.ToString() + ", " + target[target_total].Y.ToString() + ", " + target[target_total].Theta.ToString("f2") + "\r\n"));
                                
                                //textBox1.Text = textBox1.Text + target[target_total].X.ToString() + ", " + target[target_total].Y.ToString() + ", "+target[target_total].Theta.ToString("f2")+ "\r\n";
                             }
                        }

                        if (target_total >= 1)
                        {
                            target_now = 1;
                            btnDelete.Enabled = true;
                            btnGo.Enabled = true;
                        }

                     }
                     RunOnUiThread(() => view.Invalidate());
                     //view.Invalidate();
                 }
             };


            btnConnect.Click+=(sender,e)=>
            {
                string Port1="";
                string Port2="";
                int Baund1=0;
                int Baund2=0;
                int count1 = 0;

                btnCalCompass.Visibility = ViewStates.Gone;
                
                tag_open = Uart2C.OpenUart("ttymxc2");
                if (tag_open > 0)
                {
                    Uart2C.SetUart(1); //0: B9600, 1:B115200, 2:B19200                    
                    textView.Append("Open ttymxc2 successfully, Baund Rate=115200, fd_num=" + tag_open +"\r\n");
                } 
                else
                {
                    textView.Append("Open ttymx2 fail!!\r\n");
                }

                //Brian+ for test: call SetAppearance() to set screen2map_x and screen2map_y 
                SetAppearance();
                
                //if (tagPort.IsOpen && drivingPort.IsOpen)
                //if ((pic32_open > 0) && (tag_open > 0))   
                if(tag_open > 0 )
                {
                    
                    //timer1.Enabled = true;

                    //Brian+ Add timers to hook driving_DataReceived() and tag_DataReceived()
                    //drivingBuff.Received = new byte[100];
                    //drivingBuff.Start = -1;
                    //drivingBuff.End = -1;
                    //Pic32DataRecvTimer = new System.Timers.Timer();
                    //Pic32DataRecvTimer.Interval = 50;
                    //Pic32DataRecvTimer.Elapsed += new System.Timers.ElapsedEventHandler(driving_DataReceived);
                    //Pic32DataRecvTimer.Start();

                    TagDataRecvTimer = new System.Timers.Timer();
                    TagDataRecvTimer.Interval = 100;
                    TagDataRecvTimer.Elapsed += new System.Timers.ElapsedEventHandler(tag_DataReceived);
                    TagDataRecvTimer.Start();

                    hpcounter4.Start();
                    btnConnect.Enabled = false;
                    myFlag.screen_ready = true;
                    btnCalCompass.Enabled = true;

                    //TowardOneTarget.Start();
                    //TowardOneTarget.ControlEvent += ControlOut;

                    //Brian+: 2015/04/22 [xmpp]Add send coordinate timer
                    SendCoordinateTimer = new System.Timers.Timer();
                    SendCoordinateTimer.Interval = 1000;
                    SendCoordinateTimer.Elapsed += new System.Timers.ElapsedEventHandler(SendCoordinateHandler);
                    SendCoordinateTimer.Start();
                }
                Thread.Sleep(500);
                ThreadAlgorithm.Start();
                timer1.Start();
            };

            #region originally in btnLoad
            view.Visibility = ViewStates.Visible;
            bool success;
            string file1 = path + @"/default.set";
            string file2 = path + @"/Raw";

            success = ReadConfig(file1);
            if (!success)
            {
                textView.Append(file1 + " doesn't exist.\r\n");
            }
            else
            {
                textView.Append("Reading " + file1 + "\r\n");
                myMap.Grid_W = cfg.GridWidth;
                myMap.Grid_H = cfg.GridHeight;
                myMap.East = cfg.MapEast;
                myVehicle.East = cfg.MapEast;

                textView.Append("Loading image file " + file2 + ".bmp \r\n");
                success = myMap.LoadFile(file2);
                if (!success)
                {
                    textView.Append(file2 + " doesn't exist.\r\n");
                }
                else
                {
                    textView.Append("Processing data.\r\n");
                    success = myMap.Preprocess();
                    if (!success)
                    {
                        textView.Append("Initial fail. \r\n");
                    }
                    else
                    {
                        Bitmap bitmap = BitmapFactory.DecodeFile(file2 + ".bmp");
                        Drawable drawable = new BitmapDrawable(bitmap);
                        linearContent.SetBackgroundDrawable(drawable);

                        labelTable.Visibility = ViewStates.Visible;
                        btnConnect.Enabled = true;

                    }

                }

            }
            #endregion
        }
        //Brian+ 2015/04/20: Add xmpp AutoModeTimer handler
        private void AutoModeTimerHandler(object sender, EventArgs e)
        {
            int iCurrentTime = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            //Log.Debug("Brian", "[XMPP]ScheduleTime=" + sScheduleTime + ", CurrentTime=" + sCurrentTime);
            if (iCurrentTime == iScheduleTime)
            {
                AutoGenCornerCoodinate(AutoTarget[AutoTargetNow].X, AutoTarget[AutoTargetNow].Y, NaviMode);
                if (!myFlag.moving)
                {
                    myFlag.moving = true;
                    
                    if (AutoTargetNow == 0)
                        xmppSendMsg("auto start");
                    
                    TowardTargets.NewTask(target, target_total);
                    wr.Write("[Auto]Navigation start at" + DateTime.Now.ToString() +
                             " to (" + AutoTarget[AutoTargetNow].X + ", " + AutoTarget[AutoTargetNow].Y + ")");
                    wr.Write("\r\n");
                        //Log.Debug("Brian", "[Auto]Navigation start at" + sCurrentTime);
                }
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

        //Brian+ 2015/04/20: Add xmpp SendCoordinateTimer handler
        private void SendCoordinateHandler(object sender, EventArgs e)
        {
            int tag_x = 0, tag_y = 0, compass = 0;

            //if (bXmppConnection && bBeaconFind)
            if (bXmppConnection)
            {
                tag_x = Convert.ToInt32(myTag.Avg.X / pad2cm_x);
                tag_y = Convert.ToInt32(myTag.Avg.Y / pad2cm_y);
                compass = Convert.ToInt32(myVehicle.compass);

                strSendMsg = "source " + tag_x.ToString() + " " + tag_y.ToString() + " " + compass.ToString();
                xmppSendMsg(strSendMsg);
               
            }
        }

        //Brian+ 2015/05/07: Add xmppSendMsg() to send xmpp message
        public void xmppSendMsg(string message)
        {
            var msg = new Matrix.Xmpp.Client.Message
            {
                Type = Matrix.Xmpp.MessageType.Chat,
                To = strTargetName,
                Body = message
            };
            
            if (xmppClient != null)
                xmppClient.Send(msg);
        }

        public void AutoCheckWalkable(int TargetX, int TargetY, int Mode)
        {
            int walkable;

            walkable = myMap.CheckWalk((int)(TargetX * screen2grid_x), (int)(TargetY * screen2grid_y));

            if (walkable != 0)
            {
                //Can not walk, send message to pad
                if (Mode == 2)
                    strSendMsg = "semiauto walkable 0";
                else if (Mode == 3)
                    strSendMsg = "auto walkable 0";
                
                xmppSendMsg(strSendMsg);
                TargetNotWalkable = 1;
            }
            else
            {
                if (Mode == 2)
                    strSendMsg = "semiauto walkable 1";
                else if (Mode == 3)
                    strSendMsg = "auto walkable 1";

                xmppSendMsg(strSendMsg);
                
            }
            
        }

        private void comboTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            Single diffX;
            Single diffY;

            target[0].X = myTag.Avg.X;
            target[0].Y = myTag.Avg.Y;

            if (!myFlag.moving && target_total < 100)
            {
                target_total++;
                // target[target_total].X = favorite[comboTarget.SelectedIndex].X;
                // target[target_total].Y = favorite[comboTarget.SelectedIndex].Y;
                diffX = target[target_total].X - target[target_total - 1].X;
                diffY = target[target_total].Y - target[target_total - 1].Y;
                target[target_total].Theta = (Single)(Math.Atan2(diffY, diffX) * 180f / 3.14f);
                if (target[target_total].Theta < 0) target[target_total].Theta = target[target_total].Theta + 360;
                else if (target[target_total].Theta > 360) target[target_total].Theta = target[target_total].Theta - 360;

                if (target_total >= 1)
                {
                    btnDelete.Enabled = true;
                    btnGo.Enabled = true;
                }
            }
            //view.Invalidate();
            RunOnUiThread(() => view.Invalidate());
        }
        
        public void AutoGenCornerCoodinate(int TargetX, int TargetY, int Mode)
        {
            short walkable;
            Single diffX;
            Single diffY;
            Single tmpSingle1, tmpSingle2, tmpSingle3;

            Point a = new Point();
            Point b = new Point();

            //Log.Debug("Brian", "[Auto]GenCornerCoodinate(" + TargetX +", " + TargetY + ")");

            //Brian+ mark for test
            walkable = myMap.CheckWalk((int)(TargetX * screen2grid_x), (int)(TargetY * screen2grid_y));

            //Ignore distance < 20cm(400/20) between 2 points
            tmpSingle1 = target[target_total].X - TargetX * screen2cm_x;
            tmpSingle2 = target[target_total].Y - TargetY * screen2cm_y;
            tmpSingle3 = tmpSingle1 * tmpSingle1 + tmpSingle2 * tmpSingle2;
            //if (tmpSingle3 < 400) return;  

            target_total = 0;
            target_now = 1;

            if (walkable != 0)
            {
             
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
                    if (myMap.Autoflag == true) myMap.action();
                    RunOnUiThread(() => textView.Append("Add " + myMap.path_Result.Count.ToString() + " targets\r\n"));
                    if (myMap.path_Result.Count <= 0) return; 

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
                        RunOnUiThread(() => textView.Append(target[target_total].X.ToString() + ", " + target[target_total].Y.ToString() + ", " + target[target_total].Theta.ToString("f2") + "\r\n"));

                    }
                }

                //Log.Debug("Brian", "[Auto]target_total ==" + target_total);
                wr.Write("[Auto]Gen coordinate, target_total=" + target_total);
                wr.Write("\r\n");

                if (target_total >= 1)
                {

                    RunOnUiThread(() => btnDelete.Enabled = true);
                    RunOnUiThread(() => btnGo.Enabled = true);
                    RunOnUiThread(() => view.Invalidate());


                }
                
            }
        }
        
        //Brian+ 2015/04/27: Add GenCornerCoodinate() to generate corner's coordinate
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
            
            //Brian+ 2015/05/04: clear path only for semi-auto mode because it support one target only
            if (Mode == 2)
            {
                target_total = 0;
                target_now = 1;
            }
            
            if (walkable != 0)
            {
                //Can not walk, send message to pad
                if (Mode == 2)
                    strSendMsg = "semiauto walkable 0";
                else if (Mode == 3)
                    strSendMsg = "auto walkable 0";
                xmppSendMsg(strSendMsg);
                
            }
            else
            {
                if (Mode == 2)
                    strSendMsg = "semiauto walkable 1";
                else if (Mode == 3)
                    strSendMsg = "auto walkable 1";

                xmppSendMsg(strSendMsg);
                
                target[0].X = myTag.Avg.X;
                target[0].Y = myTag.Avg.Y;
                
                if (!myFlag.moving && target_total < 100)
                {
                    a.X = (int)(target[target_total].X / screen2cm_x * screen2grid_x);
                    a.Y = (int)(target[target_total].Y / screen2cm_y * screen2grid_y);
                    b.X = (int)(TargetX * screen2grid_x);
                    b.Y = (int)(TargetY * screen2grid_y);
                    myMap.initial_position(a, b);
                    if (myMap.Autoflag == true) myMap.action();
                    RunOnUiThread(() => textView.Append("Add " + myMap.path_Result.Count.ToString() + " targets\r\n"));

                    if (myMap.path_Result.Count <= 0) return;
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
                        RunOnUiThread(() => textView.Append(target[target_total].X.ToString() + ", " + target[target_total].Y.ToString() + ", " + target[target_total].Theta.ToString("f2") + "\r\n"));

                    }
                }
                                
                if (target_total >= 1)
                {

                    RunOnUiThread(() => btnDelete.Enabled = true);
                    RunOnUiThread(() => btnGo.Enabled = true);
                    RunOnUiThread(() => view.Invalidate());
                   
                }
                //return target;
            }
        }
        
        private void tag_DataReceived(object sender, EventArgs e)
        {
            int readbuff;
            int tmpInt1, tmpInt2, tmpInt3, tmpId;
            String TagRecvData = null;
            Single tmpSingle1;
            bool boolmsg;

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
                        Log.Debug("Brian", "meas=" + tmpId.ToString() + ", " + anchor[tmpId].Range.ToString());
                    }
                }
                else
                {
                    tagbuff[tagbufflen] = (byte)readbuff;
                    tagbufflen++;
                }
            }

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
        private void driving_DataReceived(object sender, EventArgs e)
        {
            int readbuff;
            int startindex;
            short tmpShort;
            byte[] Pic32RecvData = new byte[50];          

            Pic32RecvData = Uart2C.ReceiveMsgUartByte(pic32_open);

            if (Pic32RecvData == null) return;
            if (Pic32RecvData.Length< 16) return;
            //Log.Debug("Brian", "Pic32RecvData(Convert)=" + ToHexString(Pic32RecvData));

            startindex = -1;
            for (int j = 0; j < Pic32RecvData.Length-16 ; j++)
            {
                if (Pic32RecvData[j] == 0x53 && Pic32RecvData[j + 1] == 0x20 && Pic32RecvData[j + 16] == 0x45)
                {
                    startindex = j + 1;
                    break;
                }
            }
			
			if(startindex > 0)
			{
                if ((startindex < Pic32RecvData.Length - 15 ) && (Pic32RecvData[startindex + 15] == 0x45))
                {
                    byte[] tmpArray = new byte[15];
                    Array.Copy(Pic32RecvData, startindex, tmpArray, 0, 15);
                    myVehicle.UpdatedAll(tmpArray);
                }
			}

            TowardTargets.Vehicle.Bumper = myVehicle.Bumper;
            for (int i = 0; i < 5; i++) TowardTargets.Vehicle.sonic[i] = myVehicle.sonic[i];
            //tmpString = test_count.ToString() +" D=" + myVehicle.compass.ToString() + ", Sonic=" + myVehicle.sonic[0].ToString() + "," + myVehicle.sonic[1].ToString() + "," + myVehicle.sonic[2].ToString() + "," + myVehicle.sonic[3].ToString() + "," + myVehicle.sonic[4].ToString() + "," + myVehicle.sonic[5].ToString() + "," + myVehicle.sonic[6].ToString() + ", B=" + myVehicle.Bumper.ToString();

            //RunOnUiThread(() => labelTable.Text = tmpString);
        }

        private void SetAppearance()
        {
            MainWidth = this.linearContent.Width;
            MainHeight = this.linearContent.Height;

            screen2cm_x = (Single)cfg.MapWidth / (Single)MainWidth;
            screen2cm_y = (Single)cfg.MapHeight / (Single)MainHeight;
            screen2grid_x = (Single)cfg.GridWidth / (Single)MainWidth;
            screen2grid_y = (Single)cfg.GridHeight / (Single)MainHeight;
            pad2cm_x = (Single)cfg.MapWidth / 640;
            pad2cm_y = (Single)cfg.MapHeight / 359;

            Log.Debug("Brian", "ScreenWidth=" + MainWidth.ToString());
            Log.Debug("Brian", "ScreenHeight=" + MainHeight.ToString());

            labelVehicle.SetX(offset);
            labelVehicle.SetY(MainHeight - 50);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            string tmpString="";

            for (i = 0; i < anchor.Count; i++)
            {
                if (anchor[i].Reliability >= 0) tmpString = tmpString + anchor[i].Range.ToString("f1") + " cm | ";
                else tmpString = tmpString + "Offline |";
            }

            RunOnUiThread(() => labelTable.Text = tmpString);
            RunOnUiThread(() => labelVehicle.Text = " D=" + myVehicle.compass.ToString() + ", x=" + myTag.Avg.X.ToString("f1") + " y=" + myTag.Avg.Y.ToString("f1"));

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
            
            RunOnUiThread(() => view.Invalidate());

            if (TowardTargets.Status == TowardTargets.e_status.Finish)
            {
                target_total = 0;
                target_now = 1;
                TowardTargets.Status = TowardTargets.e_status.None;

                //Brian+ 2015/05/07: Send reach target XMPP message to pad
                string strReachMsg="";
                
                if (NaviMode == 2)
                {
                    strReachMsg = "semiauto end";
                    xmppSendMsg(strReachMsg);
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
                        wr.Write("[Auto]Reach target number " + AutoTargetNow + ", prepare to next target at " + iScheduleTime / 60 + ":" +
                                 iScheduleTime % 60);
                        wr.Write("\r\n");
                        wr.Flush();
                    }
                    else
                    {
                        AutoTargetNow = 0;
                        AutoTargetAmount = 0;
                        AutoModeTimer.Close();
                        strReachMsg = "auto end";
                        xmppSendMsg(strReachMsg);
                        //Log.Debug("Brian", "Reach number " + AutoTargetNow + "target, prepare to next one!!");
                        wr.Write("Reach the last target, Stop auto navigation!!");
                        wr.Write("\r\n");
                        wr.Flush();
                        //wr.Close();
                    }

                }
                              
            }

            if (TowardTargets.Status == TowardTargets.e_status.None) myFlag.moving = false;
            else myFlag.moving = true;

            RunOnUiThread(() => view.Invalidate());
        }

        //Brian+ 2015/04/23: Add SendManualCommand() for sending manual command to PIC32
        private void SendManualCommand(int fd, string direction)
        {
            byte[] outbyte = new byte[6];

            outbyte[0] = 0x53;
            outbyte[1] = 0x11;
            outbyte[3] = 0x00;
            outbyte[4] = 0x00;
            outbyte[5] = 0x45;
            if (String.Compare(direction, "forward", true) == 0)
                outbyte[2] = 0x01;
            else if (String.Compare(direction, "backward", true) == 0)
                outbyte[2] = 0x02;
            else if (String.Compare(direction, "left", true) == 0)
                outbyte[2] = 0x03;
            else if (String.Compare(direction, "right", true) == 0)
                outbyte[2] = 0x04;
            else if (String.Compare(direction, "forRig", true) == 0)
                outbyte[2] = 0x07;
            else if (String.Compare(direction, "bacRig", true) == 0)
                outbyte[2] = 0x08;
            else if (String.Compare(direction, "forLeft", true) == 0)
                outbyte[2] = 0x05;
            else if (String.Compare(direction, "bacLeft", true) == 0)
                outbyte[2] = 0x06;

            Log.Debug("Brian", "outbyte[]=" + ToHexString(outbyte));
            Uart2C.SendMsgUart(pic32_open, outbyte);
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
                if (anchor_num<2)
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
                    for (i = 0; i < anchor.Count;i++ )
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
                    Log.Debug("Brian", "interval= " + myEKF.dT.ToString("f1"));
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

        /*
        private void renew_anchor_rate()
        {	
			while(true)
			{
                Thread.Sleep(1000);
				anchor1.RateSum = anchor1.Rate;
				anchor1.Rate = 0;
				anchor2.RateSum = anchor2.Rate;
				anchor2.Rate = 0;
				anchor3.RateSum = anchor3.Rate;
				anchor3.Rate = 0;
                anchor4.RateSum = anchor4.Rate;
                anchor4.Rate = 0;
                anchor5.RateSum = anchor5.Rate;
                anchor5.Rate = 0;
                anchor6.RateSum = anchor6.Rate;
                anchor6.Rate = 0;

                for (i = anchor_num.Length - 1; i > 0; i--)
                {
                    anchor_num[i] = anchor_num[i - 1];
                }
                anchor_num[0] = 0;

                if (anchor1.RateSum > 0)
                {
                    anchor1.Message = anchor1.Range.ToString("f0") + " cm  " + anchor1.RateSum.ToString() + "/s";
                    anchor_num[0]++;
                }
                else anchor1.Message = "offline";
                if (anchor2.RateSum > 0)
                {
                    anchor2.Message = anchor2.Range.ToString("f0") + " cm  " + anchor2.RateSum.ToString() + "/s";
                    anchor_num[0]++;
                }
                else anchor2.Message = "offline";
                if (anchor3.RateSum > 0)
                {
                    anchor3.Message = anchor3.Range.ToString("f0") + " cm  " + anchor3.RateSum.ToString() + "/s";
                    anchor_num[0]++;
                }
                else anchor3.Message = "offline";
                if (anchor4.RateSum > 0)
                {
                    anchor4.Message = anchor4.Range.ToString("f0") + " cm  " + anchor4.RateSum.ToString() + "/s";
                    anchor_num[0]++;
                }
                else anchor4.Message = "offline";
                if (anchor5.RateSum > 0)
                {
                    anchor5.Message = anchor5.Range.ToString("f0") + " cm  " + anchor5.RateSum.ToString() + "/s";
                    anchor_num[0]++;
                }
                else anchor5.Message = "offline";
                if (anchor6.RateSum > 0)
                {
                    anchor6.Message = anchor6.Range.ToString("f0") + " cm  " + anchor6.RateSum.ToString() + "/s";
                    anchor_num[0]++;
                }
                else anchor6.Message = "offline";
			}
			
        }
        */
        protected override void OnDestroy()
        {
            myFlag.moving = false;

            wr.Write("{0:mm:ss} ", DateTime.Now);
            wr.Write("Program End\r\n");
            wr.Close();
                        
            base.OnDestroy(); 

        }

        private bool ReadConfig(string ff)
        {
            if (File.Exists(ff))
            {
                StreamReader sr = new StreamReader(ff);
                int tmpInt,count1=0;
                string line;
                string[] linesplit;
                var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem);
                adapter.Add("Favorite");
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
                    else if(linesplit[0] == "anchor")
                    {
                        class_Anchors tmpanchor = new class_Anchors(Convert.ToInt32(linesplit[1]), Convert.ToSingle(linesplit[2]), Convert.ToSingle(linesplit[3]), Convert.ToSingle(linesplit[4]));
                        anchor.Add(tmpanchor);
                    }
                    else if (linesplit[0] == "favorite")
                    {
                        favorite[count1].Note = linesplit[1];
                        favorite[count1].X = Convert.ToInt16(linesplit[2]);
                        favorite[count1].Y = Convert.ToInt16(linesplit[3]);
                        adapter.Add(favorite[count1].Note);
                        count1++;
                    }
                    
                }
                sr.Close();
                spinnerTarget.Adapter = adapter;
                Log.Debug("Brian", "anchor num=" + anchor.Count.ToString());
                return true;
            }
            else return false;
        }

        private bool loc_initial()
        {
            class_Anchors[] candidate3 = new class_Anchors[3];
            Single[] tmpSingleA = new Single[6];
            int anchor_count = 0;

            for (i = 0; i < 3;i++ )
            {
                candidate3[i]=anchor[i];
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
                //RunOnUiThread(() => textView.Append("Searching for beacon...\r\n"));
                return false;
            }
            else
            {
                if (!loc_trilateration(candidate3[0], candidate3[1], candidate3[2]))
                {
                    return false;
                }
                Log.Debug("Brian", "Beacon found!!");

                myEKF = new class_EKFL5(myTag.X, myTag.Y, 0f,anchor.Count);
                for (i = 0; i < anchor.Count; i++)
                {
                    myEKF.Anchor[i] = anchor[i];
                }

                return true;
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
            if(v1)
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
            for(count=0;count<myTag_Old_X.Length;count++)
            {
                myTag_Old_X[count] = x;
                myTag_Old_Y[count] = y;
            }
            return true;
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

        private void ShowCurserMsg(Single screenX, Single screenY, Single screenWidth)
        {
            Single cmX, cmY;

            cmX = screenX * screen2cm_x;
            cmY = screenY * screen2cm_y;
            RunOnUiThread(() => labelXY.Text = cmX.ToString("f1") + ", " + cmY.ToString("f1"));
            if (screenX>screenWidth-150) RunOnUiThread(() => labelXY.SetX(screenX-150));
            else RunOnUiThread(() => labelXY.SetX(screenX));
            RunOnUiThread(() => labelXY.SetY(screenY));
        }

        private bool GenPath(Single screenX, Single screenY)
        {
            short walkable;
            Single diffX;
            Single diffY;
            Single tmpSingle1, tmpSingle2, tmpSingle3;

            Point a = new Point();
            Point b = new Point();
            target[0].X = myTag.Avg.X;
            target[0].Y = myTag.Avg.Y;

            walkable = myMap.CheckWalk((int)(screenX * screen2grid_x), (int)(screenY * screen2grid_y));
            if (walkable != 0) return false;

            tmpSingle1 = target[target_total].X - screenX * screen2cm_x;
            tmpSingle2 = target[target_total].Y - screenY * screen2cm_y;
            tmpSingle3 = tmpSingle1 * tmpSingle1 + tmpSingle2 * tmpSingle2;
            if (tmpSingle3 < 400) return false;

            if (!myFlag.moving && target_total < 100)
            {
                a.X = (int)(target[target_total].X / screen2cm_x * screen2grid_x);
                a.Y = (int)(target[target_total].Y / screen2cm_y * screen2grid_y);
                b.X = (int)(screenX * screen2grid_x);
                b.Y = (int)(screenY * screen2grid_y);
                myMap.initial_position(a, b);

                if (myMap.Autoflag == true)
                {
                    if (myMap.action() == false) { Log.Debug("Brian", "actionFalse"); } else { Log.Debug("Brian", "actionTrue"); }
                }
                //if (myMap.action() == false) return;

                RunOnUiThread(() => textView.Append("Add " + myMap.path_Result.Count.ToString() + " targets\r\n"));
                //myMap.path_Result.Clear();
                //myMap.path_Result.Add(b);
                

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
                    RunOnUiThread(() => textView.Append(target[target_total].X.ToString() + ", " + target[target_total].Y.ToString() + ", " + target[target_total].Theta.ToString("f2") + "\r\n"));

                    //textBox1.Text = textBox1.Text + target[target_total].X.ToString() + ", " + target[target_total].Y.ToString() + ", "+target[target_total].Theta.ToString("f2")+ "\r\n";
                }
            }

            if (target_total >= 1)
            {
                target_now = 1;
                btnDelete.Enabled = true;
                btnGo.Enabled = true;
            }
            return true;
        }
    }


    public class DrawView : View
    {
        public event EventHandler<Canvas> OnDrawing;
        public event EventHandler<MotionEvent> OnTouching;
        
        public DrawView(Context context)
            : base(context)
        {

        }
        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
        }
        protected override void OnDraw(Canvas canvas)
        {
            if (OnDrawing != null)
                OnDrawing(this, canvas);
        }
        public override bool OnTouchEvent(MotionEvent e)
        {
            if (OnTouching != null)
                OnTouching(this, e);
            return true;
        }
    }
}

