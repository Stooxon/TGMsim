﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGMsim
{
    class M_Garbo : Mode
    {

        public M_Garbo()
        {
            ModeName = "GARBAGE CLEAR";
            border = Color.DarkGreen;
            limitType = 4;
            limit = 1;
            drawSec = false;
            startWithRandField = true;
            hasCredits = false;
            delayTable.Add(new List<int> { 30 });
            delayTable.Add(new List<int> { 30 });
            delayTable.Add(new List<int> { 16 });
            delayTable.Add(new List<int> { 30 });
            delayTable.Add(new List<int> { 41 });
        }

        public override void onClear(int lines, Tetromino tet, long time, bool bravo)
        {
            level += lines;
            if(bravo)
            {
                level = endLevel;
                inCredits = true;
            }
        }

        public override void updateMusic()
        {
            if (Audio.song != "Casual 1")
                Audio.playMusic("Casual 1");
        }
    }
}
