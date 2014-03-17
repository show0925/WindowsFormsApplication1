using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using GXService.CardRecognize.Contract;

namespace GXService.CardRecognize.Service
{
    public class CardNetworkService
    {
        private readonly IRecognizer _3X4Recognizer = new CardNetwork3X4();
        private readonly IRecognizer _2X2Recognizer = new CardNetwork2X2();
        private readonly IRecognizer _templateMatchRecognizer = new CardTemplateMatcher();

        public CardNum ComputeNum(Bitmap bmpNum)
        {
            var result3X4 = new KeyValuePair<CardNum, double>();
            var result2X2 = new KeyValuePair<CardNum, double>();
            var resultTemplateMatch = new KeyValuePair<CardNum, double>();

            var bmpNum3X4 = bmpNum.Clone(new Rectangle(0, 0, bmpNum.Width, bmpNum.Height), bmpNum.PixelFormat);
            var bmpNum2X2 = bmpNum.Clone(new Rectangle(0, 0, bmpNum.Width, bmpNum.Height), bmpNum.PixelFormat);
            var bmpNumTemplate = bmpNum.Clone(new Rectangle(0, 0, bmpNum.Width, bmpNum.Height), bmpNum.PixelFormat);

            var t1 = Task.Factory.StartNew(() => result3X4 = _3X4Recognizer.ComputeNum(bmpNum3X4));
            var t2 = Task.Factory.StartNew(() => result2X2 = _2X2Recognizer.ComputeNum(bmpNum2X2));
            var t3 = Task.Factory.StartNew(() => resultTemplateMatch = _templateMatchRecognizer.ComputeNum(bmpNumTemplate));
            Task.WaitAll(t1, t2, t3);

            var result = new Dictionary<CardNum, int>();
            if (result2X2.Key != CardNum.未知)
            {
                result.Add(result2X2.Key, 1);
            }
            if (result3X4.Key != CardNum.未知)
            {
                if (result.ContainsKey(result3X4.Key))
                {
                    result[result3X4.Key]++;
                }
                else
                {
                    result.Add(result3X4.Key, 1);
                }
            }
            if (resultTemplateMatch.Key != CardNum.未知)
            {
                if (result.ContainsKey(resultTemplateMatch.Key))
                {
                    result[resultTemplateMatch.Key]++;
                }
                else
                {
                    result.Add(resultTemplateMatch.Key, 1);
                }
            }

            //三种中有两种相同则返回结果
            foreach (var item in result.Where(item => item.Value > 1))
            {
                return item.Key;
            }

            //三种都不同，则以模板匹配的结果，其次2x2的结果
            return resultTemplateMatch.Key != CardNum.未知 ? resultTemplateMatch.Key : result2X2.Key;
        }

        public CardColor ComputeColor(Bitmap bmpColor)
        {
            //以模板匹配的结果为准
            return _templateMatchRecognizer.ComputeColor(bmpColor).Key;
        }
    }
}
