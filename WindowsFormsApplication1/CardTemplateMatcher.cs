using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using AForge.Imaging;
using GXService.CardRecognize.Contract;
using Image = System.Drawing.Image;

namespace GXService.CardRecognize.Service
{
    public class CardTemplateMatcher : IRecognizer
    {
        private readonly string _templateBitmapDir = AppDomain.CurrentDomain.BaseDirectory + @"\Template";
        private readonly Dictionary<int, List<Bitmap>> _templateNumBitmaps = new Dictionary<int, List<Bitmap>>();
        private readonly Dictionary<int, List<Bitmap>> _templateColorBitmaps = new Dictionary<int, List<Bitmap>>();
        private readonly ExhaustiveTemplateMatching _templateMatching = new ExhaustiveTemplateMatching();
        
        public CardTemplateMatcher()
        {
            LoadTemplate();
        }

        public KeyValuePair<CardNum, double> ComputeNum(Bitmap bmpNum)
        {
            var result = new KeyValuePair<CardNum, double>(CardNum.未知, 0);

            //如果图片格式不是24位，则转换成8位图片
            if (bmpNum.PixelFormat != PixelFormat.Format8bppIndexed)
            {
                bmpNum = bmpNum.Clone(new Rectangle(0, 0, bmpNum.Width, bmpNum.Height), PixelFormat.Format8bppIndexed);
            }

            foreach (var bitmapTemplate in _templateNumBitmaps)
            {
                bitmapTemplate.Value.ForEach(tmpl =>
                {
                    var matches = _templateMatching.ProcessImage(bmpNum, tmpl);
                    if (matches.Length > 0 && result.Value < matches[0].Similarity)
                    {
                        result = new KeyValuePair<CardNum, double>((CardNum)bitmapTemplate.Key, matches[0].Similarity);
                    }
                });
            }

            return result;
        }

        public KeyValuePair<CardColor, double> ComputeColor(Bitmap bmpColor)
        {
            var result = new KeyValuePair<CardColor, double>(CardColor.未知, 0);
            
            //如果图片格式不是24位，则转换成24位图片
            if (bmpColor.PixelFormat != PixelFormat.Format24bppRgb)
            {
                bmpColor = bmpColor.Clone(new Rectangle(0, 0, bmpColor.Width, bmpColor.Height), PixelFormat.Format24bppRgb);
            }
            
            foreach (var bitmapTemplate in _templateColorBitmaps)
            {
                bitmapTemplate.Value.ForEach(tmpl =>
                {
                    var matches = _templateMatching.ProcessImage(bmpColor, tmpl);
                    if (matches.Length > 0 && result.Value < matches[0].Similarity)
                    {
                        result = new KeyValuePair<CardColor, double>((CardColor)bitmapTemplate.Key, matches[0].Similarity);
                    }
                });
            }
            return result;
        }

        public List<double> GetNumFeature(Bitmap bmpNum)
        {
            //模板匹配不需要获取特征点
            throw new InvalidOperationException("模板匹配不需要获取特征点");
        }

        private void LoadTemplate()
        {
            Directory.EnumerateDirectories(_templateBitmapDir + @"\大小").ToList().ForEach(dir =>
            {
                var key = Convert.ToInt32(dir.Substring(dir.LastIndexOf('\\') + 1));
                _templateNumBitmaps.Add(key, new List<Bitmap>());
                Directory.EnumerateFiles(dir).ToList().ForEach(file => _templateNumBitmaps[key].Add((Bitmap)Image.FromFile(file)));
            });

            Directory.EnumerateDirectories(_templateBitmapDir + @"\花色").ToList().ForEach(dir =>
            {
                var key = Convert.ToInt32(dir.Substring(dir.LastIndexOf('\\') + 1));
                _templateColorBitmaps.Add(key, new List<Bitmap>());
                Directory.EnumerateFiles(dir).ToList().ForEach(file => _templateColorBitmaps[key].Add((Bitmap)Image.FromFile(file)));
            });
        }
    }
}
