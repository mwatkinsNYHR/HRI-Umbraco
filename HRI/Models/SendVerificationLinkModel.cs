﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HRI.Models
{
    public class SendVerificationLinkModel
    {
        public string UserName { get; set; }
        public string RedirectUrl { get; set; }
    }
}