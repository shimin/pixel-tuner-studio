/*****************************************************************
 * Copyright (C) Knights Warrior Corporation. All rights reserved.
 * 
 * Author:   圣殿骑士（Knights Warrior） 
 * Email:    PixelTuner@msn.com
 * Website:  http://www.cnblogs.com/PixelTuner/       http://knightswarrior.blog.51cto.com/
 * Create Date:  5/8/2010 
 * Usage:
 *
 * RevisionHistory
 * Date         Author               Description
 * 
*****************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixelTunerAutoupdater
{
    public class ConstFile
    {
        public const string TEMPFOLDERNAME = "TempFolder";
        public const string CONFIGFILEKEY = "config_";
        public const string FILENAME = "AutoUpdater.config";
        public const string ROOLBACKFILE = "PixelTuner.exe";
        public const string MESSAGETITLE = "AutoUpdate Program";
        public const string CANCELORNOT = "PixelTuner Update is in progress. Do you really want to cancel?";
        public const string APPLYTHEUPDATE = "Program need to restart to apply the update,Please click OK to restart the program!";
        public const string NOTNETWORK = "PixelTuner.exe update is unsuccessful. PixelTuner.exe will now restart. Please try to update again.";
    }
}
