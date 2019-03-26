﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.IO;
using System.Drawing.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Globalization;

namespace TGMsim
{
    public partial class Form1 : Form
    {

        GameTimer timer = new GameTimer();
        double FPS = 60.00; //59.84 for TGM1, 61.68 for TGM2, 60.00 for TGM3
        long startTime;
        long interval;

        Controller pad1 = new Controller();
        GameRules rules = new GameRules();

        Image imgBuffer;
        Graphics graphics, drawBuffer;

        Profile player;
        Preferences prefs;

        string fileName;

        PrivateFontCollection fonts = new PrivateFontCollection();
        Font f_Maestro;

        int menuState = 0; //title, login, game select, mode select, ingame, results, hiscore roll, custom game, settings

        Field field1;
        Login login;
        GameSelect gSel;
        ModeSelect mSel;
        CheatMenu cMen;

        List<GameResult> hiscoreTable = new List<GameResult>();
        bool saved;
        string repNam;
        
        NAudio.Wave.WaveOutEvent songPlayer = new NAudio.Wave.WaveOutEvent();
        
        System.Windows.Media.MediaPlayer s_Start = new System.Windows.Media.MediaPlayer();
        System.Windows.Media.MediaPlayer s_Login = new System.Windows.Media.MediaPlayer();
        System.Windows.Media.MediaPlayer s_GSel = new System.Windows.Media.MediaPlayer();

        public Form1()
        {
            InitializeComponent();

            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;

            this.ClientSize = new Size(800, 600);//Size(1280, 780);
            this.Icon = new Icon(@"Res/GFX/fundoshi.ico");

            //interval = (long)TimeSpan.FromSeconds(1.0 / FPS).TotalMilliseconds;
            fonts.AddFontFile(@"Res\Maestro.ttf");
            FontFamily fontFam = fonts.Families[0];
            f_Maestro = new System.Drawing.Font(fontFam, 16, GraphicsUnit.Pixel);

            imgBuffer = (Image)new Bitmap(this.ClientSize.Width, this.ClientSize.Height);

            // hiscoreTable.Add(new List<GameResult>());

            cMen = new CheatMenu();
            player = new Profile();
            prefs = new Preferences(player, pad1);

            readPrefs();

            graphics = this.CreateGraphics();
            drawBuffer = Graphics.FromImage(imgBuffer);


            Audio.addSound(s_Start, "/Res/Audio/SE/SEI_class.wav");
            Audio.addSound(s_Login, "/Res/Audio/SE/SEI_data_ok.wav");
            Audio.addSound(s_GSel, "/Res/Audio/SE/SEI_mode_ok.wav");


            Audio.playMusic("Hello Again");
            
        }

        public void gameLoop()
        {
            timer.start();

            int cycle = 1;

            while (this.Created)
            {
                startTime = timer.elapsedTime;

                gameLogic();
                gameRender();

                interval =  16 + (cycle % 1);
                cycle++;
                if (cycle == 4) cycle = 1;

                Application.DoEvents();
                while (timer.elapsedTime - startTime < interval);
            }
        }

        void changeMenu(int newMenu)
        {

            switch (newMenu) //activate the new menu
            {  //title, login, game select, mode select, ingame, results, hiscore roll, custom game, settings, cheats
                case 0:
                    menuState = 0;
                    FPS = 60.00;
                    break;
                case 1:
                    menuState = 1;
                    login = new Login();
                    break;
                case 2:
                    if (menuState > 3 && menuState != 8)
                    {
                        Audio.stopMusic();
                        Audio.playMusic("Hello Again");
                    }
                    menuState = 2;
                    FPS = 60.00;
                    pad1.setLag(0);
                    if (gSel == null)
                        gSel = new GameSelect();
                    break;
                case 3:
                    Audio.playSound(s_GSel);
                    menuState = 3;
                    //if (gSel.menuSelection != 5 && gSel.menuSelection != 0 && gSel.menuSelection != 7)
                        //loadHiscores(gSel.menuSelection);
                    mSel = new ModeSelect(gSel.menuSelection);
                    break;
                case 4:
                    saved = false;
                    menuState = 4;
                    setupGame();
                    break;

                case 6: //hiscores
                    menuState = 6;
                    loadHiscores(gSel.menuSelection, mSel.selection+1);
                    //Audio.stopMusic();
                    //Audio.playMusic("Hiscores");
                    break;

                case 8:
                    menuState = 8;
                    readPrefs();
                    break;
                case 9:
                    menuState = 9;
                    break;
            }

            menuState = newMenu;
        }

        void gameLogic()
        {
            pad1.poll();

            //deal with game logic
            switch (menuState)
            {
                case 0: //title
                    titleLogic();
                    break;
                case 1: //login
                    loginLogic();
                    break;
                case 2: //game select
                    gSel.logic(pad1);
                    if ((pad1.inputRot1 | pad1.inputRot3) == 1)
                    {
                        if (gSel.prompt)
                        {
                            if (gSel.pSel == 1)
                            {
                                cMen = new CheatMenu();
                                player.name = "   ";
                                gSel.prompt = false;
                                changeMenu(1);
                            }
                            else
                            {
                                gSel.prompt = false;
                            }
                        }
                        else
                        {
                            if (gSel.menuSelection == 8) //settings
                                changeMenu(8);
                            else //chose a game, let's choose a mode
                            {
                                changeMenu(3);
                            }
                        }
                    }
                    else
                        if (pad1.inputRot2 == 1)
                    {
                        if (gSel.prompt)
                            gSel.prompt = false;
                        else
                        {
                            gSel.pSel = 0;
                            gSel.prompt = true;
                        }
                    }
                    else if (pad1.inputHold == 1)
                    {
                        if (queryReplay())
                        {
                            loadReplay();
                            saved = false;
                            menuState = 4;
                        }
                    }
                    break;
                case 3: //mode select
                    mSel.logic(pad1);
                    if ((pad1.inputRot1 | pad1.inputRot3) == 1)
                    {
                        if (!mSel.modes[mSel.game][mSel.selection].enabled)
                            return;
                        else if (mSel.game == 7 && mSel.selection == 0) //custom
                            changeMenu(7);
                        else
                            changeMenu(4); //load mode
                    }
                    if (pad1.inputRot2 == 1)
                        changeMenu(2);
                    if (pad1.inputHold == 1)
                        changeMenu(6);//hiscores for mode, TODO: make this part of mode select
                    break;
                case 4: //ingame
                    field1.logic();
                    if (field1.gameRunning == false)
                    {
                        //test and save a hiscore ONCE
                        if (saved == false && field1.isPlayback == false)
                        {
                            field1.results.username = player.name;
                            if (rules.gameRules == GameRules.Games.TGM3 && field1.MOD.ModeName == "MASTER" && player.name != "   ")
                            {
                                //add result to history
                                for (int i = 0; i < 6; i++)
                                {
                                    player.TIHistory[i] = player.TIHistory[i + 1];
                                }
                                player.TIHistory[6] = field1.results.grade;

                                //if exam, check results for promotion
                                if (field1.ruleset.exam != -1)
                                {
                                    if (field1.results.grade >= field1.ruleset.exam)
                                    {
                                        player.TIGrade = field1.ruleset.exam;
                                    }
                                }

                                //scale GM back if unqualified
                                else if (field1.results.grade == 32 && player.TIGrade != 32)
                                {
                                    field1.results.grade = 31;
                                }
                            }

                            if (!field1.cheating && field1.ruleset.exam == -1)
                                field1.newHiscore = testHiscore(field1.results);
                            if (player.name != "   ")
                                player.updateUser();
                            if (field1.MOD.modeID == Mode.ModeType.DYNAMO && field1.MOD.level == 999 && player.dynamoProgress == field1.MOD.variant && player.dynamoProgress != 4)
                                player.dynamoProgress = field1.MOD.variant + 1;
                            saved = true;
                        }
                        if(field1.isPlayback)
                        {
                            field1.results.username = repNam;
                        }

                    }
                    if (field1.cont == true)
                    {
                        field1.cont = false;
                        if (field1.isPlayback)
                            loadReplay();
                        else
                            setupGame();
                    }
                    if (field1.exit == true)
                        changeMenu(2);
                    if (field1.record == true)
                        if (player.name != "   ")
                            writeReplay();
                        else
                            field1.record = false;
                    break;
                case 6://hiscores
                    if (pad1.inputPressedRot2)
                        changeMenu(3);
                    break;
                case 7://custom game
                    if (pad1.inputPressedRot2)
                        changeMenu(2);
                    break;
                case 8://settings
                    if (pad1.inputRot2 == 1 && prefs.menuState == 0)
                    {
                        pad1 = prefs.nPad;
                        savePrefs();
                        changeMenu(2);
                    }
                    prefs.logic();
                    break;
                case 9://cheats
                    if (pad1.inputStart == 1)
                        changeMenu(2);
                    cMen.logic(pad1);
                    break;
            }
        }

        private void loginLogic()
        {
            if (player.name == "   ")
            {

                login.logic(pad1);

                if (login.loggedin)
                {
                    pad1.inputStart = 0;
                    player = login.temp;
                    Audio.playSound(s_Login);
                    savePrefs();
                    if (player.name == "   ")
                        changeMenu(9);
                    else
                        changeMenu(2);
                }
            } else {
                if (player.readUserData())
                    changeMenu(2);
                else player.name = "   ";
            }
        }

        private void titleLogic()
        {
            if (pad1.inputStart == 1 && Audio.loadedTally == Audio.loadedWaiting)
            {
                pad1.inputStart = 0;
                Audio.playSound(s_Start);
                changeMenu(1);
            }
        }

        void gameRender()
        {
            //draw temp BG so bleeding doesn't occur
            drawBuffer.FillRectangle(new SolidBrush(Color.Black), this.ClientRectangle);


            switch (menuState)
            {
                case 0:
                    drawBuffer.DrawString("TGM sim title screen thingy", DefaultFont, new SolidBrush(Color.White), 325, 250);
                    if( Audio.loadedTally < Audio.loadedWaiting)
                        drawBuffer.DrawString("LOADING", f_Maestro, new SolidBrush(Color.White), 360, 500);
                    else
                        drawBuffer.DrawString("PRESS START", f_Maestro, new SolidBrush(Color.White), 350, 500);
                    break;
                case 1:
                    drawBuffer.DrawString("login", DefaultFont, new SolidBrush(Color.White), 5, 5);
                    login.render(drawBuffer);
                    break;
                case 2:
                    drawBuffer.DrawString("game select", DefaultFont, new SolidBrush(Color.White), 5, 5);
                    gSel.render(drawBuffer);
                    break;
                case 3:
                    drawBuffer.DrawString("mode select", DefaultFont, new SolidBrush(Color.White), 5, 5);
                    mSel.render(drawBuffer);
                    break;
                case 4:
                    field1.draw(drawBuffer);
                    break;
                case 6:
                    drawBuffer.DrawString("hiscores", DefaultFont, new SolidBrush(Color.White), 5, 5);
                    int gam = mSel.game - 1;
                    for (int i = 0; i < 6; i++ )
                    {
                        if (hiscoreTable[i].lineC == 1)
                        {
                            drawBuffer.DrawLine(new Pen(Color.LimeGreen), 240, 111 + 30 * i, 550, 111 + 30 * i);
                        }
                        if (hiscoreTable[i].lineC == 2)
                        {
                            drawBuffer.DrawLine(new Pen(Color.Orange), 240, 111 + 30 * i, 550, 111 + 30 * i);
                        }
                        drawBuffer.DrawString(hiscoreTable[i].username, DefaultFont, new SolidBrush(Color.White), 250, 100 + 30 * i);
                        drawBuffer.DrawString(hiscoreTable[i].level.ToString(), DefaultFont, new SolidBrush(Color.White), 290, 100 + 30 * i);
                        drawBuffer.DrawString(rules.grades[hiscoreTable[i].grade], DefaultFont, new SolidBrush(Color.White), 330, 100 + 30 * i);
                        var temptimeVAR = hiscoreTable[i].time;
                        var min = (int)Math.Floor((double)temptimeVAR / 60000);
                        temptimeVAR -= min * 60000;
                        var sec = (int)Math.Floor((double)temptimeVAR / 1000);
                        temptimeVAR -= sec * 1000;
                        var msec = (int)temptimeVAR;
                        var msec10 = (int)(msec / 10);
                        drawBuffer.DrawString(string.Format("{0,2:00}:{1,2:00}:{2,2:00}", min, sec, msec10), DefaultFont, new SolidBrush(Color.White), 360, 100 + 30 * i);
                        if (gam != 0)
                        {
                            for (int j = 0; j < 6; j++)
                            {
                                drawBuffer.DrawString(hiscoreTable[i].medals[j].ToString(), DefaultFont, new SolidBrush(Color.White), 420 + 10*j, 100 + 30 * i);
                            }
                        }
                    }

                        break;
                case 8:
                    drawBuffer.DrawString("preferences", DefaultFont, new SolidBrush(Color.White), 5, 5);
                    prefs.render(drawBuffer);
                    break;
                case 9:
                    drawBuffer.DrawString("cheats", DefaultFont, new SolidBrush(Color.White), 5, 5);
                    cMen.render(drawBuffer);
                    break;
            }
            if (menuState > 1)
                drawBuffer.DrawString(player.name, f_Maestro, new SolidBrush(Color.White), 765, 5);

#if DEBUG
            SolidBrush debugBrush = new SolidBrush(Color.White);
            //denote debug
            drawBuffer.DrawString("DEBUG", DefaultFont, debugBrush, 20, 710);
            //draw the current inputs
            drawBuffer.DrawString(Keyboard.IsKeyDown(pad1.keyUp) ? "1" : "0", DefaultFont, debugBrush, 28, 720);
            drawBuffer.DrawString(Keyboard.IsKeyDown(pad1.keyLeft) ? "1" : "0", DefaultFont, debugBrush, 20, 730);
            drawBuffer.DrawString(Keyboard.IsKeyDown(pad1.keyDown) ? "1" : "0", DefaultFont, debugBrush, 28, 740);
            drawBuffer.DrawString(Keyboard.IsKeyDown(pad1.keyRight) ? "1" : "0", DefaultFont, debugBrush, 36, 730);
            drawBuffer.DrawString(Keyboard.IsKeyDown(pad1.keyRot1) ? "1" : "0", DefaultFont, debugBrush, 50, 720);
            drawBuffer.DrawString(Keyboard.IsKeyDown(pad1.keyRot2) ? "1" : "0", DefaultFont, debugBrush, 58, 720);
            drawBuffer.DrawString(Keyboard.IsKeyDown(pad1.keyRot3) ? "1" : "0", DefaultFont, debugBrush, 66, 720);
            drawBuffer.DrawString(Keyboard.IsKeyDown(pad1.keyHold) ? "1" : "0", DefaultFont, debugBrush, 50, 730);
            drawBuffer.DrawString(Keyboard.IsKeyDown(pad1.keyStart) ? "1" : "0", DefaultFont, debugBrush, 74, 730);

#endif

            //draw the buffer, then set to refresh
            this.BackgroundImage = imgBuffer;
            this.Invalidate();

        }

        private int checkExam()
        {
            List<int> cream = new List<int>(player.TIHistory);

            //cream = player.TIHistory;
            cream.Sort();

            int avg = (cream[5] + cream[4] + cream[3])/3;

            Random rand = new Random();
            if (avg > player.TIGrade && rand.Next(2) == 0)
                return avg;
            return -1;
            
        }

        private void setupGame()
        {
            Audio.stopMusic();
            rules = new GameRules();
            rules.setup((GameRules.Games)mSel.game, mSel.modes[mSel.game][mSel.selection].id, mSel.variant);

            //m.mute = prefs.muted;

            if (player.name == "   ")
            {
                if (!rules.mod.bigmode)
                    rules.mod.bigmode = cMen.cheats[3];
                Audio.muted = cMen.cheats[4];
            }
            else if (mSel.game == 4 && mSel.selection == 0)
                rules.exam = checkExam();

            if (prefs.delay)
                pad1.setLag(rules.lag);
            FPS = rules.FPS;

            field1 = new Field(pad1, rules, -1);
            if (player.name == "   ")
            {
                field1.godmode = cMen.cheats[0];
                if (cMen.cheats[1])
                    field1.g20 = cMen.cheats[1];
                field1.g0 = cMen.cheats[2];
                if (field1.godmode || field1.g0)
                    field1.cheating = true;
                if (cMen.cheats[5])
                    field1.w4 = true;
                if (cMen.cheats[6])
                    field1.toriless = true;
            }
            saved = false;
        }

        private bool testHiscore(GameResult gameResult)
        {
            int m = mSel.selection + 1;
            loadHiscores(gameResult.game, m);
            if (hiscoreTable.Count == 0) //oh no! error reading the file!
                return false;

            switch (gameResult.game)
            {
                /*case 1:
                    for (int i = 0; i < hiscoreTable.Count; i++) //for each entry in TGM1
                    {
                        if (hiscoreTable[i].grade < gameResult.grade)
                        {
                            saveHiscore(gameResult, gameResult.game, m, i);
                            return true;
                        }
                        if (hiscoreTable[i].grade == gameResult.grade)
                        {
                            //compare time
                            if (hiscoreTable[i].time > gameResult.time)
                            {
                                saveHiscore(gameResult, gameResult.game, m, i);
                                return true;
                            }
                        }
                        //else try the next one.
                    }
                    break;*/
                case 4:
                    for (int i = 0; i < hiscoreTable.Count; i++)
                    {
                        if (hiscoreTable[i].level < gameResult.level)
                        {
                            saveHiscore(gameResult, gameResult.game, m, i);
                            return true;
                        }
                        if (hiscoreTable[i].level == gameResult.level)
                        {
                            if (hiscoreTable[i].grade < gameResult.grade)
                            {
                                saveHiscore(gameResult, gameResult.game, m, i);
                                return true;
                            }
                            if (hiscoreTable[i].grade == gameResult.grade)
                            {
                                if (hiscoreTable[i].time > gameResult.time)
                                {
                                    saveHiscore(gameResult, gameResult.game, m, i);
                                    return true;
                                }
                            }
                        }

                    }
                    break;
                default:
                    for (int i = 0; i < hiscoreTable.Count; i++)
                    {
                        if (hiscoreTable[i].grade < gameResult.grade)
                        {
                            saveHiscore(gameResult, gameResult.game, m, i);
                            return true;
                        }
                        else if (hiscoreTable[i].grade == gameResult.grade)
                        {
                            //compare line
                            if (hiscoreTable[i].lineC < gameResult.lineC)
                            {
                                saveHiscore(gameResult, gameResult.game, m, i);
                                return true;
                            }
                            //compare time
                            if (hiscoreTable[i].time > gameResult.time)
                            {
                                saveHiscore(gameResult, gameResult.game, m, i);
                                return true;
                            }
                            //compare level
                            if (hiscoreTable[i].level < gameResult.level)
                            {
                                saveHiscore(gameResult, gameResult.game, m, i);
                                return true;
                            }
                        }
                    }
                    break;
            }
            return false;
        }

        private void writeReplay()
        {
            var tim = System.DateTime.Now.ToString("G", new CultureInfo("ja-JP"));
            tim = tim.Replace("/", "_").Replace(":", "_");
            string repFile = "Sav/"+tim+" "+rules.GameName+" "+rules.mod.ModeName+".rep";
            using (FileStream fsStream = new FileStream(repFile, FileMode.OpenOrCreate))
            using (BinaryWriter sw = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                sw.Write(player.name);
                sw.Write((byte)1);
                sw.Write(pad1.replay.Count);
                sw.Write((int)rules.gameRules);
                sw.Write((int)(rules.mod.modeID) + (rules.mod.variant << 13));
                sw.Write(field1.seed);
                for (int i = 0; i < pad1.replay.Count; i++)
                    sw.Write(pad1.replay[i]);
            }
            field1.record = false;
            field1.recorded = true;
        }

        private bool queryReplay()
        {
            OpenFileDialog box = new OpenFileDialog();
            box.Filter = "replay files (*.rep)|*.rep";
            box.RestoreDirectory = true;

            if (box.ShowDialog() == DialogResult.OK)
            {
                fileName = box.FileName;
                return true;
            }
            else
                return false;
            
        }

        private void loadReplay()
        {
            FileStream fs = new FileStream(fileName, FileMode.Open);
            BinaryReader sr = new BinaryReader(fs);
            pad1.replay = new List<short>();
            repNam = sr.ReadString();
            byte ver = sr.ReadByte();
            int length = sr.ReadInt32();
            int gam = sr.ReadInt32();
            int t = sr.ReadInt32();
            int mod = t & 0x0FFF;
            int v = (t >> 13) & 0x000F;
            int s = sr.ReadInt32();
            for (int i = 0; i < length; i++)
                pad1.replay.Add(sr.ReadInt16());
            sr.Close();
            fs.Dispose();

            if (ver != 1)
                throw new Exception();
            rules = new GameRules();
            rules.setup((GameRules.Games)gam, (Mode.ModeType)mod, v);
            field1 = new Field(pad1, rules, s);
            Audio.stopMusic();
        }

        private void saveHiscore(GameResult gameResult, int g, int m, int place)
        {

            hiscoreTable.Insert(place, gameResult);
            hiscoreTable.RemoveAt(hiscoreTable.Count - 1);

            string hiFile = "Sav/gm" + g + m + ".hst";
            File.Delete(hiFile);
            using (FileStream fsStream = new FileStream(hiFile, FileMode.Create))
            using (BinaryWriter sw = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                for (int i = 0; i < hiscoreTable.Count; i++)
                {
                    sw.Write(hiscoreTable[i].username);
                    sw.Write(hiscoreTable[i].grade);
                    sw.Write(hiscoreTable[i].time);
                    for (int j = 0; j < 6; j++)
                        sw.Write((byte)(hiscoreTable[i].medals[j] & 0xFF));
                    sw.Write((byte)hiscoreTable[i].lineC);
                    sw.Write((int)hiscoreTable[i].level);
                }
            }
        }
        
        private void loadHiscores(int game, int mode)
        {
            string filename = "Sav/gm" + game.ToString() + mode.ToString() + ".hst";
            if (!File.Exists(filename))
            {
                if (game == 1 && mode == 1)
                    defaultTGMScores();
                else if (game == 2 && mode == 1)
                    defaultTGM2Scores();
                else if (game == 3 && mode == 1)
                    defaultTAPScores();
                else if (game == 4 && mode == 1)
                    defaultTGM3Scores();
                else
                    defaultGenericScores(game, mode);
            }
            hiscoreTable = new List<GameResult>();
            BinaryReader scores = new BinaryReader(File.OpenRead(filename));
            //otherwise, load up the hiscores into memory!
            
            while (true)
            {

                GameResult tempRes = new GameResult();

                tempRes.username = scores.ReadString();
                tempRes.grade = scores.ReadInt32();
                tempRes.time = scores.ReadInt64();
                tempRes.medals = new List<int>();
                for (int i = 0; i < 6; i++)
                {
                    tempRes.medals.Add((int)scores.ReadByte());
                }
                tempRes.lineC = scores.ReadByte();
                tempRes.level = scores.ReadInt32();
                hiscoreTable.Add(tempRes);
                if (scores.BaseStream.Position == scores.BaseStream.Length)
                    break;
            }
            scores.Close();
        }

        public bool defaultTGMScores()
        {
            using (FileStream fsStream = new FileStream("Sav/gm11.hst", FileMode.OpenOrCreate))
            using (BinaryWriter sw = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                long temptime;
                sw.Write("SAK");
                sw.Write(11);
                temptime = 540000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("CHI");
                sw.Write(10);
                temptime = 480000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("NAI");
                sw.Write(9);
                temptime = 420000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("MIZ");
                sw.Write(6);
                temptime = 360000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("KAR");
                sw.Write(5);
                temptime = 300000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("NAG");
                sw.Write(4);
                temptime = 240000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
            }
            return true;
        }
        public bool defaultTGM2Scores()
        {
            using (FileStream fsStream = new FileStream("Sav/gm21.hst", FileMode.Create))
            using (BinaryWriter sw = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                long temptime;
                sw.Write("T.A");
                sw.Write(9);
                temptime = 1200000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("T.A");
                sw.Write(6);
                temptime = 1080000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("T.A");
                sw.Write(3);
                temptime = 960000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("T.A");
                sw.Write(3);
                temptime = 1200000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("T.A");
                sw.Write(2);
                temptime = 1080000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("T.A");
                sw.Write(1);
                temptime = 960000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
            }
            return true;
        }

        public bool defaultTAPScores()
        {
            using (FileStream fsStream = new FileStream("Sav/gm31.hst", FileMode.Create))
            using (BinaryWriter sw = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                long temptime;
                sw.Write("T.A");
                sw.Write(9);
                temptime = 1200000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("T.A");
                sw.Write(6);
                temptime = 1080000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("T.A");
                sw.Write(3);
                temptime = 960000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("T.A");
                sw.Write(3);
                temptime = 1200000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("T.A");
                sw.Write(2);
                temptime = 1080000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
                sw.Write("T.A");
                sw.Write(1);
                temptime = 960000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(0);
            }
            return true;
        }
        public bool defaultTGM3Scores()
        {
            using (FileStream fsStream = new FileStream("Sav/gm41.hst", FileMode.Create))
            using (BinaryWriter sw = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                long temptime;
                sw.Write("ARK");
                sw.Write(6);
                temptime = 1200000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(500);
                sw.Write("ARK");
                sw.Write(4);
                temptime = 1080000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(400);
                sw.Write("ARK");
                sw.Write(3);
                temptime = 1200000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(400);
                sw.Write("ARK");
                sw.Write(2);
                temptime = 960000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(300);
                sw.Write("ARK");
                sw.Write(2);
                temptime = 1080000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(300);
                sw.Write("ARK");
                sw.Write(1);
                temptime = 960000;
                sw.Write(temptime);
                sw.Write(new byte[7]);
                sw.Write(200);
            }
            return true;
        }

        public bool defaultGenericScores(int game, int mode)
        {
            using (FileStream fsStream = new FileStream("Sav/gm" + game + mode + ".hst", FileMode.Create))
            using (BinaryWriter sw = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                for (int i = 0; i < 6; i++)
                {
                    sw.Write("NIL");
                    sw.Write(0);
                    sw.Write((long)0);
                    sw.Write(new byte[7]);
                    sw.Write(0);
                }
            }
            return true;
        }

        private void savePrefs()
        {
            using (FileStream fsStream = new FileStream("Sav/prefs.dat", FileMode.Create))
            using (BinaryWriter sw = new BinaryWriter(fsStream, Encoding.UTF8))
            {
                sw.Write(prefs.delay);
                sw.Write((char)prefs.nPad.keyUp);
                sw.Write((char)prefs.nPad.keyDown);
                sw.Write((char)prefs.nPad.keyLeft);
                sw.Write((char)prefs.nPad.keyRight);
                sw.Write((char)prefs.nPad.keyRot1);
                sw.Write((char)prefs.nPad.keyRot2);
                sw.Write((char)prefs.nPad.keyRot3);
                sw.Write((char)prefs.nPad.keyHold);
                sw.Write((char)prefs.nPad.keyStart);
                byte temp = (byte)(((Audio.musVol & 0xF) << 4) + (Audio.sfxVol & 0xF));
                sw.Write(temp);
                sw.Write(player.name);
            }
        }

        private void readPrefs()
        {
            BinaryReader prf = new BinaryReader(File.OpenRead("Sav/prefs.dat"));
            prefs.delay = prf.ReadBoolean();
            prefs.nPad.keyUp = (Key)prf.ReadByte();
            prefs.nPad.keyDown = (Key)prf.ReadByte();
            prefs.nPad.keyLeft = (Key)prf.ReadByte();
            prefs.nPad.keyRight = (Key)prf.ReadByte();
            prefs.nPad.keyRot1 = (Key)prf.ReadByte();
            prefs.nPad.keyRot2 = (Key)prf.ReadByte();
            prefs.nPad.keyRot3 = (Key)prf.ReadByte();
            prefs.nPad.keyHold = (Key)prf.ReadByte();
            prefs.nPad.keyStart = (Key)prf.ReadByte();
            byte temp = prf.ReadByte();
            Audio.musVol = (temp >> 4) & 0x0F;
            Audio.sfxVol = temp & 0x0F;
            player.name = prf.ReadString();
            prf.Close();
        }
    }
}
