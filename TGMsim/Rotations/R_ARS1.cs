﻿using System.Collections.Generic;

namespace TGMsim
{
    class R_ARS1 : Rotation
    {
        public R_ARS1()
        {
            type = "ARS 1+2";
        }

        public override Tetromino rotate(Tetromino tet, int p, List<List<int>> gameField, int rule, bool large, bool spawn)
        {

            Tetromino testTet = tet.clone((tet.rotation + p + 4)%4);

            int bigOffset = 1;
            if (large)
                bigOffset = 2;

            if (!spawn && testRestrict(tet, p, gameField, large))//test kick restrictions
            {
                for (int i = 1; i < 3; i++)//test wallkicks in order, stop at first rotation that works
                {
                    if (!checkUnder(testTet, gameField, large))
                    {
                        if (i != 2)
                            testTet.move(1*bigOffset, 0);
                        else
                            testTet.move(-2*bigOffset, 0);
                    }
                }
            }
            if (!checkUnder(testTet, gameField, large)) //did any kick of the rotation work?
                return tet;

            return testTet;
        }

        protected bool testRestrict(Tetromino tet, int p, List<List<int>> gameField, bool large)
        {
            int lowY = 22;
            int big = 2;
            if (large)
                big = 1;
            for (int q = 0; q < tet.bits.Count; q++)
            {
                if (tet.bits[q].y < lowY)
                    lowY = tet.bits[q].y;
            }

            if (tet.id == Tetromino.Piece.I)//I doesn't kick
                return false;


            //universal center-testing (two up from bottom center will never kick, even when considering the exception below)
            if (tet.bits[1].x > -1 && tet.bits[1].y + ((1 + (tet.rotation / 2)) * (3 - big)) < 22)
                if (gameField[tet.bits[1].x][tet.bits[1].y + ((1 + (tet.rotation / 2)) * (3 - big))] != 0)
                    return false;

            if (tet.id == Tetromino.Piece.J || tet.id == Tetromino.Piece.L)
            {
                if (tet.rotation % 2 == 0)
                {
                    if (gameField[tet.bits[1].x][tet.bits[1].y - 1 + (tet.rotation / 2 * (3 - big)) * 2] != 0)//if hooked
                    {
                        if (gameField[tet.bits[1].x + (((int)tet.id - 4) * -2) + 1][tet.bits[1].y + (tet.rotation / 2) + 1] != 0 && tet.rotation - (((((int)tet.id - 4) * 2) - 1) * p) == 1)//if exception blocked
                            return true;
                        return false;
                    }
                }
            }
            return true;
        }
        
    }
}