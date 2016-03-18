﻿using System.Runtime.Serialization;
using SharedObjects;

namespace Messages.ReplyMessages
{
    [DataContract]
    public class BidAck : Reply
    {
        static BidAck() { Register(typeof(BidAck)); }

        [DataMember]
        public bool Won { get; set; }
        [DataMember]
        public Umbrella Umbrella { get; set; }
    }
}