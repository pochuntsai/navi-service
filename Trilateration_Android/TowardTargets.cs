
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Trilateration_Android;

namespace Trilateration
{
    // Declare a delegate event
    public delegate void ControlOutputEventHandler(object sender, EventArgs args);

    class TowardTargets
    {
        public enum e_status { None = 0x00, HasTask = 0x01, Initialized = 0x02, Moving = 0x03, HasObstacle = 0x04, Arrived = 0x08, Finish = 0x10,
            NoObstacle= 0xFB, NoMoving=0xFC};

        private static short max_speed = 95;
        private static short max_turn = 35;
        private static short wp_range = 60;
        private static short tg_range = 30;
        private static e_status status = e_status.None;
        private static short speed, turn;
        private static short range;
        private static struct_PointF[] target;
        private static obstacle ob = new obstacle(80, 0.1f);

        private static HighPerformanceCounter hpcounter1 = new HighPerformanceCounter();

        // Input
        public static struct_PointF Pose;
        public static bool PauseVehicle;
        public static bool PureMove;
        public static class_Vehicle Vehicle = new class_Vehicle();

        // Output
        public static string OutStr;
        public static int TargetTotal;
        public static int TargetNow;
        public static e_status Status
        {
            get {return status;}
            set { status = value; }
        }
        public static short OutSpeed
        {
            get { return speed; }
        }
        public static short OutTurn
        {
            get { return turn; }
        }

        // Declare event
        public static event ControlOutputEventHandler ControlEvent;

        public static void Start()
        {
            PauseVehicle = false;
            PureMove = false;
            Thread mainloop = new Thread(new ThreadStart(MainLoop));
            mainloop.IsBackground = true;
            mainloop.Start();
        }

        public static void Abort()
        {
            speed = 0;
            turn = 0;
            PauseVehicle = true;
            status = e_status.Finish;
        }

        /// <summary>
        /// To enable and proceed the calculation of moving toward a target
        /// </summary>
        /// <param name="x">The x of the target</param>
        /// <param name="y">The y of the target</param>
        /// <param name="theta">The bearings of the target</param>
        public static bool NewTask(struct_PointF[] task,int numbers)
        {
            if (numbers >= 1)
            {
                target = new struct_PointF[numbers + 1];
                for (int i = 0; i <= numbers; i++)
                {
                    target[i] = task[i];
                }
                    //Array.Copy(task, target, numbers);
                TargetTotal = numbers;
                TargetNow = 1;
                status = e_status.HasTask;
                PauseVehicle = false;
                if (TargetTotal == 1) range = tg_range;
                else range = wp_range;
                return true;
            }
            else return false;
        }

        private static void MainLoop()
        {
            Single[] k = new Single[2] { 0.8f, 1f };

            Single diff_dist;
            Single diff_angle;
            Single target_angle;
            Single robot_angle;
            Single tmpSingle1, tmpSingle2;
            Single total_dist=1;
            double Vcross, Vcross_old, d_Vcross;
            double Scross;
            double[] Vdot=new double[3];
            double deviation, deviation_old, d_deviation;
            double d_origin_path, d_current_path, d_gain;
            
            double tmpDouble1;
            double a, b;
            int tmpInt;
            struct_PointF Pose_old;
            struct_PointF Vt;
            struct_PointF Vr;
            short back_count = 0;
            short lock_count = 0;
            short hit_count = 0;

            status = e_status.None;
            Pose_old.X = Pose.X;
            Pose_old.Y = Pose.Y;
            deviation = 0;
            deviation_old = 0;
            Vcross_old = 0;
            while (true)
            {
                OutStr = "";
                hpcounter1.Start();

                #region Check status
                if (Vehicle.Bumper == 0xFF)
                {
                    hit_count++;
                    back_count = 20;
                }

                ob.save_sensor_reading(Vehicle.sonic);
                //if (ob.HasObstacle) status = status | e_status.HasObstacle;
                //else status = status & e_status.NoObstacle;
                #endregion

                #region Mode 1 : Force stop
                if (PauseVehicle) goto Wait;
                #endregion

                #region Mode 2 : Move forward
                if(PureMove)
                {
                    status = e_status.Moving;
                    if(back_count>0)
                    {
                        back_count--;
                        hit_count = 0;
                        speed = -30;
                        turn = 0;
                    }
                    else if(ob.Escaped)
                    {
                        speed = 80;
                        turn = 0;
                    }
                    else if(ob.HasObstacle)
                    {
                        ob.avoid(turn);
                        speed = (short)ob.OutSpeed;
                        turn = (short)ob.OutTurn;
                    }
                    else
                    {
                        speed = 80;
                        turn = 0;
                    }
                    if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                    goto Wait;
                }
                #endregion

                #region Mode 3 : Move to target
                if (status == e_status.HasTask)
                {
                    #region Get a new task, need to initial
                    hit_count = 0;
                    back_count = 0;
                    total_dist=(Single)Math.Sqrt((target[TargetNow].X - target[TargetNow-1].X) * (target[TargetNow].X - target[TargetNow-1].X) + (target[TargetNow].Y - target[TargetNow-1].Y) * (target[TargetNow].Y - target[TargetNow-1].Y));
                    status = status | e_status.Initialized;
                    MakeTurn(target[TargetNow].Theta);
                    MakeTurn(target[TargetNow].Theta);
                    ForwardOnly(1);
                    #endregion
                }
                if ((status & e_status.Moving) >0)
                {
                    #region diff_dist, diff_angle and check arrival
                    // calculate distance difference
                    diff_dist = (Single)Math.Sqrt((target[TargetNow].X - Pose.X) * (target[TargetNow].X - Pose.X) + (target[TargetNow].Y - Pose.Y) * (target[TargetNow].Y - Pose.Y));

                    // calculate angle difference
                    target_angle = (Single)(Math.Atan2((target[TargetNow].Y - Pose.Y), (target[TargetNow].X - Pose.X)) * 180f / 3.14f);
                    robot_angle = (Single)(Math.Atan2((Pose.Y - Pose_old.Y), (Pose.X - Pose_old.X)) * 180f / 3.14f);
                    diff_angle = target_angle - Pose.Theta;
                    if (diff_angle > 180) diff_angle = diff_angle - 360;
                    else if (diff_angle < -180) diff_angle = diff_angle + 360;

                    // check if arrived to the target
                    if (diff_dist < range)
                    {
                        if (TargetNow == TargetTotal) status = status | e_status.Arrived;
                        else
                        {
                            deviation = Dot2Line(target[TargetNow].X, target[TargetNow].Y, target[TargetNow + 1].X, target[TargetNow + 1].Y, Pose.X, Pose.Y);
                            if (deviation < 30) status = status | e_status.Arrived;
                        }
                    }

                    #endregion

                    if ((status & e_status.Arrived)>0)
                    {
                        #region actions while arrived

                        if (TargetNow == TargetTotal)
                        {
                            speed = 0;
                            turn = 0;
                            status = e_status.Finish;
                        }
                        else
                        {
                            status = e_status.Moving;
                            TargetNow++;
                            if (TargetNow == TargetTotal) range = tg_range;
                            else range = wp_range;
                            total_dist=(Single)Math.Sqrt((target[TargetNow].X - target[TargetNow-1].X) * (target[TargetNow].X - target[TargetNow-1].X) + (target[TargetNow].Y - target[TargetNow-1].Y) * (target[TargetNow].Y - target[TargetNow-1].Y));
                            MakeTurn2(target[TargetNow].Theta);
                        }
                        #endregion
                    }
                    else if (back_count > 0)
                    {
                        #region perform moving backward because has hit something
                        back_count--;
                        if (hit_count == 1)
                        {
                            if (back_count == 0)
                            {
                                hit_count++;
                            }
                            else
                            {
                                speed = -40;
                                turn = 0;
                            }
                        }
                        else
                        {
                            if (back_count == 1)
                            {
                                if (diff_angle >= 0) MakeTurn(Pose.Theta + 45);
                                else MakeTurn(Pose.Theta - 45);
                            }
                            else if (back_count == 0)
                            {
                                ForwardOnly(8);
                                hit_count = 0;
                            }
                            else
                            {
                                speed = -35;
                                turn = 0;
                            }
                        }
                        Pose_old.X = Pose.X;
                        Pose_old.Y = Pose.Y;
                        #endregion
                    }
                    else if(ob.Escaped)
                    {
                        speed = (short)(0.5 * max_speed);
                        turn = 0;
                    }
                    else if (ob.HasObstacle)
                    {
                        lock_count = 50;
                        ob.avoid(turn);
                        speed = (short)ob.OutSpeed;
                        turn = (short)ob.OutTurn;
                        Pose_old.X = Pose.X;
                        Pose_old.Y = Pose.Y;
                        OutStr = "avoiding";
                    }
                    else if(ob.InCorridor)
                    {
                        ob.KeepStraight(turn);
                        turn = (short)ob.OutTurn;
                        Pose_old.X = Pose.X;
                        Pose_old.Y = Pose.Y;
                        OutStr = ob.OutStr+","+turn.ToString();
                    }
                    else
                    {
                        if (lock_count > 0) lock_count--;

                        #region determine the speed of the vehicle (for reference)
                        if (diff_dist < 200)    // if pretty close to the target
                        {
                            speed = (short)(40 + diff_dist * (max_speed - 40) / 200f);
							if(speed < 15) speed = 15;
                            max_turn = 90;

                            if (TargetNow == TargetTotal)
                            {
                                if (lock_count == 0 && (diff_angle > 30 || diff_angle < -30))
                                {
                                    Thread.Sleep(100);
                                    MakeTurn((Single)(Math.Atan2((target[TargetTotal].Y - Pose.Y), (target[TargetTotal].X - Pose.X)) * 180f / 3.14f));
                                    Pose_old.X = Pose.X;
                                    Pose_old.Y = Pose.Y;
                                }
                            }
                        }
                        else                        // ordinary situation
                        {
                            speed = max_speed;
                            max_turn = 35;
                        }
                        #endregion

                        #region determine the turn of the vehicle
                        tmpSingle1 = (Single)Math.Sqrt((Pose.X - Pose_old.X) * (Pose.X - Pose_old.X) + (Pose.Y - Pose_old.Y) * (Pose.Y - Pose_old.Y));
                        if (tmpSingle1 < 5)
                        {
                            // distance is too short, keep going but reduce turn angle
                            tmpSingle2 = turn * 0.8f;
                            turn = (short)tmpSingle2;
                        }
                        else
                        {
                            // calculate deviation between the vehicle and the given path
                            // calculate the cross of the two vectors 
                            Vr.X = Pose.X - target[TargetNow - 1].X;
                            Vr.Y = Pose.Y - target[TargetNow - 1].Y;
                            Vt.X = target[TargetNow].X - target[TargetNow - 1].X;
                            Vt.Y = target[TargetNow].Y - target[TargetNow - 1].Y;
                            Vcross = Vr.X * Vt.Y - Vr.Y * Vt.X;
                            if ((target[TargetNow].X - target[TargetNow - 1].X) > -5 && (target[TargetNow].X - target[TargetNow-1].X) < 5)
                            {
                                deviation = Math.Abs(Pose.X - target[TargetNow].X);
                            }
                            else
                            {
                                a = (target[TargetNow].Y - target[TargetNow - 1].Y) / (target[TargetNow-1].X - target[TargetNow].X);
                                deviation = Math.Abs(a * (Pose.X - target[TargetNow - 1].X) + (Pose.Y - target[TargetNow - 1].Y)) / Math.Sqrt(a * a + 1);
                            }
                            if (Vcross < 0) deviation = 0f - deviation;
                            d_deviation = deviation - deviation_old;
                            //OutStr = Pose.X.ToString()+","+ target[TargetNow - 1].X.ToString()+","+ deviation.ToString("f1");
                            d_origin_path = deviation * k[0] + d_deviation * k[1];
                            deviation_old = deviation;

                            // calculate deviation between the vehicle and the current path
                            // calculate the cross of the two vectors 
                            Vr.X = Pose.X - Pose_old.X;
                            Vr.Y = Pose.Y - Pose_old.Y;
                            Vt.X = target[TargetNow].X - Pose.X;
                            Vt.Y = target[TargetNow].Y - Pose.Y;
                            Scross = Math.Sqrt(Vr.X * Vr.X + Vr.Y * Vr.Y) * Math.Sqrt(Vt.X * Vt.X + Vt.Y * Vt.Y);
                            Vcross = Vr.X * Vt.Y - Vr.Y * Vt.X;
                            Vcross = 15f*Vcross / Scross;
                            //OutStr = OutStr + "," + Vcross.ToString("f1");
                            d_Vcross = Vcross - Vcross_old;
                            d_current_path = Vcross * k[0] + d_Vcross * k[1];
                            Vcross_old = Vcross;
                            for (int i = Vdot.Length - 1; i >= 1; i--)
                            {
                                Vdot[i] = Vdot[i - 1];
                            }
                            Vdot[0] = Vr.X * Vt.X + Vr.Y * Vt.Y;
                            
                            // combine 2 deviations
                            d_gain = 0.1 * diff_dist / total_dist;
                            //OutStr = d_origin_path.ToString("f1") + "," + d_current_path.ToString("f1") ;
                            //d_gain = 0.3;
                            tmpDouble1 = d_gain * d_origin_path + (1 - d_gain) * d_current_path;

                            if (Vdot[0] < 0 && Vdot[1] < 0)
                            {
                                Vdot[0] = 0;
                                MakeTurn2(diff_angle + Pose.Theta);
                            }
                            else
                            {
                                // calculate turn
                                //turn = (short)(tmpDouble1 + turn);
                                turn = (short)tmpDouble1;
                                if (turn > max_turn) turn = (short)max_turn;
                                else if (turn < max_turn * -1) turn = (short)(max_turn * -1);

                                if (turn > 0) turn = (short)(turn + 5);
                                else if (turn < 0) turn = (short)(turn - 5);
                            }
                       
                            Pose_old.X = Pose.X;
                            Pose_old.Y = Pose.Y;
                        }
                        #endregion

                    }
                    if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                    goto Wait;

                }
                #endregion

            Wait:
                hpcounter1.Stop();
                tmpInt = (int)(hpcounter1.Duration * 1000f);
                if (tmpInt < 90)
                {
                    Thread.Sleep(90 - tmpInt);
                }

            }
        }

        private static void MakeTurn(Single TargetAngle)
        {
            Single diff_angle=10;

            while ((diff_angle > 5 || diff_angle < -5)&& !PauseVehicle)
            {
                diff_angle = TargetAngle - Pose.Theta;
                if (diff_angle > 180) diff_angle = diff_angle - 360;
                else if (diff_angle < -180) diff_angle = diff_angle + 360;

                turn = (short)diff_angle;
                if (turn > 0 && turn < 15) turn = 15;
                else if (turn < 0 && turn > -15) turn = -15;
                speed = 0;

                if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                Thread.Sleep(100);

            }
        }

        private static void MakeTurn2(Single TargetAngle)
        {
            Single diff_angle = 20;

            while ((diff_angle > 10 || diff_angle < -10) && !PauseVehicle)
            {
                diff_angle = TargetAngle - Pose.Theta;
                if (diff_angle > 180) diff_angle = diff_angle - 360;
                else if (diff_angle < -180) diff_angle = diff_angle + 360;

                if (diff_angle > 0) turn = 100;
                else turn = -100;
                speed = 30;

                if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                Thread.Sleep(100);
            }
        }

        private static void ForwardOnly(int cycle)
        {
            if (cycle == 0) return;

            for (int i = 0; i < cycle; i++)
            {
                speed = 80;
                turn = 0;
                if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                Thread.Sleep(100);
            }
        }

        private static double Dot2Line(Single LineX1, Single LineY1, Single LineX2, Single LineY2, Single DotX, Single DotY)
        {
            double a, b, d;

            if ((LineX1 - LineX2) > -1 && (LineX1 - LineX2) < 1)
            {
                a = 0;
                b = LineY1;
            }
            else
            {
                a = (LineY1 - LineY2) / (LineX1 - LineX2);
                b = (LineY1 * LineX2 - LineY2 * LineX1) / (LineX2 - LineX1);
            }
            d = Math.Abs(a * DotX - DotY + b) / Math.Sqrt(a * a + 1);
            return d;
        }
    }
}
