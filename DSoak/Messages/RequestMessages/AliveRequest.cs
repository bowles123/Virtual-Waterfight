﻿using System.Runtime.Serialization;

namespace Messages.RequestMessages
{
    [DataContract]
    public class AliveRequest : Request
    {
        static AliveRequest() { Register(typeof (AliveRequest)); }
    }
}
