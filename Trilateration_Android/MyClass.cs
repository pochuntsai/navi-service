#define G25_PIC32
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;

namespace Trilateration_Android
{
    public struct struct_config
    {
        public ushort MapWidth;
        public ushort MapHeight;
        public ushort GridWidth;
        public ushort GridHeight;
        public ushort MaxSpeed;
        public short MapEast;
    }

    public struct move_command
    {
        public int speed;
        public int turn;
        public Single var1;
        public Single var2;
        public Single var3;
        public bool arrived;
        public bool vehicle_done;
        public bool vehicle_turnning;
    }
    public class class_Vehicle
    {
        private Single p_compass;

        public Single compass
        {
            get { return p_compass; }
            set
            {
                if (value < -360) p_compass = value + 360;
                else if (value > 360) p_compass = value - 360;
                else p_compass = value;
            }
        }
        public short East;
        public byte[] sonic = new byte[7];
        public byte Bumper;
		public Single V;
		public Single W;
#if G25_PIC32
        //Brian+: Add new status info for G2.5
        public byte BatteryLevel;
        public byte Status;
        public byte Error;
#endif
        public class_Vehicle()
        {
            p_compass = 0f;
            East = 0;
        }
		
		public void UpdatedAll(byte[] Raw)
		{
			short tmpShort;

			tmpShort = (short)((Raw[1] << 8) + Raw[2]);
			tmpShort = (short)(tmpShort - East);
			if (tmpShort < -180) tmpShort = (short)(tmpShort + 360);
			else if (tmpShort > 180) tmpShort = (short)(tmpShort - 360);
			if (tmpShort < 360 && tmpShort > -360)
			{
                if(p_compass>-150 && p_compass<150)
                {
                    if((p_compass-tmpShort)>30)
                    {
                        p_compass = p_compass - 30;
                    }
                    else if((p_compass - tmpShort) < -30)
                    {
                        p_compass = p_compass + 30;
                    }
                    else p_compass = tmpShort;
                }
				else p_compass = tmpShort;
			}
            tmpShort = (short)((Raw[3] << 8) + Raw[4]);
            V = tmpShort / 100f;
            tmpShort = (short)((Raw[5] << 8) + Raw[6]);
            W = tmpShort;
			for (int i = 0; i < 7; i++)
			{
				sonic[i] = Raw[7 + i];
			}
			Bumper = Raw[14];
#if G25_PIC32            
            //Brian+ for parse new info for G2.5
            BatteryLevel = Raw[15];
            Status = Raw[16];
            Error = Raw[17];
            
#endif
		}
    }

    public struct struct_PointF
    {
        public Single X;
        public Single Y;
        public Single Theta;
    }

    //Brian+ for auto mode struct
    public struct struct_AutoTarget
    {
        public int X;
        public int Y;
        public int StopTime;
    }

    public struct struct_Location
    {
        public int X;
        public int Y;
        public string Note;
    }

    public class class_flag
    {
        public bool loc_init_done;
        public bool loc_on;
        public bool moving;
        public bool testing;
        public bool screen_ready;
        public int loc_init_step;

        public class_flag()
        {
            loc_init_done = false;
            loc_on = false;
            moving = false;
            testing = false;
            loc_init_step = 0;
        }
    }

    public class class_Anchors
    {
        private int p_id;
        private Single p_x, p_y, p_dz;
        private Single p_reliability=0;
        private Single p_meansquarederror;
        private Single p_range;

        public int ID { get { return p_id; } }

        public Single X { get { return p_x; } }
        public Single Y { get { return p_y; } }
        public Single dZ { get { return p_dz; } }

        public Single Measurement 
        { 
            set 
            {
                if (value > 1800) p_reliability = 0;
                else if (value < 100) p_reliability = 0;
                else p_reliability = 1;
                p_range = value; } }

        public Single Range
        {
            get { return p_range; }
            set { p_range = value; }
        }

        public Single Reliability { get { return p_reliability; } }

        public bool Chosen;

        public class_Anchors(int id, Single x, Single y, Single dz)
        {
            p_id = id;
            p_x = x;
            p_y = y;
            p_dz = dz;
            p_reliability = 0;
        }

    }

    public class beacon
    {
        private short rate;
        private Single p_range;

        //public bool Updated;
        //Toby's patch
        //public int PixelX;
        //public int PixelY;
        public Single X;
        public Single Y;
        public Single Z;
        public Single Range=0;
		public short RateSum=0;
        public short Rate
        {
            get { return rate; }
            set
            {
                if (value > 100) rate = 100;
                else if (value < 0) rate = 0;
                else rate = value;
            }
        }
        public double SampleTime;
        public string Message;
        public struct_PointF Avg = new struct_PointF();

        public bool SaveMeasure(int Measurement,Single MaxMove)
        {
            Single thisMove;

            if (Measurement <= 0) return false;

            if (RateSum <= 0)
            {
                p_range = (Single)Measurement;
            }
            else
            {
                MaxMove = MaxMove / RateSum;
                thisMove = (Single)(Measurement - Range);
                if (thisMove > MaxMove) p_range = Range + MaxMove;
                else if (thisMove < -1 * MaxMove) p_range = Range - MaxMove;
                else p_range = (Single)Measurement;
            }

            if (p_range <= 0)
            {
                return false;
            }
            else
            {
                Range = p_range;
                return true;
            }
        }
    }

}