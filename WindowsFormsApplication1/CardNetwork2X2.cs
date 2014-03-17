using System;
using AForge.Neuro;

namespace GXService.CardRecognize.Service
{
    public class CardNetwork2X2 : CardNetwork
    {
        public CardNetwork2X2()
            :base(Network.Load(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + "\\network2x2.data"), 2, 2)
        {}
    }
}
