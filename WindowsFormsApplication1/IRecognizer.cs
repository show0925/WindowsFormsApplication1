using System.Collections.Generic;
using System.Drawing;

namespace GXService.CardRecognize.Contract
{
    public interface IRecognizer
    {
        KeyValuePair<CardNum, double> ComputeNum(Bitmap bmpNum);

        KeyValuePair<CardColor, double> ComputeColor(Bitmap bmpColor);

        List<double> GetNumFeature(Bitmap bmpNum);
    }
}
