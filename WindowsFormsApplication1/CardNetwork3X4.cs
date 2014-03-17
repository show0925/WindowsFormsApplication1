using System;
using AForge.Neuro;

namespace GXService.CardRecognize.Service
{
    public class CardNetwork3X4 : CardNetwork
    {
        public CardNetwork3X4()
            :base(Network.Load(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + "\\network3x4.data"), 3, 4)
        {}
    }
}
