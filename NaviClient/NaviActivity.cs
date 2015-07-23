using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Util;

namespace NaviClient
{
    [Activity(Label = "NaviClient", MainLauncher = true, Icon = "@drawable/icon")]
    public class NaviActivity : Activity
    {
        bool isBound = false;
        Messenger naviServiceMessenger, naviClientMessager;
        NaviServiceConnection naviServiceConnection;
        string Tag = "Brian";
        TextView txtCoordinate, txtMessage;
        string Robot_X = "", Robot_Y = "", Robot_Compass = "";
        string CornerNum = "", Corner1_X = "", Corner1_Y = ""; 
        System.Timers.Timer UpdateCoordinateTimer, UpdateMsgTimer;
        string NavServiceMsg = "";

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it
            Button btnUp = this.FindViewById<Button>(Resource.Id.button1);
            Button btnDown = this.FindViewById<Button>(Resource.Id.button2);
            Button btnLeft = this.FindViewById<Button>(Resource.Id.button3);
            Button btnRight = this.FindViewById<Button>(Resource.Id.button4);
            Button btnUL = this.FindViewById<Button>(Resource.Id.button5);
            Button btnUR = this.FindViewById<Button>(Resource.Id.button6);
            Button btnBL = this.FindViewById<Button>(Resource.Id.button7);
            Button btnBR = this.FindViewById<Button>(Resource.Id.button8);
            Button btnSemiGo = this.FindViewById<Button>(Resource.Id.SemiGoButton);
            Button btnSemiSet = this.FindViewById<Button>(Resource.Id.SemiSetButton);
            Button btnSemiStop = this.FindViewById<Button>(Resource.Id.SemiStopButton);
            Button btnAutoSet = this.FindViewById<Button>(Resource.Id.AutoSetButton);
            Button btnAutoStop = this.FindViewById<Button>(Resource.Id.AutoStopButton);
            Button btnUpdate = this.FindViewById<Button>(Resource.Id.button9); ;

            EditText editSemiX = this.FindViewById<EditText>(Resource.Id.editSemiX);
            EditText editSemiY = this.FindViewById<EditText>(Resource.Id.editSemiY);
            EditText editAutoX1 = this.FindViewById<EditText>(Resource.Id.editAutoX1);
            EditText editAutoY1 = this.FindViewById<EditText>(Resource.Id.editAutoY1);
            EditText editAutoStopTime1 = this.FindViewById<EditText>(Resource.Id.editAutoStopTime1);
            EditText editAutoX2 = this.FindViewById<EditText>(Resource.Id.editAutoX2);
            EditText editAutoY2 = this.FindViewById<EditText>(Resource.Id.editAutoY2);
            EditText editAutoStopTime2 = this.FindViewById<EditText>(Resource.Id.editAutoStopTime2);
            EditText editSchedule = this.FindViewById<EditText>(Resource.Id.editAutoSchedule);

            UpdateCoordinateTimer = new System.Timers.Timer();
            UpdateCoordinateTimer.Interval = 1000;
            UpdateCoordinateTimer.Elapsed += new System.Timers.ElapsedEventHandler(UpdateCoordinateHandler);
            UpdateCoordinateTimer.Stop();

            UpdateMsgTimer = new System.Timers.Timer();
            UpdateMsgTimer.Interval = 1000;
            UpdateMsgTimer.Elapsed += new System.Timers.ElapsedEventHandler(UpdateMsgHandler);
            UpdateMsgTimer.Start();
                       
            txtCoordinate = this.FindViewById<TextView>(Resource.Id.textView15);
            txtMessage = this.FindViewById<TextView>(Resource.Id.textView17);

            btnUpdate.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Update", "1");
                    message.Data = b;
                    message.What = 0;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                    Log.Debug(Tag, "[NavClient]Send update coordinate messsage to server!!!");
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };
            
            btnAutoStop.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Stop", "1");
                    message.Data = b;
                    message.What = 34;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            btnAutoSet.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Target1_X", editAutoX1.Text);
                    b.PutString("Target1_Y", editAutoY1.Text);
                    b.PutString("Target1_StopTime", editAutoStopTime1.Text);
                    b.PutString("Target2_X", editAutoX2.Text);
                    b.PutString("Target2_Y", editAutoY2.Text);
                    b.PutString("Target2_StopTime", editAutoStopTime2.Text);
                    b.PutString("ScheduleTime", editSchedule.Text);
                    b.PutString("TargetNum", "2");
                    
                    message.Data = b;
                    message.What = 30;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            
            btnSemiGo.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Start", "1");
                    message.Data = b;
                    message.What = 23;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };


            btnSemiStop.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Stop", "1");
                    message.Data = b;
                    message.What = 25;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            
            btnSemiSet.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("X", editSemiX.Text);
                    b.PutString("Y", editSemiY.Text);
                    message.Data = b;
                    message.What = 20;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            btnUp.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Direction", "forward");
                    message.Data = b;
                    message.What = 10;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            btnDown.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Direction", "backward");
                    message.Data = b;
                    message.What = 10;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            btnLeft.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Direction", "left");
                    message.Data = b;
                    message.What = 10;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            btnRight.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Direction", "right");
                    message.Data = b;
                    message.What = 10;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            btnUL.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Direction", "forLeft");
                    message.Data = b;
                    message.What = 10;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            btnUR.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Direction", "forRig");
                    message.Data = b;
                    message.What = 10;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            btnBL.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Direction", "bacLeft");
                    message.Data = b;
                    message.What = 10;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };

            btnBR.Click += delegate
            {
                if (isBound)
                {

                    Message message = Message.Obtain();
                    Bundle b = new Bundle();
                    b.PutString("Direction", "bacRig");
                    message.Data = b;
                    message.What = 10;
                    //Added by Brian: set message.Replyto
                    message.ReplyTo = naviClientMessager;
                    naviServiceMessenger.Send(message);
                }
                else
                    Log.Debug(Tag, "Client can't bound to Navigation Service!!!");
            };
        }

        protected override void OnStart()
        {
            base.OnStart();

            var naviServiceIntent = new Intent("com.XYZRobot.NaviService");
            naviServiceConnection = new NaviServiceConnection(this);
            BindService(naviServiceIntent, naviServiceConnection, Bind.AutoCreate);

            //Added by Brian: Add s2cMessager 
            //s2cMessager = new Messenger(new ClientHandler());
        }

        private void UpdateMsgHandler(object sender, EventArgs e)
        {
            RunOnUiThread(() => txtMessage.Text = NavServiceMsg);
        }

        private void UpdateCoordinateHandler(object sender, EventArgs e)
        {
            Log.Debug("Brian", "[NavClient]Update coordinate timer!! ");
            RunOnUiThread(() => txtCoordinate.Text = Robot_X + ", " + Robot_Y);    
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (isBound)
            {
                UnbindService(naviServiceConnection);
                isBound = false;
            }
        }

        class NaviServiceConnection : Java.Lang.Object, IServiceConnection
        {
            NaviActivity activity;

            public NaviServiceConnection(NaviActivity activity)
            {
                this.activity = activity;
            }

            public void OnServiceConnected(ComponentName name, IBinder service)
            {
                activity.naviServiceMessenger = new Messenger(service);
                activity.naviClientMessager = new Messenger(new naviClientHandler(this.activity));
                activity.isBound = true;

                Log.Debug("Brian", "[OnServiceConnected]Bound to Navigation Service!!!");
            }

            public void OnServiceDisconnected(ComponentName name)
            {
                activity.naviServiceMessenger.Dispose();
                activity.naviServiceMessenger = null;
                activity.naviClientMessager.Dispose();
                activity.naviClientMessager = null;
                activity.isBound = false;

                Log.Debug("Brian", "[OnServiceDisConnected]Unbound to Navigation Service!!!");
            }
        }

        class naviClientHandler : Handler
        {
            
            NaviActivity activity;
            public naviClientHandler(NaviActivity activity)
            {
                this.activity = activity;
            }
             
            public override void HandleMessage(Message msg)
            {
                Log.Debug("Brian", "[NaviClient]Receive message(what) from service:" + msg.What.ToString());

                switch (msg.What)
                {
                    case 0:
                        activity.Robot_X = msg.Data.GetString("X");
                        activity.Robot_Y = msg.Data.GetString("Y");
                        Log.Debug("Brian", "[NaviClient]Robot_X=" +  activity.Robot_X + ", Robot_Y=" +  activity.Robot_Y);
                        activity.UpdateCoordinateTimer.Start();

                        break;
                    case 21:
                        activity.NavServiceMsg = "[SemiAutoMode] Target is not walkable!!";
                        break;
                    case 22:
                        activity.CornerNum = msg.Data.GetString("CornerNum");
                        if (Convert.ToInt16(activity.CornerNum) != 0)
                        {
                            activity.Corner1_X = msg.Data.GetString("Corner1_X");
                            activity.Corner1_Y = msg.Data.GetString("Corner1_Y");
                            activity.NavServiceMsg = "[SemiAutoMode] CornerNum=" + activity.CornerNum + ", Corner1_X=" + activity.Corner1_X + ", Corner1_Y=" + activity.Corner1_Y;
                        }
                        else
                            activity.NavServiceMsg = "[SemiAutoMode] CornerNum=0";
                        break;
                    case 24:
                        activity.NavServiceMsg = "[SemiAutoMode]Robot reachs destination!!";
                        break;
                    case 31:
                        activity.NavServiceMsg = "[AutoMode] Target walkable=" + msg.Data.GetString("Walkable"); 
                        break;
                    case 32:
                        activity.NavServiceMsg = "[AutoMode] Start auto navigation!!";
                        break;
                    case 33:
                        activity.NavServiceMsg = "[AutoMode] Auto navigation is done!!";
                        break;

                }
                
                
            }
        }
    }
}

