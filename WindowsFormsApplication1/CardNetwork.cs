using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using AForge.Neuro;
using GXService.CardRecognize.Contract;

namespace GXService.CardRecognize.Service
{
    public class CardNetwork : IRecognizer
    {
        private readonly int _templateWidth = 2;
        private readonly int _templateHeight = 2;
        private readonly Network _network;

        protected CardNetwork(Network network, int templateWidth, int templateHeight)
        {
            _network = network;
            _templateWidth = templateWidth;
            _templateHeight = templateHeight;
        }

        public KeyValuePair<CardNum, double> ComputeNum(Bitmap bmpNum)
        {
            return GetCardNum(_network.Compute(GetNumFeature(bmpNum).ToArray()));
        }

        public KeyValuePair<CardColor, double> ComputeColor(Bitmap bmpColor)
        {
            var result = new KeyValuePair<CardColor, double>(CardColor.未知, 0);

            //花色特征点不明显，所以使用模板匹配方法进行识别

            return result;
        }

        public List<double> GetNumFeature(Bitmap bmpNum)
        {
            var result = new List<double>();
            var wMax = bmpNum.Width / _templateWidth;
            var hMax = bmpNum.Height / _templateHeight;
            var percentBase = Math.Pow(10, (_templateHeight*_templateWidth).ToString(CultureInfo.InvariantCulture).Length);

            //特征点提取
            var bmpData = bmpNum.LockBits(new Rectangle(0, 0, bmpNum.Width, bmpNum.Height), ImageLockMode.ReadOnly, bmpNum.PixelFormat);
            var bmpDataBuffer = new byte[bmpData.Stride * bmpData.Height];
            Marshal.Copy(bmpData.Scan0, bmpDataBuffer, 0, bmpDataBuffer.Length);
            for (var j = 1; j <= hMax; j++)
            {
                for (var i = 1; i <= wMax; i++)
                {
                    double count = 0;
                    for (var w = (i - 1) * _templateWidth; w < (i * _templateWidth - 1); w++)
                    {
                        for (var h = (j - 1) * _templateHeight; h < (j * _templateHeight - 1); h++)
                        {
                            if (bmpDataBuffer[h * bmpData.Stride + w] == 255)
                            {
                                count++;
                            }
                        }
                    }

                    result.Add(count / percentBase);
                }
            }
            bmpNum.UnlockBits(bmpData);

            return result;
        }

        protected KeyValuePair<CardNum, double> GetCardNum(double[] computeResult)
        {
            if (null == computeResult || computeResult.Length != 13)
            {
                return new KeyValuePair<CardNum, double>(CardNum.未知, 0);
            }

            var index = computeResult.Select(r => ((int)(r * 10)) / 10.0).ToList().FindIndex(r => r >= 0.9);
            if (-1 == index)
            {
                return new KeyValuePair<CardNum, double>(CardNum.未知, 0);
            }
            
            //索引顺序为:0,1,2,3,4,5,6,7,8,9,10,11,12
            //牌的大小顺序为:2,3,4,5,6,6,7,8,9,10,J,Q,K,A
            var num = (CardNum)(index == 0 ? 14 : index + 1);

            return new KeyValuePair<CardNum, double>(num, computeResult[index]);
        }
    }
}
