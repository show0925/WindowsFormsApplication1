using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using GXService.CardRecognize.Contract;
using GXService.Utils;

namespace GXService.CardRecognize.Service
{
    public class CardRecognizeService : ICardsRecognizer
    {
        private readonly List<CardTypeRecognizer> _recognizers = new List<CardTypeRecognizer>
            {
                new StraightFlushCardTypeRecognizer(),
                new BoomCardTypeRecognizer(),
                new GourdCardTypeRecognizer(),
                new FlushCardTypeRecognizer(),
                new StraightCardTypeRecognizer(),
                new ThreeSameCardTypeRecognizer(),
                new TwoDoubleCardTypeRecognizer(),
                new DoubleCardTypeRecognizer(),
                new OnePieceCardTypeRecognizer()
            };

        //灰度化并二值化
        private readonly FiltersSequence _seq = new FiltersSequence
            {
                //过滤蓝色
                new ColorFiltering(new IntRange(0, 100), new IntRange(0, 100), new IntRange(130, 255))
                {
                    FillColor = new RGB(Color.White),
                    FillOutsideRange = false
                },
                //灰度化
                new Grayscale(0.2125, 0.7154, 0.0721),
                //二值化
                new Threshold(200)
            };

        //过滤噪音，最小宽度1、最小高度12、最大宽度14、最大高度16
        private readonly BlobsFiltering _blobsFiltering = new BlobsFiltering(1, 12, 14, 16);

        //块记录器
        private readonly BlobCounter _blobCounter = new BlobCounter();

        //最大块查找器
        private readonly ExtractBiggestBlob _extractBiggestBlob = new ExtractBiggestBlob();

        //图片数字大小归一化
        private readonly ResizeBilinear _resizeNumFilter = new ResizeBilinear(12, 16);

        //图片花色大小归一化
        private readonly ResizeBilinear _resizeColorFilter = new ResizeBilinear(10, 10);

        //模板匹配对象
        private readonly ExhaustiveTemplateMatching _tm = new ExhaustiveTemplateMatching();

        //扑克牌神经网络服务对象
        private readonly CardNetworkService _cardNetworkService = new CardNetworkService();
        private int index = 0;
        public void Start()
        {
        }

        public void Stop()
        {
        }

        public Rectangle Match(byte[] captureBmpData, byte[] tmplBmpData, float similarityThreshold)
        {
            _tm.SimilarityThreshold = similarityThreshold;
            var captureBmp = captureBmpData.Deserialize() as Bitmap;
            var tmplBmp = tmplBmpData.Deserialize() as Bitmap;
            if (captureBmp == null || tmplBmp == null)
            {
                throw new Exception("图片反序列化失败");
            }

            if (captureBmp.PixelFormat != tmplBmp.PixelFormat)
            {
                captureBmp = captureBmp.Clone(new Rectangle(0, 0, captureBmp.Width, captureBmp.Height),
                                              tmplBmp.PixelFormat);
            }

            var matches = _tm.ProcessImage(captureBmp, tmplBmp);
            if (matches == null || !matches.Any())
            {
                throw new Exception("无匹配图案");
            }
            return matches.OrderBy(m => m.Similarity).Last().Rectangle;
        }

        public RecognizeResult Recognize(RecoginizeData data)
        {
            using (var src = data.CardsBitmap.Deserialize() as Bitmap)
            {
                if (src == null)
                {
                    throw new InvalidDataException(string.Format("待识别扑克的图片数据反序列化失败！"));
                }
                var result = new RecognizeResult
                {
                    Result = new List<Card>()
                };
                src.Save(AppDomain.CurrentDomain.BaseDirectory + string.Format("Color\\{0}.bmp", index++));

                //过滤蓝色、灰度化、二值化
                var toRec = _seq.Apply(src);
                toRec.Save(AppDomain.CurrentDomain.BaseDirectory + string.Format("Color\\{0}.bmp", index++));

                //黑色与白色互换
                ExchangeIndexColor(toRec, 255, 0);
                toRec.Save(AppDomain.CurrentDomain.BaseDirectory + string.Format("Color\\{0}.bmp", index++));

                //去掉干扰线
                RemoveInterferenceLines(toRec, 0);
                toRec.Save(AppDomain.CurrentDomain.BaseDirectory + string.Format("Color\\{0}.bmp", index++));

                //过滤噪音
                toRec = _blobsFiltering.Apply(toRec);
                toRec.Save(AppDomain.CurrentDomain.BaseDirectory + string.Format("Color\\{0}.bmp", index++));

                //获取所有的块
                _blobCounter.ProcessImage(toRec);

                //去掉偏移的块区域
                var rects = RemoveOffsetBlobs(_blobCounter.GetObjectsRectangles().ToList());

                //合并区域
                rects = MergeBlobs(rects);

                //识别扑克牌数字
                rects.ForEach(rectNum =>
                {
                    //识别牌数字
                    var bmpNum = _resizeNumFilter.Apply(toRec.Clone(rectNum, toRec.PixelFormat));
                    var cardNum = _cardNetworkService.ComputeNum(bmpNum);
                    if (cardNum == CardNum.未知)
                    {
                        var dir = AppDomain.CurrentDomain.BaseDirectory + @"未知\数字\";
                        Directory.CreateDirectory(dir);
                        bmpNum.Save(dir + string.Format("{0}.bmp", index++));
                    }

                    //识别牌花色(花色图片需要重新处理，所以需要用原图src)
                    var bmpColor = GetColorBitmap(src, rectNum);
                    //bmpColor.Save(".\\Color\\" + string.Format("{0}.bmp", index++));
                    var cardColor = _cardNetworkService.ComputeColor(bmpColor);
                    if (cardColor == CardColor.未知)
                    {
                        var dir = AppDomain.CurrentDomain.BaseDirectory + @"未知\花色\";
                        Directory.CreateDirectory(dir);
                        bmpColor.Save(dir + string.Format("{0}.bmp", index++));
                    }

                    //将识别出来的牌信息添加到结果集中
                    result.Result.Add(new Card
                    {
                        Num = cardNum,
                        Color = cardColor,
                        Rect = rectNum
                    });
                });

                GC.Collect();

                return result;
            }
        }

        /// <summary>
        /// 去掉干扰线
        /// </summary>
        /// <param name="src"></param>
        /// <param name="bgColorIndex"></param>
        private void RemoveInterferenceLines(Bitmap src, byte bgColorIndex)
        {
            var bmpData = src.LockBits(new Rectangle(0, 0, src.Width, src.Height), ImageLockMode.ReadWrite, src.PixelFormat);
            var bmpDataBuffer = new byte[bmpData.Stride * bmpData.Height];
            Marshal.Copy(bmpData.Scan0, bmpDataBuffer, 0, bmpDataBuffer.Length);
            for (var w = 0; w < bmpData.Width; w++)
            {
                var count = 0;
                for (var h = 0; h < bmpData.Height; h++)
                {
                    if (bmpDataBuffer[h * bmpData.Stride + w] != bgColorIndex)
                    {
                        count++;
                    }
                }

                if (count > src.Height * 4 / 5)
                {
                    for (var h = 0; h < src.Height; h++)
                    {
                        bmpDataBuffer[h * bmpData.Stride + w] = bgColorIndex;
                    }
                }

            }
            Marshal.Copy(bmpDataBuffer, 0, bmpData.Scan0, bmpDataBuffer.Length);
            src.UnlockBits(bmpData);
        }

        /// <summary>
        /// 去掉偏移大于3的所有块区域
        /// </summary>
        /// <param name="rects"></param>
        /// <returns></returns>
        private List<Rectangle> RemoveOffsetBlobs(List<Rectangle> rects)
        {
            var topDirectory = new Dictionary<int/*随机的一个top高度*/, List<int>/*偏移小于3的所有top高度*/>();
            rects.ForEach(rt =>
            {
                foreach (var key in topDirectory.Keys)
                {
                    if (Math.Abs(rt.Top - key) < 3)
                    {
                        topDirectory[key].Add(rt.Top);
                        return;
                    }
                }

                topDirectory.Add(rt.Top, new List<int> { rt.Top });
            });

            //找出值最多的集合，取此集合的平均高度为整体的平均高度
            var avKey = topDirectory.Keys.First();
            foreach (var dic in topDirectory.Where(dic => dic.Value.Count > topDirectory[avKey].Count))
            {
                avKey = dic.Key;
            }

            var avTop = topDirectory[avKey].Average();

            rects = rects.Where(r => Math.Abs(avTop - r.Top) < 3).ToList();
            rects.Sort(new RectangleLeftComparer());

            return rects;
        }

        /// <summary>
        /// 合并间隔小于4的块区域
        /// </summary>
        /// <param name="rects"></param>
        /// <returns></returns>
        private List<Rectangle> MergeBlobs(List<Rectangle> rects)
        {
            if (rects.Count <= 0)
            {
                return new List<Rectangle>();
            }

            //处理10特殊情况，因为10分两部分，需要合并区域
            var tmpRects = new List<Rectangle> { rects[0] };
            var rectIndex = rects[0];
            rects.ForEach(r =>
            {
                if (r != rectIndex)
                {
                    //如果两个区域间隔小于4，则说明是10，需要合并区域
                    if ((r.Left - rectIndex.Right) < 4)
                    {
                        tmpRects.Remove(rectIndex);
                        tmpRects.Add(new Rectangle(rectIndex.X,
                            rectIndex.Y > r.Y ? r.Y : rectIndex.Y,
                            r.Right - rectIndex.Left,
                            rectIndex.Height > r.Height ? rectIndex.Height : r.Height));
                    }
                    else
                    {
                        tmpRects.Add(r);
                    }
                }

                rectIndex = r;
            });

            return tmpRects;
        }

        /// <summary>
        /// 交换索引颜色
        /// </summary>
        /// <param name="src"></param>
        /// <param name="colorIndex1"></param>
        /// <param name="colorIndex2"></param>
        private void ExchangeIndexColor(Bitmap src, byte colorIndex1, byte colorIndex2)
        {
            var bmpData = src.LockBits(new Rectangle(0, 0, src.Width, src.Height), ImageLockMode.ReadWrite, src.PixelFormat);
            var bmpDataBuffer = new byte[bmpData.Stride * bmpData.Height];
            Marshal.Copy(bmpData.Scan0, bmpDataBuffer, 0, bmpDataBuffer.Length);
            for (var w = 0; w < bmpData.Width; w++)
            {
                for (var h = 0; h < bmpData.Height; h++)
                {
                    if (bmpDataBuffer[h * bmpData.Stride + w] == colorIndex1)
                    {
                        bmpDataBuffer[h * bmpData.Stride + w] = colorIndex2;
                    }
                    else if (bmpDataBuffer[h * bmpData.Stride + w] == colorIndex2)
                    {
                        bmpDataBuffer[h * bmpData.Stride + w] = colorIndex1;
                    }
                }
            }
            Marshal.Copy(bmpDataBuffer, 0, bmpData.Scan0, bmpDataBuffer.Length);
            src.UnlockBits(bmpData);
        }

        /// <summary>
        /// 根据数字图片区域获取花色图片
        /// </summary>
        /// <param name="src"></param>
        /// <param name="rectNum"></param>
        /// <returns></returns>
        private Bitmap GetColorBitmap(Bitmap src, Rectangle rectNum)
        {
            var colorRect = new Rectangle(rectNum.X, rectNum.Y + rectNum.Height, rectNum.Width, src.Height - rectNum.Height - rectNum.Y);

            //处理花色图片
            var bmpColor = _seq.Apply(src.Clone(colorRect, src.PixelFormat));

            //反色
            ExchangeIndexColor(bmpColor, 0, 255);

            //返回最大块的花色块图片
            var maxBlobBmp = _extractBiggestBlob.Apply(bmpColor);

            //需要返回24位的图片，所以需要拷贝原图
            colorRect = new Rectangle(colorRect.X + _extractBiggestBlob.BlobPosition.X, colorRect.Y + _extractBiggestBlob.BlobPosition.Y, maxBlobBmp.Width, maxBlobBmp.Height);
            return _resizeColorFilter.Apply(src.Clone(colorRect, PixelFormat.Format24bppRgb));
        }
        
        /// <summary>
        /// 根据当前牌解析出最优牌型
        /// </summary>
        /// <param name="cards"></param>
        /// <returns></returns>
        public CardTypeResult ParseCardType(List<Card> cards)
        {
            return GetBestResult(ParseCardTypeResult(cards));
        }

        /// <summary>
        /// 根据敌方牌解析出最优牌型
        /// </summary>
        /// <param name="cards"></param>
        /// <param name="cardsEnemy"></param>
        /// <returns></returns>
        public CardTypeResult ParseCardTypeVsEnemy(List<Card> cards, List<Card> cardsEnemy)
        {
            return GetBestResult(ParseCardType(cards), ParseCardTypeResult(cards));
        }

        private List<CardTypeResult> ParseCardTypeResult(IEnumerable<Card> cards)
        {
            var result = new List<CardTypeResult>();
            var resultTmp = new List<CardType>();
            var tmp = cards.ToList();

            _recognizers.Where(rec => !(rec is OnePieceCardTypeRecognizer))
                        .ToList()
                        .ForEach(rec => resultTmp.AddRange(rec.Recognize(tmp)));

            resultTmp.ForEach(bodyType =>
                {
                    var tmpCards = tmp.FindAll(card => !bodyType.GetCards().Contains(card)).ToList();
                    _recognizers
                        .ForEach(rec =>
                                 rec.Recognize(tmpCards)
                                    .ForEach(tailType =>
                                        {
                                            var headType = HeadCardTypeFactory.GetSingleton()
                                                                              .GetHeadCardType(tmp.FindAll(
                                                                                  card =>
                                                                                  !bodyType.GetCards().Contains(card) &&
                                                                                  !tailType.GetCards().Contains(card))
                                                                                                  .ToList());
                                            if (tailType.Compare(bodyType, EmRegionCompare.Tail) >= 0 && bodyType.CompareTypeRule(headType) >= 0)
                                            {
                                                result.Add(new CardTypeResult(headType, bodyType, tailType));   
                                            }
                                        }));
                });

            return result;
        }

        private static CardTypeResult GetBestResult(List<CardTypeResult> results)
        {
            CardTypeResult best = null;
            results.ForEach(ctr =>
            {
                best = best == null
                           ? ctr
                           : (best.Compare(ctr) >= 0
                                  ? best
                                  : ctr);
            });
            return best;
        }

        private static CardTypeResult GetBestResult(CardTypeResult bestResult, List<CardTypeResult> resultsEnemy)
        {
            resultsEnemy.ForEach(res =>
            {
                bestResult = bestResult.Compare(res) >= 0
                                 ? bestResult
                                 : res;
            });
            return bestResult;
        }
    }
}
