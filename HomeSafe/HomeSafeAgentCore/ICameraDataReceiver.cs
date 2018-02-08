
namespace HomeSafeAgentCore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Drawing;

    public interface ICameraDataReceiver
    {
        void OnCameraDataReady(Image image);
    }
}
