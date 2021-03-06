﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Android.Graphics;

namespace Trilateration
{
    partial class Map
    {
        private Bitmap rawMap;
        private string rawFileName;
        private Single pxl_per_grid_w;
        private Single pxl_per_grid_h;
        private ushort width;
        private ushort height;

        /// <summary>
        /// Read only, width of the map in pixel
        /// </summary>
        public ushort Width
        { get { return width; } }

        /// <summary>
        /// Read only, height of the map in pixel
        /// </summary>
        public ushort Height
        { get { return height; } }

        public ushort Grid_W;
        public ushort Grid_H;
        public short East;
        public short[,] Walkability;

        /// <summary>
        /// Before using other functions of this class,
        /// have to load an existed map file.
        /// </summary>
        /// <param name="ff">path and name of the map, without the extension</param>
        public bool LoadFile(string ff)
        {
            rawFileName = ff;
            string filename = rawFileName + ".bmp";
            if (!File.Exists(filename)) return false;
            else
            {
                // save map data into memory, read width and height of the map
                rawMap=BitmapFactory.DecodeFile(filename);
                //rawMap = new Bitmap(filename);
                width = (ushort)rawMap.Width;
                height = (ushort)rawMap.Height;

                return true;
            }
        }

        /// <summary>
        /// After LoadFile, have to preprocess data to mark walkable pixels/grids
        /// </summary>
        public bool Preprocess()
        {
            int safety_x = 3;   // width of safety range        
            int safety_y = 3;   // height of safety range
            int Psafety_x = (int)Math.Round((double)safety_x/2);   // width of safety range        
            int Psafety_y = (int)Math.Round((double)safety_y/2);   // height of safety range
            bool restore_new_map = true;    // if need to restore the walkable map

            int i, j;
            int range_x, range_y, index_x, index_y;

            if (width * height == 0) return false;
            if (Grid_W * Grid_H == 0) return false;
            if (rawMap.Height == 0) return false;

            Walkability = new short[Grid_W, Grid_H];
            pxl_per_grid_w = (Single)width / Grid_W;
            pxl_per_grid_h = (Single)height / Grid_H;

            for (i = 0; i < Grid_W; i++)
            {
                for (j = 0; j < Grid_H; j++)
                {
                    //rawMap.GetPixel
                    //Color pixelColor = rawMap.GetPixel((int)(i * pxl_per_grid_w), (int)(j * pxl_per_grid_h));
                    int pixel = rawMap.GetPixel((int)(i * pxl_per_grid_w), (int)(j * pxl_per_grid_h));
                    Color pixelColor = new Color(pixel);
                    if ((pixelColor.R != 255) && (pixelColor.G != 255) && (pixelColor.B != 255))
                    {
                        Walkability[i, j] = 1; // if the color is not while, then cannot walk (=1)
                    }
                    else
                    {
                        Walkability[i, j] = 0;
                    }
                }
            }

            for (i = 0; i < Grid_W; i++)
            {
                for (j = 0; j < Grid_H; j++)
                {
                    // search every walkable grid in the map
                    if (Walkability[i, j] == 0)
                    {
                        // make a safety range for every walkable grid
                        for (range_x = safety_x * -1; range_x <= safety_x; range_x++)
                        {
                            for (range_y = safety_y * -1; range_y <= safety_y; range_y++)
                            {
                                index_x = i + range_x;
                                if (index_x < 0) index_x = 0;
                                if (index_x >= Grid_W) index_x = Grid_W-1;
                                index_y = j + range_y;
                                if (index_y < 0) index_y = 0;
                                if (index_y >= Grid_H) index_y = Grid_H-1;

                                // if any obstacle in the safety range, reset this grid as another type of walkability
                                if (Walkability[index_x, index_y] == 1) Walkability[i, j] = 2;
                            }
                        }
                    }
                    
                }
            }

            for (i = 0; i < Grid_W; i++)
            {
                for (j = 0; j < Grid_H; j++)
                {
                    // search every walkable grid in the map
                    if (Walkability[i, j] == 1)
                    {
                        // make a safety range for every walkable grid
                        for (range_x = Psafety_x * -1; range_x <= Psafety_x; range_x++)
                        {
                            for (range_y = Psafety_y * -1; range_y <= Psafety_y; range_y++)
                            {
                                index_x = i + range_x;
                                if (index_x < 0) index_x = 0;
                                if (index_x >= Grid_W) index_x = Grid_W - 1;
                                index_y = j + range_y;
                                if (index_y < 0) index_y = 0;
                                if (index_y >= Grid_H) index_y = Grid_H - 1;

                                // if any obstacle in the safety range, reset this grid as another type of walkability
                                if (Walkability[index_x, index_y] == 2) Walkability[i, j] = 3;
                            }
                        }
                    }

                }
            }
            if (restore_new_map)
            {
                Bitmap newmap=Bitmap.CreateBitmap(Convert.ToInt32( Grid_W),Convert.ToInt32(Grid_H), Bitmap.Config.Argb8888);
                //Bitmap newmap = new Bitmap(Grid_W, Grid_H);

                for (i = 0; i < Grid_W; i++)
                {
                    for (j = 0; j < Grid_H; j++)
                    {
                        if (Walkability[i, j] == 1) newmap.SetPixel(i, j, Color.Black);
                        if (Walkability[i, j] == 2) newmap.SetPixel(i, j, Color.Gray);
                        if (Walkability[i, j] == 3) newmap.SetPixel(i, j, Color.Green);
                    }
                }
               //newmap.Save(rawFileName + "_e.bmp");
                //if(File.Exists(rawFileName + "_e.bmp"))

                using (FileStream stream = new FileStream(rawFileName + "_e.bmp", FileMode.Create))
                {
                    newmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
                }
            }

			A_star();
            return true;
        }

        /// <summary>
        /// Input the pixel of interest, output the corresponding walkability
        /// </summary>
        public short CheckWalk(int grid_x, int grid_y)
        {
            if (grid_x >= Grid_W) return 1;
            if (grid_y >= Grid_H) return 1;

            return Walkability[grid_x, grid_y];
        }
    }
}
