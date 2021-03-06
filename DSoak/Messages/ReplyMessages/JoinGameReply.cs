﻿using System.Runtime.Serialization;

namespace Messages.ReplyMessages
{
    [DataContract]
    public class JoinGameReply : Reply
    {
        [DataMember]
        public int GameId { get; set; }
        [DataMember]
        public int InitialLifePoints { get; set; }
    }
}
