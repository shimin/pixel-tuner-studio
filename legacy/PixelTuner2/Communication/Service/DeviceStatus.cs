using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Communication.Service
{
    public enum ConnectionStatus
    {
        [DescriptionAttribute("Dev_Props_State_Uploading")]
        Uploading = 0,
        [DescriptionAttribute("Dev_Props_State_Downloading")]
        Downloading = 1,
        [DescriptionAttribute("Dev_Props_State_Idle")]
        Idle = 2,
        [DescriptionAttribute("Dev_Props_State_Online")]
        Online = 3,
        [DescriptionAttribute("Dev_Props_State_Offline")]
        Offline = 4,
        [DescriptionAttribute("Dev_Props_State_Error")]
        Error = 5,
        [DescriptionAttribute("Dev_Props_State_UploadError")]
        UploadError = 6,
    }
}
