using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrameCloud
{
    [MessageType(MessageType.PasswordRequest)]
    public sealed class PasswordRequestMessage : Message
    {
        public override MessageType Type => MessageType.PasswordRequest;
    }

    [MessageType(MessageType.PasswordResponse)]
    public sealed class PasswordResponseMessage : Message
    {
        public override MessageType Type => MessageType.PasswordResponse;

        public string Password;
    }

    [MessageType(MessageType.ProjectHash)]
    public sealed class ProjectHashMessage : Message
    {
        public override MessageType Type => MessageType.ProjectHash;

        public ProjectHash Hash;
    }
}
