using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Trilateration_Android
{
    class class_EKFL5
    {
        //struct anchor
        //{
        //    public Single X;
        //    public Single Y;
        //    public Single Z;
        //    public Single Range;
        //}

        struct position
        {
            public Single X;
            public Single Y;
            public Single Theta;
        }

        // constant variables
        private int dim = 6;

        // private variables
        private int i, j, k;      
        private Single tmpSingle1;
        private position d_move;
        private Single dt;

        // matrix for calculation
        private Single[,] H, HT, Z, hX,Q,R,K;
        private Single[,] PoHT, HPoHTR, IHPoHTR,KdZ,KH,KHPo;
        private Single[,] P, Po;    // covariance
        private Single[,] Xk, Xke;    // state

        // required
        private Single v, w, compass;
        private bool dr_only;

        // output
        private position tag;
        private position tag_old;
        private string out_val1;

        public bool DRonly
        {
            set { dr_only = value; }
        }

        public Single Velocity
        {
            get { return v; }
            set { v = value; }
        }

        public Single Omega
        {
            get { return w; }
            set { w = value; }
        }

        public Single dT
        {
            get { return dt; }
            set { dt = value; }
        }

        public class_Anchors[] Anchor;

        public Single tagX
        {
            get { return tag.X; }
        }

        public Single tagY
        {
            get { return tag.Y; }
        }

        public Single Compass
        {
            set { compass=value; }
        }

        public String Outval
        {
            get { return out_val1; }
        }

        public class_EKFL5(Single x, Single y, Single theta,int anchor_num)
        {
            tag.X = x;
            tag.Y = y;
            tag.Theta = theta;
            tag_old = tag;
            dim = anchor_num;

            Anchor = new class_Anchors[dim];
            R = new Single[dim, dim];
            Q = new Single[2, 2]{{3f,0f},{0f,3f}};
            for (i = 0; i < dim; i++)
            {
                for (j = 0; j < dim; j++)
                {
                    if (i == j) R[i,j] = 20f;
                    else R[i,j] = 0f;
                }
            }
            Po = new Single[2, 2] { { 1, 0 }, { 0, 1 } };
            ResetVariables();

        }

        public void Calculation()
        {
            ResetVariables();
            if (float.IsNaN(tag.X) || float.IsNaN(tag.Y))
            {
                // Do nothing
            }
            else tag_old = tag;

            // state equation
            StateEquation(out d_move.X, out d_move.Y, out d_move.Theta);
            out_val1 = d_move.X.ToString() + "," + d_move.Y.ToString();
            Xke[0, 0] = tag_old.X + d_move.X;
            Xke[1, 0] = tag_old.Y + d_move.Y;
            Xke[2, 0] = tag_old.Theta + d_move.Theta;
            
            // calculate measurement Z, estimate h(X) and its Jocobian H
            for (i = 0; i < dim; i++)
            {
                Z[i,0] = (Single)Math.Sqrt(Math.Abs(Math.Pow(Anchor[i].Range, 2) - Math.Pow(Anchor[i].dZ, 2)));        // ranging value project to 2D plane
                if (Anchor[i].Range > 0)
                {
                    hX[i, 0] = (Single)Math.Sqrt(Math.Pow((tag_old.X - Anchor[i].X), 2) + Math.Pow((tag_old.Y - Anchor[i].Y), 2)+9);    // distance between previous tag and the anchor
                    H[i, 0] = (tag_old.X - Anchor[i].X) / hX[i, 0];
                    H[i, 1] = (tag_old.Y - Anchor[i].Y) / hX[i, 0];
                }
                else
                {
                    hX[i, 0] = 0;
                    H[i, 0] = 0;
                    H[i, 1] = 0;
                }
            }

            // transpose Jacobian matrix
            for (i = 0; i < dim; i++)
            {
                for (j = 0; j < 2; j++)
                {
                    HT[j,i] = H[i,j];
                }
            }

            // calculate Kalman gain K
            for (i = 0; i < 2; i++)
            {
                for (j = 0; j < dim; j++)
                {
                    PoHT[i,j] = 0;
                    for (k = 0; k < 2; k++)
                    {
                        PoHT[i,j] = PoHT[i,j] + (Po[i,k] * HT[k,j]);

                    }
                }
            }
            for (i = 0; i < dim; i++)
            {
                for (j = 0; j < dim; j++)
                {
                    HPoHTR[i,j] = 0;
                    for (k = 0; k < 2; k++)
                    {
                        HPoHTR[i,j] = HPoHTR[i,j] + (H[i,k] * PoHT[k,j]);
                    }
                    HPoHTR[i, j] = HPoHTR[i, j] + R[i, j];
                }
            }
            // calculate the inverse matrix
            Inverse(out IHPoHTR, HPoHTR, dim);

            for (i = 0; i < 2; i++)
            {
                for (j = 0; j < dim; j++)
                {
                    K[i,j] = 0;
                    for (k = 0; k < dim; k++)
                    {
                        K[i, j] = K[i, j] + (PoHT[i, k] * IHPoHTR[k, j]);
                    }
                }
            }

            // Update state with measurement and estimate
            for (i = 0; i < 2; i++)
            {
                KdZ[i,0] = 0;
                for (j = 0; j < dim; j++)
                {
                    KdZ[i, 0] = KdZ[i, 0] + (K[i, j] * (Z[j, 0] - hX[j, 0]));
                }
            }
            for (i = 0; i < 2; i++)
            {
                if (dr_only) Xk[i, 0] = Xke[i, 0];
                else Xk[i,0] = Xke[i,0] + KdZ[i,0];
            }
            Xk[2,0] = Xke[2,0];

            if (float.IsNaN(Xk[0, 0])) tag.X = tag_old.X;
            else tag.X = Xk[0, 0];
            if (float.IsNaN(Xk[1, 0])) tag.Y = tag_old.Y;
            else tag.Y = Xk[1, 0];
            tag.Theta = Xk[2, 0];

            // update Error Covariance
            if (!dr_only)
            {
                for (i = 0; i < 2; i++)
                {
                    for (j = 0; j < 2; j++)
                    {
                        for (k = 0; k < dim; k++)
                        {
                            KH[i, j] = KH[i, j] + (K[i, k] * H[k, j]);

                        }
                    }
                }
                for (i = 0; i < 2; i++)
                {
                    for (j = 0; j < 2; j++)
                    {
                        KHPo[i, j] = KHPo[i, j] + (KH[i, j] * Po[i, j]);
                    }
                }
                for (i = 0; i < 2; i++)
                {
                    for (j = 0; j < 2; j++)
                    {
                        P[i, j] = Po[i, j] - KHPo[i, j];
                    }
                }
                for (i = 0; i < 2; i++)
                {
                    for (j = 0; j < 2; j++)
                    {
                        Po[i, j] = P[i, j] + Q[i, j];
                    }
                }
            }

            //for (i = 0; i < dim; i++)
            //{
            //    out_val1 = out_val1 + K[0, i].ToString("f3") + ",";
            //}
            //for (i = 0; i < dim; i++)
            //{
            //    out_val1 = out_val1 + K[1, i].ToString("f3") + ",";
            //}
        }

        private void Inverse(out Single[,] Ans, Single[,] origin, int column)
        {
            int i, j, k, l;
            Single tmpSingle1, tmpSingle2;

            Single[,] whole = new Single[column, column * 2];
            Single[,] Iinv = new Single[column, column];

            Ans = new Single[column, column];

            for (i = 0; i < column; i++)
            {
                for (j = 0; j < column; j++)
                {
                    if (i == j) Iinv[i,j] = 1;
                    else Iinv[i,j] = 0;
                }
            }
            for (i = 0; i < column; i++)
            {
                for (j = 0; j < column; j++)
                {
                    whole[i,j] = origin[i,j];
                    whole[i, j + column] = Iinv[i, j];
                }
            }
            for (i = 0; i < column; i++)
            {
                tmpSingle1 = whole[i,i];
                for (j = 0; j < (2 * column); j++)
                {
                    whole[i,j] = whole[i,j] / tmpSingle1;
                }
                for (k = 0; k < column; k++)
                {
                    if (k != i)
                    {
                        tmpSingle2 = whole[k,i];
                        for (l = 0; l < (2 * column); l++)
                        {
                            whole[k,l] = -1 * (tmpSingle2 * whole[i,l]) + whole[k,l];
                        }
                    }
                }
            }

            for (i = 0; i < column; i++)
            {
                for (j = column; j < (2 * column); j++)
                {
                    Ans[i, j - column] = whole[i, j];    //get the answer matrix
                }
            }
        }

        private void StateEquation(out Single dx, out Single dy, out Single dtheta)
        {
            Single DegToRad =0.0175f;
            Single RadToDeg =57.296f;
            Single D = 11.83f;
            Single piD = (Single)(D * Math.PI);

            dtheta = compass - w * dt * RadToDeg;
            dx = (Single)((v * dt) * Math.Cos(dtheta * DegToRad));
            dy = (Single)((v * dt) * Math.Sin(dtheta * DegToRad));
        }

        private void ResetVariables()
        {
            H = new Single[dim, 2];     // Jacobian matrix
            HT = new Single[2, dim];    // Transpose Jacobina matrix
            Z = new Single[dim, 1];     // measurement value matrix
            hX = new Single[dim, 1];     // estimate value matrix
            PoHT = new Single[2, dim];
            HPoHTR = new Single[dim, dim];
            IHPoHTR = new Single[dim, dim];
            K = new Single[2, dim];     // Kalman gain
            KdZ = new Single[2, 1];
            Xk = new Single[3, 1];      // state
            Xke = new Single[3, 1];     // estimate state
            KH = new Single[2, 2] { { 1, 0 }, { 0, 1 } };
            KHPo = new Single[2, 2] { { 1, 0 }, { 0, 1 } };
            P = new Single[2, 2];       // covariance matrix
        }
    }
}
