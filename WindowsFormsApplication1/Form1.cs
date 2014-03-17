using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GXService.CardRecognize.Service;
using GXService.Utils;
using GXService.CardRecognize.Contract;

namespace GXService.CardRecognize.Client
{
    public partial class Form1 : Form
    {
        private readonly CardRecognizeService _proxyRecognize = new CardRecognizeService();

        //头墩、中墩、尾墩牌框中心点
        private readonly Point _headCenterPoint = new Point(500, 270);
        private readonly Point _bodyCenterPoint = new Point(480, 370);
        private readonly Point _tailCenterPoint = new Point(440, 460);

        //十三张手牌区域
        private readonly Rectangle _rectSsz = new Rectangle(300, 540, 450, 50);

        //其他友方牌型分析结果
        private readonly List<CardTypeResult> _friendCardTypeResults = new List<CardTypeResult>();

        private int index = 0;

        private Bitmap bmpSave;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                //查找游戏牌局窗口并抓取图片进行识别分析
                var wndGame = "thirtn".FindWindow();
                //var bmp = wndGame.Capture();

                var bmp = Image.FromFile(@"0.bmp") as Bitmap;
                if (null == bmp)
                {
                    return;
                }

                //var fileName = string.Format("{0}.bmp", index++);
                //while (!File.Exists(fileName))
                //{
                //    fileName = string.Format("{0}.bmp", index++);
                //}

                //bmp.Save(fileName);
                //return;
                var result = _proxyRecognize.Recognize(new RecoginizeData
                {
                    CardsBitmap = bmp.Clone(_rectSsz, bmp.PixelFormat).Serialize()
                });

                string text = "";
                result.Result.ToList().ForEach(card => text += "(" + card.Num + "," + card.Color + ")");

                var resultParseType = _proxyRecognize.ParseCardType(result.Result);

                //_proxyBroadcast.Broadcast(resultParseType.Serialize());
                //return;
                resultParseType.CardTypeHead
                    .Cards
                    .ToList()
                    .ForEach(card =>
                    {
                        var tmpRect = new Rectangle(new Point(card.Rect.X + _rectSsz.X, card.Rect.Y + _rectSsz.Y),
                            card.Rect.Size);

                        //将牌选出来
                        tmpRect.Center().MouseLClick(wndGame);
                    });
                //所有头墩牌被点出，需要点击头墩框，将牌放到头墩框中
                _headCenterPoint.MouseLClick(wndGame);

                resultParseType.CardTypeMiddle
                    .Cards
                    .ToList()
                    .ForEach(card =>
                    {
                        var tmpRect = new Rectangle(new Point(card.Rect.X + _rectSsz.X, card.Rect.Y + _rectSsz.Y),
                            card.Rect.Size);

                        //将牌选出来
                        tmpRect.Center().MouseLClick(wndGame);
                    });
                //所有中墩牌被点出，需要点击中墩框，将牌放到中墩框中
                _bodyCenterPoint.MouseLClick(wndGame);

                resultParseType.CardTypeTail
                    .Cards
                    .ToList()
                    .ForEach(card =>
                    {
                        var tmpRect = new Rectangle(new Point(card.Rect.X + _rectSsz.X, card.Rect.Y + _rectSsz.Y),
                            card.Rect.Size);

                        //将牌选出来
                        tmpRect.Center().MouseLClick(wndGame);
                    });
                //所有尾墩牌被点出，需要点击尾墩框，将牌放到尾墩框中
                _tailCenterPoint.MouseLClick(wndGame);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException == null ? ex.ToString() : ex.InnerException.ToString());
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var fileName = string.Format("{0}.bmp", index++);
            while (File.Exists(fileName))
            {
                fileName = string.Format("{0}.bmp", index++);
            }

            bmpSave.Save(fileName);
        }
    }

    public class CardsTest
    {
        private readonly List<Card> _cards = new List<Card>();

        public CardsTest()
        {
            InitCards();
        }

        private void InitCards()
        {
            var nums = new[]
                {
                    CardNum._A, CardNum._2, CardNum._3, CardNum._4,
                    CardNum._5, CardNum._6, CardNum._7, CardNum._8,
                    CardNum._9, CardNum._10, CardNum._J, CardNum._Q,
                    CardNum._K
                };

            var colors = new[]
                {
                    CardColor.黑桃, CardColor.红桃, CardColor.梅花, CardColor.方块
                };

            colors.ToList()
                  .ForEach(color =>
                           nums.ToList()
                               .ForEach(num =>
                                        _cards.Add(new Card { Num = num, Color = color })));
        }

        public List<Card> GetNextPlayerCards(Int32 count)
        {
            var result = new List<Card>();

            if (count > _cards.Count)
            {
                return _cards.ToList();
            }

            var rnd = new Random();
            for (var i = 0; i < count; i++)
            {
                var index = rnd.Next(0, _cards.Count);
                //index = i + 13 * (i / 4);
                result.Add(_cards[index]);
                _cards.RemoveAt(index);
            }

            return result.OrderBy(card => card.Num).ThenBy(card => card.Color).ToList();
        }
    }

    class CardTypeResultComparer : IEqualityComparer<CardTypeResult>
    {
        private readonly CardTypeComparer _comparer = new CardTypeComparer();

        public bool Equals(CardTypeResult x, CardTypeResult y)
        {
            //Check whether the compared objects reference the same data.
            if (ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                return false;

            //Check whether the products' properties are equal.
            return _comparer.Equals(x.CardTypeHead, y.CardTypeHead) 
                && _comparer.Equals(x.CardTypeMiddle, y.CardTypeMiddle) 
                && _comparer.Equals(x.CardTypeTail, y.CardTypeTail);
        }

        public int GetHashCode(CardTypeResult c)
        {
            //Check whether the object is null
            if (ReferenceEquals(c, null)) return 0;

            //Calculate the hash code for the product.
            return _comparer.GetHashCode(c.CardTypeHead) ^
                   _comparer.GetHashCode(c.CardTypeMiddle) ^
                   _comparer.GetHashCode(c.CardTypeTail);
        }
    }

    class CardTypeComparer : IEqualityComparer<CardType>
    {
        private readonly ListValueComparer<Card> _cardsValueComparer = new ListValueComparer<Card>(new CardNoRectComparer()); 

        public bool Equals(CardType x, CardType y)
        {
            //Check whether the compared objects reference the same data.
            if (ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                return false;

            //Check whether the products' properties are equal.
            return _cardsValueComparer.Equals(x.Cards.ToList(), y.Cards.ToList());
        }

        public int GetHashCode(CardType c)
        {
            return _cardsValueComparer.GetHashCode(c.Cards.ToList());
        }
    }

    class CardFullComparer : IEqualityComparer<Card>
    {
        public bool Equals(Card x, Card y)
        {
            //Check whether the compared objects reference the same data.
            if (ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                return false;

            //Check whether the products' properties are equal.
            return x.Num == y.Num && x.Color == y.Color && x.Rect == y.Rect;
        }

        public int GetHashCode(Card c)
        {
            //Check whether the object is null
            if (ReferenceEquals(c, null)) return 0;

            //Calculate the hash code for the product.
            return c.Num.GetHashCode() ^ c.Color.GetHashCode() ^ c.Rect.GetHashCode();
        }
    }

    class CardNoRectComparer : IEqualityComparer<Card>
    {
        public bool Equals(Card x, Card y)
        {
            //Check whether the compared objects reference the same data.
            if (ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                return false;

            //Check whether the products' properties are equal.
            return x.Num == y.Num && x.Color == y.Color;
        }

        public int GetHashCode(Card c)
        {
            //Check whether the object is null
            if (ReferenceEquals(c, null)) return 0;

            //Calculate the hash code for the product.
            return c.Num.GetHashCode() ^ c.Color.GetHashCode();
        }
    }
}
