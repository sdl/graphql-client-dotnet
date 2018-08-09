﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sdl.Web.PublicContentApi.ContentModel
{
    /// <summary>
    /// Context Data
    /// </summary>
    public class ContextData : IContextData
    {
        /// <summary>
        /// List of claim values to pass to query.
        /// </summary>
        public List<ClaimValue> ClaimValues { get; set; } = new List<ClaimValue>();
    }  
}
