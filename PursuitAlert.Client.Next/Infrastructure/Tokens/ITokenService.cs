﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Infrastructure.Tokens
{
    public interface ITokenService
    {
        Task<string> GetServiceAccountToken();
    }
}